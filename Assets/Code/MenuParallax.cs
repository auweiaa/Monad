using UnityEngine;

public class MenuParallax : MonoBehaviour
{
    public float offsetMultiplier = 1f;
    public float smoothTime = 0.3f;

    private Vector3 startPos;   //Vector3
    private Vector3 velocity;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        if (Camera.main == null) return;

        Vector3 offset = Camera.main.ScreenToViewportPoint(Input.mousePosition);
        offset -= new Vector3(0.5f, 0.5f, 0f); // centering it 

        Vector3 target = startPos + offset * offsetMultiplier;
        target.z = startPos.z; //og z now is preserved

        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime);
    }
}
