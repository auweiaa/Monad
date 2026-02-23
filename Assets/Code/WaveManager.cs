using UnityEngine;
using TMPro;

public class WaveManager : MonoBehaviour
{

    private enum WaveState
    {
        Countdown, Waiting, Break
    }

    [Header("UI")]
    [SerializeField] private TMP_Text countdownText;

    [Header("WaveSetting")]
    [SerializeField] private float initialCountdown   = 60f;
    [SerializeField] private float breakDuration      = 5f;
    [SerializeField] private float spawnIntervalInWave = 0.5f;
    [SerializeField] private int enemiesToSpawn = 10;

    [Header("EnemySpawner")]
    [SerializeField] private EnemySpawner[] enemySpawners;
    [SerializeField] private int levelForSpawner2 = 15;
    [SerializeField] private int levelForSpawner3 = 30;


    private int level;
    private int activeSpawnersCount;
    private float breakTimer;
    private float countdownTimer;
    private WaveState currentState;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (enemySpawners == null || enemySpawners.Length == 0)
        {
            Debug.LogError("[WaveManager] No enemy spawners are assigned in Inspector. Disabling WaveManager.");
            enabled = false;
            return;
        }

        //check if all spawner are assigned in the inspector
        if (HasUnassignedSpawners())
        {
            Debug.LogError("[WaveManager] Invalid spawner configuration. See previous error(s). Disabling WaveManager.");
            enabled = false;
            return;
        }

        level = 0;
        activeSpawnersCount = 1;

        EnterCountdownState();

        for (int i = 0; i < enemySpawners.Length; i++)
        {
            enemySpawners[i].StatusChanged -= OnSpawnerStatusChanged; // prevents phantom double events
            enemySpawners[i].StatusChanged += OnSpawnerStatusChanged;
        }
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case WaveState.Countdown:
                UpdateCountDown();
                if (countdownTimer <= 0f)
                {
                    HideCountdownText();
                    StartNextWave();
                }                    
                break;

            case WaveState.Break:
                breakTimer -= Time.deltaTime;
                if (breakTimer <= 0f) EnterCountdownState();
                break;

            case WaveState.Waiting:
                // Intentionally empty: waiting for wave completion driven by spawner events.
                break;

            default:
                Debug.LogWarning($"[WaveManager] Unknown state: {currentState}");
                break;
        }
    }


    private void EnterCountdownState()
    {
        countdownTimer = initialCountdown;
        ShowCountdownText();
        currentState = WaveState.Countdown;

        Debug.Log($"[WaveManager] Wave {level + 1} prepared. Starting Countdown");
    }

    private void EnterBreakState()
    {
        breakTimer = breakDuration;
        currentState = WaveState.Break;

        Debug.Log($"[WaveManager] Wave {level} completed. Break for {breakDuration}s.");
    }

    private void UpdateCountDown()
    {
        countdownTimer -= Time.deltaTime;

        if (countdownText != null)
        {
            int secs = Mathf.CeilToInt(Mathf.Max(0f, countdownTimer));
            int upcomingWave = level + 1;
            countdownText.text = $"Wave {upcomingWave} starts in {secs}s";
        }
    }

    private void ShowCountdownText()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(true);
    }

    private void HideCountdownText()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(false);
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

    private bool HasUnassignedSpawners()
    {
        bool error = false;

        for (int i = 0; i < enemySpawners.Length; i++)
        {
            if (enemySpawners[i] == null)
            {
                Debug.LogError($"[WaveManager] EnemySpawner at array index {i} is not assigned.");
                error = true;
            }
        }

        return error;
    }

    private void StartNextWave()
    {
        // setting new level and amount of active spawner related to the level
        level++;
        activeSpawnersCount = GetActiveSpawnersCount(level);

        Debug.Log($"[WaveManager] Starting {level}. wave with {activeSpawnersCount} active spawner(s). Enemies per spawner: {enemiesToSpawn}, interval: {spawnIntervalInWave}s");


        for (int i = 0; i < activeSpawnersCount; i++)
        {
            Debug.Log($"[WaveManager] Spawner {i}: {enemySpawners[i].name} begins {level}. wave");
            enemySpawners[i].BeginWave(level, enemiesToSpawn, spawnIntervalInWave);
        }

        currentState = WaveState.Waiting;

        CheckWaveCompletion();
    }

    private int GetActiveSpawnersCount(int waveLevel)
    {
        int count = 1;

        if (waveLevel >= levelForSpawner2) count = 2;
        if (waveLevel >= levelForSpawner3) count = 3;

        count = Mathf.Min(count, enemySpawners.Length);

        return count;
    }
    
    private void OnSpawnerStatusChanged(EnemySpawner spawner)
    {
        if (currentState != WaveState.Waiting) return;

        CheckWaveCompletion();
    }

    private void CheckWaveCompletion()
    {
        bool allSpawnerFinished = true;
        int totalAliveEnemies = 0;

        for (int i = 0; i < activeSpawnersCount; i++)
        {
            totalAliveEnemies += enemySpawners[i].AliveCount;

            if (!enemySpawners[i].FinishedSpawning)
            {
                allSpawnerFinished = false;
                break;
            }
        }

        if (allSpawnerFinished && totalAliveEnemies == 0) EnterBreakState();
    }

}
