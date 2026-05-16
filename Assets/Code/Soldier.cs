using UnityEngine;
using UnityEngine.InputSystem;

public enum Team
{
    Player,
    Enemy
}

public enum SoldierType
{
    Spear,
    Shield,
    Sword,
    Bow
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))] // Nötig um Klicks zu registrieren
public class Soldier : MonoBehaviour
{
    [Header("Welcher Soldat ist das?")]
    public SoldierType soldierType;

    [Header("Team Einstellungen")]
    public Team team = Team.Player;

    [Header("Bilder / Icons")]
    public Sprite spearSprite;
    public Sprite shieldSprite;
    public Sprite swordSprite;
    public Sprite bowSprite;

    // Das Ziel wird jetzt automatisch anhand des Teams ermittelt
    private string enemyTag;

    // Diese Werte werden nun automatisch durch den Code gesetzt
    [Header("Aktuelle Stats (Werden automatisch gesetzt)")]
    public float maxHealth = 100f;
    private float currentHealth;
    public float shield = 0f;
    public float damage;
    public float attackRange;
    public float attackCooldown;
    public float moveSpeed = 3f;

    private float lastAttackTime;
    private Transform currentTarget;

    private Vector2 randomWanderTarget;
    private float nextWanderTime;

    // UI Elemente für Anzeige
    private TextMesh nameText;
    private TextMesh healthText;
    private LineRenderer circleRenderer;

    // Befehls-Menü Variablen
    public bool holdPosition = false;
    private bool showMenu = false;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Lädt die Bilder automatisch im Unity Editor aus dem Ordner "Assets/textures"
        if (spearSprite == null) spearSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/textures/speersoldat.png");
        if (shieldSprite == null) shieldSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/textures/schildsoldat.png");
        if (swordSprite == null) swordSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/textures/schwertkämpfer.png");
        if (bowSprite == null) bowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/textures/bogensoldat.png");

    }
