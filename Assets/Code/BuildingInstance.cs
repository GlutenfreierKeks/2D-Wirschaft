using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BuildingInstance : MonoBehaviour
{
    public BuildingData data;
    public bool isLocal;
    
    private bool isConstructed = false;
    private float constructionProgress = 0f;
    private Renderer[] renderers;
    private FogRevealer revealer;

    public bool IsConstructed() => isConstructed;

    private int workersArrived = 0;
    private int workersAssigned = 0;
    private List<Villager> assignedWorkers = new List<Villager>();
    private List<Villager> operatingWorkers = new List<Villager>();

    private void Update()
    {
        if (isConstructed && operatingWorkers.Count < data.workersNeeded)
        {
            TryHireOperatingWorkers();
        }
    }

    public void TryHireOperatingWorkers()
    {
        if (VillagerManager.Instance == null) return;

        while (operatingWorkers.Count < data.workersNeeded)
        {
            Villager v = VillagerManager.Instance.GetAvailableVillager();
            if (v != null)
            {
                v.AssignAsOperatingWorker(this);
                operatingWorkers.Add(v);
            }
            else
            {
                break; 
            }
        }
    }

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        revealer = GetComponent<FogRevealer>();
        if (revealer != null) revealer.enabled = false; 

        // Request workers from manager
        if (VillagerManager.Instance != null)
        {
            VillagerManager.Instance.RequestConstruction(this);
        }

        StartCoroutine(ConstructionRoutine());
    }

    public bool NeedsMoreWorkers() => workersAssigned < data.requiredWorkers;
    public int GetAssignedWorkerCount() => workersAssigned;

    public void RegisterWorker(Villager v)
    {
        workersAssigned++;
        assignedWorkers.Add(v);
    }

    public void NotifyWorkerArrived(Villager v)
    {
        workersArrived++;
        Debug.Log($"[BuildingInstance] Worker arrived at {data.buildingName}. Total: {workersArrived}/{data.requiredWorkers}");
    }

    private IEnumerator ConstructionRoutine()
    {
        // 1. Blueprint Phase: Blueish and very transparent
        SetColor(Color.cyan);
        SetAlpha(0.2f);

        // Wait for all workers to arrive at the site
        while (workersArrived < data.requiredWorkers)
        {
            yield return null;
        }

        // 2. Building Phase: Normal colors, fading in
        SetColor(Color.white);
        
        while (constructionProgress < 1f)
        {
            constructionProgress += Time.deltaTime / data.buildTime;
            SetAlpha(Mathf.Lerp(0.1f, 1f, constructionProgress));
            yield return null;
        }

        CompleteConstruction();
    }

    private void SetAlpha(float alpha)
    {
        foreach (var r in renderers)
        {
            if (r.gameObject.name == "FogMask") continue;
            Color c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }
    }

    private void SetColor(Color color)
    {
        foreach (var r in renderers)
        {
            if (r.gameObject.name == "FogMask") continue;
            float currentAlpha = r.material.color.a;
            Color newCol = color;
            newCol.a = currentAlpha;
            r.material.color = newCol;
        }
    }

    private void CompleteConstruction()
    {
        isConstructed = true;
        if (revealer != null) revealer.enabled = true;

        // Release workers
        foreach (var w in assignedWorkers) w.Release();
        assignedWorkers.Clear();

        // Hire operating workers
        TryHireOperatingWorkers();

        // If it's a worker hub, maybe it converts nearby villagers? 
        // For now just handle production
        if (data.productionResourceId == "bevolkerung")
        {
            Player_UI.Instance.AddMaxPopulation(data.productionAmount);
            // Soldier limit increases with population capacity (e.g. 1 soldier per 2 people)
            int soldierLimitBonus = Mathf.Max(1, data.productionAmount / 2);
            Player_UI.Instance.SetMaxResource("soldaten", Player_UI.Instance.GetMaxResource("soldaten") + soldierLimitBonus);
        }
        
        if (!string.IsNullOrEmpty(data.productionResourceId) || data.producesVillagers)
        {
            StartCoroutine(ProductionRoutine());
        }
    }

    private IEnumerator ProductionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(data.productionInterval);
            
            // Check if building has enough operating workers to produce
            if (operatingWorkers.Count < data.workersNeeded)
            {
                Debug.Log($"[BuildingInstance] {data.buildingName} skips production cycle due to missing workers ({operatingWorkers.Count}/{data.workersNeeded})");
                continue;
            }

            if (ResourceManager.Instance != null)
            {
                // 1. Check consumption
                if (!string.IsNullOrEmpty(data.consumedResourceId) && data.consumedAmount > 0)
                {
                    if (ResourceManager.Instance.HasResource(data.consumedResourceId, data.consumedAmount))
                    {
                        ResourceManager.Instance.SpendResource(data.consumedResourceId, data.consumedAmount);
                    }
                    else
                    {
                        // Cannot afford production this cycle
                        continue; 
                    }
                }

                // 2. Produce
                if (!string.IsNullOrEmpty(data.productionResourceId))
                {
                    ResourceManager.Instance.AddResource(data.productionResourceId, data.productionAmount);
                }

                if (data.producesVillagers && VillagerManager.Instance != null)
                {
                    // Only spawn if population capacity allows (optional, but good practice)
                    if (Player_UI.Instance.GetResource("bevolkerung") < Player_UI.Instance.GetMaxPopulation())
                    {
                        VillagerManager.Instance.SpawnVillagerAt(transform.position, Villager.Role.Villager);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Release construction workers if still building
        foreach (var w in assignedWorkers)
        {
            if (w != null) w.Release();
        }
        assignedWorkers.Clear();

        // Release operating workers
        foreach (var w in operatingWorkers)
        {
            if (w != null)
            {
                w.isOperatingWorker = false;
                w.Release();
            }
        }
        operatingWorkers.Clear();
    }
}
