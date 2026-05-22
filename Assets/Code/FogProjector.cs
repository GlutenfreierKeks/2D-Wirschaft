using UnityEngine;
using System.Collections.Generic;

public class FogProjector : MonoBehaviour
{
    private static List<FogRevealer> revealers = new List<FogRevealer>();

    [Header("Fog Settings")]
    [SerializeField] private Material fogMaterial;
    [SerializeField] private int maskResolution = 1024;
    [SerializeField] private float mapSize = 5000f;

    private RenderTexture maskTexture;
    private RenderTexture exploredTexture;
    private Camera maskCamera;
    private GameObject fogQuad;

    private Material accumulatorMaterial;
    private static HashSet<Vector2> exploredCells = new HashSet<Vector2>();

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
                    exploredCells.Add(new Vector2(Mathf.Round(center.x + x), Mathf.Round(center.y + y)));
                }
            }
        }
    }

    public static void AddRevealer(FogRevealer r) => revealers.Add(r);
    public static void RemoveRevealer(FogRevealer r) => revealers.Remove(r);

    private void Start()
    {
        Shader accumulatorShader = Shader.Find("Hidden/FogAccumulator");
        if (accumulatorShader == null)
        {
            Debug.LogWarning("[FogProjector] Hidden/FogAccumulator shader not found. Verwende Fallback-Shader.");
            accumulatorShader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
        }

        if (accumulatorShader == null)
        {
            Debug.LogError("[FogProjector] Kein Accumulator-Shader gefunden. FogProjector wird deaktiviert.");
            enabled = false;
            return;
        }

        accumulatorMaterial = new Material(accumulatorShader);
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

        // Create the current visibility mask (clears every frame)
        maskTexture = new RenderTexture(maskResolution, maskResolution, 24); // Added depth buffer (24 bits)
        maskTexture.filterMode = FilterMode.Bilinear;

        // Create the persistent explored mask
        exploredTexture = new RenderTexture(maskResolution, maskResolution, 24);
        exploredTexture.filterMode = FilterMode.Bilinear;

        // Create a camera to render the mask
        GameObject camObj = new GameObject("FogMaskCamera");
        camObj.transform.position = new Vector3(0, 0, -50f);
        maskCamera = camObj.AddComponent<Camera>();
        maskCamera.orthographic = true;
        maskCamera.orthographicSize = mapSize / 2f;
        maskCamera.targetTexture = maskTexture;
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = Color.black;
        maskCamera.cullingMask = 1 << 31; // Render only the mask layer

        // Make the main camera ignore layer 31
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.cullingMask &= ~(1 << 31);
        }
        else
        {
            Debug.LogWarning("[FogProjector] Keine Hauptkamera gefunden. Layer 31 wird nicht ausgeblendet.");
        }

        // Create the fog overlay quad
        fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fogQuad.name = "FogOverlay";
        fogQuad.transform.position = new Vector3(0, 0, -5f); // In front of grid/islands
        fogQuad.transform.localScale = new Vector3(mapSize, mapSize, 1);
        Destroy(fogQuad.GetComponent<MeshCollider>());

        if (fogMaterial != null && fogQuad != null)
        {
            Renderer fogRenderer = fogQuad.GetComponent<Renderer>();
            if (fogRenderer == null)
            {
                Debug.LogError("[FogProjector] Kein Renderer auf FogOverlay gefunden.");
                return;
            }

            Material instancedMat = fogRenderer.material = fogMaterial;
            if (instancedMat == null)
            {
                Debug.LogError("[FogProjector] Nebelmaterial konnte nicht erstellt werden.");
                return;
            }

            if (instancedMat.HasProperty("_MaskTex")) instancedMat.SetTexture("_MaskTex", maskTexture);
            if (instancedMat.HasProperty("_ExploredTex")) instancedMat.SetTexture("_ExploredTex", exploredTexture);

            // Dynamically load and assign Fog1 and Fog2 textures from Resources
            Texture2D fog1 = Resources.Load<Texture2D>("Textures/Fog1");
            Texture2D fog2 = Resources.Load<Texture2D>("Textures/Fog2");
            if (fog1 == null) fog1 = Resources.Load<Texture2D>("Fog1");
            if (fog2 == null) fog2 = Resources.Load<Texture2D>("Fog2");

            if (fog1 != null)
            {
                if (instancedMat.HasProperty("_MainTex"))
                {
                    instancedMat.SetTexture("_MainTex", fog1);
                }
            }
            else
            {
                Debug.LogWarning("[FogProjector] Fog1 texture nicht gefunden. Suche in Resources/Fog1 oder Resources/Textures/Fog1.");
            }

            if (fog2 != null)
            {
                if (instancedMat.HasProperty("_DetailTex"))
                {
                    instancedMat.SetTexture("_DetailTex", fog2);
                }
            }
            else
            {
                Debug.LogWarning("[FogProjector] Fog2 texture nicht gefunden. Suche in Resources/Fog2 oder Resources/Textures/Fog2.");
            }
        }
    }

    private void Update()
    {
        if (maskTexture != null && exploredTexture != null)
        {
            Graphics.Blit(maskTexture, exploredTexture, accumulatorMaterial);
        }
    }
}
