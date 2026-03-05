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
    [SerializeField] private GameObject countdownPanel;

    [SerializeField] private SpawnArrowIndicatorUI spawnArrowIndicatorUI;
    [SerializeField, Min(0.1f)] private float spawnArrowDuration = 3f;

    [Header("WaveSetting")]
    [SerializeField] private float initialCountdown   = 60f;
    [SerializeField] private float breakDuration      = 5f;
    [SerializeField] private float baseWaveDuration      = 20f;
    [SerializeField] private float baseEnemySpawnInterval = 0.5f;
    [SerializeField] private int baseEnemyCount = 10;

    [Header("EnemySpawner")]
    [SerializeField] private EnemySpawner[] enemySpawners;
    [SerializeField] private int levelForSpawner2 = 15;
    [SerializeField] private int levelForSpawner3 = 30;

    [SerializeField]
    private DifficultyManager difficultyManager;

    private float currentWaveDuration;
    private int level;
    private bool startWaveCountdown;

    [SerializeField] private TextMeshProUGUI levelText;
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


        if (difficultyManager == null)
        {
            difficultyManager = FindFirstObjectByType<DifficultyManager>();

            if (difficultyManager == null)
            {
                Debug.LogError("[WaveManager] No difficulty Manager is assigned in Inspector. Disabling WaveManager.");
                enabled = false;
                return;
            }
        }


        level = 0;
        activeSpawnersCount = 1;

        currentWaveDuration = baseWaveDuration;

        if (spawnArrowIndicatorUI == null)
        {
            spawnArrowIndicatorUI = FindFirstObjectByType<SpawnArrowIndicatorUI>();
        }

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
                // waiting for wave duration to end
                if (startWaveCountdown) UpdateCurrentWaveDuration();
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
    private void EnterWaitingState()
    {
        currentState = WaveState.Waiting;
        //countdown starts when alle enemies have spawned
        startWaveCountdown = false; 
        currentWaveDuration = baseWaveDuration;
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

    private void UpdateCurrentWaveDuration()
    {
        if (currentWaveDuration <= 0f) CheckWaveCompletion();
        currentWaveDuration -= Time.deltaTime;
    }
    private void ShowCountdownText()
    {
        if (countdownText != null) countdownPanel.gameObject.SetActive(true);
    }

    private void HideCountdownText()
    {
        if (countdownText != null) countdownPanel.gameObject.SetActive(false);
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
        levelText.text = level.ToString();
        activeSpawnersCount = GetActiveSpawnersCount(level);
        

        WaveConfiguration waveConfig = difficultyManager.BuildWaveConfiguration(level, baseEnemyCount, baseEnemySpawnInterval);
        
        Debug.Log($"[WaveManager] Starting {level}. wave with {activeSpawnersCount} active spawner(s). Enemies per spawner: {baseEnemyCount}, interval: {baseEnemySpawnInterval}s");

        EnemySpawner[] activeSpawners = new EnemySpawner[activeSpawnersCount];  

        for (int i = 0; i < activeSpawnersCount; i++)
        {
            activeSpawners[i] = enemySpawners[i];   
            Debug.Log($"[WaveManager] Spawner {i}: {enemySpawners[i].name} begins {level}. wave");
            enemySpawners[i].BeginWave(waveConfig);
        }

        if (spawnArrowIndicatorUI != null)
        {
            spawnArrowIndicatorUI.ShowForSpawners(activeSpawners, spawnArrowDuration);
        }

        EnterWaitingState();

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
        //if (currentState != WaveState.Waiting) return;

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

        if (allSpawnerFinished) startWaveCountdown = true;        

        if(currentWaveDuration <= 0f || totalAliveEnemies == 0) {
            EnterBreakState();
        }
    }

}
