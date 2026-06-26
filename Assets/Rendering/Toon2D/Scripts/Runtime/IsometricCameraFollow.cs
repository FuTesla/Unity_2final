using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class IsometricCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(-5f, 7.1f, -5f);
    public float followSmoothTime = 0.08f;
    public bool lockRotation = true;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        var desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, followSmoothTime);

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
}
