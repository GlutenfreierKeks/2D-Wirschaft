using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BuildingInstance))]
public class ArcherTower : MonoBehaviour
{
    public int slots = 1;
    public int stationed = 0;
    public float range = 15f;
    public float damage = 20f;
    public float cooldown = 3f;
    public Team team = Team.Player;

    private float lastShot = 0f;

    private void Start()
    {
        // Ensure team matches building ownership if possible
        BuildingInstance bi = GetComponent<BuildingInstance>();
        if (bi != null)
        {
            team = bi.isLocal ? Team.Player : Team.Enemy;
        }
    }

    public bool TryStationArcher()
    {
        if (stationed < slots)
        {
            stationed++;
            return true;
        }
        return false;
    }

    public bool RemoveStationedArcher()
    {
        if (stationed > 0)
        {
            stationed--;
            return true;
        }
        return false;
    }

    private void Update()
    {
        if (stationed <= 0) return;

        if (lastShot > 0f) lastShot -= Time.deltaTime;

        if (lastShot > 0f) return;

        Soldier target = FindTarget();
        if (target != null)
        {
            ArrowProjectile.Spawn(transform.position + new Vector3(0f, 0.5f, 0f), target.transform.position);
            target.TakeDamage(damage);
            lastShot = cooldown;
        }
    }

    private Soldier FindTarget()
    {
        float closest = float.MaxValue;
        Soldier best = null;
        for (int i = 0; i < Soldier.ActiveSoldiers.Count; i++)
        {
            Soldier s = Soldier.ActiveSoldiers[i];
            if (s == null) continue;
            if (!IsHostileTo(s)) continue;

            float d = Vector2.Distance(transform.position, s.transform.position);
            if (d <= range && d < closest)
            {
                closest = d;
                best = s;
            }
        }
        return best;
    }

    private bool IsHostileTo(Soldier other)
    {
        if (other == null) return false;
        if (team == Team.Player && other.team == Team.Player) return false;
        if (team == Team.Enemy && other.team == Team.Enemy) return false;
        // If both have owner actor numbers, compare
        if (other.ownerActorNumber > 0 && team == Team.Player)
        {
            return other.ownerActorNumber != 0;
        }
        return other.team != team;
    }
}
