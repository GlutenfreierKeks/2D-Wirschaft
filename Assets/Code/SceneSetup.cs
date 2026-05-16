using UnityEngine;

public class SceneSetup : MonoBehaviour
{
    private void Awake()
    {
        EnsureManager<GridManager>("GridManager");
        EnsureManager<IslandManager>("IslandManager");
        EnsureManager<BuildingManager>("BuildingManager");
        EnsureManager<ResourceManager>("ResourceManager");
        EnsureManager<PlacementManager>("PlacementManager");
        EnsureManager<FogProjector>("FogProjector");
        EnsureManager<SelectionManager>("SelectionManager");
        
        Debug.Log("[SceneSetup] All managers initialized.");
    }

    private T EnsureManager<T>(string name) where T : MonoBehaviour
    {
        T instance = FindFirstObjectByType<T>();
        if (instance == null)
        {
            GameObject obj = new GameObject(name);
            instance = obj.AddComponent<T>();
            Debug.Log($"[SceneSetup] Created missing manager: {name}");
        }
        return instance;
    }
}
