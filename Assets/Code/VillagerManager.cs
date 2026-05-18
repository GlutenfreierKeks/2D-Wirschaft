using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class VillagerManager : MonoBehaviour
{
    public static VillagerManager Instance;

    [Header("Settings")]
    public GameObject villagerPrefab;
    public float wanderRadius = 10f;
    [HideInInspector]
    public float globalMood = 100f;

    private List<Villager> activeVillagers = new List<Villager>();
    public IReadOnlyList<Villager> ActiveVillagers => activeVillagers;
    private List<BuildingInstance> pendingBuildings = new List<BuildingInstance>();
    private float currentFoodMoodEffect = 0f;
    private float wheatConsumedAccumulator = 0f;
    private float luxuryConsumedAccumulator = 0f;
    private bool lowWheatAlertSent;
    private bool lowWoodAlertSent;
    private bool lowStoneAlertSent;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SpawnStartingPopulation(int islandIndex)
    {
        int startVillagers = 10;
        int startWorkers = 2;

        if (PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LobbySettingsKeys.StartVillagers, out object villagersObj))
            {
                startVillagers = System.Convert.ToInt32(villagersObj);
            }

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LobbySettingsKeys.StartWorkers, out object workersObj))
            {
                startWorkers = System.Convert.ToInt32(workersObj);
            }
        }

        for (int i = 0; i < startVillagers; i++) SpawnVillager(islandIndex, Villager.Role.Villager);
        for (int i = 0; i < startWorkers; i++) SpawnVillager(islandIndex, Villager.Role.Worker);
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
        UpdateFoodEffect();
        UpdateHUD();
        CheckAlerts();

        if (pendingBuildings.Count > 0)
        {
            TryAssignWorkers();
        }
    }

    private void UpdateHUD()
    {
        if (Player_UI.Instance == null) return;

        int freeVillagers = 0;
        int workers = 0;
        int employedVillagers = 0;

        foreach (var v in activeVillagers)
        {
            if (v == null) continue;
            
            if (v.role == Villager.Role.Worker)
            {
                workers++;
            }
            else
            {
                if (v.isOperatingWorker)
                {
                    employedVillagers++;
                }
                else
                {
                    freeVillagers++;
                }
            }
        }

        int totalVillagers = freeVillagers + employedVillagers;
        Player_UI.Instance.SetMaxResource("dorfbewohner", totalVillagers);
        Player_UI.Instance.SetResource("dorfbewohner", freeVillagers);
        Player_UI.Instance.SetResource("arbeiter", workers);
        Player_UI.Instance.SetResource("bevolkerung", totalVillagers + workers);

        // Calculate global mood based on all active villagers
        float totalMood = 0f;
        int moodCount = 0;
        foreach (var v in activeVillagers)
        {
            if (v != null)
            {
                totalMood += v.mood;
                moodCount++;
            }
        }
        globalMood = moodCount > 0 ? (totalMood / moodCount) : 100f;
        int avgMood = Mathf.RoundToInt(globalMood);
        Player_UI.Instance.SetResource("stimmung", avgMood);
    }

    public Villager GetAvailableVillager()
    {
        foreach (var v in activeVillagers)
        {
            if (v != null && v.role == Villager.Role.Villager && !v.isOperatingWorker && !v.IsBusy())
            {
                return v;
            }
        }

        NotificationManager.Instance?.Notify("no_free_villagers", "Du hast keine freien Arbeitslosen mehr.", 10f);
        return null;
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

    public Villager GetAvailableWorker()
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
        if (total <= 0)
        {
            NotificationManager.Instance?.Notify("no_workers_total", "Du hast keine Bauarbeiter.", 10f);
        }
        else
        {
            NotificationManager.Instance?.Notify("no_workers_free", "Alle Bauarbeiter sind gerade beschaftigt.", 10f);
        }
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

    public float GetFoodMoodEffect() => currentFoodMoodEffect;

    private void UpdateFoodEffect()
    {
        if (Player_UI.Instance == null) return;

        int fruits = Player_UI.Instance.GetResource("fruechte");
        int meat = Player_UI.Instance.GetResource("fleisch");
        int weizen = Player_UI.Instance.GetResource("weizen");
        int pop = Mathf.Max(1, activeVillagers.Count);
        
        // 1. Wheat reserve levels (Primary food: crisis if low)
        float wheatRatio = (float)weizen / pop;
        float wheatEffect = 0f;
        if (weizen == 0)
        {
            // Wheat fully depleted: severe starvation! (increased penalty)
            wheatEffect = -0.25f;
        }
        else if (wheatRatio < 0.5f)
        {
            // Low wheat reserve: strong shortage panic! (increased penalty)
            wheatEffect = -0.12f;
        }

        // 2. Dynamic Fruits & Meat reserve scales (Feasting: consume more when abundant, granting stronger boosts!)
        float fruitsRatio = (float)fruits / pop;
        float meatRatio = (float)meat / pop;
        
        float fruitsConsMod = fruits > 0 ? Mathf.Clamp(fruitsRatio, 0.1f, 1.5f) : 0f;
        float meatConsMod = meat > 0 ? Mathf.Clamp(meatRatio, 0.1f, 1.5f) : 0f;
        
        float luxuryEffect = 0f;
        if (fruits > 0)
        {
            luxuryEffect += Mathf.Min(0.08f, fruitsConsMod * 0.018f); // Slightly harder positive boosts
        }
        if (meat > 0)
        {
            luxuryEffect += Mathf.Min(0.08f, meatConsMod * 0.018f); // Slightly harder positive boosts
        }

        // Combine effects and clamp
        currentFoodMoodEffect = Mathf.Clamp(wheatEffect + luxuryEffect, -0.25f, 0.12f);

        // 3. Proportional consumption for staple (wheat): ALWAYS 0.2 units per villager per 60 seconds (1 minute)
        wheatConsumedAccumulator += pop * 0.2f * (Time.deltaTime / 60f);
        if (wheatConsumedAccumulator >= 1f)
        {
            int toConsume = Mathf.FloorToInt(wheatConsumedAccumulator);
            wheatConsumedAccumulator -= toConsume;
            
            if (weizen >= toConsume)
            {
                Player_UI.Instance.AddResource("weizen", -toConsume);
            }
            else
            {
                if (weizen > 0) Player_UI.Instance.AddResource("weizen", -weizen);
            }
        }

        // 4. Dynamic luxury consumption (fruits & meat): consume more when abundant, up to 1.5x base rate (max 0.225/min)!
        luxuryConsumedAccumulator += pop * 0.15f * (Time.deltaTime / 60f);
        if (luxuryConsumedAccumulator >= 1f)
        {
            int baseToConsume = Mathf.FloorToInt(luxuryConsumedAccumulator);
            luxuryConsumedAccumulator -= baseToConsume;
            
            // Scaled consumption based on abundance
            int targetFruits = fruits > 0 ? Mathf.Max(1, Mathf.RoundToInt(baseToConsume * fruitsConsMod)) : 0;
            int targetMeat = meat > 0 ? Mathf.Max(1, Mathf.RoundToInt(baseToConsume * meatConsMod)) : 0;
            
            int fruitsTaken = Mathf.Min(fruits, targetFruits);
            int meatTaken = Mathf.Min(meat, targetMeat);
            
            if (fruitsTaken > 0) Player_UI.Instance.AddResource("fruechte", -fruitsTaken);
            if (meatTaken > 0) Player_UI.Instance.AddResource("fleisch", -meatTaken);
        }
    }

    private void CheckAlerts()
    {
        if (globalMood < 35f)
        {
            NotificationManager.Instance?.Notify("low_mood", "Villager Stimmung ist niedrig.", 18f);
        }

        if (Player_UI.Instance == null)
        {
            return;
        }

        int wheat = Player_UI.Instance.GetResource("weizen");
        int wood = Player_UI.Instance.GetResource("holz");
        int stone = Player_UI.Instance.GetResource("stein");

        int wheatThreshold = Mathf.Max(2, activeVillagers.Count / 2);
        if (wheat <= wheatThreshold)
        {
            if (!lowWheatAlertSent)
            {
                NotificationManager.Instance?.Notify("low_weizen", "Ressource Weizen ist low.", 14f);
                lowWheatAlertSent = true;
            }
        }
        else if (wheat > wheatThreshold + 2)
        {
            lowWheatAlertSent = false;
        }

        if (wood <= 15)
        {
            if (!lowWoodAlertSent)
            {
                NotificationManager.Instance?.Notify("low_holz", "Ressource Holz ist low.", 14f);
                lowWoodAlertSent = true;
            }
        }
        else if (wood > 20)
        {
            lowWoodAlertSent = false;
        }

        if (stone <= 15)
        {
            if (!lowStoneAlertSent)
            {
                NotificationManager.Instance?.Notify("low_stein", "Ressource Stein ist low.", 14f);
                lowStoneAlertSent = true;
            }
        }
        else if (stone > 20)
        {
            lowStoneAlertSent = false;
        }
    }
}
