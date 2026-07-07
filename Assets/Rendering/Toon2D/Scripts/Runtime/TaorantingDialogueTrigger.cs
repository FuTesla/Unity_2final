using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TaorantingDialogueTrigger : MonoBehaviour
{
    private const string TaorantingTriggerObjectName = "T_taoranting";
    private const string TuitaiTriggerObjectName = "T_tuitai";
    private const string AiwantingTriggerObjectName = "T_aiwanting";
    private const string LevelOneSceneName = "Lvl_1";
    private const string CanvasName = "Taoranting Dialogue HUD";
    private const string DialogueBoxResourcePath = "UI/dialogue_box";

    private static readonly string[] TaorantingLines =
    {
        "\u9676\u7136\u4EAD\uFF0C\u53C8\u79F0\u201C\u6C5F\u4EAD\u201D",
        "\u662F\u6E05\u4EE3\u7684\u540D\u4EAD\uFF0C\u4E5F\u662F\u4E2D\u56FD\u56DB\u5927\u540D\u4EAD\u4E4B\u4E00",
        "\u9676\u7136\u4EAD\u53D6\u5510\u4EE3\u8BD7\u4EBA\u767D\u5C45\u6613\u7684\u8BD7",
        "\u201C\u66F4\u5F85\u83CA\u9EC4\u5BB6\u915D\u719F\uFF0C\u5171\u541B\u4E00\u9189\u4E00\u9676\u7136\u201D",
        "\u4E3A\u4EAD\u9898\u989D\u66F0\u201C\u9676\u7136\u201D"
    };

    private static readonly string[] TuitaiLines =
    {
        "\u9676\u7136\u4EAD\u516C\u56ED\u7684\u5439\u53F0",
        "\u662F\u626C\u5DDE\u7626\u897F\u6E56\u6807\u5FD7\u6027\u201C\u5439\u53F0\u201D\u76841:1\u4EFF\u5EFA\u4EAD",
        "\u5B83\u4E0D\u4EC5\u590D\u5236\u4E86\u539F\u4EAD\u7684\u7CBE\u5DE7\u9020\u578B",
        "\u66F4\u518D\u73B0\u4E86\u53E4\u5178\u56ED\u6797\u4E2D\u201C\u6846\u666F\u201D\u7684\u7ECF\u5178\u624B\u6CD5"
    };

    private static readonly string[] AiwantingLines =
    {
        "\u516C\u56ED\u4E8E2010\u5E74\u5728\u4E2D\u5FC3\u5C9B\u5357\u4FA7\u4EFF\u5EFA\u4E86\u8FD9\u5EA7\u7231\u665A\u4EAD",
        "\u5B83\u5B8C\u5168\u4F9D\u7167\u957F\u6C99\u5CB3\u9E93\u5C71\u539F\u4EAD\u4EFF\u5EFA",
        "\u4FDD\u7559\u4E86\u56DB\u89D2\u516B\u67F1\u3001\u91CD\u6A90\u78A7\u74E6\u3001\u4EAD\u89D2\u98DE\u7FD8\u7684\u7ECF\u5178\u9020\u578B",
        "\u7231\u665A\u4EAD\u662F\u8457\u540D\u7684\u201C\u4E2D\u56FD\u56DB\u5927\u540D\u4EAD\u201D\u4E4B\u4E00",
        "\u5B83\u4E0D\u4EC5\u56E0\u675C\u7267\u7684\u8BD7\u53E5\u5F97\u540D",
        "\u9752\u5E74\u65F6\u4EE3\u7684\u6BDB\u6CFD\u4E1C\u3001\u8521\u548C\u68EE\u7B49\u4EBA\u5E38\u5728\u6B64\u805A\u4F1A\u3001\u8BFB\u4E66",
        "\u5177\u6709\u91CD\u8981\u7684\u7EA2\u8272\u5386\u53F2\u610F\u4E49"
    };

    public string[] dialogueLines =
    {
        "\u9676\u7136\u4EAD\uFF0C\u53C8\u79F0\u201C\u6C5F\u4EAD\u201D",
        "\u662F\u6E05\u4EE3\u7684\u540D\u4EAD\uFF0C\u4E5F\u662F\u4E2D\u56FD\u56DB\u5927\u540D\u4EAD\u4E4B\u4E00",
        "\u9676\u7136\u4EAD\u53D6\u5510\u4EE3\u8BD7\u4EBA\u767D\u5C45\u6613\u7684\u8BD7",
        "\u201C\u66F4\u5F85\u83CA\u9EC4\u5BB6\u915D\u719F\uFF0C\u5171\u541B\u4E00\u9189\u4E00\u9676\u7136\u201D",
        "\u4E3A\u4EAD\u9898\u989D\u66F0\u201C\u9676\u7136\u201D"
    };

    public Vector3 boundsPadding = new Vector3(0.35f, 1.2f, 0.35f);

    private static Font readableFont;
    private static Sprite dialogueBoxSprite;
    private static Sprite roundedRectSprite;

    private TopDownCharacterMotor playerMotor;
    private CharacterController playerController;
    private GameObject dialogueHud;
    private Text dialogueText;
    private int lineIndex;
    private bool hasTriggered;
    private bool isShowing;
    private bool restoreMotorEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool hasCursorSnapshot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= BindSceneTrigger;
        SceneManager.sceneLoaded += BindSceneTrigger;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindInitialScene()
    {
        BindSceneTrigger(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void BindSceneTrigger(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, LevelOneSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var transform in FindObjectsOfType<Transform>(true))
        {
            if (transform == null
                || transform.gameObject.scene != scene
                || !IsSupportedTriggerName(transform.name))
            {
                continue;
            }

            if (transform.GetComponent<TaorantingDialogueTrigger>() == null)
            {
                transform.gameObject.AddComponent<TaorantingDialogueTrigger>();
            }
        }
    }

    private void Awake()
    {
        ApplyDialogueLinesForTriggerName();
    }

    private void Update()
    {
        if (isShowing)
        {
            if (Input.GetMouseButtonDown(0))
            {
                AdvanceDialogue();
            }

            return;
        }

        if (hasTriggered || !IsPlayerInsideTriggerBounds())
        {
            return;
        }

        StartDialogue();
    }

    private void OnDisable()
    {
        if (isShowing)
        {
            EndDialogue(false);
        }
    }

    private bool IsPlayerInsideTriggerBounds()
    {
        ResolvePlayer();
        if (playerMotor == null)
        {
            return false;
        }

        var bounds = GetTriggerBounds();
        return playerController != null
            ? bounds.Intersects(playerController.bounds)
            : bounds.Contains(playerMotor.transform.position);
    }

    private void ResolvePlayer()
    {
        if (playerMotor == null)
        {
            playerMotor = FindObjectOfType<TopDownCharacterMotor>();
        }

        if (playerMotor != null && playerController == null)
        {
            playerController = playerMotor.GetComponent<CharacterController>();
        }
    }

    private Bounds GetTriggerBounds()
    {
        var hasBounds = false;
        var bounds = new Bounds(transform.position, Vector3.one);

        foreach (var collider in GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled)
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

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled)
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

        bounds.Expand(boundsPadding);
        return bounds;
    }

    private void StartDialogue()
    {
        ApplyDialogueLinesForTriggerName();
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            hasTriggered = true;
            return;
        }

        ResolvePlayer();
        EnsureEventSystem();
        EnsureDialogueUi();

        hasTriggered = true;
        isShowing = true;
        lineIndex = 0;
        CaptureCursorState();

        restoreMotorEnabled = playerMotor != null && playerMotor.enabled;
        if (playerMotor != null)
        {
            playerMotor.enabled = false;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (dialogueHud != null)
        {
            dialogueHud.SetActive(true);
        }

        ShowCurrentLine();
    }

    private void ApplyDialogueLinesForTriggerName()
    {
        if (string.Equals(name, TuitaiTriggerObjectName, System.StringComparison.Ordinal))
        {
            dialogueLines = (string[])TuitaiLines.Clone();
            return;
        }

        if (string.Equals(name, AiwantingTriggerObjectName, System.StringComparison.Ordinal))
        {
            dialogueLines = (string[])AiwantingLines.Clone();
            return;
        }

        if (string.Equals(name, TaorantingTriggerObjectName, System.StringComparison.Ordinal))
        {
            dialogueLines = (string[])TaorantingLines.Clone();
        }
    }

    private static bool IsSupportedTriggerName(string objectName)
    {
        return string.Equals(objectName, TaorantingTriggerObjectName, System.StringComparison.Ordinal)
            || string.Equals(objectName, TuitaiTriggerObjectName, System.StringComparison.Ordinal)
            || string.Equals(objectName, AiwantingTriggerObjectName, System.StringComparison.Ordinal);
    }

    private void AdvanceDialogue()
    {
        lineIndex++;
        if (lineIndex >= dialogueLines.Length)
        {
            EndDialogue(true);
            return;
        }

        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        if (dialogueText != null)
        {
            dialogueText.text = dialogueLines[lineIndex];
        }
    }

    private void EndDialogue(bool completed)
    {
        isShowing = false;

        if (dialogueHud != null)
        {
            dialogueHud.SetActive(false);
        }

        if (playerMotor != null)
        {
            playerMotor.enabled = restoreMotorEnabled;
        }

        RestoreCursorState();

        if (completed)
        {
            QuestTrackerUI.RegisterFamousPavilionVisit(name);
        }
    }

    private void EnsureDialogueUi()
    {
        if (dialogueHud != null && dialogueText != null)
        {
            return;
        }

        var staleHud = GameObject.Find(CanvasName);
        if (staleHud != null)
        {
            Destroy(staleHud);
        }

        dialogueHud = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        dialogueHud.SetActive(false);

        var canvas = dialogueHud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 165;

        var scaler = dialogueHud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var root = dialogueHud.GetComponent<RectTransform>();
        Stretch(root);

        var panel = CreateImage("Taoranting Dialogue Panel", root, new Color(1f, 1f, 1f, 0.82f));
        panel.sprite = GetDialogueBoxSprite();
        panel.type = panel.sprite != null && panel.sprite.border != Vector4.zero
            ? Image.Type.Sliced
            : Image.Type.Simple;
        panel.material = Graphic.defaultGraphicMaterial;
        panel.raycastTarget = true;
        panel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, 82f);
        panel.rectTransform.sizeDelta = new Vector2(1260f, 305f);

        dialogueText = CreateText("Taoranting Dialogue Text", panel.rectTransform, string.Empty, 34, FontStyle.Normal, Color.black);
        dialogueText.rectTransform.anchorMin = Vector2.zero;
        dialogueText.rectTransform.anchorMax = Vector2.one;
        dialogueText.rectTransform.offsetMin = new Vector2(140f, 68f);
        dialogueText.rectTransform.offsetMax = new Vector2(-140f, -64f);
        dialogueText.alignment = TextAnchor.MiddleCenter;
    }

    private void CaptureCursorState()
    {
        if (hasCursorSnapshot)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        hasCursorSnapshot = true;
    }

    private void RestoreCursorState()
    {
        if (!hasCursorSnapshot)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        hasCursorSnapshot = false;
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
        text.font = GetReadableFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.material = Graphic.defaultGraphicMaterial;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite GetDialogueBoxSprite()
    {
        if (dialogueBoxSprite != null)
        {
            return dialogueBoxSprite;
        }

        dialogueBoxSprite = Resources.Load<Sprite>(DialogueBoxResourcePath);
        if (dialogueBoxSprite != null)
        {
            return dialogueBoxSprite;
        }

        var texture = Resources.Load<Texture2D>(DialogueBoxResourcePath);
        if (texture != null)
        {
            const float border = 96f;
            dialogueBoxSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        return dialogueBoxSprite != null ? dialogueBoxSprite : GetRoundedRectSprite();
    }

    private static Font GetReadableFont()
    {
        readableFont = GameFontUtility.GetUIFont();
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
            name = "Generated Taoranting Dialogue Sprite",
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
