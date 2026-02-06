using UnityEngine;

[CreateAssetMenu(fileName = "TowerData", menuName = "Towers/TowerData")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string towerName;
    [SerializeField] private GameObject towerPrefab;

    [Header("Combat Stats")]
    [Min(1f)]
    [SerializeField] private float range = 1f;

    [Min(1)]
    [SerializeField] private int health = 1;

    [Min(1)]
    [SerializeField] private int damage = 0;

    [Tooltip("Attacks per second. Higher = faster.")]
    [Min(0f)]
    [SerializeField] private float attackSpeed = 1f;

    [Header("Placement")]
    [Tooltip("Ground footprint in tiles. Examples: (2,2), (3,3).")]
    [SerializeField] private Vector2Int footprint = new Vector2Int(1, 1);

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Cost")]
    [SerializeField] private ResourceCost cost;

    public string TowerName => towerName;
    public float Range => range;
    public int Health => health;
    public int Damage => damage;
    public float AttackSpeed => attackSpeed;
    public Vector2Int Footprint => footprint;
    public GameObject ProjectilePrefab => projectilePrefab;
    public ResourceCost Cost => cost;
    public GameObject TowerPrefab => towerPrefab;
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(towerName))
        {
            towerName = name;
        }

        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;

        if (range < 0f) range = 0f;
        if (attackSpeed < 0f) attackSpeed = 0f;
        if (health < 1) health = 1;
        if (damage < 0) damage = 0;

        cost = cost.ClampNonNegative();
    }
}


