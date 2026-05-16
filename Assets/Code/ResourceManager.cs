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

    public bool CanAfford(int woodCost, int stoneCost)
    {
        if (Player_UI.Instance == null) return false;
        return Player_UI.Instance.GetResource("holz") >= woodCost && 
               Player_UI.Instance.GetResource("stein") >= stoneCost;
    }

    public void SpendResources(int woodCost, int stoneCost)
    {
        if (Player_UI.Instance == null) return;
        Player_UI.Instance.AddResource("holz", -woodCost);
        Player_UI.Instance.AddResource("stein", -stoneCost);
    }

    public void AddResource(string id, int amount)
    {
        if (Player_UI.Instance == null) return;
        Player_UI.Instance.AddResource(id, amount);
    }
}
