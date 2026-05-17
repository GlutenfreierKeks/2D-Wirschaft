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
    
    [HideInInspector]
    public bool isOperatingWorker = false;
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
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetPosition.x, targetPosition.y, transform.position.z), moveSpeed * Time.deltaTime);
            
            if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
            {
                OnReachedTarget();
            }
        }
        else
        {
            if (isOperatingWorker && assignedBuilding != null)
            {
                // Decrease cooldown
                if (workActionCooldown > 0f)
                {
                    workActionCooldown -= Time.deltaTime;
                }
                else
                {
                    // Choose a new spot to "work" at
                    PerformWorkAction();
                }

                // Occasionally hop to simulate working
                if (Random.value < 0.005f)
                {
                    StartCoroutine(HopRoutine());
                }
            }
            else if (assignedBuilding == null)
            {
                // Just idle wander, much less frequent
                if (Random.value < 0.002f) Wander();
            }
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
        targetPosition = (Vector2)building.transform.position + Random.insideUnitCircle * 1.2f;
        isMoving = true;
        workActionCooldown = Random.Range(3f, 7f);
        
        if (sr != null) sr.color = new Color(0.7f, 1f, 0.7f); // Light green tint for active workers
        else if (rend != null) rend.material.color = new Color(0.7f, 1f, 0.7f);
        
        Debug.Log($"[Villager] Assigned as operating worker to {building.data.buildingName}. New Target: {targetPosition}");
    }

    private void PerformWorkAction()
    {
        if (assignedBuilding == null) return;
        
        // Find a random spot very close to the building's operating stand
        float radius = 1.0f;
        targetPosition = (Vector2)assignedBuilding.transform.position + Random.insideUnitCircle * radius;
        isMoving = true;
        
        // Random cooldown before moving/working again
        workActionCooldown = Random.Range(3f, 7f);
    }

    private IEnumerator HopRoutine()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        float height = 0.2f;
        Vector3 basePos = transform.position;
        
        while (elapsed < duration)
        {
            if (isMoving) yield break; // Cancel hop if they started moving
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float yOffset = Mathf.Sin(t * Mathf.PI) * height;
            transform.position = new Vector3(basePos.x, basePos.y + yOffset, basePos.z);
            yield return null;
        }
        if (!isMoving) transform.position = basePos;
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
        BuildingInstance[] hubs = FindObjectsByType<BuildingInstance>(FindObjectsSortMode.None);
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
                PromoteToSoldier();
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
            Soldier s = gameObject.AddComponent<Soldier>();
            s.soldierType = (SoldierType)Random.Range(0, 4);
            s.team = Team.Player;
            
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
            FindWork();
        }
    }

    public void Release()
    {
        isOperatingWorker = false;
        assignedBuilding = null;
        isMoving = false;
        
        // Reset color to normal
        if (role == Role.Worker)
        {
            if (sr != null) sr.color = Color.orange;
            else if (rend != null) rend.material.color = Color.orange;
        }
        else
        {
            if (sr != null) sr.color = Color.white;
            else if (rend != null) rend.material.color = Color.white;
        }
        
        Wander();
    }
}
