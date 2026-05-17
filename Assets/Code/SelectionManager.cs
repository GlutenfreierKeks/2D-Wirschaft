using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField] private Color highlightColor = new Color(1, 1, 1, 0.3f);

    private GameObject highlightObj;
    private Camera cam;
    private BuildingInfoPanel infoPanel;

    private void Start()
    {
        cam = Camera.main;
        CreateHighlightObject();
        EnsureInfoPanel();
        EnsureDayNightManager();
    }

    /// <summary>
    /// Erstellt BuildingInfoPanel automatisch falls es nicht in der Szene ist.
    /// </summary>
    private void EnsureInfoPanel()
    {
        infoPanel = BuildingInfoPanel.Instance;
        if (infoPanel == null)
        {
            GameObject panelGO = new GameObject("BuildingInfoPanel");
            infoPanel = panelGO.AddComponent<BuildingInfoPanel>();
            Debug.Log("[SelectionManager] BuildingInfoPanel auto-created.");
        }
    }

    private void EnsureDayNightManager()
    {
        if (DayNightManager.Instance == null)
        {
            GameObject go = new GameObject("DayNightManager");
            go.AddComponent<DayNightManager>();
            Debug.Log("[SelectionManager] DayNightManager auto-created.");
        }
    }

    private void CreateHighlightObject()
    {
        highlightObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        highlightObj.name = "SelectionHighlight";
        highlightObj.transform.localScale = new Vector3(1f, 1f, 1f);
        highlightObj.transform.position   = new Vector3(0f, 0f, -0.19f);
        Destroy(highlightObj.GetComponent<MeshCollider>());

        Renderer rend = highlightObj.GetComponent<Renderer>();
        rend.material       = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = highlightColor;
        highlightObj.SetActive(false);
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // Block selection while sub-menu is open
        if (Player_UI.Instance != null && Player_UI.Instance.IsSubMenuOpen) return;

        // Block if mouse is over any UI element
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 worldPos    = cam.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

        float snappedX = Mathf.Round(worldPos.x);
        float snappedY = Mathf.Round(worldPos.y);
        highlightObj.transform.position = new Vector3(snappedX, snappedY, -0.19f);

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        highlightObj.SetActive(true);

        // Mausposition in Weltkoordinaten (z=0 für 2D)
        Vector2 worldPos2D = cam.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

        // Alle 2D-Collider an diesem Punkt prüfen
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos2D);

        Debug.Log($"[SelectionManager] Klick auf Weltpos {worldPos2D} | " +
                  $"Gefundene Collider: {hits.Length}");

        BuildingInstance clickedBuilding = null;
        foreach (var col in hits)
        {
            Debug.Log($"  → Collider: {col.gameObject.name} | hat BuildingInstance: " +
                      $"{col.GetComponent<BuildingInstance>() != null}");
            BuildingInstance b = col.GetComponent<BuildingInstance>();
            if (b != null) { clickedBuilding = b; break; }
        }

        if (infoPanel == null) infoPanel = BuildingInfoPanel.Instance;

        if (clickedBuilding != null)
        {
            Debug.Log($"[SelectionManager] Gebäude angeklickt: {clickedBuilding.data?.buildingName}");
            infoPanel?.Show(clickedBuilding);
        }
        else
        {
            if (infoPanel != null && infoPanel.IsVisible)
                infoPanel.Hide();
        }
    }
}
