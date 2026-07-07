using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BookReadPortal : MonoBehaviour
{
    private static readonly Vector3 LevelOneReturnSpawnPosition = new Vector3(175f, 10f, 105f);

    public Transform bookTarget;
    public string targetSceneName = "Lvl_1";
    public KeyCode interactKey = KeyCode.F;
    public float interactDistance = 3.0f;
    public Vector3 promptWorldOffset = new Vector3(1.15f, 0.75f, 0f);
    public Vector2 promptSize = new Vector2(220f, 50f);
    public string promptText = "\u6309\u4e0bF\u9605\u8bfb";
    public bool completeQuestWhenLoadingLevelTwo = true;
    public string levelTwoQuestCompletionText = "\u4EFB\u52A1\u5B8C\u6210";
    public bool requireFragmentsForLevelOneReturn = true;
    public string requiredFragmentItemName = PlayerInventoryUI.TaorantingAlbumItemName;
    public int requiredFragmentCount = 25;
    public string insufficientFragmentsMessage = "\u6B8B\u9875\u4E0D\u8DB3";
    public KeyCode backdoorKey = KeyCode.R;
    public float backdoorInputWindow = 6f;

    private const string CanvasName = "Book Read Prompt HUD";
    private const string PromptName = "Book Read Prompt";
    private const string NoticeCanvasName = "Book Requirement Notice HUD";
    private const string NoticeTextName = "Book Requirement Notice Text";

    private static Font readableFont;
    private static Sprite promptSprite;

    private TopDownCharacterMotor playerMotor;
    private Camera mainCamera;
    private GameObject promptRoot;
    private RectTransform promptRect;
    private Text requirementNoticeText;
    private CanvasGroup requirementNoticeCanvasGroup;
    private Coroutine requirementNoticeRoutine;
    private float backdoorArmedUntil = -1f;

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

        if (inRange
            && RequiresFragmentsForTargetScene()
            && Time.unscaledTime <= backdoorArmedUntil
            && Input.GetKeyDown(backdoorKey))
        {
            backdoorArmedUntil = -1f;
            TeleportToTargetScene(false);
            return;
        }

        if (inRange && Input.GetKeyDown(interactKey))
        {
            if (RequiresFragmentsForTargetScene() && !HasRequiredFragments())
            {
                ArmBackdoorInput();
                ShowRequirementNotice();
                return;
            }

            backdoorArmedUntil = -1f;
            TeleportToTargetScene(true);
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

    private bool RequiresFragmentsForTargetScene()
    {
        return requireFragmentsForLevelOneReturn
            && string.Equals(targetSceneName, "Lvl_1", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool HasRequiredFragments()
    {
        var inventory = playerMotor != null ? playerMotor.GetComponent<PlayerInventoryUI>() : null;
        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventoryUI>();
        }

        return inventory != null
            && inventory.GetInventoryItemCount(requiredFragmentItemName) >= Mathf.Max(1, requiredFragmentCount);
    }

    private void ArmBackdoorInput()
    {
        backdoorArmedUntil = Time.unscaledTime + Mathf.Max(0.1f, backdoorInputWindow);
    }

    private void TeleportToTargetScene(bool completeQuestIfRequired)
    {
        if (completeQuestIfRequired && RequiresFragmentsForTargetScene())
        {
            QuestTrackerUI.ShowCompletedQuest(QuestTrackerUI.LevelTwoFragmentQuestText, levelTwoQuestCompletionText);
        }

        if (completeQuestIfRequired
            && completeQuestWhenLoadingLevelTwo
            && string.Equals(targetSceneName, "Lvl_2", System.StringComparison.OrdinalIgnoreCase))
        {
            QuestTrackerUI.CompleteQuestForSceneTransition(levelTwoQuestCompletionText);
        }

        if (string.Equals(targetSceneName, "Lvl_1", System.StringComparison.OrdinalIgnoreCase))
        {
            SceneDirectControlRouter.RequestDirectControl(targetSceneName, LevelOneReturnSpawnPosition);
        }
        else
        {
            SceneDirectControlRouter.RequestDirectControl(targetSceneName);
        }

        SceneManager.LoadScene(targetSceneName);
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

    private void ShowRequirementNotice()
    {
        EnsureRequirementNoticeUi();
        if (requirementNoticeText == null || requirementNoticeCanvasGroup == null)
        {
            return;
        }

        requirementNoticeText.text = insufficientFragmentsMessage;
        requirementNoticeCanvasGroup.alpha = 1f;
        requirementNoticeCanvasGroup.gameObject.SetActive(true);

        if (requirementNoticeRoutine != null)
        {
            StopCoroutine(requirementNoticeRoutine);
        }

        requirementNoticeRoutine = StartCoroutine(HideRequirementNoticeAfterDelay());
    }

    private void EnsureRequirementNoticeUi()
    {
        if (requirementNoticeText != null && requirementNoticeCanvasGroup != null)
        {
            return;
        }

        var canvasObject = GameObject.Find(NoticeCanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(NoticeCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 235;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        requirementNoticeCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        requirementNoticeCanvasGroup.interactable = false;
        requirementNoticeCanvasGroup.blocksRaycasts = false;

        var textTransform = canvasObject.transform.Find(NoticeTextName);
        if (textTransform != null)
        {
            requirementNoticeText = textTransform.GetComponent<Text>();
        }

        if (requirementNoticeText == null)
        {
            var label = new GameObject(NoticeTextName, typeof(RectTransform), typeof(Text));
            label.transform.SetParent(canvasObject.transform, false);
            requirementNoticeText = label.GetComponent<Text>();

            var outline = requirementNoticeText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.78f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        requirementNoticeText.alignment = TextAnchor.MiddleLeft;
        requirementNoticeText.font = GetReadableFont();
        requirementNoticeText.fontSize = 30;
        requirementNoticeText.fontStyle = FontStyle.Bold;
        requirementNoticeText.color = new Color(1f, 0.86f, 0.45f, 1f);
        requirementNoticeText.material = Graphic.defaultGraphicMaterial;
        requirementNoticeText.raycastTarget = false;
        requirementNoticeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        requirementNoticeText.verticalOverflow = VerticalWrapMode.Overflow;

        var rect = requirementNoticeText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(36f, -118f);
        rect.sizeDelta = new Vector2(420f, 58f);

        requirementNoticeCanvasGroup.gameObject.SetActive(false);
    }

    private IEnumerator HideRequirementNoticeAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2.2f);

        if (requirementNoticeCanvasGroup != null)
        {
            requirementNoticeCanvasGroup.alpha = 0f;
            requirementNoticeCanvasGroup.gameObject.SetActive(false);
        }

        requirementNoticeRoutine = null;
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
