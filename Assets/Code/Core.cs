using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Core : MonoBehaviour
{
    [SerializeField] private GameObject orb;
    [SerializeField] private Light2D orbLight;
    [Header("Orb Bobbing")]
    [SerializeField] private float bobAmplitude = 0.25f;
    [SerializeField] private float bobSpeed = 1.5f;

    private Vector3 orbBaseOffset;


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

}
