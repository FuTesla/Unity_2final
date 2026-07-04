using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BookReadPortal : MonoBehaviour
{
    public Transform bookTarget;
    public string targetSceneName = "Lvl_1";
    public KeyCode interactKey = KeyCode.F;
    public float interactDistance = 3.0f;
    public Vector3 promptWorldOffset = new Vector3(1.15f, 0.75f, 0f);
    public Vector2 promptSize = new Vector2(220f, 50f);
    public string promptText = "\u6309\u4e0bF\u9605\u8bfb";

    private const string CanvasName = "Book Read Prompt HUD";
    private const string PromptName = "Book Read Prompt";

    private static Font readableFont;
    private static Sprite promptSprite;

    private TopDownCharacterMotor playerMotor;
    private Camera mainCamera;
    private GameObject promptRoot;
    private RectTransform promptRect;

    private void Awake()
    {
        ResolveReferences();
        EnsurePromptUi();
        SetPromptVisible(false);
    }

    private void Start()
    {
        ResolveReferences();
        UpdatePrompt();
    }

    private void Update()
    {
        ResolveReferences();

        var inRange = IsPlayerInRange();
        SetPromptVisible(inRange);
        if (inRange)
        {
            UpdatePromptPosition();
        }

        if (inRange && Input.GetKeyDown(interactKey))
        {
            SceneDirectControlRouter.RequestDirectControl(targetSceneName);
            SceneManager.LoadScene(targetSceneName);
        }
    }

    private void OnDisable()
    {
        SetPromptVisible(false);
    }

    private void ResolveReferences()
    {
        if (playerMotor == null)
        {
            playerMotor = FindObjectOfType<TopDownCharacterMotor>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private bool IsPlayerInRange()
    {
        if (bookTarget == null || playerMotor == null)
        {
            return false;
        }

        var playerPosition = playerMotor.transform.position;
        var bookPosition = bookTarget.position;
        playerPosition.y = 0f;
        bookPosition.y = 0f;
        return (playerPosition - bookPosition).sqrMagnitude <= interactDistance * interactDistance;
    }

    private void UpdatePrompt()
    {
        var inRange = IsPlayerInRange();
        SetPromptVisible(inRange);
        if (inRange)
        {
            UpdatePromptPosition();
        }
    }

    private void UpdatePromptPosition()
    {
        if (promptRect == null || bookTarget == null || mainCamera == null)
        {
            return;
        }

        var right = bookTarget.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        var worldPosition = bookTarget.position + right * promptWorldOffset.x + Vector3.up * promptWorldOffset.y + bookTarget.forward * promptWorldOffset.z;
        var screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            SetPromptVisible(false);
            return;
        }

        promptRect.position = screenPosition;
    }

    private void EnsurePromptUi()
    {
        EnsureEventSystem();

        var canvasObject = GameObject.Find(CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 180;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        var existing = canvasObject.transform.Find(PromptName);
        if (existing != null)
        {
            promptRoot = existing.gameObject;
            promptRect = promptRoot.GetComponent<RectTransform>();
            return;
        }

        promptRoot = new GameObject(PromptName, typeof(RectTransform), typeof(Image));
        promptRoot.transform.SetParent(canvasObject.transform, false);
        promptRect = promptRoot.GetComponent<RectTransform>();
        promptRect.sizeDelta = promptSize;

        var background = promptRoot.GetComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.05f, 0.82f);
        background.sprite = GetPromptSprite();
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(promptRoot.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(-16f, 0f);

        var text = label.GetComponent<Text>();
        text.text = promptText;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = GetReadableFont();
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.material = Graphic.defaultGraphicMaterial;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        if (text.font != null)
        {
            text.font.RequestCharactersInTexture(promptText, text.fontSize, text.fontStyle);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.activeSelf != visible)
        {
            promptRoot.SetActive(visible);
        }
    }

    private static Font GetReadableFont()
    {
        if (readableFont != null)
        {
            return readableFont;
        }

        try
        {
            readableFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
                24);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Book read prompt font lookup failed: {exception.Message}");
        }

        if (readableFont == null)
        {
            readableFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return readableFont;
    }

    private static Sprite GetPromptSprite()
    {
        if (promptSprite != null)
        {
            return promptSprite;
        }

        const int size = 64;
        const int radius = 8;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated Book Prompt Sprite",
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
        promptSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return promptSprite;
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
