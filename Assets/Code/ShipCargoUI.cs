using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Ship Cargo UI Panel - im Stil des BuildingInfoPanel (rechts eingeschoben).
/// Zeigt Schiffs-Fracht und nächstes Lagerhaus mit Klick-Transfer.
/// </summary>
public class ShipCargoUI : MonoBehaviour
{
    public static ShipCargoUI Instance { get; private set; }

    // ── Stil-Farben (identisch zu BuildingInfoPanel) ─────────────────────
    private readonly Color bgColor     = new Color(0.10f, 0.06f, 0.02f, 0.97f);
    private readonly Color borderColor = new Color(0.72f, 0.52f, 0.18f, 1.00f);
    private readonly Color labelColor  = new Color(0.90f, 0.78f, 0.52f, 0.85f);
    private readonly Color valueColor  = new Color(1.00f, 0.95f, 0.75f, 1.00f);
    private readonly Color btnDanger   = new Color(0.65f, 0.10f, 0.08f, 1.00f);
    private readonly Color btnNeutral  = new Color(0.25f, 0.42f, 0.18f, 1.00f);
    private readonly Color slotColor   = new Color(0.22f, 0.15f, 0.08f, 1f);
    private readonly Color highlightColor = new Color(0.5f, 0.8f, 1f, 1f);

    // ── Panel-Objekte ────────────────────────────────────────────────────
    private GameObject panelRoot;
    private RectTransform rootRT;
    private TextMeshProUGUI txtName;
    private TextMeshProUGUI txtCrew;
    private TextMeshProUGUI txtCapacity;
    private Button btnClose;
    private Button btnSail;
    private Transform shipCargoContainer;
    private Transform warehouseContainer;
    private GameObject itemSlotPrefab;

    // ── Animation ────────────────────────────────────────────────────────
    private float targetPosX = 480f;
    private float currentPosX = 480f;
    private const float AnimSpeed = 12f;
    private bool isPanelActive = false;

    // ── State ────────────────────────────────────────────────────────────
    private Ship currentShip;
    private Warehouse currentWarehouse;
    private List<GameObject> shipItemSlots = new List<GameObject>();
    private List<GameObject> warehouseItemSlots = new List<GameObject>();

