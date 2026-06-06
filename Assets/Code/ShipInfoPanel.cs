using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI panel shown when a ship is selected. Lets the player load soldiers/workers,
/// set a sail destination, and unload passengers.
/// </summary>
public class ShipInfoPanel : MonoBehaviour
{
    public static ShipInfoPanel Instance;

    // ── Colors ────────────────────────────────────────────────────────────────
    private readonly Color bgColor     = new Color(0.06f, 0.10f, 0.16f, 0.97f);
    private readonly Color headerColor = new Color(0.08f, 0.14f, 0.22f, 1.00f);
    private readonly Color borderColor = new Color(0.25f, 0.60f, 0.90f, 1.00f);
    private readonly Color labelColor  = new Color(0.70f, 0.88f, 1.00f, 0.85f);
    private readonly Color valueColor  = new Color(0.92f, 0.97f, 1.00f, 1.00f);
    private readonly Color btnBlue     = new Color(0.15f, 0.38f, 0.62f, 1.00f);
    private readonly Color btnGreen    = new Color(0.15f, 0.50f, 0.22f, 1.00f);
    private readonly Color btnOrange   = new Color(0.60f, 0.35f, 0.06f, 1.00f);
    private readonly Color btnDanger   = new Color(0.55f, 0.10f, 0.08f, 1.00f);

    // ── Panel objects ─────────────────────────────────────────────────────────
    private GameObject panelRoot;
    private RectTransform rootRT;
    private TextMeshProUGUI txtTitle;
    private TextMeshProUGUI txtStatus;
    private TextMeshProUGUI txtPassengers;
    private TextMeshProUGUI txtHint;
    private Button btnLoadSoldiers;
    private Button btnLoadWorkers;
    private Button btnSailTo;
    private Button btnUnload;
    private Button btnClose;

    // ── Animation ─────────────────────────────────────────────────────────────
    private float targetPosX = 480f;
    private float currentPosX = 480f;
    private const float AnimSpeed = 12f;
    private bool isPanelActive = false;

    // ── Sail-target mode ──────────────────────────────────────────────────────
    public static bool IsWaitingForSailTarget { get; private set; } = false;

