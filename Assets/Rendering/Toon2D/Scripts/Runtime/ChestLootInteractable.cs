using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChestLootInteractable : MonoBehaviour
{
    private const string LevelTwoSceneName = "Lvl_2";

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.F;
    public float interactDistance = 3f;
    public Transform player;
    public TopDownCharacterMotor playerMotor;
    public Vector3 promptWorldOffset = new Vector3(0f, 0.08f, 0f);
    public Vector2 promptSize = new Vector2(280f, 54f);
    public float promptScreenVerticalOffset = 18f;

    [Header("Visuals")]
    public GameObject closedChestPrefab;
    public Vector3 modelLocalPosition = Vector3.zero;
    public Vector3 modelLocalEulerAngles = Vector3.zero;
    public Vector3 modelLocalScale = Vector3.one;

    [Header("Loot")]
    public string itemName = "\u9676\u7136\u4ead\u753b\u96c6\u6b8b\u672c";
    public int itemCount = 5;
    public GameObject itemModelPrefab;
    public string bonusItemName = "\u714e\u86cb";
    public int bonusItemCount = 2;
    public GameObject bonusItemModelPrefab;

    private const string CanvasName = "Chest Loot Prompt HUD";
    private const string PromptName = "Chest Loot Prompt";
    private const string ClosedVisualName = "Closed Chest Visual";
    private const string OpenLootVisualName = "Open Loot Chest Visual";
    private const string EmptyVisualName = "Empty Chest Visual";

    private static Font readableFont;
    private static Sprite promptSprite;

    private Camera mainCamera;
    private GameObject closedVisual;
    private GameObject promptRoot;
    private RectTransform promptCanvasRoot;
    private RectTransform promptRect;
    private Text promptLabel;
    private bool hasCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapSceneChestBinding()
    {
        SceneManager.sceneLoaded -= BindLooseLevelTwoChests;
        SceneManager.sceneLoaded += BindLooseLevelTwoChests;
    }

    private static void BindLooseLevelTwoChests(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, LevelTwoSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var transform in FindObjectsOfType<Transform>(true))
        {
            if (transform == null
                || transform.gameObject.scene != scene
                || !LooksLikeChestObject(transform.name)
                || transform.GetComponentInParent<ChestLootInteractable>() != null)
            {
                continue;
            }

            transform.gameObject.AddComponent<ChestLootInteractable>();
        }
    }

    private static bool LooksLikeChestObject(string objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName)
            && (objectName.IndexOf("Chest_Closed", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Loot Chest", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("\u5B9D\u7BB1", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void Awake()
    {
        ResolveReferences();
        EnsurePromptUi();
        EnsureVisuals();
        RefreshVisualState();
        RefreshPromptText();
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

        if (!hasCollected)
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
        inventory.AddInventoryItem(bonusItemName, bonusItemCount, bonusItemModelPrefab);
        hasCollected = true;
        RefreshVisualState();
        RefreshPromptText();
        UpdatePrompt();
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

        ApplyVisualTransform(closedVisual);
        DisableLegacyVisual(OpenLootVisualName);
        DisableLegacyVisual(EmptyVisualName);
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
        SetVisualActive(closedVisual, true);
        DisableLegacyVisual(OpenLootVisualName);
        DisableLegacyVisual(EmptyVisualName);
    }

    private void DisableLegacyVisual(string visualName)
    {
        var visual = FindVisual(visualName);
        if (visual != null)
        {
            SetVisualActive(visual, false);
        }
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
        var shouldShow = !hasCollected && IsPlayerInRange();
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

        promptLabel.text = "\u6309\u4e0bF\u6253\u5f00\u5b9d\u7bb1";

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

        if (!TryGetPromptScreenPosition(out var screenPosition))
        {
            SetPromptVisible(false);
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(promptCanvasRoot, screenPosition, null, out var localPoint))
        {
            promptRect.anchoredPosition = localPoint;
        }
    }

    private bool TryGetPromptScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;
        if (!TryGetVisualBounds(closedVisual != null ? closedVisual.transform : transform, out var bounds))
        {
            var fallback = mainCamera.WorldToScreenPoint(transform.position + promptWorldOffset);
            if (fallback.z <= 0f)
            {
                return false;
            }

            screenPosition = new Vector2(fallback.x, fallback.y + promptScreenVerticalOffset);
            return true;
        }

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var hasVisibleCorner = false;
        var center = bounds.center + promptWorldOffset;
        var extents = bounds.extents;

        for (var x = -1; x <= 1; x += 2)
        {
            for (var y = -1; y <= 1; y += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    var corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    var projected = mainCamera.WorldToScreenPoint(corner);
                    if (projected.z <= 0f)
                    {
                        continue;
                    }

                    hasVisibleCorner = true;
                    min = Vector2.Min(min, projected);
                    max = Vector2.Max(max, projected);
                }
            }
        }

        if (!hasVisibleCorner)
        {
            return false;
        }

        screenPosition = new Vector2((min.x + max.x) * 0.5f, max.y + promptScreenVerticalOffset);
        return true;
    }

    private static bool TryGetVisualBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root != null ? root.position : Vector3.zero, Vector3.zero);
        if (root == null)
        {
            return false;
        }

        var hasBounds = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        foreach (var collider in root.GetComponentsInChildren<Collider>(false))
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
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
        DisableLegacySharedPrompt(canvasObject.transform);

        var instancePromptName = $"{PromptName} {GetInstanceID()}";
        var existing = canvasObject.transform.Find(instancePromptName);
        if (existing != null)
        {
            promptRoot = existing.gameObject;
        }
        else
        {
            promptRoot = new GameObject(instancePromptName, typeof(RectTransform), typeof(Image));
            promptRoot.transform.SetParent(canvasObject.transform, false);
        }

        promptRect = promptRoot.GetComponent<RectTransform>();
        promptLabel = promptRoot.GetComponentInChildren<Text>(true);

        if (promptLabel == null)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(promptRoot.transform, false);
            promptLabel = label.GetComponent<Text>();
        }

        ConfigurePromptUi();
    }

    private void ConfigurePromptUi()
    {
        if (promptRect == null)
        {
            return;
        }

        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.sizeDelta = promptSize;
        promptRect.localScale = Vector3.one;

        var background = promptRoot != null ? promptRoot.GetComponent<Image>() : null;
        if (background != null)
        {
            background.color = new Color(0.04f, 0.05f, 0.05f, 0.86f);
            background.sprite = GetPromptSprite();
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;
        }

        ConfigurePromptLabel();
    }

    private static void DisableLegacySharedPrompt(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return;
        }

        var oldPrompt = canvasTransform.Find(PromptName);
        if (oldPrompt != null)
        {
            oldPrompt.gameObject.SetActive(false);
        }
    }

    private void ConfigurePromptLabel()
    {
        if (promptLabel == null)
        {
            return;
        }

        var labelRect = promptLabel.rectTransform;
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(-16f, 0f);
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
        readableFont = GameFontUtility.GetUIFont();
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
