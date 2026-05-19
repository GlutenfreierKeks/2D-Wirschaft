using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using UnityEngine.EventSystems;

/// <summary>
/// Ressourcen-HUD – Main Camera, Vogelperspektive.
///
/// RESSOURCEN ANPASSEN:
///   Im Inspector unter "Ressourcen" Einträge hinzufügen / entfernen.
///   Jede ResourceDefinition hat:
///     • id          – eindeutiger Schlüssel  z.B. "holz"
///     • displayName – Anzeigename            z.B. "Holz"
///     • icon        – Sprite (im Inspector zuweisen)
///     • startValue  – Startwert
///
/// WERT AUS EINEM ANDEREN SKRIPT ÄNDERN:
///   Player_UI.Instance.SetResource("holz", 42);
///   Player_UI.Instance.AddResource("holz", +10);
///   int v = Player_UI.Instance.GetResource("holz");
/// </summary>
public class Player_UI : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static Player_UI Instance { get; private set; }

    // ── Ressourcen-Definition ────────────────────────────────────────────────

    [System.Serializable]
    public class ResourceDefinition
    {
        public string id          = "resource";
        public string displayName = "Ressource";
        public Sprite icon        = null;
        [Min(0)] public int startValue = 0;
        [Min(0)] public int maxValue   = 0; // 0 = kein Maximum
    }

    [Header("Ressourcen (beliebig erweiterbar)")]
    [SerializeField] private List<ResourceDefinition> startingResources = new List<ResourceDefinition>
    {
        new ResourceDefinition { id = "holz",        displayName = "Holz",        startValue = 100 },
        new ResourceDefinition { id = "stein",       displayName = "Stein",       startValue = 100 },
        new ResourceDefinition { id = "eisen",       displayName = "Eisen",       startValue = 0   },
        new ResourceDefinition { id = "gold",        displayName = "Gold",        startValue = 0   },
        new ResourceDefinition { id = "bevolkerung", displayName = "Bevölkerung", startValue = 10, maxValue = 20 },
        new ResourceDefinition { id = "dorfbewohner", displayName = "Freie Arbeiter", startValue = 10, maxValue = 999 },
        new ResourceDefinition { id = "arbeiter",     displayName = "Arbeiter",     startValue = 2,  maxValue = 999 },
    };

    // ── Menü-Definition ──────────────────────────────────────────────────────

    [System.Serializable]
    public class MenuCategory
    {
        public string id          = "category";
        public string displayName = "Kategorie";
    }

    [Header("Menü Kategorien (unten)")]
    [SerializeField] private List<MenuCategory> menuCategories = new List<MenuCategory>
    {
        new MenuCategory { id = "ressourcen", displayName = "Ressourcen" },
        new MenuCategory { id = "hauser",     displayName = "Häuser" },
        new MenuCategory { id = "andere",     displayName = "Andere" }
    };

    [System.Serializable]
    public class MenuItem
    {
        public string id          = "item";
        public string displayName = "Item";
        public Sprite icon        = null;
        public string categoryId  = "hauser";
        public BuildingData buildingData = null; 
    }

    [Header("Submenü Items")]
    [SerializeField] private List<MenuItem> menuItems = new List<MenuItem>();

    // ── Style  (Parchment / Karten-Look) ─────────────────────────────────────

    [Header("Style")]
    [SerializeField] private Color barColor    = new Color(0.18f, 0.10f, 0.03f, 0.96f);   // dunkles Eichenbraun
    [SerializeField] private Color slotColor   = new Color(0.42f, 0.27f, 0.09f, 0.92f);   // warmes Holzbraun
    [SerializeField] private Color borderColor = new Color(0.72f, 0.52f, 0.18f, 1.00f);   // goldene Umrandung
    [SerializeField] private Color labelColor  = new Color(0.90f, 0.78f, 0.52f, 0.85f);   // Pergament-Beige
    [SerializeField] private Color valueColor  = new Color(1.00f, 0.95f, 0.75f, 1.00f);   // helles Cremegold
    [SerializeField] private float barHeight   = 80f;      // größer als vorher
    [SerializeField] private float slotPadding = 18f;
    [SerializeField] private float iconSize    = 44f;      // Icons deutlich größer
    [SerializeField] private float borderWidth = 2f;

    // ── Laufzeit ─────────────────────────────────────────────────────────────

    private readonly Dictionary<string, int>                values = new();
    private readonly Dictionary<string, int>                maxValues = new();
    private readonly Dictionary<string, TextMeshProUGUI>    labels = new();
    private readonly Dictionary<string, string>             currentRates = new();

    private float rateUpdateTimer = 0f;
    private List<string> dummyBreakdown = new List<string>();

    private GameObject mainMenuContainer;
    private GameObject subMenuBlocker;
    private GameObject subMenuContainer;
    private Transform subMenuContent;
    private GameObject soldierCommandContainer;
    private TextMeshProUGUI soldierCommandHintLabel;
    private Button moveCommandButton;
    private Button attackCommandButton;
    private Button observeCommandButton;
    
    // ── Tooltip ──────────────────────────────────────────────────────────────
    private GameObject tooltipPanel;
    private TextMeshProUGUI tooltipTitleText;
    private TextMeshProUGUI tooltipBodyText;
    private string activeTooltipResourceId = "";
    private Vector2 activeTooltipScreenPosition;

    private class RateSourceEntry
    {
        public string Label;
        public int Count;
        public float RatePerUnit;
        public float TotalRate;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        // Pre-initialize critical resources to avoid "Unknown Resource" errors
        EnsureResourceExists("dorfbewohner", 10, 999);
        EnsureResourceExists("arbeiter", 2, 999);
        EnsureResourceExists("bevolkerung", 10, 20);
        EnsureResourceExists("stimmung", 100, 100); // Villager Mood: starts at 100%
        EnsureResourceExists("holz", 100, 999);
        EnsureResourceExists("stein", 100, 999);
        EnsureResourceExists("eisen", 10, 999);
        EnsureResourceExists("gold", 10, 999);
        EnsureResourceExists("weizen", 10, 999);
        EnsureResourceExists("fruechte", 0, 999);
        EnsureResourceExists("fleisch", 0, 999);
        EnsureResourceExists("geld", 0, 999);
        EnsureResourceExists("soldaten", 0, 5); // Start mit Limit 5
    }

    private void EnsureResourceExists(string id, int start, int max)
    {
        if (!values.ContainsKey(id)) values[id] = start;
        if (!maxValues.ContainsKey(id)) maxValues[id] = max;
    }

    private void Start()
    {
        // Sync Time.timeScale with selected lobby speed!
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameSpeed", out object speedObj))
        {
            float speed = System.Convert.ToSingle(speedObj);
            Time.timeScale = speed;
            Debug.Log($"[GameSpeed] Syncing Time.timeScale to selected lobby speed: {speed}x");
        }
        else
        {
            Time.timeScale = 1.0f; // Default standard speed
        }

        BuildUI();
    }

    // ── Öffentliche API ──────────────────────────────────────────────────────
    
    public bool IsSubMenuOpen => subMenuBlocker != null && subMenuBlocker.activeSelf;

    public void SetSoldierCommandState(bool hasSelection, ArmyCommandMode activeMode, bool waitingForSecondObservePoint, string overrideHint = null)
    {
        if (soldierCommandContainer == null)
        {
            return;
        }

        soldierCommandContainer.SetActive(hasSelection);
        if (!hasSelection)
        {
            return;
        }

        SetCommandButtonHighlight(moveCommandButton, activeMode == ArmyCommandMode.Move);
        SetCommandButtonHighlight(attackCommandButton, activeMode == ArmyCommandMode.AttackMove);
        SetCommandButtonHighlight(observeCommandButton, activeMode == ArmyCommandMode.Observe);

        if (!string.IsNullOrEmpty(overrideHint))
        {
            soldierCommandHintLabel.text = overrideHint;
            return;
        }

        if (waitingForSecondObservePoint)
        {
            soldierCommandHintLabel.text = "Wahle jetzt den zweiten Punkt.";
            return;
        }

        switch (activeMode)
        {
            case ArmyCommandMode.Move:
                soldierCommandHintLabel.text = "Marsch aktiv: Klicke ein Ziel auf Land.";
                break;
            case ArmyCommandMode.AttackMove:
                soldierCommandHintLabel.text = "Angriff aktiv: Klicke ein Ziel auf Land.";
                break;
            case ArmyCommandMode.Observe:
                soldierCommandHintLabel.text = "Observieren: Wahle zwei Punkte fur die Route.";
                break;
            default:
                soldierCommandHintLabel.text = "Befehle fur die aktuelle Soldatengruppe.";
                break;
        }
    }

    public void SetResource(string id, int value)
    {
        if (!values.ContainsKey(id))
        {
            Debug.LogWarning($"[Player_UI] Unbekannte Ressource: '{id}'");
            return;
        }

        int max = maxValues[id];
        if (max > 0)
        {
            values[id] = Mathf.Clamp(value, 0, max);
        }
        else
        {
            values[id] = Mathf.Max(0, value);
        }

        if (labels.TryGetValue(id, out var lbl))
        {
            string baseText = "";
            if (id == "stimmung")
                baseText = $"{values[id]}%";
            else if (max > 0)
                baseText = $"{values[id]}/{max}";
            else
                baseText = values[id].ToString();

            string rateText = currentRates.ContainsKey(id) ? currentRates[id] : "";
            lbl.text = baseText + rateText;
        }
    }

    public void AddResource(string id, int delta) => SetResource(id, GetResource(id) + delta);

    public int GetResource(string id) => values.TryGetValue(id, out int v) ? v : 0;
    public int GetMaxResource(string id) => maxValues.TryGetValue(id, out int v) ? v : 0;

    public int GetMaxPopulation() => maxValues.TryGetValue("bevolkerung", out int v) ? v : 0;
    
    public void SetMaxResource(string id, int max)
    {
        if (maxValues.ContainsKey(id))
        {
            maxValues[id] = max;
            SetResource(id, GetResource(id)); // Refresh display
        }
    }

    public void AddMaxPopulation(int delta)
    {
        if (maxValues.TryGetValue("bevolkerung", out int currentMax))
        {
            SetMaxResource("bevolkerung", currentMax + delta);
        }
    }

    // ── UI-Aufbau ────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ──────────────────────────────────────────────────────────
        var canvasGO = new GameObject("PlayerHUD_Canvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        EnsureEventSystemExists();

        // ── Goldener Rahmen (äußere Hülle) ──────────────────────────────────
        var borderGO = new GameObject("BarBorder",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGO.transform.SetParent(canvasGO.transform, false);
        borderGO.GetComponent<Image>().color = borderColor;

        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin        = new Vector2(0f, 1f);
        borderRT.anchorMax        = new Vector2(0f, 1f);
        borderRT.pivot            = new Vector2(0f, 1f);
        borderRT.anchoredPosition = new Vector2(12f, -12f);
        borderRT.sizeDelta        = new Vector2(0f, barHeight + borderWidth * 2f);

        var borderHL = borderGO.AddComponent<HorizontalLayoutGroup>();
        borderHL.padding                = new RectOffset(
            (int)borderWidth, (int)borderWidth,
            (int)borderWidth, (int)borderWidth);
        borderHL.spacing                = 0;
        borderHL.childAlignment         = TextAnchor.MiddleLeft;
        borderHL.childForceExpandHeight = true;
        borderHL.childForceExpandWidth  = false;

        var borderCSF = borderGO.AddComponent<ContentSizeFitter>();
        borderCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Dunkle Leiste (Innenbereich) ─────────────────────────────────────
        var barGO = new GameObject("ResourceBar",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barGO.transform.SetParent(borderGO.transform, false);
        barGO.GetComponent<Image>().color = barColor;

        var barHL = barGO.AddComponent<HorizontalLayoutGroup>();
        barHL.padding                = new RectOffset(8, 8, 0, 0);
        barHL.spacing                = 3f;
        barHL.childAlignment         = TextAnchor.MiddleLeft;
        barHL.childForceExpandHeight = true;
        barHL.childForceExpandWidth  = false;

        var barCSF = barGO.AddComponent<ContentSizeFitter>();
        barCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var barLE = barGO.AddComponent<LayoutElement>();
        barLE.minHeight = barHeight;

        // ── Slots ──────────────────────────────────────────────────────────
        foreach (var def in startingResources)
        {
            if (labels.ContainsKey(def.id)) continue;
            values[def.id] = def.startValue;
            maxValues[def.id] = def.maxValue;
            labels[def.id] = CreateSlot(barGO.transform, def);
        }

        // Ensure critical resources have slots even if not in startingResources list
        EnsureSlot(barGO.transform, "bevolkerung", "Bevölkerung", 10, 20);
        EnsureSlot(barGO.transform, "dorfbewohner", "Freie Arbeiter", 10, 0);
        EnsureSlot(barGO.transform, "stimmung", "Zufriedenheit", 100, 100); // Spawns right next to workers!
        EnsureSlot(barGO.transform, "arbeiter", "Arbeiter", 2, 0);
        EnsureSlot(barGO.transform, "holz", "Holz", 100, 0);
        EnsureSlot(barGO.transform, "stein", "Stein", 100, 0);
        EnsureSlot(barGO.transform, "eisen", "Eisen", 0, 0);
        EnsureSlot(barGO.transform, "gold", "Gold (Erz)", 0, 0);
        EnsureSlot(barGO.transform, "geld", "Geld (Münzen)", 0, 0);
        EnsureSlot(barGO.transform, "weizen", "Weizen", 0, 0);
        EnsureSlot(barGO.transform, "fruechte", "Früchte", 0, 0);
        EnsureSlot(barGO.transform, "fleisch", "Fleisch", 0, 0);

        BuildBottomMenu(canvasGO.transform);
        BuildSoldierCommandMenu(canvasGO.transform);
        BuildTooltipPanel(canvasGO.transform);
    }

    private void EnsureSlot(Transform parent, string id, string name, int start, int max)
    {
        if (labels.ContainsKey(id)) return;
        
        ResourceDefinition def = new ResourceDefinition { id = id, displayName = name, startValue = start, maxValue = max };
        values[id] = start;
        maxValues[id] = max;
        labels[id] = CreateSlot(parent, def);
    }

    // ── Unteres Menü ─────────────────────────────────────────────────────────

    private void BuildBottomMenu(Transform canvasTransform)
    {
        // ── Container unten mittig ──────────────────────────────────────────
        var bottomGO = new GameObject("BottomMenu", typeof(RectTransform));
        bottomGO.transform.SetParent(canvasTransform, false);

        var bottomRT = bottomGO.GetComponent<RectTransform>();
        bottomRT.anchorMin        = new Vector2(0.5f, 0f);
        bottomRT.anchorMax        = new Vector2(0.5f, 0f);
        bottomRT.pivot            = new Vector2(0.5f, 0f);
        bottomRT.anchoredPosition = new Vector2(0f, 20f); // Abstand von unten
        bottomRT.sizeDelta        = new Vector2(600f, 100f);

        // ── Main Menu Container (Die 3 Buttons) ─────────────────────────────
        mainMenuContainer = new GameObject("MainMenu", typeof(RectTransform));
        mainMenuContainer.transform.SetParent(bottomGO.transform, false);
        
        var mainRT = mainMenuContainer.GetComponent<RectTransform>();
        mainRT.anchorMin = Vector2.zero;
        mainRT.anchorMax = Vector2.one;
        mainRT.sizeDelta = Vector2.zero;

        var mainHL = mainMenuContainer.AddComponent<HorizontalLayoutGroup>();
        mainHL.spacing                = 20f;
        mainHL.childAlignment         = TextAnchor.MiddleCenter;
        mainHL.childForceExpandHeight = true;
        mainHL.childForceExpandWidth  = false;

        foreach (var cat in menuCategories)
        {
            CreateMenuButton(mainMenuContainer.transform, cat.displayName, () => OpenSubMenu(cat.displayName));
        }

        // ── Sub Menu Blocker (Full Screen zum Schließen bei Klick daneben) ──
        subMenuBlocker = new GameObject("SubMenuBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        subMenuBlocker.transform.SetParent(canvasTransform, false);
        
        var blockerRT = subMenuBlocker.GetComponent<RectTransform>();
        blockerRT.anchorMin = Vector2.zero;
        blockerRT.anchorMax = Vector2.one;
        blockerRT.sizeDelta = Vector2.zero;
        
        var blockerImg = subMenuBlocker.GetComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0); // Voll transparent
        
        var blockerBtn = subMenuBlocker.GetComponent<Button>();
        blockerBtn.onClick.AddListener(CloseSubMenu);

        // ── Sub Menu Container (Das eigentliche Panel) ──────────────────────
        subMenuContainer = new GameObject("SubMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        subMenuContainer.transform.SetParent(subMenuBlocker.transform, false);
        
        var subImg = subMenuContainer.GetComponent<Image>();
        // Etwas transparenter und edler
        subImg.color = new Color(barColor.r, barColor.g, barColor.b, 0.85f); 

        var subRT = subMenuContainer.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 0f);
        subRT.anchorMax = new Vector2(0.5f, 0f);
        subRT.pivot     = new Vector2(0.5f, 0f);
        subRT.anchoredPosition = new Vector2(0f, 130f); // Über dem Hauptmenü
        subRT.sizeDelta = new Vector2(700f, 250f); // Etwas kleiner


        // Rahmen fürs Submenü (optional, nutzen Outline)
        var subOutline = subMenuContainer.AddComponent<Outline>();
        subOutline.effectColor = borderColor;
        subOutline.effectDistance = new Vector2(borderWidth, -borderWidth);

        // Layout fürs Submenü
        var subVL = subMenuContainer.AddComponent<VerticalLayoutGroup>();
        subVL.padding = new RectOffset(20, 20, 20, 20);
        subVL.spacing = 10f;
        subVL.childAlignment = TextAnchor.UpperCenter;

        // Scrollable Area / Grid für die Items
        var scrollGO = new GameObject("ScrollArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        scrollGO.transform.SetParent(subMenuContainer.transform, false);
        
        // Transparentes Bild für Scroll-Events
        var scrollImg = scrollGO.GetComponent<Image>();
        scrollImg.color = new Color(0,0,0,0);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.viewport = scrollGO.GetComponent<RectTransform>();

        // Maske hinzufügen, damit Items nichts verdecken
        scrollGO.AddComponent<RectMask2D>();

        var scrollLE = scrollGO.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1f;
        scrollLE.flexibleWidth = 1f;

        subMenuContent = new GameObject("Content", typeof(RectTransform)).transform;
        subMenuContent.SetParent(scrollGO.transform, false);
        
        var contentRT = subMenuContent.GetComponent<RectTransform>();
        // Anchors für vertikales Scrolling
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);

        scrollRect.content = contentRT;

        var contentGL = subMenuContent.gameObject.AddComponent<GridLayoutGroup>();
        contentGL.cellSize = new Vector2(160, 160);
        contentGL.spacing = new Vector2(15, 15);
        contentGL.padding = new RectOffset(15, 15, 15, 15);
        contentGL.startCorner = GridLayoutGroup.Corner.UpperLeft;
        contentGL.startAxis = GridLayoutGroup.Axis.Horizontal;
        contentGL.childAlignment = TextAnchor.UpperLeft;

        // Damit der Content mit der Anzahl der Items wächst
        var contentCSF = subMenuContent.gameObject.AddComponent<ContentSizeFitter>();
        contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Startzustand
        subMenuBlocker.SetActive(false);
    }

    private void BuildSoldierCommandMenu(Transform canvasTransform)
    {
        soldierCommandContainer = new GameObject("SoldierCommands", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        soldierCommandContainer.transform.SetParent(canvasTransform, false);

        Image panelImage = soldierCommandContainer.GetComponent<Image>();
        panelImage.color = new Color(barColor.r, barColor.g, barColor.b, 0.92f);

        Outline panelOutline = soldierCommandContainer.AddComponent<Outline>();
        panelOutline.effectColor = borderColor;
        panelOutline.effectDistance = new Vector2(borderWidth, -borderWidth);

        RectTransform panelRT = soldierCommandContainer.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 145f);
        panelRT.sizeDelta = new Vector2(520f, 150f);

        VerticalLayoutGroup layout = soldierCommandContainer.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        GameObject hintGO = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        hintGO.transform.SetParent(soldierCommandContainer.transform, false);
        soldierCommandHintLabel = hintGO.GetComponent<TextMeshProUGUI>();
        soldierCommandHintLabel.text = "Befehle fur die aktuelle Soldatengruppe.";
        soldierCommandHintLabel.fontSize = 20f;
        soldierCommandHintLabel.fontStyle = FontStyles.Bold;
        soldierCommandHintLabel.color = valueColor;
        soldierCommandHintLabel.alignment = TextAlignmentOptions.Center;
        soldierCommandHintLabel.textWrappingMode = TextWrappingModes.Normal;

        GameObject rowGO = new GameObject("CommandRow", typeof(RectTransform));
        rowGO.transform.SetParent(soldierCommandContainer.transform, false);
        HorizontalLayoutGroup rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 14f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        moveCommandButton = CreateIconCommandButton(rowGO.transform, "->", "Gehe da hin", () => SelectionManager.Instance?.BeginMoveCommand());
        attackCommandButton = CreateIconCommandButton(rowGO.transform, "X", "Angreifen", () => SelectionManager.Instance?.BeginAttackCommand());
        observeCommandButton = CreateIconCommandButton(rowGO.transform, "<>", "Observieren", () => SelectionManager.Instance?.BeginObserveCommand());

        soldierCommandContainer.SetActive(false);
    }

    private Button CreateIconCommandButton(Transform parent, string iconText, string labelText, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = new GameObject($"Cmd_{labelText}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.minWidth = 150f;
        le.minHeight = 84f;

        Image img = btnGO.GetComponent<Image>();
        img.color = slotColor;

        Outline outline = btnGO.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderWidth, -borderWidth);

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlaySelectSound();
            onClick?.Invoke();
        });

        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(btnGO.transform, false);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.MiddleCenter;
        contentLayout.spacing = 2f;
        contentLayout.padding = new RectOffset(8, 8, 10, 8);

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        iconGO.transform.SetParent(contentGO.transform, false);
        TextMeshProUGUI iconLabel = iconGO.GetComponent<TextMeshProUGUI>();
        iconLabel.text = iconText;
        iconLabel.fontSize = 28f;
        iconLabel.fontStyle = FontStyles.Bold;
        iconLabel.color = valueColor;
        iconLabel.alignment = TextAlignmentOptions.Center;

        GameObject textGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(contentGO.transform, false);
        TextMeshProUGUI textLabel = textGO.GetComponent<TextMeshProUGUI>();
        textLabel.text = labelText;
        textLabel.fontSize = 18f;
        textLabel.fontStyle = FontStyles.Bold;
        textLabel.color = valueColor;
        textLabel.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private void SetCommandButtonHighlight(Button button, bool active)
    {
        if (button == null)
        {
            return;
        }

        Image img = button.GetComponent<Image>();
        if (img != null)
        {
            img.color = active
                ? Color.Lerp(slotColor, new Color(0.35f, 0.75f, 1f, slotColor.a), 0.6f)
                : slotColor;
        }
    }

    private void CreateMenuButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick, float width = 160f, float height = 60f, Sprite icon = null, BuildingData buildingData = null)
    {
        var btnGO = new GameObject($"Btn_{text}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var le = btnGO.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.minHeight = height;

        // Button Grafik
        var img = btnGO.GetComponent<Image>();
        img.color = slotColor;

        var outline = btnGO.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderWidth, -borderWidth);

        // Button Logik
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        
        // Farben für Hover etc (Pergament-Style)
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); // Heller bei Hover
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f); // Dunkler beim Klicken
        colors.selectedColor    = Color.white;
        btn.colors = colors;

        btn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlaySelectSound();
            onClick?.Invoke();
        });

        // Container für Inhalt (Vertikal zentriert)
        var containerGO = new GameObject("Content", typeof(RectTransform));
        containerGO.transform.SetParent(btnGO.transform, false);
        var cRT = containerGO.GetComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero;
        cRT.anchorMax = Vector2.one;
        cRT.sizeDelta = Vector2.zero;

        var vl = containerGO.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(5, 5, 8, 5);
        vl.spacing = 2f;
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;

        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(containerGO.transform, false);
            var iImg = iconGO.GetComponent<Image>();
            iImg.sprite = icon;
            iImg.preserveAspect = true;
            iImg.raycastTarget = false;
            var iLE = iconGO.AddComponent<LayoutElement>();
            
            // Wenn 4 Kosten da sind, Icon leicht größer machen
            int costCount = 0;
            if (buildingData != null)
            {
                if (buildingData.woodCost > 0) costCount++;
                if (buildingData.stoneCost > 0) costCount++;
                if (buildingData.ironCost > 0) costCount++;
                if (buildingData.goldCost > 0) costCount++;
            }

            iLE.preferredHeight = (costCount >= 4) ? height * 0.6f : height * 0.5f; 
            iLE.flexibleHeight = 0;
        }

        // Button Text
        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(containerGO.transform, false);

        var tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = icon != null ? 14f : 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = valueColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        // Baukosten hinzufügen
        if (buildingData != null)
        {
            int costCount = 0;
            if (buildingData.woodCost > 0) costCount++;
            if (buildingData.stoneCost > 0) costCount++;
            if (buildingData.ironCost > 0) costCount++;
            if (buildingData.goldCost > 0) costCount++;

            var costContainer = new GameObject("Costs", typeof(RectTransform));
            costContainer.transform.SetParent(containerGO.transform, false);
            
            if (costCount >= 4)
            {
                // Bei 4 Items 2x2 Grid verwenden, um Platzmangel zu vermeiden
                var costGL = costContainer.AddComponent<GridLayoutGroup>();
                costGL.cellSize = new Vector2(70, 25);
                costGL.spacing = new Vector2(5, 2);
                costGL.childAlignment = TextAnchor.MiddleCenter;
                costGL.startCorner = GridLayoutGroup.Corner.UpperLeft;
                costGL.startAxis = GridLayoutGroup.Axis.Horizontal;

                var csf = costContainer.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                var costHL = costContainer.AddComponent<HorizontalLayoutGroup>();
                costHL.childAlignment = TextAnchor.MiddleCenter;
                costHL.spacing = 10f;
                costHL.childForceExpandWidth = false;
            }

            if (buildingData.woodCost > 0) AddCostInfo(costContainer.transform, buildingData.woodCost.ToString(), GetIcon("holz"));
            if (buildingData.stoneCost > 0) AddCostInfo(costContainer.transform, buildingData.stoneCost.ToString(), GetIcon("stein"));
            if (buildingData.ironCost > 0) AddCostInfo(costContainer.transform, buildingData.ironCost.ToString(), GetIcon("eisen"));
            if (buildingData.goldCost > 0) AddCostInfo(costContainer.transform, buildingData.goldCost.ToString(), GetIcon("gold"));
        }
    }

    private Sprite GetIcon(string id)
    {
        if (startingResources == null) return null;
        foreach (var res in startingResources)
        {
            if (res.id == id) return res.icon;
        }
        return null;
    }

    private void AddCostInfo(Transform parent, string amount, Sprite icon)
    {
        var costGO = new GameObject("CostItem", typeof(RectTransform));
        costGO.transform.SetParent(parent, false);
        var hl = costGO.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 4f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childForceExpandWidth = false;

        // Icon
        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(costGO.transform, false);
            var img = iconGO.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = iconGO.AddComponent<LayoutElement>();
            le.preferredWidth = 22f;
            le.preferredHeight = 22f;
        }

        // Amount
        var txtGO = new GameObject("Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(costGO.transform, false);
        var tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = amount;
        tmp.fontSize = 16f; // Größer
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = valueColor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void OpenSubMenu(string categoryName)
    {
        // Wir lassen das Main Menu jetzt sichtbar (wie vom User gewünscht)
        // mainMenuContainer.SetActive(false);
        
        // Items für diese Kategorie suchen
        string catId = "";
        foreach(var cat in menuCategories) {
            if(cat.displayName == categoryName) {
                catId = cat.id;
                break;
            }
        }

        // Alten Inhalt löschen
        foreach(Transform child in subMenuContent) {
            Destroy(child.gameObject);
        }

        // Neue Items spawnen
        foreach(var item in menuItems) {
            if(item.categoryId == catId && item.displayName != "Zurück") {
                CreateMenuButton(subMenuContent, item.displayName, () => {
                    Debug.Log("Geklickt auf: " + item.displayName);
                    if (item.buildingData != null && PlacementManager.Instance != null)
                    {
                        PlacementManager.Instance.StartPlacement(item.buildingData);
                        CloseSubMenu(); // Close UI after picking building
                    }
                }, 160f, 160f, item.icon, item.buildingData);
            }
        }

        // Scroll-Position zurücksetzen
        if (subMenuContent is RectTransform rt) rt.anchoredPosition = Vector2.zero;

        subMenuBlocker.SetActive(true);
    }

    private void CloseSubMenu()
    {
        subMenuBlocker.SetActive(false);
        // Da wir es nicht mehr deaktivieren, brauchen wir es hier auch nicht aktivieren
        // mainMenuContainer.SetActive(true);
    }

    private TextMeshProUGUI CreateSlot(Transform parent, ResourceDefinition def)
    {
        // ── Slot-Rahmen (goldene Linie links als Trenner) ─────────────────
        var wrapper = new GameObject($"Wrap_{def.id}", typeof(RectTransform));
        wrapper.transform.SetParent(parent, false);

        var wHL = wrapper.AddComponent<HorizontalLayoutGroup>();
        wHL.padding                = new RectOffset(0, 0, 0, 0);
        wHL.spacing                = 0;
        wHL.childAlignment         = TextAnchor.MiddleLeft;
        wHL.childForceExpandHeight = true;
        wHL.childForceExpandWidth  = false;

        var wCSF = wrapper.AddComponent<ContentSizeFitter>();
        wCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Trennlinie links (außer beim ersten Slot)
        if (parent.childCount > 1)
        {
            var divGO = new GameObject("Div",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divGO.transform.SetParent(wrapper.transform, false);
            divGO.GetComponent<Image>().color = new Color(
                borderColor.r, borderColor.g, borderColor.b, 0.35f);
            var divLE = divGO.AddComponent<LayoutElement>();
            divLE.minWidth  = 1f;
            divLE.minHeight = barHeight * 0.55f;
            divLE.preferredWidth = 1f;
        }

        // ── Slot-Container ─────────────────────────────────────────────────
        var slot = new GameObject($"Slot_{def.id}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slot.transform.SetParent(wrapper.transform, false);
        slot.GetComponent<Image>().color = slotColor;
        slot.AddComponent<ResourceTooltipTrigger>().resourceId = def.id;

        var hl = slot.AddComponent<HorizontalLayoutGroup>();
        hl.padding                = new RectOffset(
            (int)slotPadding, (int)slotPadding, 6, 6);
        hl.spacing                = 10f;
        hl.childAlignment         = TextAnchor.MiddleCenter;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = true;

        var csf = slot.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var le = slot.AddComponent<LayoutElement>();
        le.minHeight = barHeight - 4f;

        // ── Icon ──────────────────────────────────────────────────────────
        var iconGO = new GameObject("Icon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(slot.transform, false);
        var iconImg = iconGO.GetComponent<Image>();

        if (def.icon != null)
        {
            iconImg.sprite = def.icon;
            iconImg.preserveAspect = true;
        }
        else
        {
            // Platzhalter: helles Kästchen mit Anfangsbuchstabe wenn kein Icon
            iconImg.color = new Color(1f, 1f, 1f, 0.08f);
        }
        iconImg.raycastTarget = false;

        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.minWidth      = iconLE.preferredWidth  = iconSize;
        iconLE.minHeight     = iconLE.preferredHeight = iconSize;

        // ── Textspalte ────────────────────────────────────────────────────
        var col = new GameObject("TextCol", typeof(RectTransform));
        col.transform.SetParent(slot.transform, false);
        col.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var vl = col.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment         = TextAnchor.MiddleLeft;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;
        vl.spacing                = 1f;

        // Name (klein, Pergamentton)
        var nameGO = new GameObject("Name",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(col.transform, false);
        var nameTMP = nameGO.GetComponent<TextMeshProUGUI>();
        nameTMP.text          = def.displayName.ToUpper();
        nameTMP.fontSize      = 11f;
        nameTMP.fontStyle     = FontStyles.Bold;
        nameTMP.color         = labelColor;
        nameTMP.alignment     = TextAlignmentOptions.Left;
        nameTMP.raycastTarget = false;

        // Wert (groß, Cremegold)
        var valGO = new GameObject("Value",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        valGO.transform.SetParent(col.transform, false);
        valGO.AddComponent<ResourceTooltipTrigger>().resourceId = def.id;
        var valTMP = valGO.GetComponent<TextMeshProUGUI>();
        
        if (def.maxValue > 0)
            valTMP.text = $"{def.startValue}/{def.maxValue}";
        else
            valTMP.text = def.startValue.ToString();
            
        valTMP.fontSize      = 22f;
        valTMP.fontStyle     = FontStyles.Bold;
        valTMP.color         = valueColor;
        valTMP.alignment     = TextAlignmentOptions.Left;
        valTMP.raycastTarget = true;

        return valTMP;
    }

    // ── Tooltip Logik & Hilfsklassen ─────────────────────────────────────────

    private void Update()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdateTooltipContent();
        }

        rateUpdateTimer -= Time.deltaTime;
        if (rateUpdateTimer <= 0f)
        {
            rateUpdateTimer = 1f; // Jede Sekunde aktualisieren
            UpdateAllRates();
        }
    }

    private void UpdateAllRates()
    {
        List<string> keys = new List<string>(values.Keys);
        foreach (string id in keys)
        {
            bool isTickResource = id != "bevolkerung" && id != "dorfbewohner" && id != "arbeiter" && id != "soldaten" && id != "stimmung";
            if (!isTickResource)
            {
                currentRates[id] = "";
                continue;
            }

            float prod, cons;
            dummyBreakdown.Clear();
            CalculateResourceRates(id, out prod, out cons, dummyBreakdown);
            float net = prod - cons;

            if (Mathf.Abs(net) > 0.01f)
            {
                string netColor = net >= 0 ? "#55FF55" : "#FF5555";
                string prefix = net > 0 ? "+" : "";
                currentRates[id] = $" <size=14><color={netColor}>{prefix}{net:F1}/Min</color></size>";
            }
            else
            {
                currentRates[id] = " <size=14><color=#AAAAAA>0/Min</color></size>";
            }

            // Label aktualisieren
            if (labels.ContainsKey(id))
            {
                SetResource(id, values[id]);
            }
        }
    }

    private void BuildTooltipPanel(Transform canvasTransform)
    {
        tooltipPanel = new GameObject("ResourceTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipPanel.transform.SetParent(canvasTransform, false);

        var img = tooltipPanel.GetComponent<Image>();
        img.color = new Color(barColor.r, barColor.g, barColor.b, 0.98f);
        img.raycastTarget = false;

        var outline = tooltipPanel.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderWidth, -borderWidth);

        var shadow = tooltipPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(4f, -4f);

        var rt = tooltipPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(320f, 180f);

        var vl = tooltipPanel.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14, 14, 14, 14);
        vl.spacing = 8f;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;

        var csf = tooltipPanel.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(tooltipPanel.transform, false);
        tooltipTitleText = titleGO.GetComponent<TextMeshProUGUI>();
        tooltipTitleText.fontSize = 18f;
        tooltipTitleText.fontStyle = FontStyles.Bold;
        tooltipTitleText.color = borderColor;
        tooltipTitleText.alignment = TextAlignmentOptions.Left;
        tooltipTitleText.raycastTarget = false;

        var divGO = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divGO.transform.SetParent(tooltipPanel.transform, false);
        var divImg = divGO.GetComponent<Image>();
        divImg.color = new Color(borderColor.r, borderColor.g, borderColor.b, 0.4f);
        divImg.raycastTarget = false;
        var divLE = divGO.AddComponent<LayoutElement>();
        divLE.minHeight = 1f;
        divLE.preferredHeight = 1f;

        var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyGO.transform.SetParent(tooltipPanel.transform, false);
        tooltipBodyText = bodyGO.GetComponent<TextMeshProUGUI>();
        tooltipBodyText.fontSize = 14f;
        tooltipBodyText.color = valueColor;
        tooltipBodyText.alignment = TextAlignmentOptions.Left;
        tooltipBodyText.textWrappingMode = TextWrappingModes.Normal;
        tooltipBodyText.raycastTarget = false;

        tooltipPanel.SetActive(false);
    }

    public void ShowTooltip(string resourceId, Vector2 screenPosition)
    {
        activeTooltipResourceId = resourceId;
        activeTooltipScreenPosition = screenPosition;
        UpdateTooltipContent();

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(true);
            UpdateTooltipPosition(screenPosition);
        }
    }

    public void UpdateTooltipHoverPosition(Vector2 screenPosition)
    {
        activeTooltipScreenPosition = screenPosition;
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdateTooltipPosition(screenPosition);
        }
    }

    public void HideTooltip()
    {
        activeTooltipResourceId = "";
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private void UpdateTooltipPosition(Vector2 screenPosition)
    {
        if (tooltipPanel == null)
        {
            return;
        }

        var rt = tooltipPanel.GetComponent<RectTransform>();
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tooltipPanel.transform.parent as RectTransform,
            screenPosition,
            null,
            out localPoint
        );

        float tooltipWidth = rt.rect.width > 0f ? rt.rect.width : 320f;
        float tooltipHeight = rt.rect.height > 0f ? rt.rect.height : 180f;
        float x = localPoint.x + (tooltipWidth * 0.5f) + 16f;
        float y = localPoint.y - 16f;

        RectTransform parentRect = tooltipPanel.transform.parent as RectTransform;
        if (parentRect != null)
        {
            float halfWidth = parentRect.rect.width * 0.5f;
            float halfHeight = parentRect.rect.height * 0.5f;
            x = Mathf.Clamp(x, -halfWidth + tooltipWidth * 0.5f + 8f, halfWidth - tooltipWidth * 0.5f - 8f);
            y = Mathf.Clamp(y, -halfHeight + tooltipHeight + 8f, halfHeight - 8f);
        }

        rt.anchoredPosition = new Vector2(x, y);
    }

    private void UpdateTooltipContent()
    {
        if (string.IsNullOrEmpty(activeTooltipResourceId)) return;

        string displayName = GetResourceDisplayName(activeTooltipResourceId);
        tooltipTitleText.text = displayName.ToUpper();

        float production = 0f;
        float consumption = 0f;
        List<string> breakdown = new List<string>();

        CalculateResourceRates(activeTooltipResourceId, out production, out consumption, breakdown);

        string body = "";
        bool isTickResource = activeTooltipResourceId != "bevolkerung" && 
                              activeTooltipResourceId != "dorfbewohner" && 
                              activeTooltipResourceId != "arbeiter" && 
                              activeTooltipResourceId != "soldaten" && 
                              activeTooltipResourceId != "stimmung";

        if (isTickResource)
        {
            float net = production - consumption;
            string netColor = net >= 0 ? "#55FF55" : "#FF5555";
            string netPrefix = net > 0 ? "+" : "";
            body += $"<b>Änderung:</b> <color={netColor}>{netPrefix}{net:F1}/Min</color>\n";
            body += $"<b>Plus:</b> <color=#55FF55>+{production:F1}/Min</color>   ";
            body += $"<b>Minus:</b> <color=#FF5555>-{consumption:F1}/Min</color>\n\n";
        }

        if (breakdown.Count > 0)
        {
            body += string.Join("\n", breakdown);
        }

        string detailedBreakdown = "";
        if (isTickResource)
        {
            detailedBreakdown = BuildDetailedRateBreakdown(activeTooltipResourceId);
            if (!string.IsNullOrEmpty(detailedBreakdown))
            {
                if (!string.IsNullOrEmpty(body) && !body.EndsWith("\n\n"))
                {
                    body += "\n\n";
                }
                body += detailedBreakdown;
            }
        }

        if (isTickResource && breakdown.Count == 0 && string.IsNullOrEmpty(detailedBreakdown))
        {
            body += "Keine aktiven Quellen oder Verbraucher.";
        }

        tooltipBodyText.text = body;
    }

    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;

        var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemGO.transform.SetParent(transform);
    }

    private void CalculateResourceRates(string id, out float production, out float consumption, List<string> breakdown)
    {
        production = 0f;
        consumption = 0f;

        int pop = 1;
        int activeVillagerCount = 0;
        if (VillagerManager.Instance != null && VillagerManager.Instance.ActiveVillagers != null)
        {
            activeVillagerCount = VillagerManager.Instance.ActiveVillagers.Count;
        }
        pop = Mathf.Max(1, activeVillagerCount);

        if (id == "bevolkerung")
        {
            int current = GetResource("bevolkerung");
            int max = GetMaxResource("bevolkerung");
            breakdown.Add($"Aktuelle Gesamtbevölkerung: {current}");
            breakdown.Add($"Maximales Limit: {max}");
            breakdown.Add("");
            breakdown.Add("Erhöhe dein Limit durch den Bau von Wohnhäusern.");
            return;
        }

        if (id == "dorfbewohner")
        {
            int current = GetResource("dorfbewohner");
            breakdown.Add($"Freie, nicht in Betrieben arbeitende Bürger: {current}");
            breakdown.Add("");
            breakdown.Add("Freie Arbeiter errichten Gebäude und können zu Soldaten rekrutiert werden.");
            return;
        }

        if (id == "arbeiter")
        {
            int current = GetResource("arbeiter");
            breakdown.Add($"In Produktionsbetrieben angestellte Bürger: {current}");
            breakdown.Add("");
            breakdown.Add("Arbeiter verrichten ihren Dienst in Farmen, Minen und Werkstätten.");
            return;
        }

        if (id == "soldaten")
        {
            int current = GetResource("soldaten");
            int max = GetMaxResource("soldaten");
            breakdown.Add($"Ausgebildete Soldaten: {current} / {max}");
            breakdown.Add("");
            breakdown.Add("Kaserne erhöht das Limit und bildet Soldaten aus freien Bürgern aus.");
            return;
        }

        if (id == "stimmung")
        {
            float globalMood = 100f;
            if (VillagerManager.Instance != null) globalMood = VillagerManager.Instance.globalMood;
            
            float foodEffect = 0f;
            if (VillagerManager.Instance != null) foodEffect = VillagerManager.Instance.GetFoodMoodEffect();

            breakdown.Add($"Globale Zufriedenheit: {globalMood:F0}%");
            breakdown.Add("");
            breakdown.Add($"Nahrungs-Effekt: {(foodEffect >= 0 ? "+" : "")}{foodEffect * 100f:F0}%");
            breakdown.Add("");
            breakdown.Add("Höhere Zufriedenheit steigert das Arbeitstempo und die Erträge in Betrieben.");
            return;
        }

        var buildings = FindObjectsByType<BuildingInstance>();
        float globalMoodForYield = 100f;
        if (VillagerManager.Instance != null) globalMoodForYield = VillagerManager.Instance.globalMood;
        float yieldModifier = Mathf.Lerp(0.3f, 1.0f, globalMoodForYield / 100f);

        Dictionary<string, float> prodSources = new Dictionary<string, float>();
        Dictionary<string, float> consSources = new Dictionary<string, float>();

        foreach (var b in buildings)
        {
            if (b == null || !b.isLocal || !b.IsConstructed()) continue;

            if (b.GetOperatingWorkerCount() < b.data.workersNeeded) continue;

            float scheduleMultiplier = 1.0f;
            switch (b.currentSchedule)
            {
                case BuildingInstance.ScheduleMode.DayOnly: scheduleMultiplier = 0.5f; break;
                case BuildingInstance.ScheduleMode.Leisure: scheduleMultiplier = 0.375f; break;
                case BuildingInstance.ScheduleMode.Continuous: scheduleMultiplier = 1.0f; break;
            }

            if (!string.IsNullOrEmpty(b.data.productionResourceId) && b.data.productionResourceId == id && !b.data.isBarracks)
            {
                if (!b.IsProductionPaused)
                {
                    int baseAmount = b.data.productionAmount;
                    int actualProduction = Mathf.Max(1, Mathf.RoundToInt(baseAmount * yieldModifier));
                    float interval = b.data.productionInterval > 0f ? b.data.productionInterval : 60f;
                    float rate = ((actualProduction * 60f) / interval) * scheduleMultiplier;

                    string bName = b.data.buildingName;
                    if (prodSources.ContainsKey(bName)) prodSources[bName] += rate;
                    else prodSources[bName] = rate;

                    production += rate;
                }
            }

            if (!string.IsNullOrEmpty(b.data.consumedResourceId) && b.data.consumedResourceId == id)
            {
                if (!b.IsProductionPaused)
                {
                    float interval = b.data.productionInterval > 0f ? b.data.productionInterval : 60f;
                    float rate = ((b.data.consumedAmount * 60f) / interval) * scheduleMultiplier;

                    string bName = b.data.buildingName;
                    if (consSources.ContainsKey(bName)) consSources[bName] += rate;
                    else consSources[bName] = rate;

                    consumption += rate;
                }
            }
        }

        if (id == "weizen")
        {
            float rate = pop * 0.2f;
            consumption += rate;
            consSources["Bürger (Grundnahrung)"] = rate;
        }
        else if (id == "fruechte")
        {
            int fruits = GetResource("fruechte");
            float fruitsRatio = (float)fruits / pop;
            float fruitsConsMod = fruits > 0 ? Mathf.Clamp(fruitsRatio, 0.1f, 1.5f) : 0f;
            float rate = pop * 0.15f * fruitsConsMod;
            if (rate > 0)
            {
                consumption += rate;
                consSources["Bürger (Luxusbedarf)"] = rate;
            }
        }
        else if (id == "fleisch")
        {
            int meat = GetResource("fleisch");
            float meatRatio = (float)meat / pop;
            float meatConsMod = meat > 0 ? Mathf.Clamp(meatRatio, 0.1f, 1.5f) : 0f;
            float rate = pop * 0.15f * meatConsMod;
            if (rate > 0)
            {
                consumption += rate;
                consSources["Bürger (Luxusbedarf)"] = rate;
            }
        }

        if (prodSources.Count > 0)
        {
            breakdown.Add("<b>Einnahmen / Produktion:</b>");
            foreach (var kvp in prodSources)
            {
                breakdown.Add($"<color=#55FF55>+{kvp.Value:F1}/Min</color> durch {kvp.Key}");
            }
        }

        if (consSources.Count > 0)
        {
            if (breakdown.Count > 0) breakdown.Add("");
            breakdown.Add("<b>Ausgaben / Verbrauch:</b>");
            foreach (var kvp in consSources)
            {
                breakdown.Add($"<color=#FF5555>-{kvp.Value:F1}/Min</color> durch {kvp.Key}");
            }
        }
    }

    private string BuildDetailedRateBreakdown(string resourceId)
    {
        List<string> lines = new List<string>();
        Dictionary<string, RateSourceEntry> productionSources = new Dictionary<string, RateSourceEntry>();
        Dictionary<string, RateSourceEntry> consumptionSources = new Dictionary<string, RateSourceEntry>();

        var buildings = FindObjectsByType<BuildingInstance>();
        float globalMoodForYield = VillagerManager.Instance != null ? VillagerManager.Instance.globalMood : 100f;
        float yieldModifier = Mathf.Lerp(0.3f, 1.0f, globalMoodForYield / 100f);

        foreach (var building in buildings)
        {
            if (building == null || !building.isLocal || !building.IsConstructed() || building.IsProductionPaused)
            {
                continue;
            }

            if (building.GetOperatingWorkerCount() < building.data.workersNeeded)
            {
                continue;
            }

            float scheduleMultiplier = 1f;
            switch (building.currentSchedule)
            {
                case BuildingInstance.ScheduleMode.DayOnly:
                    scheduleMultiplier = 0.5f;
                    break;
                case BuildingInstance.ScheduleMode.Leisure:
                    scheduleMultiplier = 0.375f;
                    break;
                case BuildingInstance.ScheduleMode.Continuous:
                    scheduleMultiplier = 1f;
                    break;
            }

            if (!string.IsNullOrEmpty(building.data.productionResourceId) && building.data.productionResourceId == resourceId && !building.data.isBarracks)
            {
                int actualProduction = Mathf.Max(1, Mathf.RoundToInt(building.data.productionAmount * yieldModifier));
                float interval = building.data.productionInterval > 0f ? building.data.productionInterval : 60f;
                float rate = ((actualProduction * 60f) / interval) * scheduleMultiplier;
                AddOrUpdateDetailedSource(productionSources, building.data.buildingName, rate);
            }

            if (!string.IsNullOrEmpty(building.data.consumedResourceId) && building.data.consumedResourceId == resourceId)
            {
                float interval = building.data.productionInterval > 0f ? building.data.productionInterval : 60f;
                float rate = ((building.data.consumedAmount * 60f) / interval) * scheduleMultiplier;
                AddOrUpdateDetailedSource(consumptionSources, building.data.buildingName, rate);
            }
        }

        int villagerCount = 0;
        if (VillagerManager.Instance != null && VillagerManager.Instance.ActiveVillagers != null)
        {
            villagerCount = VillagerManager.Instance.ActiveVillagers.Count;
        }

        if (resourceId == "weizen" && villagerCount > 0)
        {
            AddDetailedFixedSource(consumptionSources, "Bürger", villagerCount, 0.2f);
        }
        else if (resourceId == "fruechte" && villagerCount > 0)
        {
            int fruits = GetResource("fruechte");
            float fruitsRatio = (float)fruits / Mathf.Max(1, villagerCount);
            float perVillagerRate = fruits > 0 ? 0.15f * Mathf.Clamp(fruitsRatio, 0.1f, 1.5f) : 0f;
            if (perVillagerRate > 0f)
            {
                AddDetailedFixedSource(consumptionSources, "Bürger", villagerCount, perVillagerRate);
            }
        }
        else if (resourceId == "fleisch" && villagerCount > 0)
        {
            int meat = GetResource("fleisch");
            float meatRatio = (float)meat / Mathf.Max(1, villagerCount);
            float perVillagerRate = meat > 0 ? 0.15f * Mathf.Clamp(meatRatio, 0.1f, 1.5f) : 0f;
            if (perVillagerRate > 0f)
            {
                AddDetailedFixedSource(consumptionSources, "Bürger", villagerCount, perVillagerRate);
            }
        }

        if (productionSources.Count > 0)
        {
            lines.Add("<b>Details:</b>");
            foreach (var entry in GetSortedDetailedSources(productionSources))
            {
                lines.Add($"<color=#55FF55>+{entry.TotalRate:F1}/Min</color> durch {FormatDetailedSource(entry)}");
            }
        }

        if (consumptionSources.Count > 0)
        {
            if (lines.Count == 0)
            {
                lines.Add("<b>Details:</b>");
            }
            foreach (var entry in GetSortedDetailedSources(consumptionSources))
            {
                lines.Add($"<color=#FF5555>-{entry.TotalRate:F1}/Min</color> durch {FormatDetailedSource(entry)}");
            }
        }

        return string.Join("\n", lines);
    }

    private void AddOrUpdateDetailedSource(Dictionary<string, RateSourceEntry> sources, string label, float rate)
    {
        if (sources.TryGetValue(label, out var entry))
        {
            entry.Count++;
            entry.TotalRate += rate;
            entry.RatePerUnit = entry.TotalRate / Mathf.Max(1, entry.Count);
            return;
        }

        sources[label] = new RateSourceEntry
        {
            Label = label,
            Count = 1,
            RatePerUnit = rate,
            TotalRate = rate
        };
    }

    private void AddDetailedFixedSource(Dictionary<string, RateSourceEntry> sources, string label, int count, float ratePerUnit)
    {
        sources[label] = new RateSourceEntry
        {
            Label = label,
            Count = count,
            RatePerUnit = ratePerUnit,
            TotalRate = count * ratePerUnit
        };
    }

    private List<RateSourceEntry> GetSortedDetailedSources(Dictionary<string, RateSourceEntry> sources)
    {
        List<RateSourceEntry> entries = new List<RateSourceEntry>(sources.Values);
        entries.Sort((a, b) => b.TotalRate.CompareTo(a.TotalRate));
        return entries;
    }

    private string FormatDetailedSource(RateSourceEntry entry)
    {
        if (entry.Count <= 1)
        {
            return $"{entry.Label} ({entry.RatePerUnit:F2}/Min)";
        }

        return $"{entry.Count}x {entry.Label} (je {entry.RatePerUnit:F2}/Min)";
    }

    private void AddRateSource(Dictionary<string, RateSourceEntry> sources, string label, float rate)
    {
        AddOrUpdateDetailedSource(sources, label, rate);
    }

    private List<RateSourceEntry> GetSortedRateSources(Dictionary<string, RateSourceEntry> sources)
    {
        return GetSortedDetailedSources(sources);
    }

    private string FormatRateSource(RateSourceEntry entry)
    {
        return FormatDetailedSource(entry);
    }

    private string GetResourceDisplayName(string id)
    {
        foreach (var res in startingResources)
        {
            if (res.id == id) return res.displayName;
        }
        switch(id)
        {
            case "bevolkerung": return "Bevölkerung";
            case "dorfbewohner": return "Freie Arbeiter";
            case "arbeiter": return "Arbeiter";
            case "stimmung": return "Zufriedenheit";
            case "soldaten": return "Soldaten";
            case "weizen": return "Weizen";
            case "fruechte": return "Früchte";
            case "fleisch": return "Fleisch";
            case "geld": return "Geld";
            default: return id;
        }
    }
}

public class ResourceTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public string resourceId;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Player_UI.Instance != null)
        {
            Player_UI.Instance.ShowTooltip(resourceId, eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Player_UI.Instance != null)
        {
            Player_UI.Instance.HideTooltip();
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (Player_UI.Instance != null)
        {
            Player_UI.Instance.UpdateTooltipHoverPosition(eventData.position);
        }
    }
}
