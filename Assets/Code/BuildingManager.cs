using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public enum PlacementRule { LandOnly, WaterOnly, Pier }

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;
    private static HashSet<Vector2> occupiedBuildingCells = new HashSet<Vector2>();

    public static bool IsOccupied(Vector2 pos)
    {
        return occupiedBuildingCells.Contains(new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)));
    }

    public static void RegisterOccupancy(Vector2 center, int width, int height)
    {
        float startX = -(width - 1) / 2f;
        float startY = -(height - 1) / 2f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                occupiedBuildingCells.Add(center + new Vector2(startX + x, startY + y));
            }
        }
    }

    [Header("Building Prefabs")]
    [SerializeField] private Material warehouseMaterial;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SpawnBuilding(BuildingData data, Vector2 position, bool isLocal = true)
    {
        if (data.prefab == null) return;

        GameObject building = Instantiate(data.prefab, new Vector3(position.x, position.y, -0.21f), Quaternion.identity);
        building.name = data.buildingName;
        
        // Add building instance logic
        BuildingInstance instance = building.AddComponent<BuildingInstance>();
        instance.data = data;
        instance.isLocal = isLocal;

        FogRevealer revealer = building.GetComponent<FogRevealer>();
        if (revealer == null) revealer = building.AddComponent<FogRevealer>();
        revealer.isLocalPlayer = isLocal;

        // Apply red tint to enemy buildings
        if (!isLocal)
        {
            foreach (Renderer r in building.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name != "FogMask") // Don't tint the fog mask
                    r.material.color = new Color(1f, 0.5f, 0.5f, 1f);
            }
        }

        RegisterOccupancy(position, data.width, data.height);
    }

    public void SpawnMainWarehouse(Vector2 position, bool isLocal = true)
    {
        GameObject warehouse = GameObject.CreatePrimitive(PrimitiveType.Quad);
        warehouse.name = isLocal ? "MyWarehouse" : "EnemyWarehouse";
        warehouse.transform.position = new Vector3(position.x, position.y, -0.21f);
        warehouse.transform.localScale = new Vector3(3f, 3f, 1f); 

        FogRevealer revealer = warehouse.AddComponent<FogRevealer>();
        revealer.radius = 10f;
        revealer.isLocalPlayer = isLocal;

        Renderer rend = warehouse.GetComponent<Renderer>();
        if (!isLocal) rend.material.color = new Color(1f, 0.5f, 0.5f, 1f);

        Texture2D tex = Resources.Load<Texture2D>("warehouse_texture");
        if (tex != null)
        {
            rend.material = new Material(Shader.Find("Unlit/Transparent"));
            rend.material.mainTexture = tex;
            if (!isLocal) rend.material.color = new Color(1f, 0.5f, 0.5f, 1f);
        }
        
        RegisterOccupancy(position, 3, 3);
        Destroy(warehouse.GetComponent<MeshCollider>());
    }
}
