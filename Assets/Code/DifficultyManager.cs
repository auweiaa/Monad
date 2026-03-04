using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    public WaveConfiguration BuildWaveConfiguration(int level, int baseEnemyCount, float enemySpawnInterval)
    {
        float health = GetHealthMultiplier(level);
        float damage = GetDamageMultiplier(level);
        int enemiesToSpawn = GetEnemyAmountMultiplier(baseEnemyCount, level);

        return new WaveConfiguration(enemySpawnInterval, enemiesToSpawn, health, damage);
    }

    
    private float GetHealthMultiplier(int level)
    {
        return 1 + (level - 1) * 0.15f; // first level should start with multiplier 1
    }
    private float GetDamageMultiplier(int level)
    {
        return 1 + (level / 3) * 0.1f;
    }
    private int GetEnemyAmountMultiplier(int baseEnemyCount, int level)
    {
        return baseEnemyCount + level / 2;
    }


}
