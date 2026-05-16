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
        new ResourceDefinition { id = "eisen",       displayName = "Eisen",       startValue = 100 },
        new ResourceDefinition { id = "gold",        displayName = "Gold",        startValue = 100 },
        new ResourceDefinition { id = "weizen",      displayName = "Weizen",      startValue = 100 },
        new ResourceDefinition { id = "holz",        displayName = "Holz",        startValue = 100 },
        new ResourceDefinition { id = "stein",       displayName = "Stein",       startValue = 100 },
        new ResourceDefinition { id = "wüstenfrucht", displayName = "Wüstenfrucht", startValue = 0   },
        new ResourceDefinition { id = "bevolkerung", displayName = "Bevölkerung", startValue = 10, maxValue = 20 },
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
    private GameObject subMenuContainer;
    private TextMeshProUGUI subMenuTitle;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake() => Instance = this;

    private void Start() => BuildUI();

    // ── Öffentliche API ──────────────────────────────────────────────────────

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
            if (max > 0)
                lbl.text = $"{values[id]}/{max}";
            else
                lbl.text = values[id].ToString();
        }
    }

    public void AddResource(string id, int delta) => SetResource(id, GetResource(id) + delta);

    public int GetResource(string id) => values.TryGetValue(id, out int v) ? v : 0;

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
            values[def.id] = def.startValue;
            maxValues[def.id] = def.maxValue;
            labels[def.id] = CreateSlot(barGO.transform, def);
        }

        BuildBottomMenu(canvasGO.transform);
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

        // ── Sub Menu Container (Versteckt am Anfang) ────────────────────────
        subMenuContainer = new GameObject("SubMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        subMenuContainer.transform.SetParent(bottomGO.transform, false);
        
        var subImg = subMenuContainer.GetComponent<Image>();
        subImg.color = new Color(slotColor.r, slotColor.g, slotColor.b, 0.98f); // Hintergrund fürs Submenü

        var subRT = subMenuContainer.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 0f);
        subRT.anchorMax = new Vector2(0.5f, 0f);
        subRT.pivot     = new Vector2(0.5f, 0f);
        subRT.sizeDelta = new Vector2(800f, 250f); // Größeres Fenster für den Inhalt

        // Rahmen fürs Submenü (optional, nutzen Outline)
        var subOutline = subMenuContainer.AddComponent<Outline>();
        subOutline.effectColor = borderColor;
        subOutline.effectDistance = new Vector2(borderWidth, -borderWidth);

        // Layout fürs Submenü
        var subVL = subMenuContainer.AddComponent<VerticalLayoutGroup>();
        subVL.padding = new RectOffset(20, 20, 20, 20);
        subVL.spacing = 10f;
        subVL.childAlignment = TextAnchor.UpperCenter;

        // Titel und Zurück-Button Zeile
        var subHeaderGO = new GameObject("Header", typeof(RectTransform));
        subHeaderGO.transform.SetParent(subMenuContainer.transform, false);
        var headerHL = subHeaderGO.AddComponent<HorizontalLayoutGroup>();
        headerHL.childForceExpandHeight = false;
        headerHL.childForceExpandWidth  = true;
        headerHL.childAlignment = TextAnchor.MiddleLeft;

        var headerLE = subHeaderGO.AddComponent<LayoutElement>();
        headerLE.minHeight = 40f;

        CreateMenuButton(subHeaderGO.transform, "< Zurück", CloseSubMenu, 120f, 40f);

        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(subHeaderGO.transform, false);
        subMenuTitle = titleGO.GetComponent<TextMeshProUGUI>();
        subMenuTitle.text = "Kategorie";
        subMenuTitle.fontSize = 24f;
        subMenuTitle.fontStyle = FontStyles.Bold;
        subMenuTitle.color = valueColor;
        subMenuTitle.alignment = TextAlignmentOptions.Center;
        
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1f;

        // Platzhalter für den Inhalt
        var contentGO = new GameObject("ContentPlaceholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        contentGO.transform.SetParent(subMenuContainer.transform, false);
        var contentTMP = contentGO.GetComponent<TextMeshProUGUI>();
        contentTMP.text = "(Hier kommen später die auswählbaren Elemente hin)";
        contentTMP.fontSize = 18f;
        contentTMP.color = labelColor;
        contentTMP.alignment = TextAlignmentOptions.Center;
        contentTMP.enableWordWrapping = true;
        var contentLE = contentGO.AddComponent<LayoutElement>();
        contentLE.flexibleHeight = 1f;

        // Startzustand
        subMenuContainer.SetActive(false);
    }

    private void CreateMenuButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick, float width = 160f, float height = 60f)
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

        // Button Text
        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);

        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        var tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = valueColor;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private void OpenSubMenu(string categoryName)
    {
        mainMenuContainer.SetActive(false);
        subMenuTitle.text = categoryName.ToUpper();
        subMenuContainer.SetActive(true);
    }

    private void CloseSubMenu()
    {
        subMenuContainer.SetActive(false);
        mainMenuContainer.SetActive(true);
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
