using UnityEngine;
using System.Collections.Generic;

public enum IslandType { Plains, Desert, Jungle, Stone }
public enum ResourceType { Wood, Stone, Iron, Gold, Animal, Fruit, Wheat, None }

public class IslandManager : MonoBehaviour
{
    public static IslandManager Instance;
    private static Dictionary<Vector2, ResourceType> resourceNodes = new Dictionary<Vector2, ResourceType>();

    // Track node SpriteRenderers so we can hide them in fog
    private List<(Vector2 pos, SpriteRenderer sr)> resourceNodeRenderers = new List<(Vector2, SpriteRenderer)>();

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
        resourceNodeRenderers.Clear();

        if (defaultNodeSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            defaultNodeSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1.0f);
        }
    }

    private void Start()
    {
        GenerateIslands();
    }

    private void Update()
    {
        // Hide resource nodes that are still covered by fog
        foreach (var entry in resourceNodeRenderers)
        {
            if (entry.sr == null) continue;
            entry.sr.enabled = FogProjector.IsExplored(entry.pos);
        }
    }

    private void GenerateIslands()
    {
        float range = (GridManager.Instance != null) ? (GridManager.Instance.GetGridSize() / 2f) - mapMargin : 1000f;

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

        Vector2 biasDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

        for (int i = 0; i < blocksPerIsland; i++)
        {
            if (i % (blocksPerIsland / 4) == 0)
                biasDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

            int index = (Random.value > 0.6f) ? edgeGrowthCells.Count - 1 : Random.Range(0, edgeGrowthCells.Count);
            Vector2 current = edgeGrowthCells[index];

            Vector2[] neighbors = { current + Vector2.up, current + Vector2.down, current + Vector2.left, current + Vector2.right };

            System.Array.Sort(neighbors, (a, b) => {
                float scoreA = Vector2.Dot((a - current), biasDir) + Random.Range(-0.5f, 0.5f);
                float scoreB = Vector2.Dot((b - current), biasDir) + Random.Range(-0.5f, 0.5f);
                return scoreB.CompareTo(scoreA);
            });

            foreach (Vector2 next in neighbors)
            {
                if (!occupiedCells.Contains(next))
                {
                    occupiedCells.Add(next);
                    edgeGrowthCells.Add(next);
                    if (Random.value > 0.3f) break;
                }
            }
        }

        // Fill holes
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

        foreach (Vector2 cell in occupiedCells) allLandCells.Add(cell);

        DistributeResources(occupiedCells, type);

        // Build mesh
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

        Color centerColor = Color.green;
        Color borderColor = Color.yellow;

        switch (type)
        {
            case IslandType.Desert: centerColor = new Color(0.95f, 0.85f, 0.5f); borderColor = new Color(0.8f, 0.7f, 0.4f); break;
            case IslandType.Jungle: centerColor = new Color(0.1f, 0.4f, 0.1f); borderColor = new Color(0.3f, 0.5f, 0.2f); break;
            case IslandType.Stone:  centerColor = new Color(0.5f, 0.5f, 0.5f); borderColor = new Color(0.3f, 0.3f, 0.3f); break;
        }

        foreach (Vector2 cell in occupiedCells)
        {
            bool isBorder = false;
            foreach (Vector2 n in new Vector2[] { cell + Vector2.up, cell + Vector2.down, cell + Vector2.left, cell + Vector2.right })
                if (!occupiedCells.Contains(n)) { isBorder = true; break; }

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

    // -------------------------------------------------------------------------
    // Resource distribution (biome-aware, every island gets ALL resource types)
    // -------------------------------------------------------------------------

    private struct ResourceConfig
    {
        public ResourceType type;
        public int clusterCount;
        public int clusterRadius;
        public float spawnChance;
        public ResourceConfig(ResourceType t, int count, int radius, float chance)
        { type = t; clusterCount = count; clusterRadius = radius; spawnChance = chance; }
    }

    private void DistributeResources(HashSet<Vector2> cells, IslandType biome)
    {
        List<Vector2> cellList = new List<Vector2>(cells);
        if (cellList.Count == 0) return;

        ResourceConfig[] configs = GetResourceConfigsForBiome(biome);
        foreach (var cfg in configs)
        {
            for (int h = 0; h < cfg.clusterCount; h++)
            {
                Vector2 hubCenter = cellList[Random.Range(0, cellList.Count)];
                CreateResourceCluster(hubCenter, cfg.type, cfg.clusterRadius, cfg.spawnChance);
            }
        }
    }

    private ResourceConfig[] GetResourceConfigsForBiome(IslandType biome)
    {
        switch (biome)
        {
            // Plains: viel Weizen, Holz, etwas von allem, wenig Eisen/Gold
            case IslandType.Plains:
                return new ResourceConfig[]
                {
                    new ResourceConfig(ResourceType.Wheat,  3, 6, 0.85f),
                    new ResourceConfig(ResourceType.Wood,   2, 4, 0.65f),
                    new ResourceConfig(ResourceType.Stone,  1, 3, 0.55f),
                    new ResourceConfig(ResourceType.Fruit,  1, 3, 0.50f),
                    new ResourceConfig(ResourceType.Animal, 1, 3, 0.50f),
                    new ResourceConfig(ResourceType.Iron,   1, 2, 0.40f),
                    new ResourceConfig(ResourceType.Gold,   1, 1, 0.30f),
                };
            // Wüste: viel Frucht, viel Stein, etwas Gold, wenig Holz/Weizen/Eisen
            case IslandType.Desert:
                return new ResourceConfig[]
                {
                    new ResourceConfig(ResourceType.Fruit,  3, 5, 0.80f),
                    new ResourceConfig(ResourceType.Stone,  3, 5, 0.80f),
                    new ResourceConfig(ResourceType.Gold,   2, 3, 0.60f),
                    new ResourceConfig(ResourceType.Iron,   1, 2, 0.30f),
                    new ResourceConfig(ResourceType.Wood,   1, 2, 0.30f),
                    new ResourceConfig(ResourceType.Wheat,  1, 2, 0.30f),
                    new ResourceConfig(ResourceType.Animal, 1, 1, 0.25f),
                };
            // Jungle: viel Holz, viel Eisen, etwas Frucht, wenig Rest
            case IslandType.Jungle:
                return new ResourceConfig[]
                {
                    new ResourceConfig(ResourceType.Wood,   3, 6, 0.90f),
                    new ResourceConfig(ResourceType.Iron,   2, 4, 0.70f),
                    new ResourceConfig(ResourceType.Fruit,  2, 3, 0.55f),
                    new ResourceConfig(ResourceType.Stone,  1, 2, 0.40f),
                    new ResourceConfig(ResourceType.Animal, 1, 2, 0.40f),
                    new ResourceConfig(ResourceType.Wheat,  1, 2, 0.30f),
                    new ResourceConfig(ResourceType.Gold,   1, 1, 0.25f),
                };
            // Stein: viel Stein, Eisen, Gold – wenig organisches
            case IslandType.Stone:
                return new ResourceConfig[]
                {
                    new ResourceConfig(ResourceType.Stone,  3, 6, 0.90f),
                    new ResourceConfig(ResourceType.Iron,   3, 5, 0.80f),
                    new ResourceConfig(ResourceType.Gold,   2, 4, 0.70f),
                    new ResourceConfig(ResourceType.Wood,   1, 2, 0.30f),
                    new ResourceConfig(ResourceType.Wheat,  1, 1, 0.25f),
                    new ResourceConfig(ResourceType.Fruit,  1, 1, 0.25f),
                    new ResourceConfig(ResourceType.Animal, 1, 1, 0.25f),
                };
        }
        return new ResourceConfig[0];
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

    private void SpawnResourceNode(Vector2 pos, ResourceType type)
    {
        resourceNodes[pos] = type;

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

        // Start hidden – Update() will reveal when explored
        sr.enabled = false;

        // Track for fog toggling
        resourceNodeRenderers.Add((pos, sr));
    }

    private Color GetColorForResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:   return new Color(0.4f, 0.25f, 0.1f);
            case ResourceType.Stone:  return Color.gray;
            case ResourceType.Iron:   return new Color(0.7f, 0.7f, 0.8f);
            case ResourceType.Gold:   return new Color(1f, 0.85f, 0f);
            case ResourceType.Animal: return Color.white;
            case ResourceType.Fruit:  return Color.magenta;
            case ResourceType.Wheat:  return new Color(0.9f, 0.8f, 0.2f);
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
