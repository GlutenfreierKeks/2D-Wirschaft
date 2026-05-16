using UnityEngine;
using System.Collections.Generic;

public enum IslandType { Plains, Desert, Jungle, Stone }

public class IslandManager : MonoBehaviour
{
    public static IslandManager Instance;

    [Header("Island Settings")]
    [SerializeField] private Material islandMaterial;
    [SerializeField] private int islandCount = 6;
    [SerializeField] private int blocksPerIsland = 1000;
    [SerializeField] private float mapMargin = 100f;
    [SerializeField] private float minDistanceBetweenIslands = 500f;

    private List<Vector2> islandPositions = new List<Vector2>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        GenerateIslands();
    }

    private void GenerateIslands()
    {
        float range = (GridManager.Instance != null) ? (GridManager.Instance.GetGridSize() / 2f) - mapMargin : 1000f;

        // Ensure at least one of each type
        IslandType[] allTypes = (IslandType[])System.Enum.GetValues(typeof(IslandType));
        
        int typeIndex = 0;
        int attempts = 0;
        while (islandPositions.Count < islandCount && attempts < 100)
        {
            attempts++;
            float x = Mathf.Round(Random.Range(-range, range));
            float y = Mathf.Round(Random.Range(-range, range));
            Vector2 newPos = new Vector2(x, y);

            bool tooClose = false;
            foreach (Vector2 p in islandPositions)
            {
                if (Vector2.Distance(p, newPos) < minDistanceBetweenIslands)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                // Cycle through types first, then random
                IslandType type = typeIndex < allTypes.Length ? allTypes[typeIndex] : (IslandType)Random.Range(0, allTypes.Length);
                typeIndex++;

                islandPositions.Add(newPos);
                CreateIslandMesh(newPos, type);
            }
        }
    }

    private void CreateIslandMesh(Vector2 startPos, IslandType type)
    {
        HashSet<Vector2> occupiedCells = new HashSet<Vector2>();
        occupiedCells.Add(startPos);
        List<Vector2> edgeGrowthCells = new List<Vector2> { startPos };

        // Directional bias for elongated shapes (like Italy)
        Vector2 biasDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        float biasStrength = 0.7f;

        for (int i = 0; i < blocksPerIsland; i++)
        {
            // Occasionally change bias to create "turns"
            if (i % (blocksPerIsland / 4) == 0)
            {
                biasDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            }

            int index = (Random.value > 0.6f) ? edgeGrowthCells.Count - 1 : Random.Range(0, edgeGrowthCells.Count);
            Vector2 current = edgeGrowthCells[index];

            // Prioritize neighbors that align with biasDir
            Vector2[] neighbors = { current + Vector2.up, current + Vector2.down, current + Vector2.left, current + Vector2.right };
            
            // Shuffle neighbors but weight them by bias
            System.Array.Sort(neighbors, (a, b) => {
                float scoreA = Vector2.Dot((a - current), biasDir) + Random.Range(-0.5f, 0.5f);
                float scoreB = Vector2.Dot((b - current), biasDir) + Random.Range(-0.5f, 0.5f);
                return scoreB.CompareTo(scoreA); // High score first
            });

            foreach (Vector2 next in neighbors)
            {
                if (!occupiedCells.Contains(next))
                {
                    occupiedCells.Add(next);
                    edgeGrowthCells.Add(next);
                    if (Random.value > 0.3f) break; // High chance to move to next block
                }
            }
        }

        // Fill holes (post-generation pass)
        for (int iteration = 0; iteration < 2; iteration++)
        {
            List<Vector2> holesToFill = new List<Vector2>();
            foreach (Vector2 cell in occupiedCells)
            {
                foreach (Vector2 n in new Vector2[] { cell + Vector2.up, cell + Vector2.down, cell + Vector2.left, cell + Vector2.right })
                {
                    if (!occupiedCells.Contains(n))
                    {
                        int landCount = 0;
                        foreach (Vector2 n2 in new Vector2[] { n + Vector2.up, n + Vector2.down, n + Vector2.left, n + Vector2.right })
                            if (occupiedCells.Contains(n2)) landCount++;
                        if (landCount >= 3) holesToFill.Add(n); 
                    }
                }
            }
            foreach (Vector2 hole in holesToFill) occupiedCells.Add(hole);
        }

        GameObject islandObj = new GameObject($"Island_{type}_{startPos}");
        islandObj.transform.SetParent(transform);
        islandObj.transform.position = new Vector3(0, 0, -0.1f);

        MeshFilter meshFilter = islandObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = islandObj.AddComponent<MeshRenderer>();
        meshRenderer.material = islandMaterial;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();
        int vIndex = 0;

        // Colors for types
        Color centerColor = Color.green;
        Color borderColor = Color.yellow;

        switch (type)
        {
            case IslandType.Desert: centerColor = new Color(0.95f, 0.85f, 0.5f); borderColor = new Color(0.8f, 0.7f, 0.4f); break;
            case IslandType.Jungle: centerColor = new Color(0.1f, 0.4f, 0.1f); borderColor = new Color(0.3f, 0.5f, 0.2f); break;
            case IslandType.Stone: centerColor = new Color(0.5f, 0.5f, 0.5f); borderColor = new Color(0.3f, 0.3f, 0.3f); break;
        }

        foreach (Vector2 cell in occupiedCells)
        {
            bool isBorder = false;
            foreach (Vector2 n in new Vector2[] { cell + Vector2.up, cell + Vector2.down, cell + Vector2.left, cell + Vector2.right })
            {
                if (!occupiedCells.Contains(n)) { isBorder = true; break; }
            }

            Color cellColor = isBorder ? borderColor : centerColor;
            vertices.Add(new Vector3(cell.x - 0.5f, cell.y - 0.5f, 0));
            vertices.Add(new Vector3(cell.x - 0.5f, cell.y + 0.5f, 0));
            vertices.Add(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0));
            vertices.Add(new Vector3(cell.x + 0.5f, cell.y - 0.5f, 0));
            for (int j = 0; j < 4; j++) colors.Add(cellColor);
            triangles.Add(vIndex); triangles.Add(vIndex + 1); triangles.Add(vIndex + 2);
            triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);
            vIndex += 4;
        }

        Mesh mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }

    public Vector2 GetIslandPosition(int index)
    {
        if (islandPositions.Count == 0) return Vector2.zero;
        return islandPositions[index % islandPositions.Count];
    }
}
