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

    // ── Produktion ────────────────────────────────────────────────────────
    public bool IsProductionPaused { get; private set; } = false;
    public int  TotalProduced      { get; private set; } = 0;
    public float ProductionProgress { get; private set; } = 0f;
    private float productionTimer = 0f;

    // ── Schedule ─────────────────────────────────────────────────────────
    public enum ScheduleMode { Continuous, DayOnly, Leisure }
    public ScheduleMode currentSchedule = ScheduleMode.DayOnly;

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

        // Production timer logic (pauses during night / when understaffed / when paused)
        if (isConstructed && !IsProductionPaused)
        {
            bool canProduce = !string.IsNullOrEmpty(data.productionResourceId) || data.producesVillagers;
            if (canProduce)
            {
                if (operatingWorkers.Count >= data.workersNeeded && IsCurrentlyWorkTime())
                {
                    productionTimer += Time.deltaTime;
                    float interval = data.productionInterval > 0f ? data.productionInterval : 1f;
                    ProductionProgress = Mathf.Clamp01(productionTimer / interval);

                    if (productionTimer >= interval)
                     {
                         productionTimer = 0f;
                         ProductionProgress = 0f;
                         TriggerProductionCycle();
                     }
                }
                else
                {
                    // Freeze progress! Do not wipe the production timer or progress to 0.
                    // This allows workers to resume exactly where they left off when returning from breaks or sleep.
                }
            }
            else
            {
                ProductionProgress = 0f;
            }
        }
        else
        {
            ProductionProgress = 0f;
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

    public bool IsCurrentlyWorkTime()
    {
        if (!isConstructed || IsProductionPaused) return false;
        if (DayNightManager.Instance == null) return true;

        float hour = DayNightManager.Instance.currentHour;

        switch (currentSchedule)
        {
            case ScheduleMode.Continuous:
                return true;

            case ScheduleMode.DayOnly:
                return hour >= 6f && hour < 18f;

            case ScheduleMode.Leisure:
                bool isDay = hour >= 6f && hour < 18f;
                bool isLeisureTime = hour >= 12f && hour < 15f;
                return isDay && !isLeisureTime;

            default:
                return true;
        }
    }

    public void RemoveOperatingWorker(Villager v)
    {
        if (operatingWorkers.Contains(v))
        {
            operatingWorkers.Remove(v);
        }
    }

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        revealer = GetComponent<FogRevealer>();
        if (revealer != null) revealer.enabled = false;

        // Add a collider so the building can be clicked via Physics2D overlap.
        if (GetComponent<Collider2D>() == null)
        {
            var col = gameObject.AddComponent<BoxCollider2D>();
            Vector3 s = transform.localScale;
            col.size = new Vector2(
                s.x > 0.001f ? data.width  / s.x : data.width,
                s.y > 0.001f ? data.height / s.y : data.height
            );
            Debug.Log($"[BuildingInstance] Collider size set to {col.size} " +
                      $"(scale={s}, w={data.width}, h={data.height})");
        }

        // Request workers from manager
        if (VillagerManager.Instance != null)
            VillagerManager.Instance.RequestConstruction(this);

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
        if (data.productionResourceId == "bevolkerung")
        {
            Player_UI.Instance.AddMaxPopulation(data.productionAmount);
            int soldierLimitBonus = Mathf.Max(1, data.productionAmount / 2);
            Player_UI.Instance.SetMaxResource("soldaten", Player_UI.Instance.GetMaxResource("soldaten") + soldierLimitBonus);
        }
    }

    private void TriggerProductionCycle()
    {
        // Check if building has enough operating workers to produce
        if (operatingWorkers.Count < data.workersNeeded) return;

        // Check if it's currently work time according to the schedule
        if (!IsCurrentlyWorkTime()) return;

        if (ResourceManager.Instance != null)
        {
            // 1. Verbrauch prüfen
            if (!string.IsNullOrEmpty(data.consumedResourceId) && data.consumedAmount > 0)
            {
                if (ResourceManager.Instance.HasResource(data.consumedResourceId, data.consumedAmount))
                    ResourceManager.Instance.SpendResource(data.consumedResourceId, data.consumedAmount);
                else
                    return; // Ressourcen fehlen, Zyklus überspringen
            }

            // 2. Produzieren
            bool producedSomething = false;
            if (!string.IsNullOrEmpty(data.productionResourceId))
            {
                int baseAmount = data.productionAmount;
                float globalMood = 100f;
                if (VillagerManager.Instance != null)
                {
                    globalMood = VillagerManager.Instance.globalMood;
                }
                
                // Mood modifier: slowly declining yield down to 30% under worst conditions (0% mood)
                float yieldModifier = Mathf.Lerp(0.3f, 1.0f, globalMood / 100f);
                int actualProduction = Mathf.Max(1, Mathf.RoundToInt(baseAmount * yieldModifier));

                ResourceManager.Instance.AddResource(data.productionResourceId, actualProduction);
                TotalProduced += actualProduction;
                producedSomething = true;
            }

            if (data.producesVillagers && VillagerManager.Instance != null)
            {
                if (Player_UI.Instance.GetResource("dorfbewohner") < Player_UI.Instance.GetMaxPopulation())
                {
                    VillagerManager.Instance.SpawnVillagerAt(transform.position, Villager.Role.Villager);
                    TotalProduced++;
                    producedSomething = true;
                }
            }

            if (producedSomething)
            {
                SpawnProductionParticles();
            }
        }
    }

    private void SpawnProductionParticles()
    {
        GameObject pObj = new GameObject("ProductionParticles");
        pObj.transform.position = transform.position + new Vector3(0f, 0f, -0.5f);
        
        ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = 1.0f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.gravityModifier = -0.3f; // Rise upwards gently
        main.stopAction = ParticleSystemStopAction.Destroy;

        // Custom resource icon textures as sprites!
        ParticleSystemRenderer psr = pObj.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            Sprite resSprite = null;
            if (!string.IsNullOrEmpty(data.productionResourceId))
            {
                string capId = Capitalize(data.productionResourceId);
                resSprite = Resources.Load<Sprite>($"{capId}_Icon");
                if (resSprite == null) resSprite = Resources.Load<Sprite>($"{capId}_Overlay");
            }
            else if (data.producesVillagers)
            {
                resSprite = Resources.Load<Sprite>("Villager");
            }

            if (resSprite != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.mainTexture = resSprite.texture;
                psr.material = mat;
                main.startColor = Color.white; // Keep original texture colors
            }
            else
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.material = new Material(Shader.Find("Sprites/Default"));
                
                Color pColor = Color.green;
                if (data.productionResourceId == "gold") pColor = new Color(1f, 0.85f, 0f);
                else if (data.productionResourceId == "stein") pColor = Color.gray;
                else if (data.productionResourceId == "eisen") pColor = new Color(0.7f, 0.7f, 0.8f);
                else if (data.productionResourceId == "dorfbewohner") pColor = Color.cyan;
                main.startColor = pColor;
            }
        }

        // Add a gentle rotation over time
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        
        var burst = new ParticleSystem.Burst(0f, 12);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;

        ps.Play();
    }

    private string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    // ── Öffentliche Steuerung ────────────────────────────────────────────────

    /// <summary>Produktion pausieren / fortsetzen.</summary>
    public void ToggleProduction()
    {
        IsProductionPaused = !IsProductionPaused;
        Debug.Log($"[BuildingInstance] {data.buildingName} Produktion: {(IsProductionPaused ? "PAUSIERT" : "AKTIV")}");

        if (IsProductionPaused)
        {
            // Alle Arbeiter entlassen, damit sie sich andere Arbeit suchen
            foreach (var w in operatingWorkers)
            {
                if (w != null)
                {
                    w.Release();
                }
            }
            operatingWorkers.Clear();
        }
        else
        {
            // Sofort versuchen neue Arbeiter zuzuweisen
            TryHireOperatingWorkers();
        }
    }

    /// <summary>Gebäude abreißen – gibt Hälfte der Baukosten zurück.</summary>
    public void Demolish()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource("holz",  data.woodCost  / 2);
            ResourceManager.Instance.AddResource("stein", data.stoneCost / 2);
            ResourceManager.Instance.AddResource("eisen", data.ironCost  / 2);
            ResourceManager.Instance.AddResource("gold",  data.goldCost  / 2);
        }
        Debug.Log($"[BuildingInstance] {data.buildingName} abgerissen.");
        Destroy(gameObject);
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
