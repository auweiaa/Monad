using UnityEngine;
using System.Collections;

/// <summary>
/// Spawns enemies randomly around the map and directs them towards a target position.
/// Attach this to an empty GameObject in your scene.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField, Min(0.5f)] private float spawnInterval = 10f;
    [SerializeField, Min(1)] private int maxEnemies = 20;
    
    [Header("Target Position")]
    [SerializeField] private Vector2 targetPosition = new Vector2(-5.285247f, 0.3300301f);
    
    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 15f;
    [SerializeField] private float minSpawnDistance = 10f;
    
    [Header("Enemy Stats")]
    [SerializeField, Min(0.1f)] private float enemySpeed = 1f;
    
    private int currentEnemyCount = 0;
    private Coroutine spawnCoroutine;

    private void Start()
    {
        // Load the enemy prefab from Resources if not assigned
        if (enemyPrefab == null)
        {
            enemyPrefab = Resources.Load<GameObject>("Enemy");
            if (enemyPrefab == null)
            {
                Debug.LogError("EnemySpawner: No enemy prefab assigned and couldn't load from Resources!");
                return;
            }
        }
        
        spawnCoroutine = StartCoroutine(SpawnEnemiesRoutine());
    }

    private void OnDestroy()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            Debug.Log("Current Enemy Count: " + currentEnemyCount);
            
            if (currentEnemyCount < maxEnemies)
            {
                SpawnEnemy();
            }
        }
    }

    private void SpawnEnemy()
    {
        Vector2 spawnPosition = GetRandomSpawnPosition();
        GameObject enemyObj = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        
        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = enemyObj.AddComponent<Enemy>();
        }
        
        enemy.Initialize(targetPosition, enemySpeed, this);
        currentEnemyCount++;
    }

    private Vector2 GetRandomSpawnPosition()
    {
        // Get the prefab's position as the center for spawning
        Vector2 prefabCenter = enemyPrefab != null ? (Vector2)enemyPrefab.transform.position : Vector2.zero;
        
        // Spawn enemies in a ring around the prefab location (between minSpawnDistance and spawnRadius)
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(minSpawnDistance, spawnRadius);
        
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        return prefabCenter + offset;
    }

    public void OnEnemyDestroyed()
    {
        Debug.Log("Enemy destroyed on spawner " + gameObject.name + ". Count before: " + currentEnemyCount);
        //currentEnemyCount = Mathf.Max(0, currentEnemyCount - 1);
        Debug.Log("Count after: " + currentEnemyCount);
    }

    private void OnDrawGizmosSelected()
    {
        // Get the prefab's position as the center for visualization
        Vector3 prefabCenter = enemyPrefab != null ? enemyPrefab.transform.position : Vector3.zero;
        
        // Target position
        Vector3 targetCenter = new Vector3(targetPosition.x, targetPosition.y, 0f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetCenter, 0.5f);
        
        // Spawn radius (centered on prefab location)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(prefabCenter, spawnRadius);
        
        // Min spawn distance (centered on prefab location)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(prefabCenter, minSpawnDistance);
    }
}


[DisallowMultipleComponent]
public class Enemy : MonoBehaviour
{
    private EnemySpawner spawner;

    public void Initialize(Vector2 target, float speed, EnemySpawner parentSpawner)
    {
        spawner = parentSpawner;
    }

    private void OnDestroy() 
    {
        Debug.Log("Enemy destroyed: " + gameObject.name + " at position " + transform.position);
        if (spawner != null)
        {
            spawner.OnEnemyDestroyed();
        }
    }
}
