using UnityEngine;

[System.Serializable]
public class DynamicWheelScanner
{
    public bool showGizmos = true, showLine = true;

    [Header("General Setting")]
    [Range(0, 3)][SerializeField] float wheelOffset; //It can be used as a spacer if the need arises.
    [Range(1, 15)][SerializeField] int rayCount;
    [Range(0, 360)][SerializeField] int scanRange = 100, scanCenterAngle = 90;
    [SerializeField] LayerMask scanLayer;

    [Header("Manual Slice Configuration")]
    [SerializeField]
    SliceSettings centerSlice = new SliceSettings
    {
        boxSize = new Vector3(0.15f, 0.1f, 0f),
        castDistance = 0.66f
    };
    [SerializeField]
    SliceSettings[] sideSlicePairs = new SliceSettings[]
    {
        new SliceSettings
        {
            offset = 0.15f,
            boxSize = new Vector3(0.15f, 0.1f, 0f),
            castDistance = 0.65f,
            rayAngle = 0.5f,
            boxAngle = 0f
        }
    };

    public WheelContactResult wheelResult;

    [System.Serializable]
    private struct SliceSettings
    {
        public float offset;
        public Vector3 boxSize;
        public float boxAngle;
        public float rayAngle;
        public float castDistance;
    }
    private struct ScanSliceDefinition
    {
        public float offset;
        public Vector3 boxSize;
        public float boxAngle;
        public float rayAngle;
        public float maxDistance;
        public bool isSide;
    }
    private struct RawHit
    {
        public bool isHit;
        public Vector3 point;
        public Vector3 normal;
        public float penetrationDepth;
    }
    public struct WheelContactResult
    {
        public bool isGrounded;
        public Vector3 groundNormal;
        public float nearestHitDistance;
        public float maxPenetration;
        public PhysicsMaterial surfaceMaterial;
    }

    private RawHit[] _rawHits;
    private ScanSliceDefinition[] _slices;

    public void Initialize()
    {
        int calculatedSliceCount = 1 + (sideSlicePairs != null ? sideSlicePairs.Length * 2 : 0);
        int totalRays = rayCount * calculatedSliceCount;

        if (_rawHits == null || _rawHits.Length != totalRays)
        {
            _rawHits = new RawHit[totalRays];
        }
        if (_slices == null || _slices.Length != calculatedSliceCount)
        {
            _slices = new ScanSliceDefinition[calculatedSliceCount];
        }

        _slices[0] = new ScanSliceDefinition
        {
            offset = 0,
            rayAngle = centerSlice.rayAngle,
            boxAngle = centerSlice.boxAngle,
            maxDistance = centerSlice.castDistance,
            boxSize = centerSlice.boxSize,
            isSide = false
        };

        if (sideSlicePairs != null)
        {
            int sliceIndex = 1;
            for (int i = 0; i < sideSlicePairs.Length; i++)
            {
                SliceSettings settings = sideSlicePairs[i];
                _slices[sliceIndex++] = new ScanSliceDefinition
                {
                    offset = -settings.offset,
                    rayAngle = -settings.rayAngle,
                    boxAngle = -settings.boxAngle,
                    maxDistance = settings.castDistance,
                    boxSize = settings.boxSize,
                    isSide = true
                };
                _slices[sliceIndex++] = new ScanSliceDefinition
                {
                    offset = settings.offset,
                    rayAngle = settings.rayAngle,
                    boxAngle = settings.boxAngle,
                    maxDistance = settings.castDistance,
                    boxSize = settings.boxSize,
                    isSide = true
                };
            }
        }
    }

    public void PerformScan(Transform wheelTransform, Vector3 chassisUp)
    {
        if (_slices == null) Initialize();
        int totalRays = rayCount * _slices.Length;

        if (Application.isEditor || _rawHits == null || _rawHits.Length != totalRays) Initialize();

        int globalIndex = 0;

        wheelResult.isGrounded = false;
        wheelResult.nearestHitDistance = float.MaxValue;
        wheelResult.maxPenetration = 0f;
        wheelResult.surfaceMaterial = null;

        Vector3 wheelAxle = wheelTransform.right;
        Vector3 nonSpinningForward = Vector3.Cross(wheelAxle, chassisUp);
        Vector3 nonSpinningUp = Vector3.Cross(nonSpinningForward, wheelAxle);
        Quaternion hubRotation = (nonSpinningForward != Vector3.zero) ? Quaternion.LookRotation(nonSpinningForward, nonSpinningUp) : Quaternion.identity;

        for (int s = 0; s < _slices.Length; s++)
        {
            ScanSliceDefinition slice = _slices[s];

            for (int i = 0; i < rayCount; i++)
            {
                CalculateCastParameters(i, slice, wheelTransform, hubRotation, out Vector3 direction, out Quaternion worldRot, out Vector3 startPos);

                if (Physics.BoxCast(startPos, slice.boxSize / 2f, direction, out RaycastHit hit, worldRot, slice.maxDistance, scanLayer))
                {
                    Vector3 stablePoint = startPos + (direction.normalized * hit.distance);

                    _rawHits[globalIndex].isHit = true;
                    _rawHits[globalIndex].point = stablePoint;
                    _rawHits[globalIndex].normal = hit.normal;


                    float currentPenetration = slice.maxDistance - hit.distance;
                    _rawHits[globalIndex].penetrationDepth = currentPenetration;

                    wheelResult.isGrounded = true;
                    if (hit.distance < wheelResult.nearestHitDistance)
                    {
                        wheelResult.nearestHitDistance = hit.distance; //By adding an offset to this result, you can position the wheel and calculate the suspension force. 
                                                                       // Blending it with (maxPenetration) can produce a more realistic result.
                    }

                    if (currentPenetration > wheelResult.maxPenetration)
                    {
                        wheelResult.maxPenetration = currentPenetration;
                        wheelResult.surfaceMaterial = hit.collider.sharedMaterial;
                    }
                }
                else
                {
                    _rawHits[globalIndex].isHit = false;
                    _rawHits[globalIndex].normal = Vector3.zero;
                    _rawHits[globalIndex].penetrationDepth = 0f;
                }
                globalIndex++;
            }
        }
    }

