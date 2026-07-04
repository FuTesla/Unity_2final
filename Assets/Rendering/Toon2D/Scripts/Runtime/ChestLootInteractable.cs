using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChestLootInteractable : MonoBehaviour
{
    private enum ChestState
    {
        Closed,
        OpenWithLoot,
        Empty
    }

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.F;
    public float interactDistance = 3f;
    public Transform player;
    public TopDownCharacterMotor playerMotor;
    public Vector3 promptWorldOffset = new Vector3(1.15f, 1f, 0f);
    public Vector2 promptSize = new Vector2(280f, 54f);

    [Header("Visuals")]
    public GameObject closedChestPrefab;
    public GameObject openLootChestPrefab;
    public GameObject emptyChestPrefab;
    public Vector3 modelLocalPosition = Vector3.zero;
    public Vector3 modelLocalEulerAngles = Vector3.zero;
    public Vector3 modelLocalScale = Vector3.one;

    [Header("Loot")]
    public string itemName = "\u9676\u7136\u4ead\u753b\u96c6\u6b8b\u672c";
    public int itemCount = 5;
    public GameObject itemModelPrefab;

    private const string CanvasName = "Chest Loot Prompt HUD";
    private const string PromptName = "Chest Loot Prompt";
    private const string ClosedVisualName = "Closed Chest Visual";
    private const string OpenLootVisualName = "Open Loot Chest Visual";
    private const string EmptyVisualName = "Empty Chest Visual";

    private static Font readableFont;
    private static Sprite promptSprite;

    private Camera mainCamera;
    private GameObject closedVisual;
    private GameObject openLootVisual;
    private GameObject emptyVisual;
    private GameObject promptRoot;
    private RectTransform promptCanvasRoot;
    private RectTransform promptRect;
    private Text promptLabel;
    private ChestState state = ChestState.Closed;

    private void Awake()
    {
        ResolveReferences();
        EnsurePromptUi();
        EnsureVisuals();
        SetState(ChestState.Closed);
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
        UpdatePrompt();

        if (!Input.GetKeyDown(interactKey) || !IsPlayerInRange())
        {
            return;
        }

        if (state == ChestState.Closed)
        {
            SetState(ChestState.OpenWithLoot);
            return;
        }

        if (state == ChestState.OpenWithLoot)
        {
            TryCollectLoot();
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

        if (player == null && playerMotor != null)
        {
            player = playerMotor.transform;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private bool IsPlayerInRange()
    {
        if (player == null)
        {
            return false;
        }

        var playerPosition = player.position;
        var chestPosition = transform.position;
        playerPosition.y = 0f;
        chestPosition.y = 0f;
        return (playerPosition - chestPosition).sqrMagnitude <= interactDistance * interactDistance;
    }

    private void TryCollectLoot()
    {
        var inventory = player != null ? player.GetComponent<PlayerInventoryUI>() : FindObjectOfType<PlayerInventoryUI>();
        if (inventory == null)
        {
            Debug.LogWarning("Chest loot could not be collected because no PlayerInventoryUI was found.");
            return;
        }

        inventory.AddInventoryItem(itemName, itemCount, itemModelPrefab);
        SetState(ChestState.Empty);
    }

    private void SetState(ChestState nextState)
    {
        state = nextState;
        RefreshVisualState();
        RefreshPromptText();
    }

    private void EnsureVisuals()
    {
        closedVisual = FindVisual(ClosedVisualName);
        if (closedVisual == null)
        {
            var editorPreview = transform.Find("Chest Editor Preview");
            if (editorPreview != null)
            {
                closedVisual = editorPreview.gameObject;
                closedVisual.name = ClosedVisualName;
            }
        }

        if (closedVisual == null)
        {
            closedVisual = CreateVisual(closedChestPrefab, ClosedVisualName);
        }

        openLootVisual = FindVisual(OpenLootVisualName);
        if (openLootVisual == null)
        {
            openLootVisual = CreateVisual(openLootChestPrefab, OpenLootVisualName);
        }

        emptyVisual = FindVisual(EmptyVisualName);
        if (emptyVisual == null)
        {
            emptyVisual = CreateVisual(emptyChestPrefab, EmptyVisualName);
        }

        ApplyVisualTransform(closedVisual);
        ApplyVisualTransform(openLootVisual);
        ApplyVisualTransform(emptyVisual);
    }

    private GameObject FindVisual(string visualName)
    {
        var child = transform.Find(visualName);
        return child != null ? child.gameObject : null;
    }

    private GameObject CreateVisual(GameObject prefab, string visualName)
    {
        if (prefab == null)
        {
            return null;
        }

        var visual = Instantiate(prefab, transform);
        visual.name = visualName;
        ApplyVisualTransform(visual);
        visual.SetActive(false);
        return visual;
    }

    private void ApplyVisualTransform(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        visual.transform.localPosition = modelLocalPosition;
        visual.transform.localRotation = Quaternion.Euler(modelLocalEulerAngles);
        visual.transform.localScale = modelLocalScale;
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();
        SetVisualActive(closedVisual, state == ChestState.Closed);
        SetVisualActive(openLootVisual, state == ChestState.OpenWithLoot);
        SetVisualActive(emptyVisual, state == ChestState.Empty);
    }

    private static void SetVisualActive(GameObject visual, bool active)
    {
        if (visual != null && visual.activeSelf != active)
        {
            visual.SetActive(active);
        }
    }

    private void UpdatePrompt()
    {
        var shouldShow = state != ChestState.Empty && IsPlayerInRange();
        SetPromptVisible(shouldShow);
        if (shouldShow)
        {
            RefreshPromptText();
            UpdatePromptPosition();
        }
    }

    private void RefreshPromptText()
    {
        if (promptLabel == null)
        {
            return;
        }

        promptLabel.text = state == ChestState.Closed
            ? "\u6309\u4e0bF\u6253\u5f00\u5b9d\u7bb1"
            : $"\u6309\u4e0bF\u62fe\u53d6 {itemName}*{itemCount}";

        if (promptLabel.font != null)
        {
            promptLabel.font.RequestCharactersInTexture(promptLabel.text, promptLabel.fontSize, promptLabel.fontStyle);
        }
    }

    private void UpdatePromptPosition()
    {
        if (mainCamera == null || promptCanvasRoot == null || promptRect == null)
        {
            return;
        }

        var right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        var worldPosition = transform.position + right * promptWorldOffset.x + Vector3.up * promptWorldOffset.y + transform.forward * promptWorldOffset.z;
        var screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            SetPromptVisible(false);
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(promptCanvasRoot, screenPosition, null, out var localPoint))
        {
            promptRect.anchoredPosition = localPoint;
        }
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
            canvas.sortingOrder = 190;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        promptCanvasRoot = canvasObject.GetComponent<RectTransform>();
        Stretch(promptCanvasRoot);

        var existing = canvasObject.transform.Find(PromptName);
        if (existing != null)
        {
            promptRoot = existing.gameObject;
            promptRect = promptRoot.GetComponent<RectTransform>();
            promptLabel = promptRoot.GetComponentInChildren<Text>(true);
            return;
        }

        promptRoot = new GameObject(PromptName, typeof(RectTransform), typeof(Image));
        promptRoot.transform.SetParent(canvasObject.transform, false);
        promptRect = promptRoot.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0f, 0.5f);
        promptRect.sizeDelta = promptSize;

        var background = promptRoot.GetComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.05f, 0.86f);
        background.sprite = GetPromptSprite();
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(promptRoot.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(-16f, 0f);

        promptLabel = label.GetComponent<Text>();
        promptLabel.alignment = TextAnchor.MiddleCenter;
        promptLabel.font = GetReadableFont();
        promptLabel.fontSize = 22;
        promptLabel.fontStyle = FontStyle.Bold;
        promptLabel.color = Color.white;
        promptLabel.material = Graphic.defaultGraphicMaterial;
        promptLabel.raycastTarget = false;
        promptLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        promptLabel.verticalOverflow = VerticalWrapMode.Overflow;
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
            Debug.LogWarning($"Chest prompt font lookup failed: {exception.Message}");
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
            name = "Generated Chest Prompt Sprite",
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
