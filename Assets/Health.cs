using System;
using UnityEngine;
using UnityEngine.Events;

// ATTACH THIS TO: anything that should be able to take damage — the player
// vehicle, enemies, destructible props.
public class Health : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Death")]
    public bool destroyOnDeath = true; // a Respawner sets this false so the car can be revived instead

    [Header("Events")]
    public UnityEvent onDeath; // Inspector hook

    public event Action<Health> Died; // code hook (Respawner, MatchDirector)

    public float CurrentHealth { get; private set; }
    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;
    public bool IsDead => CurrentHealth <= 0f;

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f) return; // already dead, ignore extra hits

        CurrentHealth -= amount;
        if (CurrentHealth <= 0f)
        {
            CurrentHealth = 0f;
            Die();
        }
    }

    public void Revive()
    {
        CurrentHealth = maxHealth;
    }

    void Die()
    {
        Died?.Invoke(this);
        onDeath?.Invoke();
        if (destroyOnDeath) Destroy(gameObject);
    }
}
