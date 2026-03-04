using System.Collections.Generic;
using UnityEngine;

public class LightningProjectile : MonoBehaviour, ITowerProjectile
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float speed = 8f;
    [Tooltip("Distance to target at which the projectile counts as arrived and deals damage.")]
    [SerializeField, Min(0.01f)] private float targetReachDistance = 0.3f;
    [Tooltip("Rotation offset in degrees if your sprite doesn't face +X (right) by default. Example: if it faces up (+Y), set -90.")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    [Header("Hit Filtering")]
    [SerializeField] private string enemyTag = "Enemy";
    [Tooltip("Optional layer filter for bounce search. Leave as Everything to search all layers.")]
    [SerializeField] private LayerMask enemyLayerMask = ~0;

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float maxLifetimeSeconds = 5f;

    [Header("Chain Lightning")]
    [Tooltip("Total unique enemies to hit INCLUDING the first.")]
    [SerializeField, Min(1)] private int maxEnemiesHit = 3;
    [Tooltip("Search radius from the last hit point for the next bounce target.")]
    [SerializeField, Min(0f)] private float bounceRadius = 2.5f;
    [Tooltip("If no target is found from the hit point, retry from projectile position with this radius multiplier.")]
    [SerializeField, Min(1f)] private float fallbackRadiusMultiplier = 1.75f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem explosion;
    [SerializeField] private GameObject sprite;

    [Header("Debug")]
    [SerializeField] private bool debugChainLogs = false;
    [SerializeField] private bool debugDrawBounceRadius = false;

    private Transform target;
    private int damage;
    private float age;
    private int hitCount;
    private bool isEnding;

    private readonly HashSet<Transform> hitEnemies = new HashSet<Transform>();

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector2 lastDirection = Vector2.right;
    private Vector2 lastBounceCenter;
    private bool hasPendingTriggerHit;
    private Vector2 pendingTriggerBounceCenter;

    private void Awake()
    {
        if (explosion != null)
        {
            explosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            explosion.Clear(true);
        }

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
        hasPendingTriggerHit = false;
        hitEnemies.Clear();

        this.target = NormalizeAimTransform(target);
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

        if (!ValidateOrAcquireTarget())
        {
            return;
        }

        Vector2 pos = rb.position;
        Vector2 toTarget = (Vector2)target.position - pos;
        bool reachedByDistance = toTarget.sqrMagnitude <= targetReachDistance * targetReachDistance;
        bool reachedByTrigger = hasPendingTriggerHit;

        if (reachedByDistance || reachedByTrigger)
        {
            // Use actual impact position when arriving by distance; root pivots can be offset.
            Vector2 bounceCenter = reachedByTrigger ? pendingTriggerBounceCenter : rb.position;
            hasPendingTriggerHit = false;

            if (!TryGetEnemyFromTransform(target, out Transform enemyRoot, out Enemy enemy))
            {
                target = null;
                TryResolveInvalidTarget();
                return;
            }

            ResolveHitAndAdvance(enemyRoot, enemy, bounceCenter);
            return;
        }

        Vector2 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : lastDirection;
        lastDirection = dir;

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

        if (!TryGetEnemyFromCollider(other, out Transform enemyRoot, out Enemy enemy))
        {
            return;
        }

        // Strict current-target policy: ignore incidental collisions.
        if (!IsCurrentTarget(enemyRoot))
        {
            return;
        }

        if (enemy == null || enemy.IsDead)
        {
            return;
        }

        hasPendingTriggerHit = true;
        pendingTriggerBounceCenter = other.ClosestPoint(rb != null ? rb.position : (Vector2)transform.position);
    }

    private bool ValidateOrAcquireTarget()
    {
        if (TryGetEnemyFromTransform(target, out Transform enemyRoot, out Enemy enemy))
        {
            target = NormalizeAimTransform(target);
            return true;
        }

        target = null;
        return TryResolveInvalidTarget();
    }

    private bool TryResolveInvalidTarget()
    {
        Vector2 center = rb != null ? rb.position : (Vector2)transform.position;
        if (!TryFindNextTargetWithFallback(center, out Transform next, out float usedRadius))
        {
            if (debugChainLogs)
            {
                Debug.Log($"[LightningProjectile:{GetInstanceID()}] No valid target. Destroying.", this);
            }
            EndProjectile();
            return false;
        }

        target = next;
        hasPendingTriggerHit = false;

        Vector2 toTarget = (Vector2)target.position - center;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            lastDirection = toTarget.normalized;
        }

        if (debugChainLogs)
        {
            Debug.Log($"[LightningProjectile:{GetInstanceID()}] Reacquired target '{target.name}' (radius {usedRadius:0.00}).", this);
        }

        return true;
    }

    private bool IsCurrentTarget(Transform enemyRoot)
    {
        if (enemyRoot == null || target == null)
        {
            return false;
        }

        if (target == enemyRoot)
        {
            return true;
        }

        Enemy targetEnemy = target.GetComponentInParent<Enemy>();
        return targetEnemy != null && targetEnemy.transform == enemyRoot;
    }

    private void ResolveHitAndAdvance(Transform enemyRoot, Enemy enemy, Vector2 bounceCenter)
    {
        if (enemyRoot == null || enemy == null)
        {
            EndProjectile();
            return;
        }

        bool alreadyHit = hitEnemies.Contains(enemyRoot);
        if (!alreadyHit)
        {
            hitEnemies.Add(enemyRoot);
            hitCount++;

            if (!enemy.IsDead)
            {
                enemy.TakeDamage((float)damage);
            }

            if (debugChainLogs)
            {
                Debug.Log($"[LightningProjectile:{GetInstanceID()}] Hit '{enemyRoot.name}' ({hitCount}/{Mathf.Max(1, maxEnemiesHit)}).", this);
            }

            if (explosion != null)
            {
                explosion.Clear(true);
                explosion.Play(true);
            }
        }

        if (hitCount >= Mathf.Max(1, maxEnemiesHit))
        {
            EndProjectile();
            return;
        }

        lastBounceCenter = bounceCenter;

        if (!TryFindNextTargetWithFallback(bounceCenter, out Transform next, out float usedRadius))
        {
            if (debugChainLogs)
            {
                Debug.Log($"[LightningProjectile:{GetInstanceID()}] No bounce target found after '{enemyRoot.name}'. Destroying.", this);
            }

            EndProjectile();
            return;
        }

        target = next;
        hasPendingTriggerHit = false;

        Vector2 currentPosition = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 toTarget = (Vector2)target.position - currentPosition;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            lastDirection = toTarget.normalized;
        }

        if (debugChainLogs)
        {
            float targetDist = Vector2.Distance(lastBounceCenter, target.position);
            Debug.Log($"[LightningProjectile:{GetInstanceID()}] Bouncing to '{target.name}' (distance {targetDist:0.00}, radius {usedRadius:0.00}).", this);
        }
    }

    private bool TryFindNextTargetWithFallback(Vector2 center, out Transform next, out float usedRadius)
    {
        usedRadius = bounceRadius;
        next = FindNextTarget(center, bounceRadius);
        if (next != null)
        {
            return true;
        }

        if (fallbackRadiusMultiplier <= 1f)
        {
            return false;
        }

        usedRadius = bounceRadius * fallbackRadiusMultiplier;
        next = FindNextTarget(center, usedRadius);
        return next != null;
    }

    private Transform FindNextTarget(Vector2 center, float radius)
    {
        if (radius <= 0f)
        {
            return null;
        }

        // Explicit semantics: "Nothing" mask means no candidates.
        if (enemyLayerMask.value == 0)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, enemyLayerMask);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        Transform bestAim = null;
        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i];
            if (c == null)
            {
                continue;
            }

            if (!TryGetEnemyFromCollider(c, out Transform enemyRoot, out Enemy enemy))
            {
                continue;
            }

            if (enemy == null || enemy.IsDead || hitEnemies.Contains(enemyRoot))
            {
                continue;
            }

            // Use collider geometry instead of transform pivot for robust selection.
            Vector2 candidatePoint = c.ClosestPoint(center);
            float sqr = (candidatePoint - center).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestAim = NormalizeAimTransform(c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform);
            }
        }

        return bestAim;
    }

    private Transform NormalizeAimTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        Rigidbody2D rbCandidate = candidate.GetComponentInChildren<Rigidbody2D>();
        if (rbCandidate != null)
        {
            return rbCandidate.transform;
        }

        Collider2D colliderCandidate = candidate.GetComponentInChildren<Collider2D>();
        if (colliderCandidate != null)
        {
            return colliderCandidate.transform;
        }

        return candidate;
    }

    private bool TryGetEnemyFromTransform(Transform candidate, out Transform enemyRoot, out Enemy enemy)
    {
        enemyRoot = null;
        enemy = null;

        if (candidate == null)
        {
            return false;
        }

        enemy = candidate.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.IsDead)
        {
            return false;
        }

        enemyRoot = enemy.transform;
        if (!string.IsNullOrEmpty(enemyTag) && !enemyRoot.CompareTag(enemyTag))
        {
            return false;
        }

        return true;
    }

    private bool TryGetEnemyFromCollider(Collider2D c, out Transform enemyRoot, out Enemy enemy)
    {
        enemyRoot = null;
        enemy = null;

        if (c == null)
        {
            return false;
        }

        Transform candidate = c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform;
        if (candidate == null)
        {
            return false;
        }

        enemy = candidate.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.IsDead)
        {
            return false;
        }

        enemyRoot = enemy.transform;
        if (!string.IsNullOrEmpty(enemyTag) && !enemyRoot.CompareTag(enemyTag))
        {
            return false;
        }

        return true;
    }

    private void EndProjectile()
    {
        if (isEnding)
        {
            return;
        }

        isEnding = true;
        hasPendingTriggerHit = false;

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

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawBounceRadius || bounceRadius <= 0f)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, bounceRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastBounceCenter, bounceRadius);
        }
    }
}
