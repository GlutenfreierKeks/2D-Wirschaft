using UnityEngine;

public class Villager : MonoBehaviour
{
    public enum Role { Villager, Worker }
    public Role role = Role.Villager;
    
    public float moveSpeed = 1.5f;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private BuildingInstance assignedBuilding;

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
        else if (assignedBuilding == null && !isMoving)
        {
            // Just idle wander, much less frequent
            if (Random.value < 0.002f) Wander();
        }
    }

    public void AssignToBuild(BuildingInstance building, Vector2 offset)
    {
        assignedBuilding = building;
        targetPosition = (Vector2)building.transform.position + offset;
        isMoving = true;
        Debug.Log($"[Villager] Assigned to {building.data.buildingName}. New Target: {targetPosition}");
    }

    public bool IsBusy() => assignedBuilding != null;

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
        // Simple selection: Click to tell them to find work
        if (role == Role.Villager)
        {
            FindWork();
        }
    }

    public void Release()
    {
        assignedBuilding = null;
        isMoving = false;
        Wander();
    }
}
