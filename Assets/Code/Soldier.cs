using UnityEngine;

public enum SoldierType
{
    Spear,
    Shield,
    Sword,
    Bow
}

public class Soldier : MonoBehaviour
{
    [Header("Welcher Soldat ist das?")]
    public SoldierType soldierType;

    [Header("Bilder / Icons")]
    public Sprite spearSprite;
    public Sprite shieldSprite;
    public Sprite swordSprite;
    public Sprite bowSprite;

    [Header("Zieleinstellungen")]
    public string enemyTag = "Enemy";

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
    }

    private void Update()
    {
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
            else
            {
                // Gegner ist nicht im Radius -> Zum Gegner bewegen
                MoveTowards(currentTarget.position);
            }
        }
        else
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
