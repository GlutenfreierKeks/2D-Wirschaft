using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Manages the main gameplay loop and player spawning.
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI gameStatusText;

    [Header("Player Settings")]
    [SerializeField] private string playerPrefabName = "Player"; // Prefab must be in a 'Resources' folder

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
            UpdateStatusText();
        }
        else
        {
            if (gameStatusText != null) gameStatusText.text = "Game Started! (Offline Mode)";
            Debug.LogWarning("GameManager loaded, but client is not in a Photon room.");
        }
    }

    private void SpawnPlayer()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager instance not found! Cannot calculate spawn position.");
            return;
        }

        // Check if we are in Test Mode
        bool isTestMode = false;
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("TestMode", out object testModeValue))
        {
            isTestMode = (bool)testModeValue;
        }

        // Get player index and total players for distribution
        int playerIndex = 0;
        Player[] players = PhotonNetwork.PlayerList;
        int realPlayerCount = players.Length;
        int simulatedTotalPlayers = isTestMode ? Mathf.Max(realPlayerCount, 3) : realPlayerCount;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].IsLocal)
            {
                playerIndex = i;
                break;
            }
        }

        Vector3 spawnPos = GridManager.Instance.GetSpawnPosition(playerIndex, simulatedTotalPlayers);
        
        Debug.Log($"Positioning camera for player {playerIndex + 1}/{simulatedTotalPlayers} at {spawnPos}");
        
        // Move the camera to the spawn position
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(spawnPos.x, spawnPos.y, -10f);
        }
    }

    private void UpdateStatusText()
    {
        if (gameStatusText != null)
        {
            gameStatusText.text = $"Game Started! Players: {PhotonNetwork.CurrentRoom.PlayerCount}";
        }
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
}