#endif

    private void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        // Tag und Ziel automatisch setzen
        gameObject.tag = (team == Team.Player) ? "Player" : "Enemy";
        enemyTag = (team == Team.Player) ? "Enemy" : "Player";

        // Stats basierend auf dem ausgewählten Typen setzen
        switch (soldierType)
        {
            case SoldierType.Spear:
                maxHealth = 100f;
                damage = 25f;
                attackRange = 10f;
                attackCooldown = 2f;
                shield = 0f;
                if (sr != null && spearSprite != null) sr.sprite = spearSprite;
                break;

            case SoldierType.Shield:
                maxHealth = 100f;
                damage = 35f;
                attackRange = 1f;
                attackCooldown = 1.5f;
                shield = 50f;
                if (sr != null && shieldSprite != null) sr.sprite = shieldSprite;
                break;

            case SoldierType.Sword:
                maxHealth = 100f;
                damage = 30f;
                attackRange = 2f;
                attackCooldown = 1f;
                shield = 0f;
                if (sr != null && swordSprite != null) sr.sprite = swordSprite;
                break;

            case SoldierType.Bow:
                maxHealth = 100f;
                damage = 20f;
                attackRange = 15f;
                attackCooldown = 3f;
                shield = 0f;
                if (sr != null && bowSprite != null) sr.sprite = bowSprite;
                break;
        }
    }

    private void Start()
    {
        currentHealth = maxHealth;
        SetNewWanderTarget();

        // Erstelle direkt beim Starten die Text-Anzeigen und den Kreis!
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        // 1. Name Tag (Art des Soldaten) Text über dem Soldaten
        GameObject nameObj = new GameObject("NameTag");
        nameObj.transform.SetParent(this.transform);
        nameObj.transform.localPosition = new Vector3(0, 1.2f, 0); // Leicht über dem Kopf
        nameText = nameObj.AddComponent<TextMesh>();
        nameText.text = soldierType.ToString();
        nameText.characterSize = 0.1f;
        nameText.fontSize = 40;
        nameText.anchor = TextAnchor.MiddleCenter;
        nameText.alignment = TextAlignment.Center;
        nameText.color = Color.white;
        nameText.GetComponent<MeshRenderer>().sortingOrder = 10; // Damit es im 2D Raum sichtbar ist

        // 2. Lebensanzeige unter dem Namen
        GameObject hpObj = new GameObject("HealthTag");
        hpObj.transform.SetParent(this.transform);
        hpObj.transform.localPosition = new Vector3(0, 0.8f, 0);
        healthText = hpObj.AddComponent<TextMesh>();
        healthText.characterSize = 0.1f;
        healthText.fontSize = 35;
        healthText.anchor = TextAnchor.MiddleCenter;
        healthText.alignment = TextAlignment.Center;
        healthText.GetComponent<MeshRenderer>().sortingOrder = 10;
        UpdateHealthText();

        // 3. Angriffsradius Kreis zeichnen (LineRenderer)
        circleRenderer = gameObject.AddComponent<LineRenderer>();
        circleRenderer.startWidth = 0.15f; // Etwas dicker gemacht, damit man ihn besser sieht
        circleRenderer.endWidth = 0.15f;
        circleRenderer.useWorldSpace = false; // Der Kreis bewegt sich mit dem Soldaten mit
        circleRenderer.loop = true; // Schließt den Kreis am Ende
        
        // Material für den Kreis (Standard 2D Material)
        Material lineMat = new Material(Shader.Find("Sprites/Default"));
        circleRenderer.material = lineMat;
        
        // Blau für Spieler, Rot für Feinde
        Color circleColor = (team == Team.Player) ? new Color(0, 0.5f, 1f, 0.4f) : new Color(1, 0, 0, 0.4f);
        circleRenderer.startColor = circleColor;
        circleRenderer.endColor = circleColor;
        circleRenderer.sortingOrder = 5; // Auf +5 gesetzt, damit es nicht vom Hintergrundbild verdeckt wird! (Der Name hat 10)

        int segments = 50; // Anzahl der Ecken des Kreises (50 sieht sehr rund aus)
        circleRenderer.positionCount = segments;
        float angle = 0f;

        for (int i = 0; i < segments; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * attackRange;
            float y = Mathf.Cos(Mathf.Deg2Rad * angle) * attackRange;

            circleRenderer.SetPosition(i, new Vector3(x, y, 0));
            angle += (360f / segments);
        }
    }

    private void UpdateHealthText()
    {
        if (healthText != null)
        {
            if (shield > 0)
            {
                healthText.text = $"HP: {currentHealth} | Schild: {shield}";
                healthText.color = Color.cyan; // Blau wenn Schild aktiv ist
            }
            else
            {
                healthText.text = $"HP: {currentHealth}";
                if (currentHealth > maxHealth * 0.5f) healthText.color = Color.green;
                else if (currentHealth > maxHealth * 0.25f) healthText.color = Color.yellow;
                else healthText.color = Color.red;
            }
        }
    }

    private void Update()
    {
        // Prüfen ob der Spieler mit Rechtsklick auf diesen Soldaten klickt (Neues Input System)
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Collider2D hit = Physics2D.OverlapPoint(mousePos);
            
            // Menü öffnet sich NUR, wenn man den Soldaten trifft UND er zum eigenen Team gehört!
            if (hit != null && hit.gameObject == this.gameObject && team == Team.Player)
            {
                showMenu = !showMenu;
            }
        }

        FindNearestEnemy();

        if (currentTarget != null)
        {
            float distance = Vector2.Distance(transform.position, currentTarget.position);

            if (distance <= attackRange)
            {
                // Gegner ist im Angriffsradius -> Angreifen
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    Attack();
                    lastAttackTime = Time.time;
                }
            }
            else if (!holdPosition)
            {
                // Gegner ist nicht im Radius und er soll nicht stehenbleiben -> Zum Gegner bewegen
                MoveTowards(currentTarget.position);
            }
        }
        else if (!holdPosition)
        {
            // Kein Gegner auf der Karte -> Zufällig bewegen (passiv)
            WanderRandomly();
        }
    }

    private void FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            // Sich selbst nicht als Ziel auswählen
            if (enemy == this.gameObject) continue;

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }
        
        currentTarget = closestEnemy;
    }

    private void MoveTowards(Vector2 targetPos)
    {
        transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
    }

    private void WanderRandomly()
    {
        if (Time.time >= nextWanderTime)
        {
            SetNewWanderTarget();
            // Nächster Richtungswechsel in 2 bis 5 Sekunden
            nextWanderTime = Time.time + Random.Range(2f, 5f);
        }

        MoveTowards(randomWanderTarget);

        if (Vector2.Distance(transform.position, randomWanderTarget) < 0.1f)
        {
            SetNewWanderTarget();
        }
    }

    private void SetNewWanderTarget()
    {
        // Ein zufälliger Punkt im Umkreis von 3 Metern
        randomWanderTarget = (Vector2)transform.position + new Vector2(Random.Range(-3f, 3f), Random.Range(-3f, 3f));
    }

    private void Attack()
    {
        Soldier enemySoldier = currentTarget.GetComponent<Soldier>();
        if (enemySoldier != null)
        {
            enemySoldier.TakeDamage(damage);
            Debug.Log(gameObject.name + " (" + soldierType + ") greift an für " + damage + " Schaden!");
        }
    }

    public void TakeDamage(float amount)
    {
        if (shield > 0)
        {
            shield -= amount;
            if (shield < 0)
            {
                currentHealth += shield;
                shield = 0;
            }
        }
        else
        {
            currentHealth -= amount;
        }

        UpdateHealthText(); // Aktualisiert den Text, sobald Schaden genommen wurde!

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log(gameObject.name + " ist gestorben!");
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (showMenu)
        {
            // Position vom Soldaten auf den Bildschirm umrechnen
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            float guiY = Screen.height - screenPos.y; 
            
            // Ein kleines Menü über dem Soldaten zeichnen
            GUILayout.BeginArea(new Rect(screenPos.x - 60, guiY - 100, 120, 100), GUI.skin.box);
            GUILayout.Label("Befehle", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            
            if (holdPosition)
            {
                if (GUILayout.Button("Weitergehen"))
                {
                    holdPosition = false;
                    showMenu = false;
                }
            }
            else
            {
                if (GUILayout.Button("Stehenbleiben"))
                {
                    holdPosition = true;
                    showMenu = false;
                }
            }

            if (GUILayout.Button("Schließen"))
            {
                showMenu = false;
            }
            
            GUILayout.EndArea();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
