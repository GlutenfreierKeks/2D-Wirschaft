using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Handles the main menu logic including connecting to Photon, setting player names, 
/// and creating or joining rooms.
/// Scene: StartMenuScene
/// </summary>
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

    private const string PLAYER_NAME_PREF_KEY = "PlayerName";
    private readonly byte[] maxPlayersOptions = { 2, 4, 6, 8 };

    private void Start()
    {
        // Disable interaction until connected to Master Server
        SetUIInteractable(false);

        // Load saved player name if available
        if (PlayerPrefs.HasKey(PLAYER_NAME_PREF_KEY))
        {
            playerNameInput.text = PlayerPrefs.GetString(PLAYER_NAME_PREF_KEY);
        }

        statusText.text = "Connecting to Photon...";

        // Connect to Photon Master Server using settings defined in PhotonServerSettings
        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>
    /// Called when the client successfully connects to the Photon Master Server.
    /// </summary>
    public override void OnConnectedToMaster()
    {
        statusText.text = "Connected to Master Server.";
        SetUIInteractable(true);
    }

    /// <summary>
    /// Enables or disables the core interactable UI elements.
    /// </summary>
    private void SetUIInteractable(bool isInteractable)
    {
        if (createRoomButton != null) createRoomButton.interactable = isInteractable;
        if (joinRandomButton != null) joinRandomButton.interactable = isInteractable;
        if (joinByNameButton != null) joinByNameButton.interactable = isInteractable;
        if (playerNameInput != null) playerNameInput.interactable = isInteractable;
        if (roomNameInput != null) roomNameInput.interactable = isInteractable;
        if (maxPlayersDropdown != null) maxPlayersDropdown.interactable = isInteractable;
        if (testLobbyButton != null) testLobbyButton.interactable = isInteractable;
    }

    /// <summary>
    /// Checks and applies the player's name before attempting any room operations.
    /// </summary>
    private bool SetupPlayerName()
    {
        string pName = playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(pName))
        {
            statusText.text = "Error: Please enter a valid display name.";
            return false;
        }

        PhotonNetwork.LocalPlayer.NickName = pName;
        PlayerPrefs.SetString(PLAYER_NAME_PREF_KEY, pName);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>
    /// Attempt to create a room with the specified name and settings.
    /// Linked to the "CREATE ROOM" button.
    /// </summary>
    public void OnCreateRoomButtonClicked()
    {
        if (!SetupPlayerName()) return;

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "Error: Please enter a room name.";
            return;
        }

        byte selectedMaxPlayers = maxPlayersOptions[maxPlayersDropdown.value];

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = selectedMaxPlayers,
            IsOpen = true,
            IsVisible = true
        };

        statusText.text = $"Creating Room '{roomName}'...";
        SetUIInteractable(false);
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    /// <summary>
    /// Attempt to join a specific room by its name.
    /// Linked to the "JOIN BY NAME" button.
    /// </summary>
    public void OnJoinByNameButtonClicked()
    {
        if (!SetupPlayerName()) return;

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "Error: Please enter a room name to join.";
            return;
        }

        statusText.text = $"Joining Room '{roomName}'...";
        SetUIInteractable(false);
        PhotonNetwork.JoinRoom(roomName);
    }

    /// <summary>
    /// Attempt to join any random open room.
    /// Linked to the "JOIN ROOM" button.
    /// </summary>
    public void OnJoinRandomButtonClicked()
    {
        if (!SetupPlayerName()) return;

        statusText.text = "Joining Random Room...";
        SetUIInteractable(false);
        PhotonNetwork.JoinRandomRoom();
    }

    /// <summary>
    /// Creates a room in 'Test Mode' with simulated players.
    /// </summary>
    public void OnTestLobbyButtonClicked()
    {
        if (!SetupPlayerName()) return;

        string roomName = "TestRoom_" + Random.Range(1000, 9999);
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 4,
            IsOpen = true,
            IsVisible = false, // Keep test rooms hidden from global list
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { { "TestMode", true } },
            CustomRoomPropertiesForLobby = new string[] { "TestMode" }
        };

        statusText.text = "Starting Test Lobby...";
        SetUIInteractable(false);
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    /// <summary>
    /// Callback when successfully joining a room.
    /// </summary>
    public override void OnJoinedRoom()
    {
        statusText.text = "Successfully joined room! Loading Lobby...";
        // Use Photon's LoadLevel to ensure networked scene loading if needed
        PhotonNetwork.LoadLevel(SceneNames.LobbyScene);
    }

    /// <summary>
    /// Callback when joining a room fails (e.g., full or doesn't exist).
    /// </summary>
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        statusText.text = $"Failed to join room: {message}";
        SetUIInteractable(true);
    }

    /// <summary>
    /// Callback when joining a random room fails (e.g., no rooms available).
    /// </summary>
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        statusText.text = "No available rooms found. Create one instead.";
        SetUIInteractable(true);
    }

    /// <summary>
    /// Callback when creating a room fails (e.g., name already taken).
    /// </summary>
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        statusText.text = $"Failed to create room: {message}";
        SetUIInteractable(true);
    }
}
