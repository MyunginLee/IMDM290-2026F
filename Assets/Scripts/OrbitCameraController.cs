using UnityEngine;

public class OrbitCameraController : MonoBehaviour
{
    [Header("Who moves")]
    public Transform cameraToOrbit;        // Drag your Main Camera (or any camera) here.

    [Header("Orbit settings")]
    public float radius = 150f;            // Circle radius on XZ plane
    public float heightY = 0f;             // Fixed Y height
    public float angularSpeedDegPerSec = 10f; // Degrees per second (positive = CCW)
    public Vector3 lookTarget = new Vector3(0f,0f,180f); // What the camera looks at

    private float angleDeg; // internal state

    void Reset()
    {
        // If this component sits on the camera, auto-assign it
        if (cameraToOrbit == null && Camera.main != null)
            cameraToOrbit = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cameraToOrbit == null) return;

        angleDeg += angularSpeedDegPerSec * Time.deltaTime;
        float rad = angleDeg * Mathf.Deg2Rad;

        // Position on the XZ circle
        Vector3 pos = new Vector3(Mathf.Cos(rad) * radius, heightY, 90f+ Mathf.Sin(rad) * radius);
        cameraToOrbit.position = pos;

        // Always look at target (defaults to world origin)
        cameraToOrbit.rotation = Quaternion.LookRotation(lookTarget - cameraToOrbit.position, Vector3.up);
    }
}
