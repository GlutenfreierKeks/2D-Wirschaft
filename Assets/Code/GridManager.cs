using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid Settings")]
    [SerializeField] private int gridSize = 5000;
    [SerializeField] private Material gridMaterial;
    
    [Header("Spawning")]
    [SerializeField] private float spawnRadius = 800f;

    private GameObject gridObj;
    private Transform mainCamTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Ensure spawn radius is inside the grid
        float maxSafeRadius = (gridSize / 2f) * 0.8f;
        if (spawnRadius > maxSafeRadius)
        {
            spawnRadius = maxSafeRadius;
        }

        CreateGridPlane();
        if (Camera.main != null) mainCamTransform = Camera.main.transform;
    }

    private void CreateGridPlane()
    {
        gridObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        gridObj.name = "BackgroundGrid";
        gridObj.transform.SetParent(transform);
        gridObj.transform.position = Vector3.zero;
        // Large scale for safety, though it moves with camera now
        gridObj.transform.localScale = new Vector3(gridSize, gridSize, 1);
        
        Destroy(gridObj.GetComponent<MeshCollider>());

        if (gridMaterial != null)
        {
            gridObj.GetComponent<Renderer>().material = gridMaterial;
        }
    }

    private void Update()
    {
        if (mainCamTransform != null && gridObj != null)
        {
            // Move grid with camera to simulate infinity
            gridObj.transform.position = new Vector3(mainCamTransform.position.x, mainCamTransform.position.y, 10f);
        }
    }

    public int GetGridSize()
    {
        return gridSize;
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
