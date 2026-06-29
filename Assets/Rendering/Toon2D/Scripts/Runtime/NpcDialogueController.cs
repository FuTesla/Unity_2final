using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NpcDialogueController : MonoBehaviour
{
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.F;
    public float interactDistance = 3.2f;
    public Transform player;
    public TopDownCharacterMotor playerMotor;

    [Header("Camera")]
    public IsometricCameraFollow cameraFollow;
    public Camera mainCamera;
    public float dialogueOrthographicSize = 2.15f;
    public float cameraTransitionDuration = 0.65f;
    public float returnCameraTransitionDuration = 0.55f;

    [Header("Overhead Bubble")]
    public Vector3 bubbleOffset = new Vector3(0f, 2.65f, 0f);
    public Vector2 bubbleSize = new Vector2(155f, 78f);

    [Header("Interaction Prompt")]
    public Vector3 promptOffset = new Vector3(1.15f, 1.45f, 0f);
    public Vector2 promptSize = new Vector2(260f, 56f);
    public string promptText = "\u6309\u4e0bF\u4ee5\u4ea4\u4e92";

    [Header("Dialogue")]
    public string[] dialogueLines =
    {
        "\u4f60\u597d\uff0c\u5192\u9669\u8005\u3002",
        "\u8fd9\u7247\u533a\u57df\u5f88\u5371\u9669\uff0c\u8bf7\u4fdd\u6301\u8b66\u60d5\u3002",
        "\u795d\u4f60\u4e00\u8def\u987a\u5229\u3002"
    };

    private const string DialogueHudName = "Dialogue HUD";

    private GameObject dialogueHud;
    private Text dialogueText;
    private GameObject overheadBubble;
    private GameObject promptHud;
    private GameObject interactionPrompt;
    private RectTransform promptCanvasRoot;
    private RectTransform interactionPromptRect;
    private static Font readableFont;
    private static Sprite roundedRectSprite;
    private float previousCameraSize;
    private Coroutine cameraTransition;
    private bool previousMotorEnabled;
    private int lineIndex;
    private bool isTalking;
    private bool isCameraTransitioning;
    private bool hasCameraSnapshot;
    private bool dialogueCompleted;

    private void Awake()
    {
        ResolveReferences();
        HideStaleDialogueHud();
        EnsureDialogueUi();
        EnsureOverheadBubble();
        EnsureInteractionPrompt();
        HideDialogueUi();
        SetOverheadBubbleVisible(true);
        SetInteractionPromptVisible(false);
    }

    private void Start()
    {
        HideDialogueUi();
        SetOverheadBubbleVisible(!dialogueCompleted);
        UpdateInteractionPrompt();
    }

    private void Update()
    {
        if (isTalking)
        {
            if (Input.GetMouseButtonDown(0))
            {
                AdvanceDialogue();
            }

            return;
        }

        UpdateInteractionPrompt();

        if (!isCameraTransitioning && Input.GetKeyDown(interactKey) && IsPlayerInRange())
        {
            StartDialogue();
        }
    }

    private void OnDisable()
    {
        if (isTalking)
        {
            EndDialogue(true);
        }

        StopCameraTransition();
        RestoreCameraImmediate();
        HideDialogueUi();
        SetOverheadBubbleVisible(!dialogueCompleted);
        SetInteractionPromptVisible(false);
    }

    private void StartDialogue()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            return;
        }

        ResolveReferences();
        EnsureDialogueUi();

        isTalking = true;
        lineIndex = 0;
        previousMotorEnabled = playerMotor != null && playerMotor.enabled;
        SetOverheadBubbleVisible(false);
        SetInteractionPromptVisible(false);

        if (playerMotor != null)
        {
            playerMotor.enabled = false;
        }

        StopCameraTransition();

        if (mainCamera != null)
        {
            previousCameraSize = mainCamera.orthographicSize;
            hasCameraSnapshot = true;
        }

        var startSize = mainCamera != null ? mainCamera.orthographicSize : previousCameraSize;
        BeginCameraTransition(startSize, dialogueOrthographicSize, cameraTransitionDuration, null);

        ShowDialogueUi();
        ShowCurrentLine();
    }

    private void AdvanceDialogue()
    {
        lineIndex++;
        if (lineIndex >= dialogueLines.Length)
        {
            EndDialogue(false);
            return;
        }

        ShowCurrentLine();
    }

    private void EndDialogue(bool immediate)
    {
        isTalking = false;
        dialogueCompleted = true;
        HideDialogueUi();
        SetOverheadBubbleVisible(false);
        SetInteractionPromptVisible(false);
        StopCameraTransition();

        if (immediate)
        {
            RestoreCameraImmediate();
            RestorePlayerMovement();
            return;
        }

        var startSize = mainCamera != null ? mainCamera.orthographicSize : dialogueOrthographicSize;
        BeginCameraTransition(startSize, previousCameraSize, returnCameraTransitionDuration, FinishDialogueReturn);
    }

    private void ShowCurrentLine()
    {
        if (dialogueText != null)
        {
            dialogueText.text = dialogueLines[lineIndex];
        }
    }

    private bool IsPlayerInRange()
    {
        ResolveReferences();
        if (player == null)
        {
            return false;
        }

        var offset = player.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= interactDistance * interactDistance;
    }

    private void ResolveReferences()
    {
        if (playerMotor == null)
        {
            playerMotor = FindObjectOfType<TopDownCharacterMotor>();
        }

        if (player == null && playerMotor != null)
        {
            player = playerMotor.transform;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (cameraFollow == null && mainCamera != null)
        {
            cameraFollow = mainCamera.GetComponent<IsometricCameraFollow>();
        }
    }

    private void EnsureDialogueUi()
    {
        if (dialogueHud != null && dialogueText != null)
        {
            return;
        }

        EnsureEventSystem();

        dialogueHud = new GameObject(DialogueHudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        dialogueHud.SetActive(false);

        var canvas = dialogueHud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;

        var scaler = dialogueHud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var root = dialogueHud.GetComponent<RectTransform>();
        Stretch(root);

        var panel = CreateImage("Dialogue Panel", root, new Color(0.035f, 0.04f, 0.05f, 0.9f));
        panel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, 36f);
        panel.rectTransform.sizeDelta = new Vector2(1320f, 190f);

        dialogueText = CreateText("NPC Dialogue Text", panel.rectTransform, string.Empty, 31, FontStyle.Normal, Color.white);
        dialogueText.rectTransform.anchorMin = Vector2.zero;
        dialogueText.rectTransform.anchorMax = Vector2.one;
        dialogueText.rectTransform.offsetMin = new Vector2(44f, 30f);
        dialogueText.rectTransform.offsetMax = new Vector2(-44f, -30f);
        dialogueText.alignment = TextAnchor.MiddleLeft;
    }

    private void HideStaleDialogueHud()
    {
        var staleHud = GameObject.Find(DialogueHudName);
        if (staleHud == null || staleHud == dialogueHud)
        {
            return;
        }

        staleHud.SetActive(false);
        Destroy(staleHud);
    }

    private void EnsureOverheadBubble()
    {
        if (overheadBubble != null)
        {
            return;
        }

        overheadBubble = new GameObject("NPC Dialogue Bubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        overheadBubble.transform.SetParent(transform, false);
        overheadBubble.transform.localPosition = bubbleOffset;

        var canvas = overheadBubble.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;

        var root = overheadBubble.GetComponent<RectTransform>();
        root.sizeDelta = bubbleSize;
        root.localScale = Vector3.one * 0.01f;

        var bubble = CreateRoundedImage("Bubble", root, Color.white);
        Stretch(bubble.rectTransform);
        bubble.raycastTarget = false;

        var dots = CreateText("Dots", bubble.rectTransform, "...", 42, FontStyle.Bold, new Color(0.08f, 0.08f, 0.09f, 1f));
        Stretch(dots.rectTransform);

        SetOverheadBubbleVisible(true);
    }

    private void EnsureInteractionPrompt()
    {
        if (interactionPrompt != null)
        {
            return;
        }

        if (promptHud == null)
        {
            promptHud = new GameObject("NPC Interaction Prompt HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = promptHud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 220;

            var scaler = promptHud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            promptCanvasRoot = promptHud.GetComponent<RectTransform>();
            Stretch(promptCanvasRoot);
        }

        interactionPrompt = new GameObject("NPC Interaction Prompt", typeof(RectTransform));
        interactionPrompt.transform.SetParent(promptCanvasRoot, false);
        interactionPromptRect = interactionPrompt.GetComponent<RectTransform>();
        interactionPromptRect.anchorMin = new Vector2(0.5f, 0.5f);
        interactionPromptRect.anchorMax = new Vector2(0.5f, 0.5f);
        interactionPromptRect.pivot = new Vector2(0f, 0.5f);
        interactionPromptRect.sizeDelta = promptSize;

        var background = CreateRoundedImage("Prompt Background", interactionPromptRect, new Color(0.015f, 0.018f, 0.022f, 0.94f));
        Stretch(background.rectTransform);
        background.raycastTarget = false;

        var label = CreateText("Prompt Text", background.rectTransform, promptText, 18, FontStyle.Bold, Color.white);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(18f, 0f);
        label.rectTransform.offsetMax = new Vector2(-18f, 0f);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        SetInteractionPromptVisible(false);
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            return;
        }

        if (overheadBubble != null)
        {
            overheadBubble.transform.rotation = mainCamera.transform.rotation;
        }

        if (interactionPrompt != null)
        {
            UpdateInteractionPromptPosition();
        }
    }

    private void SetOverheadBubbleVisible(bool visible)
    {
        if (overheadBubble != null)
        {
            overheadBubble.SetActive(visible);
        }
    }

    private void UpdateInteractionPrompt()
    {
        var shouldShow = !dialogueCompleted && !isTalking && !isCameraTransitioning && IsPlayerInRange();
        SetInteractionPromptVisible(shouldShow);
        if (shouldShow)
        {
            UpdateInteractionPromptPosition();
        }
    }

    private void SetInteractionPromptVisible(bool visible)
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(visible);
        }
    }

    private void UpdateInteractionPromptPosition()
    {
        if (mainCamera == null || promptCanvasRoot == null || interactionPromptRect == null)
        {
            return;
        }

        var worldPoint = transform.position + new Vector3(0f, promptOffset.y, promptOffset.z);
        var screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0f)
        {
            SetInteractionPromptVisible(false);
            return;
        }

        screenPoint.x += promptOffset.x * 100f;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(promptCanvasRoot, screenPoint, null, out var localPoint))
        {
            interactionPromptRect.anchoredPosition = localPoint;
        }
    }

    private void BeginCameraTransition(float fromSize, float toSize, float duration, System.Action onComplete)
    {
        cameraTransition = StartCoroutine(BlendCamera(fromSize, toSize, duration, onComplete));
    }

    private IEnumerator BlendCamera(float fromSize, float toSize, float duration, System.Action onComplete)
    {
        isCameraTransitioning = true;
        var elapsed = 0f;
        var safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / safeDuration);
            var eased = Smooth01(t);
            ApplyCameraFrame(Mathf.Lerp(fromSize, toSize, eased));
            yield return null;
        }

        ApplyCameraFrame(toSize);

        isCameraTransitioning = false;
        cameraTransition = null;
        onComplete?.Invoke();
    }

    private void ApplyCameraFrame(float orthographicSize)
    {
        if (mainCamera != null && orthographicSize > 0f)
        {
            mainCamera.orthographicSize = orthographicSize;
        }
    }

    private void StopCameraTransition()
    {
        if (cameraTransition == null)
        {
            return;
        }

        StopCoroutine(cameraTransition);
        cameraTransition = null;
        isCameraTransitioning = false;
    }

    private void RestoreCameraImmediate()
    {
        if (!hasCameraSnapshot)
        {
            return;
        }

        if (cameraFollow != null)
        {
            cameraFollow.ResetVelocity();
        }

        if (mainCamera != null && previousCameraSize > 0f)
        {
            mainCamera.orthographicSize = previousCameraSize;
        }

        hasCameraSnapshot = false;
    }

    private void RestorePlayerMovement()
    {
        if (playerMotor != null)
        {
            playerMotor.enabled = previousMotorEnabled;
        }
    }

    private void FinishDialogueReturn()
    {
        hasCameraSnapshot = false;
        RestorePlayerMovement();
    }

    private static float Smooth01(float value)
    {
        return value * value * (3f - 2f * value);
    }

    private void ShowDialogueUi()
    {
        if (dialogueHud != null)
        {
            dialogueHud.SetActive(true);
        }
    }

    private void HideDialogueUi()
    {
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (dialogueHud != null)
        {
            dialogueHud.SetActive(false);
        }
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image CreateRoundedImage(string objectName, Transform parent, Color color)
    {
        var image = CreateImage(objectName, parent, color);
        image.sprite = GetRoundedRectSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.material = Graphic.defaultGraphicMaterial;
        return image;
    }

    private static Text CreateText(string objectName, Transform parent, string value, int fontSize, FontStyle style, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);

        var text = obj.GetComponent<Text>();
        text.text = value;
        text.font = GetReadableFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.material = Graphic.defaultGraphicMaterial;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        if (text.font != null)
        {
            text.font.RequestCharactersInTexture(value, fontSize, style);
        }

        return text;
    }

    private static Font GetReadableFont()
    {
        if (readableFont != null)
        {
            return readableFont;
        }

        readableFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
            24);

        if (readableFont == null)
        {
            readableFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (readableFont == null)
        {
            readableFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return readableFont;
    }

    private static Sprite GetRoundedRectSprite()
    {
        if (roundedRectSprite != null)
        {
            return roundedRectSprite;
        }

        const int size = 64;
        const int radius = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated Rounded UI Sprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var nearestX = Mathf.Clamp(x, radius, size - radius - 1);
                var nearestY = Mathf.Clamp(y, radius, size - radius - 1);
                var dx = x - nearestX;
                var dy = y - nearestY;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = Mathf.Clamp01(radius + 0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        roundedRectSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return roundedRectSprite;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
