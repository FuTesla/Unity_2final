using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class PlayerInventoryUI : MonoBehaviour
{
    private const int PreviewLayer = 31;
    private const string WeaponControlsName = "Inventory Weapon Controls";
    private const string BackgroundBlurVeilName = "Inventory Background Blur Veil";
    private const string BackgroundBlurVolumeName = "Inventory Background Blur Volume";
    private static bool isBuildingCharacterPreview;
    private static Font readableFont;
    private static Sprite roundedRectSprite;
    private const string BackpackItemsName = "Runtime Backpack Items";
    private const string EmptyBackpackHintName = "Runtime Backpack Empty Hint";
    private const string BackpackItemPreviewCameraName = "Runtime Backpack Item Preview Camera";
    private const string BackpackItemPreviewStageName = "Runtime Backpack Item Preview Stage";
    private const string BackpackItemPreviewLightName = "Runtime Backpack Item Preview Light";

    public KeyCode toggleKey = KeyCode.Tab;
    public GameObject inventoryRoot;
    public TopDownCharacterMotor motor;
    public Animator animator;
    public RawImage characterPreviewImage;
    public Camera characterPreviewCamera;
    public Transform characterPreviewStage;

    private PlayerHealth health;
    private RenderTexture characterPreviewTexture;
    private GameObject characterPreviewInstance;
    private Animator characterPreviewAnimator;
    private Button unarmedButton;
    private Button swordButton;
    private Button handgunButton;
    private Image backgroundBlurVeil;
    private Volume backgroundBlurVolume;
    private VolumeProfile backgroundBlurProfile;
    private Component mainCameraAdditionalData;
    private readonly List<InventoryItemEntry> inventoryItems = new List<InventoryItemEntry>();
    private RectTransform backpackItemsRoot;
    private Camera backpackItemPreviewCamera;
    private Transform backpackItemPreviewStage;
    private Light backpackItemPreviewLight;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool hasCursorSnapshot;
    private bool previousPostProcessingEnabled;
    private bool hasPostProcessingSnapshot;
    private bool isOpen;

    public bool IsOpen => isOpen;

    public void AddInventoryItem(string itemName, int count, GameObject modelPrefab)
    {
        if (string.IsNullOrWhiteSpace(itemName) || count <= 0)
        {
            return;
        }

        var existing = inventoryItems.Find(item => item.Name == itemName);
        if (existing != null)
        {
            existing.Count += count;
            if (existing.ModelPrefab == null)
            {
                existing.ModelPrefab = modelPrefab;
            }
        }
        else
        {
            inventoryItems.Add(new InventoryItemEntry(itemName, count, modelPrefab));
        }

        EnsureInventoryLayout();
    }

    private void Awake()
    {
        if (isBuildingCharacterPreview)
        {
            enabled = false;
            return;
        }

        if (motor == null)
        {
            motor = GetComponent<TopDownCharacterMotor>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        health = GetComponent<PlayerHealth>();
        EnsureCharacterPreview();
        EnsureInventoryLayout();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        SetBackgroundBlur(false);
        RestoreCursorState();

        if (characterPreviewTexture != null)
        {
            characterPreviewTexture.Release();
            Destroy(characterPreviewTexture);
            characterPreviewTexture = null;
        }
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            SetOpen(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
        }
    }

    public void SetOpen(bool open)
    {
        isOpen = open;

        if (inventoryRoot != null)
        {
            inventoryRoot.SetActive(open);
        }

        if (characterPreviewCamera != null)
        {
            characterPreviewCamera.enabled = open;
        }

        if (characterPreviewInstance != null)
        {
            characterPreviewInstance.SetActive(open);
        }

        if (motor != null && health != null && health.IsDead)
        {
            motor.enabled = false;
        }

        if (open)
        {
            EnsureInventoryLayout();
            CaptureCursorState();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetBackgroundBlur(true);
            SetIdleAnimation();
            SetPreviewIdleAnimation();
            RefreshWeaponButtonStates();
        }
        else
        {
            SetBackgroundBlur(false);
            RestoreCursorState();
        }
    }

    private void EnsureCharacterPreview()
    {
        if (characterPreviewImage == null || characterPreviewCamera == null)
        {
            return;
        }

        if (characterPreviewTexture == null)
        {
            characterPreviewTexture = new RenderTexture(768, 1024, 16, RenderTextureFormat.ARGB32)
            {
                name = "Inventory Character Preview",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            characterPreviewTexture.Create();
        }

        characterPreviewImage.texture = characterPreviewTexture;
        characterPreviewCamera.targetTexture = characterPreviewTexture;
        characterPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        characterPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        characterPreviewCamera.cullingMask = 1 << PreviewLayer;
        characterPreviewCamera.enabled = false;

        if (characterPreviewInstance != null)
        {
            return;
        }

        var sourceVisual = animator != null ? animator.gameObject : gameObject;
        isBuildingCharacterPreview = true;
        try
        {
            characterPreviewInstance = Instantiate(sourceVisual, GetPreviewSpawnPosition(), Quaternion.identity, characterPreviewStage);
        }
        finally
        {
            isBuildingCharacterPreview = false;
        }

        characterPreviewInstance.name = "Inventory Character Preview";
        characterPreviewInstance.transform.localPosition = Vector3.zero;
        characterPreviewInstance.transform.localRotation = Quaternion.identity;
        characterPreviewInstance.transform.localScale = Vector3.one * 0.92f;
        SetLayerRecursively(characterPreviewInstance, PreviewLayer);
        StripPreviewGameplayComponents(characterPreviewInstance);

        characterPreviewAnimator = characterPreviewInstance.GetComponentInChildren<Animator>();
        if (characterPreviewAnimator != null)
        {
            characterPreviewAnimator.runtimeAnimatorController = animator != null ? animator.runtimeAnimatorController : characterPreviewAnimator.runtimeAnimatorController;
            characterPreviewAnimator.applyRootMotion = false;
            characterPreviewAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            characterPreviewAnimator.enabled = true;
        }

        characterPreviewInstance.SetActive(false);
    }

    private void SetIdleAnimation()
    {
        if (animator == null)
        {
            return;
        }

        SetFloatIfExists("Speed", 0f);
        SetFloatIfExists("MoveSpeed", 0f);
        SetBoolIfExists("IsMoving", false);
        SetBoolIfExists("Moving", false);
        SetBoolIfExists("IsRunning", false);
        SetBoolIfExists("Running", false);
    }

    private void SetPreviewIdleAnimation()
    {
        if (characterPreviewAnimator == null)
        {
            return;
        }

        SetFloatIfExists(characterPreviewAnimator, "Speed", 0f);
        SetFloatIfExists(characterPreviewAnimator, "MoveSpeed", 0f);
        SetBoolIfExists(characterPreviewAnimator, "IsMoving", false);
        SetBoolIfExists(characterPreviewAnimator, "Moving", false);
        SetBoolIfExists(characterPreviewAnimator, "IsRunning", false);
        SetBoolIfExists(characterPreviewAnimator, "Running", false);
        characterPreviewAnimator.Update(0f);
    }

    private Vector3 GetPreviewSpawnPosition()
    {
        return characterPreviewStage != null ? characterPreviewStage.position : new Vector3(5000f, 5000f, 5000f);
    }

    private void EnsureInventoryLayout()
    {
        if (inventoryRoot == null)
        {
            return;
        }

        EnsureEventSystem();
        RemoveSampleItems(inventoryRoot.transform);
        EnsureBackgroundBlurVeil();
        CenterCharacterPreview();
        RefreshBackpackItems();
        EnsureWeaponButtons();
        RefreshWeaponButtonStates();
    }

    private static void RemoveSampleItems(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            RemoveSampleItems(child);

            if (IsSampleItemObject(child.name))
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    private static bool IsSampleItemObject(string objectName)
    {
        return objectName.StartsWith("Backpack Column Item", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Nearby Column Item", System.StringComparison.OrdinalIgnoreCase);
    }

    private void CenterCharacterPreview()
    {
        if (characterPreviewImage == null)
        {
            return;
        }

        var previewRect = characterPreviewImage.rectTransform;
        previewRect.SetParent(inventoryRoot.transform, false);
        previewRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.anchoredPosition = Vector2.zero;
        previewRect.sizeDelta = new Vector2(430f, 600f);

        if (characterPreviewStage != null)
        {
            characterPreviewStage.localPosition = Vector3.zero;
            characterPreviewStage.localRotation = Quaternion.identity;
            characterPreviewStage.localScale = Vector3.one;
        }

        if (characterPreviewCamera != null)
        {
            characterPreviewCamera.transform.localPosition = new Vector3(0f, 1.35f, 4.6f);
            characterPreviewCamera.transform.localRotation = Quaternion.Euler(8f, 180f, 0f);
            characterPreviewCamera.fieldOfView = 28f;
            characterPreviewCamera.orthographic = false;
        }
    }

    private void EnsureBackgroundBlurVeil()
    {
        if (inventoryRoot == null)
        {
            return;
        }

        var existing = FindDeepChild(inventoryRoot.transform, BackgroundBlurVeilName);
        if (existing != null)
        {
            backgroundBlurVeil = existing.GetComponent<Image>();
        }

        if (backgroundBlurVeil == null)
        {
            backgroundBlurVeil = CreateImage(BackgroundBlurVeilName, inventoryRoot.transform, new Color(0f, 0f, 0f, 0.28f));
            backgroundBlurVeil.raycastTarget = false;
        }

        Stretch(backgroundBlurVeil.rectTransform);
        backgroundBlurVeil.transform.SetAsFirstSibling();
    }

    private void SetBackgroundBlur(bool enabled)
    {
        if (backgroundBlurVeil != null)
        {
            backgroundBlurVeil.enabled = enabled;
        }

        EnsureBackgroundBlurVolume();
        if (backgroundBlurVolume != null)
        {
            backgroundBlurVolume.weight = enabled ? 1f : 0f;
        }

        SetMainCameraPostProcessing(enabled);
    }

    private void EnsureBackgroundBlurVolume()
    {
        if (backgroundBlurVolume != null)
        {
            return;
        }

        var volumeObject = GameObject.Find(BackgroundBlurVolumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(BackgroundBlurVolumeName);
        }

        backgroundBlurVolume = volumeObject.GetComponent<Volume>();
        if (backgroundBlurVolume == null)
        {
            backgroundBlurVolume = volumeObject.AddComponent<Volume>();
        }

        backgroundBlurVolume.isGlobal = true;
        backgroundBlurVolume.priority = 850f;
        backgroundBlurVolume.weight = 0f;

        if (backgroundBlurProfile == null)
        {
            backgroundBlurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            backgroundBlurProfile.name = "Runtime Inventory Background Blur";
            ConfigureDepthOfField(backgroundBlurProfile);
        }

        backgroundBlurVolume.sharedProfile = backgroundBlurProfile;
    }

    private static void ConfigureDepthOfField(VolumeProfile profile)
    {
        var depthOfFieldType = System.Type.GetType("UnityEngine.Rendering.Universal.DepthOfField, Unity.RenderPipelines.Universal.Runtime");
        if (profile == null || depthOfFieldType == null)
        {
            return;
        }

        var depthOfField = profile.Add(depthOfFieldType, true);
        if (depthOfField == null)
        {
            return;
        }

        SetVolumeParameter(depthOfField, "mode", "Gaussian");
        SetVolumeParameter(depthOfField, "gaussianStart", 0f);
        SetVolumeParameter(depthOfField, "gaussianEnd", 7f);
        SetVolumeParameter(depthOfField, "gaussianMaxRadius", 1f);
        SetVolumeParameter(depthOfField, "highQualitySampling", true);
    }

    private static void SetVolumeParameter(VolumeComponent component, string fieldName, object value)
    {
        var field = component.GetType().GetField(fieldName);
        if (field == null)
        {
            return;
        }

        var parameter = field.GetValue(component);
        if (parameter == null)
        {
            return;
        }

        var parameterType = parameter.GetType();
        var overrideStateField = parameterType.GetField("overrideState");
        if (overrideStateField != null)
        {
            overrideStateField.SetValue(parameter, true);
        }
        else
        {
            var overrideStateProperty = parameterType.GetProperty("overrideState");
            if (overrideStateProperty != null && overrideStateProperty.CanWrite)
            {
                overrideStateProperty.SetValue(parameter, true, null);
            }
        }

        var valueField = parameterType.GetField("value");
        var valueProperty = parameterType.GetProperty("value");
        var valueType = valueField != null ? valueField.FieldType : valueProperty != null ? valueProperty.PropertyType : null;
        if (valueType == null)
        {
            return;
        }

        if (value is string enumName && valueType.IsEnum)
        {
            value = System.Enum.Parse(valueType, enumName);
        }

        if (valueField != null)
        {
            valueField.SetValue(parameter, value);
            return;
        }

        if (valueProperty != null && valueProperty.CanWrite)
        {
            valueProperty.SetValue(parameter, value, null);
        }
    }

    private void SetMainCameraPostProcessing(bool enabled)
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        var additionalDataType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (additionalDataType == null)
        {
            return;
        }

        mainCameraAdditionalData = mainCamera.GetComponent(additionalDataType);
        if (mainCameraAdditionalData == null)
        {
            return;
        }

        var renderPostProcessingProperty = additionalDataType.GetProperty("renderPostProcessing");
        if (renderPostProcessingProperty == null || !renderPostProcessingProperty.CanRead || !renderPostProcessingProperty.CanWrite)
        {
            return;
        }

        if (enabled)
        {
            if (!hasPostProcessingSnapshot)
            {
                previousPostProcessingEnabled = (bool)renderPostProcessingProperty.GetValue(mainCameraAdditionalData, null);
                hasPostProcessingSnapshot = true;
            }

            renderPostProcessingProperty.SetValue(mainCameraAdditionalData, true, null);
            return;
        }

        if (hasPostProcessingSnapshot)
        {
            renderPostProcessingProperty.SetValue(mainCameraAdditionalData, previousPostProcessingEnabled, null);
            hasPostProcessingSnapshot = false;
        }
    }

    private void EnsureWeaponButtons()
    {
        var existing = FindDeepChild(inventoryRoot.transform, WeaponControlsName);
        RectTransform controls;
        if (existing != null)
        {
            controls = existing.GetComponent<RectTransform>();
        }
        else
        {
            var controlsObject = new GameObject(WeaponControlsName, typeof(RectTransform));
            controlsObject.transform.SetParent(inventoryRoot.transform, false);
            controls = controlsObject.GetComponent<RectTransform>();
        }

        controls.anchorMin = new Vector2(0.5f, 0.5f);
        controls.anchorMax = new Vector2(0.5f, 0.5f);
        controls.pivot = new Vector2(0.5f, 0.5f);
        controls.anchoredPosition = new Vector2(360f, 0f);
        controls.sizeDelta = new Vector2(190f, 250f);

        unarmedButton = FindButton(controls, "Unarmed Button") ?? CreateWeaponButton("Unarmed Button", controls, "\u5f92\u624b", new Vector2(0f, 82f));
        swordButton = FindButton(controls, "Sword Button") ?? CreateWeaponButton("Sword Button", controls, "\u5251", new Vector2(0f, 0f));
        handgunButton = FindButton(controls, "Handgun Button") ?? CreateWeaponButton("Handgun Button", controls, "\u624b\u67aa", new Vector2(0f, -82f));

        unarmedButton.onClick.RemoveAllListeners();
        swordButton.onClick.RemoveAllListeners();
        handgunButton.onClick.RemoveAllListeners();
        unarmedButton.onClick.AddListener(SelectUnarmed);
        swordButton.onClick.AddListener(SelectSword);
        handgunButton.onClick.AddListener(SelectHandgun);
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

    private Button CreateWeaponButton(string objectName, Transform parent, string label, Vector2 position)
    {
        var image = CreateImage(objectName, parent, new Color(0.12f, 0.32f, 0.54f, 0.96f));
        image.sprite = GetRoundedRectSprite();
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(170f, 58f);

        var button = image.gameObject.AddComponent<Button>();
        button.colors = GetButtonColors(false);

        var text = CreateText("Label", rect, label, 24, FontStyle.Bold, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private void SelectUnarmed()
    {
        if (motor != null)
        {
            motor.SetWeaponType(TopDownCharacterMotor.WeaponType.Unarmed);
        }

        RefreshWeaponButtonStates();
    }

    private void SelectSword()
    {
        if (motor != null)
        {
            motor.SetWeaponType(TopDownCharacterMotor.WeaponType.Sword);
        }

        RefreshWeaponButtonStates();
    }

    private void SelectHandgun()
    {
        if (motor != null)
        {
            motor.SetWeaponType(TopDownCharacterMotor.WeaponType.Handgun);
        }

        RefreshWeaponButtonStates();
    }

    private void RefreshWeaponButtonStates()
    {
        var weaponType = motor != null ? motor.CurrentWeaponType : TopDownCharacterMotor.WeaponType.Unarmed;
        SetButtonSelected(unarmedButton, weaponType == TopDownCharacterMotor.WeaponType.Unarmed);
        SetButtonSelected(swordButton, weaponType == TopDownCharacterMotor.WeaponType.Sword);
        SetButtonSelected(handgunButton, weaponType == TopDownCharacterMotor.WeaponType.Handgun);
    }

    private void RefreshBackpackItems()
    {
        if (inventoryRoot == null)
        {
            return;
        }

        EnsureBackpackItemsRoot();
        if (backpackItemsRoot == null)
        {
            return;
        }

        for (var i = backpackItemsRoot.childCount - 1; i >= 0; i--)
        {
            DestroyBackpackItemRow(backpackItemsRoot.GetChild(i).gameObject);
        }

        if (inventoryItems.Count == 0)
        {
            CreateEmptyBackpackHint();
            return;
        }

        EnsureBackpackItemPreviewRig();

        for (var i = 0; i < inventoryItems.Count; i++)
        {
            CreateBackpackItemRow(inventoryItems[i], i);
        }
    }

    private static void DestroyBackpackItemRow(GameObject row)
    {
        if (row == null)
        {
            return;
        }

        foreach (var rawImage in row.GetComponentsInChildren<RawImage>(true))
        {
            if (rawImage.texture is RenderTexture renderTexture)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        Destroy(row);
    }

    private void EnsureBackpackItemsRoot()
    {
        if (backpackItemsRoot != null)
        {
            return;
        }

        var parent = FindDeepChild(inventoryRoot.transform, "Backpack Column");
        if (parent == null)
        {
            parent = inventoryRoot.transform;
        }

        var existing = FindDeepChild(parent, BackpackItemsName);
        if (existing != null)
        {
            backpackItemsRoot = existing.GetComponent<RectTransform>();
            return;
        }

        var rootObject = new GameObject(BackpackItemsName, typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);
        backpackItemsRoot = rootObject.GetComponent<RectTransform>();
        backpackItemsRoot.anchorMin = new Vector2(0f, 1f);
        backpackItemsRoot.anchorMax = new Vector2(1f, 1f);
        backpackItemsRoot.pivot = new Vector2(0.5f, 1f);
        backpackItemsRoot.anchoredPosition = new Vector2(0f, -74f);
        backpackItemsRoot.sizeDelta = new Vector2(0f, 430f);
    }

    private void CreateEmptyBackpackHint()
    {
        var hintImage = CreateImage(EmptyBackpackHintName, backpackItemsRoot, new Color(0.02f, 0.028f, 0.034f, 0.62f));
        hintImage.sprite = GetRoundedRectSprite();
        hintImage.type = Image.Type.Sliced;
        hintImage.raycastTarget = false;

        var rect = hintImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(370f, 96f);

        var title = CreateText("Title", rect, "\u80cc\u5305\u6682\u65e0\u7269\u54c1", 22, FontStyle.Bold, Color.white);
        title.alignment = TextAnchor.MiddleCenter;
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(16f, 22f);
        title.rectTransform.offsetMax = new Vector2(-16f, -14f);

        var subtitle = CreateText("Subtitle", rect, "\u63a2\u7d22\u573a\u666f\u540e\u4f1a\u5728\u8fd9\u91cc\u663e\u793a\u6536\u96c6\u7269", 15, FontStyle.Normal, new Color(0.72f, 0.78f, 0.82f, 1f));
        subtitle.alignment = TextAnchor.MiddleCenter;
        subtitle.rectTransform.anchorMin = Vector2.zero;
        subtitle.rectTransform.anchorMax = Vector2.one;
        subtitle.rectTransform.offsetMin = new Vector2(16f, 6f);
        subtitle.rectTransform.offsetMax = new Vector2(-16f, -44f);
    }

    private void CreateBackpackItemRow(InventoryItemEntry item, int index)
    {
        var rowImage = CreateImage($"Backpack Item {index + 1:00}", backpackItemsRoot, new Color(0.025f, 0.034f, 0.042f, 0.92f));
        rowImage.sprite = GetRoundedRectSprite();
        rowImage.type = Image.Type.Sliced;
        rowImage.raycastTarget = false;

        var row = rowImage.rectTransform;
        row.anchorMin = new Vector2(0.5f, 1f);
        row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -index * 104f);
        row.sizeDelta = new Vector2(386f, 92f);

        var previewFrame = CreateImage("Preview Frame", row, new Color(0.08f, 0.11f, 0.13f, 0.92f));
        previewFrame.sprite = GetRoundedRectSprite();
        previewFrame.type = Image.Type.Sliced;
        previewFrame.raycastTarget = false;
        previewFrame.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        previewFrame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        previewFrame.rectTransform.pivot = new Vector2(0f, 0.5f);
        previewFrame.rectTransform.anchoredPosition = new Vector2(12f, 0f);
        previewFrame.rectTransform.sizeDelta = new Vector2(76f, 76f);

        var preview = CreateItemPreview(item, previewFrame.rectTransform);
        preview.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        preview.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        preview.rectTransform.pivot = new Vector2(0f, 0.5f);
        preview.rectTransform.anchoredPosition = new Vector2(6f, 0f);
        preview.rectTransform.sizeDelta = new Vector2(64f, 64f);

        var nameText = CreateText("Name", row, item.Name, 21, FontStyle.Bold, Color.white);
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        nameText.rectTransform.offsetMin = new Vector2(104f, 32f);
        nameText.rectTransform.offsetMax = new Vector2(-82f, -12f);
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;

        var typeText = CreateText("Type", row, "\u6536\u96c6\u7269", 15, FontStyle.Normal, new Color(0.66f, 0.74f, 0.8f, 1f));
        typeText.alignment = TextAnchor.MiddleLeft;
        typeText.rectTransform.anchorMin = new Vector2(0f, 0f);
        typeText.rectTransform.anchorMax = new Vector2(1f, 0f);
        typeText.rectTransform.pivot = new Vector2(0f, 0f);
        typeText.rectTransform.anchoredPosition = new Vector2(104f, 16f);
        typeText.rectTransform.sizeDelta = new Vector2(170f, 22f);

        var countBadge = CreateImage("Count Badge", row, new Color(0.78f, 0.46f, 0.12f, 1f));
        countBadge.sprite = GetRoundedRectSprite();
        countBadge.type = Image.Type.Sliced;
        countBadge.raycastTarget = false;
        countBadge.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        countBadge.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        countBadge.rectTransform.pivot = new Vector2(1f, 0.5f);
        countBadge.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
        countBadge.rectTransform.sizeDelta = new Vector2(58f, 34f);

        var countText = CreateText("Count", countBadge.rectTransform, $"x{item.Count}", 18, FontStyle.Bold, Color.white);
        Stretch(countText.rectTransform);
    }

    private RawImage CreateItemPreview(InventoryItemEntry item, Transform parent)
    {
        var obj = new GameObject("Model Preview", typeof(RectTransform), typeof(RawImage));
        obj.transform.SetParent(parent, false);

        var rawImage = obj.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.color = Color.white;

        var texture = new RenderTexture(192, 192, 16, RenderTextureFormat.ARGB32)
        {
            name = $"Inventory Item Preview {item.Name}",
            antiAliasing = 2,
            filterMode = FilterMode.Bilinear
        };
        texture.Create();
        rawImage.texture = texture;

        if (item.ModelPrefab != null && backpackItemPreviewCamera != null && backpackItemPreviewStage != null)
        {
            RenderItemPreview(item, texture);
        }

        return rawImage;
    }

    private void EnsureBackpackItemPreviewRig()
    {
        if (backpackItemPreviewCamera == null)
        {
            var cameraObject = GameObject.Find(BackpackItemPreviewCameraName);
            if (cameraObject == null)
            {
                cameraObject = new GameObject(BackpackItemPreviewCameraName, typeof(Camera));
                cameraObject.transform.position = new Vector3(5200f, 5201.1f, 5203.35f);
                cameraObject.transform.rotation = Quaternion.Euler(15f, 180f, 0f);
            }

            backpackItemPreviewCamera = cameraObject.GetComponent<Camera>();
            backpackItemPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
            backpackItemPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            backpackItemPreviewCamera.cullingMask = 1 << PreviewLayer;
            backpackItemPreviewCamera.fieldOfView = 22f;
            backpackItemPreviewCamera.orthographic = false;
            backpackItemPreviewCamera.allowHDR = false;
            backpackItemPreviewCamera.enabled = false;
        }

        if (backpackItemPreviewStage == null)
        {
            var stageObject = GameObject.Find(BackpackItemPreviewStageName);
            if (stageObject == null)
            {
                stageObject = new GameObject(BackpackItemPreviewStageName);
                stageObject.transform.position = new Vector3(5200f, 5200f, 5200f);
            }

            backpackItemPreviewStage = stageObject.transform;
        }

        var lightObject = GameObject.Find(BackpackItemPreviewLightName);
        if (lightObject == null)
        {
            lightObject = new GameObject(BackpackItemPreviewLightName, typeof(Light));
            lightObject.transform.position = new Vector3(5200f, 5202.8f, 5201.6f);
            lightObject.transform.rotation = Quaternion.Euler(48f, 155f, 0f);
        }

        var previewLight = lightObject.GetComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 2.2f;
        previewLight.color = new Color(1f, 0.96f, 0.88f, 1f);
        previewLight.cullingMask = 1 << PreviewLayer;
        previewLight.enabled = false;
        backpackItemPreviewLight = previewLight;
        lightObject.layer = PreviewLayer;
    }

    private void RenderItemPreview(InventoryItemEntry item, RenderTexture targetTexture)
    {
        for (var i = backpackItemPreviewStage.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(backpackItemPreviewStage.GetChild(i).gameObject);
        }

        var instance = Instantiate(item.ModelPrefab, backpackItemPreviewStage);
        instance.name = $"Preview {item.Name}";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        instance.transform.localScale = Vector3.one * 1.25f;
        SetLayerRecursively(instance, PreviewLayer);

        var previousLightEnabled = backpackItemPreviewLight != null && backpackItemPreviewLight.enabled;
        if (backpackItemPreviewLight != null)
        {
            backpackItemPreviewLight.enabled = true;
        }

        backpackItemPreviewCamera.targetTexture = targetTexture;
        backpackItemPreviewCamera.Render();
        backpackItemPreviewCamera.targetTexture = null;

        if (backpackItemPreviewLight != null)
        {
            backpackItemPreviewLight.enabled = previousLightEnabled;
        }

        DestroyImmediate(instance);
    }

    private static void SetButtonSelected(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        button.colors = GetButtonColors(selected);
    }

    private static ColorBlock GetButtonColors(bool selected)
    {
        var normal = selected
            ? new Color(0.78f, 0.46f, 0.12f, 1f)
            : new Color(0.12f, 0.32f, 0.54f, 0.96f);

        var highlighted = selected
            ? new Color(0.92f, 0.58f, 0.18f, 1f)
            : new Color(0.18f, 0.44f, 0.72f, 1f);

        return new ColorBlock
        {
            normalColor = normal,
            highlightedColor = highlighted,
            pressedColor = new Color(0.08f, 0.22f, 0.42f, 1f),
            selectedColor = highlighted,
            disabledColor = new Color(0.24f, 0.24f, 0.24f, 0.55f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
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

    private static void StripPreviewGameplayComponents(GameObject preview)
    {
        foreach (var behaviour in preview.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
            Destroy(behaviour);
        }

        foreach (var controller in preview.GetComponentsInChildren<CharacterController>(true))
        {
            controller.enabled = false;
            Destroy(controller);
        }

        foreach (var collider in preview.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Destroy(collider);
        }

        foreach (var rigidbody in preview.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            Destroy(rigidbody);
        }

        foreach (var camera in preview.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
            Destroy(camera);
        }

        foreach (var listener in preview.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
            Destroy(listener);
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            var match = FindDeepChild(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        image.material = Graphic.defaultGraphicMaterial;
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
        text.raycastTarget = false;
        return text;
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
            Debug.LogWarning($"Inventory font lookup failed: {exception.Message}");
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
            name = "Generated Inventory Button Sprite",
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

    private void SetFloatIfExists(string parameterName, float value)
    {
        SetFloatIfExists(animator, parameterName, value);
    }

    private static void SetFloatIfExists(Animator targetAnimator, string parameterName, float value)
    {
        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == parameterName)
            {
                targetAnimator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private void SetBoolIfExists(string parameterName, bool value)
    {
        SetBoolIfExists(animator, parameterName, value);
    }

    private static void SetBoolIfExists(Animator targetAnimator, string parameterName, bool value)
    {
        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                targetAnimator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private sealed class InventoryItemEntry
    {
        public InventoryItemEntry(string name, int count, GameObject modelPrefab)
        {
            Name = name;
            Count = count;
            ModelPrefab = modelPrefab;
        }

        public string Name { get; }
        public int Count { get; set; }
        public GameObject ModelPrefab { get; set; }
    }
}
