using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraOcclusionFader : MonoBehaviour
{
    private const string OverlayShaderName = "Custom/PlayerAlwaysOnTopOverlay";

    [Header("Target")]
    public Transform target;
    public Vector3 targetFocusOffset = new Vector3(0f, 1.05f, 0f);
    public Vector3 targetHeadOffset = new Vector3(0f, 1.65f, 0f);
    public Vector3 targetBodyOffset = new Vector3(0f, 0.65f, 0f);

    [Header("Occlusion Detection")]
    public bool enableOcclusionHandling = true;
    public LayerMask occlusionLayers = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    public float minTargetClearance = 0.35f;
    public bool includeRendererBoundsWithoutColliders = true;
    public float rendererBoundsScanInterval = 0.12f;
    public float rendererScreenTolerance = 45f;
    public float minOccluderTopRelativeToFocus = -0.25f;

    [Header("Player Overlay")]
    public bool renderPlayerOnTopWhenOccluded = true;
    public int overlayRenderQueue = 5000;
    public bool disablePlayerShadowsWhileOverlay;

    private readonly Dictionary<Renderer, PlayerOverlayRenderer> overlaidRenderers = new Dictionary<Renderer, PlayerOverlayRenderer>();
    private readonly List<Renderer> restoreBuffer = new List<Renderer>();
    private Camera targetCamera;
    private Renderer[] targetRenderers;
    private Renderer[] rendererCache;
    private float nextRendererScanTime;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (!enableOcclusionHandling || !renderPlayerOnTopWhenOccluded || target == null || targetCamera == null)
        {
            RestoreAll();
            return;
        }

        if (IsTargetOccluded())
        {
            ApplyTargetOverlay();
        }
        else
        {
            RestoreAll();
        }
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        RestoreAll();
    }

    private bool IsTargetOccluded()
    {
        var focusPoint = target.TransformPoint(targetFocusOffset);
        return IsPointOccluded(focusPoint)
            || IsPointOccluded(target.TransformPoint(targetHeadOffset))
            || IsPointOccluded(target.TransformPoint(targetBodyOffset))
            || IsRendererBoundsOccluded(focusPoint);
    }

    private bool IsPointOccluded(Vector3 point)
    {
        var cameraPosition = transform.position;
        var toPoint = point - cameraPosition;
        var distance = toPoint.magnitude - Mathf.Max(0f, minTargetClearance);
        if (distance <= 0.01f)
        {
            return false;
        }

        var ray = new Ray(cameraPosition, toPoint.normalized);
        var hits = Physics.RaycastAll(ray, distance, occlusionLayers, triggerInteraction);
        for (var i = 0; i < hits.Length; i++)
        {
            var hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
            if (hitTransform != null && !IsTargetTransform(hitTransform))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsRendererBoundsOccluded(Vector3 focusPoint)
    {
        if (!includeRendererBoundsWithoutColliders)
        {
            return false;
        }

        var focusScreenPoint = targetCamera.WorldToScreenPoint(focusPoint);
        if (focusScreenPoint.z <= targetCamera.nearClipPlane)
        {
            return false;
        }

        RefreshRendererCacheIfNeeded();
        if (rendererCache == null)
        {
            return false;
        }

        for (var i = 0; i < rendererCache.Length; i++)
        {
            if (RendererBlocksTarget(rendererCache[i], focusPoint, focusScreenPoint))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshRendererCacheIfNeeded()
    {
        if (rendererCache != null && Time.unscaledTime < nextRendererScanTime)
        {
            return;
        }

        rendererCache = FindObjectsOfType<Renderer>(true);
        nextRendererScanTime = Time.unscaledTime + Mathf.Max(0.02f, rendererBoundsScanInterval);
    }

    private bool RendererBlocksTarget(Renderer renderer, Vector3 focusPoint, Vector3 focusScreenPoint)
    {
        if (renderer == null || !renderer.enabled || IsTargetTransform(renderer.transform))
        {
            return false;
        }

        var bounds = renderer.bounds;
        if (bounds.max.y < focusPoint.y + minOccluderTopRelativeToFocus)
        {
            return false;
        }

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var hasPointInFrontOfTarget = false;

        IncludeBoundsScreenPoint(bounds.center, focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);
        IncludeBoundsScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z), focusScreenPoint, ref min, ref max, ref hasPointInFrontOfTarget);

        if (!hasPointInFrontOfTarget)
        {
            return false;
        }

        var tolerance = Mathf.Max(0f, rendererScreenTolerance);
        return focusScreenPoint.x >= min.x - tolerance
            && focusScreenPoint.x <= max.x + tolerance
            && focusScreenPoint.y >= min.y - tolerance
            && focusScreenPoint.y <= max.y + tolerance;
    }

    private void IncludeBoundsScreenPoint(
        Vector3 worldPoint,
        Vector3 focusScreenPoint,
        ref Vector2 min,
        ref Vector2 max,
        ref bool hasPointInFrontOfTarget)
    {
        var screenPoint = targetCamera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= targetCamera.nearClipPlane)
        {
            return;
        }

        if (screenPoint.z < focusScreenPoint.z - Mathf.Max(0f, minTargetClearance))
        {
            hasPointInFrontOfTarget = true;
        }

        min.x = Mathf.Min(min.x, screenPoint.x);
        min.y = Mathf.Min(min.y, screenPoint.y);
        max.x = Mathf.Max(max.x, screenPoint.x);
        max.y = Mathf.Max(max.y, screenPoint.y);
    }

    private void ApplyTargetOverlay()
    {
        RefreshTargetRenderers();
        if (targetRenderers == null)
        {
            return;
        }

        for (var i = 0; i < targetRenderers.Length; i++)
        {
            var renderer = targetRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!overlaidRenderers.TryGetValue(renderer, out var overlay))
            {
                overlay = new PlayerOverlayRenderer(renderer);
                overlaidRenderers.Add(renderer, overlay);
            }

            overlay.Apply(overlayRenderQueue, disablePlayerShadowsWhileOverlay);
        }

        RestoreMissingTargetRenderers();
    }

    private void RefreshTargetRenderers()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = target.GetComponentsInChildren<Renderer>(true);
        }
    }

    private void RestoreMissingTargetRenderers()
    {
        restoreBuffer.Clear();
        foreach (var pair in overlaidRenderers)
        {
            if (pair.Key == null || !IsTargetTransform(pair.Key.transform))
            {
                restoreBuffer.Add(pair.Key);
            }
        }

        for (var i = 0; i < restoreBuffer.Count; i++)
        {
            RestoreRenderer(restoreBuffer[i]);
        }
    }

    private void RestoreAll()
    {
        restoreBuffer.Clear();
        foreach (var pair in overlaidRenderers)
        {
            restoreBuffer.Add(pair.Key);
        }

        for (var i = 0; i < restoreBuffer.Count; i++)
        {
            RestoreRenderer(restoreBuffer[i]);
        }
    }

    private void RestoreRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            overlaidRenderers.Remove(renderer);
            return;
        }

        if (overlaidRenderers.TryGetValue(renderer, out var overlay))
        {
            overlay.Restore();
            overlaidRenderers.Remove(renderer);
        }
    }

    private bool IsTargetTransform(Transform candidate)
    {
        return target != null && candidate != null && candidate.root == target.root;
    }

    private sealed class PlayerOverlayRenderer
    {
        private readonly Renderer renderer;
        private readonly Material[] originalSharedMaterials;
        private readonly ShadowCastingMode originalShadowCastingMode;
        private readonly bool originalReceiveShadows;
        private Material[] runtimeMaterials;

        public PlayerOverlayRenderer(Renderer renderer)
        {
            this.renderer = renderer;
            originalSharedMaterials = renderer.sharedMaterials;
            originalShadowCastingMode = renderer.shadowCastingMode;
            originalReceiveShadows = renderer.receiveShadows;
        }

        public void Apply(int renderQueue, bool disableShadows)
        {
            if (runtimeMaterials == null)
            {
                var sourceMaterials = renderer.sharedMaterials;
                runtimeMaterials = new Material[sourceMaterials.Length];
                for (var i = 0; i < sourceMaterials.Length; i++)
                {
                    runtimeMaterials[i] = CreateOverlayMaterial(sourceMaterials[i]);
                }

                renderer.sharedMaterials = runtimeMaterials;
            }

            for (var i = 0; i < runtimeMaterials.Length; i++)
            {
                ConfigureOverlayMaterial(runtimeMaterials[i], renderQueue);
            }

            if (disableShadows)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void Restore()
        {
            renderer.sharedMaterials = originalSharedMaterials;
            renderer.shadowCastingMode = originalShadowCastingMode;
            renderer.receiveShadows = originalReceiveShadows;

            if (runtimeMaterials == null)
            {
                return;
            }

            for (var i = 0; i < runtimeMaterials.Length; i++)
            {
                if (runtimeMaterials[i] != null)
                {
                    UnityEngine.Object.Destroy(runtimeMaterials[i]);
                }
            }

            runtimeMaterials = null;
        }

        private static void ConfigureOverlayMaterial(Material material, int renderQueue)
        {
            if (material == null)
            {
                return;
            }

            material.SetInt("_ZTest", (int)CompareFunction.Always);
            material.SetInt("_ZWrite", 1);
            material.renderQueue = Mathf.Max((int)RenderQueue.Overlay, renderQueue);
        }

        private static Material CreateOverlayMaterial(Material source)
        {
            var shader = Shader.Find(OverlayShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material material;
            if (shader != null)
            {
                material = new Material(shader);
            }
            else if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                return null;
            }

            material.name = source != null ? source.name + " Always On Top" : "Player Always On Top";

            if (source != null)
            {
                CopyTexture(source, material, "_BaseMap");
                CopyTexture(source, material, "_MainTex");
                CopyColor(source, material, "_BaseColor");
                CopyColor(source, material, "_Color");
            }

            return material;
        }

        private static void CopyTexture(Material source, Material target, string propertyName)
        {
            if (source.HasProperty(propertyName) && target.HasProperty("_BaseMap"))
            {
                target.SetTexture("_BaseMap", source.GetTexture(propertyName));
            }
        }

        private static void CopyColor(Material source, Material target, string propertyName)
        {
            if (source.HasProperty(propertyName) && target.HasProperty("_BaseColor"))
            {
                target.SetColor("_BaseColor", source.GetColor(propertyName));
            }
        }
    }
}
