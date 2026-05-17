using UnityEngine;
using UnityEditor;
using System.IO;

public class BuildingGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Buildings")]
    public static void Generate()
    {
        string folderPath = "Assets/Data/Buildings";
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        // --- Wohngebäude ---
        CreateBuilding("Kleines Haus", 4, 2, 0, 0, "bevolkerung", 5, 0, ResourceType.None); 
        CreateBuilding("Großes Haus", 10, 8, 2, 0, "bevolkerung", 12, 60, ResourceType.None, "", 0, false, true); // Spawnt Bewohner

        // --- Rohstoffe (Produktion) ---
        CreateBuilding("Holzhütte", 2, 0, 0, 0, "holz", 6, 60, ResourceType.Wood);
        CreateBuilding("Steinwerk", 5, 2, 0, 0, "stein", 5, 60, ResourceType.Stone);
        CreateBuilding("Eisenmine", 8, 10, 0, 0, "eisen", 3, 60, ResourceType.Iron);
        CreateBuilding("Goldmine", 10, 10, 8, 0, "gold", 2, 60, ResourceType.Gold);

        // --- Landwirtschaft ---
        CreateBuilding("Farm", 5, 2, 0, 0, "weizen", 10, 60, ResourceType.Wheat);
        CreateBuilding("Fruchtfarm", 6, 3, 0, 0, "fruechte", 5, 60, ResourceType.Fruit);
        CreateBuilding("Wüstenfarm", 8, 5, 0, 0, "weizen", 12, 60, ResourceType.Wheat); 
        
        // --- Veredelung & Spezial ---
        CreateBuilding("Viehhof", 8, 4, 0, 0, "fleisch", 5, 60, ResourceType.Animal, "weizen", 3); 
        CreateBuilding("Goldschmiede", 10, 5, 5, 0, "geld", 10, 60, ResourceType.None, "gold", 1); 
        CreateBuilding("Kaserne", 10, 10, 10, 5, "", 0, 0, ResourceType.None, "", 0, true); 

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Alle Gebäude wurden in Assets/Data/Buildings erstellt!");
    }

    private static void CreateBuilding(string name, int wood, int stone, int iron, int gold, string prodId, int prodAmt, float prodInt, ResourceType requiredType = ResourceType.None, string consId = "", int consAmt = 0, bool isBarracks = false, bool prodVillagers = false)
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingName = name;
        data.woodCost = wood;
        data.stoneCost = stone;
        data.ironCost = iron;
        data.goldCost = gold;
        data.requiredResourceType = requiredType;
        data.productionResourceId = prodId;
        data.productionAmount = prodAmt;
        data.productionInterval = prodInt;
        data.producesVillagers = prodVillagers;
        data.consumedResourceId = consId;
        data.consumedAmount = consAmt;
        data.isBarracks = isBarracks;

        string path = $"Assets/Data/Buildings/{name.Replace(" ", "_")}.asset";
        AssetDatabase.CreateAsset(data, path);
    }
}
