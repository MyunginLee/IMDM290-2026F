using UnityEngine;

public class ToonDemoSpinner : MonoBehaviour
{
    [SerializeField] private Vector3 axis = Vector3.up;
    [SerializeField] private float degreesPerSecond = 18f;
    [SerializeField] private Space space = Space.World;

    private void Update()
    {
        transform.Rotate(axis.normalized, degreesPerSecond * Time.deltaTime, space);
    }
}
