using UnityEngine;
using System.Collections.Generic;

public class ResourceHistory : MonoBehaviour
{
    public static ResourceHistory Instance { get; private set; }

    [System.Serializable]
    public class HistoryData
    {
        public List<float> popHistory = new List<float>();
        public List<float> moodHistory = new List<float>();
        public List<float> soldierHistory = new List<float>();
        public List<float> woodIncomeHistory = new List<float>();
        public List<float> stoneIncomeHistory = new List<float>();
        public List<float> ironIncomeHistory = new List<float>();
        public List<float> goldIncomeHistory = new List<float>();
    }

    public HistoryData data = new HistoryData();
    public int maxDataPoints = 60; // 60 points at 5s interval = 5 minutes of history
    public float recordInterval = 5f;
    private float timer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (Player_UI.Instance == null) return;

        timer += Time.deltaTime;
        if (timer >= recordInterval)
        {
            timer = 0f;
            RecordData();
        }
    }

    public void RecordData()
    {
        AddDataPoint(data.popHistory, Player_UI.Instance.GetResource("bevolkerung"));
        
        float mood = VillagerManager.Instance != null ? VillagerManager.Instance.globalMood : 100f;
        AddDataPoint(data.moodHistory, mood);

        AddDataPoint(data.soldierHistory, Player_UI.Instance.GetResource("soldaten"));

        AddDataPoint(data.woodIncomeHistory, GetNetIncome("holz"));
        AddDataPoint(data.stoneIncomeHistory, GetNetIncome("stein"));
        AddDataPoint(data.ironIncomeHistory, GetNetIncome("eisen"));
        AddDataPoint(data.goldIncomeHistory, GetNetIncome("gold"));
    }

    private float GetNetIncome(string resourceId)
    {
        float prod = 0f;
        float cons = 0f;
        List<string> dummy = new List<string>();
        Player_UI.Instance.CalculateResourceRates(resourceId, out prod, out cons, dummy);
        return prod - cons; // Net income per minute
    }

    private void AddDataPoint(List<float> list, float value)
    {
        list.Add(value);
        if (list.Count > maxDataPoints)
        {
            list.RemoveAt(0);
        }
    }
}
