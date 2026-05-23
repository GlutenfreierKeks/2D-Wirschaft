using UnityEngine;
using System.Collections.Generic;

public class Steg : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

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
}
