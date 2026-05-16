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
                    fr.radius = 80f; // Large enough to cover the spawn island
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
