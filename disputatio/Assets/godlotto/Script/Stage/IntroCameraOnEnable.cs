using UnityEngine;

public sealed class IntroCameraOnEnable : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector3 cameraPosition = new Vector3(538f, 797f, -10f);
    [SerializeField] private float orthographicSize = 300f;

    private void OnEnable()
    {
        Camera cameraToMove = targetCamera != null ? targetCamera : Camera.main;

        if (cameraToMove == null)
        {
            return;
        }

        cameraToMove.transform.position = cameraPosition;
        cameraToMove.orthographicSize = orthographicSize;
    }
}
