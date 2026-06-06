using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum ShipState { Idle, Building, Loading, Sailing, Unloading }

/// <summary>
/// Represents a ship unit that can carry soldiers and workers across water to other islands.
/// Ships are built at a Steg (pier) and can be selected to load/unload passengers and set sail.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class Ship : MonoBehaviour
{
    // ── Static registry ────────────────────────────────────────────────────────
    public static readonly List<Ship> AllShips = new List<Ship>();

    // ── Settings ──────────────────────────────────────────────────────────────
    [Header("Ship Settings")]
    public int capacity = 8;
    public float sailSpeed = 2.5f;
    public float loadUnloadTime = 2f;

    // ── State ─────────────────────────────────────────────────────────────────
    public ShipState State { get; private set; } = ShipState.Idle;
    public bool IsSelected { get; private set; } = false;

    // ── Passengers ────────────────────────────────────────────────────────────
    private readonly List<Soldier> soldierPassengers   = new List<Soldier>();
    private readonly List<Villager> villagerPassengers = new List<Villager>();

    public int PassengerCount   => soldierPassengers.Count + villagerPassengers.Count;
    public int FreeSlotsCount   => capacity - PassengerCount;

    // ── Navigation ────────────────────────────────────────────────────────────
    private Vector2 sailTarget;
    private bool hasSailTarget = false;

    // ── Visuals ───────────────────────────────────────────────────────────────
    private SpriteRenderer sr;
    private TextMesh nameTag;
    private TextMesh statusTag;
    private LineRenderer selectionCircle;

    // ── Owner ─────────────────────────────────────────────────────────────────
    public int ownerActorNumber = 0;

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (!AllShips.Contains(this)) AllShips.Add(this);
    }

    private void OnDisable()
    {
        AllShips.Remove(this);
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 22;
        BuildShipSprite();
    }

    private void Start()
    {
        SetupVisuals();
        SetSelected(false);
        UpdateStatusTag();
    }

    private void Update()
    {
        if (State == ShipState.Sailing && hasSailTarget)
        {
            Vector2 pos2D = transform.position;
            Vector2 dir   = (sailTarget - pos2D);
            float dist    = dir.magnitude;

            if (dist < 0.12f)
            {
                transform.position = new Vector3(sailTarget.x, sailTarget.y, transform.position.z);
                State         = ShipState.Idle;
                hasSailTarget = false;
                UpdateStatusTag();
                StartCoroutine(UnloadPassengers());
                return;
            }

            // Rotate ship to face direction
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 5f);

            Vector2 next = Vector2.MoveTowards(pos2D, sailTarget, sailSpeed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        UpdateStatusTag();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectionCircle != null) selectionCircle.enabled = selected;
    }

    /// <summary>Load a soldier into the ship if there is space.</summary>
    public bool LoadSoldier(Soldier s)
    {
        if (s == null || FreeSlotsCount <= 0 || State != ShipState.Idle) return false;
        soldierPassengers.Add(s);
        s.gameObject.SetActive(false); // hide while on ship
        NotificationManager.Instance?.Notify("ship_load", $"Soldat eingeschifft. ({PassengerCount}/{capacity})", 3f);
        UpdateStatusTag();
        return true;
    }

    /// <summary>Load a villager/worker into the ship if there is space.</summary>
    public bool LoadVillager(Villager v)
    {
        if (v == null || FreeSlotsCount <= 0 || State != ShipState.Idle) return false;
        villagerPassengers.Add(v);
        v.gameObject.SetActive(false); // hide while on ship
        NotificationManager.Instance?.Notify("ship_load", $"Bauarbeiter eingeschifft. ({PassengerCount}/{capacity})", 3f);
        UpdateStatusTag();
        return true;
    }

    /// <summary>Order the ship to sail to the given world position.</summary>
    public void SailTo(Vector2 target)
    {
        if (State == ShipState.Sailing || State == ShipState.Building) return;
        sailTarget    = target;
        hasSailTarget = true;
        State         = ShipState.Sailing;
        UpdateStatusTag();
    }

    /// <summary>Unload all passengers at the current position immediately.</summary>
    public void UnloadAll()
    {
        if (State == ShipState.Sailing || State == ShipState.Building) return;
        StartCoroutine(UnloadPassengers());
    }

    // ── Loading helpers ───────────────────────────────────────────────────────

    /// <summary>Load nearby soldiers (within radius) into the ship.</summary>
    public int LoadNearbySoldiers(float radius = 5f)
    {
        if (State != ShipState.Idle) return 0;
        int loaded = 0;
        var toLoad = new List<Soldier>(Soldier.ActiveSoldiers);
        foreach (var s in toLoad)
        {
            if (FreeSlotsCount <= 0) break;
            if (s == null || !s.IsOwnedByLocalPlayer) continue;
            if (Vector2.Distance(transform.position, s.transform.position) <= radius)
            {
                if (LoadSoldier(s)) loaded++;
            }
        }
        return loaded;
    }

    /// <summary>Load nearby workers (Villager with Worker role) into the ship.</summary>
    public int LoadNearbyWorkers(float radius = 5f)
    {
        if (State != ShipState.Idle) return 0;
        int loaded = 0;
        if (VillagerManager.Instance == null) return 0;

        var allVillagers = Object.FindObjectsByType<Villager>(FindObjectsSortMode.None);
        foreach (var v in allVillagers)
        {
            if (FreeSlotsCount <= 0) break;
            if (v == null || v.role != Villager.Role.Worker) continue;
            if (Vector2.Distance(transform.position, v.transform.position) <= radius)
            {
                if (LoadVillager(v)) loaded++;
            }
        }
        return loaded;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private IEnumerator UnloadPassengers()
    {
        State = ShipState.Unloading;
        UpdateStatusTag();
        yield return new WaitForSeconds(loadUnloadTime);

        Vector2 landingSpot = FindNearestLandSpot();

        // Unload soldiers
        foreach (var s in soldierPassengers)
        {
            if (s == null) continue;
            s.transform.position = new Vector3(
                landingSpot.x + Random.Range(-1.5f, 1.5f),
                landingSpot.y + Random.Range(-1.5f, 1.5f),
                s.transform.position.z);
            s.gameObject.SetActive(true);
        }
        soldierPassengers.Clear();

        // Unload villagers
        foreach (var v in villagerPassengers)
        {
            if (v == null) continue;
            v.transform.position = new Vector3(
                landingSpot.x + Random.Range(-1.5f, 1.5f),
                landingSpot.y + Random.Range(-1.5f, 1.5f),
                v.transform.position.z);
            v.gameObject.SetActive(true);
        }
        villagerPassengers.Clear();

        State = ShipState.Idle;
        transform.rotation = Quaternion.identity;
        UpdateStatusTag();

        int count = soldierPassengers.Count + villagerPassengers.Count;
        NotificationManager.Instance?.Notify("ship_unload", "Einheiten von Bord gegangen!", 4f);
    }

    private Vector2 FindNearestLandSpot()
    {
        Vector2 pos = transform.position;
        // BFS to find nearest land cell
        Queue<Vector2> q = new Queue<Vector2>();
        HashSet<Vector2> visited = new HashSet<Vector2>();
        q.Enqueue(new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)));
        visited.Add(q.Peek());

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        while (q.Count > 0 && visited.Count < 500)
        {
            Vector2 cur = q.Dequeue();
            if (IslandManager.IsLand(cur)) return cur;
            foreach (var d in dirs)
            {
                Vector2 next = cur + d;
                if (visited.Add(next)) q.Enqueue(next);
            }
        }
        return pos;
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    private void BuildShipSprite()
    {
        // Build a simple ship sprite procedurally (hull shape)
        int w = 24, h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color clear  = Color.clear;
        Color hull   = new Color(0.45f, 0.28f, 0.10f);    // brown hull
        Color deck   = new Color(0.60f, 0.40f, 0.15f);    // lighter deck
        Color mast   = new Color(0.30f, 0.18f, 0.06f);    // dark mast
        Color sail   = new Color(0.95f, 0.92f, 0.80f);    // cream sail
        Color sailAc = new Color(0.80f, 0.30f, 0.20f);    // red accent stripe

        // Fill transparent
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, clear);

        // Hull (trapezoid bottom half)
        for (int y = 0; y < 12; y++)
        {
            int margin = (int)(y * 0.4f);
            for (int x = margin; x < w - margin; x++)
                tex.SetPixel(x, y, hull);
        }

        // Deck (flat top of hull)
        for (int y = 12; y < 14; y++)
            for (int x = 2; x < w - 2; x++)
                tex.SetPixel(x, y, deck);

        // Mast (center vertical bar)
        for (int y = 13; y < 28; y++)
            tex.SetPixel(w / 2, y, mast);
        tex.SetPixel(w / 2 - 1, 13, mast);
        tex.SetPixel(w / 2 + 1, 13, mast);

        // Sail
        for (int y = 16; y < 26; y++)
        {
            int halfW = (int)(5f * Mathf.Sin(Mathf.PI * (y - 16f) / 10f));
            for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
            {
                Color c = (y == 20) ? sailAc : sail;
                if (x >= 0 && x < w) tex.SetPixel(x, y, c);
            }
        }

        tex.filterMode = FilterMode.Point;
        tex.Apply();

        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.25f), 16f);
        sr.sprite = s;
    }

    private void SetupVisuals()
    {
        // Name tag
        var nameGO = new GameObject("ShipNameTag");
        nameGO.transform.SetParent(transform);
        nameGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        nameTag = nameGO.AddComponent<TextMesh>();
        nameTag.text = "⛵ Schiff";
        nameTag.characterSize = 0.1f;
        nameTag.fontSize = 36;
        nameTag.anchor = TextAnchor.MiddleCenter;
        nameTag.alignment = TextAlignment.Center;
        nameTag.color = new Color(1f, 0.9f, 0.6f);
        nameTag.GetComponent<MeshRenderer>().sortingOrder = 12;

        // Status tag
        var statusGO = new GameObject("ShipStatusTag");
        statusGO.transform.SetParent(transform);
        statusGO.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        statusTag = statusGO.AddComponent<TextMesh>();
        statusTag.characterSize = 0.09f;
        statusTag.fontSize = 28;
        statusTag.anchor = TextAnchor.MiddleCenter;
        statusTag.alignment = TextAlignment.Center;
        statusTag.GetComponent<MeshRenderer>().sortingOrder = 12;

        // Selection circle
        selectionCircle = gameObject.GetComponent<LineRenderer>();
        if (selectionCircle == null) selectionCircle = gameObject.AddComponent<LineRenderer>();
        selectionCircle.startWidth = 0.08f;
        selectionCircle.endWidth = 0.08f;
        selectionCircle.useWorldSpace = false;
        selectionCircle.loop = true;
        selectionCircle.material = new Material(Shader.Find("Sprites/Default"));
        selectionCircle.startColor = new Color(0.3f, 0.8f, 1f, 0.6f);
        selectionCircle.endColor = selectionCircle.startColor;
        selectionCircle.sortingOrder = 5;
        selectionCircle.positionCount = 36;
        float r = 1.0f;
        for (int i = 0; i < 36; i++)
        {
            float a = i * Mathf.PI * 2f / 36f;
            selectionCircle.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
    }

    private void UpdateStatusTag()
    {
        if (statusTag == null) return;
        switch (State)
        {
            case ShipState.Idle:
                statusTag.text = $"Bereit · {PassengerCount}/{capacity}";
                statusTag.color = new Color(0.6f, 1f, 0.6f);
                break;
            case ShipState.Building:
                statusTag.text = "⚙ Im Bau...";
                statusTag.color = new Color(1f, 0.8f, 0.3f);
                break;
            case ShipState.Sailing:
                statusTag.text = $"⛵ Segelt... · {PassengerCount} Bord";
                statusTag.color = new Color(0.4f, 0.8f, 1f);
                break;
            case ShipState.Unloading:
                statusTag.text = "↧ Entlädt...";
                statusTag.color = new Color(1f, 0.7f, 0.3f);
                break;
        }
    }

    public string GetStatusText()
    {
        return State switch
        {
            ShipState.Idle      => $"Bereit ({PassengerCount}/{capacity} Passagiere)",
            ShipState.Building  => "Im Bau...",
            ShipState.Sailing   => $"Segelt... ({PassengerCount} an Bord)",
            ShipState.Unloading => "Entlädt Passagiere...",
            _                   => "Unbekannt"
        };
    }
}
