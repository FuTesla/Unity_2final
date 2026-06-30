using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TopDownGameplayBinder : MonoBehaviour
{
    [Header("Target Search")]
    public string preferredNameContains = "Medieval";
    public string fallbackNameContains = "replace";

    [Header("Controller")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 5.8f;
    public float characterRadius = 0.35f;
    public float characterHeight = 1.8f;
    public Vector3 characterCenter = new Vector3(0f, 0.9f, 0f);
    public string animatorControllerPath = "Assets/Gameplay/AnimationControllers/AC_Medieval_Player.controller";

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(-5f, 7.1f, -5f);

    [Header("Attack Test Enemy")]
    public string attackTestEnemyName = "Enemy_Stand";
    public float attackTestEnemyRadius = 0.35f;
    public float attackTestEnemyHeight = 1.8f;
    public Vector3 attackTestEnemyCenter = new Vector3(0f, 0.9f, 0f);
    public Vector3 attackTestHealthBarOffset = new Vector3(0f, 2.15f, 0f);
    public Vector2 attackTestHealthBarSize = new Vector2(1.35f, 0.16f);

    private void Start()
    {
        var target = FindCharacterRoot();
        if (target == null)
        {
            Debug.LogWarning("TopDownGameplayBinder could not find a Medieval character in the scene.");
            return;
        }

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
        motor.runSpeed = runSpeed;
        motor.cameraTransform = transform;

        var pauseMenu = target.GetComponent<PauseMenuController>();
        if (pauseMenu == null)
        {
            pauseMenu = target.gameObject.AddComponent<PauseMenuController>();
        }

        pauseMenu.toggleKey = KeyCode.Escape;
        pauseMenu.motor = motor;
        pauseMenu.inventoryUI = target.GetComponent<PlayerInventoryUI>();

        var animator = target.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
#if UNITY_EDITOR
            var animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorControllerPath);
            if (animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
            }
#endif
        }
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
        follow.lockRotation = true;

        var camera = GetComponent<Camera>();
        if (camera != null)
        {
            camera.orthographic = true;
            camera.orthographicSize = 6f;
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
}
