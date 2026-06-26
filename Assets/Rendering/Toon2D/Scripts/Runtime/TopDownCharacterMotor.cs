using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class TopDownCharacterMotor : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 5.8f;
    public float rotationSpeed = 720f;
    public float gravity = -20f;
    public KeyCode runKey = KeyCode.LeftShift;

    [Header("Input Space")]
    public Transform cameraTransform;

    [Header("Animation")]
    public RuntimeAnimatorController animatorController;

    private CharacterController characterController;
    private Animator animator;
    private float verticalVelocity;
    private float basePitch;
    private float baseRoll;

    public float MoveAmount { get; private set; }
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
        }

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

        UpdateAnimator(input.magnitude, isRunning);
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

        var speed01 = inputAmount <= 0.05f ? 0f : isRunning ? 1f : 0.5f;
        SetFloatIfExists("Speed", speed01);
        SetFloatIfExists("MoveSpeed", speed01);
        SetBoolIfExists("IsMoving", speed01 > 0.05f);
        SetBoolIfExists("Moving", speed01 > 0.05f);
        SetBoolIfExists("IsRunning", isRunning);
        SetBoolIfExists("Running", isRunning);
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
}
