using TMPro;
using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class LabelManager : MonoBehaviour
{
    public static LabelManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Your screen-space Canvas.")]
    [SerializeField] private Canvas canvas;

    [Tooltip("Optional container under the canvas to parent labels to. If null, uses the canvas root.")]
    [SerializeField] private RectTransform labelContainer;

    [Tooltip("Camera used for WorldToScreenPoint when spawning at a world position. If null, uses Camera.main.")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("Prefab with a TextMeshProUGUI on it.")]
    [SerializeField] private GameObject labelPrefab;

    [Header("Default Label Behaviour")]
    [SerializeField, Min(0.05f)] private float duration = 1.0f;
    [SerializeField] private Vector2 riseOffsetPixels = new Vector2(0f, 60f);
    [Tooltip("Extra Y offset applied (in screen pixels) when spawning labels from world positions.")]
    [SerializeField] private float spawnYOffsetPixels = 0f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private Color defaultColor = Color.white;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (labelContainer == null && canvas != null)
        {
            labelContainer = canvas.transform as RectTransform;
        }
    }
    public void SpawnLabel(string text, Vector3 worldPosition)
    {
        SpawnLabel(text, worldPosition, defaultColor, duration, riseOffsetPixels);
    }

    public void SpawnLabel(string text, Vector3 worldPosition, Color color, float labelDuration, Vector2 riseOffset)
    {
        var cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("LabelManager: No camera assigned and Camera.main is null. Can't convert world position to screen position.");
            return;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
        if (screenPos.z < 0f)
        {
            return;
        }

        screenPos.y += spawnYOffsetPixels;
        SpawnLabelScreen(text, (Vector2)screenPos, color, labelDuration, riseOffset);
    }

    /// <summary>
    /// Spawns a label at a screen position (pixels).
    /// </summary>
    public void SpawnLabelScreen(string text, Vector2 screenPositionPixels)
    {
        SpawnLabelScreen(text, screenPositionPixels, defaultColor, duration, riseOffsetPixels);
    }

    public void SpawnLabelScreen(string text, Vector2 screenPositionPixels, Color color, float labelDuration, Vector2 riseOffset)
    {
        if (canvas == null)
        {
            Debug.LogWarning("LabelManager: Canvas is not assigned.");
            return;
        }

        if (labelPrefab == null)
        {
            Debug.LogWarning("LabelManager: labelPrefab is not assigned.");
            return;
        }

        RectTransform container = labelContainer != null ? labelContainer : (canvas.transform as RectTransform);
        if (container == null)
        {
            Debug.LogWarning("LabelManager: labelContainer/canvas RectTransform is missing.");
            return;
        }

        var instance = Instantiate(labelPrefab, container);
        var rect = instance.GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.LogWarning("LabelManager: labelPrefab must have a RectTransform (UI object).");
            Destroy(instance);
            return;
        }

        Camera uiCamera = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPositionPixels, uiCamera, out Vector2 localPoint))
        {
            Destroy(instance);
            return;
        }

        rect.anchoredPosition = localPoint;

        var tmp = instance.GetComponent<TextMeshProUGUI>() ?? instance.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
        }

        float dur = Mathf.Max(0.05f, labelDuration);
        StartCoroutine(AnimateAndDestroy(instance, rect, tmp, riseOffset, dur));
    }

    private IEnumerator AnimateAndDestroy(GameObject instance, RectTransform rect, TextMeshProUGUI tmp, Vector2 riseOffsetPixels, float durationSeconds)
    {
        Vector2 startPos = rect.anchoredPosition;
        Vector2 endPos = startPos + riseOffsetPixels;

        Color baseColor = tmp != null ? tmp.color : defaultColor;
        float t = 0f;

        while (t < durationSeconds)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float a = durationSeconds > 0f ? Mathf.Clamp01(t / durationSeconds) : 1f;

            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, a);

            if (tmp != null)
            {
                float fade = 1f - a;
                tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);
            }

            yield return null;
        }

        if (instance != null)
        {
            Destroy(instance);
        }
    }
}
