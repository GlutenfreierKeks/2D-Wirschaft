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

    public static void AddRevealer(FogRevealer r) => revealers.Add(r);
    public static void RemoveRevealer(FogRevealer r) => revealers.Remove(r);

    private void Start()
    {
        accumulatorMaterial = new Material(Shader.Find("Hidden/FogAccumulator"));
        SetupFog();
    }

    private void SetupFog()
    {
        // Create the current visibility mask (clears every frame)
        maskTexture = new RenderTexture(maskResolution, maskResolution, 0);
        maskTexture.filterMode = FilterMode.Bilinear;

        // Create the persistent explored mask (does NOT clear)
        exploredTexture = new RenderTexture(maskResolution, maskResolution, 0);
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
        Camera.main.cullingMask &= ~(1 << 31);

        // Create the fog overlay quad
        fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fogQuad.name = "FogOverlay";
        fogQuad.transform.position = new Vector3(0, 0, -5f); // In front of grid/islands
        fogQuad.transform.localScale = new Vector3(mapSize, mapSize, 1);
        Destroy(fogQuad.GetComponent<MeshCollider>());

        if (fogMaterial != null)
        {
            fogQuad.GetComponent<Renderer>().material = fogMaterial;
            fogQuad.GetComponent<Renderer>().material.SetTexture("_MaskTex", maskTexture);
            fogQuad.GetComponent<Renderer>().material.SetTexture("_ExploredTex", exploredTexture);
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
