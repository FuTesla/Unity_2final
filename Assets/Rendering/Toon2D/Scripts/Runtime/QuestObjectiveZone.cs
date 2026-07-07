using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class QuestObjectiveZone : MonoBehaviour
{
    [Header("Editable Zone")]
    public Vector3 zoneCenter = Vector3.zero;
    public Vector3 zoneSize = new Vector3(4.2f, 2.6f, 4.2f);

    [Header("Quest")]
    public string requiredQuestText = "\u7EE7\u7EED\u5411\u524D\u63A2\u7D22";
    public string completionText = "\u4EFB\u52A1\u5B8C\u6210";

    private const string VisualName = "Quest Objective Visual";

    private BoxCollider triggerCollider;
    private Rigidbody triggerBody;
    private Transform visualRoot;
    private Material runtimeMaterial;
    private bool isCompleted;
    private TopDownCharacterMotor playerMotor;
    private CharacterController playerController;

    private void OnEnable()
    {
        EnsureZoneSetup();
    }

    private void Reset()
    {
        EnsureZoneSetup();
    }

    private void OnValidate()
    {
        EnsureZoneSetup();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            TryCompleteQuestFromPlayerPosition();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCompleteQuestFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCompleteQuestFromCollider(other);
    }

    public Bounds GetWorldBounds(Vector3 padding)
    {
        var worldSize = Vector3.Scale(
            new Vector3(
                Mathf.Max(zoneSize.x, 0.1f),
                Mathf.Max(zoneSize.y, 0.1f),
                Mathf.Max(zoneSize.z, 0.1f)),
            Abs(transform.lossyScale));

        return new Bounds(transform.TransformPoint(zoneCenter), worldSize + padding);
    }

    private void EnsureZoneSetup()
    {
        EnsureTriggerCollider();

        EnsureVisual();
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider>();
        }

        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        triggerCollider.enabled = true;
        triggerCollider.isTrigger = true;
        triggerCollider.center = zoneCenter;
        triggerCollider.size = zoneSize;

        if (triggerBody == null)
        {
            triggerBody = GetComponent<Rigidbody>();
        }

        if (triggerBody == null)
        {
            triggerBody = gameObject.AddComponent<Rigidbody>();
        }

        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
        triggerBody.detectCollisions = true;
    }

    private void EnsureVisual()
    {
        if (visualRoot == null)
        {
            var existing = transform.Find(VisualName);
            if (existing != null)
            {
                visualRoot = existing;
            }
        }

        if (visualRoot == null)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = VisualName;
            primitive.transform.SetParent(transform, false);
            var primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(primitiveCollider);
                }
                else
                {
                    DestroyImmediate(primitiveCollider);
                }
            }

            visualRoot = primitive.transform;
        }

        RemoveVisualColliders();

        visualRoot.localPosition = zoneCenter;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = zoneSize;

        var renderer = visualRoot.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            var shader = Shader.Find("Custom/DamageZoneRed");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "Quest Objective Green"
            };

            if (runtimeMaterial.HasProperty("_BaseColor"))
            {
                runtimeMaterial.SetColor("_BaseColor", new Color(0.18f, 0.78f, 0.28f, 0.38f));
            }
            else if (runtimeMaterial.HasProperty("_Color"))
            {
                runtimeMaterial.color = new Color(0.18f, 0.78f, 0.28f, 0.38f);
            }

            if (runtimeMaterial.HasProperty("_Surface"))
            {
                runtimeMaterial.SetFloat("_Surface", 1f);
            }

            if (runtimeMaterial.HasProperty("_Blend"))
            {
                runtimeMaterial.SetFloat("_Blend", 0f);
            }

            runtimeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            runtimeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            runtimeMaterial.SetInt("_ZWrite", 0);
            runtimeMaterial.DisableKeyword("_ALPHATEST_ON");
            runtimeMaterial.EnableKeyword("_ALPHABLEND_ON");
            runtimeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            runtimeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        renderer.sharedMaterial = runtimeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void RemoveVisualColliders()
    {
        if (visualRoot == null)
        {
            return;
        }

        foreach (var collider in visualRoot.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }
    }

    private void TryCompleteQuestFromPlayerPosition()
    {
        if (isCompleted || !CanCompleteCurrentQuest())
        {
            return;
        }

        if (playerMotor == null)
        {
            playerMotor = FindObjectOfType<TopDownCharacterMotor>();
        }

        if (playerMotor == null)
        {
            return;
        }

        if (playerController == null)
        {
            playerController = playerMotor.GetComponent<CharacterController>();
        }

        var zoneBounds = GetWorldBounds(new Vector3(0.8f, 1.2f, 0.8f));

        var insideZone = playerController != null
            ? zoneBounds.Intersects(playerController.bounds)
            : zoneBounds.Contains(playerMotor.transform.position);

        if (!insideZone)
        {
            return;
        }

        isCompleted = true;
        QuestTrackerUI.ShowCompletedQuest(requiredQuestText, completionText);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void TryCompleteQuestFromCollider(Collider other)
    {
        if (isCompleted || other == null || !CanCompleteCurrentQuest())
        {
            return;
        }

        if (other.GetComponentInParent<TopDownCharacterMotor>() == null)
        {
            return;
        }

        isCompleted = true;
        QuestTrackerUI.ShowCompletedQuest(requiredQuestText, completionText);
    }

    private bool CanCompleteCurrentQuest()
    {
        if (!QuestTrackerUI.HasActiveQuest || QuestTrackerUI.IsCompleted)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(requiredQuestText)
            || QuestTrackerUI.IsCurrentQuest(requiredQuestText);
    }
}
