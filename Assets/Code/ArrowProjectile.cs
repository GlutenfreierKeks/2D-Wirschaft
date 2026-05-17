using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    private static Sprite fallbackSprite;

    private Vector3 targetPosition;
    private float speed;

    public static void Spawn(Vector3 from, Vector3 to)
    {
        GameObject arrow = new GameObject("ArrowProjectile");
        arrow.transform.position = from;
        ArrowProjectile projectile = arrow.AddComponent<ArrowProjectile>();
        projectile.Initialize(from, to);
    }

    private void Initialize(Vector3 from, Vector3 to)
    {
        targetPosition = new Vector3(to.x, to.y, from.z);
        speed = 18f;

        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetFallbackSprite();
        sr.color = new Color(0.28f, 0.18f, 0.08f, 1f);
        sr.sortingOrder = 24;

        Vector2 dir = (targetPosition - from).normalized;
        transform.right = dir;
        transform.localScale = new Vector3(0.6f, 0.08f, 1f);
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
        {
            Destroy(gameObject);
        }
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return fallbackSprite;
    }
}
