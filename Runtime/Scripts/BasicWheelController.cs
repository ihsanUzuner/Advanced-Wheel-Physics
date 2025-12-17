using UnityEngine;

public class BasicWheelController : MonoBehaviour
{
    [Header("References")]
    public Transform wheelTransform;
    public MeshRenderer wheelRenderer;
    [Header("Base Wheel Settings")]
    public bool showGizmos = true;
    public float rimRadius = 0.25f;
    public float wheelRadius = 0.67f;
    public float wheelMass = 20f;
    public float wheelInertia = 1.2f;



    [Header("Modules")]
    public DynamicWheelScanner tireScanner;


    void FixedUpdate()
    {
        UpdateTirePhysics();
    }


    public void UpdateTirePhysics()
    {
        tireScanner.PerformScan(wheelTransform, transform.root.up);
    }

    // public void UpdateDeformationVisuals()
    // {
    //     bridge.ApplyToMaterial(
    //         transform,
    //         wheelTransform,
    //         tireScanner.HitCount,
    //         tireScanner.CollisionPoints,
    //         tireScanner.CollisionNormals,
    //         tireScanner.DeformationAmounts,
    //         tireScanner.DeformationRadii,
    //         _currentLateralSlip
    //     );
    // }



#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        if (tireScanner != null && wheelTransform != null)
        {
            tireScanner.DrawGizmos(wheelTransform, transform.root.up);
        }

        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(wheelTransform.position, wheelTransform.right, rimRadius);

        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.DrawWireDisc(wheelTransform.position, wheelTransform.right, wheelRadius);
    }
#endif
}
