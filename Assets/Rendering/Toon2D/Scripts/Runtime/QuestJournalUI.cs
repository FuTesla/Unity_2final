using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuestJournalUI : MonoBehaviour
{
    private struct QuestEntry
    {
        public string Title;
        public string State;
        public string Description;
    }

    public KeyCode toggleKey = KeyCode.J;

    private const string CanvasName = "Quest Journal HUD";

    private static QuestJournalUI instance;
    private static Font readableFont;
    private static Sprite roundedRectSprite;

    private GameObject journalRoot;
    private RectTransform listRoot;
    private Text detailTitle;
    private Text detailState;
    private Text detailDescription;
    private TopDownCharacterMotor playerMotor;
    private PlayerInventoryUI inventoryUI;
    private bool restoreMotorEnabled;
    private int selectedIndex;
    private bool isOpen;

    public static bool IsOpen => instance != null && instance.isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOnSceneLoad()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var existing = FindObjectOfType<QuestJournalUI>();
        if (existing != null)
        {
            instance = existing;
            instance.EnsureUi();
            return;
        }

        var host = new GameObject("QuestJournalUI");
        instance = host.AddComponent<QuestJournalUI>();
        instance.EnsureUi();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureUi();
        SetOpen(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
            return;
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            SetOpen(false);
        }
    }

    private void SetOpen(bool open)
    {
        if (isOpen == open)
        {
            return;
        }

        isOpen = open;
        EnsureUi();
        ResolvePlayerReferences();

        if (open)
        {
            if (inventoryUI != null && inventoryUI.IsOpen)
            {
                inventoryUI.SetOpen(false);
            }

            restoreMotorEnabled = playerMotor != null && playerMotor.enabled;
            if (playerMotor != null)
            {
                playerMotor.enabled = false;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            RefreshContent();
        }
        else if (playerMotor != null)
        {
            playerMotor.enabled = restoreMotorEnabled;
        }

        if (journalRoot != null)
        {
            journalRoot.SetActive(open);
        }
    }

    private void ResolvePlayerReferences()
    {
        if (playerMotor == null)
        {
            playerMotor = FindObjectOfType<TopDownCharacterMotor>();
        }

        if (inventoryUI == null && playerMotor != null)
        {
            inventoryUI = playerMotor.GetComponent<PlayerInventoryUI>();
        }
    }

    private void EnsureUi()
    {
        if (journalRoot != null && listRoot != null && detailTitle != null)
        {
            return;
        }

        EnsureEventSystem();

        var existingCanvas = GameObject.Find(CanvasName);
        if (existingCanvas != null)
        {
            Destroy(existingCanvas);
        }

        journalRoot = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        journalRoot.SetActive(false);

        var canvas = journalRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 210;

        var scaler = journalRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var root = journalRoot.GetComponent<RectTransform>();
        Stretch(root);

        var veil = CreateImage("Quest Journal Veil", root, new Color(0f, 0f, 0f, 0.5f));
        Stretch(veil.rectTransform);
        veil.raycastTarget = true;

        var frame = CreateRoundedImage("Quest Journal Frame", root, new Color(0.045f, 0.052f, 0.06f, 0.97f));
        frame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        frame.rectTransform.anchoredPosition = Vector2.zero;
        frame.rectTransform.sizeDelta = new Vector2(1160f, 680f);

        var title = CreateText("Quest Journal Title", frame.rectTransform, "\u4EFB\u52A1", 46, FontStyle.Bold, Color.white);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -34f);
        title.rectTransform.sizeDelta = new Vector2(-64f, 64f);

        var detailPanel = CreateRoundedImage("Quest Detail Panel", frame.rectTransform, new Color(0.08f, 0.1f, 0.12f, 0.96f));
        detailPanel.rectTransform.anchorMin = new Vector2(0f, 0f);
        detailPanel.rectTransform.anchorMax = new Vector2(0f, 1f);
        detailPanel.rectTransform.pivot = new Vector2(0f, 0.5f);
        detailPanel.rectTransform.anchoredPosition = new Vector2(36f, -34f);
        detailPanel.rectTransform.sizeDelta = new Vector2(650f, -150f);

        detailTitle = CreateText("Quest Detail Title", detailPanel.rectTransform, string.Empty, 34, FontStyle.Bold, Color.white);
        detailTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        detailTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        detailTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        detailTitle.rectTransform.anchoredPosition = new Vector2(0f, -28f);
        detailTitle.rectTransform.sizeDelta = new Vector2(-56f, 58f);
        detailTitle.alignment = TextAnchor.MiddleLeft;

        detailState = CreateText("Quest Detail State", detailPanel.rectTransform, string.Empty, 22, FontStyle.Bold, new Color(0.76f, 0.9f, 1f, 1f));
        detailState.rectTransform.anchorMin = new Vector2(0f, 1f);
        detailState.rectTransform.anchorMax = new Vector2(1f, 1f);
        detailState.rectTransform.pivot = new Vector2(0.5f, 1f);
        detailState.rectTransform.anchoredPosition = new Vector2(0f, -96f);
        detailState.rectTransform.sizeDelta = new Vector2(-56f, 42f);
        detailState.alignment = TextAnchor.MiddleLeft;

        detailDescription = CreateText("Quest Detail Description", detailPanel.rectTransform, string.Empty, 25, FontStyle.Normal, new Color(0.9f, 0.92f, 0.95f, 1f));
        detailDescription.rectTransform.anchorMin = Vector2.zero;
        detailDescription.rectTransform.anchorMax = Vector2.one;
        detailDescription.rectTransform.offsetMin = new Vector2(28f, 34f);
        detailDescription.rectTransform.offsetMax = new Vector2(-28f, -158f);
        detailDescription.alignment = TextAnchor.UpperLeft;
        detailDescription.verticalOverflow = VerticalWrapMode.Overflow;

        var listPanel = CreateRoundedImage("Quest List Panel", frame.rectTransform, new Color(0.07f, 0.08f, 0.095f, 0.96f));
        listPanel.rectTransform.anchorMin = new Vector2(1f, 0f);
        listPanel.rectTransform.anchorMax = new Vector2(1f, 1f);
        listPanel.rectTransform.pivot = new Vector2(1f, 0.5f);
        listPanel.rectTransform.anchoredPosition = new Vector2(-36f, -34f);
        listPanel.rectTransform.sizeDelta = new Vector2(390f, -150f);

        var listTitle = CreateText("Quest List Title", listPanel.rectTransform, "\u4EFB\u52A1\u5217\u8868", 28, FontStyle.Bold, Color.white);
        listTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        listTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        listTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        listTitle.rectTransform.anchoredPosition = new Vector2(0f, -22f);
        listTitle.rectTransform.sizeDelta = new Vector2(-36f, 46f);

        listRoot = CreateRect("Quest List Items", listPanel.rectTransform);
        listRoot.anchorMin = Vector2.zero;
        listRoot.anchorMax = Vector2.one;
        listRoot.offsetMin = new Vector2(18f, 24f);
        listRoot.offsetMax = new Vector2(-18f, -86f);

        var hint = CreateText("Quest Journal Hint", frame.rectTransform, "J / ESC", 20, FontStyle.Normal, new Color(0.72f, 0.76f, 0.82f, 1f));
        hint.rectTransform.anchorMin = new Vector2(0f, 0f);
        hint.rectTransform.anchorMax = new Vector2(1f, 0f);
        hint.rectTransform.pivot = new Vector2(0.5f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        hint.rectTransform.sizeDelta = new Vector2(-72f, 34f);
    }

    private void RefreshContent()
    {
        var entries = BuildQuestEntries();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, entries.Length - 1);

        ClearChildren(listRoot);

        for (var index = 0; index < entries.Length; index++)
        {
            CreateQuestListButton(index, entries[index]);
        }

        ShowQuestDetails(entries[selectedIndex]);
    }

    private QuestEntry[] BuildQuestEntries()
    {
        var currentTitle = string.IsNullOrWhiteSpace(QuestTrackerUI.CurrentQuestText)
            ? "\u7EE7\u7EED\u5411\u524D\u63A2\u7D22"
            : QuestTrackerUI.CurrentQuestText.Split('\n')[0].Trim();
        var currentState = QuestTrackerUI.IsCompleted
            ? "\u5DF2\u5B8C\u6210"
            : QuestTrackerUI.HasActiveQuest ? "\u8FDB\u884C\u4E2D" : "\u672A\u63A5\u53D6";
        var currentDescription = QuestTrackerUI.IsCompleted
            ? "\u4F60\u5DF2\u7ECF\u7A7F\u8FC7\u524D\u65B9\u7684\u7EFF\u8272\u76EE\u6807\u533A\u57DF\uFF0C\u5F53\u524D\u4EFB\u52A1\u5DF2\u5B8C\u6210\u3002"
            : "\u4E0E NPC \u5BF9\u8BDD\u540E\u63A5\u53D6\u4EFB\u52A1\uFF0C\u7136\u540E\u7A7F\u8FC7\u524D\u65B9\u7684\u7EFF\u8272\u76EE\u6807\u533A\u57DF\u3002";

        return new[]
        {
            new QuestEntry
            {
                Title = currentTitle,
                State = currentState,
                Description = currentDescription
            },
            new QuestEntry
            {
                Title = "\u68C0\u67E5\u6751\u53E3",
                State = "\u793A\u4F8B",
                Description = "\u524D\u5F80\u6751\u53E3\u67E5\u770B\u9053\u8DEF\u60C5\u51B5\uFF0C\u786E\u8BA4\u662F\u5426\u6709\u654C\u4EBA\u51FA\u6CA1\u3002"
            },
            new QuestEntry
            {
                Title = "\u6536\u96C6\u8865\u7ED9",
                State = "\u793A\u4F8B",
                Description = "\u5728\u573A\u666F\u4E2D\u5BFB\u627E\u53EF\u7528\u7684\u8865\u7ED9\u7269\u8D44\uFF0C\u4E3A\u63A5\u4E0B\u6765\u7684\u63A2\u7D22\u505A\u51C6\u5907\u3002"
            },
            new QuestEntry
            {
                Title = "\u8FD4\u56DE NPC \u5904",
                State = "\u793A\u4F8B",
                Description = "\u5B8C\u6210\u63A2\u7D22\u540E\u56DE\u5230 NPC \u9644\u8FD1\uFF0C\u83B7\u53D6\u4E0B\u4E00\u6B65\u6307\u793A\u3002"
            }
        };
    }

    private void CreateQuestListButton(int index, QuestEntry entry)
    {
        var row = CreateRoundedImage($"Quest Row {index}", listRoot, index == selectedIndex
            ? new Color(0.16f, 0.32f, 0.48f, 0.96f)
            : new Color(0.11f, 0.13f, 0.15f, 0.94f));
        row.rectTransform.anchorMin = new Vector2(0f, 1f);
        row.rectTransform.anchorMax = new Vector2(1f, 1f);
        row.rectTransform.pivot = new Vector2(0.5f, 1f);
        row.rectTransform.anchoredPosition = new Vector2(0f, -index * 84f);
        row.rectTransform.sizeDelta = new Vector2(0f, 68f);

        var button = row.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = row.color;
        colors.highlightedColor = new Color(0.2f, 0.4f, 0.58f, 1f);
        colors.pressedColor = new Color(0.08f, 0.2f, 0.32f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var capturedIndex = index;
        button.onClick.AddListener(() =>
        {
            selectedIndex = capturedIndex;
            RefreshContent();
        });

        var label = CreateText("Quest Row Label", row.rectTransform, entry.Title, 22, FontStyle.Bold, Color.white);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16f, 20f);
        label.rectTransform.offsetMax = new Vector2(-16f, 0f);
        label.alignment = TextAnchor.MiddleLeft;

        var state = CreateText("Quest Row State", row.rectTransform, entry.State, 16, FontStyle.Normal, new Color(0.78f, 0.84f, 0.9f, 1f));
        state.rectTransform.anchorMin = Vector2.zero;
        state.rectTransform.anchorMax = Vector2.one;
        state.rectTransform.offsetMin = new Vector2(16f, 0f);
        state.rectTransform.offsetMax = new Vector2(-16f, -36f);
        state.alignment = TextAnchor.MiddleLeft;
    }

    private void ShowQuestDetails(QuestEntry entry)
    {
        detailTitle.text = entry.Title;
        detailState.text = entry.State;
        detailDescription.text = entry.Description;
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

    private static Image CreateRoundedImage(string objectName, Transform parent, Color color)
    {
        var image = CreateImage(objectName, parent, color);
        image.sprite = GetRoundedRectSprite();
        image.type = Image.Type.Sliced;
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
        text.material = Graphic.defaultGraphicMaterial;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (var index = root.childCount - 1; index >= 0; index--)
        {
            Destroy(root.GetChild(index).gameObject);
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
            Debug.LogWarning($"Quest journal font lookup failed: {exception.Message}");
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
        const int radius = 14;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated Quest Journal Rounded UI Sprite",
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
