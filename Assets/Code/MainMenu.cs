using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviourPunCallbacks
{
    [Header("UI References - Inputs")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Dropdown maxPlayersDropdown;

    [Header("UI References - Buttons")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRandomButton;
    [SerializeField] private Button joinByNameButton;

    [Header("UI References - Status")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button testLobbyButton;

    private const string PlayerNamePrefKey = "PlayerName";
    private readonly byte[] maxPlayersOptions = { 2, 4, 6, 8 };

    private readonly Color bgColor = new Color(0.07f, 0.09f, 0.11f, 1f);
    private readonly Color panelColor = new Color(0.13f, 0.10f, 0.07f, 0.92f);
    private readonly Color accentColor = new Color(0.85f, 0.68f, 0.29f, 1f);
    private readonly Color textColor = new Color(0.94f, 0.90f, 0.80f, 1f);
    private TMP_InputField runtimePlayerNameInput;
    private TMP_InputField runtimeRoomNameInput;
    private TMP_Dropdown runtimeMaxPlayersDropdown;
    private TextMeshProUGUI runtimeStatusText;
    private CanvasGroup runtimeMenuCanvasGroup;

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        LoadPlayerName();
        BuildRuntimeMenu();
        SetUIInteractable(false);
        SetStatus("Verbinde mit Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        SetStatus("Verbinde mit Lobby-Dienst...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        SetStatus("Verbunden. Erstelle eine Lobby oder tritt einer Runde bei.");
        SetUIInteractable(true);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetStatus($"Verbindung getrennt: {cause}");
        SetUIInteractable(false);
    }

    private void LoadPlayerName()
    {
        if (PlayerPrefs.HasKey(PlayerNamePrefKey))
        {
            playerNameInput.text = PlayerPrefs.GetString(PlayerNamePrefKey);
        }
    }

    private void SetUIInteractable(bool isInteractable)
    {
        if (createRoomButton != null) createRoomButton.interactable = isInteractable;
        if (joinRandomButton != null) joinRandomButton.interactable = isInteractable;
        if (joinByNameButton != null) joinByNameButton.interactable = isInteractable;
        if (playerNameInput != null) playerNameInput.interactable = isInteractable;
        if (roomNameInput != null) roomNameInput.interactable = isInteractable;
        if (maxPlayersDropdown != null) maxPlayersDropdown.interactable = isInteractable;
        if (testLobbyButton != null) testLobbyButton.interactable = isInteractable;

        if (runtimePlayerNameInput != null) runtimePlayerNameInput.interactable = isInteractable;
        if (runtimeRoomNameInput != null) runtimeRoomNameInput.interactable = isInteractable;
        if (runtimeMaxPlayersDropdown != null) runtimeMaxPlayersDropdown.interactable = isInteractable;

        if (runtimeMenuCanvasGroup != null)
        {
            runtimeMenuCanvasGroup.interactable = isInteractable;
            runtimeMenuCanvasGroup.blocksRaycasts = isInteractable;
            runtimeMenuCanvasGroup.alpha = isInteractable ? 1f : 0.72f;
        }
    }

    private bool EnsureReadyForMatchmaking()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            return true;
        }

        SetStatus("Noch nicht mit Photon verbunden. Bitte kurz warten...");
        return false;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (runtimeStatusText != null)
        {
            runtimeStatusText.text = message;
        }
    }

    private bool SetupPlayerName()
    {
        string playerName = playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            SetStatus("Bitte gib erst einen Spielernamen ein.");
            return false;
        }

        PhotonNetwork.LocalPlayer.NickName = playerName;
        PlayerPrefs.SetString(PlayerNamePrefKey, playerName);
        PlayerPrefs.Save();
        return true;
    }

    public void OnCreateRoomButtonClicked()
    {
        if (!EnsureReadyForMatchmaking() || !SetupPlayerName()) return;

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            SetStatus("Bitte gib einen Lobby-Namen ein.");
            return;
        }

        byte selectedMaxPlayers = maxPlayersOptions[Mathf.Clamp(maxPlayersDropdown.value, 0, maxPlayersOptions.Length - 1)];
        int seed = Random.Range(1, 1000000);
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = selectedMaxPlayers,
            IsOpen = true,
            IsVisible = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { 
                { LobbySettingsKeys.WorldSize, "Standard" },
                { LobbySettingsKeys.MapSeed, seed } 
            },
            CustomRoomPropertiesForLobby = new[] { LobbySettingsKeys.WorldSize, LobbySettingsKeys.MapSeed }
        };

        SetStatus($"Erstelle Lobby '{roomName}'...");
        SetUIInteractable(false);
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void OnJoinByNameButtonClicked()
    {
        if (!EnsureReadyForMatchmaking() || !SetupPlayerName()) return;

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            SetStatus("Bitte gib den Namen der Lobby ein.");
            return;
        }

        SetStatus($"Trete Lobby '{roomName}' bei...");
        SetUIInteractable(false);
        PhotonNetwork.JoinRoom(roomName);
    }

    public void OnJoinRandomButtonClicked()
    {
        if (!EnsureReadyForMatchmaking() || !SetupPlayerName()) return;

        SetStatus("Suche eine offene Lobby...");
        SetUIInteractable(false);
        PhotonNetwork.JoinRandomRoom();
    }

    public void OnTestLobbyButtonClicked()
    {
        if (!EnsureReadyForMatchmaking() || !SetupPlayerName()) return;

        string roomName = "TestRoom_" + Random.Range(1000, 9999);
        int seed = Random.Range(1, 1000000);
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 4,
            IsOpen = true,
            IsVisible = false,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { 
                { "TestMode", true },
                { LobbySettingsKeys.MapSeed, seed }
            },
            CustomRoomPropertiesForLobby = new[] { "TestMode", LobbySettingsKeys.MapSeed }
        };

        SetStatus("Erstelle Test-Lobby...");
        SetUIInteractable(false);
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[MainMenu] OnJoinedRoom: Raum={PhotonNetwork.CurrentRoom.Name}, Spieler={PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}");
        if (!IsSceneInBuildSettings(SceneNames.LobbyScene))
        {
            SetStatus($"Fehler: Szene '{SceneNames.LobbyScene}' nicht in Build Settings.");
            SetUIInteractable(true);
            Debug.LogError($"Scene '{SceneNames.LobbyScene}' fehlt in den Build Settings.");
            return;
        }

        SetStatus("Lobby gefunden. Wechsle in den Warteraum...");
        PhotonNetwork.LoadLevel(SceneNames.LobbyScene);
    }

    private bool IsSceneInBuildSettings(string sceneName)
    {
        int count = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return true;
        }
        return false;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetStatus($"Beitritt fehlgeschlagen: {message}");
        SetUIInteractable(true);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        SetStatus("Keine offene Lobby gefunden. Erstelle einfach eine neue.");
        SetUIInteractable(true);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetStatus($"Lobby konnte nicht erstellt werden: {message}");
        SetUIInteractable(true);
    }

    private void BuildRuntimeMenu()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        // Fix: Make sure canvas is always overlay so camera FOV doesn't clip/hide it
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        HideOriginalUi();
        EnsureBackdrop(canvas.transform, "MainMenuBackdrop");
        EnsureTitle(canvas.transform, "MainMenuTitle", "ISLES OF WEALTH", new Vector2(0f, -60f));
        RemoveExistingRuntimeMenu(canvas.transform);

        RectTransform root = CreateRect("MainMenuRuntimeRoot", canvas.transform);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(620f, 500f);

        Image panel = root.gameObject.AddComponent<Image>();
        panel.color = panelColor;
        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateRuntimeLabel(root, "Spiele deinen Inselstaat, eröffne eine Lobby und starte direkt in eine neue Runde.", 18f, 56f, new Color(0.86f, 0.89f, 0.90f, 1f));
        runtimePlayerNameInput = CreateRuntimeInput(root, "SPIELERNAME", playerNameInput != null ? playerNameInput.text : "");
        runtimeRoomNameInput = CreateRuntimeInput(root, "LOBBYNAME", roomNameInput != null ? roomNameInput.text : "");
        runtimeMaxPlayersDropdown = CreateRuntimeDropdown(root, "MAX. SPIELER", new[] { "2", "4", "6", "8" }, maxPlayersDropdown != null ? maxPlayersDropdown.value : 1);

        CreateRuntimeButton(root, "LOBBY ERSTELLEN", new Color(0.27f, 0.42f, 0.24f, 1f), OnRuntimeCreateRoom);
        CreateRuntimeButton(root, "PER NAME BEITRETEN", new Color(0.30f, 0.24f, 0.15f, 1f), OnRuntimeJoinByName);
        CreateRuntimeButton(root, "OFFENE LOBBY FINDEN", new Color(0.19f, 0.24f, 0.30f, 1f), OnRuntimeJoinRandom);
        CreateRuntimeButton(root, "TEST-LOBBY", new Color(0.20f, 0.21f, 0.24f, 1f), OnRuntimeTestLobby, 48f);

        runtimeStatusText = CreateRuntimeLabel(root, "", 16f, 50f, new Color(0.87f, 0.89f, 0.90f, 1f));
        root.gameObject.AddComponent<MainMenuStatusMirror>().Initialize(statusText, runtimeStatusText);

        runtimeMenuCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        runtimeMenuCanvasGroup.interactable = false;
        runtimeMenuCanvasGroup.blocksRaycasts = false;
        runtimeMenuCanvasGroup.alpha = 0.72f;
    }

    private void HideOriginalUi()
    {
        if (playerNameInput != null) playerNameInput.gameObject.SetActive(false);
        if (roomNameInput != null) roomNameInput.gameObject.SetActive(false);
        if (maxPlayersDropdown != null) maxPlayersDropdown.gameObject.SetActive(false);
        if (createRoomButton != null) createRoomButton.gameObject.SetActive(false);
        if (joinRandomButton != null) joinRandomButton.gameObject.SetActive(false);
        if (joinByNameButton != null) joinByNameButton.gameObject.SetActive(false);
        if (testLobbyButton != null) testLobbyButton.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    private void RemoveExistingRuntimeMenu(Transform parent)
    {
        Transform existing = parent.Find("MainMenuRuntimeRoot");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }
    }

    private void OnRuntimeCreateRoom()
    {
        SyncRuntimeFieldsToOriginals();
        OnCreateRoomButtonClicked();
    }

    private void OnRuntimeJoinByName()
    {
        SyncRuntimeFieldsToOriginals();
        OnJoinByNameButtonClicked();
    }

    private void OnRuntimeJoinRandom()
    {
        SyncRuntimeFieldsToOriginals();
        OnJoinRandomButtonClicked();
    }

    private void OnRuntimeTestLobby()
    {
        SyncRuntimeFieldsToOriginals();
        OnTestLobbyButtonClicked();
    }

    private void SyncRuntimeFieldsToOriginals()
    {
        if (playerNameInput != null && runtimePlayerNameInput != null)
        {
            playerNameInput.text = runtimePlayerNameInput.text;
        }

        if (roomNameInput != null && runtimeRoomNameInput != null)
        {
            roomNameInput.text = runtimeRoomNameInput.text;
        }

        if (maxPlayersDropdown != null && runtimeMaxPlayersDropdown != null)
        {
            maxPlayersDropdown.value = runtimeMaxPlayersDropdown.value;
        }
    }

    private void EnsureBackdrop(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return;
        }

        GameObject backdrop = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(parent, false);
        RectTransform rt = backdrop.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image image = backdrop.GetComponent<Image>();
        image.color = bgColor;
        backdrop.transform.SetAsFirstSibling();
    }

    private void EnsureTitle(Transform parent, string objectName, string textValue, Vector2 anchoredPos)
    {
        Transform existing = parent.Find(objectName);
        if (existing == null)
        {
            GameObject titleGo = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(parent, false);
            RectTransform rt = titleGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(900f, 90f);
            TextMeshProUGUI text = titleGo.GetComponent<TextMeshProUGUI>();
            text.text = textValue;
            text.fontSize = 40f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = textColor;
            return;
        }

        TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
        if (existingText != null)
        {
            existingText.text = textValue;
        }
    }

    private void ApplyPanel(GameObject go, Color fill)
    {
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = fill;
        Outline outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private TMP_InputField CreateRuntimeInput(RectTransform parent, string label, string value)
    {
        GameObject wrapper = new GameObject(label + "_Wrapper", typeof(RectTransform), typeof(LayoutElement));
        wrapper.transform.SetParent(parent, false);
        wrapper.GetComponent<LayoutElement>().minHeight = 72f;

        CreateCaption(wrapper.transform, label);

        GameObject bg = new GameObject(label + "_Input", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(wrapper.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(0f, 50f);
        ApplyPanel(bg, panelColor);

        GameObject textArea = new GameObject("TextArea", typeof(RectTransform));
        textArea.transform.SetParent(bg.transform, false);
        RectTransform taRt = textArea.GetComponent<RectTransform>();
        taRt.anchorMin = new Vector2(0f, 0f);
        taRt.anchorMax = new Vector2(1f, 1f);
        taRt.offsetMin = new Vector2(14f, 8f);
        taRt.offsetMax = new Vector2(-14f, -8f);

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderGo.transform.SetParent(textArea.transform, false);
        RectTransform phRt = placeholderGo.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = Vector2.zero;
        phRt.offsetMax = Vector2.zero;
        TextMeshProUGUI placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
        placeholder.text = label;
        placeholder.fontSize = 18f;
        placeholder.color = new Color(0.73f, 0.67f, 0.58f, 1f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(textArea.transform, false);
        RectTransform txRt = textGo.GetComponent<RectTransform>();
        txRt.anchorMin = Vector2.zero;
        txRt.anchorMax = Vector2.one;
        txRt.offsetMin = Vector2.zero;
        txRt.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 20f;
        text.color = textColor;

        TMP_InputField input = bg.AddComponent<TMP_InputField>();
        input.textViewport = taRt;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.text = value;
        return input;
    }

    private TMP_Dropdown CreateRuntimeDropdown(RectTransform parent, string label, string[] options, int selectedIndex)
    {
        GameObject wrapper = new GameObject(label + "_Wrapper", typeof(RectTransform), typeof(LayoutElement));
        wrapper.transform.SetParent(parent, false);
        wrapper.GetComponent<LayoutElement>().minHeight = 72f;

        CreateCaption(wrapper.transform, label);

        GameObject bg = new GameObject(label + "_Dropdown", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(wrapper.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(0f, 50f);
        ApplyPanel(bg, panelColor);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(bg.transform, false);
        RectTransform lblRt = labelGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(14f, 8f);
        lblRt.offsetMax = new Vector2(-40f, -8f);
        TextMeshProUGUI caption = labelGo.GetComponent<TextMeshProUGUI>();
        caption.fontSize = 20f;
        caption.color = textColor;

        GameObject arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowGo.transform.SetParent(bg.transform, false);
        RectTransform arrRt = arrowGo.GetComponent<RectTransform>();
        arrRt.anchorMin = new Vector2(1f, 0.5f);
        arrRt.anchorMax = new Vector2(1f, 0.5f);
        arrRt.pivot = new Vector2(1f, 0.5f);
        arrRt.anchoredPosition = new Vector2(-14f, 0f);
        arrRt.sizeDelta = new Vector2(24f, 24f);
        TextMeshProUGUI arrowText = arrowGo.GetComponent<TextMeshProUGUI>();
        arrowText.text = "v";
        arrowText.fontSize = 18f;
        arrowText.color = accentColor;
        arrowText.alignment = TextAlignmentOptions.Center;

        TMP_Dropdown dropdown = bg.AddComponent<TMP_Dropdown>();
        dropdown.captionText = caption;
        dropdown.options.Clear();
        for (int i = 0; i < options.Length; i++)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(options[i]));
        }
        dropdown.value = Mathf.Clamp(selectedIndex, 0, options.Length - 1);
        dropdown.RefreshShownValue();
        return dropdown;
    }

    private void CreateRuntimeButton(RectTransform parent, string label, Color fill, UnityEngine.Events.UnityAction action, float height = 54f)
    {
        GameObject go = new GameObject(label + "_Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = height;
        ApplyPanel(go, fill);

        TextMeshProUGUI text = CreateRuntimeLabel(go.GetComponent<RectTransform>(), label, 19f, height, textColor);
        text.alignment = TextAlignmentOptions.Center;

        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(action);
    }

    private TextMeshProUGUI CreateRuntimeLabel(RectTransform parent, string value, float fontSize, float height, Color color)
    {
        GameObject go = new GameObject(value + "_Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = height;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private void CreateCaption(Transform parent, string label)
    {
        GameObject captionGo = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        captionGo.transform.SetParent(parent, false);
        RectTransform rt = captionGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(0f, 18f);
        TextMeshProUGUI text = captionGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 12f;
        text.fontStyle = FontStyles.Bold;
        text.color = accentColor;
        text.alignment = TextAlignmentOptions.TopLeft;
    }
}
