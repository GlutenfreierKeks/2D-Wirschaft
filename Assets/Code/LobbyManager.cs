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

    #endregion
}
