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
    [SerializeField] private float spawnIntervalInWave = 0.5f;
    [SerializeField] private int enemiesToSpawn = 10;

    [Header("EnemySpawner")]
    [SerializeField] private EnemySpawner[] enemySpawners;
    [SerializeField] private int levelForSpawner2 = 15;
    [SerializeField] private int levelForSpawner3 = 30;


    private int level;
    private int activeSpawnersCount;
    private float breakTimer;
    private WaveState currentState;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentState = WaveState.Break;
        breakTimer = breakDuration;
        level = 0;
        activeSpawnersCount = 1;

        if (enemySpawners == null)
        {
            Debug.LogError("[WaveManager] No enemySpawner in Scene.");
            return;
        }

        for (int i = 0; i < enemySpawners.Length; i++)
        {
            if (enemySpawners[i] != null)
            {
                enemySpawners[i].StatusChanged += OnSpawnerStatusChanged;
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
        if (enemySpawners == null) return;

        for (int i = 0; i < enemySpawners.Length; i++)
        {
            if (enemySpawners[i] != null)
            {
                // unsubscribe each spawner
                enemySpawners[i].StatusChanged -= OnSpawnerStatusChanged;
            }
        }
    }

    private void StartNextWave()
    {

        if (enemySpawners == null || enemySpawners.Length == 0 || enemySpawners[0] == null)
        {
            Debug.LogError("[WaveManager] No active spawners available to start a wave.");
            return;
        }


        // setting new level and amount of active spawner related to the level
        level++;
        activeSpawnersCount = GetActiveSpawnersCount(level);

        Debug.Log($"Starting wave {level} with {activeSpawnersCount} active spawners");

        for (int i = 0; i < activeSpawnersCount; i++)
        {
            if (i >= enemySpawners.Length) break;

            if (enemySpawners[i] != null)
            {
                enemySpawners[i].BeginnWave(level, enemiesToSpawn, spawnIntervalInWave);
            }
        }

        currentState = WaveState.Waiting;

        CheckWaveCompletion();
    }

    private int GetActiveSpawnersCount(int currentLevel)
    {
        int count = 1;
       
        if (currentLevel >= levelForSpawner2) count = 2;
        if (currentLevel >= levelForSpawner3) count = 3;

        if (enemySpawners != null)
        {
            count = Mathf.Min(count, enemySpawners.Length);
        }

        return count;
    }
    
    private void OnSpawnerStatusChanged(EnemySpawner spawner)
    {
        if (currentState != WaveState.Waiting) return;

        CheckWaveCompletion();
    }

    private void CheckWaveCompletion()
    {
        if (enemySpawners.Length == 0) return;

        bool allSpawnerFinished = true;
        int totalAliveEnemies = 0;

        for (int i = 0; i < activeSpawnersCount; i++)
        {
            if (enemySpawners[i] == null) continue;
            
            totalAliveEnemies += enemySpawners[i].AliveCount;

            if (!enemySpawners[i].FinishedSpawning)
            {
                allSpawnerFinished = false;
                break;
            }
        }

        if (allSpawnerFinished && totalAliveEnemies == 0)
        {
            currentState = WaveState.Break;
            breakTimer = breakDuration;
        }
    }

}
