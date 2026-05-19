using UnityEngine;
using System.Collections;

public class Villager : MonoBehaviour
{
    public enum Role { Villager, Worker }
    public Role role = Role.Villager;
    
    public float moveSpeed = 1.5f;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private BuildingInstance assignedBuilding;
    public BuildingInstance AssignedBuilding => assignedBuilding;
    
    [HideInInspector]
    public bool isOperatingWorker = false;
    [HideInInspector]
    public float stamina = 100f;
    [HideInInspector]
    public float mood = 80f; // Villager Mood: starts at 80%
    private float workActionCooldown = 0f;

    private SpriteRenderer sr;
    private Renderer rend;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        rend = GetComponent<Renderer>();
        
        // Ensure they are visible and on top
        if (sr != null) sr.sortingOrder = 20;
        
        Wander();
    }

    private void Update()
    {
        // Erhöhte Sterberate für alle bei schlechtem Mood
        if (VillagerManager.Instance != null)
        {
            float gMood = VillagerManager.Instance.globalMood;
            if (gMood < 45f)
            {
                // Sickness chance: higher when mood is closer to 0 (balanced to not be too extreme)
                float sicknessIntensity = 1f - (gMood / 45f); // 0.0 to 1.0
                if (Random.value < Time.deltaTime * 0.00012f * sicknessIntensity)
                {
                    DieOfSickness();
                    return;
                }
            }

            // Food availability mood scaling
            float foodEffect = VillagerManager.Instance.GetFoodMoodEffect();
            if (foodEffect > 0f)
                mood = Mathf.Min(100f, mood + foodEffect * Time.deltaTime);
            else if (foodEffect < 0f)
                mood = Mathf.Max(0f, mood + foodEffect * Time.deltaTime);
        }

        if (isMoving)
        {
            // Apply mood visual modifier to move speed: happy runs fast, sad walks slowly!
            float speedMod = 1.0f;
            if (mood > 80f) speedMod = 1.25f;
            else if (mood < 30f) speedMod = 0.7f;

            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetPosition.x, targetPosition.y, transform.position.z), moveSpeed * speedMod * Time.deltaTime);
            
            if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
            {
                OnReachedTarget();
            }
        }
        else
        {
            if (isOperatingWorker && assignedBuilding != null)
            {
                float hour = DayNightManager.Instance != null ? DayNightManager.Instance.currentHour : 12f;
                bool isWorkTime = assignedBuilding.IsCurrentlyWorkTime();

                if (isWorkTime)
                {
                    // WORK TIME: stay close, work, consume stamina if night
                    SetVisibility(true);

                    // Restore color tint
                    if (sr != null) sr.color = new Color(0.7f, 1f, 0.7f, 1f);

                    // Stamina drain if forced to work at night
                    if (hour >= 18f || hour < 6f)
                    {
                        stamina = Mathf.Max(0f, stamina - Time.deltaTime * 2.0f);
                        if (stamina <= 0f && Random.value < Time.deltaTime * 0.02f)
                        {
                            DieOfExhaustion();
                            return;
                        }
                    }

                    // VERY SLOW Mood changes during active work hours (adjusted to be more challenging):
                    if (assignedBuilding.currentSchedule == BuildingInstance.ScheduleMode.Leisure)
                    {
                        // Leisure shift slowly increases mood
                        mood = Mathf.Min(100f, mood + Time.deltaTime * 0.07f);
                    }
                    else if (assignedBuilding.currentSchedule == BuildingInstance.ScheduleMode.DayOnly)
                    {
                        // Regular day shift is mildly tiring: decays slowly to a baseline of 25%
                        mood = Mathf.Max(25f, mood - Time.deltaTime * 0.04f);
                    }
                    else if (assignedBuilding.currentSchedule == BuildingInstance.ScheduleMode.Continuous)
                    {
                        // 24/7 night-shift work decays mood slowly but surely to 0%
                        mood = Mathf.Max(0f, mood - Time.deltaTime * 0.22f);
                    }

                    // Active working actions
                    if (workActionCooldown > 0f)
                    {
                        workActionCooldown -= Time.deltaTime;
                    }
                    else
                    {
                        PerformWorkAction(0.25f); // Tight work inside
                    }

                    // Occasionally work animation
                    if (Random.value < 0.005f)
                    {
                        StartCoroutine(WorkAnimationRoutine());
                    }
                }
                else
                {
                    // NOT WORK TIME: Either sleep or leisure
                    bool isLeisureTime = hour >= 12f && hour < 15f && assignedBuilding.currentSchedule == BuildingInstance.ScheduleMode.Leisure;

                    if (isLeisureTime)
                    {
                        // LEISURE TIME: wander freely and recover stamina & mood slowly
                        SetVisibility(true);
                        
                        // Restore color tint
                        if (sr != null) sr.color = new Color(0.7f, 1f, 0.7f, 1f);

                        stamina = Mathf.Min(100f, stamina + Time.deltaTime * 3.0f);
                        mood = Mathf.Min(100f, mood + Time.deltaTime * 0.16f); // Slow leisure recovery (was 0.22f)

                        if (workActionCooldown > 0f)
                        {
                            workActionCooldown -= Time.deltaTime;
                        }
                        else
                        {
                            PerformWorkAction(4.5f); // Wide stroll around building
                        }
                    }
                    else
                    {
                        // SLEEP TIME: go to nearest house to sleep, recover stamina & mood slowly
                        BuildingInstance sleepHouse = FindSleepHouse();

                        if (sleepHouse != null)
                        {
                            float dist = Vector2.Distance(transform.position, sleepHouse.transform.position);
                            if (dist < 0.4f)
                            {
                                // Inside sleeping!
                                SetVisibility(false);
                                stamina = Mathf.Min(100f, stamina + Time.deltaTime * 5.0f);
                                
                                // Large house gives a comfort boost to mood recovery!
                                float sleepMoodRecovery = 0.12f;
                                if (sleepHouse.data != null && sleepHouse.data.buildingName.Contains("Großes Haus"))
                                {
                                    sleepMoodRecovery = 0.22f; // Much faster mood recovery for premium comfort!
                                }
                                mood = Mathf.Min(100f, mood + Time.deltaTime * sleepMoodRecovery);
                            }
                            else
                            {
                                // Walk to sleep house
                                SetVisibility(true);
                                targetPosition = sleepHouse.transform.position;
                                isMoving = true;
                            }
                        }
                        else
                        {
                            // Sleep outside near building center (fade transparency)
                            SetVisibility(true);
                            if (sr != null) sr.color = new Color(0.7f, 1f, 0.7f, 0.4f); // semi-transparent sleep
                            stamina = Mathf.Min(100f, stamina + Time.deltaTime * 3.5f);
                            mood = Mathf.Min(100f, mood + Time.deltaTime * 0.08f); // sleeping outside is less comfortable
                        }
                    }
                }
            }
            else if (assignedBuilding == null)
            {
                // Unemployed: ensure visible and recover stamina & mood slowly
                SetVisibility(true);
                stamina = Mathf.Min(100f, stamina + Time.deltaTime * 1.5f);
                mood = Mathf.Min(100f, mood + Time.deltaTime * 0.04f); // happy just wandering slowly (was 0.06f)

                // Just idle wander, much less frequent
                if (Random.value < 0.002f) Wander();
            }
        }

        // Soft floor: the lower the mood, the harder it is to sink further.
        // A slight positive resistance force applies at very low levels (< 35%) to act as a stabilizer.
        if (mood < 35f)
        {
            mood = Mathf.Min(100f, mood + Time.deltaTime * 0.015f * (35f - mood));
        }
    }

    public void AssignToBuild(BuildingInstance building, Vector2 offset)
    {
        assignedBuilding = building;
        targetPosition = (Vector2)building.transform.position + offset;
        isMoving = true;
        Debug.Log($"[Villager] Assigned to {building.data.buildingName}. New Target: {targetPosition}");
    }

    public bool IsBusy() => assignedBuilding != null || isOperatingWorker;

    public void AssignAsOperatingWorker(BuildingInstance building)
    {
        isOperatingWorker = true;
        assignedBuilding = building;
        // Keep them strictly inside the building's visual boundaries (radius 0.25f)
        targetPosition = (Vector2)building.transform.position + Random.insideUnitCircle * 0.25f;
        isMoving = true;
        workActionCooldown = Random.Range(3f, 7f);
        
        if (sr != null) sr.color = new Color(0.7f, 1f, 0.7f); // Light green tint for active workers
        else if (rend != null) rend.material.color = new Color(0.7f, 1f, 0.7f);
        
        Debug.Log($"[Villager] Assigned as operating worker to {building.data.buildingName}. New Target: {targetPosition}");
    }

    private void PerformWorkAction()
    {
        PerformWorkAction(0.25f);
    }

    private void PerformWorkAction(float radius)
    {
        if (assignedBuilding == null) return;
        
        targetPosition = (Vector2)assignedBuilding.transform.position + Random.insideUnitCircle * radius;
        isMoving = true;
        
        // Random cooldown before moving/working again
        workActionCooldown = Random.Range(3f, 7f);
    }

    private IEnumerator WorkAnimationRoutine()
    {
        float elapsed = 0f;
        float duration = 1.0f; // 1 second of active working action
        Vector3 basePos = transform.position;
        
        while (elapsed < duration)
        {
            if (isMoving) 
            {
                transform.rotation = Quaternion.identity;
                yield break;
            }
            elapsed += Time.deltaTime;
            
            // 1. Shaking left and right (tilting)
            float tiltAngle = Mathf.Sin(elapsed * 25f) * 12f; // Fast tilt
            transform.rotation = Quaternion.Euler(0, 0, tiltAngle);
            
            // 2. Tiny bounce up and down
            float yOffset = Mathf.Abs(Mathf.Sin(elapsed * 12f)) * 0.15f;
            transform.position = new Vector3(basePos.x, basePos.y + yOffset, basePos.z);
            
            yield return null;
        }
        
        // Reset to normal state
        if (!isMoving)
        {
            transform.position = basePos;
            transform.rotation = Quaternion.identity;
        }
    }

    private void Wander()
    {
        // Try to find a nearby land cell so villagers never walk into the ocean
        Vector2 currentPos = transform.position;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            Vector2 candidate = currentPos + Random.insideUnitCircle * 5f;
            // Snap to grid so IsLand() lookup works correctly
            candidate = new Vector2(Mathf.Round(candidate.x), Mathf.Round(candidate.y));
            if (IslandManager.IsLand(candidate))
            {
                targetPosition = candidate;
                isMoving = true;
                return;
            }
        }
        // No valid land cell found nearby – stay in place
        isMoving = false;
    }

    public void FindWork()
    {
        BuildingInstance target = FindNearestOpportunity();
        if (target != null)
        {
            targetPosition = target.transform.position;
            isMoving = true;
            assignedBuilding = target; 
        }
        else
        {
            Debug.Log("Kein Job oder Kaserne gefunden!");
        }
    }

    private BuildingInstance FindNearestOpportunity()
    {
        BuildingInstance[] hubs = FindObjectsByType<BuildingInstance>();
        BuildingInstance nearest = null;
        float minDist = float.MaxValue;

        foreach (var h in hubs)
        {
            if (h.IsConstructed() && (h.data.isWorkerHub || h.data.isBarracks))
            {
                float d = Vector2.Distance(transform.position, h.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = h;
                }
            }
        }
        return nearest;
    }

    private void OnReachedTarget()
    {
        isMoving = false;
        if (assignedBuilding != null)
        {
            if (isOperatingWorker)
            {
                // Operating workers just stay at the building
                return;
            }
            
            if (assignedBuilding.data.isWorkerHub)
            {
                PromoteToWorker();
            }
            else if (assignedBuilding.data.isBarracks)
            {
                if (assignedBuilding.IsConstructed())
                {
                    PromoteToSoldier();
                }
                else
                {
                    assignedBuilding.NotifyWorkerArrived(this);
                }
            }
            else
            {
                assignedBuilding.NotifyWorkerArrived(this);
            }
        }
    }

    private void PromoteToWorker()
    {
        role = Role.Worker;
        assignedBuilding = null;
        if (sr != null) sr.color = Color.orange;
        else if (rend != null) rend.material.color = Color.orange;
        Wander();
    }

    private void PromoteToSoldier()
    {
        if (Player_UI.Instance == null) return;
        
        int current = Player_UI.Instance.GetResource("soldaten");
        int max = Player_UI.Instance.GetMaxResource("soldaten");
        
        if (current < max)
        {
            Debug.Log($"[Villager] Promoting to Soldier. Type: {(SoldierType)Random.Range(0, 4)}");
            Player_UI.Instance.AddResource("soldaten", 1);
            Player_UI.Instance.AddResource("dorfbewohner", -1);
            
            if (VillagerManager.Instance != null)
            {
                VillagerManager.Instance.NotifyVillagerConverted(this);
            }

            // Transform into Soldier
            transform.rotation = Quaternion.identity; // Reset rotation!
            SetVisibility(true); // Ensure visible!
            Soldier s = gameObject.AddComponent<Soldier>();
            s.soldierType = (SoldierType)Random.Range(0, 4);
            s.team = Team.Player;
            if (Photon.Pun.PhotonNetwork.LocalPlayer != null)
            {
                s.ownerActorNumber = Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber;
            }
            
            Destroy(this); // Remove Villager script
        }
        else
        {
            Debug.Log("Soldaten-Limit erreicht!");
            assignedBuilding = null;
            Wander();
        }
    }

    private void OnMouseDown()
    {
        // Simple selection: Click to tell them to find work (only free villagers)
        if (role == Role.Villager && !isOperatingWorker)
        {
            AudioManager.Instance?.PlaySelectSound();
            FindWork();
        }
    }

    public void Release()
    {
        isOperatingWorker = false;
        assignedBuilding = null;
        isMoving = false;
        
        // ONLY reset to Villager if they are NOT a Worker!
        // A construction worker should stay a construction worker so they can build more things.
        if (role != Role.Worker)
        {
            role = Role.Villager;
        }

        transform.rotation = Quaternion.identity; // Reset rotation!
        SetVisibility(true); // Ensure visible!
        
        // Keep orange color for workers, white for normal villagers
        if (sr != null) sr.color = (role == Role.Worker) ? Color.orange : Color.white;
        else if (rend != null) rend.material.color = (role == Role.Worker) ? Color.orange : Color.white;
        
        Wander();
    }

    private void SetVisibility(bool visible)
    {
        if (sr != null) sr.enabled = visible;
        else if (rend != null) rend.enabled = visible;
    }

    public struct MoodRateSnapshot
    {
        public float workRatePerSecond;
        public float housingRatePerSecond;
        public bool isSleepingInHouse;
        public bool isSleepingHomeless;
    }

    /// <summary>Aktuelle Stimmungsänderung durch Arbeit/Schlaf (Punkte pro Sekunde, 0–100-Skala).</summary>
    public MoodRateSnapshot GetMoodRateSnapshot()
    {
        MoodRateSnapshot snapshot = new MoodRateSnapshot();
        float hour = DayNightManager.Instance != null ? DayNightManager.Instance.currentHour : 12f;

        if (isOperatingWorker && assignedBuilding != null)
        {
            if (assignedBuilding.IsCurrentlyWorkTime())
            {
                switch (assignedBuilding.currentSchedule)
                {
                    case BuildingInstance.ScheduleMode.Leisure:
                        snapshot.workRatePerSecond = 0.07f;
                        break;
                    case BuildingInstance.ScheduleMode.DayOnly:
                        snapshot.workRatePerSecond = -0.04f;
                        break;
                    case BuildingInstance.ScheduleMode.Continuous:
                        snapshot.workRatePerSecond = -0.22f;
                        break;
                }
            }
            else
            {
                bool isLeisureTime = hour >= 12f && hour < 15f &&
                    assignedBuilding.currentSchedule == BuildingInstance.ScheduleMode.Leisure;

                if (isLeisureTime)
                {
                    snapshot.workRatePerSecond = 0.16f;
                }
                else
                {
                    BuildingInstance sleepHouse = FindSleepHouse();
                    if (sleepHouse != null)
                    {
                        float dist = Vector2.Distance(transform.position, sleepHouse.transform.position);
                        if (dist < 0.4f)
                        {
                            snapshot.isSleepingInHouse = true;
                            snapshot.housingRatePerSecond = 0.12f;
                            if (sleepHouse.data != null && sleepHouse.data.buildingName.Contains("Großes Haus"))
                            {
                                snapshot.housingRatePerSecond = 0.22f;
                            }
                        }
                    }
                    else
                    {
                        snapshot.isSleepingHomeless = true;
                        snapshot.housingRatePerSecond = 0.08f;
                    }
                }
            }
        }

        return snapshot;
    }

    private BuildingInstance FindSleepHouse()
    {
        BuildingInstance[] buildings = FindObjectsByType<BuildingInstance>();
        BuildingInstance nearestHouse = null;
        float minDist = float.MaxValue;
        
        foreach (var b in buildings)
        {
            if (b.IsConstructed() && (b.data.productionResourceId == "bevolkerung" || b.name.Contains("Haus") || b.name.Contains("Warehouse") || b.name.Contains("MyWarehouse")))
            {
                float dist = Vector2.Distance(transform.position, b.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestHouse = b;
                }
            }
        }
        return nearestHouse;
    }

    private void DieOfExhaustion()
    {
        Debug.LogWarning($"[Villager] Ein Dorfbewohner ist vor Erschöpfung gestorben!");
        
        // Spawn a cute floating warning text above them!
        GameObject textGO = new GameObject("ExhaustionText");
        textGO.transform.position = transform.position + Vector3.up * 0.5f;
        var textMesh = textGO.AddComponent<TextMesh>();
        textMesh.text = "☠ Erschöpft gestorben!";
        textMesh.fontSize = 18;
        textMesh.characterSize = 0.08f;
        textMesh.color = Color.red;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        
        // Add a simple translation over time
        textGO.AddComponent<SelfMovingText>();
        
        NotificationManager.Instance?.Notify("villager_death", "Ein Villager ist gestorben.", 4f);
        Destroy(textGO, 2f);
        
        if (assignedBuilding != null)
        {
            assignedBuilding.RemoveOperatingWorker(this);
        }
        
        if (VillagerManager.Instance != null)
        {
            VillagerManager.Instance.NotifyVillagerConverted(this);
        }
        
        Destroy(gameObject);
    }

    private void DieOfSickness()
    {
        Debug.LogWarning($"[Villager] {gameObject.name} ist an Stress/Krankheit gestorben!");
        
        // Spawn a cute floating warning text above them!
        GameObject textGO = new GameObject("SicknessText");
        textGO.transform.position = transform.position + Vector3.up * 0.5f;
        var textMesh = textGO.AddComponent<TextMesh>();
        textMesh.text = "☠ An Stress gestorben!";
        textMesh.fontSize = 18;
        textMesh.characterSize = 0.08f;
        textMesh.color = new Color(0.9f, 0.1f, 0.1f); // red
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        
        // Float upwards
        textGO.AddComponent<SelfMovingText>();
        
        NotificationManager.Instance?.Notify("villager_death", "Ein Villager ist gestorben.", 4f);
        Destroy(textGO, 2.5f);

        // Remove from work
        if (assignedBuilding != null)
        {
            assignedBuilding.RemoveOperatingWorker(this);
        }
        
        if (VillagerManager.Instance != null)
        {
            VillagerManager.Instance.NotifyVillagerConverted(this);
        }
        
        Destroy(gameObject);
    }
}

// Tiny helper to float the death text upward
public class SelfMovingText : MonoBehaviour
{
    private void Update()
    {
        transform.Translate(Vector3.up * Time.deltaTime * 0.6f);
    }
}
