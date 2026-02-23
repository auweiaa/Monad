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

    [Header("Detection Collider (Manual)")]
    [Tooltip("Assign a trigger collider manually (for example PolygonCollider2D). This script will not auto-create one.")]
    [SerializeField] private Collider2D detectionCollider;

    /// <summary>Collider used to detect enemies in range. Area projectiles (e.g. AreaBlaster) can use this to damage the same area.</summary>
    public Collider2D DetectionCollider => detectionCollider;

    [Header("Projectile Spawn")]
    [Tooltip("Optional muzzle/spawn transform. If null, uses this transform.")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;

    [Header("Data (optional override)")]
    [Tooltip("If set, this TowerData is used when PlacedTower data isn't available yet.")]
    [SerializeField] private TowerData fallbackTowerData;

    private readonly List<Transform> inRangeOrdered = new List<Transform>(16);
    private readonly HashSet<Transform> inRangeSet = new HashSet<Transform>();
    private readonly Collider2D[] overlapBuffer = new Collider2D[128];
    private readonly HashSet<Transform> currentlyInside = new HashSet<Transform>();
    private readonly ContactFilter2D overlapFilter = new ContactFilter2D
    {
        useTriggers = true,
        useLayerMask = false,
        useDepth = false,
        useNormalAngle = false
    };

    private TowerData towerData;
    private float cooldown;

    private void Awake()
    {
        EnsureDetectionCollider();
        TryResolveTowerData();
    }

    private void OnEnable()
    {
        EnsureDetectionCollider();
        TryResolveTowerData();
    }

    public void SetTowerData(TowerData data)
    {
        towerData = data;
    }

    private void EnsureDetectionCollider()
    {
        if (detectionCollider == null)
        {
            detectionCollider = GetComponent<Collider2D>();
        }
        if (detectionCollider == null)
        {
            Debug.LogWarning($"{nameof(TowerEnemyDetect)} on {name}: No detection collider assigned. Assign a trigger collider in the Inspector.", this);
            return;
        }

        if (!detectionCollider.isTrigger)
        {
            Debug.LogWarning($"{nameof(TowerEnemyDetect)} on {name}: Detection collider should have Is Trigger enabled. Enforcing at runtime.", this);
            detectionCollider.isTrigger = true;
        }
    }

    private void Update()
    {
        if (towerData == null)
        {
            TryResolveTowerData();
        }

        RefreshInRangeFromDetectionCollider();
        if (towerData == null)
        {
            return;
        }
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

    private void OnDisable()
    {
        inRangeOrdered.Clear();
        inRangeSet.Clear();
    }

    private void RefreshInRangeFromDetectionCollider()
    {
        if (detectionCollider == null || !detectionCollider.enabled)
        {
            inRangeOrdered.Clear();
            inRangeSet.Clear();
            return;
        }

        int count = detectionCollider.Overlap(overlapFilter, overlapBuffer);
        currentlyInside.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = overlapBuffer[i];
            if (hit == null || !hit.transform.root.CompareTag(enemyTag))
            {
                continue;
            }

            Transform t = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
            if (t == null || !currentlyInside.Add(t))
            {
                continue;
            }

            if (!inRangeSet.Contains(t))
            {
                inRangeSet.Add(t);
                inRangeOrdered.Add(t);
            }
        }

        for (int i = inRangeOrdered.Count - 1; i >= 0; i--)
        {
            Transform t = inRangeOrdered[i];
            if (t == null || !currentlyInside.Contains(t))
            {
                inRangeSet.Remove(t);
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

