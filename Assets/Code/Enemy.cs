using UnityEngine;
using System;

[DisallowMultipleComponent]
public class Enemy : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------
    /// <summary>Fired when this enemy dies. Passes a reference to itself.</summary>
    public event Action<Enemy> OnDeath;

    /// <summary>Fired whenever health changes. Args: (currentHealth, maxHealth).</summary>
    public event Action<float, float> OnHealthChanged;

    // Spawner back-reference (set via Initialize)
    private EnemySpawner spawner;

    // -----------------------------------------------------------------------
    // Stats
    // -----------------------------------------------------------------------
    [Header("Health")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float currentHealth;

    [Header("Movement Stats")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Attack Stats")]
    [SerializeField] private float attackDamage = 5f;
    [SerializeField] private float attackInterval = 1f;
    [SerializeField] private float attackModeDistance = 3f;

    [Header("Defense")]
    [SerializeField] private float armor = 0f;          // Flat damage reduction
    [SerializeField][Range(0f, 1f)] private float damageResistance = 0f; // 0 = no resistance, 1 = immune

    [Header("Reward")]
    [SerializeField] private int experienceReward = 10;
    [SerializeField] private int goldReward = 5;

    [Header("State")]
    [SerializeField] private bool isAlive = true;

    // -----------------------------------------------------------------------
    // Public read-only properties
    // -----------------------------------------------------------------------
    public float MaxHealth        => maxHealth;
    public float CurrentHealth    => currentHealth;
    public float MoveSpeed        => moveSpeed;
    public float AttackDamage     => attackDamage;
    public float AttackInterval   => attackInterval;
    public float AttackModeDistance => attackModeDistance;
    public float Armor            => armor;
    public float DamageResistance => damageResistance;
    public int   ExperienceReward => experienceReward;
    public int   GoldReward       => goldReward;
    public bool  IsAlive          => isAlive;
    public bool  IsDead           => !isAlive;

    /// <summary>Health as a 0-1 fraction, safe to use for UI bars.</summary>
    public float HealthFraction => maxHealth > 0f ? currentHealth / maxHealth : 0f;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
    {
        currentHealth = maxHealth;
        isAlive = true;
    }

    // -----------------------------------------------------------------------
    // Spawner initialisation (called by EnemySpawner after Instantiate)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called by EnemySpawner right after spawning to wire up the back-reference.
    /// <paramref name="speed"/> overrides the serialised MoveSpeed if > 0.
    /// </summary>
    public void Initialize(Vector2 target, float speed, EnemySpawner parentSpawner)
    {
        spawner = parentSpawner;
        if (speed > 0f) moveSpeed = speed;
        // target is now handled by EnemyMovement (it finds Core by tag automatically)
    }

    private void OnDestroy()
    {
        if (spawner != null)
            spawner.OnEnemyDestroyed();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Apply damage to the enemy, respecting armor and resistance.</summary>
    public void TakeDamage(float rawDamage)
    {
        if (IsDead) return;

        float mitigated = Mathf.Max(0f, rawDamage - armor);          // Flat reduction
        float final     = mitigated * (1f - damageResistance);        // Percentage resistance
        final           = Mathf.Max(0f, final);

        currentHealth = Mathf.Clamp(currentHealth - final, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>Restore health to the enemy (will not exceed maxHealth).</summary>
    public void Heal(float amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>Instantly kill the enemy, triggering the death sequence.</summary>
    public void Kill()
    {
        if (IsDead) return;
        currentHealth = 0f;
        Die();
    }

    /// <summary>Change move speed at runtime (e.g. slow effects).</summary>
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0f, speed);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------
    private void Die()
    {
        isAlive = false;
        OnDeath?.Invoke(this);
        OnHealthChanged?.Invoke(0f, maxHealth);

        // Disable physics to avoid lingering collisions
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Destroy the GameObject (extend delay if you want a death animation)
        Destroy(gameObject, 0.1f);
    }
}
