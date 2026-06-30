using UnityEngine;
using System.Collections;

public sealed class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public string firstHitTrigger = "Hit";
    public string secondHitTrigger = "Hit2";
    public string firstHitStateName = "Hit";
    public string secondHitStateName = "Hit 2";
    public float hitFeedbackInterval = 0.7f;
    public string deathTrigger = "Death";
    public string deathStateName = "Death";
    public string damageZoneName = "Damage Zone";
    public float damageZoneRadius = 1.3f;
    public float damagePerSecond = 10f;
    public float damagePopupYOffset = 1.9f;
    public float minDamagePopupAmount = 0.5f;
    public float damagePopupInterval = 0.35f;

    private Animator animator;
    private TopDownCharacterMotor motor;
    private IsometricCameraFollow cameraFollow;
    private PauseMenuController pauseMenu;
    private float currentHealth;
    private float lastHitFeedbackTime = float.NegativeInfinity;
    private float pendingPopupDamage;
    private float nextDamagePopupTime;
    private bool playSecondHitAnimationNext;
    private bool isDead;
    private bool deathMenuShown;

    public float CurrentHealth => currentHealth;
    public float Health01 => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => isDead;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        motor = GetComponent<TopDownCharacterMotor>();
        cameraFollow = Camera.main != null ? Camera.main.GetComponent<IsometricCameraFollow>() : null;
        pauseMenu = GetComponent<PauseMenuController>();
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        var damageZone = GameObject.Find(damageZoneName);
        if (damageZone == null)
        {
            return;
        }

        var planarOffset = new Vector2(
            transform.position.x - damageZone.transform.position.x,
            transform.position.z - damageZone.transform.position.z);

        if (planarOffset.sqrMagnitude <= damageZoneRadius * damageZoneRadius)
        {
            TakeDamage(damagePerSecond * Time.deltaTime);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        if (cameraFollow == null && Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<IsometricCameraFollow>();
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (currentHealth <= 0f)
        {
            ShowDamagePopup(amount);
            Die();
            return;
        }

        PlayHitFeedback();
        ShowDamagePopup(amount);
    }

    private void ShowDamagePopup(float amount)
    {
        if (amount >= minDamagePopupAmount)
        {
            TryShowDamagePopup(amount);
            return;
        }

        pendingPopupDamage += amount;
        if (pendingPopupDamage < minDamagePopupAmount || Time.time < nextDamagePopupTime)
        {
            return;
        }

        TryShowDamagePopup(pendingPopupDamage);
        pendingPopupDamage = 0f;
        nextDamagePopupTime = Time.time + damagePopupInterval;
    }

    private void TryShowDamagePopup(float amount)
    {
        try
        {
            DamagePopup.Show(transform.position + Vector3.up * damagePopupYOffset, amount);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Damage popup failed on player: {exception.Message}", this);
        }
    }

    private void PlayHitFeedback()
    {
        if (Time.time - lastHitFeedbackTime < hitFeedbackInterval)
        {
            return;
        }

        lastHitFeedbackTime = Time.time;

        if (cameraFollow != null)
        {
            cameraFollow.TryPlayDamageShake();
        }

        if (animator == null)
        {
            return;
        }

        SetFloatIfExists("Speed", 0f);
        SetBoolIfExists("IsMoving", false);
        SetBoolIfExists("Moving", false);
        SetBoolIfExists("IsRunning", false);
        SetBoolIfExists("Running", false);

        var triggerName = playSecondHitAnimationNext ? secondHitTrigger : firstHitTrigger;
        var stateName = playSecondHitAnimationNext ? secondHitStateName : firstHitStateName;
        playSecondHitAnimationNext = !playSecondHitAnimationNext;

        if (SetTriggerIfExists(triggerName))
        {
            return;
        }

        if (!string.IsNullOrEmpty(stateName))
        {
            animator.CrossFadeInFixedTime(stateName, 0.05f);
        }
    }

    private void Die()
    {
        isDead = true;

        if (motor != null)
        {
            motor.enabled = false;
        }

        if (pauseMenu == null)
        {
            pauseMenu = GetComponent<PauseMenuController>();
        }

        StartCoroutine(ShowDeathMenuAfterAnimation());

        if (animator == null)
        {
            ShowDeathMenuImmediate();
            return;
        }

        SetFloatIfExists("Speed", 0f);
        SetBoolIfExists("IsMoving", false);
        SetBoolIfExists("Moving", false);
        SetBoolIfExists("IsRunning", false);
        SetBoolIfExists("Running", false);
        SetTriggerIfExists(deathTrigger);
        animator.CrossFadeInFixedTime(deathStateName, 0.05f);
    }

    private IEnumerator ShowDeathMenuAfterAnimation()
    {
        if (animator == null)
        {
            ShowDeathMenuImmediate();
            yield break;
        }

        yield return null;

        var waitTime = GetDeathAnimationDuration();
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        ShowDeathMenuImmediate();
    }

    private float GetDeathAnimationDuration()
    {
        if (animator == null)
        {
            return 0f;
        }

        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (!string.IsNullOrEmpty(deathStateName) && state.IsName(deathStateName))
        {
            return Mathf.Max(0.05f, state.length);
        }

        return 1f;
    }

    private void ShowDeathMenuImmediate()
    {
        if (deathMenuShown)
        {
            return;
        }

        deathMenuShown = true;

        if (pauseMenu == null)
        {
            pauseMenu = GetComponent<PauseMenuController>();
        }

        if (pauseMenu != null)
        {
            pauseMenu.ShowDeathMenu();
        }
    }

    private void SetFloatIfExists(string parameterName, float value)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == parameterName)
            {
                animator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private void SetBoolIfExists(string parameterName, bool value)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private bool SetTriggerIfExists(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                animator.SetTrigger(parameterName);
                return true;
            }
        }

        return false;
    }
}
