using UnityEngine;
using Photon.Pun;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;

    [Header("Building Prefabs")]
    [SerializeField] private Material warehouseMaterial;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SpawnMainWarehouse(Vector2 position)
    {
        GameObject warehouse = GameObject.CreatePrimitive(PrimitiveType.Quad);
        warehouse.name = "MainWarehouse";
        warehouse.transform.position = new Vector3(position.x, position.y, -0.21f); // Slightly in front of land
        warehouse.transform.localScale = new Vector3(3f, 3f, 1f); 

        FogRevealer revealer = warehouse.AddComponent<FogRevealer>();
        revealer.radius = 10f;

        Renderer rend = warehouse.GetComponent<Renderer>();
        // We'll try to find the texture we just generated
        Texture2D tex = Resources.Load<Texture2D>("warehouse_texture");
        
        if (tex != null || warehouseMaterial != null)
        {
            rend.material = warehouseMaterial != null ? warehouseMaterial : new Material(Shader.Find("Unlit/Transparent"));
            if (tex != null) rend.material.mainTexture = tex;
        }
        else
        {
            // If no texture found yet, set a nice brown color as fallback
            rend.material = new Material(Shader.Find("Unlit/Transparent"));
            rend.material.color = new Color(0.5f, 0.3f, 0.1f, 1f);
            Debug.LogWarning("Warehouse texture not found in Resources. Please move the generated image to Assets/Resources/warehouse_texture.png");
        }
        
        Destroy(warehouse.GetComponent<MeshCollider>());
    }
}
