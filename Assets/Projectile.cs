using UnityEngine;

// ATTACH THIS TO: your bullet/rocket prefab (not to the vehicle).
public class Projectile : MonoBehaviour
{
    public float speed = 40f;
    public float damage = 10f;
    public float lifeTime = 3f; // auto-destroy if it never hits anything, so it doesn't fly forever

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
        Health targetHealth = other.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
        }
        Destroy(gameObject);
    }
}
