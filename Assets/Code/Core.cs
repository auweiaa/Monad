using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System;

public class Core : MonoBehaviour
{
    [SerializeField] private GameObject orb;
    [SerializeField] private Light2D orbLight;
    [Header("Orb Bobbing")]
    [SerializeField] private float bobAmplitude = 0.25f;
    [SerializeField] private float bobSpeed = 1.5f;

    [Header("Health System")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Destruction Effects")]
    [SerializeField] private ParticleSystem[] coreParticleSystems;
    [SerializeField] private float explosionSize = 90f;
    [SerializeField] private float explosionDuration = 5f;

    public event Action<float> HealthChanged; // currentHealth
    public event Action OnCoreDeathEvent; // Fired when core dies

    private Vector3 orbBaseOffset;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => currentHealth <= 0f;


    void Awake()
    {
        currentHealth = maxHealth;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SoundManager.Instance.PlaySfx3D("force", transform.position, 1f);

        if (orb != null)
        {
            orbBaseOffset = orb.transform.position - transform.position;
        }
    }

    private void Update()
    {
        if (orb != null)
        {
            float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            orb.transform.position = transform.position + orbBaseOffset + Vector3.up * yOffset;
        }

        if (orbLight != null)
        {
            orbLight.intensity = Mathf.Sin(Time.time * 2f) * 0.5f + 0.5f;
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);

        UnityEngine.Debug.Log($"Core took {damage} damage! Health: {currentHealth}/{maxHealth}");

        HealthChanged?.Invoke(currentHealth);

        if (IsDestroyed)
        {
            OnCoreDestroyed();
        }
    }

    private void OnCoreDestroyed()
    {
        UnityEngine.Debug.Log("Core has been destroyed!");
        
        OnCoreDeathEvent?.Invoke();
        
        if (coreParticleSystems != null && coreParticleSystems.Length > 0)
        {
            StartCoroutine(HandleCoreExplosion());
        }
    }

    private IEnumerator HandleCoreExplosion()
    {
        // Set particle size to explosion size for all particle systems
        foreach (ParticleSystem ps in coreParticleSystems)
        {
            if (ps != null)
            {
                var main = ps.main;
                main.startSize = explosionSize;
            }
        }
        
        // Wait for the specified duration
        yield return new WaitForSeconds(explosionDuration);
        
        // Set particle size to 0 for all particle systems
        foreach (ParticleSystem ps in coreParticleSystems)
        {
            if (ps != null)
            {
                var main = ps.main;
                main.startSize = 0f;
            }
        }
    }

}
