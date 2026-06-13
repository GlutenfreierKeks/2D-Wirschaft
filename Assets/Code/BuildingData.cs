using UnityEngine;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Buildings/BuildingData")]
public class BuildingData : ScriptableObject
{
    public string buildingName;
    public GameObject prefab;
    public PlacementRule placementRule = PlacementRule.LandOnly;
    public ResourceType requiredResourceType = ResourceType.None;
    
    [Header("Size in Grid Cells")]
    [Tooltip("Normale Gebaude: Breite x Hoehe. Schiffe: die laengere Seite gilt automatisch als Schiffslaenge, die kuerzere als Schiffbreite.")]
    public int width = 1;
    [Tooltip("Bei Schiffen zeigt die Grundausrichtung nach Sueden. Die Platzierungslogik dreht das Schiff automatisch passend zum Pier.")]
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

    [Header("Ship Settings")]
    public ShipData shipData;

    [Header("Defense")]
    public bool isDefenseTower = false;
    public float towerRange = 18f;
    public int towerDamage = 18;
    public float towerAttackCooldown = 2.5f;
    public float fogRevealRadius = 5f;

    [Header("Health")]
    [Tooltip("Start-Lebenspunkte (Standard 100, Lagerhaus 2000)")]
    public int maxHP = 100;

    [Header("Warehouse Type")]
    [Tooltip("Wenn aktiv, ist dies ein Lagerhaus-Typ. Nur auf fremden Inseln baubar. Ermöglicht weiteres Bauen auf dieser Insel.")]
    public bool isWarehouseType = false;

    [Header("Visuals")]
    public Color ghostColor = new Color(0, 1, 0, 0.5f);
    
    [Header("Island Ownership")]
    [Tooltip("Wenn aktiv, kann dieses Gebäude auf anderen Inseln gebaut werden (z.B. Lagerhaus). Andere Gebäude nur auf eigener Insel.")]
    public bool canBuildOnOtherIslands = false;
}
