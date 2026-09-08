using UnityEngine;
using UnityEngine.Events;

// ATTACH THIS TO: anything that should be able to take damage and be destroyed —
// the target dummy, the player vehicle, enemies.
public class Health : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Events")]
    public UnityEvent onDeath; // hook explosions / score / game-over here later

    public float CurrentHealth { get; private set; }
    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f) return; // already dead, ignore extra hits this frame

        CurrentHealth -= amount;
        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        // Placeholder for now — fire the hook, then remove the object.
        // Later: play an explosion effect, award points, trigger respawn, etc.
        onDeath?.Invoke();
        Destroy(gameObject);
    }
}
