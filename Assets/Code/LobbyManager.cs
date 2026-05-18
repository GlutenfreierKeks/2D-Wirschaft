using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomInfoText;
    [SerializeField] private TextMeshProUGUI playerListText;
    [SerializeField] private TextMeshProUGUI masterClientNoteText;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button startTestButton;
    [SerializeField] private JoinLogUI joinLogUI;

    [Header("Speed Settings")]
    [SerializeField] private Sprite pfeil1;
    [SerializeField] private Sprite pfeil2;
    [SerializeField] private Sprite pfeil3;

    private readonly Color bgColor = new Color(0.06f, 0.08f, 0.10f, 1f);
    private readonly Color panelColor = new Color(0.13f, 0.09f, 0.05f, 0.95f);
    private readonly Color accentColor = new Color(0.86f, 0.67f, 0.26f, 1f);
    private readonly Color labelColor = new Color(0.95f, 0.92f, 0.82f, 1f);

    private readonly int[] speedOptions = { 1, 2, 3 };
    private readonly float[] dayLengthOptions = { 60f, 90f, 150f };
    private readonly string[] dayLengthLabels = { "Kurz", "Normal", "Lang" };
    private readonly int[] villagerOptions = { 6, 10, 14 };
    private readonly int[] workerOptions = { 1, 2, 4 };
    private readonly string[] worldSizeLabels = { "Kompakt", "Standard", "Gross" };

    private Button lobbyStartButton;
    private TextMeshProUGUI startBtnText;
    private RectTransform settingsRoot;
    private TextMeshProUGUI settingsSummaryText;

    private Button[] speedButtons;
    private Button[] dayLengthButtons;
    private Button[] villagerButtons;
    private Button[] workerButtons;
    private Button[] worldSizeButtons;

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.RemoveAllListeners();
            leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
        }

        if (startTestButton != null)
        {
            startTestButton.onClick.RemoveAllListeners();
            startTestButton.onClick.AddListener(OnStartTestButtonClicked);
            startTestButton.gameObject.SetActive(false);
        }

        EnsureDefaultLobbySettings();
        StyleExistingUi();
        BuildLobbyLayout();
        UpdateLobbyUI();
        CheckAutoStart();
    }

    private void EnsureDefaultLobbySettings()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        var currentProps = PhotonNetwork.CurrentRoom.CustomProperties;

        if (!currentProps.ContainsKey(LobbySettingsKeys.GameSpeed)) props[LobbySettingsKeys.GameSpeed] = 1;
        if (!currentProps.ContainsKey(LobbySettingsKeys.DayLength)) props[LobbySettingsKeys.DayLength] = 90f;
        if (!currentProps.ContainsKey(LobbySettingsKeys.StartVillagers)) props[LobbySettingsKeys.StartVillagers] = 10;
        if (!currentProps.ContainsKey(LobbySettingsKeys.StartWorkers)) props[LobbySettingsKeys.StartWorkers] = 2;
        if (!currentProps.ContainsKey(LobbySettingsKeys.WorldSize)) props[LobbySettingsKeys.WorldSize] = "Standard";
        if (!currentProps.ContainsKey(LobbySettingsKeys.MapSeed)) props[LobbySettingsKeys.MapSeed] = Random.Range(1, 1000000);

        if (props.Count > 0)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    private void UpdateLobbyUI()
    {
        if (!PhotonNetwork.InRoom) return;

        Room currentRoom = PhotonNetwork.CurrentRoom;
        bool isTestMode = currentRoom.CustomProperties.ContainsKey("TestMode") && (bool)currentRoom.CustomProperties["TestMode"];

        roomInfoText.text = $"LOBBY {currentRoom.Name}\n{currentRoom.PlayerCount}/{currentRoom.MaxPlayers} Spieler bereit";

        playerListText.text = "";
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string role = player.IsMasterClient ? "Host" : "Mitspieler";
            string self = player.IsLocal ? "  •  Du" : "";
            playerListText.text += $"• {player.NickName}  ({role}){self}\n";
        }

        if (isTestMode)
        {
            playerListText.text += "• Dummy Player 1  (Bot)\n";
            playerListText.text += "• Dummy Player 2  (Bot)\n";
            if (startTestButton != null && PhotonNetwork.IsMasterClient)
            {
                startTestButton.gameObject.SetActive(true);
            }
        }

        if (masterClientNoteText != null)
        {
            masterClientNoteText.text = PhotonNetwork.IsMasterClient
                ? "Du bist Host. Stelle die Spielregeln ein und starte, wenn die Runde passt."
                : "Der Host richtet gerade die Runde ein. Deine Ansicht aktualisiert sich automatisch.";
        }

        UpdateSettingsSummary();
        UpdateSettingsButtons();
        UpdateSpeedUI();
    }

    private void UpdateSettingsSummary()
    {
        if (settingsSummaryText == null || !PhotonNetwork.InRoom)
        {
            return;
        }

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        int speed = props.TryGetValue(LobbySettingsKeys.GameSpeed, out object speedObj) ? System.Convert.ToInt32(speedObj) : 1;
        float dayLength = props.TryGetValue(LobbySettingsKeys.DayLength, out object dayObj) ? System.Convert.ToSingle(dayObj) : 90f;
        int villagers = props.TryGetValue(LobbySettingsKeys.StartVillagers, out object villObj) ? System.Convert.ToInt32(villObj) : 10;
        int workers = props.TryGetValue(LobbySettingsKeys.StartWorkers, out object workObj) ? System.Convert.ToInt32(workObj) : 2;
        string worldSize = props.TryGetValue(LobbySettingsKeys.WorldSize, out object worldObj) ? worldObj.ToString() : "Standard";

        settingsSummaryText.text = $"Tempo {speed}x   •   Tag {GetDayLengthLabel(dayLength)}   •   Start {villagers} Dorfbewohner / {workers} Bauarbeiter   •   Welt {worldSize}";
    }

    private void OnStartTestButtonClicked()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.LoadLevel(SceneNames.GameScene);
        }
    }

    public void OnLeaveRoomButtonClicked()
    {
        if (leaveRoomButton != null) leaveRoomButton.interactable = false;
        PhotonNetwork.LeaveRoom();
    }

    private void CheckAutoStart()
    {
        if (!PhotonNetwork.InRoom) return;

        bool isTestMode = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("TestMode") && (bool)PhotonNetwork.CurrentRoom.CustomProperties["TestMode"];
        if (!isTestMode && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.LoadLevel(SceneNames.GameScene);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        joinLogUI?.LogPlayerJoin(newPlayer.NickName);
        UpdateLobbyUI();
        CheckAutoStart();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateLobbyUI();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdateLobbyUI();
        CheckAutoStart();
    }

    public override void OnLeftRoom()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.StartMenuScene);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(LobbySettingsKeys.GameSpeed) ||
            propertiesThatChanged.ContainsKey(LobbySettingsKeys.DayLength) ||
            propertiesThatChanged.ContainsKey(LobbySettingsKeys.StartVillagers) ||
            propertiesThatChanged.ContainsKey(LobbySettingsKeys.StartWorkers) ||
            propertiesThatChanged.ContainsKey(LobbySettingsKeys.WorldSize))
        {
            UpdateLobbyUI();
        }
    }

    private void BuildLobbyLayout()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Fix: Make sure canvas is always overlay so camera FOV doesn't clip/hide it
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        RemoveExistingRuntimeLayout(canvas.transform, "LobbyRuntimeLayout", "LobbyBackdrop");
        CreateBackdrop(canvas.transform);

        RectTransform root = CreateRect("LobbyRuntimeLayout", canvas.transform);
        Stretch(root);

        RectTransform leftCard = CreateCard(root, "LobbyLeftCard", new Vector2(0.05f, 0.10f), new Vector2(0.40f, 0.90f));
        RectTransform rightCard = CreateCard(root, "LobbyRightCard", new Vector2(0.43f, 0.10f), new Vector2(0.95f, 0.90f));

        CreateReadoutProxy(leftCard, "LobbyReadout", roomInfoText, new Vector2(18f, -18f), new Vector2(-18f, 62f), 28f, FontStyles.Bold, labelColor);
        CreateReadoutProxy(leftCard, "PlayersReadout", playerListText, new Vector2(18f, -94f), new Vector2(-18f, 300f), 20f, FontStyles.Normal, new Color(0.89f, 0.90f, 0.86f, 1f));
        CreateReadoutProxy(leftCard, "HostNoteReadout", masterClientNoteText, new Vector2(18f, -404f), new Vector2(-18f, 92f), 16f, FontStyles.Italic, new Color(0.86f, 0.80f, 0.63f, 1f));

        if (leaveRoomButton != null)
        {
            CreateButtonProxy(leftCard, "LeaveProxy", leaveRoomButton, "Lobby verlassen", new Color(0.40f, 0.17f, 0.12f, 1f), 16f, 56f);
        }

        if (joinLogUI != null && joinLogUI.transform is RectTransform joinLogRt)
        {
            RectTransform logPanel = CreateRect("JoinLogPanel", leftCard);
            logPanel.anchorMin = new Vector2(0f, 0f);
            logPanel.anchorMax = new Vector2(1f, 0f);
            logPanel.pivot = new Vector2(0.5f, 0f);
            logPanel.anchoredPosition = new Vector2(0f, 84f);
            logPanel.sizeDelta = new Vector2(-32f, 92f);
            Image logBg = logPanel.gameObject.AddComponent<Image>();
            logBg.color = new Color(0.09f, 0.11f, 0.12f, 0.70f);
            Outline logOutline = logPanel.gameObject.AddComponent<Outline>();
            logOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
            logOutline.effectDistance = new Vector2(1f, -1f);
        }

        if (startTestButton != null)
        {
            CreateButtonProxy(leftCard, "TestProxy", startTestButton, "Testspiel starten", new Color(0.18f, 0.24f, 0.34f, 1f), 82f, 50f);
        }

        CreateSettingsPanel(rightCard);
    }

    private void CreateSettingsPanel(RectTransform parent)
    {
        CreateStaticLabel(parent, "SPIEL SETTINGS", 24f, new Vector2(20f, -20f), new Vector2(-20f, 34f));

        settingsSummaryText = CreateStaticLabel(parent, "", 15f, new Vector2(20f, -60f), new Vector2(-20f, 44f));
        settingsSummaryText.color = new Color(0.86f, 0.88f, 0.90f, 1f);

        settingsRoot = CreateRect("SettingsRoot", parent);
        settingsRoot.anchorMin = new Vector2(0f, 0f);
        settingsRoot.anchorMax = new Vector2(1f, 1f);
        settingsRoot.offsetMin = new Vector2(18f, 100f);
        settingsRoot.offsetMax = new Vector2(-18f, -78f);

        speedButtons = CreateOptionRow(settingsRoot, 0, "Spieltempo", new[] { "1x", "2x", "3x" }, index => SetGameSpeed(speedOptions[index]));
        dayLengthButtons = CreateOptionRow(settingsRoot, 1, "Taglaenge", dayLengthLabels, index => SetDayLength(dayLengthOptions[index]));
        villagerButtons = CreateOptionRow(settingsRoot, 2, "Start Dorfbewohner", new[] { "6", "10", "14" }, index => SetStartVillagers(villagerOptions[index]));
        workerButtons = CreateOptionRow(settingsRoot, 3, "Start Bauarbeiter", new[] { "1", "2", "4" }, index => SetStartWorkers(workerOptions[index]));
        worldSizeButtons = CreateOptionRow(settingsRoot, 4, "Weltgroesse", worldSizeLabels, index => SetWorldSize(worldSizeLabels[index]));

        CreateStartButton(parent);
    }

    private Button[] CreateOptionRow(RectTransform parent, int rowIndex, string label, string[] options, System.Action<int> onChoose)
    {
        float top = -rowIndex * 84f;
        CreateStaticLabel(parent, label.ToUpper(), 16f, new Vector2(6f, top), new Vector2(-6f, 22f));

        RectTransform row = CreateRect(label + "_Row", parent);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, top - 24f);
        row.sizeDelta = new Vector2(0f, 48f);

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        Button[] buttons = new Button[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int localIndex = i;
            buttons[i] = CreateOptionButton(row, options[i], () => onChoose(localIndex));
        }

        return buttons;
    }

    private Button CreateOptionButton(RectTransform parent, string text, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject(text + "_Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnGO.transform.SetParent(parent, false);
        LayoutElement le = btnGO.GetComponent<LayoutElement>();
        le.minHeight = 46f;
        le.minWidth = 0f;
        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.29f, 0.21f, 0.12f, 1f);
        Outline outline = btnGO.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(action);

        TextMeshProUGUI txt = CreateCenteredButtonText(btnGO.transform, text);
        txt.fontSize = 16f;
        txt.color = labelColor;
        return btn;
    }

    private void CreateStartButton(RectTransform parent)
    {
        GameObject startBtnGO = new GameObject("LobbyStartGameBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        startBtnGO.transform.SetParent(parent, false);
        RectTransform startBtnRT = startBtnGO.GetComponent<RectTransform>();
        startBtnRT.anchorMin = new Vector2(0f, 0f);
        startBtnRT.anchorMax = new Vector2(1f, 0f);
        startBtnRT.pivot = new Vector2(0.5f, 0f);
        startBtnRT.anchoredPosition = new Vector2(0f, 16f);
        startBtnRT.sizeDelta = new Vector2(-36f, 56f);

        Image startBtnImg = startBtnGO.GetComponent<Image>();
        startBtnImg.color = new Color(0.24f, 0.38f, 0.22f, 1f);
        Outline outline = startBtnGO.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        startBtnText = CreateCenteredButtonText(startBtnGO.transform, "SPIEL STARTEN");
        startBtnText.fontSize = 18f;
        startBtnText.color = labelColor;

        lobbyStartButton = startBtnGO.GetComponent<Button>();
        lobbyStartButton.onClick.AddListener(OnLobbyStartButtonClicked);
    }

    private void SetGameSpeed(int speed)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SetRoomProperty(LobbySettingsKeys.GameSpeed, speed);
    }

    private void SetDayLength(float seconds)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SetRoomProperty(LobbySettingsKeys.DayLength, seconds);
    }

    private void SetStartVillagers(int amount)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SetRoomProperty(LobbySettingsKeys.StartVillagers, amount);
    }

    private void SetStartWorkers(int amount)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SetRoomProperty(LobbySettingsKeys.StartWorkers, amount);
    }

    private void SetWorldSize(string worldSize)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SetRoomProperty(LobbySettingsKeys.WorldSize, worldSize);
    }

    private void SetRoomProperty(string key, object value)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { key, value } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    private void UpdateSpeedUI()
    {
        if (lobbyStartButton == null || startBtnText == null) return;

        bool isMaster = PhotonNetwork.IsMasterClient;
        lobbyStartButton.interactable = isMaster;
        startBtnText.text = isMaster ? "SPIEL STARTEN" : "WARTE AUF HOST";
        startBtnText.color = isMaster ? labelColor : new Color(0.65f, 0.65f, 0.65f, 1f);
    }

    private void UpdateSettingsButtons()
    {
        if (!PhotonNetwork.InRoom) return;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        int speed = props.TryGetValue(LobbySettingsKeys.GameSpeed, out object speedObj) ? System.Convert.ToInt32(speedObj) : 1;
        float dayLength = props.TryGetValue(LobbySettingsKeys.DayLength, out object dayObj) ? System.Convert.ToSingle(dayObj) : 90f;
        int villagers = props.TryGetValue(LobbySettingsKeys.StartVillagers, out object villObj) ? System.Convert.ToInt32(villObj) : 10;
        int workers = props.TryGetValue(LobbySettingsKeys.StartWorkers, out object workObj) ? System.Convert.ToInt32(workObj) : 2;
        string worldSize = props.TryGetValue(LobbySettingsKeys.WorldSize, out object worldObj) ? worldObj.ToString() : "Standard";

        HighlightButtons(speedButtons, speedOptions, speed);
        HighlightButtons(dayLengthButtons, dayLengthOptions, dayLength);
        HighlightButtons(villagerButtons, villagerOptions, villagers);
        HighlightButtons(workerButtons, workerOptions, workers);
        HighlightButtons(worldSizeButtons, worldSizeLabels, worldSize);

        SetButtonsInteractable(speedButtons, PhotonNetwork.IsMasterClient);
        SetButtonsInteractable(dayLengthButtons, PhotonNetwork.IsMasterClient);
        SetButtonsInteractable(villagerButtons, PhotonNetwork.IsMasterClient);
        SetButtonsInteractable(workerButtons, PhotonNetwork.IsMasterClient);
        SetButtonsInteractable(worldSizeButtons, PhotonNetwork.IsMasterClient);
    }

    private void HighlightButtons(Button[] buttons, int[] values, int active)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            SetButtonVisual(buttons[i], values[i] == active);
        }
    }

    private void HighlightButtons(Button[] buttons, float[] values, float active)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            SetButtonVisual(buttons[i], Mathf.Approximately(values[i], active));
        }
    }

    private void HighlightButtons(Button[] buttons, string[] values, string active)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            SetButtonVisual(buttons[i], values[i] == active);
        }
    }

    private void SetButtonsInteractable(Button[] buttons, bool interactable)
    {
        foreach (Button button in buttons)
        {
            button.interactable = interactable;
            if (!interactable && button.GetComponent<Image>() != null)
            {
                button.GetComponent<Image>().color = new Color(0.24f, 0.20f, 0.16f, 0.85f);
            }
        }
    }

    private void SetButtonVisual(Button button, bool active)
    {
        if (button == null) return;
        Image img = button.GetComponent<Image>();
        if (img != null)
        {
            img.color = active ? new Color(0.72f, 0.55f, 0.20f, 1f) : new Color(0.29f, 0.21f, 0.12f, 1f);
        }
    }

    private string GetDayLengthLabel(float seconds)
    {
        if (Mathf.Approximately(seconds, 60f)) return "Kurz";
        if (Mathf.Approximately(seconds, 150f)) return "Lang";
        return "Normal";
    }

    private void OnLobbyStartButtonClicked()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.LoadLevel(SceneNames.GameScene);
        }
    }

    private void CreateBackdrop(Transform parent)
    {
        GameObject backdrop = new GameObject("LobbyBackdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(parent, false);
        RectTransform rt = backdrop.GetComponent<RectTransform>();
        Stretch(rt);
        Image image = backdrop.GetComponent<Image>();
        image.color = bgColor;
        backdrop.transform.SetAsFirstSibling();
    }

    private void RemoveExistingRuntimeLayout(Transform canvasTransform, params string[] names)
    {
        for (int i = canvasTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasTransform.GetChild(i);
            for (int j = 0; j < names.Length; j++)
            {
                if (child.name == names[j])
                {
                    Destroy(child.gameObject);
                    break;
                }
            }
        }
    }

    private void StyleExistingUi()
    {
        if (roomInfoText != null) roomInfoText.gameObject.SetActive(false);
        if (playerListText != null) playerListText.gameObject.SetActive(false);
        if (masterClientNoteText != null) masterClientNoteText.gameObject.SetActive(false);
        if (leaveRoomButton != null) leaveRoomButton.gameObject.SetActive(false);
        if (startTestButton != null) startTestButton.gameObject.SetActive(false);
    }

    private void CreateReadoutProxy(RectTransform parent, string name, TextMeshProUGUI source, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Place(rect, pos, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = color;
        text.enableWordWrapping = true;
        rect.gameObject.AddComponent<LobbyTextMirror>().Initialize(source, text);
    }

    private void CreateButtonProxy(RectTransform parent, string name, Button sourceButton, string label, Color fill, float y, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-32f, height);

        Image image = go.GetComponent<Image>();
        image.color = fill;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(() => sourceButton.onClick.Invoke());

        TextMeshProUGUI text = CreateCenteredButtonText(go.transform, label.ToUpper());
        text.fontSize = 18f;
        text.color = labelColor;
    }

    private RectTransform CreateCard(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = panelColor;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(3f, -3f);
        return rect;
    }

    private void StyleLobbyButton(Button button, string text, Color fill)
    {
        if (button == null) return;
        Image img = button.GetComponent<Image>();
        if (img != null) img.color = fill;
        Outline outline = button.gameObject.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = text.ToUpper();
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.color = labelColor;
        }
    }

    private void StyleText(TextMeshProUGUI text, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        if (text == null) return;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
    }

    private TextMeshProUGUI CreateStaticLabel(RectTransform parent, string textValue, float size, Vector2 pos, Vector2 height)
    {
        RectTransform rect = CreateRect(textValue + "_Label", parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = height;
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = textValue;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = labelColor;
        text.enableWordWrapping = true;
        return text;
    }

    private TextMeshProUGUI CreateCenteredButtonText(Transform parent, string value)
    {
        RectTransform rect = CreateRect("Text", parent);
        Stretch(rect);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        return text;
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private void Place(RectTransform rect, Vector2 offsetTopLeft, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = offsetTopLeft;
        rect.sizeDelta = size;
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
