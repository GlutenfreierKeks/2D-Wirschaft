using UnityEngine;
using System.Collections;

/// <summary>
/// Enables sailing mode when a ship is selected.
/// Click anywhere on water to sail there. Ship rotates to face destination.
/// </summary>
public class ShipSailingMode : MonoBehaviour
{
    public static ShipSailingMode Instance { get; private set; }

    [Header("Visual Feedback")]
    public Color sailIndicatorColor = new Color(0.3f, 0.8f, 1f, 0.8f);
    public float indicatorSize = 1f;

    [Header("Movement Settings")]
    public float maxClickDistance = 100f;

    private Ship targetShip;
    private bool isActive = false;
    private GameObject currentIndicator;
    private Vector3 destination;
    private bool hasDestination = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!isActive || targetShip == null) return;

        HandleMouseInput();
        UpdateIndicator();
    }

    public void EnableSailingMode(Ship ship)
    {
        if (ship == null) return;

        targetShip = ship;
        isActive = true;
        hasDestination = false;

        // Create destination indicator
        if (currentIndicator != null)
            Destroy(currentIndicator);

        currentIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        currentIndicator.name = "SailIndicator";
        currentIndicator.transform.localScale = Vector3.one * indicatorSize;
        
        var sr = currentIndicator.GetComponent<Renderer>();
        if (sr != null)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = sailIndicatorColor;
            sr.material = mat;
        }

        var collider = currentIndicator.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        currentIndicator.SetActive(false);

        Debug.Log($"[ShipSailingMode] Sailing mode enabled for {ship.GetShipName()}");
    }

    public void DisableSailingMode()
    {
        isActive = false;
        targetShip = null;
        hasDestination = false;

        if (currentIndicator != null)
        {
            Destroy(currentIndicator);
            currentIndicator = null;
        }

        Debug.Log("[ShipSailingMode] Sailing mode disabled");
    }

    private void HandleMouseInput()
    {
        // Right click to cancel
        if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
        {
            DisableSailingMode();
            return;
        }

        // Left click to set destination
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            mousePos.z = 0;

            // Check if on water
            Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(mousePos.x), Mathf.RoundToInt(mousePos.y));

            if (IslandManager.IsLand(gridPos))
            {
                NotificationManager.Instance?.Notify("sail_land", "Schiffe können nur auf Wasser fahren!", 3f);
                return;
            }

            // Check distance
            if (targetShip != null)
            {
                float distance = Vector3.Distance(mousePos, targetShip.transform.position);
                if (distance > maxClickDistance)
                {
                    NotificationManager.Instance?.Notify("sail_too_far", "Ziel zu weit entfernt!", 3f);
                    return;
                }
            }

            // Set destination
            destination = mousePos;
            hasDestination = true;

            // Move ship
            if (targetShip != null)
            {
                targetShip.MoveTo(destination);

                // Hide indicator and disable sailing mode
                if (currentIndicator != null)
                    currentIndicator.SetActive(false);

                // Disable sailing mode after setting destination
                // Ship will continue moving automatically
                StartCoroutine(DelayedDisable());
            }
        }
    }

    private System.Collections.IEnumerator DelayedDisable()
    {
        yield return new WaitForSeconds(0.5f);
        DisableSailingMode();
    }

    private void UpdateIndicator()
    {
        if (currentIndicator == null || !hasDestination) return;

        currentIndicator.transform.position = new Vector3(destination.x, destination.y, -0.3f);
        currentIndicator.SetActive(true);

        // Pulse animation
        float scale = indicatorSize * (1f + 0.2f * Mathf.Sin(Time.time * 4f));
        currentIndicator.transform.localScale = Vector3.one * scale;
    }

    public bool IsActive()
    {
        return isActive;
    }

    public Ship GetTargetShip()
    {
        return targetShip;
    }
}