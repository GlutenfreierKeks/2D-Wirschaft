using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Rechtes Seitenfeld das erscheint wenn ein Gebäude angeklickt wird.
/// Zeigt Stats, Produktion, Verbrauch – mit Pause- und Abriss-Button.
/// Wird vollständig per Code erstellt (kein Prefab nötig).
/// Verwendet das neue Input System und bietet eine geschmeidige Slide-In/Out Animation.
/// </summary>
public class BuildingInfoPanel : MonoBehaviour
{
    public static BuildingInfoPanel Instance;

    // ── Farben (zur Player_UI passend) ──────────────────────────────────────
    private readonly Color bgColor     = new Color(0.10f, 0.06f, 0.02f, 0.97f);
    private readonly Color headerColor = new Color(0.15f, 0.09f, 0.03f, 1.00f);
    private readonly Color borderColor = new Color(0.72f, 0.52f, 0.18f, 1.00f);
    private readonly Color labelColor  = new Color(0.90f, 0.78f, 0.52f, 0.85f);
    private readonly Color valueColor  = new Color(1.00f, 0.95f, 0.75f, 1.00f);
    private readonly Color btnDanger   = new Color(0.65f, 0.10f, 0.08f, 1.00f);
    private readonly Color btnNeutral  = new Color(0.25f, 0.42f, 0.18f, 1.00f);
    private readonly Color btnWarning  = new Color(0.55f, 0.40f, 0.06f, 1.00f);

    // ── Panel-Objekte ────────────────────────────────────────────────────────
    private GameObject panelRoot;
    private RectTransform rootRT;
    private TextMeshProUGUI txtName;
    private TextMeshProUGUI txtStatus;
    private TextMeshProUGUI txtProduces;
    private TextMeshProUGUI txtConsumes;
    private TextMeshProUGUI txtTotalProduced;
    private TextMeshProUGUI txtWorkers;
    private TextMeshProUGUI txtSize;
    private Button          btnPause;
    private TextMeshProUGUI txtPauseLabel;
    private Button          btnDemolish;

    // ── Animation & Positionierung ───────────────────────────────────────────
    private float targetPosX = 480f;    // Vollständig außerhalb des Bildschirms rechts
    private float currentPosX = 480f;
    private float animSpeed = 12f;      // Geschwindigkeit der Slide-Animation
    private bool isPanelActive = false;

    // ── Aktuell geöffnetes Gebäude ───────────────────────────────────────────
    private BuildingInstance currentBuilding;

    // ── Update-Rate ──────────────────────────────────────────────────────────
    private float refreshTimer = 0f;
    private const float RefreshInterval = 0.5f;

    // ────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        BuildPanel();
        
        // Initiale Position auf unsichtbar / außerhalb setzen
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
        // Panel-Position weich interpolieren
        currentPosX = Mathf.Lerp(currentPosX, targetPosX, Time.deltaTime * animSpeed);
        if (rootRT != null)
        {
            rootRT.anchoredPosition = new Vector2(currentPosX, 0f);
        }

        // Wenn wir das Panel schließen und es fast vollständig außerhalb ist, deaktivieren
        if (!isPanelActive && currentPosX >= 470f)
        {
            currentBuilding = null;
            panelRoot.SetActive(false);
            return;
        }

