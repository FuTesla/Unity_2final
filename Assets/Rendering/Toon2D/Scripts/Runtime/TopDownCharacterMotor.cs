using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CharacterController))]
public sealed class TopDownCharacterMotor : MonoBehaviour
{
    private const float MinimumRunSpeed = 6.4f;
    private const float MinimumRunAnimationSpeedMultiplier = 1.12f;

    public enum WeaponType
    {
        Unarmed,
        Sword,
        Handgun
    }

    [Header("Movement")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 6.4f;
    public float rotationSpeed = 720f;
    public float gravity = -20f;
    public KeyCode runKey = KeyCode.LeftShift;

    [Header("Input Space")]
    public Transform cameraTransform;

    [Header("Animation")]
    public RuntimeAnimatorController animatorController;
    public string attackTrigger = "Attack";
    public string swordAttackStateName = "Sword Slash";
    public string punchRightTrigger = "PunchRight";
    public string punchLeftTrigger = "PunchLeft";
    public string kickRightTrigger = "KickRight";
    public string punchRightStateName = "Punch Right";
    public string punchLeftStateName = "Punch Left";
    public string kickRightStateName = "Kick Right";
    public float fallbackSwordAttackDuration = 1.05f;
    public float unarmedComboResetDelay = 0.7f;
    public float fallbackUnarmedAttackDuration = 0.85f;
    public KeyCode interactKey = KeyCode.F;
    public string interactTrigger = "Interact";
    public string interactStateName = "Interact";
    public float fallbackInteractDuration = 0.8f;
    public float runAnimationSpeedMultiplier = 1.12f;

    [Header("Combat")]
    public KeyCode toggleWeaponKey = KeyCode.X;
    public string swordObjectName = "Sword";
    public float attackDamage = 25f;
    public float swordAttackDamage = 25f;
    public float unarmedPunchDamage = 10f;
    public float unarmedKickDamage = 15f;
    public float attackRange = 1.85f;
    public float attackRadius = 0.85f;
    public string attackTestEnemyName = "Enemy_Stand";
    public float fallbackAttackExtraRange = 0.75f;
    public WeaponType currentWeapon = WeaponType.Unarmed;

    private CharacterController characterController;
    private Animator animator;
    private Transform swordObject;
    private float verticalVelocity;
    private float basePitch;
    private float baseRoll;
    private float swordAttackReadyTime;
    private float unarmedAttackReadyTime;
    private float interactAnimationEndTime;
    private float lastUnarmedAttackEndTime = float.NegativeInfinity;
    private int unarmedAttackIndex;

    public WeaponType CurrentWeaponType => currentWeapon;

    public float MoveAmount { get; private set; }
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        runSpeed = Mathf.Max(runSpeed, MinimumRunSpeed);
        runAnimationSpeedMultiplier = Mathf.Max(runAnimationSpeedMultiplier, MinimumRunAnimationSpeedMultiplier);

        animator = GetComponentInChildren<Animator>();
        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
        }

        swordObject = FindDeepChild(transform, swordObjectName);
        ApplyWeaponVisuals();

