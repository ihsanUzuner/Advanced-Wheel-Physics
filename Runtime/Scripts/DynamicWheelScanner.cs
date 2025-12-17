using UnityEngine;

[System.Serializable]
public class DynamicWheelScanner
{
    public bool showBox = true, showLine = true;

    [Header("General Setting")]
    [Range(0, 3)][SerializeField] float wheelOffset; //It can be used as a spacer if the need arises.
    [Range(1, 15)][SerializeField] int rayCount = 7;
    [Range(0, 360)][SerializeField] int scanRange = 100, scanCenterAngle = 90;
    [SerializeField] LayerMask scanLayer;

    [Header("Manual Slice Configuration")]
    [SerializeField]
    SliceSettings centerSlice = new SliceSettings
    {
        boxSize = new Vector3(0.15f, 0.12f, 0.01f),
        castDistance = 0.66f
    };
    [SerializeField]
    SliceSettings[] sideSlicePairs = new SliceSettings[]
    {
        new SliceSettings
        {
            offset = 0.15f,
            boxSize = new Vector3(0.15f, 0.12f, 0.01f),
            castDistance = 0.65f,
            rayAngle = 3.90f,
            boxAngle = 10f
        }
    };

    public WheelContactResult wheelResult;
    private CachedRayData[] _cachedRays;
    private RawHit[] _rawHits;
    private ScanSliceDefinition[] _slices;
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
    private struct CachedRayData
    {
        public Vector3 localDirection;
        public Quaternion localBoxRot;
    }
    private struct RawHit
    {
        public bool isHit;
        public Vector3 point;
        public Vector3 surfaceNormal;
        public Vector3 scanDirection;// TODO: Will be used later
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

        if (_cachedRays == null || _cachedRays.Length != totalRays)
        {
            _cachedRays = new CachedRayData[totalRays];
        }

        int globalIndex = 0;
        for (int s = 0; s < _slices.Length; s++)
        {
            ScanSliceDefinition slice = _slices[s];
            for (int i = 0; i < rayCount; i++)
            {
                float finalAngle;
                if (rayCount <= 1) finalAngle = scanCenterAngle;
                else
                {
                    float halfAngle = scanRange / 2;
                    float angleStep = (scanRange == 360) ? ((float)scanRange / rayCount) : ((float)scanRange / (rayCount - 1));
                    finalAngle = (scanCenterAngle - halfAngle) + (i * angleStep);
                }

                Quaternion pitchRot = Quaternion.Euler(finalAngle, 0, 0);
                Quaternion rayYawRot = Quaternion.Euler(0, slice.rayAngle, 0);
                Quaternion boxYawRot = Quaternion.Euler(0, slice.boxAngle, 0);

                _cachedRays[globalIndex].localDirection = (pitchRot * rayYawRot) * Vector3.forward;
                _cachedRays[globalIndex].localBoxRot = pitchRot * boxYawRot;

                globalIndex++;
            }
        }
    }

