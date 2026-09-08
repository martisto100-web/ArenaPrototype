using UnityEngine;

// ATTACH THIS TO: your bullet/rocket prefab (not to the vehicle).
// Team is set by the Weapon that fires it; bullets pass through anything on the
// same team (including the shooter). Neutral objects with no TeamMember (target
// dummy, props) can be hit by any bullet.
public class Projectile : MonoBehaviour
{
    public float speed = 40f;
    public float damage = 10f;
    public float lifeTime = 3f; // auto-destroy if it never hits anything, so it doesn't fly forever

    [HideInInspector] public Team team = Team.Player;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        TeamMember hitTeam = other.GetComponentInParent<TeamMember>();
        if (hitTeam != null && hitTeam.team == team)
        {
            return; // friendly fire / self hit — fly straight through
        }

        Health targetHealth = other.GetComponentInParent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
        }
        Destroy(gameObject);
    }
}
