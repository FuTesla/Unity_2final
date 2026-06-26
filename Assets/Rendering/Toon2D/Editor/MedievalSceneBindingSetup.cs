using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MedievalSceneBindingSetup
{
    private const string ControllerPath = "Assets/Gameplay/AnimationControllers/AC_Medieval_Player.controller";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

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

        var medieval = GameObject.Find("Medieval");
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
        EditorUtility.SetDirty(motor);

        var proceduralAnimator = medieval.GetComponent<MedievalProceduralAnimator>();
        if (proceduralAnimator == null)
        {
            proceduralAnimator = Undo.AddComponent<MedievalProceduralAnimator>(medieval);
        }

        Undo.RecordObject(proceduralAnimator, "Configure Medieval Procedural Animation");
        proceduralAnimator.motor = motor;
        proceduralAnimator.animatorToDisable = animator;
        proceduralAnimator.walkCycleSpeed = 7.5f;
        proceduralAnimator.runCycleSpeed = 11f;
        proceduralAnimator.armDownAngle = 78f;
        proceduralAnimator.armForwardSwing = 0.34f;
        proceduralAnimator.armSideRelax = 0.16f;
        EditorUtility.SetDirty(proceduralAnimator);

        var follow = camera.GetComponent<IsometricCameraFollow>();
        if (follow == null)
        {
            follow = Undo.AddComponent<IsometricCameraFollow>(camera.gameObject);
        }

        Undo.RecordObject(follow, "Bind Camera To Medieval");
        follow.target = medieval.transform;
        follow.offset = new Vector3(-5f, 7.1f, -5f);
        follow.lockRotation = true;
        EditorUtility.SetDirty(follow);

        var binder = camera.GetComponent<TopDownGameplayBinder>();
        if (binder != null)
        {
            Undo.DestroyObjectImmediate(binder);
        }

        camera.orthographic = true;
        camera.orthographicSize = 6f;
        EditorUtility.SetDirty(camera);

        var scene = SceneManager.GetActiveScene();
        if (scene.path == ScenePath)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
