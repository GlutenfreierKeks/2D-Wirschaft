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
        if (!ResourceManager.Instance.CanAfford(data.woodCost, data.stoneCost, data.ironCost, data.goldCost))
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
        // 1. Check Exploration
        if (!FogProjector.IsExplored(center)) 
        {
            Debug.Log("Placement Failed: Area not explored");
            return false;
        }

        // 2. Check Resource Requirement
        if (currentBuilding.requiredResourceType != ResourceType.None)
        {
            if (IslandManager.GetResourceType(center) != currentBuilding.requiredResourceType)
            {
                Debug.Log($"Placement Failed: Needs {currentBuilding.requiredResourceType}");
                return false;
            }
        }

        float startX = -(currentBuilding.width - 1) / 2f;
        float startY = -(currentBuilding.height - 1) / 2f;

        // 3. Check Footprint (Collision & Ground)
        for (int x = 0; x < currentBuilding.width; x++)
        {
            for (int y = 0; y < currentBuilding.height; y++)
            {
                Vector2 pos = center + new Vector2(startX + x, startY + y);
                
                if (BuildingManager.IsOccupied(pos)) 
                {
                    Debug.Log("Placement Failed: Position occupied by another building");
                    return false;
                }

                bool isLand = IslandManager.IsLand(pos);
                if (isLand && !IslandManager.IsOwnIsland(pos))
                {
                    Debug.Log("Placement Failed: Can only build on your own island");
                    return false;
                }

                if (currentBuilding.placementRule == PlacementRule.LandOnly && !isLand)
                {
                    Debug.Log("Placement Failed: Needs Land");
                    return false;
                }
                
                if (currentBuilding.placementRule == PlacementRule.WaterOnly && isLand)
                {
                    Debug.Log("Placement Failed: Needs Water");
                    return false;
                }
            }
        }

        // Special check for Pier
        if (currentBuilding.placementRule == PlacementRule.Pier)
        {
            int landCount = 0;
            for (int x = 0; x < currentBuilding.width; x++)
            {
                for (int y = 0; y < currentBuilding.height; y++)
                {
                    Vector2 pos = center + new Vector2(startX + x, startY + y);
                    if (IslandManager.IsLand(pos)) landCount++;
                }
            }
            if (landCount != 1) 
            {
                Debug.Log("Placement Failed: Pier needs exactly 1 land cell");
                return false;
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
        ResourceManager.Instance.SpendResources(currentBuilding.woodCost, currentBuilding.stoneCost, currentBuilding.ironCost, currentBuilding.goldCost);
        BuildingManager.Instance.SpawnBuilding(currentBuilding, pos);

        if (Photon.Pun.PhotonNetwork.InRoom)
        {
            object[] content = new object[] { currentBuilding.buildingName, pos };
            ExitGames.Client.Photon.SendOptions sendOptions = new ExitGames.Client.Photon.SendOptions { Reliability = true };
            Photon.Pun.PhotonNetwork.RaiseEvent(2, content, new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.Others }, sendOptions);
        }

        // Remove resource nodes covered by the building footprint
        float startX = -(currentBuilding.width - 1) / 2f;
        float startY = -(currentBuilding.height - 1) / 2f;
        for (int x = 0; x < currentBuilding.width; x++)
        {
            for (int y = 0; y < currentBuilding.height; y++)
            {
                Vector2 cellPos = pos + new Vector2(startX + x, startY + y);
                IslandManager.RemoveResourceNodeAt(cellPos);
            }
        }

        CancelPlacement();
    }

    private void CancelPlacement()
    {
        isPlacing = false;
        ghostParent.SetActive(false);
    }
}
