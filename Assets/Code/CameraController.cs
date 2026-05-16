using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 100f;
    [SerializeField] private float zoomSpeed = 50f; // Adjusted for InputSystem scroll values
    [SerializeField] private float zoomSmoothing = 5f;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 2f;

    [Header("Grid Settings")]
    [SerializeField] private float gridFadeZoom = 200f; // Zoom level at which grid disappears

    private Camera cam;
    private float targetZoom;
    private Vector2 lastMousePos;
    private GameObject gridObject;

    private void Start()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
        
        // Find the grid object (created by GridManager)
        gridObject = GameObject.Find("BackgroundGrid");
    }

    private void Update()
    {
        HandleZoom();
        HandlePan();
    }

    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        
        if (scroll != 0)
        {
            float scrollDelta = scroll > 0 ? 1 : -1;
            targetZoom -= scrollDelta * zoomSpeed * 0.1f;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomSmoothing);

        // Hide grid if zoomed out too far
        if (gridObject == null) gridObject = GameObject.Find("BackgroundGrid");
        if (gridObject != null)
        {
            gridObject.SetActive(cam.orthographicSize < gridFadeZoom);
        }
    }

    private void HandlePan()
    {
        if (Mouse.current == null) return;

        // Use right button or middle button
        bool isPanning = Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed;
        Vector2 currentMousePos = Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame)
        {
            lastMousePos = currentMousePos;
        }

        if (isPanning)
        {
            Vector2 delta = currentMousePos - lastMousePos;
            Vector3 move = new Vector3(-delta.x, -delta.y, 0) * (cam.orthographicSize / 1000f) * panSpeed;
            
            transform.Translate(move, Space.World);
            lastMousePos = currentMousePos;
        }
    }
}
