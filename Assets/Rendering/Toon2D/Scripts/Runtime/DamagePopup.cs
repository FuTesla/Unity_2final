using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class DamagePopup : MonoBehaviour
{
    public float lifetime = 0.9f;
    public float riseDistance = 0.9f;

    private static Canvas popupCanvas;

    private Text text;
    private CanvasGroup canvasGroup;
    private Camera targetCamera;
    private Vector3 startWorldPosition;

    public static void Show(Vector3 worldPosition, float amount)
    {
        var cameraToUse = Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        var parentCanvas = EnsureCanvas();
        var popupObject = new GameObject("Damage Popup", typeof(RectTransform), typeof(CanvasGroup), typeof(Text), typeof(Outline), typeof(DamagePopup));
        popupObject.transform.SetParent(parentCanvas.transform, false);

        var rectTransform = popupObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(120f, 48f);

        var popup = popupObject.GetComponent<DamagePopup>();
        popup.Begin(cameraToUse, worldPosition, amount);
    }

    private static Canvas EnsureCanvas()
    {
        if (popupCanvas != null)
        {
            return popupCanvas;
        }

        var existingCanvas = GameObject.Find("Damage Popup Canvas");
        if (existingCanvas != null && existingCanvas.TryGetComponent(out popupCanvas))
        {
            return popupCanvas;
        }

        var canvasObject = new GameObject("Damage Popup Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        popupCanvas = canvasObject.GetComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 5000;

        var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        return popupCanvas;
    }

    private static string FormatAmount(float amount)
    {
        return amount >= 1f ? Mathf.RoundToInt(amount).ToString() : amount.ToString("0.0");
    }

    private void Begin(Camera cameraToUse, Vector3 worldPosition, float amount)
    {
        targetCamera = cameraToUse;
        startWorldPosition = worldPosition;

        text = GetComponent<Text>();
        text.text = FormatAmount(amount);
        text.alignment = TextAnchor.MiddleCenter;
        text.font = GameFontUtility.GetUIFont();
        text.fontSize = 34;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.red;
        text.raycastTarget = false;

        var outline = GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        UpdateScreenPosition(0f);
        StartCoroutine(Animate());
    }

    private void UpdateScreenPosition(float progress)
    {
        var cameraToFace = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToFace == null)
        {
            return;
        }

        var screenPosition = cameraToFace.WorldToScreenPoint(startWorldPosition + Vector3.up * (riseDistance * progress));
        canvasGroup.alpha = screenPosition.z > 0f ? canvasGroup.alpha : 0f;
        transform.position = screenPosition;
    }

    private IEnumerator Animate()
    {
        var elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / lifetime);
            canvasGroup.alpha = 1f - progress;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, progress);
            UpdateScreenPosition(progress);
            yield return null;
        }

        Destroy(gameObject);
    }
}
