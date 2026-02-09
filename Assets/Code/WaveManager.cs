using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{

    private enum WaveState
    {
        Waiting, Break
    }

    [Header("WaveSetting")]
    [SerializeField] private float breakDuration = 5f;
/*    [SerializeField] private int baseEnemiesPerWave = 5;
    [SerializeField] private int enemiesPerLevel = 2;*/
    [SerializeField] private float spawnIntervalInWave = 0.5f;
    [SerializeField] private int enemiesToSpawn = 10;

    [Header("EnemySpawner")]
    [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private Transform[] enemySpawnPoints;

    private readonly List<EnemySpawner> activeEnemySpawners = new List<EnemySpawner>(); // ToDo: fixed list of pre-designed EnemySpawner

    private int level;
    private float breakTimer;
    private WaveState currentState;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentState = WaveState.Break;
        breakTimer = breakDuration;
        level = 0;

        if (enemySpawnerPrefab == null && enemySpawnPoints.Length == 0)
        {
            var existing = FindFirstObjectByType<EnemySpawner>();
            if (existing != null)
            {
                existing.StatusChanged += OnSpawnerStatusChanged;
                activeEnemySpawners.Add(existing);
            }
            else
            {
                Debug.LogError("[WaveManager] No EnemySpawner in scene and no prefab/spawnpoints set.");
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (currentState == WaveState.Break)
        {
            breakTimer -= Time.deltaTime;

            if (breakTimer <= 0f) StartNextWave();
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < activeEnemySpawners.Count; i++)
        {
            if (activeEnemySpawners[i] != null)
            {
                // unsubscribe each spawner
                activeEnemySpawners[i].StatusChanged -= OnSpawnerStatusChanged;
            }
        }
    }

    private void StartNextWave()
    {

        Debug.Log($"[WaveManager] Starting wave {level} with {activeEnemySpawners.Count} spawners");


        level++;
        // enemiesToSpawn = xyz; -> making it dynamic

        CreateEnemySpawnersForLevel(level); // ToDo: delete -> not needed, will be in scene

        if (activeEnemySpawners.Count == 0)
        {
            Debug.LogError("[WaveManager] No active spawners available to start a wave.");
            return;
        }

        foreach (var spawner in activeEnemySpawners)
        {
            spawner.BeginnWave(level, enemiesToSpawn, spawnIntervalInWave);
        }

        currentState = WaveState.Waiting;

        CheckWaveCompletion();
    }

    // not needed in future -> will be in scene and attached to island.
    private void CreateEnemySpawnersForLevel(int target)
    {
        while (activeEnemySpawners.Count < target)
        {
            if (activeEnemySpawners.Count >= enemySpawnPoints.Length)
            {
                Debug.LogError("Not enough enemy spawn points for level " + target);
                return;
            }

            
            Transform spawnPoint = enemySpawnPoints[activeEnemySpawners.Count];
            EnemySpawner newSpawner = Instantiate(enemySpawnerPrefab, spawnPoint.position, spawnPoint.rotation);

            //subscribe spawner
            newSpawner.StatusChanged += OnSpawnerStatusChanged;
            activeEnemySpawners.Add(newSpawner);
        }
    }

    private void OnSpawnerStatusChanged(EnemySpawner spawner)
    {
        if (currentState != WaveState.Waiting) return;

        CheckWaveCompletion();
    }

    private void CheckWaveCompletion()
    {
        if (activeEnemySpawners.Count == 0) return;

        bool allSpawnerFinished = true;
        int totalAliveEnemies = 0;

        foreach (var spawner in activeEnemySpawners)
        {
            totalAliveEnemies += spawner.AliveCount;

            if (!spawner.FinishedSpawning) allSpawnerFinished = false;
        }

        if (allSpawnerFinished && totalAliveEnemies == 0)
        {
            currentState = WaveState.Break;
            breakTimer = breakDuration;
        }
    }

}
