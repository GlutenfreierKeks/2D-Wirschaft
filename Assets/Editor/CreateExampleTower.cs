using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public static class CreateExampleTower
{
    [MenuItem("Tools/Create Example Archer Tower")]
    public static void Create()
    {
        // Ensure folders exist
        Directory.CreateDirectory("Assets/Prefabs");
        Directory.CreateDirectory("Assets/Resources");
        Directory.CreateDirectory("Assets/Resources/Textures");

        // Create visual quad
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "ExampleArcherTower";
        go.transform.position = Vector3.zero;
        go.transform.localScale = new Vector3(3f, 3f, 1f);

        Renderer rend = go.GetComponent<Renderer>();
        Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        rend.sharedMaterial = new Material(shader);

        Texture2D tex = Resources.Load<Texture2D>("Textures/turm");
        Sprite iconSprite = null;
        if (tex == null)
        {
            string[] guids = AssetDatabase.FindAssets("turm t:Sprite");
            if (guids.Length > 0)
            {
                string iconPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                tex = iconSprite != null ? iconSprite.texture : null;
            }
        }
        if (tex == null)
        {
            string[] guids = AssetDatabase.FindAssets("turm t:Texture2D");
            if (guids.Length > 0)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (iconSprite == null)
                {
                    iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                }
            }
        }

        if (tex != null)
        {
            rend.sharedMaterial.mainTexture = tex;
        }
        else
        {
            rend.sharedMaterial.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        // Attach runtime components
        if (go.GetComponent<MeshCollider>() != null) Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        var bi = go.AddComponent<BuildingInstance>();
        var revealer = go.AddComponent<FogRevealer>();
        revealer.radius = 10f;
        var tower = go.AddComponent<ArcherTower>();
        tower.slots = 3;
        tower.range = 15f;
        tower.damage = 20f;
        tower.cooldown = 3f;
        tower.team = Team.Player;

        // Save prefab
        string prefabPath = "Assets/Prefabs/ExampleArcherTower.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.UserAction);

        // Create BuildingData asset referencing the prefab
        BuildingData bd = ScriptableObject.CreateInstance<BuildingData>();
        bd.buildingName = "ExampleArcherTower";
        bd.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        bd.width = 3;
        bd.height = 3;
        bd.isDefenseTower = true;
        bd.archerSlots = 3;
        bd.revealRadius = 10f;
        bd.towerRange = 15f;
        bd.towerDamage = 20f;
        bd.towerCooldown = 3f;
        bd.uiCategoryId = "andere";
        if (iconSprite != null)
        {
            bd.uiIcon = iconSprite;
        }

        string assetPath = "Assets/Resources/BuildingData_ExampleArcherTower.asset";
        AssetDatabase.CreateAsset(bd, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Cleanup temporary scene object
        Object.DestroyImmediate(go);

        Debug.Log($"Created example archer tower prefab at '{prefabPath}' and BuildingData asset at '{assetPath}'.");
    }
}
#endif
