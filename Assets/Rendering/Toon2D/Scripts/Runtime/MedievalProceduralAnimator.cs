using System.Collections.Generic;
using UnityEngine;

public sealed class MedievalProceduralAnimator : MonoBehaviour
{
    private const float MinimumRunCycleSpeed = 12.5f;

    public TopDownCharacterMotor motor;
    public Animator animatorToDisable;
    public float walkCycleSpeed = 7.5f;
    public float runCycleSpeed = 12.5f;
    public float armDownAngle = 62f;
    public float armForwardSwing = 0.34f;
    public float armSideRelax = 0.16f;

    private readonly Dictionary<Transform, Quaternion> baseRotations = new Dictionary<Transform, Quaternion>();
    private Transform chest;
    private Transform shoulderLeft;
    private Transform shoulderRight;
    private Transform upperArmLeft;
    private Transform upperArmRight;
    private Transform lowerArmLeft;
    private Transform lowerArmRight;
    private Transform upperLegLeft;
    private Transform upperLegRight;
    private Transform lowerLegLeft;
    private Transform lowerLegRight;

    private void Awake()
    {
        runCycleSpeed = Mathf.Max(runCycleSpeed, MinimumRunCycleSpeed);

        if (motor == null)
        {
            motor = GetComponent<TopDownCharacterMotor>();
        }

        if (animatorToDisable == null)
        {
            animatorToDisable = GetComponentInChildren<Animator>();
        }

        if (animatorToDisable != null)
        {
            animatorToDisable.enabled = false;
        }

        chest = FindDescendant("Chest");
        shoulderLeft = FindDescendant("Shoulder.L");
        shoulderRight = FindDescendant("Shoulder.R");
        upperArmLeft = FindDescendant("UpperArm.L");
        upperArmRight = FindDescendant("UpperArm.R");
        lowerArmLeft = FindDescendant("LowerArm.L");
        lowerArmRight = FindDescendant("LowerArm.R");
        upperLegLeft = FindDescendant("UpperLeg.L");
        upperLegRight = FindDescendant("UpperLeg.R");
        lowerLegLeft = FindDescendant("LowerLeg.L");
        lowerLegRight = FindDescendant("LowerLeg.R");

        CacheBase(chest);
        CacheBase(shoulderLeft);
        CacheBase(shoulderRight);
        CacheBase(upperArmLeft);
        CacheBase(upperArmRight);
        CacheBase(lowerArmLeft);
        CacheBase(lowerArmRight);
        CacheBase(upperLegLeft);
        CacheBase(upperLegRight);
        CacheBase(lowerLegLeft);
        CacheBase(lowerLegRight);
    }

    private void LateUpdate()
    {
        var moveAmount = motor != null ? motor.MoveAmount : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).magnitude;
        var isRunning = motor != null ? motor.IsRunning : Input.GetKey(KeyCode.LeftShift);
        var moving = moveAmount > 0.05f;

        if (!moving)
        {
            ApplyIdle();
            return;
        }

        var cycleSpeed = isRunning ? runCycleSpeed : walkCycleSpeed;
        var legAmplitude = isRunning ? 42f : 28f;
        var kneeAmplitude = isRunning ? 48f : 32f;
        var armAmplitude = isRunning ? 28f : 18f;
        var phase = Time.time * cycleSpeed;
        var swing = Mathf.Sin(phase);
        var oppositeSwing = -swing;

        Rotate(upperLegLeft, legAmplitude * swing, 0f, 0f);
        Rotate(upperLegRight, legAmplitude * oppositeSwing, 0f, 0f);
        Rotate(lowerLegLeft, Mathf.Max(0f, oppositeSwing) * kneeAmplitude, 0f, 0f);
        Rotate(lowerLegRight, Mathf.Max(0f, swing) * kneeAmplitude, 0f, 0f);

        RelaxArm(shoulderLeft);
        RelaxArm(shoulderRight);
        PointArm(upperArmLeft, lowerArmLeft, BuildArmDirection(oppositeSwing, -1f));
        PointArm(upperArmRight, lowerArmRight, BuildArmDirection(swing, 1f));
        BendForearm(lowerArmLeft, swing, -1f);
        BendForearm(lowerArmRight, oppositeSwing, 1f);
        Rotate(chest, 0f, 0f, Mathf.Sin(phase * 0.5f) * 3f);
    }

    private void ApplyIdle()
    {
        var breathe = Mathf.Sin(Time.time * 2.2f);
        Rotate(upperLegLeft, 0f, 0f, 0f);
        Rotate(upperLegRight, 0f, 0f, 0f);
        Rotate(lowerLegLeft, 0f, 0f, 0f);
        Rotate(lowerLegRight, 0f, 0f, 0f);
        RelaxArm(shoulderLeft);
        RelaxArm(shoulderRight);
        PointArm(upperArmLeft, lowerArmLeft, BuildArmDirection(breathe * 0.08f, -1f));
        PointArm(upperArmRight, lowerArmRight, BuildArmDirection(-breathe * 0.08f, 1f));
        BendForearm(lowerArmLeft, 0f, -1f);
        BendForearm(lowerArmRight, 0f, 1f);
        Rotate(chest, 0f, 0f, breathe * 1.5f);
    }

    private Transform FindDescendant(string targetName)
    {
        return FindDescendant(transform, targetName);
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root.name == targetName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            var found = FindDescendant(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void CacheBase(Transform bone)
    {
        if (bone != null && !baseRotations.ContainsKey(bone))
        {
            baseRotations.Add(bone, bone.localRotation);
        }
    }

    private void Rotate(Transform bone, float x, float y, float z)
    {
        if (bone == null || !baseRotations.TryGetValue(bone, out var baseRotation))
        {
            return;
        }

        bone.localRotation = baseRotation * Quaternion.Euler(x, y, z);
    }

    private void RelaxArm(Transform bone)
    {
        Rotate(bone, 0f, 0f, 0f);
    }

    private Vector3 BuildArmDirection(float swing, float sideSign)
    {
        var down = -transform.up;
        var forward = transform.forward * (swing * armForwardSwing);
        var outward = transform.right * (sideSign * armSideRelax);
        return (down + forward + outward).normalized;
    }

    private void PointArm(Transform upperArm, Transform lowerArm, Vector3 targetDirection)
    {
        if (upperArm == null || lowerArm == null)
        {
            return;
        }

        Rotate(upperArm, 0f, 0f, 0f);
        var currentDirection = lowerArm.position - upperArm.position;
        if (currentDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        upperArm.rotation = Quaternion.FromToRotation(currentDirection.normalized, targetDirection) * upperArm.rotation;
    }

    private void BendForearm(Transform lowerArm, float swing, float sideSign)
    {
        if (lowerArm == null || !baseRotations.TryGetValue(lowerArm, out var baseRotation))
        {
            return;
        }

        var bend = 14f + Mathf.Max(0f, swing) * 18f;
        lowerArm.localRotation = baseRotation * Quaternion.Euler(bend, 0f, sideSign * 8f);
    }
}
