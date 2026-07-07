using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class IsometricCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2.8f, -5.2f);
    public Vector3 focusOffset = new Vector3(0f, 1.35f, 0f);
    public float followSmoothTime = 0.08f;
    public bool lockRotation = true;
    public float cameraDistance = 5.9f;
    public float mouseSensitivityX = 3.2f;
    public float mouseSensitivityY = 2.2f;
    public float defaultPitch = 28f;
    public float minPitch = -18f;
    public float maxPitch = 58f;
    public float damageShakeInterval = 0.7f;
    public float damageShakeDuration = 0.16f;
    public float damageShakeAmplitude = 0.35f;

    private Vector3 velocity;
    private float shakeEndTime;
    private float nextAllowedShakeTime;
    private float yaw;
    private float pitch;
    private bool orbitInitialized;

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
        orbitInitialized = false;
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

        EnsureOrbitInitialized();
        UpdateMouseOrbit();

        var focusPoint = target.position + focusOffset;
        var orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        var desiredPosition = focusPoint + orbitRotation * Vector3.back * GetCameraDistance();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, followSmoothTime);

        if (Time.time < shakeEndTime)
        {
            transform.position += GetShakeOffset();
        }

        if (lockRotation)
        {
            var lookDirection = focusPoint - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }
        else
        {
            transform.LookAt(focusPoint);
        }
    }

    private void EnsureOrbitInitialized()
    {
        if (orbitInitialized)
        {
            return;
        }

        var euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = NormalizeAngle(euler.x);

        if (Mathf.Abs(pitch) < 0.001f)
        {
            pitch = defaultPitch;
        }

        if (target != null)
        {
            var toCamera = transform.position - (target.position + focusOffset);
            if (toCamera.sqrMagnitude > 0.001f)
            {
                yaw = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
                pitch = Mathf.Asin(Mathf.Clamp(toCamera.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        orbitInitialized = true;
    }

    private void UpdateMouseOrbit()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivityX;
        pitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivityY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private float GetCameraDistance()
    {
        if (cameraDistance > 0.01f)
        {
            return cameraDistance;
        }

        return Mathf.Max(0.01f, offset.magnitude);
    }

    private Vector3 GetShakeOffset()
    {
        var random = Random.insideUnitSphere * damageShakeAmplitude;
        random.y *= 0.65f;
        return random;
    }
}
