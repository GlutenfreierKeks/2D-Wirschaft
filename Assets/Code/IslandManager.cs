using UnityEngine;
using System.Collections.Generic;

public enum IslandType { Plains, Desert, Jungle, Stone }
public enum ResourceType { Wood, Stone, Iron, Gold, Animal, Fruit, Wheat, None }

public class IslandManager : MonoBehaviour
{
    public static IslandManager Instance;
    private static Dictionary<Vector2, ResourceType> resourceNodes = new Dictionary<Vector2, ResourceType>();

    public static ResourceType GetResourceType(Vector2 pos)
    {
        Vector2 snapped = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
        return resourceNodes.TryGetValue(snapped, out ResourceType type) ? type : ResourceType.None;
    }

    [Header("Island Settings")]
    [SerializeField] private Material islandMaterial;
    [SerializeField] private int islandCount = 6;
    [SerializeField] private int blocksPerIsland = 1000;
    [SerializeField] private float mapMargin = 100f;
    [SerializeField] private float minDistanceBetweenIslands = 500f;

    private List<Vector2> islandPositions = new List<Vector2>();
    private List<IslandType> islandTypes = new List<IslandType>();
    private static HashSet<Vector2> allLandCells = new HashSet<Vector2>();
    private static Sprite defaultNodeSprite;

    public static bool IsLand(Vector2 pos)
    {
        return allLandCells.Contains(new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)));
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        allLandCells.Clear();
        resourceNodes.Clear();

        if (defaultNodeSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // Use 1.0f pixels per unit so a 1x1 texture = 1x1 world units
            defaultNodeSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1.0f);
        }
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
                islandTypes.Add(type);
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

        // Register land cells for placement checks
        foreach (Vector2 cell in occupiedCells) allLandCells.Add(cell);

        // Biome-specific resource node distribution
        DistributeResources(occupiedCells, type);

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

    private void DistributeResources(HashSet<Vector2> cells, IslandType biome)
    {
        List<Vector2> cellList = new List<Vector2>(cells);
        if (cellList.Count == 0) return;

        List<ResourceType> primaryTypes = GetPrimaryResourcesForBiome(biome);
        
        // Loop through ALL resource types (except None)
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.None) continue;

            bool isPrimary = primaryTypes.Contains(type);
            
            // Determine cluster size based on whether it's primary or not
            int clusterCount = isPrimary ? Random.Range(2, 4) : 1;
            int clusterRadius = isPrimary ? Random.Range(4, 7) : Random.Range(1, 3);
            float spawnChance = isPrimary ? 0.8f : 0.4f;

            for (int h = 0; h < clusterCount; h++)
            {
                Vector2 hubCenter = cellList[Random.Range(0, cellList.Count)];
                CreateResourceCluster(hubCenter, type, clusterRadius, spawnChance);
            }
        }
    }

    private void CreateResourceCluster(Vector2 center, ResourceType type, int radius, float spawnChance = 0.8f)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    Vector2 pos = new Vector2(Mathf.Round(center.x + x), Mathf.Round(center.y + y));
                    if (IsLand(pos) && !resourceNodes.ContainsKey(pos))
                    {
                        if (Random.value < spawnChance) SpawnResourceNode(pos, type);
                    }
                }
            }
        }
    }

    private List<ResourceType> GetPrimaryResourcesForBiome(IslandType biome)
    {
        switch (biome)
        {
            case IslandType.Desert: return new List<ResourceType> { ResourceType.Iron, ResourceType.Gold };
            case IslandType.Stone: return new List<ResourceType> { ResourceType.Stone, ResourceType.Iron };
            case IslandType.Jungle: return new List<ResourceType> { ResourceType.Wood };
            case IslandType.Plains: return new List<ResourceType> { ResourceType.Wheat };
        }
        return new List<ResourceType>();
    }

    private ResourceType PickRandomSecondary(IslandType biome)
    {
        float r = Random.value;
        if (r > 0.3f) return ResourceType.None;

        switch (biome)
        {
            case IslandType.Desert: return ResourceType.Fruit;
            case IslandType.Stone: return ResourceType.Gold;
            case IslandType.Jungle: return ResourceType.Fruit;
            case IslandType.Plains: return ResourceType.Animal;
        }
        return ResourceType.None;
    }

    private void SpawnResourceNode(Vector2 pos, ResourceType type)
    {
        resourceNodes[pos] = type;

        // Create a visual icon for the node
        GameObject node = new GameObject($"Node_{type}");
        node.transform.position = new Vector3(pos.x, pos.y, -0.15f);
        node.transform.localScale = Vector3.one;
        
        SpriteRenderer sr = node.AddComponent<SpriteRenderer>();
        sr.sprite = Resources.Load<Sprite>($"{type}_Icon"); 
        if (sr.sprite == null) sr.sprite = Resources.Load<Sprite>($"{type}_Overlay");
        
        sr.sortingOrder = 11; 
        if (sr.sprite == null)
        {
            sr.sprite = defaultNodeSprite;
            sr.color = GetColorForResource(type);
        }
    }

    private Color GetColorForResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return new Color(0.4f, 0.25f, 0.1f); 
            case ResourceType.Stone: return Color.gray;
            case ResourceType.Iron: return new Color(0.7f, 0.7f, 0.8f); // Metallic Blue-Gray
            case ResourceType.Gold: return new Color(1f, 0.85f, 0f); // Bright Gold
            case ResourceType.Animal: return Color.white;
            case ResourceType.Fruit: return Color.magenta;
            case ResourceType.Wheat: return new Color(0.9f, 0.8f, 0.2f); 
        }
        return Color.clear;
    }

    public Vector2 GetIslandPosition(int index)
    {
        if (islandPositions.Count == 0) return Vector2.zero;
        return islandPositions[index % islandPositions.Count];
    }

    public IslandType GetIslandType(int index)
    {
        if (islandTypes.Count == 0) return IslandType.Plains;
        return islandTypes[index % islandTypes.Count];
    }
}
