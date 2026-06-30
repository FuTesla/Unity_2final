using UnityEngine;

public sealed class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public string deathTrigger = "Death";
    public string deathStateName = "Death";
    public string damageZoneName = "Damage Zone";
    public float damagePerSecond = 10f;
    public float damagePopupYOffset = 1.9f;
    public float minDamagePopupAmount = 0.5f;
    public float damagePopupInterval = 0.35f;

    private Animator animator;
    private EnemyAIController aiController;
    private CharacterController characterController;
    private float currentHealth;
    private float pendingPopupDamage;
    private float nextDamagePopupTime;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float Health01 => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => isDead;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        aiController = GetComponent<EnemyAIController>();
        characterController = GetComponent<CharacterController>();
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        var damageZoneObject = GameObject.Find(damageZoneName);
        var damageZone = damageZoneObject != null ? damageZoneObject.GetComponent<DamageZone>() : null;
        if (damageZone == null)
        {
            return;
        }

        if (damageZone.ContainsPosition(transform.position))
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

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        ShowDamagePopup(amount);
        if (currentHealth <= 0f)
        {
            Die();
        }
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
            Debug.LogWarning($"Damage popup failed on enemy: {exception.Message}", this);
        }
    }

    private void Die()
    {
        isDead = true;

        if (aiController != null)
        {
            aiController.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (animator == null)
        {
            return;
        }

        animator.enabled = true;
        animator.speed = 1f;
        animator.applyRootMotion = false;
        SetFloatIfExists("Speed", 0f);
        SetBoolIfExists("IsMoving", false);
        SetBoolIfExists("Moving", false);
        SetBoolIfExists("IsRunning", false);
        SetBoolIfExists("Running", false);
        SetTriggerIfExists(deathTrigger);
        if (!string.IsNullOrEmpty(deathStateName))
        {
            animator.CrossFadeInFixedTime(deathStateName, 0.05f);
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

    private void SetTriggerIfExists(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            return;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                animator.SetTrigger(parameterName);
                return;
            }
        }
    }
}
