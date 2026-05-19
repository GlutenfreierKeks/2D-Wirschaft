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

        occupiedBuildingCells.Clear();
    }

    public void SpawnBuilding(BuildingData data, Vector2 position, bool isLocal = true)
    {
        if (data.prefab == null) return;

        GameObject building = Instantiate(data.prefab, new Vector3(position.x, position.y, -0.21f), Quaternion.identity);
        building.name = data.buildingName;

        // Scale the visual so the texture covers exactly the building's grid footprint.
        // We measure the natural rendered size first (independent of any prefab scale),
        // then compute a factor so the result is exactly data.width × data.height world units.
        building.transform.localScale = Vector3.one; // reset before measuring

        float naturalW = 1f, naturalH = 1f;

        SpriteRenderer buildingSR = building.GetComponentInChildren<SpriteRenderer>();
        if (buildingSR != null && buildingSR.sprite != null)
        {
            // sprite.bounds is in local space at scale 1 – exactly what we need
            naturalW = buildingSR.sprite.bounds.size.x;
            naturalH = buildingSR.sprite.bounds.size.y;
        }
        else
        {
            Renderer buildingRend = building.GetComponentInChildren<Renderer>();
            if (buildingRend != null)
            {
                // bounds at scale (1,1,1) gives the natural world-unit size
                naturalW = buildingRend.bounds.size.x;
                naturalH = buildingRend.bounds.size.y;
            }
        }

        if (naturalW <= 0f) naturalW = 1f;
        if (naturalH <= 0f) naturalH = 1f;

        building.transform.localScale = new Vector3(
            data.width  / naturalW,
            data.height / naturalH,
            1f
        );

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

        BuildingInstance lodging = warehouse.AddComponent<BuildingInstance>();
        lodging.isLocal = isLocal;
        lodging.isPreBuiltLodging = true;
        lodging.displayNameOverride = "Hauptlager";
        lodging.sleepCapacityOverride = 5;
    }
}
