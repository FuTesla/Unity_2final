using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class MedievalAnimationSetup
{
    private const string ControllerPath = "Assets/Gameplay/AnimationControllers/AC_Medieval_Player.controller";
    private const string AnimationsPath = "Assets/Art/Female_Character/Individual Characters/FBX/Medieval.fbx";
    private const string EnemyModelPath = "Assets/Art/Female_Character/Individual Characters/FBX/Enemy_Suit.fbx";

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
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolder("Assets/Gameplay/AnimationControllers");
        ConfigureGenericImporters();

        if (ControllerIsReady(AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)))
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
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
        var importedClips = AssetDatabase.LoadAllAssetRepresentationsAtPath(AnimationsPath)
            .OfType<AnimationClip>()
            .Where(clip => clip != null && !clip.name.StartsWith("__", StringComparison.Ordinal))
            .ToArray();

        var idle = FindClip(importedClips, "CharacterArmature|Idle", "Idle_Neutral", "Idle") ?? importedClips.FirstOrDefault();
        var walk = FindClip(importedClips, "CharacterArmature|Walk", "Walk") ?? idle;
        var run = FindClip(importedClips, "CharacterArmature|Run", "Run") ?? walk;
        var swordSlash = FindClip(importedClips, "CharacterArmature|Sword_Slash", "Sword_Slash", "Slash", "Attack");
        var punchRight = FindClip(importedClips, "CharacterArmature|Punch_Right", "Punch_Right", "PunchRight");
        var punchLeft = FindClip(importedClips, "CharacterArmature|Punch_Left", "Punch_Left", "PunchLeft");
        var kickRight = FindClip(importedClips, "CharacterArmature|Kick_Right", "Kick_Right", "KickRight");
        var hit = FindClip(importedClips, "CharacterArmature|HitRecieve", "HitRecieve", "HitReceive", "Hit");
        var hit2 = FindClip(importedClips, "CharacterArmature|HitRecieve_2", "HitRecieve_2", "HitReceive_2", "Hit_2");
        var death = FindClip(importedClips, "CharacterArmature|Death", "Death", "Die", "Dead");
        var wave = FindClip(importedClips, "CharacterArmature|Wave", "Wave", "Interact");

        if (idle == null || walk == null || run == null || swordSlash == null || punchRight == null || punchLeft == null || kickRight == null || hit == null || hit2 == null || death == null || wave == null)
        {
            Debug.LogError($"Could not find Idle/Walk/Run/Sword Slash/Punch Right/Punch Left/Kick Right/Hit/Hit 2/Death/Wave clips in {AnimationsPath}.");
            return;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("PunchRight", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("PunchLeft", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("KickRight", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit2", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);

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

        var attack = stateMachine.AddState("Sword Slash");
        attack.motion = swordSlash;
        attack.writeDefaultValues = true;
        attack.speed = 1.05f;

        var enterAttack = stateMachine.AddAnyStateTransition(attack);
        enterAttack.hasExitTime = false;
        enterAttack.duration = 0.05f;
        enterAttack.canTransitionToSelf = false;
        enterAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

        var exitAttack = attack.AddTransition(locomotion);
        exitAttack.hasExitTime = true;
        exitAttack.exitTime = 0.85f;
        exitAttack.duration = 0.08f;

        AddOneShotState(stateMachine, locomotion, "Punch Right", punchRight, "PunchRight", 1f, 0.9f, 0.08f);
        AddOneShotState(stateMachine, locomotion, "Punch Left", punchLeft, "PunchLeft", 1f, 0.9f, 0.08f);
        AddOneShotState(stateMachine, locomotion, "Kick Right", kickRight, "KickRight", 1f, 0.9f, 0.08f);

        var hitState = stateMachine.AddState("Hit");
        hitState.motion = hit;
        hitState.writeDefaultValues = true;

        var enterHit = stateMachine.AddAnyStateTransition(hitState);
        enterHit.hasExitTime = false;
        enterHit.duration = 0.05f;
        enterHit.canTransitionToSelf = false;
        enterHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

        var exitHit = hitState.AddTransition(locomotion);
        exitHit.hasExitTime = true;
        exitHit.exitTime = 0.9f;
        exitHit.duration = 0.08f;

        var hit2State = stateMachine.AddState("Hit 2");
        hit2State.motion = hit2;
        hit2State.writeDefaultValues = true;

        var enterHit2 = stateMachine.AddAnyStateTransition(hit2State);
        enterHit2.hasExitTime = false;
        enterHit2.duration = 0.05f;
        enterHit2.canTransitionToSelf = false;
        enterHit2.AddCondition(AnimatorConditionMode.If, 0f, "Hit2");

        var exitHit2 = hit2State.AddTransition(locomotion);
        exitHit2.hasExitTime = true;
        exitHit2.exitTime = 0.9f;
        exitHit2.duration = 0.08f;

        var waveState = stateMachine.AddState("Wave");
        waveState.motion = wave;
        waveState.writeDefaultValues = true;

        var enterWave = stateMachine.AddAnyStateTransition(waveState);
        enterWave.hasExitTime = false;
        enterWave.duration = 0.06f;
        enterWave.canTransitionToSelf = false;
        enterWave.AddCondition(AnimatorConditionMode.If, 0f, "Interact");

        var exitWave = waveState.AddTransition(locomotion);
        exitWave.hasExitTime = true;
        exitWave.exitTime = 0.9f;
        exitWave.duration = 0.1f;

        var deathState = stateMachine.AddState("Death");
        deathState.motion = death;
        deathState.writeDefaultValues = true;

        var enterDeath = stateMachine.AddAnyStateTransition(deathState);
        enterDeath.hasExitTime = false;
        enterDeath.duration = 0.05f;
        enterDeath.canTransitionToSelf = false;
        enterDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        var layers = controller.layers;
        layers[0].defaultWeight = 1f;
        controller.layers = layers;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created {ControllerPath} using FBX clips Idle='{idle.name}', Walk='{walk.name}', Run='{run.name}', Attack='{swordSlash.name}', PunchRight='{punchRight.name}', PunchLeft='{punchLeft.name}', KickRight='{kickRight.name}', Hit='{hit.name}', Hit2='{hit2.name}', Death='{death.name}', Wave='{wave.name}'.");
    }

    private static AnimatorState AddOneShotState(
        AnimatorStateMachine stateMachine,
        AnimatorState locomotion,
        string stateName,
        Motion motion,
        string triggerName,
        float speed,
        float exitTime,
        float exitDuration)
    {
        var state = stateMachine.AddState(stateName);
        state.motion = motion;
        state.writeDefaultValues = true;
        state.speed = speed;

        var enter = stateMachine.AddAnyStateTransition(state);
        enter.hasExitTime = false;
        enter.duration = 0.05f;
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

        var exit = state.AddTransition(locomotion);
        exit.hasExitTime = true;
        exit.exitTime = exitTime;
        exit.duration = exitDuration;

        return state;
    }

    private static void ConfigureGenericImporters()
    {
        ConfigureModelImporter(EnemyModelPath, false, "enemy animation target");
        ConfigureModelImporter(AnimationsPath, true, "Medieval locomotion animation source");
    }

    private static void ConfigureModelImporter(string modelPath, bool configureLooping, string description)
    {
        var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (modelImporter == null)
        {
            if (configureLooping)
            {
                Debug.LogWarning($"Could not find animation importer at {modelPath}.");
            }

            return;
        }

        var changed = false;
        if (modelImporter.animationType != ModelImporterAnimationType.Generic)
        {
            modelImporter.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (modelImporter.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (configureLooping)
        {
            var clipAnimations = modelImporter.defaultClipAnimations;
            for (var i = 0; i < clipAnimations.Length; i++)
            {
                var clip = clipAnimations[i];
                if (!ShouldLoopClip(clip.name) || clip.loopTime)
                {
                    continue;
                }

                clip.loopTime = true;
                clipAnimations[i] = clip;
                changed = true;
            }

            if (changed)
            {
                modelImporter.clipAnimations = clipAnimations;
            }
        }

        if (changed)
        {
            modelImporter.SaveAndReimport();
            Debug.Log($"Configured {modelPath} as the {description}.");
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

    private static bool ControllerIsReady(AnimatorController controller)
    {
        return ControllerUsesSourceClips(controller)
            && ControllerHasTrigger(controller, "Attack")
            && ControllerHasTrigger(controller, "PunchRight")
            && ControllerHasTrigger(controller, "PunchLeft")
            && ControllerHasTrigger(controller, "KickRight")
            && ControllerHasTrigger(controller, "Hit")
            && ControllerHasTrigger(controller, "Hit2")
            && ControllerHasTrigger(controller, "Death")
            && ControllerHasTrigger(controller, "Interact")
            && ControllerHasState(controller, "Sword Slash")
            && ControllerHasState(controller, "Punch Right")
            && ControllerHasState(controller, "Punch Left")
            && ControllerHasState(controller, "Kick Right")
            && ControllerHasState(controller, "Hit")
            && ControllerHasState(controller, "Hit 2")
            && ControllerHasState(controller, "Death")
            && ControllerHasState(controller, "Wave");
    }

    private static bool ControllerUsesSourceClips(AnimatorController controller)
    {
        if (controller == null)
        {
            return false;
        }

        foreach (var layer in controller.layers)
        {
            foreach (var childState in layer.stateMachine.states)
            {
                if (MotionUsesSourceClip(childState.state.motion))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ControllerHasTrigger(AnimatorController controller, string parameterName)
    {
        if (controller == null)
        {
            return false;
        }

        return controller.parameters.Any(parameter => parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName);
    }

    private static bool ControllerHasState(AnimatorController controller, string stateName)
    {
        if (controller == null)
        {
            return false;
        }

        return controller.layers.Any(layer => layer.stateMachine.states.Any(childState => childState.state.name == stateName));
    }

    private static bool MotionUsesSourceClip(Motion motion)
    {
        if (motion == null)
        {
            return false;
        }

        if (motion is AnimationClip clip)
        {
            var path = AssetDatabase.GetAssetPath(clip).Replace("\\", "/");
            return path == AnimationsPath;
        }

        if (motion is BlendTree blendTree)
        {
            foreach (var child in blendTree.children)
            {
                if (MotionUsesSourceClip(child.motion))
                {
                    return true;
                }
            }
        }

        return false;
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
