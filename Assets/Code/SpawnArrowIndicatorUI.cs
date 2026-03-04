using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnArrowIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform arrowContainer;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RectTransform arrowPrefab;

    [Header("Arrow Behavior")]
    [SerializeField, Min(0f)] private float edgePaddingPixels = 60f;
    [SerializeField] private float rotationOffsetDegrees = -90f;
    [SerializeField] private bool useUnscaledTime = true;

    private readonly List<ArrowEntry> activeArrows = new List<ArrowEntry>();
    private float hideAtTime;
    private bool isShowing;
    private bool hasWarnedMissingReferences;

    private class ArrowEntry
    {
        public Transform Target;
        public RectTransform Rect;
    }

    private void Awake()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (arrowContainer == null && canvas != null)
        {
            arrowContainer = canvas.transform as RectTransform;
        }
    }

    public void ShowForSpawners(EnemySpawner[] spawners, float durationSeconds)
    {
        if (spawners == null || spawners.Length == 0)
        {
            HideAllArrows();
            return;
        }

        if (!HasRequiredReferences())
        {
            HideAllArrows();
            return;
        }

        EnsureArrowCount(spawners.Length);

        for (int i = 0; i < spawners.Length; i++)
        {
            Transform target = spawners[i] != null ? spawners[i].transform : null;
            activeArrows[i].Target = target;
            activeArrows[i].Rect.gameObject.SetActive(target != null);
        }

        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        hideAtTime = now + Mathf.Max(0.1f, durationSeconds);
        isShowing = true;
        UpdateArrows();
    }

    private void Update()
    {
        if (!isShowing)
        {
            return;
        }

        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (now >= hideAtTime)
        {
            HideAllArrows();
            return;
        }

        UpdateArrows();
    }

    private void UpdateArrows()
    {
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null || arrowContainer == null)
        {
            HideAllArrows();
            return;
        }

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float halfWidth = Mathf.Max(1f, screenCenter.x - edgePaddingPixels);
        float halfHeight = Mathf.Max(1f, screenCenter.y - edgePaddingPixels);
        Camera uiCamera = GetUICamera(cam);

        for (int i = 0; i < activeArrows.Count; i++)
        {
            ArrowEntry arrow = activeArrows[i];
            if (arrow.Target == null)
            {
                arrow.Rect.gameObject.SetActive(false);
                continue;
            }

            Vector3 targetScreen = cam.WorldToScreenPoint(arrow.Target.position);
            if (targetScreen.z <= 0f)
            {
                arrow.Rect.gameObject.SetActive(false);
                continue;
            }

            Vector2 direction = ((Vector2)targetScreen - screenCenter).normalized;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.up;
            }

            float scaleX = Mathf.Abs(direction.x) > 0.0001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float scaleY = Mathf.Abs(direction.y) > 0.0001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
            float scale = Mathf.Min(scaleX, scaleY);

            Vector2 edgeScreenPosition = screenCenter + direction * scale;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(arrowContainer, edgeScreenPosition, uiCamera, out Vector2 localPoint))
            {
                arrow.Rect.gameObject.SetActive(false);
                continue;
            }

            arrow.Rect.anchoredPosition = localPoint;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
            arrow.Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            arrow.Rect.gameObject.SetActive(true);
        }
    }

    private Camera GetUICamera(Camera fallbackCamera)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (canvas.worldCamera != null)
        {
            return canvas.worldCamera;
        }

        return fallbackCamera;
    }

    private bool HasRequiredReferences()
    {
        if (arrowPrefab != null && arrowContainer != null)
        {
            return true;
        }

        if (!hasWarnedMissingReferences)
        {
            Debug.LogWarning("[SpawnArrowIndicatorUI] Missing arrowPrefab or arrowContainer reference.");
            hasWarnedMissingReferences = true;
        }
        return false;
    }

    private void EnsureArrowCount(int count)
    {
        while (activeArrows.Count < count)
        {
            RectTransform arrow = Instantiate(arrowPrefab, arrowContainer);
            arrow.gameObject.SetActive(false);
            activeArrows.Add(new ArrowEntry { Target = null, Rect = arrow });
        }

        for (int i = count; i < activeArrows.Count; i++)
        {
            activeArrows[i].Target = null;
            activeArrows[i].Rect.gameObject.SetActive(false);
        }
    }

    private void HideAllArrows()
    {
        isShowing = false;
        for (int i = 0; i < activeArrows.Count; i++)
        {
            activeArrows[i].Target = null;
            if (activeArrows[i].Rect != null)
            {
                activeArrows[i].Rect.gameObject.SetActive(false);
            }
        }
    }
}
