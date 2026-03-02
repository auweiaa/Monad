using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Enemy enemy;   // your enemy script
    [SerializeField] private Image fillImage;

    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    private void Start()
    {
        fillImage.gameObject.SetActive(false);
        if (enemy == null) enemy = GetComponentInParent<Enemy>();
    }

    private void LateUpdate()
    {
        // Keep bar facing camera (billboard)
        if (cam != null) transform.forward = cam.transform.forward;
    }

    public void Refresh(float current, float max)
    {
        fillImage.gameObject.SetActive(true);
        fillImage.fillAmount = Mathf.Clamp01(current / max);
    }
}