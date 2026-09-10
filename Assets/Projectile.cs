using UnityEngine;

// ATTACH THIS TO: your bullet/rocket prefab (not to the vehicle).
// Team is set by the Weapon that fires it; bullets pass through anything on the
// same team (including the shooter) and through other bullets. Neutral objects
// with no TeamMember (destructible props) can be hit by any bullet.
//
// playerHitSfx plays a 2D one-shot ONLY when this projectile damages the local
// player's car (tag "Player") - i.e. an incoming enemy hit, from the player's
// POV. The clip set is a per-projectile field on purpose: the bullet uses
// metal-on-metal, a future rocket/laser prefab sets its own clips. Clips
// round-robin (2-3 near identical takes) with a little pitch jitter; auto-load
// from Resources/Audio/BulletImpact if the array is left empty. Loudness is the
// player setting GameSettings.BulletHitVolume (0 = off), not a field here.
public class Projectile : MonoBehaviour
{
    public float speed = 40f;
    public float damage = 10f;
    public float lifeTime = 3f; // auto-destroy if it never hits anything, so it doesn't fly forever

    [Header("Player-hit audio")]
    public AudioClip[] playerHitSfx;                       // empty -> Resources/Audio/BulletImpact
    [Range(0f, 0.4f)] public float playerHitPitchJitter = 0.06f;

    [HideInInspector] public Team team = Team.Player;

    private static AudioClip[] cachedHitSfx;               // loaded once, shared by every bullet
    private static int hitCursor;                          // round-robin across bullets

    void Awake()
    {
        if (playerHitSfx == null || playerHitSfx.Length == 0)
        {
            if (cachedHitSfx == null) cachedHitSfx = Resources.LoadAll<AudioClip>("Audio/BulletImpact");
            playerHitSfx = cachedHitSfx;
        }
    }

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
        if (other.GetComponentInParent<Projectile>() != null)
        {
            return; // bullets pass through each other
        }

        TeamMember hitTeam = other.GetComponentInParent<TeamMember>();
        if (hitTeam != null && hitTeam.team == team)
        {
            return; // friendly fire / self hit — fly straight through
        }

        Health targetHealth = other.GetComponentInParent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
            if (targetHealth.CompareTag("Player")) PlayPlayerHit();
        }
        Destroy(gameObject);
    }

    // A metal-on-metal hit on the player's car. Played on a throwaway object so
    // it outlives this bullet (destroyed on the next line). 2D = it's your car.
    void PlayPlayerHit()
    {
        if (playerHitSfx == null || playerHitSfx.Length == 0) return;

        float volume = GameSettings.BulletHitVolume;
        if (volume <= 0f) return; // player turned hit sounds off

        AudioClip clip = playerHitSfx[hitCursor % playerHitSfx.Length];
        hitCursor++;
        if (clip == null) return;

        GameObject go = new GameObject("BulletImpactSfx");
        go.transform.position = transform.position;
        AudioSource a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.spatialBlend = 0f;
        a.pitch = 1f + Random.Range(-playerHitPitchJitter, playerHitPitchJitter);
        a.volume = volume;
        a.Play();
        Destroy(go, clip.length / Mathf.Max(0.1f, a.pitch) + 0.1f);
    }
}
