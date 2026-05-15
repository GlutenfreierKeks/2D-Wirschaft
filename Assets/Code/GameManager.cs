using UnityEngine;
using TMPro;
using Photon.Pun;

/// <summary>
/// A placeholder system representing the main gameplay loop.
/// Scene: GameScene
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI gameStatusText;

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            Debug.Log($"Game Started with {playerCount} players");

            if (gameStatusText != null)
            {
                gameStatusText.text = $"Game Started! Players: {playerCount}";
            }
        }
        else
        {
            if (gameStatusText != null) gameStatusText.text = "Game Started! (Offline Mode)";
            Debug.LogWarning("GameManager loaded, but client is not in a Photon room.");
        }
    }

    /// <summary>
    /// Leaves the game, disconnects from Photon, and returns to the Main Menu.
    /// </summary>
    public void ReturnToMenu()
    {
        if (PhotonNetwork.IsConnected)
        {
            // Disconnecting triggers OnDisconnected() locally.
            PhotonNetwork.Disconnect();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.StartMenuScene);
        }
    }

    /// <summary>
    /// Callback triggered locally when the client finishes disconnecting from Photon.
    /// </summary>
    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        Debug.Log($"Disconnected from Photon. Cause: {cause}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.StartMenuScene);
    }
}
