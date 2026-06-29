using UnityEngine;

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
}
