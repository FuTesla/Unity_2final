using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TopDownGameplayBinder : MonoBehaviour
{
    private const float MinimumRunSpeed = 6.4f;
    private const float MinimumRunAnimationSpeedMultiplier = 1.12f;

    [Header("Target Search")]
    public string preferredNameContains = "Medieval";
    public string fallbackNameContains = "replace";

    [Header("Controller")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 6.4f;
    public float runAnimationSpeedMultiplier = 1.12f;
    public float characterRadius = 0.35f;
    public float characterHeight = 1.8f;
    public Vector3 characterCenter = new Vector3(0f, 0.9f, 0f);
    public RuntimeAnimatorController animatorController;
    public string animatorControllerPath = "Assets/Gameplay/AnimationControllers/AC_Medieval_Player.controller";

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0f, 2.8f, -5.2f);
    public Vector3 cameraFocusOffset = new Vector3(0f, 1.35f, 0f);
    public float cameraDistance = 5.9f;
    public float perspectiveFieldOfView = 52f;
    public float perspectiveFocalLength = 38f;
    public bool disableCameraHdr = true;
    public string skyLightRootName = "P_Sky";

    [Header("Attack Test Enemy")]
    public string attackTestEnemyName = "Enemy_Stand";
    public float attackTestEnemyRadius = 0.35f;
    public float attackTestEnemyHeight = 1.8f;
    public Vector3 attackTestEnemyCenter = new Vector3(0f, 0.9f, 0f);
    public Vector3 attackTestHealthBarOffset = new Vector3(0f, 2.15f, 0f);
    public Vector2 attackTestHealthBarSize = new Vector2(1.35f, 0.16f);

    [Header("Player HUD")]
    public Vector2 playerHealthBarPosition = new Vector2(0f, 36f);
    public Vector2 playerHealthBarSize = new Vector2(420f, 30f);

    private void Start()
    {
        var target = FindCharacterRoot();
        if (target == null)
        {
            Debug.LogWarning("TopDownGameplayBinder could not find a Medieval character in the scene.");
            return;
        }

        RestrictSkyLights();
        BindCharacter(target);
        BindCamera(target);
        BindAttackTestEnemy();
    }

    private Transform FindCharacterRoot()
    {
        var preferred = FindByName(preferredNameContains);
        if (preferred != null)
        {
            return preferred;
        }

        var fallback = FindByName(fallbackNameContains);
        if (fallback != null)
        {
            return fallback;
        }

        foreach (var renderer in FindObjectsOfType<SkinnedMeshRenderer>())
        {
            return renderer.rootBone != null ? renderer.rootBone.root : renderer.transform.root;
        }

        return null;
    }

    private static Transform FindByName(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        foreach (var transform in FindObjectsOfType<Transform>())
        {
            if (transform.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return transform.root;
            }
        }

        return null;
    }

    private void BindCharacter(Transform target)
    {
        var controller = target.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = target.gameObject.AddComponent<CharacterController>();
        }

        controller.radius = characterRadius;
        controller.height = characterHeight;
        controller.center = characterCenter;
        controller.stepOffset = 0.35f;
        controller.slopeLimit = 45f;
        controller.skinWidth = 0.04f;

        var motor = target.GetComponent<TopDownCharacterMotor>();
        if (motor == null)
        {
            motor = target.gameObject.AddComponent<TopDownCharacterMotor>();
        }

        motor.walkSpeed = walkSpeed;
        motor.runSpeed = Mathf.Max(runSpeed, MinimumRunSpeed);
        motor.runAnimationSpeedMultiplier = Mathf.Max(runAnimationSpeedMultiplier, MinimumRunAnimationSpeedMultiplier);
        motor.cameraTransform = transform;

        var pauseMenu = target.GetComponent<PauseMenuController>();
        if (pauseMenu == null)
        {
            pauseMenu = target.gameObject.AddComponent<PauseMenuController>();
        }

        pauseMenu.toggleKey = KeyCode.Escape;
        pauseMenu.motor = motor;
        pauseMenu.inventoryUI = target.GetComponent<PlayerInventoryUI>();

        var health = target.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = target.gameObject.AddComponent<PlayerHealth>();
        }

        EnsurePlayerHealthBar(health);

        var animator = target.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            var runtimeController = animatorController != null ? animatorController : animator.runtimeAnimatorController;
#if UNITY_EDITOR
            if (runtimeController == null)
            {
                runtimeController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorControllerPath);
            }