        var initialEuler = transform.eulerAngles;
        basePitch = initialEuler.x;
        baseRoll = initialEuler.z;
    }

    private void Update()
    {
        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);

        var moveDirection = GetCameraRelativeDirection(input);
        var isRunning = input.sqrMagnitude > 0.001f && Input.GetKey(runKey);
        MoveAmount = input.magnitude;
        IsRunning = isRunning;

        var targetSpeed = isRunning ? runSpeed : walkSpeed;
        var horizontalVelocity = moveDirection * targetSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        var velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            var targetYaw = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            var currentYaw = transform.eulerAngles.y;
            var nextYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(basePitch, nextYaw, baseRoll);
        }

        if (Time.time < interactAnimationEndTime)
        {
            SetMovementAnimationStopped();
        }
        else
        {
            UpdateAnimator(input.magnitude, isRunning);
        }

        if (Input.GetKeyDown(toggleWeaponKey))
        {
            ToggleWeapon();
        }

        if (currentWeapon == WeaponType.Unarmed)
        {
            UpdateUnarmedComboTimeout();
        }

        if (Input.GetMouseButtonDown(0) && IsPointerOverUi())
        {
            return;
        }

        if (currentWeapon == WeaponType.Sword && Input.GetMouseButtonDown(0))
        {
            if (TryPlaySwordAttack())
            {
                TryHitEnemy(swordAttackDamage);
            }
        }

        if (currentWeapon == WeaponType.Unarmed && Input.GetMouseButtonDown(0))
        {
            if (TryPlayNextUnarmedAttack(out var damage))
            {
                TryHitEnemy(damage);
            }
        }

        if (Input.GetKeyDown(interactKey))
        {
            PlayInteractAnimation();
        }
    }

    private void ToggleWeapon()
    {
        SetWeaponType(currentWeapon == WeaponType.Sword ? WeaponType.Unarmed : WeaponType.Sword);
    }

    public void SetWeaponType(WeaponType weaponType)
    {
        currentWeapon = weaponType;
        ApplyWeaponVisuals();
        unarmedAttackIndex = 0;
        swordAttackReadyTime = 0f;
        unarmedAttackReadyTime = 0f;
        lastUnarmedAttackEndTime = float.NegativeInfinity;
    }

    public void SelectUnarmed()
    {
        SetWeaponType(WeaponType.Unarmed);
    }

    public void SelectSword()
    {
        SetWeaponType(WeaponType.Sword);
    }

    public void SelectHandgun()
    {
        SetWeaponType(WeaponType.Handgun);
    }

    private void ApplyWeaponVisuals()
    {
        SetSwordVisible(currentWeapon == WeaponType.Sword);
    }

    private void SetSwordVisible(bool visible)
    {
        if (swordObject == null)
        {
            swordObject = FindDeepChild(transform, swordObjectName);
        }

        if (swordObject != null)
        {
            swordObject.gameObject.SetActive(visible);
        }
    }

    private void UpdateUnarmedComboTimeout()
    {
        if (unarmedAttackIndex == 0 || Time.time < unarmedAttackReadyTime)
        {
            return;
        }

        if (Time.time - lastUnarmedAttackEndTime > unarmedComboResetDelay)
        {
            unarmedAttackIndex = 0;
        }
    }

    private bool TryPlaySwordAttack()
    {
        if (Time.time < swordAttackReadyTime)
        {
            return false;
        }

        SetMovementAnimationStopped();
        SetAnimatorSpeed(1f);

        swordAttackReadyTime = Time.time + GetAttackDuration(swordAttackStateName, "Sword_Slash", fallbackSwordAttackDuration);

        if (animator != null && !string.IsNullOrEmpty(swordAttackStateName))
        {
            animator.CrossFadeInFixedTime(swordAttackStateName, 0.05f, 0, 0f);
        }

        return true;
    }

    private bool TryPlayNextUnarmedAttack(out float damage)
    {
        damage = 0f;

        if (Time.time < unarmedAttackReadyTime)
        {
            return false;
        }

        UpdateUnarmedComboTimeout();

        SetMovementAnimationStopped();
        SetAnimatorSpeed(1f);

        string stateName;
        switch (unarmedAttackIndex)
        {
            case 0:
                stateName = punchRightStateName;
                damage = unarmedPunchDamage;
                break;
            case 1:
                stateName = punchLeftStateName;
                damage = unarmedPunchDamage;
                break;
            default:
                stateName = kickRightStateName;
                damage = unarmedKickDamage;
                break;
        }

        unarmedAttackIndex = (unarmedAttackIndex + 1) % 3;
        var duration = GetUnarmedAttackDuration(stateName);
        unarmedAttackReadyTime = Time.time + duration;
        lastUnarmedAttackEndTime = unarmedAttackReadyTime;

        if (animator != null && !string.IsNullOrEmpty(stateName))
        {
            animator.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
        }

        return true;
    }

    private float GetUnarmedAttackDuration(string stateName)
    {
        return GetAttackDuration(stateName, GetUnarmedClipToken(stateName), fallbackUnarmedAttackDuration);
    }

    private float GetAttackDuration(string stateName, string clipToken, float fallbackDuration)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
        {
            return fallbackDuration;
        }

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.IndexOf(clipToken, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Mathf.Max(0.05f, clip.length);
            }
        }

        return fallbackDuration;
    }

    private void PlayInteractAnimation()
    {
        SetMovementAnimationStopped();
        SetAnimatorSpeed(1f);
        SetTriggerIfExists(interactTrigger);

        var duration = GetAnimationDuration(interactStateName, "Interact", fallbackInteractDuration);
        interactAnimationEndTime = Time.time + duration;

        if (animator != null && HasAnimatorState(interactStateName))
        {
            animator.CrossFadeInFixedTime(interactStateName, 0.05f, 0, 0f);
        }
    }

    private float GetAnimationDuration(string stateName, string clipToken, float fallbackDuration)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
        {
            return fallbackDuration;
        }

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.IndexOf(clipToken, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Mathf.Max(0.05f, clip.length);
            }
        }

        return fallbackDuration;
    }

    private bool HasAnimatorState(string stateName)
    {
        return animator != null
            && !string.IsNullOrEmpty(stateName)
            && animator.HasState(0, Animator.StringToHash(stateName));
    }

    private string GetUnarmedClipToken(string stateName)
    {
        if (stateName == punchRightStateName)
        {
            return "Punch_Right";
        }

        if (stateName == punchLeftStateName)
        {
            return "Punch_Left";
        }

        if (stateName == kickRightStateName)
        {
            return "Kick_Right";
        }

        return stateName.Replace(" ", "_");
    }

    private void TryHitEnemy(float damage)
    {
        var hitCenter = transform.position + transform.forward * attackRange;
        var hits = Physics.OverlapSphere(hitCenter, attackRadius, ~0, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<EnemyHealth>();
            if (TryDamageEnemy(enemy, damage))
            {
                return;
            }
        }

        TryDamageEnemy(FindAttackTestEnemyHealth(), damage);
    }

    private bool TryDamageEnemy(EnemyHealth enemy, float damage)
    {
        if (enemy == null || enemy.IsDead)
        {
            return false;
        }

        if (!IsEnemyInAttackArc(enemy.transform))
        {
            return false;
        }

        enemy.TakeDamage(damage);
        return true;
    }

    private bool IsEnemyInAttackArc(Transform enemy)
    {
        var toEnemy = enemy.position - transform.position;
        toEnemy.y = 0f;

        var distance = toEnemy.magnitude;
        if (distance <= 0.001f || distance > attackRange + attackRadius + fallbackAttackExtraRange)
        {
            return false;
        }

        return Vector3.Dot(transform.forward, toEnemy / distance) >= 0.15f;
    }

    private EnemyHealth FindAttackTestEnemyHealth()
    {
        var enemy = FindExactTransform(attackTestEnemyName);
        if (enemy == null)
        {
            return null;
        }

        var health = enemy.GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = enemy.gameObject.AddComponent<EnemyHealth>();
        }

        if (enemy.GetComponent<CharacterController>() == null)
        {
            var controller = enemy.gameObject.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
        }

        return health;
    }

    private static Transform FindExactTransform(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        foreach (var candidate in FindObjectsOfType<Transform>())
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        var reference = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
        var forward = reference != null ? reference.forward : Vector3.forward;
        var right = reference != null ? reference.right : Vector3.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * input.y + right * input.x).normalized;
    }

    private void UpdateAnimator(float inputAmount, bool isRunning)
    {
        if (animator == null)
        {
            return;
        }

        SetAnimatorSpeed(isRunning ? runAnimationSpeedMultiplier : 1f);
        var speed01 = inputAmount <= 0.05f ? 0f : isRunning ? 1f : 0.5f;
        SetFloatIfExists("Speed", speed01);
        SetFloatIfExists("MoveSpeed", speed01);
        SetBoolIfExists("IsMoving", speed01 > 0.05f);
        SetBoolIfExists("Moving", speed01 > 0.05f);
        SetBoolIfExists("IsRunning", isRunning);
        SetBoolIfExists("Running", isRunning);
    }

    private void SetMovementAnimationStopped()
    {
        SetFloatIfExists("Speed", 0f);
        SetFloatIfExists("MoveSpeed", 0f);
        SetBoolIfExists("IsMoving", false);
        SetBoolIfExists("Moving", false);
        SetBoolIfExists("IsRunning", false);
        SetBoolIfExists("Running", false);
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (animator != null)
        {
            animator.speed = speed;
        }
    }

    private void SetFloatIfExists(string parameterName, float value)
    {
        if (animator == null)
        {
            return;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == parameterName)
            {
                animator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private void SetBoolIfExists(string parameterName, bool value)
    {
        if (animator == null)
        {
            return;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private bool SetTriggerIfExists(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                animator.SetTrigger(parameterName);
                return true;
            }
        }

        return false;
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

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
