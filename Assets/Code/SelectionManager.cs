using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField] private Color highlightColor = new Color(1, 1, 1, 0.3f);
    
    private GameObject highlightObj;
    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
        CreateHighlightObject();
    }

    private void CreateHighlightObject()
    {
        highlightObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        highlightObj.name = "SelectionHighlight";
        highlightObj.transform.localScale = new Vector3(1f, 1f, 1);
        
        // Slightly in front of everything
        highlightObj.transform.position = new Vector3(0, 0, -0.2f);

        Destroy(highlightObj.GetComponent<MeshCollider>());
        
        Renderer rend = highlightObj.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = highlightColor;
        
        highlightObj.SetActive(false);
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // Block selection if a sub-menu is open or mouse is over UI
        if (Player_UI.Instance != null && Player_UI.Instance.IsSubMenuOpen) return;
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -cam.transform.position.z));
        
        // Snap to grid
        float snappedX = Mathf.Round(worldPos.x);
        float snappedY = Mathf.Round(worldPos.y);

        highlightObj.transform.position = new Vector3(snappedX, snappedY, -0.2f);

        // Click to "select" (just logs for now)
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log($"Selected Grid Cell: ({snappedX}, {snappedY})");
            highlightObj.SetActive(true);
        }
    }
}
