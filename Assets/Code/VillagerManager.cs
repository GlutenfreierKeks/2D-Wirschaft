using UnityEngine;
using System.Collections.Generic;

public class VillagerManager : MonoBehaviour
{
    public static VillagerManager Instance;

    [Header("Settings")]
    public GameObject villagerPrefab;
    public float wanderRadius = 10f;

    private List<Villager> activeVillagers = new List<Villager>();
    private List<BuildingInstance> pendingBuildings = new List<BuildingInstance>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SpawnStartingPopulation(int islandIndex)
    {
        for (int i = 0; i < 10; i++) SpawnVillager(islandIndex, Villager.Role.Villager);
        for (int i = 0; i < 2; i++) SpawnVillager(islandIndex, Villager.Role.Worker);
    }

    private void SpawnVillager(int islandIndex, Villager.Role role)
    {
        Vector2 islandPos = IslandManager.Instance.GetIslandPosition(islandIndex);
        Vector2 randomOffset = Random.insideUnitCircle * 8f;
        SpawnVillagerAt(islandPos + randomOffset, role);
    }

    public void SpawnVillagerAt(Vector2 position, Villager.Role role)
    {
        EnsurePrefab();
        GameObject vObj = Instantiate(villagerPrefab, new Vector3(position.x, position.y, -0.2f), Quaternion.identity);
        vObj.SetActive(true);
        Villager v = vObj.AddComponent<Villager>();
        v.role = role;
        
        // Visual distinction
        SpriteRenderer sr = vObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (role == Villager.Role.Worker) sr.color = Color.orange;
            else sr.color = Color.white;
            
            Sprite custom = Resources.Load<Sprite>(role.ToString());
            if (custom != null) sr.sprite = custom;
        }

        activeVillagers.Add(v);
    }

    private void EnsurePrefab()
    {
        if (villagerPrefab == null)
        {
            villagerPrefab = Resources.Load<GameObject>("Villager");
            if (villagerPrefab == null)
            {
                villagerPrefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
                villagerPrefab.name = "Villager_Fallback";
                Destroy(villagerPrefab.GetComponent<Collider>());
                villagerPrefab.SetActive(false);
            }
        }
    }

    public void RequestConstruction(BuildingInstance building)
    {
        Debug.Log($"[VillagerManager] Construction requested for: {building.data.buildingName}");
        pendingBuildings.Add(building);
    }

    private void Update()
    {
        UpdateHUD();

        if (pendingBuildings.Count > 0)
        {
            TryAssignWorkers();
        }
    }

    private void UpdateHUD()
    {
        if (Player_UI.Instance == null) return;

        int villagers = 0;
        int workers = 0;
        foreach (var v in activeVillagers)
        {
            if (v.role == Villager.Role.Worker) workers++;
            else villagers++;
        }

        Player_UI.Instance.SetResource("dorfbewohner", villagers);
        Player_UI.Instance.SetResource("arbeiter", workers);
        Player_UI.Instance.SetResource("bevolkerung", villagers + workers);
    }

    private void TryAssignWorkers()
    {
        for (int i = pendingBuildings.Count - 1; i >= 0; i--)
        {
            BuildingInstance b = pendingBuildings[i];
            if (b.NeedsMoreWorkers())
            {
                Villager worker = GetAvailableWorker();
                if (worker != null)
                {
                    Debug.Log($"[VillagerManager] Assigning worker to {b.data.buildingName}");
                    // Spread workers around the building
                    float angle = b.GetAssignedWorkerCount() * 2f * Mathf.PI / b.data.requiredWorkers;
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.5f;
                    
                    worker.AssignToBuild(b, offset);
                    b.RegisterWorker(worker);
                }
            }
            else
            {
                pendingBuildings.RemoveAt(i);
            }
        }
    }

    private Villager GetAvailableWorker()
    {
        int total = 0;
        int busy = 0;
        foreach (var v in activeVillagers)
        {
            if (v.role == Villager.Role.Worker)
            {
                total++;
                if (!v.IsBusy()) return v;
                busy++;
            }
        }
        // Debug.Log($"[VillagerManager] No free workers. Total: {total}, Busy: {busy}");
        return null;
    }

    public void NotifyVillagerConverted(Villager v)
    {
        if (activeVillagers.Contains(v))
        {
            activeVillagers.Remove(v);
        }
    }

    private void RemoveVillager()
    {
        if (activeVillagers.Count > 0)
        {
            Villager v = activeVillagers[activeVillagers.Count - 1];
            activeVillagers.RemoveAt(activeVillagers.Count - 1);
            Destroy(v.gameObject);
        }
    }
}