    public void PerformScan(Transform wheelTransform, Vector3 chassisUp)
    {
        if (_rawHits == null || _slices == null || _cachedRays == null)
        {
            Initialize();
        }
#if UNITY_EDITOR
        int totalRays = rayCount * _slices.Length;
        if (!Application.isPlaying || _rawHits.Length != totalRays)
        {
            Initialize();
        }
#endif

        int globalIndex = 0;

        wheelResult.isGrounded = false;
        wheelResult.nearestHitDistance = float.MaxValue;
        wheelResult.maxPenetration = 0f;
        wheelResult.surfaceMaterial = null;

        Vector3 wheelPos = wheelTransform.position;
        Vector3 wheelAxle = wheelTransform.right;

        Vector3 nonSpinningForward = Vector3.Cross(wheelAxle, chassisUp);
        Vector3 nonSpinningUp = Vector3.Cross(nonSpinningForward, wheelAxle);
        Quaternion hubRotation = (nonSpinningForward != Vector3.zero) ? Quaternion.LookRotation(nonSpinningForward, nonSpinningUp) : Quaternion.identity;

        for (int s = 0; s < _slices.Length; s++)
        {
            ScanSliceDefinition slice = _slices[s];

            for (int i = 0; i < rayCount; i++)
            {
                CalculateCastParameters(globalIndex, in slice, in wheelPos, in wheelAxle, in hubRotation, out Vector3 direction, out Quaternion worldRot, out Vector3 startPos);
                if (Physics.BoxCast(startPos, slice.boxSize / 2f, direction, out RaycastHit hit, worldRot, slice.maxDistance, scanLayer))
                {
                    Vector3 stablePoint = startPos + (direction * hit.distance);

                    _rawHits[globalIndex].isHit = true;
                    _rawHits[globalIndex].point = stablePoint;
                    _rawHits[globalIndex].surfaceNormal = hit.normal;
                    _rawHits[globalIndex].scanDirection = direction;

                    float currentPenetration = slice.maxDistance - hit.distance;
                    _rawHits[globalIndex].penetrationDepth = currentPenetration;

                    wheelResult.isGrounded = true;
                    if (hit.distance < wheelResult.nearestHitDistance)
                    {
                        wheelResult.nearestHitDistance = hit.distance;//By adding an offset to this result, you can position the wheel and calculate the suspension force. 
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
                    _rawHits[globalIndex].surfaceNormal = Vector3.zero;
                    _rawHits[globalIndex].penetrationDepth = 0f;
                }
                globalIndex++;
            }
        }
    }

    private void CalculateCastParameters(int globalIndex, in ScanSliceDefinition slice, in Vector3 wheelPos, in Vector3 wheelAxle, in Quaternion hubRotation, out Vector3 direction, out Quaternion worldRot, out Vector3 startPos)
    {
        if (_cachedRays == null || globalIndex >= _cachedRays.Length)
        {
            direction = Vector3.down; worldRot = Quaternion.identity; startPos = wheelPos; return;
        }
        CachedRayData cache = _cachedRays[globalIndex];

        direction = hubRotation * cache.localDirection;
        worldRot = hubRotation * cache.localBoxRot;
        Vector3 totalOffset = wheelAxle * (wheelOffset + slice.offset);
        startPos = wheelPos + totalOffset - direction * (slice.boxSize.z / 2f);
    }

    public Vector3 GetAverageNormal(float lerpSpeed = 15f)
    {
        Vector3 targetWeightedNormal = Vector3.zero;

        for (int i = 0; i < _rawHits.Length; i++)
        {
            if (_rawHits[i].isHit)
            {
                targetWeightedNormal += _rawHits[i].surfaceNormal * _rawHits[i].penetrationDepth;
            }
        }

        Vector3 targetDir = targetWeightedNormal == Vector3.zero ? Vector3.up : targetWeightedNormal.normalized;

        float dt = Time.deltaTime;
        if (Time.inFixedTimeStep) dt = Time.fixedDeltaTime;

        Vector3 currentNormal = wheelResult.groundNormal == Vector3.zero ? Vector3.up : wheelResult.groundNormal;

        wheelResult.groundNormal = Vector3.Lerp(currentNormal, targetDir, lerpSpeed * dt).normalized;

        return wheelResult.groundNormal;
    }





    //////-------------------------------------------------------------------
#if UNITY_EDITOR
    public void DrawGizmos(Transform wheelTransform, Vector3 chassisUp)
    {
        if (!Application.isPlaying || _cachedRays == null || _slices == null)
        {
            Initialize();
        }
        if (_slices == null || _cachedRays == null) return;

        Vector3 wheelPos = wheelTransform.position;
        Vector3 wheelAxle = wheelTransform.right;

        Vector3 nonSpinningForward = Vector3.Cross(wheelAxle, chassisUp);
        Vector3 nonSpinningUp = Vector3.Cross(nonSpinningForward, wheelAxle);
        Quaternion hubRotation = (nonSpinningForward != Vector3.zero) ? Quaternion.LookRotation(nonSpinningForward, nonSpinningUp) : Quaternion.identity;

        int globalIndex = 0;
        for (int s = 0; s < _slices.Length; s++)
        {
            ScanSliceDefinition slice = _slices[s];
            for (int i = 0; i < rayCount; i++)
            {
                CalculateCastParameters(globalIndex, slice, wheelPos, wheelAxle, hubRotation, out Vector3 direction, out Quaternion worldRot, out Vector3 startPos);
                bool isHit = Physics.BoxCast(startPos, slice.boxSize / 2f, direction, out RaycastHit hit, worldRot, slice.maxDistance, scanLayer);
                float drawLength = isHit ? hit.distance : slice.maxDistance;

                float penetration = slice.maxDistance - (isHit ? hit.distance : slice.maxDistance);
                if (isHit) Gizmos.color = Color.Lerp(Color.yellow, Color.red, penetration * 10);
                else Gizmos.color = slice.isSide ? Color.cyan : Color.green;

                if (showLine)
                {
                    Gizmos.DrawRay(startPos, direction * drawLength);
                }

                if (showBox)
                {
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(startPos + direction * drawLength, worldRot, Vector3.one);
                    Gizmos.DrawCube(Vector3.zero, slice.boxSize);
                    Gizmos.matrix = oldMatrix;
                }
                globalIndex++;
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
#endif
}
