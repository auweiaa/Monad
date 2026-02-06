using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single tower-side script responsible for:
/// - tracking in-range enemies via a 2D trigger
/// - selecting a target (Closest / FirstEntered / Strongest placeholder)
/// - spawning/activating a projectile prefab (projectile decides damage behavior)
/// </summary>
[DisallowMultipleComponent]
public class TowerEnemyDetect : MonoBehaviour
{
    public enum TargetMode
    {
        Closest = 0,
        FirstEntered = 1,
        Strongest = 2, // placeholder until enemy stats exist
    }

    [Header("Targeting")]
    [SerializeField] private TargetMode targetMode = TargetMode.Closest;
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Projectile Spawn")]
    [Tooltip("Optional muzzle/spawn transform. If null, uses this transform.")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;

    [Header("Data (optional override)")]
    [Tooltip("If set, this TowerData is used when PlacedTower data isn't available yet.")]
    [SerializeField] private TowerData fallbackTowerData;

    private readonly List<Transform> inRangeOrdered = new List<Transform>(16);

    private CircleCollider2D circle;
    private Rigidbody2D rb;

    private TowerData towerData;
    private float cooldown;
    private float lastRangeApplied = -1f;

    private void Awake()
    {
        EnsurePhysics();
        TryResolveTowerData();
        ApplyRangeIfNeeded();
    }

    private void OnEnable()
    {
        EnsurePhysics();
        TryResolveTowerData();
        ApplyRangeIfNeeded();
    }

    public void SetTowerData(TowerData data)
    {
        towerData = data;
        ApplyRangeIfNeeded(force: true);
    }

    private void EnsurePhysics()
    {
        circle = GetComponent<CircleCollider2D>();
        if (circle == null)
        {
            circle = gameObject.AddComponent<CircleCollider2D>();
        }
        circle.isTrigger = true;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        rb.gravityScale = 0f;
    }

    private void Update()
    {
        if (towerData == null)
        {
            TryResolveTowerData();
        }
        if (towerData == null)
        {
            return;
        }

        ApplyRangeIfNeeded();
        PruneDestroyed();

        float attackSpeed = towerData.AttackSpeed;
        if (attackSpeed <= 0f)
        {
            return;
        }

        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
        {
            return;
        }

        Transform target = SelectTarget();
        if (target == null)
        {
            return;
        }

        FireProjectile(target);
        cooldown = 1f / Mathf.Max(0.0001f, attackSpeed);
    }

    private void TryResolveTowerData()
    {
        var placed = GetComponent<PlacedTower>();
        if (placed != null && placed.TowerData != null)
        {
            towerData = placed.TowerData;
            return;
        }

        if (fallbackTowerData != null)
        {
            towerData = fallbackTowerData;
        }
    }

    private void ApplyRangeIfNeeded(bool force = false)
    {
        if (towerData == null || circle == null)
        {
            return;
        }

        float range = Mathf.Max(0f, towerData.Range);
        if (!force && Mathf.Approximately(lastRangeApplied, range))
        {
            return;
        }

        circle.radius = range;
        circle.isTrigger = true;
        lastRangeApplied = range;
    }

    private void OnDisable()
    {
        inRangeOrdered.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(enemyTag))
        {
            return;
        }

        Transform t = other.transform;
        if (t == null)
        {
            return;
        }

        if (!inRangeOrdered.Contains(t))
        {
            inRangeOrdered.Add(t);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(enemyTag))
        {
            return;
        }

        Transform t = other.transform;
        if (t == null)
        {
            return;
        }

        inRangeOrdered.Remove(t);
    }

    private void PruneDestroyed()
    {
        for (int i = inRangeOrdered.Count - 1; i >= 0; i--)
        {
            if (inRangeOrdered[i] == null)
            {
                inRangeOrdered.RemoveAt(i);
            }
        }
    }

    private Transform SelectTarget()
    {
        if (inRangeOrdered.Count == 0)
        {
            return null;
        }

        switch (targetMode)
        {
            case TargetMode.FirstEntered:
                // Ordered list: earliest entered is first.
                for (int i = 0; i < inRangeOrdered.Count; i++)
                {
                    if (inRangeOrdered[i] != null)
                    {
                        return inRangeOrdered[i];
                    }
                }
                return null;

            case TargetMode.Strongest:
                // Placeholder until enemy stats exist. Fall back to closest.
                goto case TargetMode.Closest;

            case TargetMode.Closest:
            default:
            {
                Vector3 pos = transform.position;
                Transform best = null;
                float bestSqr = float.PositiveInfinity;

                for (int i = 0; i < inRangeOrdered.Count; i++)
                {
                    Transform t = inRangeOrdered[i];
                    if (t == null) continue;

                    float sqr = (t.position - pos).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = t;
                    }
                }

                return best;
            }
        }
    }

    private void FireProjectile(Transform target)
    {
        if (towerData == null || target == null)
        {
            return;
        }

        GameObject prefab = towerData.ProjectilePrefab;
        if (prefab == null)
        {
            return;
        }

        Vector3 spawnPos = (muzzle != null ? muzzle.position : transform.position) + (Vector3)spawnOffset;
        spawnPos.z = 0f;

        GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);

        // Prefer interface-based activation when available.
        var projectile = instance.GetComponent<ITowerProjectile>();
        if (projectile != null)
        {
            projectile.Activate(target, towerData, transform);
            return;
        }

        // Fallback: allow projectile scripts to implement `void Activate(Transform target)` without interface.
        instance.SendMessage("Activate", target, SendMessageOptions.DontRequireReceiver);
    }
}

