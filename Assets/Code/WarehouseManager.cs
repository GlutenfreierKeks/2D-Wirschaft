using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages all warehouses and tracks island ownership.
/// Warehouses are required to build on other islands.
/// </summary>
public class WarehouseManager : MonoBehaviour
{
    public static WarehouseManager Instance { get; private set; }

    [Header("Warehouse Settings")]
    public int maxWarehousesPerPlayer = 5;

    private List<Warehouse> localWarehouses = new List<Warehouse>();
    private List<Warehouse> enemyWarehouses = new List<Warehouse>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterWarehouse(Warehouse warehouse)
    {
        if (warehouse == null) return;

        if (warehouse.isLocal)
        {
            if (!localWarehouses.Contains(warehouse))
            {
                localWarehouses.Add(warehouse);
            }
        }
        else
        {
            if (!enemyWarehouses.Contains(warehouse))
            {
                enemyWarehouses.Add(warehouse);
            }
        }

        Debug.Log($"[WarehouseManager] Registered warehouse at {warehouse.transform.position}. " +
                  $"Local: {localWarehouses.Count}, Enemy: {enemyWarehouses.Count}");
    }

    public void UnregisterWarehouse(Warehouse warehouse)
    {
        if (warehouse == null) return;

        localWarehouses.Remove(warehouse);
        enemyWarehouses.Remove(warehouse);
    }

    /// <summary>
    /// Checks if a position is on a player's island (has a local warehouse there).
    /// </summary>
    public bool IsOnLocalIsland(Vector2 position)
    {
        Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));

        foreach (var warehouse in localWarehouses)
        {
            if (warehouse == null) continue;

            Vector2Int whGrid = new Vector2Int(
                Mathf.RoundToInt(warehouse.transform.position.x),
                Mathf.RoundToInt(warehouse.transform.position.y)
            );

            if (IsSameIsland(gridPos, whGrid))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get the nearest local warehouse to a position.
    /// </summary>
    public Warehouse GetNearestLocalWarehouse(Vector2 position)
    {
        Warehouse nearest = null;
        float minDist = float.MaxValue;

        foreach (var warehouse in localWarehouses)
        {
            if (warehouse == null) continue;

            float dist = Vector2.Distance(position, warehouse.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = warehouse;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get the nearest warehouse (local or enemy) to a position.
    /// </summary>
    public Warehouse GetNearestWarehouse(Vector2 position, bool localOnly = true)
    {
        Warehouse nearest = null;
        float minDist = float.MaxValue;

        var warehouses = localOnly ? localWarehouses : localWarehouses.Concat(enemyWarehouses);

        foreach (var warehouse in warehouses)
        {
            if (warehouse == null) continue;

            float dist = Vector2.Distance(position, warehouse.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = warehouse;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get all warehouses on a specific island.
    /// </summary>
    public List<Warehouse> GetWarehousesOnIsland(Vector2 position)
    {
        Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        List<Warehouse> result = new List<Warehouse>();

        var allWarehouses = localWarehouses.Concat(enemyWarehouses);
        foreach (var warehouse in allWarehouses)
        {
            if (warehouse == null) continue;

            Vector2Int whGrid = new Vector2Int(
                Mathf.RoundToInt(warehouse.transform.position.x),
                Mathf.RoundToInt(warehouse.transform.position.y)
            );

            if (IsSameIsland(gridPos, whGrid))
            {
                result.Add(warehouse);
            }
        }

        return result;
    }

    /// <summary>
    /// Check if a position is on an island with enemy warehouse.
    /// </summary>
    public bool IsOnEnemyIsland(Vector2 position)
    {
        Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));

        foreach (var warehouse in enemyWarehouses)
        {
            if (warehouse == null) continue;

            Vector2Int whGrid = new Vector2Int(
                Mathf.RoundToInt(warehouse.transform.position.x),
                Mathf.RoundToInt(warehouse.transform.position.y)
            );

            if (IsSameIsland(gridPos, whGrid))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSameIsland(Vector2Int pos1, Vector2Int pos2)
    {
        // Beide müssen Land sein
        if (!IslandManager.IsLand(pos1) || !IslandManager.IsLand(pos2))
        {
            return false;
        }

        // Einfache Distanzprüfung für Inselzugehörigkeit
        // Bei größeren Inseln sollte ein echter Pfadfindungsalgorithmus verwendet werden
        float dist = Vector2Int.Distance(pos1, pos2);

        // Wenn nah beieinander, wahrscheinlich gleiche Insel
        // Dies ist eine vereinfachte Heuristik
        return dist < 60f;
    }

    /// <summary>
    /// Get total count of local warehouses.
    /// </summary>
    public int GetLocalWarehouseCount()
    {
        return localWarehouses.Count(w => w != null);
    }

    /// <summary>
    /// Get all local warehouses.
    /// </summary>
    public List<Warehouse> GetLocalWarehouses()
    {
        return new List<Warehouse>(localWarehouses.Where(w => w != null));
    }

    /// <summary>
    /// Handle warehouse capture - transfer ownership and resources.
    /// </summary>
    public void TransferWarehouseOwnership(Warehouse warehouse, int newOwnerPlayerId)
    {
        if (warehouse == null) return;

        // Remove from old list
        if (warehouse.isLocal)
        {
            localWarehouses.Remove(warehouse);
        }
        else
        {
            enemyWarehouses.Remove(warehouse);
        }

        // Update ownership
        warehouse.isLocal = false;

        // Add to new owner's list
        // Note: This is simplified - in multiplayer you'd need proper player ID tracking
        enemyWarehouses.Add(warehouse);

        Debug.Log($"[WarehouseManager] Warehouse captured by player {newOwnerPlayerId}");
    }
}