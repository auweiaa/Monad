using System.Collections.Generic;
using UnityEngine;

public class LightningProjectile : MonoBehaviour, ITowerProjectile
{

    [Header("Movement")]
    [SerializeField, Min(0f)] private float speed = 8f;
    [Tooltip("Rotation offset in degrees if your sprite doesn't face +X (right) by default. Example: if it faces up (+Y), set -90.")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    [Header("Hit Filtering")]
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float maxLifetimeSeconds = 5f;

    [Header("Chain Lightning")]
    [Tooltip("Total unique enemies to hit INCLUDING the first.")]
    [SerializeField, Min(1)] private int maxEnemiesHit = 3;
    [Tooltip("Search radius from the last hit point for the next bounce target.")]
    [SerializeField, Min(0f)] private float bounceRadius = 2.5f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem explosion;
    [SerializeField] private GameObject sprite;

    private Transform target;
    private int damage;
    private float age;
    private int hitCount;
    private bool isEnding;

    private readonly HashSet<Transform> hitEnemies = new HashSet<Transform>();

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
        age = 0f;
        hitCount = 0;
        isEnding = false;
        hitEnemies.Clear();

        this.target = target;
        damage = towerData != null ? towerData.Damage : 0;

        if (sprite != null)
        {
            sprite.SetActive(true);
        }
        if (explosion != null)
        {
            explosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            explosion.Clear(true);
        }
        if (col != null)
        {
            col.enabled = true;
        }

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
        if (isEnding)
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
        if (isEnding || other == null)
        {
            return;
        }

        // Tag is on root; collider may be on a child
        if (!other.transform.root.CompareTag(enemyTag))
        {
            return;
        }

        Transform root = other.transform.root;
        if (root == null)
        {
            return;
        }

        if (hitEnemies.Contains(root))
        {
            return;
        }

        // Record hit + deal damage once.
        hitEnemies.Add(root);
        hitCount++;
        var enemy = root.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage((float)damage);
        }

        // Prevent immediate re-triggering on the same collider as we bounce away.
        if (col != null)
        {
            Physics2D.IgnoreCollision(col, other, true);
        }
        if (explosion != null)
        {
            explosion.Clear(true);
            explosion.Play(true);
        }


        if (hitCount >= Mathf.Max(1, maxEnemiesHit))
        {
            EndProjectile();
            return;
        }

        Vector2 bounceCenter = other.ClosestPoint(rb != null ? rb.position : (Vector2)transform.position);
        Transform next = FindNextTarget(bounceCenter);
        if (next == null)
        {
            EndProjectile();
            return;
        }

        target = next;

        // Immediately refresh heading toward the new target.
        if (rb != null)
        {
            Vector2 toTarget = (Vector2)target.position - rb.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                lastDirection = toTarget.normalized;
            }
        }
    }

    private Transform FindNextTarget(Vector2 center)
    {
        if (bounceRadius <= 0f)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, bounceRadius);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        Transform best = null;
        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i];
            if (c == null)
            {
                continue;
            }

            // Tag is on root; collider may be on a child
            if (!c.transform.root.CompareTag(enemyTag))
            {
                continue;
            }

            Transform root = c.transform.root;
            if (root == null || hitEnemies.Contains(root))
            {
                continue;
            }

            float sqr = ((Vector2)root.position - center).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = root;
            }
        }

        return best;
    }

    private void EndProjectile()
    {
        if (isEnding)
        {
            return;
        }
        isEnding = true;

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

        Destroy(gameObject, 1f);
    }

}
