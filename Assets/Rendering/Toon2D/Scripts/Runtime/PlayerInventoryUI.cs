using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PlayerInventoryUI : MonoBehaviour
{
    private const int PreviewLayer = 31;
    private const string WeaponControlsName = "Inventory Weapon Controls";
    private const string BackgroundBlurVeilName = "Inventory Background Blur Veil";
    private const string BackgroundBlurVolumeName = "Inventory Background Blur Volume";
    private static bool isBuildingCharacterPreview;
    private static Font readableFont;
    private static Sprite roundedRectSprite;
    private static readonly Dictionary<string, Sprite> editorIconSpriteCache = new Dictionary<string, Sprite>();
    private const string BackpackItemsName = "Runtime Backpack Items";
    private const string EmptyBackpackHintName = "Runtime Backpack Empty Hint";
    private const string ItemDetailPanelName = "Runtime Item Detail Panel";
    private const string ItemSummaryPanelName = "Runtime Item Summary Panel";
    private const string ItemDescriptionPanelName = "Runtime Item Description Panel";
    private const string ItemReceivedToastCanvasName = "Item Received Toast HUD";
    private const string ItemReceivedToastTextName = "Item Received Toast Text";
    private const string BackpackItemPreviewCameraName = "Runtime Backpack Item Preview Camera";
    private const string BackpackItemPreviewStageName = "Runtime Backpack Item Preview Stage";
    private const string BackpackItemPreviewLightName = "Runtime Backpack Item Preview Light";
    public const string TaorantingAlbumItemName = "\u9676\u7136\u4ead\u753b\u96c6\u6b8b\u672c";
    private const string TaorantingAlbumDescription = "\u9676\u7136\u5e7b\u5883\u91cc\u9762\u7684\u753b\u96c6\u6b8b\u672c\uff0c\u770b\u4e0a\u53bb\u5f88\u65e7\uff0c\u5b57\u8ff9\u4e5f\u4e0d\u6e05\u4e86\uff0c\u4e5f\u8bb8\u6536\u96c6\u5168\u90e8\u4e4b\u540e\u62fc\u51d1\u8d77\u6765\u4f1a\u6709\u5927\u7528\u5904\u2026\u2026";
    private const string FriedEggItemName = "\u714e\u86cb";
    private const string FriedEggDescription = "\u5e73\u5e73\u65e0\u5947\u7684\u714e\u86cb\u3001\u4f46\u662f\u5728\u5916\u80fd\u52a9\u4f60\u514d\u4e8e\u9965\u997f\uff1b";
    private const string FellowSketchItemName = "\u540C\u9053\u8005\u7684\u5199\u751F";
    private const string UnarmedIconPath = "Assets/Art/2D/Icon_Weapeon/Unarmed.png";
    private const string SwordIconPath = "Assets/Art/2D/Icon_Weapeon/sword.png";
    private const string HandgunIconPath = "Assets/Art/2D/Icon_Weapeon/pistol.png";
    private const string TaorantingAlbumIconPath = "Assets/Art/2D/Icon_Item/Book fragment.png";
    private const string FriedEggIconPath = "Assets/Art/2D/Icon_Item/fried egg.png";
    private const string FellowSketchIconPath = "Assets/Art/2D/Icon_Item/painting.png";
    private const string UnarmedIconResourcePath = "UI/weapon_unarmed";
    private const string SwordIconResourcePath = "UI/weapon_sword";
    private const string HandgunIconResourcePath = "UI/weapon_pistol";
    private const string TaorantingAlbumIconResourcePath = "UI/item_book_fragment";
    private const string FriedEggIconResourcePath = "UI/item_fried_egg";
    private const string FellowSketchIconResourcePath = "UI/item_painting";
    private const int BackpackCardCount = 6;
    private const int BackpackCardColumns = 3;

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
    private RectTransform itemDetailRoot;
    private RectTransform itemSummaryRoot;
    private RectTransform itemDescriptionRoot;
    private InventoryItemEntry selectedInventoryItem;
    private Camera backpackItemPreviewCamera;
    private Transform backpackItemPreviewStage;
    private Light backpackItemPreviewLight;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool hasCursorSnapshot;
    private bool previousPostProcessingEnabled;
    private bool hasPostProcessingSnapshot;
    private bool isOpen;
    private Text itemReceivedToastText;
    private CanvasGroup itemReceivedToastCanvasGroup;
    private Coroutine itemReceivedToastCoroutine;

    public bool IsOpen => isOpen;

    public void AddInventoryItem(string itemName, int count, GameObject modelPrefab)
    {
        AddInventoryItem(itemName, count, modelPrefab, GetDefaultItemDescription(itemName));
    }

    public void AddInventoryItem(string itemName, int count, GameObject modelPrefab, string description)
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
            inventoryItems.Add(new InventoryItemEntry(itemName, count, modelPrefab, GetSafeItemDescription(itemName, description)));
        }

        EnsureInventoryLayout();
        ShowItemReceivedToast(itemName, count);
    }

    public int GetInventoryItemCount(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return 0;
        }

        var existing = inventoryItems.Find(item => string.Equals(item.Name, itemName, System.StringComparison.Ordinal));
        return existing != null ? existing.Count : 0;
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
        characterPreviewInstance.transform.localScale = Vector3.one * 0.86f;
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
        EnsureItemDetailPanel();
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
            || objectName.Equals("Backpack Column", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Nearby Column Item", System.StringComparison.OrdinalIgnoreCase)
            || objectName.IndexOf("Backpack Column Title", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Backpack Column Subtitle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Nearby Column Title", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Nearby Column Subtitle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Character Preview Top Line", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Character Preview Bottom Line", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Inventory Divider", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
        previewRect.anchoredPosition = new Vector2(0f, 28f);
        previewRect.sizeDelta = new Vector2(560f, 780f);

        if (characterPreviewStage != null)
        {
            characterPreviewStage.localPosition = Vector3.zero;
            characterPreviewStage.localRotation = Quaternion.identity;
            characterPreviewStage.localScale = Vector3.one;
        }

        if (characterPreviewCamera != null)
        {
            characterPreviewCamera.transform.localPosition = new Vector3(0f, 1.15f, 4.35f);
            characterPreviewCamera.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            characterPreviewCamera.fieldOfView = 27f;
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
        controls.anchoredPosition = new Vector2(620f, 0f);
        controls.sizeDelta = new Vector2(520f, 780f);

        unarmedButton = FindButton(controls, "Unarmed Button") ?? CreateWeaponButton("Unarmed Button", controls);
        swordButton = FindButton(controls, "Sword Button") ?? CreateWeaponButton("Sword Button", controls);
        handgunButton = FindButton(controls, "Handgun Button") ?? CreateWeaponButton("Handgun Button", controls);

        ConfigureWeaponButton(unarmedButton, "\u5f92\u624b", UnarmedIconPath, new Vector2(0f, 250f));
        ConfigureWeaponButton(swordButton, "\u5251", SwordIconPath, new Vector2(0f, 0f));
        ConfigureWeaponButton(handgunButton, "\u624b\u67aa", HandgunIconPath, new Vector2(0f, -250f));

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

    private Button CreateWeaponButton(string objectName, Transform parent)
    {
        var image = CreateImage(objectName, parent, new Color(0.24f, 0.26f, 0.28f, 0.96f));
        image.sprite = GetRoundedRectSprite();
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        var button = image.gameObject.AddComponent<Button>();
        button.colors = GetButtonColors(false);
        return button;
    }

    private void ConfigureWeaponButton(Button button, string label, string iconAssetPath, Vector2 position)
    {
        if (button == null)
        {
            return;
        }

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = GetRoundedRectSprite();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
        }

        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(380f, 218f);

        for (var i = rect.childCount - 1; i >= 0; i--)
        {
            Destroy(rect.GetChild(i).gameObject);
        }

        var previewFrame = CreateImage("Preview Frame", rect, Color.black);
        previewFrame.sprite = GetRoundedRectSprite();
        previewFrame.type = Image.Type.Sliced;
        previewFrame.raycastTarget = false;
        previewFrame.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        previewFrame.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        previewFrame.rectTransform.pivot = new Vector2(0.5f, 1f);
        previewFrame.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        previewFrame.rectTransform.sizeDelta = new Vector2(320f, 132f);

        var iconSprite = LoadIconSprite(iconAssetPath);
        if (iconSprite != null)
        {
            var iconImage = CreateImage("Icon", previewFrame.rectTransform, Color.white);
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            Stretch(iconImage.rectTransform);
            iconImage.rectTransform.offsetMin = new Vector2(12f, 10f);
            iconImage.rectTransform.offsetMax = new Vector2(-12f, -10f);
        }
        var text = CreateText("Label", rect, label, 30, FontStyle.Bold, Color.white);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 0f);
        text.rectTransform.pivot = new Vector2(0.5f, 0f);
        text.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        text.rectTransform.sizeDelta = new Vector2(-48f, 44f);
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

    private void EnsureItemDetailPanel()
    {
        if (inventoryRoot == null)
        {
            return;
        }

        var parent = FindDeepChild(inventoryRoot.transform, "Nearby Column");
        if (parent == null)
        {
            parent = FindDeepChild(inventoryRoot.transform, "Item Detail Column");
        }

        if (parent == null)
        {
            var detailColumnObject = new GameObject("Item Detail Column", typeof(RectTransform));
            detailColumnObject.transform.SetParent(inventoryRoot.transform, false);
            parent = detailColumnObject.GetComponent<RectTransform>();
        }

        if (parent is RectTransform detailColumn)
        {
            if (detailColumn.transform != inventoryRoot.transform)
            {
                detailColumn.SetParent(inventoryRoot.transform, false);
            }

            detailColumn.anchorMin = new Vector2(0.5f, 0.5f);
            detailColumn.anchorMax = new Vector2(0.5f, 0.5f);
            detailColumn.pivot = new Vector2(0.5f, 0.5f);
            detailColumn.anchoredPosition = new Vector2(-620f, 0f);
            detailColumn.sizeDelta = new Vector2(540f, 780f);
        }

        var existingPanel = FindDeepChild(parent, ItemDetailPanelName);
        itemDetailRoot = existingPanel != null ? existingPanel.GetComponent<RectTransform>() : null;

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (itemDetailRoot == null || child != itemDetailRoot)
            {
                Destroy(child.gameObject);
            }
        }

        if (itemDetailRoot != null)
        {
            RefreshItemDetailPanel();
            return;
        }

        var panelImage = CreateImage(ItemDetailPanelName, parent, new Color(0.018f, 0.026f, 0.032f, 0.84f));
        panelImage.sprite = GetRoundedRectSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.raycastTarget = false;
        itemDetailRoot = panelImage.rectTransform;
        itemDetailRoot.anchorMin = new Vector2(0f, 0f);
        itemDetailRoot.anchorMax = new Vector2(1f, 1f);
        itemDetailRoot.offsetMin = new Vector2(8f, 8f);
        itemDetailRoot.offsetMax = new Vector2(-8f, -8f);

        RefreshItemDetailPanel();
    }

    private void SelectInventoryItem(InventoryItemEntry item)
    {
        selectedInventoryItem = item;
        RefreshItemDetailPanel();
    }

    private void RefreshItemDetailPanel()
    {
        if (itemDetailRoot == null)
        {
            return;
        }

        EnsureItemDetailSubpanels();

        var hasSelection = selectedInventoryItem != null;
        var name = hasSelection ? selectedInventoryItem.Name : "\u672a\u9009\u62e9\u9053\u5177";
        var count = hasSelection ? $"x{selectedInventoryItem.Count}" : "--";
        var description = hasSelection
            ? selectedInventoryItem.Description
            : "\u9f20\u6807\u5de6\u952e\u5355\u51fb\u4e2d\u95f4\u80cc\u5305\u5361\u7247\u540e\uff0c\u8fd9\u91cc\u663e\u793a\u9053\u5177\u4fe1\u606f\u3002";

        ClearChildren(itemSummaryRoot);
        var previewFrame = CreateImage("Preview Frame", itemSummaryRoot, Color.black);
        previewFrame.sprite = GetRoundedRectSprite();
        previewFrame.type = Image.Type.Sliced;
        previewFrame.raycastTarget = false;
        previewFrame.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        previewFrame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        previewFrame.rectTransform.pivot = new Vector2(0f, 0.5f);
        previewFrame.rectTransform.anchoredPosition = new Vector2(18f, 0f);
        previewFrame.rectTransform.sizeDelta = new Vector2(184f, 142f);

        if (hasSelection)
        {
            var preview = CreateItemPreview(selectedInventoryItem, previewFrame.rectTransform);
            Stretch(preview.rectTransform);
            preview.rectTransform.offsetMin = new Vector2(14f, 10f);
            preview.rectTransform.offsetMax = new Vector2(-14f, -10f);
        }
        var nameText = CreateText("Name", itemSummaryRoot, name, 28, FontStyle.Bold, hasSelection ? Color.white : new Color(0.66f, 0.72f, 0.78f, 1f));
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        nameText.rectTransform.pivot = new Vector2(0f, 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(224f, -34f);
        nameText.rectTransform.sizeDelta = new Vector2(-246f, 68f);
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;

        var countText = CreateText("Count", itemSummaryRoot, "\u6301\u6709\u6570\u91cf  " + count, 24, FontStyle.Bold, new Color(1f, 0.76f, 0.36f, 1f));
        countText.alignment = TextAnchor.MiddleLeft;
        countText.rectTransform.anchorMin = new Vector2(0f, 1f);
        countText.rectTransform.anchorMax = new Vector2(1f, 1f);
        countText.rectTransform.pivot = new Vector2(0f, 1f);
        countText.rectTransform.anchoredPosition = new Vector2(224f, -126f);
        countText.rectTransform.sizeDelta = new Vector2(-246f, 38f);

        ClearChildren(itemDescriptionRoot);
        var descriptionText = CreateText("Description", itemDescriptionRoot, description, 24, FontStyle.Normal, new Color(0.72f, 0.79f, 0.84f, 1f));
        descriptionText.alignment = TextAnchor.UpperLeft;
        Stretch(descriptionText.rectTransform);
        descriptionText.rectTransform.offsetMin = new Vector2(20f, 16f);
        descriptionText.rectTransform.offsetMax = new Vector2(-20f, -16f);
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void EnsureItemDetailSubpanels()
    {
        if (itemDetailRoot == null)
        {
            return;
        }

        itemSummaryRoot = EnsurePanelRoot(ItemSummaryPanelName, itemDetailRoot);
        itemSummaryRoot.anchorMin = new Vector2(0f, 1f);
        itemSummaryRoot.anchorMax = new Vector2(1f, 1f);
        itemSummaryRoot.pivot = new Vector2(0.5f, 1f);
        itemSummaryRoot.anchoredPosition = new Vector2(0f, -16f);
        itemSummaryRoot.sizeDelta = new Vector2(-24f, 190f);

        itemDescriptionRoot = EnsurePanelRoot(ItemDescriptionPanelName, itemDetailRoot);
        itemDescriptionRoot.anchorMin = new Vector2(0f, 0f);
        itemDescriptionRoot.anchorMax = new Vector2(1f, 0f);
        itemDescriptionRoot.pivot = new Vector2(0.5f, 0f);
        itemDescriptionRoot.anchoredPosition = new Vector2(0f, 16f);
        itemDescriptionRoot.sizeDelta = new Vector2(-24f, 176f);
    }

    private static RectTransform EnsurePanelRoot(string objectName, Transform parent)
    {
        var existing = FindDeepChild(parent, objectName);
        if (existing != null)
        {
            return existing.GetComponent<RectTransform>();
        }

        var obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
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

        EnsureBackpackItemPreviewRig();

        if (selectedInventoryItem != null && !inventoryItems.Contains(selectedInventoryItem))
        {
            selectedInventoryItem = null;
        }

        RefreshItemDetailPanel();

        for (var i = 0; i < BackpackCardCount; i++)
        {
            var item = i < inventoryItems.Count ? inventoryItems[i] : null;
            CreateBackpackItemCard(item, i);
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
        if (itemDetailRoot == null)
        {
            EnsureItemDetailPanel();
        }

        if (itemDetailRoot == null)
        {
            return;
        }

        if (backpackItemsRoot == null)
        {
            var existing = FindDeepChild(inventoryRoot.transform, BackpackItemsName);
            if (existing != null)
            {
                backpackItemsRoot = existing.GetComponent<RectTransform>();
            }
        }

        if (backpackItemsRoot == null)
        {
            var rootObject = new GameObject(BackpackItemsName, typeof(RectTransform));
            rootObject.transform.SetParent(itemDetailRoot, false);
            backpackItemsRoot = rootObject.GetComponent<RectTransform>();
        }
        else if (backpackItemsRoot.transform.parent != itemDetailRoot)
        {
            backpackItemsRoot.SetParent(itemDetailRoot, false);
        }

        backpackItemsRoot.anchorMin = new Vector2(0.5f, 1f);
        backpackItemsRoot.anchorMax = new Vector2(0.5f, 1f);
        backpackItemsRoot.pivot = new Vector2(0.5f, 1f);
        backpackItemsRoot.anchoredPosition = new Vector2(0f, -230f);
        backpackItemsRoot.sizeDelta = new Vector2(492f, 292f);
    }

    private void CreateBackpackItemCard(InventoryItemEntry item, int index)
    {
        var row = index / BackpackCardColumns;
        var column = index % BackpackCardColumns;
        var isFilled = item != null;

        var rowImage = CreateImage($"Backpack Card {index + 1:00}", backpackItemsRoot, isFilled ? new Color(0.24f, 0.26f, 0.28f, 0.94f) : new Color(0.18f, 0.19f, 0.2f, 0.58f));
        rowImage.sprite = GetRoundedRectSprite();
        rowImage.type = Image.Type.Sliced;
        rowImage.raycastTarget = isFilled;

        var card = rowImage.rectTransform;
        card.anchorMin = new Vector2(0f, 1f);
        card.anchorMax = new Vector2(0f, 1f);
        card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = new Vector2(column * 164f, -row * 146f);
        card.sizeDelta = new Vector2(150f, 132f);

        if (isFilled)
        {
            var button = rowImage.gameObject.AddComponent<Button>();
            button.colors = GetButtonColors(false);
            button.onClick.AddListener(() => SelectInventoryItem(item));
        }

        var previewFrame = CreateImage("Preview Frame", card, Color.black);
        previewFrame.sprite = GetRoundedRectSprite();
        previewFrame.type = Image.Type.Sliced;
        previewFrame.raycastTarget = false;
        previewFrame.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        previewFrame.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        previewFrame.rectTransform.pivot = new Vector2(0.5f, 1f);
        previewFrame.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        previewFrame.rectTransform.sizeDelta = new Vector2(118f, 76f);

        if (isFilled)
        {
            var preview = CreateItemPreview(item, previewFrame.rectTransform);
            Stretch(preview.rectTransform);
        }
        var countLabel = isFilled ? $"x{item.Count}" : "--";
        var countText = CreateText("Count", card, countLabel, 22, FontStyle.Bold, isFilled ? Color.white : new Color(0.5f, 0.56f, 0.6f, 1f));
        countText.alignment = TextAnchor.MiddleCenter;
        countText.rectTransform.anchorMin = new Vector2(0f, 0f);
        countText.rectTransform.anchorMax = new Vector2(1f, 0f);
        countText.rectTransform.pivot = new Vector2(0.5f, 0f);
        countText.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        countText.rectTransform.sizeDelta = new Vector2(0f, 32f);
    }

    private Graphic CreateItemPreview(InventoryItemEntry item, Transform parent)
    {
        var iconSprite = LoadIconSprite(GetItemIconPath(item.Name));
        if (iconSprite != null)
        {
            var iconImage = CreateImage("Item Icon", parent, Color.white);
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            return iconImage;
        }

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
            ? new Color(0.42f, 0.44f, 0.46f, 1f)
            : new Color(0.24f, 0.26f, 0.28f, 0.96f);

        var highlighted = selected
            ? new Color(0.5f, 0.52f, 0.54f, 1f)
            : new Color(0.34f, 0.36f, 0.38f, 1f);

        return new ColorBlock
        {
            normalColor = normal,
            highlightedColor = highlighted,
            pressedColor = new Color(0.16f, 0.17f, 0.18f, 1f),
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

    private void ShowItemReceivedToast(string itemName, int count)
    {
        EnsureItemReceivedToastUi();
        if (itemReceivedToastText == null || itemReceivedToastCanvasGroup == null)
        {
            return;
        }

        itemReceivedToastText.text = $"{itemName}*{count}";
        itemReceivedToastCanvasGroup.alpha = 1f;
        itemReceivedToastCanvasGroup.gameObject.SetActive(true);

        if (itemReceivedToastCoroutine != null)
        {
            StopCoroutine(itemReceivedToastCoroutine);
        }

        itemReceivedToastCoroutine = StartCoroutine(HideItemReceivedToastAfterDelay());
    }

    private void EnsureItemReceivedToastUi()
    {
        if (itemReceivedToastText != null && itemReceivedToastCanvasGroup != null)
        {
            return;
        }

        var canvasObject = GameObject.Find(ItemReceivedToastCanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(ItemReceivedToastCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 210;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        itemReceivedToastCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        if (itemReceivedToastCanvasGroup == null)
        {
            itemReceivedToastCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
        }

        itemReceivedToastCanvasGroup.interactable = false;
        itemReceivedToastCanvasGroup.blocksRaycasts = false;

        var textTransform = FindDeepChild(canvasObject.transform, ItemReceivedToastTextName);
        if (textTransform != null)
        {
            itemReceivedToastText = textTransform.GetComponent<Text>();
        }

        if (itemReceivedToastText == null)
        {
            itemReceivedToastText = CreateText(ItemReceivedToastTextName, canvasObject.transform, string.Empty, 30, FontStyle.Bold, new Color(1f, 0.88f, 0.42f, 1f));
            itemReceivedToastText.alignment = TextAnchor.MiddleLeft;
            itemReceivedToastText.horizontalOverflow = HorizontalWrapMode.Overflow;
            itemReceivedToastText.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = itemReceivedToastText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
        }

        var rect = itemReceivedToastText.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(72f, 0f);
        rect.sizeDelta = new Vector2(560f, 72f);
        itemReceivedToastCanvasGroup.gameObject.SetActive(false);
    }

    private IEnumerator HideItemReceivedToastAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2.2f);

        if (itemReceivedToastCanvasGroup != null)
        {
            itemReceivedToastCanvasGroup.alpha = 0f;
            itemReceivedToastCanvasGroup.gameObject.SetActive(false);
        }

        itemReceivedToastCoroutine = null;
    }

    private static Sprite LoadIconSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        if (editorIconSpriteCache.TryGetValue(assetPath, out var cachedSprite))
        {
            return cachedSprite;
        }

        Sprite sprite = null;
#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
            }
        }

#endif

        if (sprite == null)
        {
            sprite = LoadRuntimeIconSprite(assetPath);
        }

        editorIconSpriteCache[assetPath] = sprite;
        return sprite;
    }

    private static Sprite LoadRuntimeIconSprite(string assetPath)
    {
        var resourcePath = GetIconResourcePath(assetPath);
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        var resourceSprite = Resources.Load<Sprite>(resourcePath);
        if (resourceSprite != null)
        {
            return resourceSprite;
        }

        var texture = Resources.Load<Texture2D>(resourcePath);
        return texture != null ? CreateRuntimeSprite(texture) : null;
    }

    private static string GetIconResourcePath(string assetPath)
    {
        if (string.Equals(assetPath, UnarmedIconPath, System.StringComparison.Ordinal))
        {
            return UnarmedIconResourcePath;
        }

        if (string.Equals(assetPath, SwordIconPath, System.StringComparison.Ordinal))
        {
            return SwordIconResourcePath;
        }

        if (string.Equals(assetPath, HandgunIconPath, System.StringComparison.Ordinal))
        {
            return HandgunIconResourcePath;
        }

        if (string.Equals(assetPath, TaorantingAlbumIconPath, System.StringComparison.Ordinal))
        {
            return TaorantingAlbumIconResourcePath;
        }

        if (string.Equals(assetPath, FriedEggIconPath, System.StringComparison.Ordinal))
        {
            return FriedEggIconResourcePath;
        }

        if (string.Equals(assetPath, FellowSketchIconPath, System.StringComparison.Ordinal))
        {
            return FellowSketchIconResourcePath;
        }

        return null;
    }

    private static Sprite CreateRuntimeSprite(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
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

    private static string GetDefaultItemDescription(string itemName)
    {
        if (string.Equals(itemName, TaorantingAlbumItemName, System.StringComparison.Ordinal))
        {
            return TaorantingAlbumDescription;
        }

        if (string.Equals(itemName, FriedEggItemName, System.StringComparison.Ordinal))
        {
            return FriedEggDescription;
        }

        return "\u9053\u5177\u8bf4\u660e\u5360\u4f4d\uff1a\u540e\u7eed\u53ef\u5728\u8fd9\u91cc\u63a5\u5165\u5177\u4f53\u7528\u9014\u548c\u80cc\u666f\u6587\u672c\u3002";
    }

    private static string GetSafeItemDescription(string itemName, string description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? GetDefaultItemDescription(itemName)
            : description;
    }

    private static string GetItemIconPath(string itemName)
    {
        if (string.Equals(itemName, TaorantingAlbumItemName, System.StringComparison.Ordinal))
        {
            return TaorantingAlbumIconPath;
        }

        if (string.Equals(itemName, FriedEggItemName, System.StringComparison.Ordinal))
        {
            return FriedEggIconPath;
        }

        if (string.Equals(itemName, FellowSketchItemName, System.StringComparison.Ordinal))
        {
            return FellowSketchIconPath;
        }

        return null;
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
        public InventoryItemEntry(string name, int count, GameObject modelPrefab, string description)
        {
            Name = name;
            Count = count;
            ModelPrefab = modelPrefab;
            Description = description;
        }

        public string Name { get; }
        public int Count { get; set; }
        public GameObject ModelPrefab { get; set; }
        public string Description { get; }
    }
}
