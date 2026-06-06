using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid Settings")]
    [SerializeField] private int gridSize = 1800;
    [SerializeField] private Material gridMaterial;
    
    [Header("Spawning")]
    [SerializeField] private float spawnRadius = 300f;

    private GameObject gridObj;
    private Transform mainCamTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        ApplyLobbySettings();

        // Ensure spawn radius is inside the grid
        float maxSafeRadius = (gridSize / 2f) * 0.8f;
        if (spawnRadius > maxSafeRadius)
        {
            spawnRadius = maxSafeRadius;
        }

        CreateGridPlane();
        CreateBorder();
        if (Camera.main != null) mainCamTransform = Camera.main.transform;
    }

    private void ApplyLobbySettings()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LobbySettingsKeys.WorldSize, out object worldSizeObj))
        {
            string preset = worldSizeObj.ToString();
            switch (preset)
            {
                case "Kompakt":
                    gridSize = 1400;
                    spawnRadius = 220f;
                    break;
                case "Gross":
                    gridSize = 2600;
                    spawnRadius = 420f;
                    break;
                default:
                    gridSize = 1800;
                    spawnRadius = 300f;
                    break;
            }
        }
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

    private void CreateBorder()
    {
        float half = gridSize / 2f;
        float thickness = 20f;
        // Dark navy / deep-sea colour so the border reads clearly against the ocean
        Color borderColor = new Color(0.04f, 0.07f, 0.22f, 1f);

        GameObject borderParent = new GameObject("MapBorder");
        borderParent.transform.SetParent(transform);
        borderParent.transform.position = Vector3.zero;

        // (position, scale) for Top / Bottom / Left / Right sides
        Vector3[] positions = {
            new Vector3(0f,  half + thickness * 0.5f, -0.06f),
            new Vector3(0f, -half - thickness * 0.5f, -0.06f),
            new Vector3(-half - thickness * 0.5f, 0f, -0.06f),
            new Vector3( half + thickness * 0.5f, 0f, -0.06f)
        };
        Vector3[] scales = {
            new Vector3(gridSize + thickness * 2f, thickness, 1f),
            new Vector3(gridSize + thickness * 2f, thickness, 1f),
            new Vector3(thickness, gridSize, 1f),
            new Vector3(thickness, gridSize, 1f)
        };

        Material borderMat = new Material(Shader.Find("Sprites/Default"));
        borderMat.color = borderColor;

        for (int i = 0; i < 4; i++)
        {
            GameObject side = GameObject.CreatePrimitive(PrimitiveType.Quad);
            side.name = "BorderSide_" + i;
            side.transform.SetParent(borderParent.transform);
            side.transform.position = positions[i];
            side.transform.localScale  = scales[i];
            Destroy(side.GetComponent<MeshCollider>());
            side.GetComponent<Renderer>().material = borderMat;
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
