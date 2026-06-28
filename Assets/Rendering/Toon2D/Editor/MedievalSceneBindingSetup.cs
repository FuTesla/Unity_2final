using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MedievalSceneBindingSetup
{
    private const string ControllerPath = "Assets/Gameplay/AnimationControllers/AC_Medieval_Player.controller";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DamageZoneMaterialPath = "Assets/Rendering/Toon2D/Materials/Demo/Damage_Zone.mat";
    private const string MedievalModelPath = "Assets/Art/Female_Character/Individual Characters/FBX/Medieval.fbx";
    private const string InventoryBlurShaderName = "Hidden/Toon2D/InventoryBlurPostProcess";

    static MedievalSceneBindingSetup()
    {
        EditorApplication.delayCall += BindOpenScene;
    }

    [MenuItem("Tools/Toon 2D/Bind Medieval Player In Scene")]
    public static void BindOpenScene()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var medieval = FindPreferredMedievalPlayer();
        if (medieval == null)
        {
            return;
        }

        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var animator = medieval.GetComponentInChildren<Animator>();
        if (animator != null && controller != null)
        {
            Undo.RecordObject(animator, "Assign Medieval Animator Controller");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.enabled = true;
            EditorUtility.SetDirty(animator);
        }

        var characterController = medieval.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = Undo.AddComponent<CharacterController>(medieval);
        }

        Undo.RecordObject(characterController, "Configure Medieval Character Controller");
        characterController.radius = 0.35f;
        characterController.height = 1.8f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.35f;
        characterController.slopeLimit = 45f;
        characterController.skinWidth = 0.04f;
        EditorUtility.SetDirty(characterController);

        var motor = medieval.GetComponent<TopDownCharacterMotor>();
        if (motor == null)
        {
            motor = Undo.AddComponent<TopDownCharacterMotor>(medieval);
        }

        Undo.RecordObject(motor, "Configure Medieval Movement");
        motor.walkSpeed = 3.2f;
        motor.runSpeed = 5.8f;
        motor.cameraTransform = camera.transform;
        motor.animatorController = controller;
        motor.attackTrigger = "Attack";
        motor.interactKey = KeyCode.F;
        motor.interactTrigger = "Interact";
        EditorUtility.SetDirty(motor);

        var health = medieval.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = Undo.AddComponent<PlayerHealth>(medieval);
        }

        Undo.RecordObject(health, "Configure Medieval Health");
        health.maxHealth = 100f;
        health.deathTrigger = "Death";
        health.damageZoneName = "Damage Zone";
        health.damageZoneRadius = 1.3f;
        health.damagePerSecond = 10f;
        EditorUtility.SetDirty(health);

        var proceduralAnimator = medieval.GetComponent<MedievalProceduralAnimator>();
        if (proceduralAnimator != null)
        {
            Undo.DestroyObjectImmediate(proceduralAnimator);
        }

        var follow = camera.GetComponent<IsometricCameraFollow>();
        if (follow == null)
        {
            follow = Undo.AddComponent<IsometricCameraFollow>(camera.gameObject);
        }

        Undo.RecordObject(follow, "Bind Camera To Medieval");
        follow.target = medieval.transform;
        follow.offset = new Vector3(-3.7f, 5.25f, -3.7f);
        follow.lockRotation = true;
        EditorUtility.SetDirty(follow);

        var pixelate = camera.GetComponent<ToonPixelatePostProcess>();
        if (pixelate == null)
        {
            pixelate = Undo.AddComponent<ToonPixelatePostProcess>(camera.gameObject);
        }

        Undo.RecordObject(pixelate, "Configure Toon Pixelate");
        pixelate.pixelateShader = Shader.Find("Hidden/Toon2D/PixelatePostProcess");
        pixelate.referenceHeight = 300;
        pixelate.colorSteps = 40f;
        pixelate.ditherStrength = 0.012f;
        EditorUtility.SetDirty(pixelate);

        var binder = camera.GetComponent<TopDownGameplayBinder>();
        if (binder != null)
        {
            Undo.DestroyObjectImmediate(binder);
        }

        camera.orthographic = true;
        camera.orthographicSize = 4f;
        EditorUtility.SetDirty(camera);

        ConfigureHealthBar(health);
        ConfigureDamageZone(medieval.transform);
        ConfigureInventoryUI(medieval, camera);
        ConfigureSceneEnemies(medieval.transform, camera, controller);
        EnsureEventSystem();

        var scene = SceneManager.GetActiveScene();
        if (scene.path == ScenePath)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static GameObject FindPreferredMedievalPlayer()
    {
        var replacement = GameObject.Find("Medieval (1)");
        var existing = GameObject.Find("Medieval");

        if (replacement != null)
        {
            if (existing != null && existing != replacement)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            Undo.RecordObject(replacement, "Rename Medieval Player");
            replacement.name = "Medieval";
            EditorUtility.SetDirty(replacement);
            return replacement;
        }

        return existing;
    }

    private static void ConfigureHealthBar(PlayerHealth health)
    {
        var canvasObject = GameObject.Find("Player HUD");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("Player HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Player HUD");
        }

        var canvas = canvasObject.GetComponent<Canvas>();
        Undo.RecordObject(canvas, "Configure Player HUD Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        EditorUtility.SetDirty(canvas);

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        Undo.RecordObject(scaler, "Configure Player HUD Scale");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(scaler);

        var barRoot = FindOrCreateRect("Health Bar", canvasObject.transform);
        Undo.RecordObject(barRoot, "Configure Health Bar");
        barRoot.anchorMin = new Vector2(0.5f, 0f);
        barRoot.anchorMax = new Vector2(0.5f, 0f);
        barRoot.pivot = new Vector2(0.5f, 0f);
        barRoot.anchoredPosition = new Vector2(0f, 36f);
        barRoot.sizeDelta = new Vector2(420f, 30f);
        EditorUtility.SetDirty(barRoot);

        var background = FindOrCreateImage("Health Bar Background", barRoot, new Color(0.04f, 0.04f, 0.045f, 0.88f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(background);

        var fill = FindOrCreateImage("Health Bar Fill", barRoot, Color.white);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(4f, 4f);
        fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        EditorUtility.SetDirty(fill);

        var healthBar = canvasObject.GetComponent<PlayerHealthBar>();
        if (healthBar == null)
        {
            healthBar = Undo.AddComponent<PlayerHealthBar>(canvasObject);
        }

        Undo.RecordObject(healthBar, "Configure Player Health Bar");
        healthBar.target = health;
        healthBar.fillImage = fill;
        healthBar.fullColor = Color.white;
        healthBar.warningColor = new Color(1f, 0.52f, 0.08f, 1f);
        healthBar.dangerColor = new Color(0.95f, 0.06f, 0.04f, 1f);
        EditorUtility.SetDirty(healthBar);
    }

    private static RectTransform FindOrCreateRect(string objectName, Transform parent)
    {
        var child = parent.Find(objectName);
        if (child != null)
        {
            return child.GetComponent<RectTransform>();
        }

        var obj = new GameObject(objectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, $"Create {objectName}");
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static Image FindOrCreateImage(string objectName, Transform parent, Color color)
    {
        var child = parent.Find(objectName);
        Image image;
        if (child == null)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(obj, $"Create {objectName}");
            obj.transform.SetParent(parent, false);
            image = obj.GetComponent<Image>();
        }
        else
        {
            image = child.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(child.gameObject);
            }
        }

        Undo.RecordObject(image, $"Configure {objectName}");
        image.color = color;
        return image;
    }

    private static RawImage FindOrCreateRawImage(string objectName, Transform parent, Color color)
    {
        var child = parent.Find(objectName);
        RawImage image;
        if (child == null)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
            Undo.RegisterCreatedObjectUndo(obj, $"Create {objectName}");
            obj.transform.SetParent(parent, false);
            image = obj.GetComponent<RawImage>();
        }
        else
        {
            image = child.GetComponent<RawImage>();
            if (image == null)
            {
                image = Undo.AddComponent<RawImage>(child.gameObject);
            }
        }

        Undo.RecordObject(image, $"Configure {objectName}");
        image.color = color;
        return image;
    }

    private static void ConfigureDamageZone(Transform player)
    {
        var zone = GameObject.Find("Damage Zone");
        if (zone == null)
        {
            zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = "Damage Zone";
            Undo.RegisterCreatedObjectUndo(zone, "Create Damage Zone");
        }

        Undo.RecordObject(zone.transform, "Place Damage Zone");
        zone.transform.position = player.position + new Vector3(2.3f, 0.08f, 0f);
        zone.transform.rotation = Quaternion.identity;
        zone.transform.localScale = new Vector3(2.6f, 0.16f, 2.6f);
        EditorUtility.SetDirty(zone.transform);

        var collider = zone.GetComponent<Collider>();
        if (collider != null)
        {
            Undo.RecordObject(collider, "Configure Damage Zone Collider");
            collider.isTrigger = true;
            EditorUtility.SetDirty(collider);
        }

        var rigidbody = zone.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = Undo.AddComponent<Rigidbody>(zone);
        }

        Undo.RecordObject(rigidbody, "Configure Damage Zone Rigidbody");
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        EditorUtility.SetDirty(rigidbody);

        var damageZone = zone.GetComponent<DamageZone>();
        if (damageZone == null)
        {
            damageZone = Undo.AddComponent<DamageZone>(zone);
        }

        Undo.RecordObject(damageZone, "Configure Damage Zone Damage");
        damageZone.damagePerSecond = 10f;
        damageZone.damageRadius = 1.3f;
        EditorUtility.SetDirty(damageZone);

        var renderer = zone.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Undo.RecordObject(renderer, "Assign Damage Zone Material");
            renderer.sharedMaterial = GetDamageZoneMaterial();
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ConfigureInventoryUI(GameObject player, Camera camera)
    {
        var blur = camera.GetComponent<InventoryBlurPostProcess>();
        if (blur == null)
        {
            blur = Undo.AddComponent<InventoryBlurPostProcess>(camera.gameObject);
        }

        Undo.RecordObject(blur, "Configure Inventory Blur");
        blur.blurShader = Shader.Find(InventoryBlurShaderName);
        blur.intensity = 0f;
        blur.iterations = 2;
        blur.spread = 2.4f;
        EditorUtility.SetDirty(blur);

        var canvasObject = GameObject.Find("Inventory HUD");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("Inventory HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Inventory HUD");
        }

        var canvas = canvasObject.GetComponent<Canvas>();
        Undo.RecordObject(canvas, "Configure Inventory HUD Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        EditorUtility.SetDirty(canvas);

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        Undo.RecordObject(scaler, "Configure Inventory HUD Scale");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(scaler);

        var root = FindOrCreateRect("Inventory Root", canvasObject.transform);
        Undo.RecordObject(root.gameObject, "Configure Inventory Root");
        root.gameObject.SetActive(true);
        Undo.RecordObject(root, "Configure Inventory Root Rect");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(root);

        var veil = FindOrCreateImage("Blur Veil", root, new Color(0f, 0f, 0f, 0.44f));
        veil.raycastTarget = true;
        veil.rectTransform.anchorMin = Vector2.zero;
        veil.rectTransform.anchorMax = Vector2.one;
        veil.rectTransform.offsetMin = Vector2.zero;
        veil.rectTransform.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(veil);

        ConfigureInventoryLeftColumns(root);
        var previewImage = ConfigureInventoryCharacterFrame(root);
        var previewCamera = ConfigureInventoryPreviewCamera(out var previewStage);

        var inventory = player.GetComponent<PlayerInventoryUI>();
        if (inventory == null)
        {
            inventory = Undo.AddComponent<PlayerInventoryUI>(player);
        }

        Undo.RecordObject(inventory, "Configure Player Inventory UI");
        inventory.toggleKey = KeyCode.Tab;
        inventory.inventoryRoot = root.gameObject;
        inventory.motor = player.GetComponent<TopDownCharacterMotor>();
        inventory.animator = player.GetComponentInChildren<Animator>();
        inventory.blurPostProcess = blur;
        inventory.characterPreviewImage = previewImage;
        inventory.characterPreviewCamera = previewCamera;
        inventory.characterPreviewStage = previewStage;
        inventory.openBlurIntensity = 0.82f;
        EditorUtility.SetDirty(inventory);

        var pauseMenu = player.GetComponent<PauseMenuController>();
        if (pauseMenu == null)
        {
            pauseMenu = Undo.AddComponent<PauseMenuController>(player);
        }

        Undo.RecordObject(pauseMenu, "Configure Pause Menu");
        pauseMenu.toggleKey = KeyCode.Escape;
        pauseMenu.motor = player.GetComponent<TopDownCharacterMotor>();
        pauseMenu.inventoryUI = inventory;
        pauseMenu.blurPostProcess = blur;
        pauseMenu.pauseBlurIntensity = 0f;
        EditorUtility.SetDirty(pauseMenu);

        root.gameObject.SetActive(false);
    }

    private static void ConfigureInventoryLeftColumns(RectTransform root)
    {
        var panel = FindOrCreateRect("Inventory Left Columns", root);
        Undo.RecordObject(panel, "Configure Inventory Left Columns");
        panel.anchorMin = new Vector2(0f, 0.5f);
        panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(72f, 0f);
        panel.sizeDelta = new Vector2(590f, 820f);
        EditorUtility.SetDirty(panel);

        var nearby = FindOrCreateRect("Nearby Column", panel);
        ConfigureInventoryColumn(nearby, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(280f, 820f), "附近", "Unknown#019 的物品");
        AddInventoryRows(nearby, new[]
        {
            new InventoryRowData("7.62毫米子弹", "14", new Color(0.67f, 0.46f, 0.22f, 1f)),
            new InventoryRowData("7.62毫米子弹", "40", new Color(0.67f, 0.46f, 0.22f, 1f)),
            new InventoryRowData("Mk47 Mutant", "", new Color(0.22f, 0.32f, 0.42f, 1f)),
            new InventoryRowData("蓝色晶片", "", new Color(0.08f, 0.45f, 0.92f, 1f)),
            new InventoryRowData("兔兔缎带", "", new Color(0.95f, 0.48f, 0.25f, 1f)),
            new InventoryRowData("滑雪镜", "", new Color(0.34f, 0.42f, 0.92f, 1f)),
            new InventoryRowData("小黑盒手套", "1", new Color(0.35f, 0.35f, 0.45f, 1f))
        });

        var backpack = FindOrCreateRect("Backpack Column", panel);
        ConfigureInventoryColumn(backpack, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(260f, 820f), "背包", "类型  最近");
        AddInventoryRows(backpack, new[]
        {
            new InventoryRowData("能量饮料", "1", new Color(0.94f, 0.36f, 0.52f, 1f)),
            new InventoryRowData("7.62毫米子弹", "60", new Color(0.67f, 0.46f, 0.22f, 1f)),
            new InventoryRowData("急救包", "2", new Color(0.95f, 0.95f, 0.88f, 1f)),
            new InventoryRowData("破片手雷", "1", new Color(0.18f, 0.45f, 0.28f, 1f)),
            new InventoryRowData("止痛药", "3", new Color(0.86f, 0.82f, 0.44f, 1f))
        });

        var divider = FindOrCreateImage("Inventory Divider", panel, new Color(1f, 1f, 1f, 0.22f));
        divider.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        divider.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        divider.rectTransform.anchoredPosition = new Vector2(306f, -22f);
        divider.rectTransform.sizeDelta = new Vector2(2f, 710f);
        EditorUtility.SetDirty(divider);
    }

    private static RawImage ConfigureInventoryCharacterFrame(RectTransform root)
    {
        var frame = FindOrCreateRect("Character Preview Frame", root);
        Undo.RecordObject(frame, "Configure Character Preview Frame");
        frame.anchorMin = new Vector2(0.5f, 0.5f);
        frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = new Vector2(64f, -8f);
        frame.sizeDelta = new Vector2(560f, 860f);
        EditorUtility.SetDirty(frame);

        var previewImage = FindOrCreateRawImage("Character Preview Render", frame, Color.white);
        previewImage.rectTransform.anchorMin = Vector2.zero;
        previewImage.rectTransform.anchorMax = Vector2.one;
        previewImage.rectTransform.offsetMin = new Vector2(0f, 8f);
        previewImage.rectTransform.offsetMax = new Vector2(0f, -8f);
        previewImage.raycastTarget = false;
        EditorUtility.SetDirty(previewImage);

        var topLine = FindOrCreateImage("Character Preview Top Line", frame, new Color(1f, 1f, 1f, 0.16f));
        topLine.rectTransform.anchorMin = new Vector2(0.15f, 1f);
        topLine.rectTransform.anchorMax = new Vector2(0.85f, 1f);
        topLine.rectTransform.pivot = new Vector2(0.5f, 1f);
        topLine.rectTransform.anchoredPosition = Vector2.zero;
        topLine.rectTransform.sizeDelta = new Vector2(0f, 2f);
        EditorUtility.SetDirty(topLine);

        var bottomLine = FindOrCreateImage("Character Preview Bottom Line", frame, new Color(1f, 1f, 1f, 0.16f));
        bottomLine.rectTransform.anchorMin = new Vector2(0.15f, 0f);
        bottomLine.rectTransform.anchorMax = new Vector2(0.85f, 0f);
        bottomLine.rectTransform.pivot = new Vector2(0.5f, 0f);
        bottomLine.rectTransform.anchoredPosition = Vector2.zero;
        bottomLine.rectTransform.sizeDelta = new Vector2(0f, 2f);
        EditorUtility.SetDirty(bottomLine);

        return previewImage;
    }

    private static Camera ConfigureInventoryPreviewCamera(out Transform previewStage)
    {
        var stage = GameObject.Find("Inventory Preview Stage");
        if (stage == null)
        {
            stage = new GameObject("Inventory Preview Stage");
            Undo.RegisterCreatedObjectUndo(stage, "Create Inventory Preview Stage");
        }

        Undo.RecordObject(stage.transform, "Configure Inventory Preview Stage");
        stage.transform.position = new Vector3(5000f, 5000f, 5000f);
        stage.transform.rotation = Quaternion.identity;
        stage.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(stage.transform);
        previewStage = stage.transform;

        var cameraObject = GameObject.Find("Inventory Preview Camera");
        if (cameraObject == null)
        {
            cameraObject = new GameObject("Inventory Preview Camera", typeof(Camera));
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Inventory Preview Camera");
        }

        Undo.RecordObject(cameraObject.transform, "Configure Inventory Preview Camera Transform");
        cameraObject.transform.position = stage.transform.position + new Vector3(0f, 1.08f, 4.8f);
        cameraObject.transform.rotation = Quaternion.Euler(4f, 180f, 0f);
        EditorUtility.SetDirty(cameraObject.transform);

        var previewCamera = cameraObject.GetComponent<Camera>();
        Undo.RecordObject(previewCamera, "Configure Inventory Preview Camera");
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << 31;
        previewCamera.nearClipPlane = 0.03f;
        previewCamera.farClipPlane = 10f;
        previewCamera.fieldOfView = 29f;
        previewCamera.depth = 50f;
        previewCamera.enabled = false;
        EditorUtility.SetDirty(previewCamera);

        return previewCamera;
    }

    private static void ConfigureSceneEnemies(Transform player, Camera camera, RuntimeAnimatorController controller)
    {
        var enemies = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.scene.IsValid() || !IsEnemySuit(enemy))
            {
                continue;
            }

            ConfigureEnemy(enemy, player, camera, controller);
        }
    }

    private static bool IsEnemySuit(GameObject candidate)
    {
        var normalized = candidate.name.Replace(" ", string.Empty).Replace("_", string.Empty);
        if (normalized.IndexOf("enemysuit", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private static void ConfigureEnemy(GameObject enemy, Transform player, Camera camera, RuntimeAnimatorController controller)
    {
        var animator = enemy.GetComponentInChildren<Animator>();
        if (animator != null && controller != null)
        {
            Undo.RecordObject(animator, "Assign Enemy Animator Controller");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.enabled = true;
            EditorUtility.SetDirty(animator);
        }

        var characterController = enemy.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = Undo.AddComponent<CharacterController>(enemy);
        }

        Undo.RecordObject(characterController, "Configure Enemy Character Controller");
        characterController.radius = 0.35f;
        characterController.height = 1.8f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.35f;
        characterController.slopeLimit = 45f;
        characterController.skinWidth = 0.04f;
        EditorUtility.SetDirty(characterController);

        var health = enemy.GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = Undo.AddComponent<EnemyHealth>(enemy);
        }

        Undo.RecordObject(health, "Configure Enemy Health");
        health.maxHealth = 100f;
        health.deathTrigger = "Death";
        health.deathStateName = "Death";
        health.damageZoneName = "Damage Zone";
        health.damagePerSecond = 10f;
        EditorUtility.SetDirty(health);

        var ai = enemy.GetComponent<EnemyAIController>();
        if (ai == null)
        {
            ai = Undo.AddComponent<EnemyAIController>(enemy);
        }

        Undo.RecordObject(ai, "Configure Enemy AI");
        ai.player = player;
        ai.animatorController = controller;
        ai.detectionDistance = 25f;
        ai.attackDistance = 1.55f;
        ai.moveSpeed = 3.4f;
        ai.attackCooldown = 1.35f;
        ai.attackDamage = 40f;
        ai.chaseAnimationSpeed = 1f;
        ai.attackTrigger = "Attack";
        ai.attackStateName = "Sword Slash";
        ai.locomotionStateName = "Locomotion";
        ai.targetHandName = "Hand.R";
        ai.swordObjectName = "Enemy Sword";
        EditorUtility.SetDirty(ai);

        ConfigureEnemySword(enemy, player);
        ConfigureEnemyHealthBar(enemy.transform, health, camera);
    }

    private static void ConfigureEnemySword(GameObject enemy, Transform player)
    {
        var sourceSword = FindDeepChild(player, "Sword");
        if (sourceSword == null)
        {
            var medievalAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MedievalModelPath);
            sourceSword = medievalAsset != null ? FindDeepChild(medievalAsset.transform, "Sword") : null;
        }

        if (sourceSword == null)
        {
            return;
        }

        var binder = enemy.GetComponent<EnemySwordBinder>();
        if (binder == null)
        {
            binder = Undo.AddComponent<EnemySwordBinder>(enemy);
        }

        Undo.RecordObject(binder, "Configure Enemy Sword Binder");
        binder.sourceSword = sourceSword;
        binder.targetHandName = "Hand.R";
        binder.boundSwordName = "Enemy Sword";
        EditorUtility.SetDirty(binder);

        var targetHand = EnemySwordBinder.FindLikelyRightHand(enemy.transform, binder.targetHandName);
        if (targetHand == null)
        {
            return;
        }

        var existing = targetHand.Find(binder.boundSwordName);
        GameObject swordObject;
        if (existing != null)
        {
            swordObject = existing.gameObject;
            Undo.RecordObject(swordObject.transform, "Update Enemy Sword");
        }
        else
        {
            swordObject = UnityEngine.Object.Instantiate(sourceSword.gameObject, targetHand);
            swordObject.name = binder.boundSwordName;
            Undo.RegisterCreatedObjectUndo(swordObject, "Create Enemy Sword");
        }

        swordObject.transform.localPosition = sourceSword.localPosition;
        swordObject.transform.localRotation = sourceSword.localRotation;
        swordObject.transform.localScale = sourceSword.localScale;
        EditorUtility.SetDirty(swordObject.transform);
    }

    private static void ConfigureEnemyHealthBar(Transform enemy, EnemyHealth health, Camera camera)
    {
        var canvasTransform = enemy.Find("Enemy Health Bar");
        GameObject canvasObject;
        if (canvasTransform == null)
        {
            canvasObject = new GameObject("Enemy Health Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(EnemyHealthBar));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Enemy Health Bar");
            canvasObject.transform.SetParent(enemy, false);
        }
        else
        {
            canvasObject = canvasTransform.gameObject;
            if (canvasObject.GetComponent<Canvas>() == null)
            {
                Undo.AddComponent<Canvas>(canvasObject);
            }

            if (canvasObject.GetComponent<EnemyHealthBar>() == null)
            {
                Undo.AddComponent<EnemyHealthBar>(canvasObject);
            }
        }

        var rect = canvasObject.GetComponent<RectTransform>();
        Undo.RecordObject(rect, "Configure Enemy Health Bar Rect");
        rect.localPosition = new Vector3(0f, 2.25f, 0f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.01f;
        rect.sizeDelta = new Vector2(150f, 18f);
        EditorUtility.SetDirty(rect);

        var canvas = canvasObject.GetComponent<Canvas>();
        Undo.RecordObject(canvas, "Configure Enemy Health Bar Canvas");
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90;
        EditorUtility.SetDirty(canvas);

        var background = FindOrCreateImage("Enemy Health Background", rect, new Color(0.04f, 0.04f, 0.045f, 0.9f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(background);

        var fill = FindOrCreateImage("Enemy Health Fill", rect, Color.white);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
        EditorUtility.SetDirty(fill);

        var healthBar = canvasObject.GetComponent<EnemyHealthBar>();
        Undo.RecordObject(healthBar, "Configure Enemy Health Bar");
        healthBar.target = health;
        healthBar.fillImage = fill;
        healthBar.targetCamera = camera;
        healthBar.fullColor = new Color(1f, 0.52f, 0.08f, 1f);
        healthBar.dangerColor = new Color(0.95f, 0.06f, 0.04f, 1f);
        healthBar.dangerThreshold = 0.2f;
        EditorUtility.SetDirty(healthBar);
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            var match = FindDeepChild(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void ConfigureInventoryColumn(RectTransform column, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, string title, string subtitle)
    {
        Undo.RecordObject(column, $"Configure {column.name}");
        column.anchorMin = anchorMin;
        column.anchorMax = anchorMax;
        column.pivot = new Vector2(0f, 0.5f);
        column.anchoredPosition = position;
        column.sizeDelta = size;
        EditorUtility.SetDirty(column);

        var titleText = FindOrCreateText($"{column.name} Title", column, title, 32, FontStyle.Bold, new Color(0.9f, 0.9f, 0.92f, 1f));
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        titleText.rectTransform.sizeDelta = new Vector2(0f, 44f);
        EditorUtility.SetDirty(titleText);

        var subtitleText = FindOrCreateText($"{column.name} Subtitle", column, subtitle, 15, FontStyle.Bold, new Color(0.8f, 0.8f, 0.82f, 1f));
        subtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        subtitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        subtitleText.rectTransform.pivot = new Vector2(0f, 1f);
        subtitleText.rectTransform.anchoredPosition = new Vector2(0f, -50f);
        subtitleText.rectTransform.sizeDelta = new Vector2(0f, 24f);
        EditorUtility.SetDirty(subtitleText);
    }

    private static void AddInventoryRows(RectTransform column, InventoryRowData[] rows)
    {
        for (var i = 0; i < rows.Length; i++)
        {
            var row = FindOrCreateImage($"{column.name} Item {i + 1:00}", column, new Color(0.42f, 0.42f, 0.42f, 0.62f));
            row.rectTransform.anchorMin = new Vector2(0f, 1f);
            row.rectTransform.anchorMax = new Vector2(1f, 1f);
            row.rectTransform.pivot = new Vector2(0f, 1f);
            row.rectTransform.anchoredPosition = new Vector2(0f, -86f - i * 62f);
            row.rectTransform.sizeDelta = new Vector2(0f, 58f);
            EditorUtility.SetDirty(row);

            var icon = FindOrCreateImage($"{row.name} Icon", row.rectTransform, rows[i].iconColor);
            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(44f, 44f);
            EditorUtility.SetDirty(icon);

            var nameText = FindOrCreateText($"{row.name} Name", row.rectTransform, rows[i].itemName, 17, FontStyle.Normal, new Color(0.92f, 0.92f, 0.94f, 1f));
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.offsetMin = new Vector2(66f, 0f);
            nameText.rectTransform.offsetMax = new Vector2(-42f, 0f);
            EditorUtility.SetDirty(nameText);

            var countText = FindOrCreateText($"{row.name} Count", row.rectTransform, rows[i].count, 17, FontStyle.Bold, new Color(0.9f, 0.9f, 0.92f, 1f));
            countText.alignment = TextAnchor.MiddleRight;
            countText.rectTransform.anchorMin = new Vector2(1f, 0f);
            countText.rectTransform.anchorMax = new Vector2(1f, 1f);
            countText.rectTransform.pivot = new Vector2(1f, 0.5f);
            countText.rectTransform.anchoredPosition = new Vector2(-10f, 0f);
            countText.rectTransform.sizeDelta = new Vector2(36f, 0f);
            EditorUtility.SetDirty(countText);
        }
    }

    private static Text FindOrCreateText(string objectName, Transform parent, string value, int fontSize, FontStyle fontStyle, Color color)
    {
        var child = parent.Find(objectName);
        Text text;
        if (child == null)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            Undo.RegisterCreatedObjectUndo(obj, $"Create {objectName}");
            obj.transform.SetParent(parent, false);
            text = obj.GetComponent<Text>();
        }
        else
        {
            text = child.GetComponent<Text>();
            if (text == null)
            {
                text = Undo.AddComponent<Text>(child.gameObject);
            }
        }

        Undo.RecordObject(text, $"Configure {objectName}");
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private readonly struct InventoryRowData
    {
        public readonly string itemName;
        public readonly string count;
        public readonly Color iconColor;

        public InventoryRowData(string itemName, string count, Color iconColor)
        {
            this.itemName = itemName;
            this.count = count;
            this.iconColor = iconColor;
        }
    }

    private static Material GetDamageZoneMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(DamageZoneMaterialPath);
        if (material != null)
        {
            return material;
        }

        var shader = Shader.Find("Standard");
        material = new Material(shader)
        {
            name = "Damage_Zone"
        };

        material.color = new Color(1f, 0.08f, 0.04f, 0.38f);
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;

        AssetDatabase.CreateAsset(material, DamageZoneMaterialPath);
        return material;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }
}
