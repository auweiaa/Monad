using UnityEngine;
using System.Collections;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Spawns enemies randomly around the map and directs them towards a target position.
/// Attach this to an empty GameObject in your scene.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Target Position")]
    [SerializeField] private Vector2 targetPosition = new Vector2(-5.285247f, 0.3300301f);
    
    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 15f;
    [SerializeField] private float minSpawnDistance = 10f;
    
    [Header("Enemy Stats")]
    [SerializeField, Min(0.1f)] private float enemySpeed = 1f;

    private int plannedToSpawn;
    private int enemiesSpawned;
    private int alive;
    private bool finishedSpawning;
    private Coroutine spawnCoroutine;

    private float enemySpawnInterval;
    public event Action<EnemySpawner> StatusChanged;

    public int AliveCount => alive;
    public bool FinishedSpawning => finishedSpawning;


    // WaveManager calls EnemySpawner to spwan Enemies:
    public void BeginWave(int waveLevel, int countToSpawn, float interval)
    {
        Debug.Log($"[EnemySpawner] BeginWave: spawn {countToSpawn} interval {interval} at {transform.position}");


        // stop old wave if still running (safety)
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        // setting private variables according to wave instructions
        plannedToSpawn = countToSpawn;
        enemiesSpawned = 0;
        alive = 0; // maybe changing this line/ concept, if we want new waves while enemies of an earlier wave are still alive
        finishedSpawning = false;

        enemySpawnInterval = interval;


        // Load the enemy prefab from Resources if not assigned
        if (enemyPrefab == null)
        {
            enemyPrefab = Resources.Load<GameObject>("EnemyPawn");
            if (enemyPrefab == null)
            {
                Debug.LogError("EnemySpawner: No enemy prefab assigned and couldn't load from Resources!");
                return;
            }
        }

        // starting Coroutine with the aboved set variables
        spawnCoroutine = StartCoroutine(SpawnEnemiesRoutine());
    }


    private void Start()
    {

    }

    private void OnDestroy()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        while (enemiesSpawned < plannedToSpawn)
        {
            SpawnEnemy();
            //Debug.Log("Current Enemy Count: " + enemiesSpawned);
            yield return new WaitForSeconds(enemySpawnInterval);

        }
        finishedSpawning = true;
        spawnCoroutine = null;
        StatusChanged?.Invoke(this);
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

        enemiesSpawned++;
        alive++;
        StatusChanged?.Invoke(this);
    }

    private Vector2 GetRandomSpawnPosition()
    {
        // Get the prefab's position as the center for spawning
  /*      Vector2 prefabCenter = enemyPrefab != null ? (Vector2)enemyPrefab.transform.position : Vector2.zero;*/
        Vector2 prefabCenter = enemyPrefab != null ? (Vector2)transform.position : Vector2.zero;
        
        // Spawn enemies in a ring around the prefab location (between minSpawnDistance and spawnRadius)
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(minSpawnDistance, spawnRadius);
        
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        return prefabCenter + offset;
    }

    public void OnEnemyDestroyed()
    {
        //Debug.Log("Enemy destroyed on spawner " + gameObject.name + ". Count before: " + alive);
        alive = Mathf.Max(0, alive - 1);
        //Debug.Log("Count after: " + alive);

        StatusChanged?.Invoke(this);
    }

    /// <summary>
    /// Kills an enemy GameObject and updates the count.
    /// Call this method to properly remove an enemy from the game.
    /// </summary>
    public void KillEnemy(GameObject enemyObject)
    {
        if (enemyObject != null)
        {
            //Debug.Log("Killing enemy: " + enemyObject.name);
            Destroy(enemyObject);
            // Note: OnEnemyDestroyed will be called automatically by the Enemy's OnDestroy
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Get the prefab's position as the center for visualization
       /* Vector3 prefabCenter = enemyPrefab != null ? enemyPrefab.transform.position : Vector3.zero;*/
        Vector3 prefabCenter = enemyPrefab != null ? transform.position : Vector3.zero;
        
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
