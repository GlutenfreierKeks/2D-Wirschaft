using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UI_LineChart : MonoBehaviour
{
    private RectTransform graphContainer;
    private List<GameObject> pointObjects = new List<GameObject>();
    private List<GameObject> lineObjects = new List<GameObject>();
    
    public Sprite circleSprite; 
    public Color graphColor = Color.green;

    private void Awake()
    {
        graphContainer = GetComponent<RectTransform>();
    }

    public void ShowGraph(List<float> valueList)
    {
        // Cleanup old
        foreach (GameObject go in pointObjects) Destroy(go);
        foreach (GameObject go in lineObjects) Destroy(go);
        pointObjects.Clear();
        lineObjects.Clear();

        if (valueList == null || valueList.Count <= 1) return;

        float graphHeight = graphContainer.rect.height;
        float graphWidth = graphContainer.rect.width;

        float yMax = valueList[0];
        float yMin = valueList[0];
        foreach (float val in valueList)
        {
            if (val > yMax) yMax = val;
            if (val < yMin) yMin = val;
        }

        // Add 10% margin top/bottom
        float range = yMax - yMin;
        if (range <= 0.01f) range = 10f; // default range if all values are equal
        yMax += range * 0.1f;
        yMin -= range * 0.1f;

        float xSize = graphWidth / (valueList.Count - 1);

        GameObject lastPointGO = null;
        for (int i = 0; i < valueList.Count; i++)
        {
            float xPosition = i * xSize;
            float yPosition = ((valueList[i] - yMin) / (yMax - yMin)) * graphHeight;

            GameObject pointGO = CreatePoint(new Vector2(xPosition, yPosition));
            if (lastPointGO != null)
            {
                CreateLineConnection(lastPointGO.GetComponent<RectTransform>().anchoredPosition, pointGO.GetComponent<RectTransform>().anchoredPosition);
            }
            lastPointGO = pointGO;
        }
    }

    private GameObject CreatePoint(Vector2 anchoredPosition)
    {
        GameObject go = new GameObject("Point", typeof(Image));
        go.transform.SetParent(graphContainer, false);
        Image img = go.GetComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = graphColor;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(6, 6);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        pointObjects.Add(go);
        return go;
    }

    private void CreateLineConnection(Vector2 dotPositionA, Vector2 dotPositionB)
    {
        GameObject go = new GameObject("Line", typeof(Image));
        go.transform.SetParent(graphContainer, false);
        Image img = go.GetComponent<Image>();
        img.color = new Color(graphColor.r, graphColor.g, graphColor.b, 0.6f);
        RectTransform rt = go.GetComponent<RectTransform>();
        Vector2 dir = (dotPositionB - dotPositionA).normalized;
        float distance = Vector2.Distance(dotPositionA, dotPositionB);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(distance, 3f);
        rt.anchoredPosition = dotPositionA + dir * distance * .5f;
        rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        lineObjects.Add(go);
    }
}
