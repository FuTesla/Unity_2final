using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NpcDialogueController : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.F;
    public float interactDistance = 2.4f;
    public Transform player;
    public TopDownCharacterMotor playerMotor;
    public IsometricCameraFollow cameraFollow;
    public Camera mainCamera;
    public Transform cameraFocus;
    public Vector3 dialogueCameraOffset = new Vector3(-2.1f, 3.1f, -2.1f);
    public float dialogueOrthographicSize = 2.6f;
    public string[] dialogueLines =
    {
        "\u4f60\u597d\uff0c\u5192\u9669\u8005\u3002",
        "\u8fd9\u7247\u533a\u57df\u5f88\u5371\u9669\uff0c\u8bf7\u4fdd\u6301\u8b66\u60d5\u3002",
        "\u795d\u4f60\u4e00\u8def\u987a\u5229\u3002"
    };

    private const string DialogueHudName = "Dialogue HUD";
    private const string DialogueRootName = "Dialogue Root";
    private static GameObject sharedDialogueRoot;
    private static Text sharedDialogueText;

    private Transform previousCameraTarget;
    private Vector3 previousCameraOffset;
    private float previousCameraSize;
    private bool previousMotorEnabled;
    private int lineIndex;
    private bool isTalking;

    private void Awake()
    {
        ResolveReferences();
        EnsureDialogueUi();
        SetDialogueVisible(false);
    }

    private void Update()
    {
        if (isTalking)
        {
            if (Input.GetMouseButtonDown(0))
            {
                AdvanceDialogue();
            }

            return;
        }

        if (Input.GetKeyDown(interactKey) && IsPlayerInRange())
        {
            StartDialogue();
        }
    }

    private void OnDisable()
    {
        if (isTalking)
        {
            EndDialogue();
        }
    }

    private void StartDialogue()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            return;
        }

        ResolveReferences();
        isTalking = true;
        lineIndex = 0;
        previousMotorEnabled = playerMotor != null && playerMotor.enabled;

        if (playerMotor != null)
        {
            playerMotor.enabled = false;
        }

        if (cameraFollow != null)
        {
            previousCameraTarget = cameraFollow.target;
            previousCameraOffset = cameraFollow.offset;
            cameraFollow.target = cameraFocus != null ? cameraFocus : transform;
            cameraFollow.offset = dialogueCameraOffset;
        }

        if (mainCamera != null)
        {
            previousCameraSize = mainCamera.orthographicSize;
            mainCamera.orthographicSize = dialogueOrthographicSize;
        }

        SetDialogueVisible(true);
        ShowCurrentLine();
    }

    private void AdvanceDialogue()
    {
        lineIndex++;
        if (lineIndex >= dialogueLines.Length)
        {
            EndDialogue();
            return;
        }

        ShowCurrentLine();
    }

    private void EndDialogue()
    {
        isTalking = false;
        SetDialogueVisible(false);

        if (cameraFollow != null)
        {
            cameraFollow.target = previousCameraTarget != null ? previousCameraTarget : player;
            cameraFollow.offset = previousCameraOffset;
        }

        if (mainCamera != null && previousCameraSize > 0f)
        {
            mainCamera.orthographicSize = previousCameraSize;
        }

        if (playerMotor != null)
        {
            playerMotor.enabled = previousMotorEnabled;
        }
    }

    private void ShowCurrentLine()
    {
        if (sharedDialogueText != null)
        {
            sharedDialogueText.text = dialogueLines[lineIndex];
        }
    }

    private bool IsPlayerInRange()
    {
        ResolveReferences();
        if (player == null)
        {
            return false;
        }

        var offset = player.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= interactDistance * interactDistance;
    }

    private void ResolveReferences()
    {
        if (playerMotor == null)
        {
            playerMotor = FindObjectOfType<TopDownCharacterMotor>();
        }

        if (player == null && playerMotor != null)
        {
            player = playerMotor.transform;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (cameraFollow == null && mainCamera != null)
        {
            cameraFollow = mainCamera.GetComponent<IsometricCameraFollow>();
        }
    }

    private static void EnsureDialogueUi()
    {
        if (sharedDialogueRoot != null && sharedDialogueText != null)
        {
            return;
        }

        EnsureEventSystem();

        var existingRoot = GameObject.Find(DialogueRootName);
        if (existingRoot != null)
        {
            sharedDialogueRoot = existingRoot;
            sharedDialogueText = existingRoot.GetComponentInChildren<Text>(true);
            return;
        }

        var canvasObject = new GameObject(DialogueHudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateRect(DialogueRootName, canvasObject.transform);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        sharedDialogueRoot = root.gameObject;

        var panel = CreateImage("Dialogue Panel", root, new Color(0.035f, 0.04f, 0.05f, 0.9f));
        panel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, 36f);
        panel.rectTransform.sizeDelta = new Vector2(1320f, 190f);

        sharedDialogueText = CreateText("NPC Dialogue Text", panel.rectTransform, string.Empty, 30, FontStyle.Normal, Color.white);
        sharedDialogueText.rectTransform.anchorMin = Vector2.zero;
        sharedDialogueText.rectTransform.anchorMax = Vector2.one;
        sharedDialogueText.rectTransform.offsetMin = new Vector2(44f, 28f);
        sharedDialogueText.rectTransform.offsetMax = new Vector2(-44f, -28f);
        sharedDialogueText.alignment = TextAnchor.MiddleLeft;

        sharedDialogueRoot.SetActive(false);
    }

    private static void SetDialogueVisible(bool visible)
    {
        if (sharedDialogueRoot != null)
        {
            sharedDialogueRoot.SetActive(visible);
        }
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        var obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string objectName, Transform parent, string value, int fontSize, FontStyle style, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
