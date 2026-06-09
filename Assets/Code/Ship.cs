using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Ship : MonoBehaviour
{
    [Header("Ship Configuration")]
    public ShipData shipData;
    public bool isLocal = true;
    
    [Header("Cargo System")]
    [SerializeField] private List<string> cargoItems = new List<string>();  // Resource IDs stored
    public int cargoCapacity = 10;
    
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 180f;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private bool isSelected = false;
    private float targetAngle = 0f;
    private float currentAngle = 0f;
    
    [Header("Crew")]
    public Villager assignedCrew;  // The villager sailing this ship
    
    [Header("Visual")]
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    public Color selectedColor = new Color(0.3f, 0.85f, 1f, 1f);
    
    // Events
    public System.Action<Ship> OnShipClicked;
    public System.Action OnCargoChanged;
    
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        if (shipData != null)
        {
            cargoCapacity = shipData.cargoCapacity;
            moveSpeed = shipData.moveSpeed;
            rotationSpeed = shipData.rotationSpeed;
        }
        
        // Aktuelle Rotation vom BuildingManager übernehmen (nicht überschreiben)
        currentAngle = transform.rotation.eulerAngles.z;
    }
    
    private void Start()
    {
        // Add click detector
        var collider = GetComponent<Collider2D>();
        if (collider == null)
        {
            var bc = gameObject.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(1.5f, 1.5f);
        }

        // Subscribe to cargo changes
        OnCargoChanged += UpdateVisualState;

        // Automatisch einen arbeitslosen Dorfbewohner als Besatzung zuweisen
        if (!HasCrew() && VillagerManager.Instance != null)
        {
            AssignFirstAvailableVillager();
        }
    }

    private void UpdateVisualState()
    {
        // Visual feedback when cargo changes - show cargo level via slight color change
        if (spriteRenderer != null)
        {
            float cargoRatio = (float)cargoItems.Count / cargoCapacity;
            spriteRenderer.color = Color.Lerp(originalColor, selectedColor, cargoRatio * 0.3f);
        }
    }
    
    private void Update()
    {
        HandleMovement();
        HandleClickDetection();
    }
    
    private void HandleClickDetection()
    {
        if (!isLocal) return;
        
        // Check if mouse is over this ship
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
        
        if (hit != null && hit.gameObject == gameObject)
        {
            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Check if shift is held for multi-select, otherwise single click
                if (UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed)
                {
                    ToggleSelection();
                }
                else
                {
                    SelectShip();
                }
            }
        }
    }
    
    private void HandleMovement()
    {
        if (!isMoving) return;
        
        Vector3 direction = targetPosition - transform.position;
        direction.z = 0;
        
        if (direction.magnitude < 0.1f)
        {
            StopMovement();
            return;
        }
        
        // Calculate target angle (ship points UP, so use -90 offset)
        targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        
        // Smooth rotation towards target
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
        float rotationStep = rotationSpeed * Time.deltaTime;
        
        if (Mathf.Abs(angleDiff) < rotationStep)
        {
            currentAngle = targetAngle;
        }
        else
        {
            currentAngle += Mathf.Sign(angleDiff) * rotationStep;
        }
        
        transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);
        
        // Move in the direction the ship is facing (up for this game)
        Vector3 moveDirection = transform.up;  // Ship faces UP
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
        
        // Reveal fog while moving
        RevealFogAlongPath();
    }
    
    public void MoveTo(Vector3 destination)
    {
        // Auto-assign crew if none assigned
        if (!HasCrew())
        {
            AssignFirstAvailableVillager();
        }

        if (!HasCrew())
        {
            Debug.Log("[Ship] Cannot move - no villager assigned as crew!");
            NotificationManager.Instance?.Notify("ship_no_crew", "Ein Schiff braucht einen Dorfbewohner um zu fahren!", 5f);
            return;
        }
        
        // Check if destination is on water
        Vector2Int destGrid = new Vector2Int(Mathf.RoundToInt(destination.x), Mathf.RoundToInt(destination.y));
        if (IslandManager.IsLand(destGrid))
        {
            Debug.Log("[Ship] Cannot move to land - ships can only sail on water!");
            return;
        }
        
        targetPosition = new Vector3(destination.x, destination.y, transform.position.z);
        isMoving = true;
        
        Debug.Log($"[Ship] Moving to {destination}");
    }

    public void AssignFirstAvailableVillager()
    {
        if (VillagerManager.Instance == null) return;

        Villager v = VillagerManager.Instance.GetAvailableVillager();
        if (v != null)
        {
            AssignCrew(v);
            Debug.Log($"[Ship] Auto-assigned villager {v.name} as crew");
        }
    }
    
    private void StopMovement()
    {
        isMoving = false;
        RevealAreaAroundShip();
    }
    
    public void StopMovementCommand()
    {
        isMoving = false;
    }
    
    private void RevealFogAlongPath()
    {
        float revealRadius = 12f;
        FogProjector.RegisterExploration(transform.position, revealRadius);
    }
    
    private void RevealAreaAroundShip()
    {
        float revealRadius = 16f;
        FogProjector.RegisterExploration(transform.position, revealRadius);
    }
    
    private void SelectShip()
    {
        // Deselect all other ships first
        Ship[] allShips = FindObjectsOfType<Ship>();
        foreach (Ship s in allShips)
        {
            s.isSelected = false;
            s.UpdateVisualSelection();
        }
        
        isSelected = true;
        UpdateVisualSelection();
        
        // Open ship UI panel — auto-create if not in scene (wie BuildingInfoPanel)
        ShipCargoUI cargoUI = ShipCargoUI.Instance;
        if (cargoUI == null)
        {
            cargoUI = FindObjectOfType<ShipCargoUI>();
        }
        if (cargoUI == null)
        {
            GameObject go = new GameObject("ShipCargoUI");
            cargoUI = go.AddComponent<ShipCargoUI>();
            Debug.Log("[Ship] ShipCargoUI auto-erstellt.");
        }
        cargoUI.ShowShipUI(this);
    }
    
    private void ToggleSelection()
    {
        isSelected = !isSelected;
        UpdateVisualSelection();
        
        if (isSelected)
        {
            OnShipClicked?.Invoke(this);
        }
    }
    
    private void UpdateVisualSelection()
    {
        if (spriteRenderer == null) return;
        
        if (isSelected)
        {
            spriteRenderer.color = selectedColor;
        }
        else
        {
            spriteRenderer.color = originalColor;
        }
    }
    
    // ── Cargo System ─────────────────────────────────────────────────────────
    
    public bool HasCrew()
    {
        return assignedCrew != null;
    }
    
    public bool AssignCrew(Villager villager)
    {
        if (assignedCrew != null) return false;
        
        assignedCrew = villager;
        villager.AssignToShip(this);
        return true;
    }
    
    public void ReleaseCrew()
    {
        if (assignedCrew != null)
        {
            assignedCrew.ReleaseFromShip();
            assignedCrew = null;
        }
    }
    
    public int GetCargoCount()
    {
        return cargoItems.Count;
    }
    
    public int GetCargoCapacity()
    {
        return cargoCapacity;
    }
    
    public int GetFreeCargoSpace()
    {
        return cargoCapacity - cargoItems.Count;
    }
    
    public bool CanAddCargo()
    {
        return cargoItems.Count < cargoCapacity;
    }
    
    public bool AddCargo(string resourceId)
    {
        if (!CanAddCargo())
        {
            Debug.Log("[Ship] Cargo full!");
            return false;
        }
        
        cargoItems.Add(resourceId);
        OnCargoChanged?.Invoke();
        return true;
    }
    
    public bool AddCargo(string resourceId, int amount)
    {
        bool success = true;
        for (int i = 0; i < amount; i++)
        {
            if (!AddCargo(resourceId))
            {
                success = false;
                break;
            }
        }
        return success;
    }
    
    public bool RemoveCargo(string resourceId)
    {
        if (cargoItems.Remove(resourceId))
        {
            OnCargoChanged?.Invoke();
            return true;
        }
        return false;
    }
    
    public bool RemoveCargo(string resourceId, int amount)
    {
        int removed = 0;
        for (int i = 0; i < amount; i++)
        {
            if (cargoItems.Remove(resourceId))
            {
                removed++;
            }
            else
            {
                break;
            }
        }
        
        if (removed > 0)
        {
            OnCargoChanged?.Invoke();
            return true;
        }
        return false;
    }
    
    public List<string> GetAllCargo()
    {
        return new List<string>(cargoItems);
    }
    
    public int GetCargoCount(string resourceId)
    {
        return cargoItems.Count(x => x == resourceId);
    }
    
    public void ClearCargo()
    {
        cargoItems.Clear();
        OnCargoChanged?.Invoke();
    }
    
    public bool IsMoving()
    {
        return isMoving;
    }
    
    public Vector3 GetTargetPosition()
    {
        return targetPosition;
    }
    
    public bool IsSelected()
    {
        return isSelected;
    }
    
    // ── Ownership & Info ──────────────────────────────────────────────────────
    
    public string GetShipName()
    {
        return shipData != null ? shipData.shipName : gameObject.name;
    }
    
    public ShipType GetShipType()
    {
        return shipData != null ? shipData.shipType : ShipType.Trade;
    }
    
    public bool IsTradeShip()
    {
        return GetShipType() == ShipType.Trade;
    }
    
    public bool IsMilitaryShip()
    {
        return GetShipType() == ShipType.Military;
    }
    
    // ── Unload cargo to warehouse ──────────────────────────────────────────────
    
    public void UnloadToWarehouse(Warehouse warehouse)
    {
        if (warehouse == null) return;
        
        foreach (string item in cargoItems.ToList())
        {
            warehouse.ReceiveResource(item, 1);
            cargoItems.Remove(item);
        }
        
        OnCargoChanged?.Invoke();
    }
    
    // ── Load cargo from warehouse ─────────────────────────────────────────────
    
    public void LoadFromWarehouse(Warehouse warehouse, string resourceId, int amount)
    {
        if (warehouse == null) return;
        
        int loaded = 0;
        for (int i = 0; i < amount && CanAddCargo(); i++)
        {
            if (warehouse.HasResource(resourceId, 1))
            {
                warehouse.SpendResource(resourceId, 1);
                cargoItems.Add(resourceId);
                loaded++;
            }
            else
            {
                break;
            }
        }
        
        if (loaded > 0)
        {
            OnCargoChanged?.Invoke();
        }
    }
    
    private void OnDestroy()
    {
        ReleaseCrew();
    }
}