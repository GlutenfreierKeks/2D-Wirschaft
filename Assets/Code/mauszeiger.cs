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
        if (cursorTexture == null)
        {
            Debug.LogWarning("Du hast noch kein Mauszeiger-Bild im Inspector zugewiesen!");
            return;
        }

        Texture2D cursorToUse = cursorTexture;
        int maxCursorSize = 96;
        if ((cursorTexture.width > maxCursorSize || cursorTexture.height > maxCursorSize) && cursorTexture.isReadable)
        {
            int scaledWidth = Mathf.Min(maxCursorSize, cursorTexture.width);
            int scaledHeight = Mathf.Min(maxCursorSize, cursorTexture.height);
            cursorToUse = ScaleTexture(cursorTexture, scaledWidth, scaledHeight);
            float scaleX = (float)scaledWidth / cursorTexture.width;
            float scaleY = (float)scaledHeight / cursorTexture.height;
            hotSpot = new Vector2(hotSpot.x * scaleX, hotSpot.y * scaleY);
        }

        Cursor.SetCursor(cursorToUse, hotSpot, CursorMode.Auto);
        Cursor.SetCursor(cursorToUse, hotSpot, CursorMode.ForceSoftware);
    }

    private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        Color[] pixels = result.GetPixels();

        float incX = 1.0f / targetWidth;
        float incY = 1.0f / targetHeight;
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                float u = x * incX;
                float v = y * incY;
                pixels[y * targetWidth + x] = source.GetPixelBilinear(u, v);
            }
        }

        result.SetPixels(pixels);
        result.Apply();
        return result;
    }
}
