using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance;

    [Header("Settings")]
    [SerializeField] private Color canPlaceColor = new Color(0, 1, 0, 0.5f);
    [SerializeField] private Color cannotPlaceColor = new Color(1, 0, 0, 0.5f);

    private GameObject ghostParent;
    private List<Renderer> ghostRenderers = new List<Renderer>();
    private BuildingData currentBuilding;
    private bool isPlacing = false;
    
    private Camera cam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        cam = Camera.main;
    }

    public void StartPlacement(BuildingData data)
    {
        if (!ResourceManager.Instance.CanAfford(data.woodCost, data.stoneCost))
        {
            Debug.Log($"Cannot afford {data.buildingName}!");
            return;
        }

        currentBuilding = data;
        isPlacing = true;
        
        if (ghostParent != null) Destroy(ghostParent);
        CreateGhost(data);
    }

    private void CreateGhost(BuildingData data)
    {
        ghostParent = new GameObject("PlacementGhost");
        ghostRenderers.Clear();

        // Create a grid of quads representing the footprint
        float startX = -(data.width - 1) / 2f;
        float startY = -(data.height - 1) / 2f;

        for (int x = 0; x < data.width; x++)
        {
            for (int y = 0; y < data.height; y++)
            {
                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Quad);
                part.transform.SetParent(ghostParent.transform);
                part.transform.localPosition = new Vector3(startX + x, startY + y, -0.2f);
                Destroy(part.GetComponent<MeshCollider>());
                
                Renderer rend = part.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("Sprites/Default"));
                ghostRenderers.Add(rend);
            }
        }
    }

    private void Update()
    {
        if (!isPlacing || currentBuilding == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -cam.transform.position.z));
        
        // Offset for even/odd sizes to keep it centered on grid
        float xOffset = (currentBuilding.width % 2 == 0) ? 0.5f : 0f;
        float yOffset = (currentBuilding.height % 2 == 0) ? 0.5f : 0f;

        float snappedX = Mathf.Round(worldPos.x - xOffset) + xOffset;
        float snappedY = Mathf.Round(worldPos.y - yOffset) + yOffset;
        
        ghostParent.transform.position = new Vector3(snappedX, snappedY, -0.2f);

        bool valid = CheckPlacementValidity(new Vector2(snappedX, snappedY));
        SetGhostColor(valid ? canPlaceColor : cannotPlaceColor);

        if (Mouse.current.leftButton.wasPressedThisFrame && valid)
        {
            PlaceBuilding(new Vector2(snappedX, snappedY));
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

    private bool CheckPlacementValidity(Vector2 center)
    {
        // Check if explored
        if (!FogProjector.IsExplored(center)) return false;

        float startX = -(currentBuilding.width - 1) / 2f;
        float startY = -(currentBuilding.height - 1) / 2f;

        if (currentBuilding.placementRule == PlacementRule.Pier)
        {
            // Pier Rule: 1 Land cell, everything else Water
            int landCount = 0;
            for (int x = 0; x < currentBuilding.width; x++)
            {
                for (int y = 0; y < currentBuilding.height; y++)
                {
                    Vector2 pos = center + new Vector2(startX + x, startY + y);
                    if (BuildingManager.IsOccupied(pos)) return false;
                    if (IslandManager.IsLand(pos)) landCount++;
                }
            }
            return landCount == 1; // Only 1 land connection allowed
        }
        else if (currentBuilding.placementRule == PlacementRule.WaterOnly)
        {
            for (int x = 0; x < currentBuilding.width; x++)
            {
                for (int y = 0; y < currentBuilding.height; y++)
                {
                    Vector2 pos = center + new Vector2(startX + x, startY + y);
                    if (BuildingManager.IsOccupied(pos) || IslandManager.IsLand(pos)) return false;
                }
            }
        }
        else // LandOnly
        {
            for (int x = 0; x < currentBuilding.width; x++)
            {
                for (int y = 0; y < currentBuilding.height; y++)
                {
                    Vector2 pos = center + new Vector2(startX + x, startY + y);
                    if (BuildingManager.IsOccupied(pos) || !IslandManager.IsLand(pos)) return false;
                }
            }
        }
        return true;
    }

    private void SetGhostColor(Color c)
    {
        foreach (var rend in ghostRenderers)
        {
            rend.material.color = c;
        }
    }

    private void PlaceBuilding(Vector2 pos)
    {
        ResourceManager.Instance.SpendResources(currentBuilding.woodCost, currentBuilding.stoneCost);
        BuildingManager.Instance.SpawnBuilding(currentBuilding, pos);
        CancelPlacement();
    }

    private void CancelPlacement()
    {
        isPlacing = false;
        ghostParent.SetActive(false);
    }
}
