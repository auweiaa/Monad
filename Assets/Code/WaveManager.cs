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
    [SerializeField] private int baseEnemiesPerWave = 5;
    [SerializeField] private int enemiesPerLevel = 2;
    [SerializeField] private float spawnIntervalInWave = 0.5f;

    [Header("EnemySpawner")]
    [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private Transform[] enemySpawnPoints;
    private List<EnemySpawner> activeEnemySpawners;

    private int level;
    private float breakTimer;

    private WaveState currentState;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        activeEnemySpawners = new List<EnemySpawner>();

        currentState = WaveState.Break;
        breakTimer = breakDuration;
        level = 0;
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case WaveState.Break:
                HandleBreak();
                break;

            case WaveState.Waiting:
                HandleWaiting();
                break;
        }
    }
    
    void HandleBreak()
    {
        breakTimer -= Time.deltaTime;

        if (breakTimer <= 0) {
            StartNextWave();
        }
    }

    void HandleWaiting()
    {
        bool allSpawnerFinished = true;
        int sumAliveEnemies = 0;

        foreach (var spawner in activeEnemySpawners)
        {
            sumAliveEnemies += spawner.AliveCount;

            if (!spawner.FinishedSpawning) allSpawnerFinished = false;    
        }

        if (allSpawnerFinished && sumAliveEnemies == 0)
        {
            currentState = WaveState.Break;
            breakTimer = breakDuration;
        }
    }

    private void StartNextWave()
    {
        level++;
        CreateEnemySpawnersForLevel(level);

        // BeginnWave(level, enemiesToSpawn, spawnIntervalInWave);
        currentState = WaveState.Waiting;
    }

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
            activeEnemySpawners.Add(newSpawner);
        }
    }

}
