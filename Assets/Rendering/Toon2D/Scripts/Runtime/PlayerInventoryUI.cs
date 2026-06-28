using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerInventoryUI : MonoBehaviour
{
    private const int PreviewLayer = 31;

    public KeyCode toggleKey = KeyCode.Tab;
    public GameObject inventoryRoot;
    public TopDownCharacterMotor motor;
    public Animator animator;
    public InventoryBlurPostProcess blurPostProcess;
    public RawImage characterPreviewImage;
    public Camera characterPreviewCamera;
    public Transform characterPreviewStage;
    [Range(0f, 1f)] public float openBlurIntensity = 0.85f;

    private PlayerHealth health;
    private RenderTexture characterPreviewTexture;
    private GameObject characterPreviewInstance;
    private Animator characterPreviewAnimator;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<TopDownCharacterMotor>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        health = GetComponent<PlayerHealth>();
        EnsureCharacterPreview();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (characterPreviewTexture != null)
        {
            characterPreviewTexture.Release();
            Destroy(characterPreviewTexture);
            characterPreviewTexture = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
        }
    }

    public void SetOpen(bool open)
    {
        isOpen = open;

        if (inventoryRoot != null)
        {
            inventoryRoot.SetActive(open);
        }

        if (blurPostProcess != null)
        {
            blurPostProcess.intensity = open ? openBlurIntensity : 0f;
        }

        if (characterPreviewCamera != null)
        {
            characterPreviewCamera.enabled = open;
        }

        if (characterPreviewInstance != null)
        {
            characterPreviewInstance.SetActive(open);
        }

        if (motor != null)
        {
            motor.enabled = !open && (health == null || !health.IsDead);
        }

        if (open)
        {
            SetIdleAnimation();
            SetPreviewIdleAnimation();
        }
    }

    private void EnsureCharacterPreview()
    {
        if (characterPreviewImage == null || characterPreviewCamera == null)
        {
            return;
        }

        if (characterPreviewTexture == null)
        {
            characterPreviewTexture = new RenderTexture(768, 1024, 16, RenderTextureFormat.ARGB32)
            {
                name = "Inventory Character Preview",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            characterPreviewTexture.Create();
        }

        characterPreviewImage.texture = characterPreviewTexture;
        characterPreviewCamera.targetTexture = characterPreviewTexture;
        characterPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        characterPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        characterPreviewCamera.cullingMask = 1 << PreviewLayer;
        characterPreviewCamera.enabled = false;

        if (characterPreviewInstance != null)
        {
            return;
        }

        var sourceVisual = animator != null ? animator.gameObject : gameObject;
        characterPreviewInstance = Instantiate(sourceVisual, GetPreviewSpawnPosition(), Quaternion.identity, characterPreviewStage);
        characterPreviewInstance.name = "Inventory Character Preview";
        characterPreviewInstance.transform.localPosition = Vector3.zero;
        characterPreviewInstance.transform.localRotation = Quaternion.identity;
        characterPreviewInstance.transform.localScale = Vector3.one * 0.92f;
        SetLayerRecursively(characterPreviewInstance, PreviewLayer);
        StripPreviewGameplayComponents(characterPreviewInstance);

        characterPreviewAnimator = characterPreviewInstance.GetComponentInChildren<Animator>();
        if (characterPreviewAnimator != null)
        {
            characterPreviewAnimator.runtimeAnimatorController = animator != null ? animator.runtimeAnimatorController : characterPreviewAnimator.runtimeAnimatorController;
            characterPreviewAnimator.applyRootMotion = false;
            characterPreviewAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            characterPreviewAnimator.enabled = true;
        }

        characterPreviewInstance.SetActive(false);
    }

    private void SetIdleAnimation()
    {
        if (animator == null)
        {
            return;
        }

        SetFloatIfExists("Speed", 0f);
        SetFloatIfExists("MoveSpeed", 0f);
        SetBoolIfExists("IsMoving", false);
        SetBoolIfExists("Moving", false);
        SetBoolIfExists("IsRunning", false);
        SetBoolIfExists("Running", false);
    }

    private void SetPreviewIdleAnimation()
    {
        if (characterPreviewAnimator == null)
        {
            return;
        }

        SetFloatIfExists(characterPreviewAnimator, "Speed", 0f);
        SetFloatIfExists(characterPreviewAnimator, "MoveSpeed", 0f);
        SetBoolIfExists(characterPreviewAnimator, "IsMoving", false);
        SetBoolIfExists(characterPreviewAnimator, "Moving", false);
        SetBoolIfExists(characterPreviewAnimator, "IsRunning", false);
        SetBoolIfExists(characterPreviewAnimator, "Running", false);
        characterPreviewAnimator.Update(0f);
    }

    private Vector3 GetPreviewSpawnPosition()
    {
        return characterPreviewStage != null ? characterPreviewStage.position : new Vector3(5000f, 5000f, 5000f);
    }

    private static void StripPreviewGameplayComponents(GameObject preview)
    {
        foreach (var behaviour in preview.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Destroy(behaviour);
        }

        foreach (var controller in preview.GetComponentsInChildren<CharacterController>(true))
        {
            Destroy(controller);
        }

        foreach (var collider in preview.GetComponentsInChildren<Collider>(true))
        {
            Destroy(collider);
        }

        foreach (var rigidbody in preview.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(rigidbody);
        }

        foreach (var camera in preview.GetComponentsInChildren<Camera>(true))
        {
            Destroy(camera);
        }

        foreach (var listener in preview.GetComponentsInChildren<AudioListener>(true))
        {
            Destroy(listener);
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void SetFloatIfExists(string parameterName, float value)
    {
        SetFloatIfExists(animator, parameterName, value);
    }

    private static void SetFloatIfExists(Animator targetAnimator, string parameterName, float value)
    {
        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == parameterName)
            {
                targetAnimator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private void SetBoolIfExists(string parameterName, bool value)
    {
        SetBoolIfExists(animator, parameterName, value);
    }

    private static void SetBoolIfExists(Animator targetAnimator, string parameterName, bool value)
    {
        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                targetAnimator.SetBool(parameterName, value);
                return;
            }
        }
    }
}
