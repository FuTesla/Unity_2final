using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class MedievalAnimationSetup
{
    private const string ControllerPath = "Assets/Gameplay/AnimationControllers/AC_Medieval_Player.controller";
    private const string ClipFolder = "Assets/Gameplay/AnimationClips";
    private const string CharacterPath = "Assets/Art/Female_Character/Humanoid Rigs/Individual Characters/FBX/Medieval.fbx";
    private const string AnimationsPath = "Assets/Art/Female_Character/Individual Characters/FBX/Medieval.fbx";

    static MedievalAnimationSetup()
    {
        EditorApplication.delayCall += EnsureController;
    }

    [InitializeOnLoadMethod]
    private static void QueueEnsureController()
    {
        EditorApplication.delayCall += EnsureController;
    }

    public static void EnsureController()
    {
        EnsureFolder("Assets/Gameplay/AnimationControllers");
        ConfigureGenericImporters();

        var existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existingController != null && UsesGeneratedLocomotionClips(existingController))
        {
            MedievalSceneBindingSetup.BindOpenScene();
            return;
        }

        if (existingController != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        CreateController();
    }

    [MenuItem("Tools/Toon 2D/Rebuild Medieval Animator Controller")]
    public static void RebuildController()
    {
        EnsureFolder("Assets/Gameplay/AnimationControllers");
        ConfigureGenericImporters();

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        CreateController();
    }

    private static void CreateController()
    {
        AnimationClip idle;
        AnimationClip walk;
        AnimationClip run;

        var proceduralClips = CreateProceduralLocomotionClips();
        idle = proceduralClips[0];
        walk = proceduralClips[1];
        run = proceduralClips[2];

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);

        var blendTree = new BlendTree
        {
            name = "BT_Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(blendTree, controller);
        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walk, 0.5f);
        blendTree.AddChild(run, 1f);

        var stateMachine = controller.layers[0].stateMachine;
        var locomotion = stateMachine.AddState("Locomotion");
        locomotion.motion = blendTree;
        locomotion.writeDefaultValues = true;
        stateMachine.defaultState = locomotion;

        var layers = controller.layers;
        layers[0].defaultWeight = 1f;
        controller.layers = layers;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        MedievalSceneBindingSetup.BindOpenScene();
        Debug.Log($"Created {ControllerPath} using stylized procedural Idle/Walk/Run clips for Medieval.");
    }

    private static AnimationClip[] CreateProceduralLocomotionClips()
    {
        EnsureFolder(ClipFolder);

        var medieval = GameObject.Find("Medieval");
        var animator = medieval != null ? medieval.GetComponentInChildren<Animator>() : null;
        var root = animator != null ? animator.transform : medieval != null ? medieval.transform : null;
        var paths = BuildBonePaths(root);

        var idle = CreateOrReplaceClip($"{ClipFolder}/Medieval_Idle_Procedural.anim", "Medieval_Idle_Procedural", 1.6f);
        AddSineCurve(idle, paths, "Chest", "localEulerAnglesRaw.z", 0f, 2f, 1.6f);
        AddSineCurve(idle, paths, "UpperArm.L", "localEulerAnglesRaw.x", 0f, 3f, 1.6f);
        AddSineCurve(idle, paths, "UpperArm.R", "localEulerAnglesRaw.x", 0f, -3f, 1.6f);
        AddCycleCurve(idle, paths, "UpperArm.L", "localEulerAnglesRaw.z", -78f, -78f, -78f, 1.6f);
        AddCycleCurve(idle, paths, "UpperArm.R", "localEulerAnglesRaw.z", 78f, 78f, 78f, 1.6f);
        AddCycleCurve(idle, paths, "LowerArm.L", "localEulerAnglesRaw.z", -8f, -8f, -8f, 1.6f);
        AddCycleCurve(idle, paths, "LowerArm.R", "localEulerAnglesRaw.z", 8f, 8f, 8f, 1.6f);
        SaveClip(idle);

        var walk = CreateOrReplaceClip($"{ClipFolder}/Medieval_Walk_Procedural.anim", "Medieval_Walk_Procedural", 0.8f);
        AddStrideCurves(walk, paths, 0.8f, 34f, 42f, 32f, 78f);
        SaveClip(walk);

        var run = CreateOrReplaceClip($"{ClipFolder}/Medieval_Run_Procedural.anim", "Medieval_Run_Procedural", 0.55f);
        AddStrideCurves(run, paths, 0.55f, 48f, 58f, 44f, 82f);
        SaveClip(run);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return new[] { idle, walk, run };
    }

    private static Dictionary<string, string> BuildBonePaths(Transform root)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root == null)
        {
            AddFallbackPaths(paths);
            return paths;
        }

        foreach (var boneName in new[]
        {
            "Chest", "UpperArm.L", "UpperArm.R", "LowerArm.L", "LowerArm.R",
            "UpperLeg.L", "UpperLeg.R", "LowerLeg.L", "LowerLeg.R"
        })
        {
            var bone = FindDescendant(root, boneName);
            if (bone != null)
            {
                paths[boneName] = AnimationUtility.CalculateTransformPath(bone, root);
            }
        }

        AddFallbackPaths(paths);
        return paths;
    }

    private static void AddFallbackPaths(Dictionary<string, string> paths)
    {
        AddFallbackPath(paths, "Chest", "CharacterArmature/Hips/Abdomen/Torso/Chest");
        AddFallbackPath(paths, "UpperArm.L", "CharacterArmature/Hips/Abdomen/Torso/Chest/Shoulder.L/UpperArm.L");
        AddFallbackPath(paths, "UpperArm.R", "CharacterArmature/Hips/Abdomen/Torso/Chest/Shoulder.R/UpperArm.R");
        AddFallbackPath(paths, "LowerArm.L", "CharacterArmature/Hips/Abdomen/Torso/Chest/Shoulder.L/UpperArm.L/LowerArm.L");
        AddFallbackPath(paths, "LowerArm.R", "CharacterArmature/Hips/Abdomen/Torso/Chest/Shoulder.R/UpperArm.R/LowerArm.R");
        AddFallbackPath(paths, "UpperLeg.L", "CharacterArmature/Hips/UpperLeg.L");
        AddFallbackPath(paths, "UpperLeg.R", "CharacterArmature/Hips/UpperLeg.R");
        AddFallbackPath(paths, "LowerLeg.L", "CharacterArmature/Hips/UpperLeg.L/LowerLeg.L");
        AddFallbackPath(paths, "LowerLeg.R", "CharacterArmature/Hips/UpperLeg.R/LowerLeg.R");
    }

    private static void AddFallbackPath(Dictionary<string, string> paths, string boneName, string path)
    {
        if (!paths.ContainsKey(boneName))
        {
            paths.Add(boneName, path);
        }
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            var match = FindDescendant(child, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static AnimationClip CreateOrReplaceClip(string path, string clipName, float length)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        var clip = new AnimationClip
        {
            name = clipName,
            frameRate = 30f,
            wrapMode = WrapMode.Loop
        };

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void AddStrideCurves(AnimationClip clip, Dictionary<string, string> paths, float length, float legSwing, float kneeBend, float armSwing, float armDown)
    {
        AddCycleCurve(clip, paths, "UpperLeg.L", "localEulerAnglesRaw.x", legSwing, -legSwing, legSwing, length);
        AddCycleCurve(clip, paths, "UpperLeg.R", "localEulerAnglesRaw.x", -legSwing, legSwing, -legSwing, length);
        AddCycleCurve(clip, paths, "LowerLeg.L", "localEulerAnglesRaw.x", 0f, kneeBend, 0f, length);
        AddCycleCurve(clip, paths, "LowerLeg.R", "localEulerAnglesRaw.x", kneeBend, 0f, kneeBend, length);
        AddCycleCurve(clip, paths, "UpperArm.L", "localEulerAnglesRaw.x", -armSwing, armSwing, -armSwing, length);
        AddCycleCurve(clip, paths, "UpperArm.R", "localEulerAnglesRaw.x", armSwing, -armSwing, armSwing, length);
        AddCycleCurve(clip, paths, "UpperArm.L", "localEulerAnglesRaw.z", -armDown, -armDown, -armDown, length);
        AddCycleCurve(clip, paths, "UpperArm.R", "localEulerAnglesRaw.z", armDown, armDown, armDown, length);
        AddCycleCurve(clip, paths, "LowerArm.L", "localEulerAnglesRaw.x", -8f, 18f, -8f, length);
        AddCycleCurve(clip, paths, "LowerArm.R", "localEulerAnglesRaw.x", 18f, -8f, 18f, length);
        AddCycleCurve(clip, paths, "LowerArm.L", "localEulerAnglesRaw.z", -12f, -18f, -12f, length);
        AddCycleCurve(clip, paths, "LowerArm.R", "localEulerAnglesRaw.z", 18f, 12f, 18f, length);
    }

    private static void AddCycleCurve(AnimationClip clip, Dictionary<string, string> paths, string boneName, string propertyName, float a, float b, float c, float length)
    {
        if (!paths.TryGetValue(boneName, out var path))
        {
            return;
        }

        var curve = new AnimationCurve(
            new Keyframe(0f, a),
            new Keyframe(length * 0.5f, b),
            new Keyframe(length, c));
        SetCurveLoopTangents(curve);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName), curve);
    }

    private static void AddSineCurve(AnimationClip clip, Dictionary<string, string> paths, string boneName, string propertyName, float center, float amplitude, float length)
    {
        AddCycleCurve(clip, paths, boneName, propertyName, center - amplitude, center + amplitude, center - amplitude, length);
    }

    private static void SaveClip(AnimationClip clip)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void SetCurveLoopTangents(AnimationCurve curve)
    {
        for (var i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        }
    }

    private static void ConfigureGenericImporters()
    {
        var characterImporter = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
        if (characterImporter == null)
        {
            Debug.LogWarning($"Could not find Medieval character importer at {CharacterPath}.");
            return;
        }

        var characterChanged = false;
        if (characterImporter.animationType != ModelImporterAnimationType.Generic)
        {
            characterImporter.animationType = ModelImporterAnimationType.Generic;
            characterChanged = true;
        }

        if (characterImporter.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            characterImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            characterChanged = true;
        }

        if (characterChanged)
        {
            characterImporter.SaveAndReimport();
        }

        var animationsImporter = AssetImporter.GetAtPath(AnimationsPath) as ModelImporter;
        if (animationsImporter == null)
        {
            Debug.LogWarning($"Could not find animation importer at {AnimationsPath}.");
            return;
        }

        var animationsChanged = false;
        if (animationsImporter.animationType != ModelImporterAnimationType.Generic)
        {
            animationsImporter.animationType = ModelImporterAnimationType.Generic;
            animationsChanged = true;
        }

        if (animationsImporter.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            animationsImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            animationsChanged = true;
        }

        var clipAnimations = animationsImporter.defaultClipAnimations;
        for (var i = 0; i < clipAnimations.Length; i++)
        {
            var clip = clipAnimations[i];
            if (!ShouldLoopClip(clip.name) || clip.loopTime)
            {
                continue;
            }

            clip.loopTime = true;
            clipAnimations[i] = clip;
            animationsChanged = true;
        }

        if (animationsChanged)
        {
            animationsImporter.clipAnimations = clipAnimations;
            animationsImporter.SaveAndReimport();
            Debug.Log($"Configured {AnimationsPath} as the Medieval locomotion animation source.");
        }
    }

    private static AnimationClip FindClip(AnimationClip[] clips, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            var match = clips.FirstOrDefault(clip => clip.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool UsesGeneratedLocomotionClips(AnimatorController controller)
    {
        foreach (var layer in controller.layers)
        {
            foreach (var childState in layer.stateMachine.states)
            {
                if (MotionUsesGeneratedClip(childState.state.motion))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MotionUsesGeneratedClip(Motion motion)
    {
        if (motion == null)
        {
            return false;
        }

        if (motion is AnimationClip clip)
        {
            var path = AssetDatabase.GetAssetPath(clip).Replace("\\", "/");
            return path.StartsWith(ClipFolder + "/", StringComparison.Ordinal) && HasStylizedArmCurves(clip);
        }

        if (motion is BlendTree blendTree)
        {
            foreach (var child in blendTree.children)
            {
                if (MotionUsesGeneratedClip(child.motion))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasStylizedArmCurves(AnimationClip clip)
    {
        return AnimationUtility.GetCurveBindings(clip).Any(binding =>
            binding.path.EndsWith("UpperArm.L", StringComparison.Ordinal)
            && binding.propertyName == "localEulerAnglesRaw.z");
    }

    private static bool ShouldLoopClip(string clipName)
    {
        return clipName.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        var folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
