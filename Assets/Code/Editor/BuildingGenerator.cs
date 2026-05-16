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
        CreateBuilding("Kleines Haus", 4, 2, 0, 0, "bevolkerung", 5, 0); 
        CreateBuilding("Großes Haus", 10, 8, 2, 0, "bevolkerung", 12, 60, true); // Spawnt Bewohner alle 60s

        // --- Rohstoffe (Produktion) ---
        CreateBuilding("Holzhütte", 2, 0, 0, 0, "holz", 10, 100);
        CreateBuilding("Steinwerk", 5, 2, 0, 0, "stein", 8, 100);
        CreateBuilding("Eisenmine", 8, 10, 0, 0, "eisen", 5, 120);
        CreateBuilding("Goldmine", 10, 10, 8, 0, "gold", 3, 150);

        // --- Landwirtschaft ---
        CreateBuilding("Farm", 5, 2, 0, 0, "weizen", 10, 100);
        CreateBuilding("Fruchtfarm", 6, 3, 0, 0, "fruechte", 8, 100);
        CreateBuilding("Wüstenfarm", 8, 5, 0, 0, "weizen", 12, 120); 
        
        // --- Veredelung & Spezial ---
        CreateBuilding("Viehhof", 8, 4, 0, 0, "fleisch", 5, 100, false, "weizen", 5); 
        CreateBuilding("Goldschmiede", 10, 5, 5, 0, "geld", 20, 120, false, "gold", 2); // 2 Gold-Erz -> 20 Münzen
        CreateBuilding("Kaserne", 10, 10, 10, 5, "", 0, 0, false, "", 0, true); 

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Alle Gebäude wurden in Assets/Data/Buildings erstellt!");
    }

    private static void CreateBuilding(string name, int wood, int stone, int iron, int gold, string prodId, int prodAmt, float prodInt, bool prodVillagers = false, string consId = "", int consAmt = 0, bool isBarracks = false)
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingName = name;
        data.woodCost = wood;
        data.stoneCost = stone;
        data.ironCost = iron;
        data.goldCost = gold;
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