    // ────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (panelRoot == null) BuildPanel();
        if (rootRT != null)
        {
            if (!isPanelActive)
            {
                currentPosX = 480f;
                targetPosX = 480f;
            }
            rootRT.anchoredPosition = new Vector2(currentPosX, 0f);
        }
        if (!isPanelActive) panelRoot.SetActive(false);
    }

    private void Update()
    {
        currentPosX = Mathf.Lerp(currentPosX, targetPosX, Time.deltaTime * AnimSpeed);
        if (rootRT != null)
            rootRT.anchoredPosition = new Vector2(currentPosX, 0f);

        if (!isPanelActive && currentPosX >= 470f)
        {
            currentShip = null;
            currentWarehouse = null;
            panelRoot.SetActive(false);
            return;
        }

        if (currentShip != null && isPanelActive)
        {
            UpdateCrewStatus();
            UpdateCapacityDisplay();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            HideShipUI();
    }

    // ── API ──────────────────────────────────────────────────────────────

    public void ShowShipUI(Ship ship)
    {
        if (ship == null) return;

        if (panelRoot == null)
        {
            BuildPanel();
            panelRoot.SetActive(false);
        }

        currentShip = ship;

        // Auto-crewing falls noch kein Matrose zugewiesen
        if (!ship.HasCrew())
        {
            ship.AssignFirstAvailableVillager();
        }

        currentWarehouse = FindNearestWarehouse(ship.transform.position);

        isPanelActive = true;
        panelRoot.SetActive(true);

        if (currentPosX >= 470f) currentPosX = 480f;
        targetPosX = -30f;

        if (txtName != null) txtName.text = ship.GetShipName().ToUpper();
        UpdateCrewStatus();
        UpdateCapacityDisplay();
        Debug.Log($"[ShipCargoUI] ShowShipUI: warehouse={(currentWarehouse != null ? currentWarehouse.name : "NULL")}, cargoCount={ship.GetCargoCount()}, freeSpace={ship.GetFreeCargoSpace()}");
        UpdateAllSlots();
    }

    public void HideShipUI()
    {
        targetPosX = 480f;
        isPanelActive = false;
    }

    public bool IsVisible => isPanelActive;

    // ── Panel bauen ──────────────────────────────────────────────────────

    private void BuildPanel()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var cGO = new GameObject("ShipCargo_Canvas");
            canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            cGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            cGO.AddComponent<GraphicRaycaster>();
        }

        // ── Äußerer Rahmen ───────────────────────────────────────────────
        panelRoot = MakeImage("ShipCargoPanel", canvas.transform, borderColor);
        rootRT = panelRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(1f, 0.5f);
        rootRT.anchorMax = new Vector2(1f, 0.5f);
        rootRT.pivot = new Vector2(1f, 0.5f);
        rootRT.sizeDelta = new Vector2(520f, 640f);

        var outline = panelRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.6f);
        outline.effectDistance = new Vector2(4f, -4f);

        // ── Innerer Hintergrund ──────────────────────────────────────────
        var inner = MakeImage("InnerBG", panelRoot.transform, bgColor);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(3, 3); innerRT.offsetMax = new Vector2(-3, -3);

        var vl = inner.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(16, 16, 16, 16);
        vl.spacing = 10f;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        // ── Header ───────────────────────────────────────────────────────
        var headerRow = MakeLayoutRow("HeaderRow", inner.transform, 0f, 40f);
        txtName = MakeTMP("ShipName", headerRow.transform, "", 20f, FontStyles.Bold, valueColor, TextAlignmentOptions.Left);
        var nameLE = txtName.gameObject.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1f;

        btnClose = MakeButton("CloseBtn", headerRow.transform, "✕", btnDanger, 34f, 34f, 16f);
        btnClose.onClick.AddListener(HideShipUI);

        MakeDivider(inner.transform);

        // ── Crew & Capacity ──────────────────────────────────────────────
        txtCrew = MakeTMP("CrewStatus", inner.transform, "", 15f, FontStyles.Normal, labelColor);
        AddLE(txtCrew.gameObject, minH: 24f);

        txtCapacity = MakeTMP("Capacity", inner.transform, "", 15f, FontStyles.Normal, labelColor);
        AddLE(txtCapacity.gameObject, minH: 24f);

        MakeDivider(inner.transform);

        // ── Cargo-Bereich (Schiff links / Lagerhaus rechts) ──────────────
        var cargoRow = new GameObject("CargoRow", typeof(RectTransform));
        cargoRow.transform.SetParent(inner.transform, false);
        var cargoHL = cargoRow.AddComponent<HorizontalLayoutGroup>();
        cargoHL.spacing = 10f;
        cargoHL.childAlignment = TextAnchor.UpperLeft;
        cargoHL.childForceExpandWidth = true;
        cargoHL.childForceExpandHeight = false;
        cargoHL.padding = new RectOffset(4, 4, 4, 4);
        AddLE(cargoRow, minH: 180f, flexW: 1f, flexH: 1f);

        // Linke Spalte: Schiff
        var shipCol = new GameObject("ShipColumn", typeof(RectTransform));
        shipCol.transform.SetParent(cargoRow.transform, false);
        var shipVL = shipCol.AddComponent<VerticalLayoutGroup>();
        shipVL.spacing = 4f;
        shipVL.childAlignment = TextAnchor.UpperLeft;
        shipVL.childForceExpandWidth = true;
        shipVL.childForceExpandHeight = false;
        var shipColLE = shipCol.AddComponent<LayoutElement>();
        shipColLE.flexibleWidth = 1f;

        var shipLabel = MakeTMP("ShipLabel", shipCol.transform, "Schiffs-Fracht", 13f, FontStyles.Bold, labelColor);
        AddLE(shipLabel.gameObject, minH: 20f);

        var shipContGO = new GameObject("ShipSlots", typeof(RectTransform));
        shipContGO.transform.SetParent(shipCol.transform, false);
        var shipContHL = shipContGO.AddComponent<GridLayoutGroup>();
        shipContHL.cellSize = new Vector2(60f, 60f);
        shipContHL.spacing = new Vector2(4f, 4f);
        shipContHL.constraint = GridLayoutGroup.Constraint.Flexible;
        shipContHL.childAlignment = TextAnchor.UpperLeft;
        shipContHL.startCorner = GridLayoutGroup.Corner.UpperLeft;
        shipContHL.startAxis = GridLayoutGroup.Axis.Horizontal;
        var shipContLE = shipContGO.AddComponent<LayoutElement>();
        shipContLE.flexibleWidth = 1f;
        shipContLE.flexibleHeight = 1f;
        shipContLE.minHeight = 64f;
        shipCargoContainer = shipContGO.transform;

        // Rechte Spalte: Lagerhaus
        var whCol = new GameObject("WarehouseColumn", typeof(RectTransform));
        whCol.transform.SetParent(cargoRow.transform, false);
        var whVL = whCol.AddComponent<VerticalLayoutGroup>();
        whVL.spacing = 4f;
        whVL.childAlignment = TextAnchor.UpperLeft;
        whVL.childForceExpandWidth = true;
        whVL.childForceExpandHeight = false;
        var whColLE = whCol.AddComponent<LayoutElement>();
        whColLE.flexibleWidth = 1f;

        var whLabel = MakeTMP("WarehouseLabel", whCol.transform, "Lagerhaus", 13f, FontStyles.Bold, labelColor);
        AddLE(whLabel.gameObject, minH: 20f);

        var whContGO = new GameObject("WarehouseSlots", typeof(RectTransform));
        whContGO.transform.SetParent(whCol.transform, false);
        var whContHL = whContGO.AddComponent<GridLayoutGroup>();
        whContHL.cellSize = new Vector2(60f, 60f);
        whContHL.spacing = new Vector2(4f, 4f);
        whContHL.constraint = GridLayoutGroup.Constraint.Flexible;
        whContHL.childAlignment = TextAnchor.UpperLeft;
        whContHL.startCorner = GridLayoutGroup.Corner.UpperLeft;
        whContHL.startAxis = GridLayoutGroup.Axis.Horizontal;
        var whContLE = whContGO.AddComponent<LayoutElement>();
        whContLE.flexibleWidth = 1f;
        whContLE.flexibleHeight = 1f;
        whContLE.minHeight = 64f;
        warehouseContainer = whContGO.transform;

        // ── Spacer ───────────────────────────────────────────────────────
        var spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(inner.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

        MakeDivider(inner.transform);

        // ── Buttons ──────────────────────────────────────────────────────
        var btnRow = MakeLayoutRow("BtnRow", inner.transform, 10f, 48f);

        btnSail = MakeButton("SailBtn", btnRow.transform, "Segeln", btnNeutral, 170f, 44f, 15f);
        btnSail.onClick.AddListener(OnSailButtonClicked);

        var btnUnload = MakeButton("UnloadBtn", btnRow.transform, "Alles entladen", btnNeutral, 170f, 44f, 14f);
        btnUnload.onClick.AddListener(OnUnloadAllClicked);

        // ── Item-Slot Prefab ─────────────────────────────────────────────
        itemSlotPrefab = new GameObject("ItemSlotTemplate");
        itemSlotPrefab.transform.SetParent(panelRoot.transform, false);
        itemSlotPrefab.SetActive(false);
        var slotRT = itemSlotPrefab.AddComponent<RectTransform>();
        slotRT.sizeDelta = new Vector2(60f, 60f);
        var slotImg = itemSlotPrefab.AddComponent<Image>();
        slotImg.color = slotColor;
        var slotOutline = itemSlotPrefab.AddComponent<Outline>();
        slotOutline.effectColor = borderColor;
        slotOutline.effectDistance = new Vector2(2f, -2f);
        var slotLE = itemSlotPrefab.AddComponent<LayoutElement>();
        slotLE.preferredWidth = 60;
        slotLE.preferredHeight = 60;

        Debug.Log("[ShipCargoUI] Panel erstellt.");
    }

    // ── Helper (identisch zu BuildingInfoPanel) ──────────────────────────

    private static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TextMeshProUGUI MakeTMP(string name, Transform parent, string text,
        float size, FontStyles style, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
        tmp.color = color; tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private Button MakeButton(string name, Transform parent, string label,
        Color bg, float w, float h, float fontSize)
    {
        var go = MakeImage(name, parent, bg);
        var btn = go.AddComponent<Button>();

        var cols = btn.colors;
        cols.highlightedColor = new Color(
            Mathf.Min(bg.r * 1.3f, 1f), Mathf.Min(bg.g * 1.3f, 1f), Mathf.Min(bg.b * 1.3f, 1f));
        cols.pressedColor = new Color(bg.r * 0.7f, bg.g * 0.7f, bg.b * 0.7f);
        btn.colors = cols;
        btn.targetGraphic = go.GetComponent<Image>();

        var outline = go.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var txt = MakeTMP("Label", go.transform, label, fontSize, FontStyles.Bold, valueColor,
            TextAlignmentOptions.Center);
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;
        txt.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;

        return btn;
    }

    private static GameObject MakeLayoutRow(string name, Transform parent, float spacing, float minH = 40f)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = spacing;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        var le = row.AddComponent<LayoutElement>();
        le.minHeight = minH;
        le.flexibleWidth = 1f;
        return row;
    }

    private static void MakeDivider(Transform parent)
    {
        var div = MakeImage("Divider", parent, new Color(0.72f, 0.52f, 0.18f, 0.35f));
        div.AddComponent<LayoutElement>().minHeight = 2f;
    }

    private static void AddLE(GameObject go, float minW = -1f, float minH = -1f,
        float flexW = -1f, float flexH = -1f)
    {
        var le = go.AddComponent<LayoutElement>();
        if (minW >= 0) le.minWidth = minW;
        if (minH >= 0) le.minHeight = minH;
        if (flexW >= 0) le.flexibleWidth = flexW;
        if (flexH >= 0) le.flexibleHeight = flexH;
    }

    // ── Cargo-Slots ──────────────────────────────────────────────────────

    private void UpdateAllSlots()
    {
        ClearSlots();
        PopulateShipSlots();
        PopulateWarehouseSlots();
    }

    private void ClearSlots()
    {
        foreach (var s in shipItemSlots) Destroy(s);
        shipItemSlots.Clear();
        foreach (var s in warehouseItemSlots) Destroy(s);
        warehouseItemSlots.Clear();
    }

    private void PopulateShipSlots()
    {
        if (currentShip == null || shipCargoContainer == null)
        {
            Debug.LogWarning($"[ShipCargoUI] PopulateShipSlots skipped: ship={(currentShip != null)} container={(shipCargoContainer != null)}");
            return;
        }
        var cargo = currentShip.GetAllCargo();
        Debug.Log($"[ShipCargoUI] Ship cargo raw: {cargo.Count} items");
        var grouped = new Dictionary<string, int>();
        foreach (var item in cargo)
        {
            if (!grouped.ContainsKey(item)) grouped[item] = 0;
            grouped[item]++;
        }
        Debug.Log($"[ShipCargoUI] Ship grouped: {grouped.Count} types");
        foreach (var kvp in grouped)
            CreateItemSlot(shipCargoContainer, kvp.Key, kvp.Value, true);

        int empty = currentShip.GetFreeCargoSpace();
        Debug.Log($"[ShipCargoUI] Empty ship slots: {empty}");
        for (int i = 0; i < Mathf.Min(empty, 8); i++)
            CreateEmptySlot(shipCargoContainer);
    }

    private void PopulateWarehouseSlots()
    {
        if (currentWarehouse == null || warehouseContainer == null)
        {
            Debug.LogWarning($"[ShipCargoUI] PopulateWarehouseSlots skipped: warehouse={(currentWarehouse != null)} container={(warehouseContainer != null)}");
            return;
        }

        var resources = currentWarehouse.GetAllResources();
        Debug.Log($"[ShipCargoUI] Warehouse resources: {resources.Count} types");
        foreach (var kvp in resources)
            CreateItemSlot(warehouseContainer, kvp.Key, kvp.Value, false);

        int empty = currentWarehouse.GetFreeStorageSpace();
        Debug.Log($"[ShipCargoUI] Warehouse empty slots: {empty}");
        for (int i = 0; i < Mathf.Min(empty, 8); i++)
            CreateEmptySlot(warehouseContainer);
    }

    private void CreateItemSlot(Transform parent, string resourceId, int amount, bool fromShip)
    {
        if (itemSlotPrefab == null)
        {
            Debug.LogWarning("[ShipCargoUI] itemSlotPrefab is NULL — skipping slot creation");
            return;
        }

        var slotObj = Instantiate(itemSlotPrefab, parent);
        slotObj.SetActive(true);
        slotObj.name = $"Slot_{resourceId}";

        // Icon
        var iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(slotObj.transform, false);
        var iconImg = iconGO.AddComponent<Image>();
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = new Vector2(4, 4); iconRT.offsetMax = new Vector2(-4, -4);

        Sprite icon = Resources.Load<Sprite>($"{char.ToUpper(resourceId[0]) + resourceId.Substring(1)}_Icon");
        if (icon == null) icon = Resources.Load<Sprite>($"{resourceId}_Icon");
        if (icon != null) iconImg.sprite = icon;

        // Amount (unten rechts auf dem Slot)
        var amtGO = new GameObject("Amount", typeof(RectTransform));
        amtGO.transform.SetParent(slotObj.transform, false);
        var amtText = amtGO.AddComponent<TextMeshProUGUI>();
        amtText.text = amount.ToString();
        amtText.fontSize = 13f;
        amtText.fontStyle = FontStyles.Bold;
        amtText.color = valueColor;
        amtText.alignment = TextAlignmentOptions.BottomRight;
        var amtRT = amtGO.GetComponent<RectTransform>();
        amtRT.anchorMin = Vector2.zero; amtRT.anchorMax = Vector2.one;
        amtRT.offsetMin = new Vector2(2, 2); amtRT.offsetMax = new Vector2(-4, -4);

        // CargoItemSlot component
        var cargoSlot = slotObj.AddComponent<CargoItemSlot>();
        cargoSlot.Initialize(resourceId, amount, fromShip);

        // Button for click
        var btn = slotObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = highlightColor;
        colors.pressedColor = new Color(0.3f, 0.2f, 0.1f);
        btn.colors = colors;
        btn.targetGraphic = slotObj.GetComponent<Image>();
        btn.onClick.AddListener(() => OnSlotClicked(resourceId, fromShip));

        if (fromShip) shipItemSlots.Add(slotObj);
        else warehouseItemSlots.Add(slotObj);
    }

    private void CreateEmptySlot(Transform parent)
    {
        if (itemSlotPrefab == null) return;

        var slotObj = Instantiate(itemSlotPrefab, parent);
        slotObj.SetActive(true);
        slotObj.name = "EmptySlot";
        slotObj.GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.05f, 0.6f);

        var amtGO = new GameObject("Dash", typeof(RectTransform));
        amtGO.transform.SetParent(slotObj.transform, false);
        var amtText = amtGO.AddComponent<TextMeshProUGUI>();
        amtText.text = "-";
        amtText.fontSize = 18f;
        amtText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        amtText.alignment = TextAlignmentOptions.Center;
        var amtRT = amtGO.GetComponent<RectTransform>();
        amtRT.anchorMin = Vector2.zero; amtRT.anchorMax = Vector2.one;
        amtRT.sizeDelta = Vector2.zero;

        if (parent == shipCargoContainer) shipItemSlots.Add(slotObj);
        else warehouseItemSlots.Add(slotObj);
    }

    private void OnSlotClicked(string resourceId, bool fromShip)
    {
        if (currentShip == null) return;

        if (fromShip)
        {
            if (currentWarehouse != null && currentWarehouse.CanStoreMore())
            {
                currentShip.RemoveCargo(resourceId);
                currentWarehouse.ReceiveResource(resourceId, 1);
            }
        }
        else
        {
            if (currentWarehouse != null && currentWarehouse.HasResource(resourceId, 1))
            {
                if (currentShip.CanAddCargo())
                {
                    currentWarehouse.SpendResource(resourceId, 1);
                    currentShip.AddCargo(resourceId);
                }
            }
        }

        UpdateAllSlots();
        UpdateCapacityDisplay();
    }

    // ── Updates ──────────────────────────────────────────────────────────

    private void UpdateCrewStatus()
    {
        if (currentShip == null || txtCrew == null) return;
        bool hasCrew = currentShip.HasCrew();
        txtCrew.text = hasCrew
            ? "<color=#88FF88>Besatzung: ✓</color>"
            : "<color=#FF6644>Besatzung: ✗ (benötigt Dorfbewohner!)</color>";

        if (btnSail != null) btnSail.interactable = hasCrew;
    }

    private void UpdateCapacityDisplay()
    {
        if (currentShip == null || txtCapacity == null) return;
        int current = currentShip.GetCargoCount();
        int capacity = currentShip.GetCargoCapacity();
        txtCapacity.text = $"Ladung: {current}/{capacity}";
    }

    // ── Buttons ──────────────────────────────────────────────────────────

    private void OnSailButtonClicked()
    {
        if (currentShip == null) return;
        if (!currentShip.HasCrew())
        {
            NotificationManager.Instance?.Notify("ship_no_crew",
                "Ein Schiff braucht einen Dorfbewohner!", 5f);
            return;
        }
        HideShipUI();

        ShipSailingMode sailing = ShipSailingMode.Instance;
        if (sailing == null)
        {
            GameObject go = new GameObject("ShipSailingMode");
            sailing = go.AddComponent<ShipSailingMode>();
        }
        sailing.EnableSailingMode(currentShip);
    }

    private void OnUnloadAllClicked()
    {
        if (currentShip == null || currentWarehouse == null) return;
        currentShip.UnloadToWarehouse(currentWarehouse);
        UpdateAllSlots();
        UpdateCapacityDisplay();
    }

    // ── Utility ──────────────────────────────────────────────────────────

    private Warehouse FindNearestWarehouse(Vector3 position)
    {
        Warehouse[] whs = FindObjectsOfType<Warehouse>();
        Warehouse nearest = null;
        float minDist = float.MaxValue;
        foreach (var wh in whs)
        {
            if (wh.isLocal)
            {
                float dist = Vector3.Distance(position, wh.transform.position);
                if (dist < minDist) { minDist = dist; nearest = wh; }
            }
        }
        return nearest;
    }
}

/// <summary>
/// Einzelner Cargo-Slot (Datenhalter für resourceId, amount, fromShip)
/// </summary>
public class CargoItemSlot : MonoBehaviour
{
    public string resourceId { get; private set; }
    public int amount { get; private set; }
    public bool fromShip { get; private set; }

    public void Initialize(string resourceId, int amount, bool fromShip)
    {
        this.resourceId = resourceId;
        this.amount = amount;
        this.fromShip = fromShip;
    }
}
