using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class AreaBlaster : MonoBehaviour, ITowerProjectile
{
    [Header("Detection (same as tower)")]
    [Tooltip("Trigger collider that defines the blast area (e.g. PolygonCollider2D). Enemies overlapping this are damaged.")]
    [SerializeField] private Collider2D detectionCollider;
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Blast Fallbacks")]
    [Tooltip("Used when TowerData is null or has zero damage so the blast still damages.")]
    [SerializeField, Min(0)] private int fallbackDamage = 10;

    [Header("Active Lifetime")]
    [SerializeField, Min(0f)] private float activeDurationSeconds = 0.6f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem explosion;
    [SerializeField] private GameObject sprite;

    private static readonly ContactFilter2D OverlapFilter = new ContactFilter2D
    {
        useTriggers = true,
        useLayerMask = false,
        useDepth = false,
        useNormalAngle = false
    };

    private readonly Collider2D[] overlapBuffer = new Collider2D[64];
    private readonly HashSet<Transform> alreadyHitThisTick = new HashSet<Transform>();
    private bool hasActivated;
    /// <summary>When set (from owner tower), we use this for overlap so we damage the same area the tower detects.</summary>
    private Collider2D effectiveCollider;

    private void Awake()
    {
        EnsureDetectionCollider();
        if (explosion != null)
        {
            explosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            explosion.Clear(true);
        }
    }

    private void EnsureDetectionCollider()
    {
        if (detectionCollider == null)
        {
            detectionCollider = GetComponent<Collider2D>();
        }
        if (detectionCollider != null && !detectionCollider.isTrigger)
        {
            detectionCollider.isTrigger = true;
        }
    }

    public void Activate(Transform target, TowerData towerData, Transform owner)
    {
        if (hasActivated)
        {
            return;
        }
        hasActivated = true;

        int damage = (towerData != null && towerData.Damage > 0) ? towerData.Damage : fallbackDamage;
        float attackSpeed = (towerData != null && towerData.AttackSpeed > 0f) ? towerData.AttackSpeed : 1f;
        Vector3 pos = owner != null ? owner.position : transform.position;
        pos.z = transform.position.z;
        transform.position = pos;

        if (sprite != null)
        {
            sprite.SetActive(false);
        }
        if (explosion != null)
        {
            explosion.Clear(true);
            explosion.Play(true);
        }

        // Use tower's detection collider so we damage everyone in the same area the tower uses (blast prefab collider may not overlap)
        if (owner != null)
        {
            var towerDetect = owner.GetComponent<TowerEnemyDetect>();
            if (towerDetect != null && towerDetect.DetectionCollider != null)
                effectiveCollider = towerDetect.DetectionCollider;
        }
        if (effectiveCollider == null)
            effectiveCollider = detectionCollider;

        if (damage > 0)
        {
            StartCoroutine(DamageAllInAreaRoutine(damage, attackSpeed));
        }

        Destroy(gameObject, Mathf.Max(0f, activeDurationSeconds));
    }

    /// <summary>
    /// Every 1/attackSpeed seconds, damage all enemies overlapping the detection collider (same system as TowerEnemyDetect).
    /// </summary>
    private IEnumerator DamageAllInAreaRoutine(int damage, float attackSpeed)
    {
        float interval = 1f / Mathf.Max(0.001f, attackSpeed);
        float endTime = Time.time + activeDurationSeconds;
        float nextTick = Time.time;

        while (Time.time < endTime)
        {
            if (Time.time >= nextTick)
            {
                DamageEnemiesInDetectionArea(damage);
                nextTick += interval;
            }
            yield return null;
        }
    }

    /// <summary>
    /// Uses the trigger collider overlap (same as TowerEnemyDetect) to find enemies and apply damage.
    /// </summary>
    private void DamageEnemiesInDetectionArea(int damage)
    {
        Collider2D col = effectiveCollider != null ? effectiveCollider : detectionCollider;
        if (col == null || !col.enabled)
            return;

        int count = col.Overlap(OverlapFilter, overlapBuffer);
        alreadyHitThisTick.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = overlapBuffer[i];
            if (hit == null || !hit.transform.root.CompareTag(enemyTag))
                continue;

            Transform root = hit.transform.root;
            if (root == null || alreadyHitThisTick.Contains(root))
                continue;

            alreadyHitThisTick.Add(root);
            var enemy = root.GetComponentInParent<Enemy>();
            if (enemy == null)
                enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null)
                enemy.TakeDamage((float)damage);
        }
    }
}
