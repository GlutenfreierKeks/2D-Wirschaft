using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private GameObject mainMenuContainer;
    private GameObject subMenuBlocker;
    private GameObject subMenuContainer;
    private Transform subMenuContent;

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

    private void Start() => BuildUI();

    // ── Öffentliche API ──────────────────────────────────────────────────────
    
    public bool IsSubMenuOpen => subMenuBlocker != null && subMenuBlocker.activeSelf;

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
            if (id == "stimmung")
                lbl.text = $"{values[id]}%";
            else if (max > 0)
                lbl.text = $"{values[id]}/{max}";
            else
                lbl.text = values[id].ToString();
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

        btn.onClick.AddListener(onClick);

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
        tmp.enableWordWrapping = true;

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
        tmp.enableWordWrapping = false;
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
        var valTMP = valGO.GetComponent<TextMeshProUGUI>();
        
        if (def.maxValue > 0)
            valTMP.text = $"{def.startValue}/{def.maxValue}";
        else
            valTMP.text = def.startValue.ToString();
            
        valTMP.fontSize      = 22f;
        valTMP.fontStyle     = FontStyles.Bold;
        valTMP.color         = valueColor;
        valTMP.alignment     = TextAlignmentOptions.Left;
        valTMP.raycastTarget = false;

        return valTMP;
    }
}
