using UnityEngine;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Buildings/BuildingData")]
public class BuildingData : ScriptableObject
{
    public string buildingName;
    public GameObject prefab;
    public PlacementRule placementRule = PlacementRule.LandOnly;
    
    [Header("Size in Grid Cells")]
    public int width = 1;
    public int height = 1;

    [Header("Costs")]
    public int woodCost = 0;
    public int stoneCost = 0;

    [Header("Construction")]
    public float buildTime = 5f;

    [Header("Production")]
    public string productionResourceId = ""; // e.g. "holz", "stein", "bevolkerung" (max)
    public int productionAmount = 0;
    public float productionInterval = 10f;

    [Header("Visuals")]
    public Color ghostColor = new Color(0, 1, 0, 0.5f);
}
