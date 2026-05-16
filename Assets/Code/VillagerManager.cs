using UnityEngine;
using System.Collections.Generic;

public class VillagerManager : MonoBehaviour
{
    public static VillagerManager Instance;

    [Header("Settings")]
    public GameObject villagerPrefab;
    public float wanderRadius = 10f;

    private List<GameObject> activeVillagers = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (Player_UI.Instance == null) return;

        int currentPop = Player_UI.Instance.GetResource("bevolkerung");
        
        // Sync number of prefabs with population count
        if (activeVillagers.Count < currentPop)
        {
            SpawnVillager();
        }
        else if (activeVillagers.Count > currentPop)
        {
            RemoveVillager();
        }
    }

    private void SpawnVillager()
    {
        // For now, spawn near the first island position
        Vector2 spawnPos = IslandManager.Instance.GetIslandPosition(0);
        Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
        
        GameObject villager = Instantiate(villagerPrefab, new Vector3(spawnPos.x + randomOffset.x, spawnPos.y + randomOffset.y, -0.2f), Quaternion.identity);
        villager.name = "Villager_" + activeVillagers.Count;
        activeVillagers.Add(villager);
    }

    private void RemoveVillager()
    {
        if (activeVillagers.Count > 0)
        {
            GameObject v = activeVillagers[activeVillagers.Count - 1];
            activeVillagers.RemoveAt(activeVillagers.Count - 1);
            Destroy(v);
        }
    }
}
