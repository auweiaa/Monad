using UnityEngine;

/// <summary>
/// Optional contract for projectile prefabs so towers can activate them without
/// knowing their specific implementation.
/// </summary>
public interface ITowerProjectile
{
    /// <summary>
    /// Called when the tower fires this projectile.
    /// </summary>
    /// <param name="target">Current chosen target (may become null later).</param>
    /// <param name="towerData">Tower stats/config that fired this projectile.</param>
    /// <param name="owner">The tower transform that spawned this projectile.</param>
    void Activate(Transform target, TowerData towerData, Transform owner);
}

