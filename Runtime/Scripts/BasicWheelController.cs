using UnityEngine;

public class BasicWheelController : MonoBehaviour
{

    [Header("References")]
    public Transform wheelTransform;

    [Header("Modules")]
    public DynamicWheelScanner tireScanner;

    void FixedUpdate()
    {
        tireScanner.PerformScan(wheelTransform, transform.root.up);
    }

    void OnDrawGizmos()
    {
        if (tireScanner != null && wheelTransform != null)
        {
            tireScanner.DrawGizmos(wheelTransform, transform.root.up);
        }
    }
}