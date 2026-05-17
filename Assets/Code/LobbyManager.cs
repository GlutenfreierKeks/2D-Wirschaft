using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Handles the lobby screen where players wait for the room to fill up.
/// Scene: LobbyScene
/// </summary>
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

    private Button lobbyStartButton;
    private TextMeshProUGUI startBtnText;

    private void Start()
    {
        // Ensure that clients sync their scene with the Master Client
        PhotonNetwork.AutomaticallySyncScene = true;

        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
        }

        if (startTestButton != null)
        {
            startTestButton.onClick.AddListener(OnStartTestButtonClicked);
            startTestButton.gameObject.SetActive(false);
        }

        // Initialize default room properties for speed if Master Client
        if (PhotonNetwork.IsMasterClient && !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("GameSpeed"))
        {
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "GameSpeed", 1 } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        CreateSpeedControlUI();
        UpdateLobbyUI();
        CheckAutoStart();
    }

    /// <summary>
    /// Refreshes the lobby texts (room info, player counts, player list).
    /// </summary>
    private void UpdateLobbyUI()
    {
        if (!PhotonNetwork.InRoom) return;

        Room currentRoom = PhotonNetwork.CurrentRoom;

        // Update Room Info
        roomInfoText.text = $"Room: {currentRoom.Name} | Players: {currentRoom.PlayerCount}/{currentRoom.MaxPlayers}";

        // Update Player List
        playerListText.text = "<b>Players in Room:</b>\n";
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string masterTag = player.IsMasterClient ? " (Master)" : "";
            string youTag = player.IsLocal ? " (You)" : "";
            playerListText.text += $"- {player.NickName}{masterTag}{youTag}\n";
        }

        // Master Client UI Note
        bool isTestMode = currentRoom.CustomProperties.ContainsKey("TestMode") && (bool)currentRoom.CustomProperties["TestMode"];

        if (isTestMode)
        {
            playerListText.text += "- [Bot] Dummy Player 1\n";
            playerListText.text += "- [Bot] Dummy Player 2\n";
            
            if (startTestButton != null && PhotonNetwork.IsMasterClient)
            {
                startTestButton.gameObject.SetActive(true);
            }
        }

        if (masterClientNoteText != null)
        {
            masterClientNoteText.text = PhotonNetwork.IsMasterClient
                ? (isTestMode ? "Test Mode: Press Start to spawn with bots." : "You are the Master Client. Game will start when room is full.")
                : "Waiting for Master Client to start the game...";
        }

        UpdateSpeedUI();
    }

    private void OnStartTestButtonClicked()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.LoadLevel(SceneNames.GameScene);
        }
    }

    /// <summary>
    /// Initiates leaving the room.
    /// </summary>
    public void OnLeaveRoomButtonClicked()
    {
        if (leaveRoomButton != null) leaveRoomButton.interactable = false;
        PhotonNetwork.LeaveRoom();
    }

    /// <summary>
    /// Evaluates whether the room is full and starts the game if this is the Master Client.
    /// </summary>
    private void CheckAutoStart()
    {
        if (!PhotonNetwork.InRoom) return;

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            // Close the room so nobody else can join during load
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.LoadLevel(SceneNames.GameScene);
        }
    }

    #region PUN Callbacks

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (joinLogUI != null)
        {
            joinLogUI.LogPlayerJoin(newPlayer.NickName);
        }

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
        // Note: Use SceneManager here because we just disconnected from the room.
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.StartMenuScene);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("GameSpeed"))
        {
            UpdateSpeedUI();
        }
    }

    private void CreateSpeedControlUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Container Panel
        GameObject panelGO = new GameObject("LobbySpeedControlPanel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);
        
        RectTransform rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);
        rt.sizeDelta = new Vector2(240f, 170f);

        Image bgImage = panelGO.GetComponent<Image>();
        bgImage.color = new Color(0.18f, 0.1f, 0.03f, 0.95f); // Bar color matches medieval style

        // Beautiful Golden Border Padding
        GameObject borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(panelGO.transform, false);
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.sizeDelta = Vector2.zero;
        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.color = new Color(0.72f, 0.52f, 0.18f, 1f); // golden

        // Content panel
        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(Image));
        contentGO.transform.SetParent(panelGO.transform, false);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(-4f, -4f); // padding for border
        Image contentImg = contentGO.GetComponent<Image>();
        contentImg.color = new Color(0.18f, 0.1f, 0.03f, 0.95f);

        // Header Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(contentGO.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 1f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -10f);
        labelRT.sizeDelta = new Vector2(-20f, 25f);
        
        TextMeshProUGUI labelText = labelGO.GetComponent<TextMeshProUGUI>();
        labelText.text = "SPIELGESCHWINDIGKEIT";
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 11;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = new Color(0.9f, 0.78f, 0.52f, 1f);

        // Horizontal Buttons Row
        GameObject rowGO = new GameObject("ButtonRow", typeof(RectTransform));
        rowGO.transform.SetParent(contentGO.transform, false);
        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 1f);
        rowRT.anchorMax = new Vector2(1f, 1f);
        rowRT.pivot = new Vector2(0.5f, 1f);
        rowRT.anchoredPosition = new Vector2(0f, -40f);
        rowRT.sizeDelta = new Vector2(-20f, 44f);

        float buttonWidth = 60f;
        float spacing = 15f;
        float startX = -buttonWidth - spacing;

        for (int i = 1; i <= 3; i++)
        {
            int speed = i;
            GameObject btnGO = new GameObject($"SpeedBtn_{speed}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rowRT.transform, false);
            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(startX + (speed - 1) * (buttonWidth + spacing), 0f);
            btnRT.sizeDelta = new Vector2(buttonWidth, 44f);

            Image btnImg = btnGO.GetComponent<Image>();
            Sprite btnSprite = GetSpeedSprite(speed);
            if (btnSprite != null)
            {
                btnImg.sprite = btnSprite;
                btnImg.color = Color.white;
            }
            else
            {
                btnImg.color = new Color(0.42f, 0.27f, 0.09f, 0.92f);
            }

            GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(btnGO.transform, false);
            RectTransform txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            TextMeshProUGUI btnTxt = txtGO.GetComponent<TextMeshProUGUI>();
            btnTxt.text = $"{speed}x";
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.fontSize = 12;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.color = Color.white;
            btnTxt.outlineWidth = 0.2f;
            btnTxt.outlineColor = Color.black;

            Button btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => SetGameSpeed(speed));
        }

        // Start Game Button
        GameObject startBtnGO = new GameObject("LobbyStartGameBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        startBtnGO.transform.SetParent(contentGO.transform, false);
        RectTransform startBtnRT = startBtnGO.GetComponent<RectTransform>();
        startBtnRT.anchorMin = new Vector2(0f, 0f);
        startBtnRT.anchorMax = new Vector2(1f, 0f);
        startBtnRT.pivot = new Vector2(0.5f, 0f);
        startBtnRT.anchoredPosition = new Vector2(0f, 10f);
        startBtnRT.sizeDelta = new Vector2(-20f, 40f);

        Image startBtnImg = startBtnGO.GetComponent<Image>();
        startBtnImg.color = new Color(0.42f, 0.27f, 0.09f, 0.92f);

        GameObject startTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        startTxtGO.transform.SetParent(startBtnGO.transform, false);
        RectTransform startTxtRT = startTxtGO.GetComponent<RectTransform>();
        startTxtRT.anchorMin = Vector2.zero;
        startTxtRT.anchorMax = Vector2.one;
        startTxtRT.sizeDelta = Vector2.zero;
        
        startBtnText = startTxtGO.GetComponent<TextMeshProUGUI>();
        startBtnText.alignment = TextAlignmentOptions.Center;
        startBtnText.fontSize = 13;
        startBtnText.fontStyle = FontStyles.Bold;
        startBtnText.color = new Color(0.95f, 0.9f, 0.8f, 1f);

        lobbyStartButton = startBtnGO.GetComponent<Button>();
        lobbyStartButton.onClick.AddListener(OnLobbyStartButtonClicked);

        UpdateSpeedUI();
    }

    private void SetGameSpeed(int speed)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "GameSpeed", speed } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        
        Debug.Log($"[LobbyManager] Master Client changed game speed to {speed}x");
    }

    private void UpdateSpeedUI()
    {
        if (lobbyStartButton == null || startBtnText == null) return;

        bool isMaster = PhotonNetwork.IsMasterClient;
        lobbyStartButton.interactable = isMaster;
        
        if (isMaster)
        {
            startBtnText.text = "SPIEL STARTEN";
            startBtnText.color = new Color(0.95f, 0.9f, 0.8f, 1f);
        }
        else
        {
            startBtnText.text = "Warte auf Host...";
            startBtnText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        int currentSpeed = 1;
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameSpeed", out object speedObj))
        {
            currentSpeed = System.Convert.ToInt32(speedObj);
        }

        for (int i = 1; i <= 3; i++)
        {
            GameObject btnGO = GameObject.Find($"SpeedBtn_{i}");
            if (btnGO != null)
            {
                Image btnImg = btnGO.GetComponent<Image>();
                Button btn = btnGO.GetComponent<Button>();
                
                btn.interactable = isMaster;

                if (i == currentSpeed)
                {
                    btnImg.color = new Color(1f, 0.85f, 0.3f, 1f); // golden highlight
                }
                else
                {
                    btnImg.color = isMaster ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.7f);
                }
            }
        }
    }

    private Sprite GetSpeedSprite(int speed)
    {
        if (speed == 1 && pfeil1 != null) return pfeil1;
        if (speed == 2 && pfeil2 != null) return pfeil2;
        if (speed == 3 && pfeil3 != null) return pfeil3;

        try
        {
            string fileName = $"pfeil{speed}.png";
            string fullPath = System.IO.Path.Combine(Application.dataPath, "Textures", fileName);
            if (System.IO.File.Exists(fullPath))
            {
                byte[] data = System.IO.File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(data);
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyManager] Failed to load speed sprite {speed} from disk: {ex.Message}");
        }

        return null;
    }

    private void OnLobbyStartButtonClicked()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.LoadLevel(SceneNames.GameScene);
        }
    }

    #endregion
}
