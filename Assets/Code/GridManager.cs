using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid Settings")]
    [SerializeField] private int gridSize = 2000;
    [SerializeField] private Material gridMaterial;
    
    [Header("Spawning")]
    [SerializeField] private float spawnRadius = 800f; // Distance from center

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        CreateGridPlane();
    }

    private void CreateGridPlane()
    {
        // Create a large quad to host the grid shader
        GameObject gridObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        gridObj.name = "BackgroundGrid";
        gridObj.transform.SetParent(transform);
        gridObj.transform.position = Vector3.zero;
        gridObj.transform.localScale = new Vector3(gridSize, gridSize, 1);
        
        // Remove collider if you don't need it for clicking the ground
        Destroy(gridObj.GetComponent<MeshCollider>());

        if (gridMaterial != null)
        {
            gridObj.GetComponent<Renderer>().material = gridMaterial;
        }
        else
        {
            Debug.LogWarning("Grid Material not assigned to GridManager!");
        }
    }

    public Vector3 GetSpawnPosition(int playerIndex, int totalPlayers)
    {
        if (totalPlayers <= 1) return Vector3.zero;

        // Calculate position on a circle for even distribution
        float angle = playerIndex * Mathf.PI * 2f / totalPlayers;
        float x = Mathf.Cos(angle) * spawnRadius;
        float y = Mathf.Sin(angle) * spawnRadius;

        return new Vector3(x, y, 0);
    }
}
