using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public string deathTrigger = "Death";
    public string damageZoneName = "Damage Zone";
    public float damageZoneRadius = 1.3f;
    public float damagePerSecond = 10f;

    private Animator animator;
    private TopDownCharacterMotor motor;
    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float Health01 => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => isDead;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        motor = GetComponent<TopDownCharacterMotor>();
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

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        if (motor != null)
        {
            motor.enabled = false;
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
        SetTriggerIfExists(deathTrigger);
        animator.CrossFadeInFixedTime("Death", 0.05f);
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
