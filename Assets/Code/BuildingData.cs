using UnityEngine;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Buildings/BuildingData")]
public class BuildingData : ScriptableObject
{
    public string buildingName;
    public GameObject prefab;
    public PlacementRule placementRule = PlacementRule.LandOnly;
    public ResourceType requiredResourceType = ResourceType.None;
    
    [Header("Size in Grid Cells")]
    public int width = 1;
    public int height = 1;

    [Header("Costs")]
    public int woodCost = 0;
    public int stoneCost = 0;
    public int ironCost = 0;
    public int goldCost = 0;

    [Header("Construction")]
    public float buildTime = 5f;
    public int requiredWorkers = 1;
    public bool isWorkerHub = false;
    public bool isBarracks = false;

    [Header("Production")]
    public string productionResourceId = ""; // e.g. "holz", "stein", "bevolkerung" (max)
    public int productionAmount = 0;
    public string consumedResourceId = "";   // e.g. "weizen"
    public int consumedAmount = 0;
    public float productionInterval = 10f;
    public bool producesVillagers = false;

    [Header("Operation")]
    public int workersNeeded = 1;

    [Header("Housing")]
    [Tooltip("0 = kein Schlafplatz. Kleines Haus: 2, Großes Haus: 4, Hauptlager: 5")]
    public int sleepCapacity = 0;

    [Header("Visuals")]
    public Color ghostColor = new Color(0, 1, 0, 0.5f);

    [Header("Defense Tower")]
    [Tooltip("If set, this building acts as a defensive archer tower and can station trained bow soldiers.")]
    public bool isDefenseTower = false;
    [Tooltip("How many archers the tower can station.")]
    public int archerSlots = 0;
    [Tooltip("If >0, this radius will be used for fog revealing instead of the default.")]
    public float revealRadius = 0f;
    [Tooltip("Tower attack range (world units).")]
    public float towerRange = 15f;
    [Tooltip("Damage per shot for stationed archers.")]
    public float towerDamage = 20f;
    [Tooltip("Cooldown between shots (seconds).")]
    public float towerCooldown = 3f;
    [Tooltip("Kategorie im Bau-Menü. Leer lässt das System selbst entscheiden.")]
    public string uiCategoryId = "";
    [Tooltip("Optionales Icon für das Bau-Menü.")]
    public Sprite uiIcon = null;
}
