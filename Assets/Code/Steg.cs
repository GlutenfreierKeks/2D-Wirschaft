using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Steg : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    // ── Ship building ─────────────────────────────────────────────────────────
    public static int ShipWoodCost  = 10;
    public static int ShipIronCost  = 5;
    public static int ShipGoldCost  = 3;
    public static float ShipBuildTime = 20f;
    public static int ShipCapacity  = 8;

    public bool IsBuildingShip { get; private set; } = false;
    public float ShipBuildProgress { get; private set; } = 0f;

    private Ship dockedShip = null;
    public Ship DockedShip => dockedShip;

    void OnEnable() => BuildingManager.RegisterSteg(this);
    void OnDisable() => BuildingManager.UnregisterSteg(this);

    // Initialize the steg to the nearest integer grid position (1x1 cells).
    // World X -> grid.x, World Y -> grid.y
    public void Initialize(Vector3 worldPos)
    {
        GridPosition = WorldToGrid(worldPos);
        transform.position = new Vector3(GridPosition.x, GridPosition.y, -0.21f);
    }

    public static Vector2Int WorldToGrid(Vector3 w) => new Vector2Int(Mathf.RoundToInt(w.x), Mathf.RoundToInt(w.y));

    public IEnumerable<Vector2Int> NeighborPositions()
    {
        yield return GridPosition + new Vector2Int(1, 0);
        yield return GridPosition + new Vector2Int(-1, 0);
        yield return GridPosition + new Vector2Int(0, 1);
        yield return GridPosition + new Vector2Int(0, -1);
    }

    // Call to remove this steg. The manager will recalculate supports and collapse unsupported stegs.
    public void Remove()
    {
        BuildingManager.OnStegRemoved(this);
        Destroy(gameObject);
    }

    // ── Ship Building ─────────────────────────────────────────────────────────

    /// <summary>Returns true if resources are available to build a ship here.</summary>
    public bool CanBuildShip()
    {
        if (IsBuildingShip) return false;
        if (dockedShip != null) return false;
        if (ResourceManager.Instance == null) return false;
        return ResourceManager.Instance.HasResource("holz", ShipWoodCost)
            && ResourceManager.Instance.HasResource("eisen", ShipIronCost)
            && ResourceManager.Instance.HasResource("gold",  ShipGoldCost);
    }

    /// <summary>Start building a ship. Returns true if started successfully.</summary>
    public bool StartBuildShip()
    {
        if (!CanBuildShip()) return false;

        ResourceManager.Instance.SpendResource("holz",  ShipWoodCost);
        ResourceManager.Instance.SpendResource("eisen", ShipIronCost);
        ResourceManager.Instance.SpendResource("gold",  ShipGoldCost);

        IsBuildingShip = true;
        ShipBuildProgress = 0f;
        StartCoroutine(BuildShipRoutine());
        NotificationManager.Instance?.Notify("ship_build_start", "Schiffbau gestartet!", 4f);
        return true;
    }

    private IEnumerator BuildShipRoutine()
    {
        float elapsed = 0f;
        while (elapsed < ShipBuildTime)
        {
            elapsed += Time.deltaTime;
            ShipBuildProgress = Mathf.Clamp01(elapsed / ShipBuildTime);
            yield return null;
        }

        IsBuildingShip = false;
        ShipBuildProgress = 0f;
        SpawnShip();
    }

    private void SpawnShip()
    {
        // Find a water cell next to this steg to place the ship
        Vector2 spawnPos = FindWaterSpawn();

        GameObject shipGO = new GameObject("Schiff");
        shipGO.transform.position = new Vector3(spawnPos.x, spawnPos.y, -0.22f);

        Ship ship = shipGO.AddComponent<Ship>();
        ship.capacity  = ShipCapacity;
        shipGO.AddComponent<BoxCollider2D>().size = new Vector2(1f, 1.5f);

        dockedShip = ship;
        NotificationManager.Instance?.Notify("ship_built", "Schiff fertig gebaut! Klicke darauf um es zu steuern.", 6f);
        AudioManager.Instance?.PlayConstructionSound(spawnPos);
    }

    private Vector2 FindWaterSpawn()
    {
        // Prefer a water cell adjacent to this steg
        Vector2Int[] dirs = {
            new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1, 0), new Vector2Int(1, 0)
        };
        foreach (var d in dirs)
        {
            Vector2Int candidate = GridPosition + d;
            Vector2 cVec = new Vector2(candidate.x, candidate.y);
            if (!IslandManager.IsLand(cVec))
                return cVec;
        }
        // Fallback: place on top of steg (shouldn't happen)
        return new Vector2(GridPosition.x, GridPosition.y);
    }
}
