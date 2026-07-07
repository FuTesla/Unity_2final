using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class EnemyAIController : MonoBehaviour
{
    public Transform player;
    public RuntimeAnimatorController animatorController;
    public float detectionDistance = 25f;
    public float attackDistance = 1.55f;
    public float moveSpeed = 3.4f;
    public float rotationSpeed = 720f;
    public float gravity = -20f;
    public float attackCooldown = 1.35f;
    public float attackDamage = 40f;
    public float chaseAnimationSpeed = 1f;
    public string attackTrigger = "Attack";
    public string attackStateName = "Sword Slash";
    public string locomotionStateName = "Locomotion";
    public string targetHandName = "Hand.R";
    public string swordObjectName = "Enemy Sword";

    private CharacterController characterController;
    private Animator animator;
    private EnemyHealth health;
    private PlayerHealth playerHealth;
    private float verticalVelocity;
    private float nextAttackTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<EnemyHealth>();

        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.speed = 1f;
        }
    }

    private void Start()
    {
        if (player == null)
        {
            var playerObject = GameObject.Find("Medieval");
            player = playerObject != null ? playerObject.transform : null;
        }

        playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        EnsureSword();
    }

    private void Update()
    {
        if (health == null)
        {
            health = GetComponent<EnemyHealth>();
        }

        if (health != null && health.IsDead)
        {
            return;
        }

        if (player == null || playerHealth == null || playerHealth.IsDead)
        {
            SetMovementAnimation(0f, false);
            ApplyGravityOnly();
            return;
        }

        var toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        var distance = toPlayer.magnitude;

        if (distance > detectionDistance)
        {
            SetMovementAnimation(0f, false);
            ApplyGravityOnly();
            return;
        }

        if (toPlayer.sqrMagnitude > 0.001f)
        {
            RotateToward(toPlayer.normalized);
        }

        if (distance > attackDistance)
        {
            MoveToward(toPlayer.normalized);
            PlayLocomotionAnimation();
            return;
        }

        SetMovementAnimation(0f, false);
        ApplyGravityOnly();
        TryAttack();
    }

    private void MoveToward(Vector3 direction)
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        var velocity = direction * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void ApplyGravityOnly()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private void RotateToward(Vector3 direction)
    {
        var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        SetTriggerIfExists(attackTrigger);
        if (animator != null && !string.IsNullOrEmpty(attackStateName))
        {
            animator.CrossFadeInFixedTime(attackStateName, 0.05f);
        }

        playerHealth.TakeDamage(attackDamage);
    }

    private void PlayLocomotionAnimation()
    {
        SetMovementAnimation(chaseAnimationSpeed, chaseAnimationSpeed >= 0.95f);

        if (animator == null || string.IsNullOrEmpty(locomotionStateName))
        {
            return;
        }

        var currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (!currentState.IsName(locomotionStateName))
        {
            animator.CrossFadeInFixedTime(locomotionStateName, 0.08f);
        }
    }

    private void EnsureSword()
    {
        var targetHand = EnemySwordBinder.FindLikelyRightHand(transform, targetHandName);
        if (targetHand == null || targetHand.Find(swordObjectName) != null)
        {
            return;
        }

        var sourceSword = player != null ? FindDeepChild(player, "Sword") : null;
        if (sourceSword != null)
        {
            var sword = Instantiate(sourceSword.gameObject, targetHand);
            sword.name = swordObjectName;
            sword.transform.localPosition = sourceSword.localPosition;
            sword.transform.localRotation = sourceSword.localRotation;
            sword.transform.localScale = sourceSword.localScale;
            return;
        }

        CreateFallbackSword(targetHand);
    }

    private void CreateFallbackSword(Transform targetHand)
    {
        var root = new GameObject(swordObjectName);
        root.transform.SetParent(targetHand, false);
        root.transform.localPosition = new Vector3(0.02f, 0.08f, 0.02f);
        root.transform.localRotation = Quaternion.Euler(8f, 84f, 174f);
        root.transform.localScale = Vector3.one;

        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "Blade";
        blade.transform.SetParent(root.transform, false);
        blade.transform.localPosition = new Vector3(0f, 0.34f, 0f);
        blade.transform.localScale = new Vector3(0.055f, 0.7f, 0.035f);
        RemoveCollider(blade);

        var guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        guard.name = "Guard";
        guard.transform.SetParent(root.transform, false);
        guard.transform.localPosition = new Vector3(0f, -0.03f, 0f);
        guard.transform.localScale = new Vector3(0.28f, 0.045f, 0.045f);
        RemoveCollider(guard);

        var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.name = "Handle";
        handle.transform.SetParent(root.transform, false);
        handle.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        handle.transform.localScale = new Vector3(0.055f, 0.24f, 0.055f);
        RemoveCollider(handle);
    }

    private static void RemoveCollider(GameObject obj)
    {
        var collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
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

    private void SetMovementAnimation(float speed, bool running)
    {
        if (animator == null)
        {
            return;
        }

        SetFloatIfExists("Speed", speed);
        SetFloatIfExists("MoveSpeed", speed);
        SetBoolIfExists("IsMoving", speed > 0.05f);
        SetBoolIfExists("Moving", speed > 0.05f);
        SetBoolIfExists("IsRunning", running);
        SetBoolIfExists("Running", running);
    }

    private void SetFloatIfExists(string parameterName, float value)
    {
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
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void SetTriggerIfExists(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                animator.SetTrigger(parameterName);
                return;
            }
        }
    }
}
