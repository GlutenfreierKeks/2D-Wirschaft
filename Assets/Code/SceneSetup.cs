using UnityEngine;

public class SceneSetup : MonoBehaviour
{
    private static SceneSetup instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureSceneSetupExists()
    {
        if (FindAnyObjectByType<SceneSetup>() == null)
        {
            GameObject setupObj = new GameObject("SceneSetup");
            setupObj.AddComponent<SceneSetup>();
        }
    }

    public static void EnsureInitialized()
    {
        if (FindAnyObjectByType<SceneSetup>() == null)
        {
            GameObject setupObj = new GameObject("SceneSetup");
            setupObj.AddComponent<SceneSetup>();
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureManager<GridManager>("GridManager");
            EnsureManager<IslandManager>("IslandManager");
            EnsureManager<BuildingManager>("BuildingManager");
            EnsureManager<ResourceManager>("ResourceManager");
            EnsureManager<PlacementManager>("PlacementManager");
            EnsureManager<FogProjector>("FogProjector");
            EnsureManager<SelectionManager>("SelectionManager");
            EnsureManager<VillagerManager>("VillagerManager");
            EnsureManager<NotificationManager>("NotificationManager");
            EnsureManager<AudioManager>("AudioManager");
            
            Debug.Log("[SceneSetup] All managers initialized.");
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private T EnsureManager<T>(string name) where T : MonoBehaviour
    {
        T instance = FindAnyObjectByType<T>();
        if (instance == null)
        {
            GameObject obj = new GameObject(name);
            instance = obj.AddComponent<T>();
            Debug.Log($"[SceneSetup] Created missing manager: {name}");
        }
        return instance;
    }
}
