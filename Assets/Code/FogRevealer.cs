using UnityEngine;

public class FogRevealer : MonoBehaviour
{
    public float radius = 5f;
    private GameObject maskObj;

    private void Awake()
    {
        // Create a visual indicator for the mask camera
        maskObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        maskObj.name = "MaskIndicator";
        maskObj.transform.SetParent(transform);
        maskObj.transform.localPosition = Vector3.zero;
        maskObj.transform.localScale = new Vector3(radius * 2.5f, radius * 2.5f, 1);
        
        // Use layer 31 for the Fog Mask (must be set in Camera as well)
        maskObj.layer = 31; 
        
        Renderer rend = maskObj.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Custom/MaskCircleShader"));
        rend.material.color = Color.white;
        
        Destroy(maskObj.GetComponent<MeshCollider>());
    }

    private void OnEnable()
    {
        if (maskObj != null) maskObj.SetActive(true);
    }

    private void OnDisable()
    {
        if (maskObj != null) maskObj.SetActive(false);
    }
}
