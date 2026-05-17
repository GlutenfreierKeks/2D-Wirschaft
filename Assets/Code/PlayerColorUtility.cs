using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public static class PlayerColorUtility
{
    private static readonly Color[] Palette =
    {
        new Color(0.25f, 0.75f, 1.00f),
        new Color(1.00f, 0.42f, 0.42f),
        new Color(0.35f, 0.92f, 0.55f),
        new Color(1.00f, 0.78f, 0.28f),
        new Color(0.78f, 0.44f, 1.00f),
        new Color(1.00f, 0.55f, 0.78f),
        new Color(0.45f, 0.90f, 0.90f),
        new Color(0.95f, 0.95f, 0.38f)
    };

    public static Color GetColorForActor(int actorNumber, bool isLocalTeam)
    {
        if (actorNumber > 0 && PhotonNetwork.InRoom)
        {
            Player[] players = PhotonNetwork.PlayerList;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].ActorNumber == actorNumber)
                {
                    return Palette[i % Palette.Length];
                }
            }
        }

        return isLocalTeam ? Palette[0] : Palette[1];
    }
}
