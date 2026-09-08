using UnityEngine;

// ATTACH THIS TO: anything that should be able to take damage and be destroyed —
// the target dummy for now, later the player vehicle and enemies too.
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        // Placeholder for now — just removes the object.
        // Later: play an explosion effect, award points, trigger respawn, etc.
        Destroy(gameObject);
    }
}
