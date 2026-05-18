using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

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

    /// <summary>
    /// Entfernt eine Rohstoffquelle am angegebenen Punkt (zerstört das Icon).
    /// </summary>
    public static void RemoveResourceNodeAt(Vector2 pos)
    {
        Vector2 snapped = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
        if (resourceNodes.ContainsKey(snapped))
        {
            resourceNodes.Remove(snapped);
        }

        if (Instance != null)
        {
            for (int i = Instance.resourceNodeRenderers.Count - 1; i >= 0; i--)
            {
                var entry = Instance.resourceNodeRenderers[i];
                if (new Vector2(Mathf.Round(entry.pos.x), Mathf.Round(entry.pos.y)) == snapped)
                {
                    if (entry.sr != null && entry.sr.gameObject != null)
                    {
                        Destroy(entry.sr.gameObject);
                    }
                    Instance.resourceNodeRenderers.RemoveAt(i);
                }
            }
        }
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

        ApplyLobbySettings();

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
                    islandCount = 4;
                    blocksPerIsland = 700;
                    minDistanceBetweenIslands = 360f;
                    mapMargin = 80f;
                    break;
                case "Gross":
                    islandCount = 8;
                    blocksPerIsland = 1350;
                    minDistanceBetweenIslands = 620f;
                    mapMargin = 120f;
                    break;
                default:
                    islandCount = 6;
                    blocksPerIsland = 1000;
                    minDistanceBetweenIslands = 500f;
                    mapMargin = 100f;
                    break;
            }
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

        int attempts = 0;
        while (occupiedCells.Count < blocksPerIsland && attempts < blocksPerIsland * 10)
        {
            attempts++;
            if (attempts % (blocksPerIsland / 4) == 0)
                biasDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

            int index = (Random.value > 0.6f) ? edgeGrowthCells.Count - 1 : Random.Range(0, edgeGrowthCells.Count);
            Vector2 current = edgeGrowthCells[index];

            Vector2[] neighbors = { current + Vector2.up, current + Vector2.down, current + Vector2.left, current + Vector2.right };

            System.Array.Sort(neighbors, (a, b) => {
                float scoreA = Vector2.Dot((a - current), biasDir) + Random.Range(-0.5f, 0.5f);
                float scoreB = Vector2.Dot((b - current), biasDir) + Random.Range(-0.5f, 0.5f);
                return scoreB.CompareTo(scoreA);
            });

            bool addedAny = false;
            foreach (Vector2 next in neighbors)
            {
                if (!occupiedCells.Contains(next))
                {
                    occupiedCells.Add(next);
                    edgeGrowthCells.Add(next);
                    addedAny = true;
                    if (Random.value > 0.3f) break;
                }
            }

            // Remove interior cells from growth list to optimize
            if (!addedAny)
            {
                bool completelySurrounded = true;
                foreach (Vector2 n in neighbors)
                {
                    if (!occupiedCells.Contains(n))
                    {
                        completelySurrounded = false;
                        break;
                    }
                }
                if (completelySurrounded)
                {
                    edgeGrowthCells.RemoveAt(index);
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

        // 1. Add logical land cells
        foreach (Vector2 cell in occupiedCells) allLandCells.Add(cell);

        // Distribute resources based on logical occupiedCells
        DistributeResources(occupiedCells, type);

        // 2. Visuell vergrößerte Kacheln für weichen Übergang (3 zusätzliche Kachel-Ebenen hinzufügen für tieferen Overlap)
        HashSet<Vector2> visualCells = new HashSet<Vector2>(occupiedCells);
        HashSet<Vector2> layer1 = new HashSet<Vector2>();
        foreach (Vector2 cell in visualCells)
        {
            foreach (Vector2 n in new Vector2[] { cell + Vector2.up, cell + Vector2.down, cell + Vector2.left, cell + Vector2.right })
            {
                if (!visualCells.Contains(n)) layer1.Add(n);
            }
        }
        foreach (var c in layer1) visualCells.Add(c);

        HashSet<Vector2> layer2 = new HashSet<Vector2>();
        foreach (Vector2 cell in layer1)
        {
            foreach (Vector2 n in new Vector2[] { cell + Vector2.up, cell + Vector2.down, cell + Vector2.left, cell + Vector2.right })
            {
                if (!visualCells.Contains(n)) layer2.Add(n);
            }
        }
        foreach (var c in layer2) visualCells.Add(c);

        HashSet<Vector2> layer3 = new HashSet<Vector2>();
        foreach (Vector2 cell in layer2)
        {
            foreach (Vector2 n in new Vector2[] { cell + Vector2.up, cell + Vector2.down, cell + Vector2.left, cell + Vector2.right })
            {
                if (!visualCells.Contains(n)) layer3.Add(n);
            }
        }
        foreach (var c in layer3) visualCells.Add(c);

        // 3. Abstand zum Ozean für weiches Alpha-Blending berechnen (BFS)
        Dictionary<Vector2, int> distToOcean = new Dictionary<Vector2, int>();
        Queue<Vector2> queue = new Queue<Vector2>();

        foreach (Vector2 cell in visualCells)
        {
            bool isOuterBorder = false;
            foreach (Vector2 n in new Vector2[] { cell + Vector2.up, cell + Vector2.down, cell + Vector2.left, cell + Vector2.right })
            {
                if (!visualCells.Contains(n))
                {
                    isOuterBorder = true;
                    break;
                }
            }
            if (isOuterBorder)
            {
                distToOcean[cell] = 1;
                queue.Enqueue(cell);
            }
        }

        while (queue.Count > 0)
        {
            Vector2 curr = queue.Dequeue();
            int curDist = distToOcean[curr];
            foreach (Vector2 n in new Vector2[] { curr + Vector2.up, curr + Vector2.down, curr + Vector2.left, curr + Vector2.right })
            {
                if (visualCells.Contains(n) && !distToOcean.ContainsKey(n))
                {
                    distToOcean[n] = curDist + 1;
                    queue.Enqueue(n);
                }
            }
        }

        // 4. Bounding Box für UV-Koordinaten berechnen
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector2 cell in visualCells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        float boundingWidth = (maxX - minX) + 1f;
        float boundingHeight = (maxY - minY) + 1f;

        // Build visual mesh
        GameObject islandObj = new GameObject($"Island_{type}_{startPos}");
        islandObj.transform.SetParent(transform);
        // Weisen jedem Insel-Mesh eine geringfügig unterschiedliche Z-Ebene zu (transform.childCount),
        // damit sich überlappende Inseln ohne Z-Fighting perfekt rendern und mischen lassen!
        float zOffset = -0.1f - (transform.childCount * 0.005f);
        islandObj.transform.position = new Vector3(0, 0, zOffset);

        MeshFilter meshFilter = islandObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = islandObj.AddComponent<MeshRenderer>();

        // Material-Liste mit allen Textur-Variationen für dieses Biom laden
        List<string> texNames = GetTextureNamesForBiome(type);
        List<Material> materials = new List<Material>();
        foreach (string tName in texNames)
        {
            Texture2D texture = Resources.Load<Texture2D>($"Textures/{tName}");
            Material mat = new Material(Shader.Find("Sprites/Default"));
            if (texture != null)
            {
                mat.mainTexture = texture;
            }
            else
            {
                Debug.LogError($"[IslandManager] Hintergrund-Textur nicht gefunden: Textures/{tName}");
            }
            materials.Add(mat);
        }
        meshRenderer.materials = materials.ToArray();

        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();
        
        // Liste von Dreiecken für jedes Submesh (jede Textur-Variation)
        List<List<int>> submeshTriangles = new List<List<int>>();
        for (int i = 0; i < texNames.Count; i++) submeshTriangles.Add(new List<int>());
        
        int vIndex = 0;

        foreach (Vector2 cell in visualCells)
        {
            // Alpha-Wert basierend auf Abstand zum Ozean
            float alpha = 1.0f;
            if (distToOcean.TryGetValue(cell, out int d))
            {
                // Über 4 Kacheln extrem weich ausblenden
                alpha = Mathf.Clamp01((d - 1) / 4.0f);
            }

            Color cellColor = new Color(1f, 1f, 1f, alpha);

            // 4 Eckpunkte des Quads
            Vector3 v0 = new Vector3(cell.x - 0.5f, cell.y - 0.5f, 0f);
            Vector3 v1 = new Vector3(cell.x - 0.5f, cell.y + 0.5f, 0f);
            Vector3 v2 = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            Vector3 v3 = new Vector3(cell.x + 0.5f, cell.y - 0.5f, 0f);

            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            // UV-Koordinaten basierend auf absoluter Position für perfektes Kachel-Tiling (1 Texturbild alle 2x2 Plots)
            float tileSize = 2f;
            uvs.Add(new Vector2(v0.x / tileSize, v0.y / tileSize));
            uvs.Add(new Vector2(v1.x / tileSize, v1.y / tileSize));
            uvs.Add(new Vector2(v2.x / tileSize, v2.y / tileSize));
            uvs.Add(new Vector2(v3.x / tileSize, v3.y / tileSize));

            for (int j = 0; j < 4; j++) colors.Add(cellColor);

            // Kachel zufällig einer der Textur-Variationen (Submeshes) zuweisen
            // Wir verwenden einen deterministischen Hash der Kachel-Position, damit das Muster stabil bleibt und nicht flimmert!
            int hash = Mathf.Abs((int)(cell.x * 73856093) ^ (int)(cell.y * 19349663));
            int submeshIndex = hash % texNames.Count;

            submeshTriangles[submeshIndex].Add(vIndex);
            submeshTriangles[submeshIndex].Add(vIndex + 1);
            submeshTriangles[submeshIndex].Add(vIndex + 2);
            submeshTriangles[submeshIndex].Add(vIndex);
            submeshTriangles[submeshIndex].Add(vIndex + 2);
            submeshTriangles[submeshIndex].Add(vIndex + 3);
            vIndex += 4;
        }

        Mesh mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.subMeshCount = texNames.Count;
        
        for (int i = 0; i < texNames.Count; i++)
        {
            mesh.SetTriangles(submeshTriangles[i].ToArray(), i);
        }
        
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }

    private List<string> GetTextureNamesForBiome(IslandType type)
    {
        List<string> texNames = new List<string>();
        switch (type)
        {
            case IslandType.Plains:
                texNames.Add("Plains_Background");
                texNames.Add("Plains_Background2");
                texNames.Add("Plains_Background3");
                texNames.Add("Plains_Background4");
                texNames.Add("Plains_Background5");
                break;
            case IslandType.Desert:
                texNames.Add("Wueste_Background");
                texNames.Add("Wueste_Background1");
                texNames.Add("Wueste_Background2");
                texNames.Add("Wueste_Background3");
                texNames.Add("Wueste_Background5");
                break;
            case IslandType.Jungle:
                texNames.Add("Jungle_Background");
                texNames.Add("Jungle_Background1");
                texNames.Add("Jungle_Background2");
                texNames.Add("Jungle_Background3");
                texNames.Add("Jungle_Background4");
                texNames.Add("Jungle_Background5");
                break;
            case IslandType.Stone:
                texNames.Add("Stone_Background");
                texNames.Add("Stone_Background2");
                texNames.Add("Stone_Background3");
                texNames.Add("Stone_Background4");
                texNames.Add("Stone_Background5");
                break;
        }
        return texNames;
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
