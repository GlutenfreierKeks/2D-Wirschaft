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
    private readonly List<Vector2> sailPath = new List<Vector2>();
    private int sailPathIndex = 0;


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

        // Reveal fog around ship (medium radius)
        var rev = gameObject.AddComponent<FogRevealer>();
        rev.radius = 7.0f;
        rev.isLocalPlayer = true;
    }


    private void Update()
    {
        // Continuously reveal explored fog around the ship's current position
        FogProjector.RegisterExploration(transform.position, 7f);

        if (State == ShipState.Sailing && hasSailTarget)
        {
            Vector2 pos2D = transform.position;
            Vector2 dir   = (sailTarget - pos2D);
            float dist    = dir.magnitude;

            if (dist < 0.12f)
            {
                transform.position = new Vector3(sailTarget.x, sailTarget.y, transform.position.z);
                
                sailPathIndex++;
                if (sailPathIndex < sailPath.Count)
                {
                    sailTarget = sailPath[sailPathIndex];
                }
                else
                {
                    State         = ShipState.Idle;
                    hasSailTarget = false;
                    UpdateStatusTag();
                    StartCoroutine(UnloadPassengers());
                }
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

        Vector2 start = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
        Vector2 dest = new Vector2(Mathf.Round(target.x), Mathf.Round(target.y));

        List<Vector2> path = FindWaterPath(start, dest);
        if (path.Count > 0)
        {
            sailPath.Clear();
            sailPath.AddRange(path);
            sailPathIndex = 0;
            sailTarget = sailPath[0];
            hasSailTarget = true;
            State = ShipState.Sailing;
            UpdateStatusTag();
        }
        else
        {
            NotificationManager.Instance?.Notify("ship_no_path", "Kein Seeweg dorthin gefunden! (Schiffe können nicht über Land fahren)", 5f);
        }
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

    /// <summary>Call one worker from the island the ship is currently at to walk to the ship.</summary>
    public int LoadNearbyWorkers(float radius = 40f)
    {
        if (State != ShipState.Idle) return 0;
        if (FreeSlotsCount <= 0) return 0;
        if (VillagerManager.Instance == null) return 0;

        // Find nearest land spot to determine the island the ship is currently docked at
        Vector2 landingSpot = FindNearestLandSpot();
        if (landingSpot.x < -9000f)
        {
            NotificationManager.Instance?.Notify("ship_no_island", "Das Schiff ist nicht nah genug an einer Insel!", 4f);
            return 0;
        }

        int shipIslandIndex = IslandManager.GetIslandIndexAt(landingSpot);
        if (shipIslandIndex < 0)
        {
            NotificationManager.Instance?.Notify("ship_no_island", "Keine Insel am Steg/Strand erkannt!", 4f);
            return 0;
        }

        // Find ONE free worker on the same island
        Villager bestWorker = null;
        float bestDist = float.MaxValue;

        var allVillagers = Object.FindObjectsByType<Villager>(FindObjectsSortMode.None);
        foreach (var v in allVillagers)
        {
            if (v == null || v.role != Villager.Role.Worker) continue;

            int workerIslandIndex = IslandManager.GetIslandIndexAt(v.transform.position);
            if (workerIslandIndex != shipIslandIndex) continue;

            float dist = Vector2.Distance(transform.position, v.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestWorker = v;
            }
        }

        if (bestWorker != null)
        {
            bestWorker.AssignToBoardShip(this);
            NotificationManager.Instance?.Notify("ship_workers_called", "Ein Bauarbeiter gerufen. Er läuft zum Schiff.", 4f);
            return 1;
        }

        NotificationManager.Instance?.Notify("ship_no_workers", "Kein freier Bauarbeiter auf dieser Insel gefunden!", 4f);
        return 0;
    }



    // ── Internal ──────────────────────────────────────────────────────────────

    private IEnumerator UnloadPassengers()
    {
        Vector2 landingSpot = FindNearestLandSpot();
        if (landingSpot.x < -9000f)
        {
            NotificationManager.Instance?.Notify("ship_unload_fail", "Aussteigen fehlgeschlagen: Keine Insel in der Nähe!", 5f);
            State = ShipState.Idle;
            UpdateStatusTag();
            yield break;
        }

        State = ShipState.Unloading;
        UpdateStatusTag();
        yield return new WaitForSeconds(loadUnloadTime);

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

        NotificationManager.Instance?.Notify("ship_unload", "Einheiten erfolgreich an Land abgesetzt!", 4f);
    }

    private Vector2 FindNearestLandSpot()
    {
        Vector2 pos = transform.position;
        // BFS to find nearest land cell within a reasonable distance (e.g. 8 tiles radius)
        Queue<Vector2> q = new Queue<Vector2>();
        HashSet<Vector2> visited = new HashSet<Vector2>();
        q.Enqueue(new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)));
        visited.Add(q.Peek());

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        while (q.Count > 0 && visited.Count < 200)
        {
            Vector2 cur = q.Dequeue();
            if (IslandManager.IsLand(cur)) return cur;
            foreach (var d in dirs)
            {
                Vector2 next = cur + d;
                if (visited.Add(next)) q.Enqueue(next);
            }
        }
        return new Vector2(-9999f, -9999f); // Sentinel indicating no land nearby
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

    // ── Water A* Pathfinding ──────────────────────────────────────────────────
    private static List<Vector2> FindWaterPath(Vector2 start, Vector2 destination)
    {
        List<Vector2> empty = new List<Vector2>();

        // Both start and destination must be water (i.e. not land)
        if (IslandManager.IsLand(start) || IslandManager.IsLand(destination))
        {
            return empty;
        }

        if (start == destination)
        {
            empty.Add(destination);
            return empty;
        }

        PriorityQueueWater frontier = new PriorityQueueWater();
        frontier.Enqueue(start, 0f);

        Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();
        Dictionary<Vector2, float> costSoFar = new Dictionary<Vector2, float>
        {
            [start] = 0f
        };

        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        int iterations = 0;
        int maxIterations = 8000;

        while (frontier.Count > 0 && iterations++ < maxIterations)
        {
            Vector2 current = frontier.Dequeue();
            if (current == destination)
            {
                return ReconstructWaterPath(cameFrom, destination);
            }

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 next = current + directions[i];

                // Water-only pathing: skip land
                if (IslandManager.IsLand(next))
                {
                    continue;
                }

                float newCost = costSoFar[current] + 1f;
                if (costSoFar.TryGetValue(next, out float existingCost) && newCost >= existingCost)
                {
                    continue;
                }

                costSoFar[next] = newCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, newCost + HeuristicWater(next, destination));
            }
        }

        return empty;
    }

    private static List<Vector2> ReconstructWaterPath(Dictionary<Vector2, Vector2> cameFrom, Vector2 destination)
    {
        List<Vector2> path = new List<Vector2> { destination };
        Vector2 current = destination;

        while (cameFrom.TryGetValue(current, out Vector2 previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        if (path.Count > 0)
        {
            path.RemoveAt(0);
        }

        return path;
    }

    private static float HeuristicWater(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private class PriorityQueueWater
    {
        private readonly List<KeyValuePair<Vector2, float>> elements = new List<KeyValuePair<Vector2, float>>();

        public int Count => elements.Count;

        public void Enqueue(Vector2 item, float priority)
        {
            elements.Add(new KeyValuePair<Vector2, float>(item, priority));
        }

        public Vector2 Dequeue()
        {
            int bestIndex = 0;
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i].Value < elements[bestIndex].Value)
                {
                    bestIndex = i;
                }
            }
            Vector2 bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }
}

