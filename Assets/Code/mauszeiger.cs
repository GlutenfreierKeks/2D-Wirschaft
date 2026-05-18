using UnityEngine;

public class mauszeiger : MonoBehaviour
{
    [Header("Mauszeiger Einstellungen")]
    [Tooltip("Ziehe hier dein neues Mauszeiger-Bild (als Texture2D) aus dem Projektfenster hinein.")]
    public Texture2D cursorTexture;

    [Tooltip("Der genaue Klick-Punkt des Zeigers. (0, 0) ist die obere linke Ecke des Bildes.")]
    public Vector2 hotSpot = Vector2.zero;

    private void Start()
    {
        // Ändert den Mauszeiger direkt beim Start des Spiels
        if (cursorTexture != null)
        {
            Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
        }
        else
        {
            Debug.LogWarning("Du hast noch kein Mauszeiger-Bild im Inspector zugewiesen!");
        }
    }
}
