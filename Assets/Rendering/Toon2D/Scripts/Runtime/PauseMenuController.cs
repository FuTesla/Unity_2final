using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.Escape;
    public GameObject pauseRoot;
    public TopDownCharacterMotor motor;
    public PlayerInventoryUI inventoryUI;

    private const string PauseHudName = "Pause HUD";
    private const string PauseRootName = "Pause Root";
    private static PauseMenuController activeController;
    private static GameObject sharedPauseRoot;
    private static Font readableFont;
    private static Sprite roundedRectSprite;

    private bool isPaused;
    private bool restoreMotorEnabled;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        if (activeController != null && activeController != this)
        {
            enabled = false;
            return;
        }

        activeController = this;

        if (motor == null)
        {
            motor = GetComponent<TopDownCharacterMotor>();
        }

        if (inventoryUI == null)
        {
            inventoryUI = GetComponent<PlayerInventoryUI>();
        }

        EnsurePauseMenu();
        WirePauseButtons();
        isPaused = false;
        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (isPaused)
        {
            SetPaused(false, true);
        }

        if (activeController == this)
        {
            activeController = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetPaused(!isPaused);
        }
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;

        var activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    public void SetPaused(bool paused)
    {
        SetPaused(paused, false);
    }

    private void SetPaused(bool paused, bool force)
    {
        if (!force && isPaused == paused)
        {
            return;
        }

        isPaused = paused;

        if (paused)
        {
            if (inventoryUI != null && inventoryUI.IsOpen)
            {
                inventoryUI.SetOpen(false);
            }

            previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            restoreMotorEnabled = motor != null && motor.enabled;

            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            AudioListener.pause = false;
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
        }

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(paused);
        }

        if (motor != null)
        {
            motor.enabled = paused ? false : restoreMotorEnabled;
        }
    }

    private void EnsurePauseMenu()
    {
        if (pauseRoot != null)
        {
            sharedPauseRoot = pauseRoot;
            BuildPauseMenuContent(pauseRoot.GetComponent<RectTransform>());
            DestroyDuplicatePauseCanvases(pauseRoot.transform.root.gameObject);
            return;
        }

        if (sharedPauseRoot != null)
        {
            pauseRoot = sharedPauseRoot;
            BuildPauseMenuContent(pauseRoot.GetComponent<RectTransform>());
            DestroyDuplicatePauseCanvases(pauseRoot.transform.root.gameObject);
            return;
        }

        EnsureEventSystem();

        var existingRoot = FindExistingPauseRoot();
        if (existingRoot != null)
        {
            pauseRoot = existingRoot.gameObject;
            sharedPauseRoot = pauseRoot;
            BuildPauseMenuContent(existingRoot);
            DestroyDuplicatePauseCanvases(pauseRoot.transform.root.gameObject);
            return;
        }

        var canvasObject = new GameObject(PauseHudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 180;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateRect(PauseRootName, canvasObject.transform);
        Stretch(root);
        pauseRoot = root.gameObject;
        sharedPauseRoot = pauseRoot;
        BuildPauseMenuContent(root);

        pauseRoot.SetActive(false);
        DestroyDuplicatePauseCanvases(canvasObject);
    }

    private static void BuildPauseMenuContent(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        Stretch(root);
        ClearChildren(root);

        var veil = CreateImage("Pause Veil", root, new Color(0f, 0f, 0f, 0.42f));
        Stretch(veil.rectTransform);
        veil.rectTransform.SetAsFirstSibling();
        veil.raycastTarget = true;

        var panel = CreateRect("Pause Controls", root);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(540f, 430f);

        var title = CreateText("Pause Title", panel, "\u6e38\u620f\u6682\u505c", 48, FontStyle.Bold, Color.white);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        title.rectTransform.sizeDelta = new Vector2(-72f, 72f);

        var hint = CreateText("Pause Hint", panel, "\u6309 ESC \u7ee7\u7eed\u6e38\u620f", 24, FontStyle.Normal, new Color(0.78f, 0.82f, 0.88f, 1f));
        hint.rectTransform.anchorMin = new Vector2(0f, 1f);
        hint.rectTransform.anchorMax = new Vector2(1f, 1f);
        hint.rectTransform.pivot = new Vector2(0.5f, 1f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -126f);
        hint.rectTransform.sizeDelta = new Vector2(-72f, 42f);

        var resume = CreateButton("Resume Button", panel, "\u7ee7\u7eed\u6e38\u620f");
        resume.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -42f);

        var restart = CreateButton("Restart Button", panel, "\u91cd\u65b0\u5f00\u59cb");
        restart.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -118f);

        var quit = CreateButton("Quit Button", panel, "\u9000\u51fa\u6e38\u620f");
        quit.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -194f);
    }

    private void WirePauseButtons()
    {
        if (pauseRoot == null)
        {
            return;
        }

        var resume = FindButton(pauseRoot.transform, "Resume Button");
        if (resume != null)
        {
            resume.onClick.RemoveAllListeners();
            resume.onClick.AddListener(Resume);
        }

        var quit = FindButton(pauseRoot.transform, "Quit Button");
        if (quit != null)
        {
            quit.onClick.RemoveAllListeners();
            quit.onClick.AddListener(QuitGame);
        }

        var restart = FindButton(pauseRoot.transform, "Restart Button");
        if (restart != null)
        {
            restart.onClick.RemoveAllListeners();
            restart.onClick.AddListener(RestartGame);
        }
    }

    private static Button FindButton(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root.GetComponent<Button>();
        }

        foreach (Transform child in root)
        {
            var match = FindButton(child, objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(root.GetChild(i).gameObject);
        }
    }

    private static RectTransform FindExistingPauseRoot()
    {
        foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rect == null || rect.name != PauseRootName)
            {
                continue;
            }

            if (!rect.gameObject.scene.IsValid())
            {
                continue;
            }

            return rect;
        }

        return null;
    }

    private static void DestroyDuplicatePauseCanvases(GameObject keep)
    {
        if (keep == null)
        {
            return;
        }

        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null || canvas.gameObject == keep || canvas.gameObject.name != PauseHudName)
            {
                continue;
            }

            if (!canvas.gameObject.scene.IsValid())
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(canvas.gameObject);
            }
            else
            {
                DestroyImmediate(canvas.gameObject);
            }
        }
    }

    private static Button CreateButton(string objectName, Transform parent, string label)
    {
        var image = CreateImage(objectName, parent, new Color(0.12f, 0.32f, 0.56f, 0.96f));
        image.sprite = GetRoundedRectSprite();
        image.type = Image.Type.Sliced;
        image.material = Graphic.defaultGraphicMaterial;

        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 56f);

        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.32f, 0.56f, 0.96f);
        colors.highlightedColor = new Color(0.18f, 0.44f, 0.72f, 1f);
        colors.pressedColor = new Color(0.08f, 0.22f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var text = CreateText("Label", rect, label, 26, FontStyle.Bold, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        var obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string objectName, Transform parent, string value, int fontSize, FontStyle style, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<Text>();
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = GetReadableFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.material = Graphic.defaultGraphicMaterial;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
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
            name = "Generated Pause Rounded UI Sprite",
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
