using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class DamageZone : MonoBehaviour
{
    public float damagePerSecond = 10f;
    public float damageRadius = 1.3f;

    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void Reset()
    {
        var zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    public bool ContainsPosition(Vector3 position)
    {
        var planarOffset = new Vector2(position.x - transform.position.x, position.z - transform.position.z);
        if (planarOffset.sqrMagnitude <= damageRadius * damageRadius)
        {
            return true;
        }

        if (zoneCollider == null)
        {
            return false;
        }

        var bounds = zoneCollider.bounds;
        return position.x >= bounds.min.x - 0.35f
            && position.x <= bounds.max.x + 0.35f
            && position.z >= bounds.min.z - 0.35f
            && position.z <= bounds.max.z + 0.35f;
    }
}
