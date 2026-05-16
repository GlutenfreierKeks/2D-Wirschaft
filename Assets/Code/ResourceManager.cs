using UnityEngine;
using TMPro;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void InitializeResources(IslandType type)
    {
        if (Player_UI.Instance == null) return;

        int wood = 0;
        int stone = 0;

        switch (type)
        {
            case IslandType.Plains: wood = 20; stone = 10; break;
            case IslandType.Desert: wood = 5; stone = 5; break;
            case IslandType.Jungle: wood = 40; stone = 5; break;
            case IslandType.Stone: wood = 10; stone = 30; break;
        }

        Player_UI.Instance.SetResource("holz", wood);
        Player_UI.Instance.SetResource("stein", stone);
    }

    public bool CanAfford(int wood, int stone, int iron, int gold)
    {
        if (Player_UI.Instance == null) return false;
        return Player_UI.Instance.GetResource("holz") >= wood && 
               Player_UI.Instance.GetResource("stein") >= stone &&
               Player_UI.Instance.GetResource("eisen") >= iron &&
               Player_UI.Instance.GetResource("gold") >= gold;
    }

    public void SpendResources(int wood, int stone, int iron, int gold)
    {
        if (Player_UI.Instance == null) return;
        Player_UI.Instance.AddResource("holz", -wood);
        Player_UI.Instance.AddResource("stein", -stone);
        Player_UI.Instance.AddResource("eisen", -iron);
        Player_UI.Instance.AddResource("gold", -gold);
    }

    public void AddResource(string id, int amount)
    {
        if (Player_UI.Instance == null) return;
        Player_UI.Instance.AddResource(id, amount);
    }

    public bool HasResource(string id, int amount)
    {
        if (Player_UI.Instance == null || string.IsNullOrEmpty(id)) return true;
        return Player_UI.Instance.GetResource(id) >= amount;
    }

    public void SpendResource(string id, int amount)
    {
        if (Player_UI.Instance == null || string.IsNullOrEmpty(id)) return;
        Player_UI.Instance.AddResource(id, -amount);
    }
}
