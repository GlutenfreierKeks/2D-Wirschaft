using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Manages the main gameplay loop and player spawning.
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte LobbyChatEventCode = 1;
    private readonly int maxChatMessages = 6;
    private readonly List<string> chatMessages = new List<string>();

    private TextMeshProUGUI chatLogText;
    private TMP_InputField chatInputField;
    private Button chatSendButton;
    private RectTransform chatPanelRoot;
    private GameObject chatLogArea;
    private GameObject chatInputArea;
    private GameObject chatToggleIcon;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI gameStatusText;

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Start()
    {
        EnsureRequiredManagers();

        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
            UpdateStatusText();
            BuildGameChatUI();
        }
        else
        {
            if (gameStatusText != null) gameStatusText.text = "Game Started! (Offline Mode)";
            Debug.LogWarning("GameManager loaded, but client is not in a Photon room.");
        }

        SpawnInitialSoldiers();
    }

    private void EnsureRequiredManagers()
    {
        EnsureManagerExists<GridManager>();
        EnsureManagerExists<IslandManager>();
        EnsureManagerExists<BuildingManager>();
        EnsureManagerExists<ResourceManager>();
        EnsureManagerExists<PlacementManager>();
        EnsureManagerExists<FogProjector>();
        EnsureManagerExists<SelectionManager>();
        EnsureManagerExists<VillagerManager>();
        EnsureManagerExists<NotificationManager>();
        EnsureManagerExists<AudioManager>();
        EnsureManagerExists<TradingManager>();
        EnsureManagerExists<TradingUI>();
    }

    private void EnsureManagerExists<T>() where T : MonoBehaviour
    {
        if (FindObjectOfType<T>() == null)
        {
            GameObject obj = new GameObject(typeof(T).Name);
            obj.AddComponent<T>();
            Debug.LogWarning($"[GameManager] Erstellte fehlenden Manager: {typeof(T).Name}");
        }
    }

    private void SpawnPlayer()
    {
        if (IslandManager.Instance == null)
        {
            Debug.LogError("IslandManager instance not found! Cannot calculate island spawn position.");
            return;
        }

        // Ensure islands are generated before we look up their positions
        IslandManager.Instance.GenerateIslands();

        // Check if we are in Test Mode
        bool isTestMode = false;
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("TestMode", out object testModeValue))
        {
            isTestMode = (bool)testModeValue;
        }

        // Get player index
        int playerIndex = 0;
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].IsLocal)
            {
                playerIndex = i;
                break;
            }
        }

        // Pick an island based on the player index
        Vector2 islandPos = IslandManager.Instance.GetIslandPosition(playerIndex);
        IslandType islandType = IslandManager.Instance.GetIslandType(playerIndex);
        
        Vector3 spawnPos = new Vector3(islandPos.x, islandPos.y, -10f);
        
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.InitializeResources(islandType);
        }
        
        Debug.Log($"[GameManager] Spawning at Island {playerIndex}: {spawnPos}");
        
        // Move camera for the local player
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector2 localIslandPos = IslandManager.Instance.GetIslandPosition(playerIndex);
            mainCam.transform.position = new Vector3(localIslandPos.x, localIslandPos.y, -10f);
            mainCam.transform.rotation = Quaternion.identity;
        }

        // Spawn warehouses for ALL players
        if (BuildingManager.Instance != null)
        {
            for (int i = 0; i < players.Length; i++)
            {
                Vector2 pos = IslandManager.Instance.GetIslandPosition(i);
                bool isLocal = players[i].IsLocal;
                BuildingManager.Instance.SpawnMainWarehouse(pos, isLocal);

                // If it's the local player, reveal the entire starting island
                if (isLocal)
                {
                    GameObject islandRevealer = new GameObject("StartIslandRevealer");
                    islandRevealer.transform.position = new Vector3(pos.x, pos.y, 0);
                    FogRevealer fr = islandRevealer.AddComponent<FogRevealer>();
                    fr.radius = 95f; // Large enough to cover the spawn island
                    fr.isLocalPlayer = true;
                    
                    // Also register it as fully explored
                    FogProjector.RegisterExploration(pos, 80f);

                    if (VillagerManager.Instance != null)
                    {
                        VillagerManager.Instance.SpawnStartingPopulation(playerIndex);
                    }
                }
            }
        }
    }

    private void UpdateStatusText()
    {
        if (gameStatusText != null)
        {
            gameStatusText.text = $"Game Started! Players: {PhotonNetwork.CurrentRoom.PlayerCount}";
        }
    }

    private void BuildGameChatUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        RectTransform root = CreateRect("GameChatPanel", canvas.transform);
        chatPanelRoot = root;
        root.anchorMin = new Vector2(0.04f, 0.02f);
        root.anchorMax = new Vector2(0.36f, 0.24f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = Vector2.zero;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image bg = root.gameObject.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.10f, 0.16f, 0.90f);
        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.76f, 0.70f, 0.45f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI title = CreateStaticLabel(root, "SPIEL-CHAT", 16f, new Vector2(12f, -10f), new Vector2(-24f, 22f));
        title.color = new Color(0.90f, 0.84f, 0.60f, 1f);
        title.alignment = TextAlignmentOptions.TopLeft;
        title.raycastTarget = false;

        RectTransform logRect = CreateRect("GameChatLog", root);
        chatLogArea = logRect.gameObject;
        logRect.anchorMin = new Vector2(0f, 0.30f);
        logRect.anchorMax = new Vector2(1f, 1f);
        logRect.pivot = new Vector2(0.5f, 1f);
        logRect.anchoredPosition = new Vector2(0f, -36f);
        logRect.offsetMin = new Vector2(12f, 0f);
        logRect.offsetMax = new Vector2(-12f, 0f);

        chatLogText = logRect.gameObject.AddComponent<TextMeshProUGUI>();
        chatLogText.text = "Spiel-Chat aktiv. Schreibe etwas, um Lastigkeit zu prüfen.";
        chatLogText.fontSize = 14f;
        chatLogText.alignment = TextAlignmentOptions.TopLeft;
        chatLogText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        chatLogText.enableWordWrapping = true;
        chatLogText.overflowMode = TextOverflowModes.Truncate;
        chatLogText.raycastTarget = false;

        RectTransform inputRow = CreateRect("GameChatInputRow", root);
        chatInputArea = inputRow.gameObject;
        inputRow.anchorMin = new Vector2(0f, 0f);
        inputRow.anchorMax = new Vector2(1f, 0f);
        inputRow.pivot = new Vector2(0.5f, 0f);
        inputRow.anchoredPosition = new Vector2(0f, 10f);
        inputRow.sizeDelta = new Vector2(-24f, 36f);

        RectTransform inputFieldRect = CreateRect("GameChatInputField", inputRow);
        inputFieldRect.anchorMin = new Vector2(0f, 0f);
        inputFieldRect.anchorMax = new Vector2(0.72f, 1f);
        inputFieldRect.offsetMin = Vector2.zero;
        inputFieldRect.offsetMax = Vector2.zero;

        Image inputBg = inputFieldRect.gameObject.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        Outline inputOutline = inputFieldRect.gameObject.AddComponent<Outline>();
        inputOutline.effectColor = new Color(0.5f, 0.5f, 0.55f, 0.7f);
        inputOutline.effectDistance = new Vector2(1f, -1f);

        chatInputField = inputFieldRect.gameObject.AddComponent<TMP_InputField>();
        chatInputField.textViewport = inputFieldRect;
        chatInputField.textComponent = CreateInputText(inputFieldRect, "");
        chatInputField.placeholder = CreateInputText(inputFieldRect, "Nachricht...", true);
        chatInputField.characterLimit = 120;
        chatInputField.onSubmit.AddListener(OnChatInputEndEdit);

        RectTransform buttonRect = CreateRect("GameChatSendButton", inputRow);
        buttonRect.anchorMin = new Vector2(0.74f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        chatSendButton = buttonRect.gameObject.AddComponent<Button>();
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.24f, 0.34f, 0.18f, 1f);
        Outline buttonOutline = buttonRect.gameObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(0.80f, 0.70f, 0.40f, 1f);
        buttonOutline.effectDistance = new Vector2(2f, -2f);
        chatSendButton.onClick.AddListener(SubmitChatInput);

        TextMeshProUGUI buttonText = CreateCenteredButtonText(buttonRect.transform, "Senden");
        buttonText.fontSize = 14f;
        buttonText.color = new Color(0.96f, 0.96f, 0.96f, 1f);
        buttonText.alignment = TextAlignmentOptions.Center;

        AddChatMessage("Spiel-Chat bereit. Wenn du den Gast siehst, bist du in derselben Lobby.");
        CreateChatCloseButton(root);

        chatPanelRoot.gameObject.SetActive(false);
        CreateChatToggleIcon(canvas.transform);
    }

    private void OnChatInputEndEdit(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        SubmitChatInput();
    }

    private void SubmitChatInput()
    {
        if (chatInputField == null) return;
        string message = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;
        SendChatMessage(message);
        chatInputField.text = string.Empty;
        chatInputField.ActivateInputField();
    }

    private void SendChatMessage(string message)
    {
        if (!PhotonNetwork.InRoom) return;
        string sender = string.IsNullOrEmpty(PhotonNetwork.NickName) ? "Spieler" : PhotonNetwork.NickName;
        string payload = $"[{sender}] {message}";

        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        SendOptions sendOptions = new SendOptions { Reliability = true };
        PhotonNetwork.RaiseEvent(LobbyChatEventCode, payload, options, sendOptions);
    }

    private void AddChatMessage(string message)
    {
        chatMessages.Add(message);
        if (chatMessages.Count > maxChatMessages)
        {
            chatMessages.RemoveAt(0);
        }

        if (chatLogText != null)
        {
            chatLogText.text = string.Join("\n", chatMessages);
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != LobbyChatEventCode) return;
        if (photonEvent.CustomData is string message)
        {
            AddChatMessage(message);
        }
    }

    private void CreateChatCloseButton(RectTransform parent)
    {
        RectTransform btnRect = CreateRect("GameChatCloseButton", parent);
        btnRect.anchorMin = new Vector2(1f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(1f, 1f);
        btnRect.anchoredPosition = new Vector2(-10f, -8f);
        btnRect.sizeDelta = new Vector2(28f, 28f);

        Image btnImage = btnRect.gameObject.AddComponent<Image>();
        btnImage.color = new Color(0.16f, 0.20f, 0.26f, 0.95f);
        btnImage.raycastTarget = true;

        Button closeBtn = btnRect.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = btnImage;
        closeBtn.onClick.AddListener(CloseChatPanel);

        TextMeshProUGUI txt = CreateCenteredButtonText(btnRect.transform, "X");
        txt.fontSize = 16f;
        txt.color = new Color(0.90f, 0.84f, 0.60f, 1f);
        txt.raycastTarget = false;

        btnRect.SetAsLastSibling();
    }

    private void CreateChatToggleIcon(Transform canvasTransform)
    {
        RectTransform iconRect = CreateRect("ChatToggleIcon", canvasTransform);
        iconRect.anchorMin = new Vector2(0.04f, 0.02f);
        iconRect.anchorMax = new Vector2(0.04f, 0.02f);
        iconRect.pivot = new Vector2(0f, 0f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(56f, 56f);

        Image iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.raycastTarget = true;

        Texture2D chatTex = Resources.Load<Texture2D>("chat-icon");
        Sprite chatSprite = chatTex != null ? Sprite.Create(chatTex, new Rect(0, 0, chatTex.width, chatTex.height), new Vector2(0.5f, 0.5f)) : null;
        if (chatSprite != null)
        {
            iconImage.sprite = chatSprite;
            iconImage.preserveAspect = true;
        }
        else
        {
            iconImage.color = new Color(0.16f, 0.20f, 0.26f, 0.95f);
            TextMeshProUGUI fallback = CreateCenteredButtonText(iconRect, "Chat");
            fallback.fontSize = 12f;
        }

        Button toggleBtn = iconRect.gameObject.AddComponent<Button>();
        toggleBtn.targetGraphic = iconImage;
        toggleBtn.onClick.AddListener(ToggleChatPanel);

        chatToggleIcon = iconRect.gameObject;
    }

    private void ToggleChatPanel()
    {
        if (chatPanelRoot == null || chatToggleIcon == null) return;
        bool show = !chatPanelRoot.gameObject.activeSelf;
        chatPanelRoot.gameObject.SetActive(show);
        chatToggleIcon.SetActive(!show);

        if (show && chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    private void CloseChatPanel()
    {
        if (chatPanelRoot != null) chatPanelRoot.gameObject.SetActive(false);
        if (chatToggleIcon != null) chatToggleIcon.SetActive(true);
    }

    private void Update()
    {
        if (chatPanelRoot == null || chatToggleIcon == null) return;
        if (Keyboard.current == null || !Keyboard.current.tKey.wasPressedThisFrame) return;

        ToggleChatPanel();

        if (chatPanelRoot.gameObject.activeSelf && chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    private TextMeshProUGUI CreateInputText(RectTransform parent, string value, bool isPlaceholder = false)
    {
        RectTransform rect = CreateRect(isPlaceholder ? "Placeholder" : "InputText", parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 6f);
        rect.offsetMax = new Vector2(-8f, -6f);

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = 14f;
        text.alignment = TextAlignmentOptions.Left;
        text.color = isPlaceholder ? new Color(0.68f, 0.68f, 0.68f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f);
        text.enableWordWrapping = false;
        return text;
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
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
        text.color = new Color(0.90f, 0.84f, 0.60f, 1f);
        text.enableWordWrapping = true;
        return text;
    }

    private TextMeshProUGUI CreateCenteredButtonText(Transform parent, string value)
    {
        RectTransform rect = CreateRect("Text", parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.96f, 0.96f, 0.96f, 1f);
        return text;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateStatusText();
        Debug.Log($"{newPlayer.NickName} joined the room.");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateStatusText();
        Debug.Log($"{otherPlayer.NickName} left the room.");
    }

    public void ReturnToMenu()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.StartMenuScene);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Disconnected from Photon. Cause: {cause}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.StartMenuScene);
    }

    private void SpawnInitialSoldiers()
    {
        Vector3 spawnPos;
        Warehouse wh = FindObjectOfType<Warehouse>();
        if (wh != null)
        {
            spawnPos = wh.transform.position + new Vector3(2f, 0f, 0f);
        }
        else if (IslandManager.Instance != null)
        {
            Vector2 islandPos = IslandManager.Instance.GetIslandPosition(0);
            spawnPos = new Vector3(islandPos.x + 2f, islandPos.y, 0f);
        }
        else
        {
            return;
        }

        SoldierType[] types = { SoldierType.Spear, SoldierType.Shield, SoldierType.Sword, SoldierType.Bow };

        foreach (var type in types)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject solObj = new GameObject($"Init_{type}_{i}");
                Vector3 offset = new Vector3(i * 1.2f, (int)type * 1.2f, 0f);
                solObj.transform.position = spawnPos + offset;

                var sr = solObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 21;
                solObj.AddComponent<BoxCollider2D>().size = new Vector2(1f, 1f);

                var s = solObj.AddComponent<Soldier>();
                s.soldierType = type;
                s.team = Team.Player;
                s.moveSpeed = 1.5f;
            }
        }

        Debug.Log("[GameManager] 8 Start-Soldaten gespawnt (2 pro Typ).");
    }
}
