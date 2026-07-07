using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuestTrackerUI : MonoBehaviour
{
    private const string CanvasName = "Quest Tracker HUD";
    private const string PanelName = "Quest Tracker Panel";
    private const string LevelTwoSceneName = "Lvl_2";
    private const string TaorantingTriggerObjectName = "T_taoranting";
    private const string TuitaiTriggerObjectName = "T_tuitai";
    private const string AiwantingTriggerObjectName = "T_aiwanting";
    private const string FriedEggRewardItemName = "\u714E\u86CB";
    public const string LevelTwoFragmentQuestText = "\u6536\u96C625\u4E2A\u6B8B\u9875";
    public const string FamousPavilionsQuestText = "\u63A2\u5BFB\u516C\u56ED\u91CC\u7684\u4E09\u5EA7\u540D\u4EAD\uFF08\u53EF\u9009\uFF09";

    private static QuestTrackerUI instance;
    private static string storedActiveQuestText = string.Empty;
    private static string storedCompletionText = string.Empty;
    private static bool storedIsCompleted;
    private static bool completionPopupPendingOnSceneLoad;
    private static int lastQuestRestoreSceneHandle = int.MinValue;
    private static bool famousPavilionsQuestIssued;
    private static bool famousPavilionsQuestCompleted;
    private static bool famousPavilionsRewardIssued;
    private static bool visitedTaoranting;
    private static bool visitedTuitai;
    private static bool visitedAiwanting;

    private GameObject canvasObject;
    private Image panelImage;
    private RectTransform panelRect;
    private Text questText;
    private string activeQuestText = string.Empty;
    private string completionText = string.Empty;
    private bool isCompleted;
    private Coroutine slideRoutine;
    private TopDownCharacterMotor playerMotor;
    private CharacterController playerController;
    private QuestObjectiveZone objectiveZone;
    private Transform objectiveTransform;
    private static Font readableFont;
    private static Sprite roundedRectSprite;
    private static readonly Vector2 VisiblePanelPosition = new Vector2(36f, -34f);
    private static readonly Vector2 HiddenPanelPosition = new Vector2(-460f, -34f);

    public static bool HasActiveQuest => !string.IsNullOrEmpty(storedActiveQuestText);
    public static string CurrentQuestText => storedActiveQuestText;
    public static bool IsCompleted => storedIsCompleted;
    public static bool HasFamousPavilionsQuest => famousPavilionsQuestIssued;
    public static bool IsFamousPavilionsQuestCompleted => famousPavilionsQuestCompleted;
    public static int FamousPavilionsVisitedCount => GetFamousPavilionsVisitedCount();

    public static void IssueFamousPavilionsQuest()
    {
        if (famousPavilionsQuestCompleted)
        {
            return;
        }

        famousPavilionsQuestIssued = true;
        TryCompleteFamousPavilionsQuest();
    }

    public static void RegisterFamousPavilionVisit(string triggerObjectName)
    {
        if (string.IsNullOrWhiteSpace(triggerObjectName))
        {
            return;
        }

        if (string.Equals(triggerObjectName, TaorantingTriggerObjectName, System.StringComparison.Ordinal))
        {
            visitedTaoranting = true;
        }
        else if (string.Equals(triggerObjectName, TuitaiTriggerObjectName, System.StringComparison.Ordinal))
        {
            visitedTuitai = true;
        }
        else if (string.Equals(triggerObjectName, AiwantingTriggerObjectName, System.StringComparison.Ordinal))
        {
            visitedAiwanting = true;
        }
        else
        {
            return;
        }

        TryCompleteFamousPavilionsQuest();
    }

    public static void ShowQuest(string questMessage)
    {
        if (string.IsNullOrWhiteSpace(questMessage))
        {
            return;
        }

        EnsureInstance();
        storedActiveQuestText = NormalizeQuestTitle(questMessage);
        storedCompletionText = string.Empty;
        storedIsCompleted = false;
        completionPopupPendingOnSceneLoad = false;
        instance.ApplyStoredState();
        instance.StopSlideRoutine();
        instance.Refresh();
        instance.ResetPanelPosition();
    }

    public static void CompleteQuest(string completionSuffix)
    {
        if (!HasActiveQuest)
        {
            return;
        }

        storedActiveQuestText = NormalizeQuestTitle(storedActiveQuestText);
        storedIsCompleted = true;
        storedCompletionText = completionSuffix ?? string.Empty;
        completionPopupPendingOnSceneLoad = false;

        EnsureInstance();
        instance.ApplyStoredState();
        instance.Refresh();
        instance.BeginCompletedSlideOut();
    }

    public static bool CompleteQuestForSceneTransition(string completionSuffix)
    {
        if (!HasActiveQuest)
        {
            return false;
        }

        storedActiveQuestText = NormalizeQuestTitle(storedActiveQuestText);
        storedIsCompleted = true;
        storedCompletionText = completionSuffix ?? string.Empty;
        completionPopupPendingOnSceneLoad = true;

        if (instance != null)
        {
            instance.ApplyStoredState();
        }

        return true;
    }

    public static void ShowCompletedQuest(string questMessage, string completionSuffix)
    {
        if (string.IsNullOrWhiteSpace(questMessage))
        {
            return;
        }

        EnsureInstance();
        storedActiveQuestText = NormalizeQuestTitle(questMessage);
        storedCompletionText = completionSuffix ?? string.Empty;
        storedIsCompleted = true;
        completionPopupPendingOnSceneLoad = false;
        instance.ApplyStoredState();
        instance.Refresh();
        instance.BeginCompletedSlideOut();
    }

    public static bool IsCurrentQuest(string questText)
    {
        return HasActiveQuest
            && string.Equals(
                NormalizeQuestTitle(storedActiveQuestText),
                NormalizeQuestTitle(questText),
                System.StringComparison.OrdinalIgnoreCase);
    }

    private static void TryCompleteFamousPavilionsQuest()
    {
        if (!famousPavilionsQuestIssued
            || famousPavilionsQuestCompleted
            || GetFamousPavilionsVisitedCount() < 3)
        {
            return;
        }

        famousPavilionsQuestCompleted = true;
        AwardFamousPavilionsReward();
    }

    private static int GetFamousPavilionsVisitedCount()
    {
        var count = 0;
        if (visitedTaoranting)
        {
            count++;
        }

        if (visitedTuitai)
        {
            count++;
        }

        if (visitedAiwanting)
        {
            count++;
        }

        return count;
    }

    private static void AwardFamousPavilionsReward()
    {
        if (famousPavilionsRewardIssued)
        {
            return;
        }

        var player = FindObjectOfType<TopDownCharacterMotor>();
        var inventory = player != null ? player.GetComponent<PlayerInventoryUI>() : null;
        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventoryUI>();
        }

        if (inventory == null)
        {
            Debug.LogWarning("Famous pavilions quest reward could not be added because no PlayerInventoryUI was found.");
            return;
        }

        inventory.AddInventoryItem(FriedEggRewardItemName, 1, null);
        famousPavilionsRewardIssued = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapSceneQuestRestore()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RestoreInitialVisibleQuestAfterSceneLoad()
    {
        RestoreVisibleQuestAfterSceneLoad();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RestoreVisibleQuestAfterSceneLoad();
    }

    private static void RestoreVisibleQuestAfterSceneLoad()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.handle == lastQuestRestoreSceneHandle)
        {
            return;
        }

        lastQuestRestoreSceneHandle = activeScene.handle;
        var shouldIssueLevelTwoQuest = ShouldIssueLevelTwoFragmentQuest(activeScene);
        if (!HasActiveQuest || (storedIsCompleted && !completionPopupPendingOnSceneLoad))
        {
            if (shouldIssueLevelTwoQuest)
            {
                ShowQuest(LevelTwoFragmentQuestText);
            }

            return;
        }

        EnsureInstance();
        instance.ApplyStoredState();
        instance.StopSlideRoutine();
        instance.Refresh();

        if (storedIsCompleted && completionPopupPendingOnSceneLoad)
        {
            completionPopupPendingOnSceneLoad = false;
            instance.BeginCompletedSlideOut();
            if (shouldIssueLevelTwoQuest)
            {
                instance.StartCoroutine(instance.ShowLevelTwoQuestAfterDelay());
            }

            return;
        }

        if (shouldIssueLevelTwoQuest)
        {
            ShowQuest(LevelTwoFragmentQuestText);
            return;
        }

        instance.ResetPanelPosition();
    }

    private static bool ShouldIssueLevelTwoFragmentQuest(Scene activeScene)
    {
        return string.Equals(activeScene.name, LevelTwoSceneName, System.StringComparison.OrdinalIgnoreCase)
            && !IsCurrentQuest(LevelTwoFragmentQuestText);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var existing = FindObjectOfType<QuestTrackerUI>();
        if (existing != null)
        {
            instance = existing;
            instance.EnsureUi();
            instance.ApplyStoredState();
            return;
        }

        var host = new GameObject("QuestTrackerUI");
        instance = host.AddComponent<QuestTrackerUI>();
        instance.EnsureUi();
        instance.ApplyStoredState();
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
        ApplyStoredState();
        Refresh();
    }

    private void Update()
    {
        if (isCompleted || string.IsNullOrWhiteSpace(activeQuestText))
        {
            return;
        }

        if (IsPlayerInsideObjectiveZone())
        {
            ShowCompletedQuest(activeQuestText, "\u4EFB\u52A1\u5B8C\u6210");
        }
    }

    private void EnsureUi()
    {
        if (canvasObject != null && panelImage != null && questText != null)
        {
            return;
        }

        var existingCanvas = GameObject.Find(CanvasName);
        if (existingCanvas != null)
        {
            canvasObject = existingCanvas;
            panelImage = FindImage(existingCanvas.transform, PanelName);
            questText = FindText(existingCanvas.transform, "Quest Tracker Text");
            if (panelImage != null && questText != null)
            {
                panelRect = panelImage.rectTransform;
                return;
            }

            Destroy(existingCanvas);
        }

        canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var root = canvasObject.GetComponent<RectTransform>();
        Stretch(root);

        panelImage = CreateRoundedImage(PanelName, root, new Color(0.07f, 0.09f, 0.12f, 0.95f));
        panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = VisiblePanelPosition;
        panelRect.sizeDelta = new Vector2(420f, 74f);

        questText = CreateText("Quest Tracker Text", panelRect, string.Empty, 25, FontStyle.Bold, Color.white);
        questText.rectTransform.anchorMin = Vector2.zero;
        questText.rectTransform.anchorMax = Vector2.one;
        questText.rectTransform.offsetMin = new Vector2(18f, 12f);
        questText.rectTransform.offsetMax = new Vector2(-18f, -12f);
        questText.alignment = TextAnchor.MiddleLeft;
    }

    private void Refresh()
    {
        EnsureUi();

        var hasQuest = !string.IsNullOrWhiteSpace(activeQuestText);
        if (canvasObject != null)
        {
            canvasObject.SetActive(hasQuest);
        }

        if (!hasQuest)
        {
            return;
        }

        if (panelImage != null)
        {
            panelImage.color = isCompleted
                ? new Color(0.14f, 0.58f, 0.22f, 0.98f)
                : new Color(0.07f, 0.09f, 0.12f, 0.95f);
            panelRect = panelImage.rectTransform;
            panelRect.sizeDelta = isCompleted
                ? new Vector2(420f, 112f)
                : new Vector2(420f, 74f);
        }

        if (questText != null)
        {
            questText.color = Color.white;
            questText.text = isCompleted && !string.IsNullOrWhiteSpace(completionText)
                ? $"{activeQuestText}\n{completionText}"
                : activeQuestText;
        }
    }

    private void BeginCompletedSlideOut()
    {
        StopSlideRoutine();
        slideRoutine = StartCoroutine(SlideCompletedQuestOut());
    }

    private IEnumerator SlideCompletedQuestOut()
    {
        ResetPanelPosition();
        yield return new WaitForSecondsRealtime(1.2f);

        if (panelRect == null)
        {
            yield break;
        }

        const float duration = 0.45f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            panelRect.anchoredPosition = Vector2.Lerp(VisiblePanelPosition, HiddenPanelPosition, t);
            yield return null;
        }

        panelRect.anchoredPosition = HiddenPanelPosition;
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }

        slideRoutine = null;
    }

    private IEnumerator ShowLevelTwoQuestAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.85f);
        ShowQuest(LevelTwoFragmentQuestText);
    }

    private void ResetPanelPosition()
    {
        if (canvasObject != null && !canvasObject.activeSelf)
        {
            canvasObject.SetActive(true);
        }

        if (panelRect == null && panelImage != null)
        {
            panelRect = panelImage.rectTransform;
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = VisiblePanelPosition;
        }
    }

    private void StopSlideRoutine()
    {
        if (slideRoutine == null)
        {
            return;
        }

        StopCoroutine(slideRoutine);
        slideRoutine = null;
    }

    private bool IsPlayerInsideObjectiveZone()
    {
        ResolvePlayer();
        ResolveObjectiveZone();

        if (playerMotor == null || objectiveTransform == null)
        {
            return false;
        }

        if (objectiveZone != null
            && !string.IsNullOrWhiteSpace(objectiveZone.requiredQuestText)
            && !IsCurrentQuest(objectiveZone.requiredQuestText))
        {
            return false;
        }

        var bounds = GetObjectiveBounds();
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

    private void ResolveObjectiveZone()
    {
        if (objectiveTransform != null)
        {
            return;
        }

        objectiveZone = FindObjectOfType<QuestObjectiveZone>();
        if (objectiveZone != null)
        {
            objectiveTransform = objectiveZone.transform;
            return;
        }

        var objectiveObject = GameObject.Find("Quest Objective Zone");
        if (objectiveObject != null)
        {
            objectiveTransform = objectiveObject.transform;
        }
    }

    private Bounds GetObjectiveBounds()
    {
        var bounds = objectiveZone != null
            ? objectiveZone.GetWorldBounds(new Vector3(1.5f, 2f, 1.5f))
            : new Bounds(objectiveTransform.position, new Vector3(7.5f, 6f, 7.5f));

        var renderers = objectiveTransform.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        bounds.Expand(new Vector3(1.5f, 2f, 1.5f));
        return bounds;
    }

    private static Image FindImage(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root.GetComponent<Image>();
        }

        foreach (Transform child in root)
        {
            var result = FindImage(child, objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Text FindText(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root.GetComponent<Text>();
        }

        foreach (Transform child in root)
        {
            var result = FindText(child, objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void ApplyStoredState()
    {
        activeQuestText = storedActiveQuestText;
        completionText = storedCompletionText;
        isCompleted = storedIsCompleted;
    }

    private static string NormalizeQuestTitle(string questText)
    {
        if (string.IsNullOrWhiteSpace(questText))
        {
            return string.Empty;
        }

        return questText.Split('\n')[0].Trim();
    }

    private static Image CreateRoundedImage(string objectName, Transform parent, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
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
        text.alignment = TextAnchor.MiddleCenter;
        text.material = Graphic.defaultGraphicMaterial;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
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
            name = "Generated Quest Rounded UI Sprite",
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
}
