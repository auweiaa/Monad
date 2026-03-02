using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    [SerializeField] private Core core;
    [SerializeField] private Slider healthSlider;

    void Awake()
    {
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }
    }

    void OnEnable()
    {
        if (core == null)
        {
            core = FindAnyObjectByType<Core>();
        }
        
        if (core != null)
        {
            core.HealthChanged += UpdateBar;

            healthSlider.minValue = 0;
            healthSlider.maxValue = core.MaxHealth;

            UpdateBar(core.CurrentHealth);
        }

    }

    void OnDisable()
    {
        if (core != null)
        {            
            core.HealthChanged -= UpdateBar;
        }
    }

    private void UpdateBar(float current)
    {
        healthSlider.value = current;
    }
}
