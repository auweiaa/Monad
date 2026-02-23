using UnityEngine;

/// <summary>
/// Simple example projectile:
/// - Activated by a tower via ITowerProjectile
/// - Homes toward the provided target
/// - On trigger hit with an Enemy-tagged collider, sends TakeDamage(int) and destroys itself
/// </summary>
[DisallowMultipleComponent]
public class CrystalProjectile : MonoBehaviour, ITowerProjectile
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float speed = 8f;
    [Tooltip("Rotation offset in degrees if your sprite doesn't face +X (right) by default. Example: if it faces up (+Y), set -90.")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    [Header("Hit Filtering")]
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float maxLifetimeSeconds = 2.5f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem explosion;
    [SerializeField] private GameObject sprite;

    [Header("Hit")]
    [Tooltip("Delay before destroying after hitting an enemy (lets hit VFX play, spreads GC).")]
    [SerializeField, Min(0f)] private float destroyDelayOnHit = 0.5f;

    private Transform target;
    private int damage;
    private float age;
    private bool isHit;

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector2 lastDirection = Vector2.right;

    private void Awake()
    {
        if (explosion != null)
        {
            explosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            explosion.Clear(true);
        }
        // Ensure we can use trigger callbacks + kinematic motion.
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        rb.gravityScale = 0f;

        col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
        }
        col.isTrigger = true;
    }

    public void Activate(Transform target, TowerData towerData, Transform owner)
    {
        this.target = target;
        damage = towerData != null ? towerData.Damage : 0;

        // Initialize heading so it moves even if target disappears immediately.
        if (target != null)
        {
            Vector2 toTarget = (Vector2)target.position - rb.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                lastDirection = toTarget.normalized;
            }
        }
    }

    private void FixedUpdate()
    {
        if (isHit)
        {
            return;
        }

        age += Time.fixedDeltaTime;
        if (maxLifetimeSeconds > 0f && age >= maxLifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (speed <= 0f)
        {
            return;
        }

        Vector2 pos = rb.position;
        Vector2 dir = lastDirection;

        if (target != null)
        {
            Vector2 toTarget = (Vector2)target.position - pos;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                dir = toTarget.normalized;
                lastDirection = dir;
            }
        }

        rb.MovePosition(pos + dir * speed * Time.fixedDeltaTime);

        if (dir.sqrMagnitude > 0.0001f)
        {
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
            rb.MoveRotation(angleDeg);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || isHit)
        {
            return;
        }

        // Tag is on root; collider may be on a child
        if (!other.transform.root.CompareTag(enemyTag))
        {
            return;
        }

        isHit = true;

        // Enemy may be on root/parent if collider is on a child
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage((float)damage);
        }

        if (col != null)
        {
            col.enabled = false;
        }
        if (sprite != null)
        {
            sprite.SetActive(false);
        }
        if (explosion != null)
        {
            explosion.Clear(true);
            explosion.Play(true);
        }

        Destroy(gameObject, destroyDelayOnHit);
    }
}
