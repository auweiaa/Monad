using UnityEngine;

[DisallowMultipleComponent]
public class AreaBlaster : MonoBehaviour, ITowerProjectile
{
    [Header("Hit Filtering")]
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Active Lifetime")]
    [SerializeField, Min(0f)] private float activeDurationSeconds = 0.6f;

    [Header("Isometric Range Projection")]
    [SerializeField] private bool useIsometricProjection = true;
    [SerializeField, Min(0.01f)] private float isometricYScale = 0.5f;
    [SerializeField] private float isometricShear = 0f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem explosion;
    [SerializeField] private GameObject sprite;

    private bool hasActivated;

    private void Awake()
    {
        if (explosion != null)
        {
            explosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            explosion.Clear(true);
        }
    }

    public void Activate(Transform target, TowerData towerData, Transform owner)
    {
        if (hasActivated)
        {
            return;
        }
        hasActivated = true;

        int damage = towerData != null ? towerData.Damage : 0;
        float radius = towerData != null ? Mathf.Max(0f, towerData.Range) : 0f;
        Vector2 center = owner != null ? (Vector2)owner.position : (Vector2)transform.position;
        transform.position = new Vector3(center.x, center.y, transform.position.z);

        if (radius > 0f && damage > 0)
        {
            DamageEnemiesInRadius(center, radius, damage);
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

        Destroy(gameObject, Mathf.Max(0f, activeDurationSeconds));
    }

    private void DamageEnemiesInRadius(Vector2 center, float radius, int damage)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        var alreadyHit = new System.Collections.Generic.HashSet<Transform>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.CompareTag(enemyTag))
            {
                continue;
            }

            Transform victim = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
            if (victim == null || alreadyHit.Contains(victim))
            {
                continue;
            }

            if (!IsWithinAoERadius(center, victim.position, radius))
            {
                continue;
            }

            alreadyHit.Add(victim);
            victim.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private bool IsWithinAoERadius(Vector2 center, Vector3 worldPoint, float radius)
    {
        Vector2 local = (Vector2)worldPoint - center;
        if (!useIsometricProjection)
        {
            return local.sqrMagnitude <= radius * radius;
        }

        float isoX = local.x + (local.y * isometricShear);
        float isoY = local.y * isometricYScale;
        float projectedSqr = (isoX * isoX) + (isoY * isoY);
        return projectedSqr <= radius * radius;
    }
}