        // Stats updaten falls ein Gebäude aktiv gewählt ist
        if (currentBuilding != null && isPanelActive)
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshInterval;
                RefreshStats();
            }
        }

        // Escape-Taste schließt das Panel über das neue Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Hide();
        }
    }

    // ── Öffentliche API ──────────────────────────────────────────────────────

    public void Show(BuildingInstance building)
    {
        currentBuilding = building;
        isPanelActive = true;
        panelRoot.SetActive(true);
        
        // Wenn es komplett versteckt war, von ganz rechts reinfliegen lassen
        if (currentPosX >= 470f)
        {
            currentPosX = 480f;
        }
        
        targetPosX = -30f; // Sichtbare Position (30px Abstand vom rechten Bildschirmrand)
        refreshTimer = 0f;  // sofort refreshen
    }

    public void Hide()
    {
        targetPosX = 480f; // Slide out nach rechts
        isPanelActive = false;
    }

    public bool IsVisible => isPanelActive;

    // ── Stats aktualisieren ──────────────────────────────────────────────────

    private void RefreshStats()
    {
        if (currentBuilding == null) return;
        BuildingData d = currentBuilding.data;

        txtName.text = d.buildingName.ToUpper();

        // Status
        string statusStr;
        if (!currentBuilding.IsConstructed())
            statusStr = "<color=#FFAA44>⚙ Im Bau...</color>";
        else if (currentBuilding.IsProductionPaused)
            statusStr = "<color=#FF6644>⏸ Produktion pausiert</color>";
        else
            statusStr = "<color=#88FF88>✔ Aktiv</color>";
        txtStatus.text = statusStr;

        // Produktion
        if (!string.IsNullOrEmpty(d.productionResourceId) && d.productionResourceId != "bevolkerung")
            txtProduces.text = $"<b>Produziert:</b>  +{d.productionAmount} {Capitalize(d.productionResourceId)}\n" +
                               $"<b>Intervall:</b> alle {d.productionInterval:F0}s";
        else if (d.producesVillagers)
            txtProduces.text = "<b>Produziert:</b> Dorfbewohner";
        else if (d.productionResourceId == "bevolkerung")
            txtProduces.text = $"Erhöht Bevölkerungs-\nkapazität um {d.productionAmount}";
        else
            txtProduces.text = "<b>Produziert:</b> –";

        // Verbrauch
        txtConsumes.text = (!string.IsNullOrEmpty(d.consumedResourceId) && d.consumedAmount > 0)
            ? $"<b>Verbraucht:</b>  -{d.consumedAmount} {Capitalize(d.consumedResourceId)}/Zyklus"
            : "<b>Verbraucht:</b> –";

        // Gesamtproduktion
        txtTotalProduced.text = $"<b>Gesamt produziert:</b> {currentBuilding.TotalProduced}";

        // Arbeiter / Größe
        txtWorkers.text = $"<b>Arbeiter benötigt:</b> {d.requiredWorkers}";
        txtSize.text    = $"<b>Größe:</b> {d.width} × {d.height} Grid-Felder";

        // Pause-Button Text
        txtPauseLabel.text = currentBuilding.IsProductionPaused ? "▶  Fortsetzen" : "⏸  Pausieren";
        btnPause.gameObject.SetActive(currentBuilding.IsConstructed() &&
            (!string.IsNullOrEmpty(d.productionResourceId) || d.producesVillagers));
    }

    // ── Panel-Aufbau (alles per Code) ────────────────────────────────────────

    private void BuildPanel()
    {
        // Canvas suchen / erstellen
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var cGO = new GameObject("InfoPanel_Canvas");
            canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            cGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            cGO.AddComponent<GraphicRaycaster>();
        }

        // ── Äußerer Rahmen (Deutlich vergrößert für premium Look) ─────────────
        panelRoot = MakeImage("BuildingInfoPanel", canvas.transform, borderColor);
        rootRT = panelRoot.GetComponent<RectTransform>();
        rootRT.anchorMin        = new Vector2(1f, 0.5f);
        rootRT.anchorMax        = new Vector2(1f, 0.5f);
        rootRT.pivot            = new Vector2(1f, 0.5f);
        rootRT.sizeDelta        = new Vector2(400f, 680f); // Vorher 320x520 -> Jetzt groß und übersichtlich
        
        // Schatten / Gold-Outline hinzufügen
        var panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0, 0, 0, 0.6f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        // ── Innerer Hintergrund ──────────────────────────────────────────────
        var inner = MakeImage("InnerBG", panelRoot.transform, bgColor);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(3, 3); innerRT.offsetMax = new Vector2(-3, -3);

        var vl = inner.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(20, 20, 20, 20); // Mehr Spacing/Padding für Premium-Haptik
        vl.spacing = 14f;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        // ── Header-Zeile (Name + Schließen-X) ───────────────────────────────
        var headerRow = MakeLayoutRow("HeaderRow", inner.transform, 0f);

        txtName = MakeTMP("NameLabel", headerRow.transform, "", 23f, FontStyles.Bold, valueColor, TextAlignmentOptions.Left);
        var nameLE = txtName.gameObject.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1f;

        var closeBtn = MakeButton("CloseBtn", headerRow.transform, "✕", btnDanger, 34f, 34f, 18f);
        closeBtn.onClick.AddListener(Hide);

        // ── Trennlinie ───────────────────────────────────────────────────────
        MakeDivider(inner.transform);

        // ── Status ───────────────────────────────────────────────────────────
        txtStatus = MakeTMP("StatusLabel", inner.transform, "", 16f, FontStyles.Normal, labelColor);
        AddLE(txtStatus.gameObject, minH: 24f);

        // ── Trennlinie ───────────────────────────────────────────────────────
        MakeDivider(inner.transform);

        // ── Stats-Block ──────────────────────────────────────────────────────
        txtProduces      = MakeStatRow(inner.transform, "");
        txtConsumes      = MakeStatRow(inner.transform, "");
        txtTotalProduced = MakeStatRow(inner.transform, "");

        MakeDivider(inner.transform);

        txtWorkers = MakeStatRow(inner.transform, "");
        txtSize    = MakeStatRow(inner.transform, "");

        // ── Spacer ───────────────────────────────────────────────────────────
        var spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(inner.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

        MakeDivider(inner.transform);

        // ── Buttons ──────────────────────────────────────────────────────────
        var btnRow = MakeLayoutRow("BtnRow", inner.transform, 16f);

        btnPause = MakeButton("PauseBtn", btnRow.transform, "⏸  Pausieren", btnWarning, 170f, 48f, 15f);
        txtPauseLabel = btnPause.GetComponentInChildren<TextMeshProUGUI>();
        btnPause.onClick.AddListener(OnPauseClicked);
        AddLE(btnPause.gameObject, minW: 170f, minH: 48f);

        btnDemolish = MakeButton("DemolishBtn", btnRow.transform, "🔨 Abreißen", btnDanger, 170f, 48f, 15f);
        btnDemolish.onClick.AddListener(OnDemolishClicked);
        AddLE(btnDemolish.gameObject, minW: 170f, minH: 48f);
    }

    // ── Button-Callbacks ─────────────────────────────────────────────────────

    private void OnPauseClicked()
    {
        if (currentBuilding == null) return;
        currentBuilding.ToggleProduction();
        RefreshStats();
    }

    private void OnDemolishClicked()
    {
        if (currentBuilding == null) return;
        currentBuilding.Demolish();
        Hide();
    }

    // ── Helper-Methoden ──────────────────────────────────────────────────────

    private string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

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
        tmp.enableWordWrapping = true;
        return tmp;
    }

    private TextMeshProUGUI MakeStatRow(Transform parent, string text)
    {
        var lbl = MakeTMP("Stat", parent, text, 15f, FontStyles.Normal, valueColor);
        AddLE(lbl.gameObject, minH: 32f);
        return lbl;
    }

    private static void MakeDivider(Transform parent)
    {
        var div = MakeImage("Divider", parent, new Color(0.72f, 0.52f, 0.18f, 0.35f));
        AddLE(div, minH: 2f);
    }

    private static GameObject MakeLayoutRow(string name, Transform parent, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = spacing;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childForceExpandHeight = false;
        hl.childForceExpandWidth = false;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        return go;
    }

    private Button MakeButton(string name, Transform parent, string label,
        Color bg, float w, float h, float fontSize)
    {
        var go = MakeImage(name, parent, bg);
        var btn = go.AddComponent<Button>();

        var cols = btn.colors;
        cols.highlightedColor = new Color(bg.r * 1.3f, bg.g * 1.3f, bg.b * 1.3f);
        cols.pressedColor     = new Color(bg.r * 0.7f, bg.g * 0.7f, bg.b * 0.7f);
        btn.colors = cols;
        btn.targetGraphic = go.GetComponent<Image>();

        var outline = go.AddComponent<Outline>();
        outline.effectColor    = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);

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
    private static void AddLE(Component c, float minW = 0f, float minH = 0f) => AddLE(c.gameObject, minW, minH);
}
