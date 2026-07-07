using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class OpeningMenuController : MonoBehaviour
{
    private const string CanvasName = "Opening Menu HUD";
    private const string RootName = "Opening Menu Root";
    private const string StartButtonName = "Opening Start Button";
    private const string QuitButtonName = "Opening Quit Button";

    public string playerNameContains = "Medieval";
    public bool showOnStart = true;
    public string startSceneName = "Lvl_1";
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private static Font readableFont;
    private static Sprite buttonSprite;
    private static bool skipNextOpeningMenu;

    private Camera openingCamera;
    private RenderTexture backgroundTexture;
    private GameObject menuRoot;
    private TopDownCharacterMotor playerMotor;
    private PlayerInventoryUI inventoryUI;
    private PauseMenuController pauseMenu;
    private bool previousMotorEnabled;
    private bool previousInventoryEnabled;
    private bool previousPauseEnabled;
    private bool hasPreviousMotorState;
    private bool hasPreviousInventoryState;
    private bool hasPreviousPauseState;
    private bool gameplayBlocked;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private AudioListener openingAudioListener;

    private void Awake()
    {
        openingCamera = GetComponent<Camera>();
        openingAudioListener = GetComponent<AudioListener>();
        if (openingAudioListener != null)
        {
            openingAudioListener.enabled = false;
        }

        BuildMenu();

        if (showOnStart && !ShouldBypassOpeningMenu())
        {
            ShowMenu();
        }
        else
        {
            HideMenu(false);
        }
    }

    private void Update()
    {
        if (!gameplayBlocked)
        {
            return;
        }

        CacheGameplayState();
        ApplyGameplayBlocked(true);

        if (openingCamera != null && !openingCamera.enabled)
        {
            openingCamera.enabled = true;
        }
    }

    public static void SkipNextOpeningMenu()
    {
        skipNextOpeningMenu = true;
    }

    private static bool ConsumeSkipNextOpeningMenu()
    {
        if (!skipNextOpeningMenu)
        {
            return false;
        }

        skipNextOpeningMenu = false;
        return true;
    }

    private void OnDestroy()
    {
        if (backgroundTexture != null)
        {
            if (openingCamera != null && openingCamera.targetTexture == backgroundTexture)
            {
                openingCamera.targetTexture = null;
            }

            backgroundTexture.Release();
            Destroy(backgroundTexture);
            backgroundTexture = null;
        }
    }

    public void StartGame()
    {
        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            HideMenu(true);
            return;
        }

        SceneDirectControlRouter.RequestDirectControl(startSceneName);
        SceneManager.LoadScene(startSceneName);
    }

    private bool ShouldBypassOpeningMenu()
    {
        if (SceneDirectControlRouter.ShouldBypassOpeningMenuForScene(gameObject.scene.name))
        {
            ConsumeSkipNextOpeningMenu();
            return true;
        }

        return ConsumeSkipNextOpeningMenu();
    }

    public void ForceCloseForDirectControl()
    {
        gameplayBlocked = false;
        HideMenu(true);
        enabled = false;
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

    private void ShowMenu()
    {
        gameplayBlocked = true;
        CacheGameplayState();
        ApplyGameplayBlocked(true);

        EnsureBackgroundTexture();
        openingCamera.enabled = true;
        if (openingAudioListener != null)
        {
            openingAudioListener.enabled = false;
        }

        if (menuRoot != null)
        {
            menuRoot.SetActive(true);
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HideMenu(bool restoreGameplay)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        if (openingCamera != null)
        {
            if (openingCamera.targetTexture == backgroundTexture)
            {
                openingCamera.targetTexture = null;
            }

            GameplayCameraExposureUtility.ApplyGameplayDefaults(openingCamera, true);
            openingCamera.enabled = false;
        }

        if (restoreGameplay)
        {
            gameplayBlocked = false;
            ApplyGameplayBlocked(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void CacheGameplayState()
    {
        var player = FindPlayer();
        var foundMotor = player != null ? player.GetComponent<TopDownCharacterMotor>() : null;
        if (foundMotor == null)
        {
            foundMotor = FindObjectOfType<TopDownCharacterMotor>();
            player = foundMotor != null ? foundMotor.gameObject : player;
        }

        if (playerMotor == null && foundMotor != null)
        {
            playerMotor = foundMotor;
        }

        if (inventoryUI == null && player != null)
        {
            inventoryUI = player.GetComponent<PlayerInventoryUI>();
        }

        if (pauseMenu == null && player != null)
        {
            pauseMenu = player.GetComponent<PauseMenuController>();
        }

        if (playerMotor != null && !hasPreviousMotorState)
        {
            previousMotorEnabled = playerMotor.enabled;
            hasPreviousMotorState = true;
        }

        if (inventoryUI != null && !hasPreviousInventoryState)
        {
            previousInventoryEnabled = inventoryUI.enabled;
            hasPreviousInventoryState = true;
        }

        if (pauseMenu != null && !hasPreviousPauseState)
        {
            previousPauseEnabled = pauseMenu.enabled;
            hasPreviousPauseState = true;
        }
    }

    private void ApplyGameplayBlocked(bool blocked)
    {
        if (playerMotor != null)
        {
            playerMotor.enabled = blocked ? false : hasPreviousMotorState && previousMotorEnabled;
        }

        if (inventoryUI != null)
        {
            inventoryUI.enabled = blocked ? false : hasPreviousInventoryState && previousInventoryEnabled;
            if (blocked && inventoryUI.IsOpen)
            {
                inventoryUI.SetOpen(false);
            }
        }

        if (pauseMenu != null)
        {
            pauseMenu.enabled = blocked ? false : hasPreviousPauseState && previousPauseEnabled;
        }
    }

    private GameObject FindPlayer()
    {
        if (!string.IsNullOrWhiteSpace(playerNameContains))
        {
            foreach (var transform in FindObjectsOfType<Transform>())
            {
                if (transform.name.IndexOf(playerNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return transform.root.gameObject;
                }
            }
        }

        var motor = FindObjectOfType<TopDownCharacterMotor>();
        return motor != null ? motor.gameObject : null;
    }

    private void BuildMenu()
    {
        EnsureEventSystem();
        EnsureBackgroundTexture();

        var existing = GameObject.Find(CanvasName);
        if (existing != null)
        {
            Destroy(existing);
        }

        var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 220;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateRect(RootName, canvasObject.transform);
        Stretch(root);
        menuRoot = root.gameObject;

        var background = new GameObject("Opening Camera Background", typeof(RectTransform), typeof(RawImage));
        background.transform.SetParent(root, false);
        var backgroundImage = background.GetComponent<RawImage>();
        backgroundImage.texture = backgroundTexture;
        backgroundImage.color = Color.white;
        backgroundImage.raycastTarget = false;
        Stretch(backgroundImage.rectTransform);

        var veil = CreateImage("Opening Veil", root, new Color(0.02f, 0.03f, 0.04f, 0.28f));
        Stretch(veil.rectTransform);
        veil.raycastTarget = false;

        var controls = CreateRect("Opening Controls", root);
        controls.anchorMin = new Vector2(0.5f, 0f);
        controls.anchorMax = new Vector2(0.5f, 0f);
        controls.pivot = new Vector2(0.5f, 0f);
        controls.anchoredPosition = new Vector2(0f, 96f);
        controls.sizeDelta = new Vector2(380f, 164f);

        var startButton = CreateButton(StartButtonName, controls, "\u5f00\u59cb\u6e38\u620f");
        startButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 96f);
        startButton.onClick.AddListener(StartGame);

        var quitButton = CreateButton(QuitButtonName, controls, "\u9000\u51fa\u6e38\u620f");
        quitButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 24f);
        quitButton.onClick.AddListener(QuitGame);
    }

    private void EnsureBackgroundTexture()
    {
        if (openingCamera == null)
        {
            openingCamera = GetComponent<Camera>();
        }

        var width = Mathf.Max(1280, Screen.width);
        var height = Mathf.Max(720, Screen.height);
        if (backgroundTexture == null || backgroundTexture.width != width || backgroundTexture.height != height)
        {
            if (backgroundTexture != null)
            {
                backgroundTexture.Release();
                Destroy(backgroundTexture);
            }

            backgroundTexture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                name = "Opening Camera Background",
                antiAliasing = 2,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear
            };
            backgroundTexture.Create();
        }

        GameplayCameraExposureUtility.ApplyGameplayDefaults(openingCamera, true);
        openingCamera.targetTexture = backgroundTexture;
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

    private static Button CreateButton(string objectName, Transform parent, string label)
    {
        var image = CreateImage(objectName, parent, new Color(0.08f, 0.22f, 0.26f, 0.88f));
        image.sprite = GetButtonSprite();
        image.type = Image.Type.Sliced;
        image.material = Graphic.defaultGraphicMaterial;

        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, 58f);

        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.22f, 0.26f, 0.88f);
        colors.highlightedColor = new Color(0.13f, 0.34f, 0.4f, 0.96f);
        colors.pressedColor = new Color(0.04f, 0.14f, 0.17f, 0.96f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var text = CreateText("Label", rect, label, 28, FontStyle.Bold, Color.white);
        Stretch(text.rectTransform);
        return button;
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
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        if (text.font != null)
        {
            text.font.RequestCharactersInTexture(value, fontSize, style);
        }

        return text;
    }

    private static Font GetReadableFont()
    {
        readableFont = GameFontUtility.GetUIFont();
        return readableFont;
    }

    private static Sprite GetButtonSprite()
    {
        if (buttonSprite != null)
        {
            return buttonSprite;
        }

        const int size = 64;
        const int radius = 10;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated Opening Button Sprite",
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
        buttonSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return buttonSprite;
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
