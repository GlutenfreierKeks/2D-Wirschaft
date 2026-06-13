using Photon.Pun;
using UnityEngine;

public class PlayerCombatHealth : MonoBehaviourPun
{
    public int maxHP = 120;
    public int currentHP = 120;

    private void Awake()
    {
        currentHP = Mathf.Max(1, maxHP);
    }

    public bool IsHostileTo(bool localBuilding)
    {
        if (photonView == null) return false;
        return localBuilding ? !photonView.IsMine : photonView.IsMine;
    }

    public void TakeDamage(int damage)
    {
        currentHP -= Mathf.Max(0, damage);
        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }
    }

    private void Die()
    {
        gameObject.SetActive(false);
        NotificationManager.Instance?.Notify("player_defeated", "Ein Spieler wurde vom Turm ausgeschaltet.", 6f);
    }
}
