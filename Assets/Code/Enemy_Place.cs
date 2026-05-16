using UnityEngine;
using UnityEngine.InputSystem;

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
        // Auswahl über die Tasten 1 bis 4 mit neuem Input System
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectSoldier(spearSoldierPrefab, "Speersoldat");
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectSoldier(shieldSoldierPrefab, "Schildsoldat");
            if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectSoldier(swordSoldierPrefab, "Schwertsoldat");
            if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectSoldier(bowSoldierPrefab, "Bogensoldat");
        }

        // Platzieren mit Linksklick
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Block placement if a sub-menu is open or mouse is over UI
            if (Player_UI.Instance != null && Player_UI.Instance.IsSubMenuOpen) return;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

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

        // Mausposition in die Weltposition umwandeln (Neues Input System)
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        
        // Den Soldaten an der Mausposition platzieren
        Instantiate(selectedPrefab, mousePos, Quaternion.identity);
    }
}