    // ── Current ship ──────────────────────────────────────────────────────────
    private Ship currentShip;
    private float refreshTimer;
    private const float RefreshInterval = 0.33f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        BuildPanel();
        if (rootRT != null)
        {
            currentPosX = 480f;
            targetPosX = 480f;
            rootRT.anchoredPosition = new Vector2(currentPosX, 0f);
        }
        panelRoot.SetActive(false);
    }

    private void Update()
    {
        currentPosX = Mathf.Lerp(currentPosX, targetPosX, Time.deltaTime * AnimSpeed);
        if (rootRT != null) rootRT.anchoredPosition = new Vector2(currentPosX, 0f);

        if (!isPanelActive && currentPosX >= 470f)
        {
            currentShip = null;
            panelRoot.SetActive(false);
            return;
        }

        if (currentShip == null || !currentShip.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        if (currentShip != null && isPanelActive)
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f) { refreshTimer = RefreshInterval; RefreshStats(); }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(Ship ship)
    {
        currentShip = ship;
        isPanelActive = true;
        IsWaitingForSailTarget = false;
        panelRoot.SetActive(true);
        if (currentPosX >= 470f) currentPosX = 480f;
        targetPosX = -30f;
        refreshTimer = 0f;
    }

    public void Hide()
    {
        targetPosX = 480f;
        isPanelActive = false;
        IsWaitingForSailTarget = false;
        if (currentShip != null) currentShip.SetSelected(false);
        currentShip = null;
    }

    public bool IsVisible => isPanelActive;

    /// <summary>Called by SelectionManager when the player clicks a world position while waiting for sail target.</summary>
    public void ReceiveSailTarget(Vector2 worldPos)
    {
        if (!IsWaitingForSailTarget || currentShip == null) return;
        IsWaitingForSailTarget = false;
        currentShip.SailTo(worldPos);
        SetHint("");
        RefreshStats();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void RefreshStats()
    {
        if (currentShip == null) return;

        txtStatus.text = currentShip.GetStatusText();
        txtPassengers.text = $"<b>Passagiere:</b> {currentShip.PassengerCount} / {currentShip.capacity}";

        bool idle = currentShip.State == ShipState.Idle;
        bool hasPax = currentShip.PassengerCount > 0;

        btnLoadSoldiers.interactable = idle && currentShip.FreeSlotsCount > 0;
        btnLoadWorkers.interactable  = idle && currentShip.FreeSlotsCount > 0;
        btnSailTo.interactable       = idle;
        btnUnload.interactable       = idle && hasPax;

        // Sail button label
        var sailLabel = btnSailTo.GetComponentInChildren<TextMeshProUGUI>();
        if (sailLabel != null)
            sailLabel.text = IsWaitingForSailTarget ? "🖱 Klicke Ziel..." : "⛵ Fahren zu...";
    }

    private void SetHint(string text)
    {
        if (txtHint != null) txtHint.text = text;
    }

    private void OnLoadSoldiers()
    {
        if (currentShip == null) return;
        int n = currentShip.LoadNearbySoldiers(6f);
        if (n == 0)
            NotificationManager.Instance?.Notify("ship_none", "Keine Soldaten in Reichweite (6 Felder).", 4f);
        RefreshStats();
    }

    private void OnLoadWorkers()
    {
        if (currentShip == null) return;
        currentShip.LoadNearbyWorkers(40f);
        RefreshStats();
    }



    private void OnSailTo()
    {
        if (currentShip == null) return;
        IsWaitingForSailTarget = !IsWaitingForSailTarget;
        SetHint(IsWaitingForSailTarget ? "Klicke auf das Fahrtziel (Wasser oder anderer Steg)..." : "");
        RefreshStats();
    }

    private void OnUnload()
    {
        if (currentShip == null) return;
        currentShip.UnloadAll();
        RefreshStats();
    }

    // ── Panel builder (all code, no prefab) ──────────────────────────────────

    private void BuildPanel()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var cGO = new GameObject("ShipPanel_Canvas");
            canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 21;
            cGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            cGO.AddComponent<GraphicRaycaster>();
        }

        // Outer border
        panelRoot = MakeImage("ShipInfoPanel", canvas.transform, borderColor);
        rootRT = panelRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(1f, 0.5f);
        rootRT.anchorMax = new Vector2(1f, 0.5f);
        rootRT.pivot     = new Vector2(1f, 0.5f);
        rootRT.sizeDelta = new Vector2(380f, 480f);
        var outline = panelRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(4f, -4f);

        // Inner background
        var inner = MakeImage("InnerBG", panelRoot.transform, bgColor);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(3, 3); innerRT.offsetMax = new Vector2(-3, -3);

        var vl = inner.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(18, 18, 18, 18);
        vl.spacing = 12f;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        // Header row
        var headerRow = MakeRow("HeaderRow", inner.transform);
        txtTitle = MakeTMP("Title", headerRow.transform, "⛵ SCHIFF", 22f, FontStyles.Bold, valueColor, TextAlignmentOptions.Left);
        var titleLE = txtTitle.gameObject.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1f;
        btnClose = MakeButton("CloseBtn", headerRow.transform, "✕", btnDanger, 34f, 34f, 17f);
        btnClose.onClick.AddListener(Hide);

        MakeDivider(inner.transform);

        // Status
        txtStatus = MakeTMP("Status", inner.transform, "Bereit", 15f, FontStyles.Normal, labelColor);
        AddLE(txtStatus.gameObject, minH: 22f);

        // Passengers
        txtPassengers = MakeTMP("Passengers", inner.transform, "Passagiere: 0 / 8", 16f, FontStyles.Bold, valueColor);
        AddLE(txtPassengers.gameObject, minH: 24f);

        MakeDivider(inner.transform);

        // Hint text (shows when waiting for sail target)
        txtHint = MakeTMP("Hint", inner.transform, "", 13f, FontStyles.Italic, new Color(0.6f, 0.9f, 1f, 0.9f));
        AddLE(txtHint.gameObject, minH: 20f);

        // Buttons
        btnLoadSoldiers = MakeButton("LoadSoldiersBtn", inner.transform, "⚔ Soldaten einladen (6 Felder)", btnBlue, 340f, 44f, 14f);
        btnLoadSoldiers.onClick.AddListener(OnLoadSoldiers);
        AddLE(btnLoadSoldiers.gameObject, minW: 340f, minH: 44f);

        btnLoadWorkers = MakeButton("LoadWorkersBtn", inner.transform, "🔨 1 Bauarbeiter rufen", btnGreen, 340f, 44f, 14f);
        btnLoadWorkers.onClick.AddListener(OnLoadWorkers);
        AddLE(btnLoadWorkers.gameObject, minW: 340f, minH: 44f);

        btnSailTo = MakeButton("SailToBtn", inner.transform, "⛵ Fahren zu...", btnOrange, 340f, 44f, 14f);
        btnSailTo.onClick.AddListener(OnSailTo);
        AddLE(btnSailTo.gameObject, minW: 340f, minH: 44f);

        btnUnload = MakeButton("UnloadBtn", inner.transform, "↧ Alle Aussteigen lassen", btnDanger, 340f, 44f, 14f);
        btnUnload.onClick.AddListener(OnUnload);
        AddLE(btnUnload.gameObject, minW: 340f, minH: 44f);
    }

    // ── Helper methods ────────────────────────────────────────────────────────

    private static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float size,
        FontStyles style, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
        tmp.color = color; tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static GameObject MakeRow(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 8f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childForceExpandHeight = false;
        hl.childForceExpandWidth = false;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 38f;
        return go;
    }

    private static void MakeDivider(Transform parent)
    {
        var div = MakeImage("Divider", parent, new Color(0.25f, 0.60f, 0.90f, 0.30f));
        AddLE(div, minH: 2f);
    }

    private Button MakeButton(string name, Transform parent, string label, Color bg, float w, float h, float fontSize)
    {
        var go = MakeImage(name, parent, bg);
        var btn = go.AddComponent<Button>();
        var cols = btn.colors;
        cols.highlightedColor = new Color(bg.r * 1.3f, bg.g * 1.3f, bg.b * 1.3f);
        cols.pressedColor     = new Color(bg.r * 0.7f, bg.g * 0.7f, bg.b * 0.7f);
        cols.disabledColor    = new Color(bg.r * 0.4f, bg.g * 0.4f, bg.b * 0.4f, 0.7f);
        btn.colors = cols;
        btn.targetGraphic = go.GetComponent<Image>();
        var outl = go.AddComponent<Outline>();
        outl.effectColor = borderColor;
        outl.effectDistance = new Vector2(2f, -2f);
        var txt = MakeTMP("Label", go.transform, label, fontSize, FontStyles.Bold, valueColor, TextAlignmentOptions.Center);
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;
        txt.raycastTarget = false;
        return btn;
    }

    private static void AddLE(GameObject go, float minW = 0f, float minH = 0f)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (minW > 0) le.minWidth  = minW;
        if (minH > 0) le.minHeight = minH;
    }
}