#endif
            if (runtimeController != null)
            {
                animator.runtimeAnimatorController = runtimeController;
                motor.animatorController = runtimeController;
            }
        }
    }

    private void EnsurePlayerHealthBar(PlayerHealth health)
    {
        if (health == null || FindObjectOfType<PlayerHealthBar>() != null)
        {
            return;
        }

        var canvasObject = new GameObject("Player HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PlayerHealthBar));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 90;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        var bar = CreateRect("Health Bar", canvasObject.transform);
        bar.anchorMin = new Vector2(0.5f, 0f);
        bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = playerHealthBarPosition;
        bar.sizeDelta = playerHealthBarSize;

        var background = CreateHealthBarImage("Health Bar Background", bar, new Color(0.04f, 0.04f, 0.045f, 0.88f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        var fill = CreateHealthBarImage("Health Bar Fill", bar, Color.white);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(4f, 4f);
        fill.rectTransform.offsetMax = new Vector2(-4f, -4f);

        var healthBar = canvasObject.GetComponent<PlayerHealthBar>();
        healthBar.target = health;
        healthBar.fillImage = fill;
    }

    private void BindCamera(Transform target)
    {
        var follow = GetComponent<IsometricCameraFollow>();
        if (follow == null)
        {
            follow = gameObject.AddComponent<IsometricCameraFollow>();
        }

        follow.target = target;
        follow.offset = cameraOffset;
        follow.focusOffset = cameraFocusOffset;
        follow.cameraDistance = cameraDistance;
        follow.lockRotation = true;

        var occlusionFader = GetComponent<CameraOcclusionFader>();
        if (occlusionFader == null)
        {
            occlusionFader = gameObject.AddComponent<CameraOcclusionFader>();
        }

        occlusionFader.target = target;
        occlusionFader.enableOcclusionHandling = false;
        occlusionFader.renderPlayerOnTopWhenOccluded = false;

        var camera = GetComponent<Camera>();
        if (camera != null)
        {
            camera.orthographic = false;
            GameplayCameraExposureUtility.ApplyGameplayDefaults(camera, disableCameraHdr);
            camera.fieldOfView = perspectiveFieldOfView;
            camera.focalLength = perspectiveFocalLength;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1000f);
        }

        transform.position = target.position + cameraOffset;
        transform.LookAt(target.position + cameraFocusOffset);
        follow.ResetVelocity();

        if (GameObject.Find("Opening Menu Root") == null)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void RestrictSkyLights()
    {
        if (string.IsNullOrWhiteSpace(skyLightRootName))
        {
            return;
        }

        var skyRoot = GameObject.Find(skyLightRootName);
        if (skyRoot == null)
        {
            return;
        }

        var skyLayerMask = 1 << skyRoot.layer;
        foreach (var light in skyRoot.GetComponentsInChildren<Light>(true))
        {
            light.cullingMask = skyLayerMask;
        }
    }

    private void BindAttackTestEnemy()
    {
        var enemy = FindExactTransform(attackTestEnemyName);
        if (enemy == null)
        {
            return;
        }

        var controller = enemy.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = enemy.gameObject.AddComponent<CharacterController>();
        }

        controller.radius = attackTestEnemyRadius;
        controller.height = attackTestEnemyHeight;
        controller.center = attackTestEnemyCenter;
        controller.stepOffset = 0.35f;
        controller.slopeLimit = 45f;
        controller.skinWidth = 0.04f;

        var health = enemy.GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = enemy.gameObject.AddComponent<EnemyHealth>();
        }

        if (enemy.GetComponentInChildren<EnemyHealthBar>(true) == null)
        {
            CreateAttackTestHealthBar(enemy, health);
        }
    }

    private static Transform FindExactTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        foreach (var transform in FindObjectsOfType<Transform>())
        {
            if (transform.name == objectName)
            {
                return transform;
            }
        }

        return null;
    }

    private void CreateAttackTestHealthBar(Transform enemy, EnemyHealth health)
    {
        var canvasObject = new GameObject("Enemy Stand Health Bar", typeof(RectTransform), typeof(Canvas), typeof(EnemyHealthBar));
        canvasObject.transform.SetParent(enemy, false);
        canvasObject.transform.localPosition = attackTestHealthBarOffset;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = attackTestHealthBarSize * 100f;

        var background = CreateHealthBarImage("Background", canvasRect, new Color(0.1f, 0.08f, 0.06f, 0.78f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        var fill = CreateHealthBarImage("Fill", canvasRect, new Color(1f, 0.52f, 0.08f, 1f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var healthBar = canvasObject.GetComponent<EnemyHealthBar>();
        healthBar.target = health;
        healthBar.fillImage = fill;
        healthBar.targetCamera = GetComponent<Camera>();
    }

    private static Image CreateHealthBarImage(string name, RectTransform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        var image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }
}
