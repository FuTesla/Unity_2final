using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class IsometricCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(-24.5f, 34.8f, -24.5f);
    public float followSmoothTime = 0.08f;
    public bool lockRotation = true;
    public KeyCode panLeftKey = KeyCode.Q;
    public KeyCode panRightKey = KeyCode.E;
    public float keyPanDistance = 2.2f;
    public float keyPanSmoothTime = 0.08f;
    public float damageShakeInterval = 0.7f;
    public float damageShakeDuration = 0.16f;
    public float damageShakeAmplitude = 0.35f;

    private Vector3 velocity;
    private Vector3 panVelocity;
    private Vector3 currentKeyPanOffset;
    private float shakeEndTime;
    private float nextAllowedShakeTime;

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }

    public void TryPlayDamageShake()
    {
        if (Time.time < nextAllowedShakeTime)
        {
            return;
        }

        nextAllowedShakeTime = Time.time + Mathf.Max(0.01f, damageShakeInterval);
        shakeEndTime = Mathf.Max(shakeEndTime, Time.time + Mathf.Max(0.01f, damageShakeDuration));
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateKeyPanOffset();

        var desiredPosition = target.position + offset + currentKeyPanOffset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, followSmoothTime);

        if (Time.time < shakeEndTime)
        {
            transform.position += GetShakeOffset();
        }

        if (lockRotation)
        {
            var lookDirection = target.position - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }
        else
        {
            transform.LookAt(target.position);
        }
    }

    private void UpdateKeyPanOffset()
    {
        var targetPanOffset = Vector3.zero;
        var right = transform.right;
        right.y = 0f;

        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        if (Input.GetKey(panLeftKey))
        {
            targetPanOffset -= right * keyPanDistance;
        }

        if (Input.GetKey(panRightKey))
        {
            targetPanOffset += right * keyPanDistance;
        }

        currentKeyPanOffset = Vector3.SmoothDamp(
            currentKeyPanOffset,
            targetPanOffset,
            ref panVelocity,
            Mathf.Max(0.01f, keyPanSmoothTime));
    }

    private Vector3 GetShakeOffset()
    {
        var random = Random.insideUnitSphere * damageShakeAmplitude;
        random.y *= 0.65f;
        return random;
    }
}
