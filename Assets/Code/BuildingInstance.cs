using UnityEngine;
using System.Collections;

public class BuildingInstance : MonoBehaviour
{
    public BuildingData data;
    public bool isLocal;
    
    private bool isConstructed = false;
    private float constructionProgress = 0f;
    private Renderer[] renderers;
    private FogRevealer revealer;

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        revealer = GetComponent<FogRevealer>();
        if (revealer != null) revealer.enabled = false; // Don't reveal fog while building

        StartCoroutine(ConstructionRoutine());
    }

    private IEnumerator ConstructionRoutine()
    {
        // Initial state: Transparent
        SetAlpha(0.1f);
        
        while (constructionProgress < 1f)
        {
            constructionProgress += Time.deltaTime / data.buildTime;
            SetAlpha(Mathf.Lerp(0.1f, 1f, constructionProgress));
            yield return null;
        }

        CompleteConstruction();
    }

    private void SetAlpha(float alpha)
    {
        foreach (var r in renderers)
        {
            if (r.gameObject.name == "FogMask") continue;
            Color c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }
    }

    private void CompleteConstruction()
    {
        isConstructed = true;
        if (revealer != null) revealer.enabled = true;

        // If it increases max population
        if (data.productionResourceId == "bevolkerung")
        {
            Player_UI.Instance.AddMaxPopulation(data.productionAmount);
        }
        else if (!string.IsNullOrEmpty(data.productionResourceId))
        {
            StartCoroutine(ProductionRoutine());
        }
    }

    private IEnumerator ProductionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(data.productionInterval);
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddResource(data.productionResourceId, data.productionAmount);
            }
        }
    }
}
