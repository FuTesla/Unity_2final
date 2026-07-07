using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraOcclusionFader : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetFocusOffset = new Vector3(0f, 1.05f, 0f);
    public Vector3 targetHeadOffset = new Vector3(0f, 1.65f, 0f);
    public Vector3 targetBodyOffset = new Vector3(0f, 0.65f, 0f);

    [Header("Occlusion Detection")]
    public bool enableOcclusionHandling;
    public LayerMask occlusionLayers = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    public float minTargetClearance = 0.35f;
    public bool includeRendererBoundsWithoutColliders = true;
    public float rendererBoundsScanInterval = 0.12f;
    public float rendererScreenTolerance = 45f;
    public float minOccluderTopRelativeToFocus = -0.25f;

    [Header("Player Overlay")]
    public bool renderPlayerOnTopWhenOccluded;
    public int overlayRenderQueue = 5000;
    public bool disablePlayerShadowsWhileOverlay;
}
