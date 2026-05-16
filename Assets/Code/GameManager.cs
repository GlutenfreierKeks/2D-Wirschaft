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
        if (IslandManager.Instance == null)
        {
            Debug.LogError("IslandManager instance not found! Cannot calculate island spawn position.");
            return;
        }

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

        // Pick an island based on the player index (so each player gets a different island if possible)
        Vector2 islandPos = IslandManager.Instance.GetIslandPosition(playerIndex);
        
        // Kamera auf die Insel bewegen (Z = -10 für die Kamera in 2D)
        Vector3 cameraPos = new Vector3(islandPos.x, islandPos.y, -10f);
        
        Debug.Log($"[GameManager] Spawning at Island {playerIndex}: {cameraPos}");
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = cameraPos;
            mainCam.transform.rotation = Quaternion.identity;
        }

        // WARNUNG BEHOBEN: Hier wird der Spieler nun auch wirklich im Netzwerk gespawnt!
        Vector3 playerPos = new Vector3(islandPos.x, islandPos.y, 0f);
        PhotonNetwork.Instantiate(playerPrefabName, playerPos, Quaternion.identity);
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