    private void CalculateCastParameters(int index, ScanSliceDefinition slice, Transform wheelTransform, Quaternion hubRotation, out Vector3 direction, out Quaternion worldRot, out Vector3 startPos)
    {
        float finalAngle;
        if (rayCount <= 1) finalAngle = scanCenterAngle;
        else
        {
            float halfAngle = scanRange / 2;
            float angleStep = (scanRange == 360) ? ((float)scanRange / rayCount) : ((float)scanRange / (rayCount - 1));
            finalAngle = (scanCenterAngle - halfAngle) + (index * angleStep);
        }

        Quaternion pitchRot = Quaternion.Euler(finalAngle, 0, 0);
        Quaternion rayYawRot = Quaternion.Euler(0, slice.rayAngle, 0);
        Quaternion boxYawRot = Quaternion.Euler(0, slice.boxAngle, 0);

        Quaternion localRayRot = pitchRot * rayYawRot;
        direction = hubRotation * (localRayRot * Vector3.forward);

        Quaternion localBoxRot = pitchRot * boxYawRot;
        worldRot = hubRotation * localBoxRot;

        Vector3 wheelAxle = wheelTransform.right;
        Vector3 totalOffset = wheelAxle * (wheelOffset + slice.offset);
        startPos = wheelTransform.position + totalOffset - direction.normalized * (slice.boxSize.z / 2f);
    }

    public Vector3 GetAverageNormal(float lerpSpeed = 15f)
    {
        Vector3 targetWeightedNormal = Vector3.zero;

        for (int i = 0; i < _rawHits.Length; i++)
        {
            if (_rawHits[i].isHit)
            {
                targetWeightedNormal += _rawHits[i].normal * _rawHits[i].penetrationDepth;
            }
        }

        Vector3 targetDir = targetWeightedNormal == Vector3.zero ? Vector3.up : targetWeightedNormal.normalized;

        float dt = Time.deltaTime;
        if (Time.inFixedTimeStep) dt = Time.fixedDeltaTime;

        Vector3 currentNormal = wheelResult.groundNormal == Vector3.zero ? Vector3.up : wheelResult.groundNormal;
        wheelResult.groundNormal = Vector3.Lerp(currentNormal, targetDir, lerpSpeed * dt).normalized;

        return wheelResult.groundNormal;
    }

    public void DrawGizmos(Transform wheelTransform, Vector3 chassisUp)
    {
        if (!showGizmos) return;
        if (Application.isEditor) Initialize();

        Vector3 wheelAxle = wheelTransform.right;
        Vector3 nonSpinningForward = Vector3.Cross(wheelAxle, chassisUp);
        Vector3 nonSpinningUp = Vector3.Cross(nonSpinningForward, wheelAxle);
        Quaternion hubRotation = (nonSpinningForward != Vector3.zero) ? Quaternion.LookRotation(nonSpinningForward, nonSpinningUp) : Quaternion.identity;

        for (int s = 0; s < _slices.Length; s++)
        {
            ScanSliceDefinition slice = _slices[s];
            for (int i = 0; i < rayCount; i++)
            {
                CalculateCastParameters(i, slice, wheelTransform, hubRotation, out Vector3 direction, out Quaternion worldRot, out Vector3 startPos);

                bool isHit = Physics.BoxCast(startPos, slice.boxSize / 2f, direction, out RaycastHit hit, worldRot, slice.maxDistance, scanLayer);
                float drawLength = isHit ? hit.distance : slice.maxDistance;

                float penetration = slice.maxDistance - (isHit ? hit.distance : slice.maxDistance);
                if (isHit) Gizmos.color = Color.Lerp(Color.yellow, Color.red, penetration * 10);
                else Gizmos.color = slice.isSide ? Color.cyan : Color.green;

                if (showLine)
                {
                    Gizmos.DrawRay(startPos, direction * drawLength);
                }

                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(startPos + direction * drawLength, worldRot, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, slice.boxSize);
                Gizmos.matrix = oldMatrix;
            }
        }

        if (_rawHits != null)
        {
            Gizmos.color = Color.orange;
            for (int i = 0; i < _rawHits.Length; i++)
            {
                if (_rawHits[i].isHit)
                {
                    Gizmos.DrawSphere(_rawHits[i].point, 0.03f);
                }
            }
        }

        Vector3 averageNormal = GetAverageNormal();
        Gizmos.color = Color.orange;
        Gizmos.DrawRay(wheelTransform.position, averageNormal * 2);
    }
}