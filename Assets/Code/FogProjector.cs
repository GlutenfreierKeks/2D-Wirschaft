using UnityEngine;
using System.Collections.Generic;

public class FogProjector : MonoBehaviour
{
    private static List<FogRevealer> revealers = new List<FogRevealer>();

    [Header("Fog Settings")]
    [SerializeField] private Material fogMaterial;
    [SerializeField] private int maskResolution = 1024;
    [SerializeField] private float mapSize = 2000f;

    private RenderTexture maskTexture;
    private RenderTexture exploredTexture;
    private Camera maskCamera;
    private Camera exploredCamera;
    private GameObject fogQuad;

    private static HashSet<Vector2> exploredCells = new HashSet<Vector2>();
    private static int explorationVersion = 0;

    private GameObject exploredMeshObj;
    private Mesh exploredMesh;
    private int cachedExploredVersion = -1;

    public static bool IsExplored(Vector2 pos)
    {
        return exploredCells.Contains(new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)));
    }

    public static void RegisterExploration(Vector2 center, float radius)
    {
        int r = Mathf.CeilToInt(radius);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x*x + y*y <= radius*radius)
                {
                    Vector2 cell = new Vector2(Mathf.Round(center.x + x), Mathf.Round(center.y + y));
                    if (exploredCells.Add(cell))
                        explorationVersion++;
                }
            }
        }
    }

    public static void AddRevealer(FogRevealer r) => revealers.Add(r);
    public static void RemoveRevealer(FogRevealer r) => revealers.Remove(r);

    private void Start()
    {
        if (GridManager.Instance != null)
        {
            int gridSize = GridManager.Instance.GetGridSize();
            mapSize = Mathf.Max(mapSize, gridSize * 1.5f);
        }

        SetupFog();
    }

    private void SetupFog()
    {
        if (fogMaterial == null)
        {
            Shader fogShader = Shader.Find("Custom/CloudFogShader");
            Shader fallbackFog = fogShader ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Hidden/Internal-Colored");
            if (fallbackFog == null)
            {
                Debug.LogError("[FogProjector] Kein Nebel-Shader gefunden. FogProjector wird deaktiviert.");
                enabled = false;
                return;
            }

            if (fogShader != null)
            {
                fogMaterial = new Material(fogShader);
            }
            else
            {
                Debug.LogWarning("[FogProjector] Kein FogMaterial zugewiesen und Custom/CloudFogShader nicht gefunden. Verwende einen Fallback-Material.");
                fogMaterial = new Material(fallbackFog);
                fogMaterial.color = new Color(0.2f, 0.2f, 0.2f, 0.75f);
            }
        }

        maskTexture = new RenderTexture(maskResolution, maskResolution, 24);
        maskTexture.filterMode = FilterMode.Bilinear;

        exploredTexture = new RenderTexture(maskResolution, maskResolution, 0, RenderTextureFormat.ARGB32);
        exploredTexture.filterMode = FilterMode.Bilinear;
        exploredTexture.Create();
        RenderTexture.active = exploredTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = null;

        CreateMaskCamera();

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.cullingMask &= ~(1 << 31);
        }
        else
        {
            Debug.LogWarning("[FogProjector] Keine Hauptkamera gefunden. Layer 31 wird nicht ausgeblendet.");
        }

        CreateExploredCamera();
        CreateFogOverlay();
    }

    private void CreateMaskCamera()
    {
        GameObject camObj = new GameObject("FogMaskCamera");
        camObj.transform.position = new Vector3(0, 0, -50f);
        maskCamera = camObj.AddComponent<Camera>();
        maskCamera.orthographic = true;
        maskCamera.orthographicSize = mapSize / 2f;
        maskCamera.targetTexture = maskTexture;
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = Color.black;
        maskCamera.cullingMask = 1 << 31;
        maskCamera.depth = 1;
    }

    private void CreateExploredCamera()
    {
        GameObject camObj = new GameObject("ExploredMaskCamera");
        camObj.transform.SetParent(transform);
        camObj.transform.position = new Vector3(0, 0, -49f);
        exploredCamera = camObj.AddComponent<Camera>();
        exploredCamera.orthographic = true;
        exploredCamera.orthographicSize = mapSize / 2f;
        exploredCamera.targetTexture = exploredTexture;
        exploredCamera.clearFlags = CameraClearFlags.Nothing;
        exploredCamera.backgroundColor = Color.black;
        exploredCamera.cullingMask = 1 << 30;
        exploredCamera.depth = 0;
        exploredCamera.enabled = false;
    }

    private void CreateFogOverlay()
    {
        fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fogQuad.name = "FogOverlay";
        fogQuad.transform.position = new Vector3(0, 0, -5f);
        fogQuad.transform.localScale = new Vector3(mapSize, mapSize, 1);
        Destroy(fogQuad.GetComponent<MeshCollider>());

        if (fogMaterial == null || fogQuad == null) return;

        Renderer fogRenderer = fogQuad.GetComponent<Renderer>();
        if (fogRenderer == null) return;

        Material instancedMat = fogRenderer.material = fogMaterial;

        if (instancedMat.HasProperty("_MaskTex")) instancedMat.SetTexture("_MaskTex", maskTexture);
        if (instancedMat.HasProperty("_ExploredTex")) instancedMat.SetTexture("_ExploredTex", exploredTexture);

        Texture2D fog1 = Resources.Load<Texture2D>("Textures/Fog1");
        Texture2D fog2 = Resources.Load<Texture2D>("Textures/Fog2");
        if (fog1 == null) fog1 = Resources.Load<Texture2D>("Fog1");
        if (fog2 == null) fog2 = Resources.Load<Texture2D>("Fog2");

        if (fog1 != null && instancedMat.HasProperty("_MainTex"))
            instancedMat.SetTexture("_MainTex", fog1);

        if (fog2 != null && instancedMat.HasProperty("_DetailTex"))
            instancedMat.SetTexture("_DetailTex", fog2);
    }

    private void Update()
    {
        if (exploredTexture != null && exploredCamera != null)
        {
            if (explorationVersion != cachedExploredVersion)
            {
                cachedExploredVersion = explorationVersion;
                RebuildExploredMesh();
            }
            exploredCamera.Render();
        }

    }

    private void RebuildExploredMesh()
    {
        if (exploredCells.Count == 0) return;

        if (exploredMeshObj == null)
        {
            exploredMeshObj = new GameObject("ExploredMesh");
            exploredMeshObj.layer = 30;
            exploredMeshObj.transform.SetParent(transform);
            exploredMeshObj.transform.position = Vector3.zero;
            exploredMeshObj.AddComponent<MeshFilter>();

            MeshRenderer mr = exploredMeshObj.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Unlit/Color"));
            mr.material.color = Color.white;
        }

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        Vector2[] offsets = {
            new Vector2(-0.5f, -0.5f),
            new Vector2(0.5f, -0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-0.5f, 0.5f)
        };

        foreach (Vector2 cell in exploredCells)
        {
            int vi = verts.Count;
            foreach (var off in offsets)
                verts.Add(new Vector3(cell.x + off.x, cell.y + off.y, 0));
            tris.Add(vi); tris.Add(vi + 1); tris.Add(vi + 2);
            tris.Add(vi); tris.Add(vi + 2); tris.Add(vi + 3);
        }

        if (exploredMesh != null) Destroy(exploredMesh);
        exploredMesh = new Mesh();
        exploredMesh.vertices = verts.ToArray();
        exploredMesh.triangles = tris.ToArray();

        exploredMeshObj.GetComponent<MeshFilter>().mesh = exploredMesh;
    }
}
