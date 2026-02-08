using System;
using System.Collections;
using UnityEngine;

public class WaveManager : MonoBehaviour
{

    private enum WaveState
    {
        Spawning, Waiting, Break
    }

    [Header("WaveSetting")]
    [SerializeField] private float breakDuration = 5f;
    [SerializeField] private int baseEnemiesPerWave = 5;
    [SerializeField] private int enemiesPerLevel = 2;
    [SerializeField] private float spawnIntervalInWave = 0.5f;


    private int level;
    private int enemiesToSpawn;
    private int enemiesSpawned;
    private int enemiesAlive;
    private float breakTimer;

    private WaveState currentState;
    private Coroutine spawnRoutine;

    [Header("EnemySpawner")]
    [SerializeField] private EnemySpawner enemySpawner;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        enemySpawner = FindFirstObjectByType<EnemySpawner>();
        if (enemySpawner == null)
        {
            Debug.LogError("[WaveManager] No EnemySpawner found in the scene. Disabling WaveManager.");
            enabled = false;
            return;
        }

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
        if (enemiesSpawned == enemiesToSpawn && enemiesAlive == 0)
        {
            currentState = WaveState.Break;
            breakTimer = breakDuration;
        }
    }

    private void StartNextWave()
    {
        if (currentState != WaveState.Break) return;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        level++;
        enemiesSpawned = 0;
        enemiesAlive = 0;
        enemiesToSpawn = 20; // ToDo: make it variable
        currentState = WaveState.Spawning;

        spawnRoutine = StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        while (enemiesSpawned < enemiesToSpawn)
        {
            // ToDo: public in EmenySpawner
            // enemySpawner.SpawnEnemy();

            enemiesSpawned++;
            enemiesAlive++;
            yield return new WaitForSeconds(spawnIntervalInWave);
        }
        currentState = WaveState.Waiting;
    }

    public void EnemyDied()
    {
        enemiesAlive = Math.Max(0, enemiesAlive - 1);
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }
}
