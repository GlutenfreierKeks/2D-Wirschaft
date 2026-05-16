using UnityEngine;

public class Enemy_Place : MonoBehaviour
{
    [Header("Soldaten Prefabs")]
    public GameObject spearSoldierPrefab;
    public GameObject shieldSoldierPrefab;
    public GameObject swordSoldierPrefab;
    public GameObject bowSoldierPrefab;

    // Der aktuell ausgewählte Soldat
    private GameObject selectedPrefab;

    private void Start()
    {
        // Standardmäßig den Speersoldaten auswählen
        selectedPrefab = spearSoldierPrefab;
    }

    private void Update()
    {
        // Auswahl über die Tasten 1 bis 4
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSoldier(spearSoldierPrefab, "Speersoldat");
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSoldier(shieldSoldierPrefab, "Schildsoldat");
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSoldier(swordSoldierPrefab, "Schwertsoldat");
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSoldier(bowSoldierPrefab, "Bogensoldat");

        // Platzieren mit Linksklick
        if (Input.GetMouseButtonDown(0))
        {
            PlaceSoldier();
        }
    }

    // Funktion um den ausgewählten Soldaten zu wechseln (Kann auch von UI-Buttons aufgerufen werden)
    public void SelectSoldier(GameObject soldierPrefab, string soldierName = "Soldat")
    {
        selectedPrefab = soldierPrefab;
        Debug.Log("Ausgewählt: " + soldierName);
    }

    private void PlaceSoldier()
    {
        if (selectedPrefab == null)
        {
            Debug.LogWarning("Kein Soldat ausgewählt!");
            return;
        }

        // Mausposition in die Weltposition umwandeln
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        // Den Soldaten an der Mausposition platzieren
        Instantiate(selectedPrefab, mousePos, Quaternion.identity);
    }
}
