using System.Collections.Generic;
using UnityEngine;

// ATTACH THIS TO: the GameDirector.
// Safety net: any car that drops below fallLimit (fell off an open-edged arena,
// or clipped through geometry) is killed so its Respawner drops it back in.
public class KillFloor : MonoBehaviour
{
    public float fallLimit = -8f;

    private Health[] cars;

    void Start()
    {
        List<Health> list = new List<Health>();
        foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h.GetComponent<Respawner>() != null) list.Add(h);
        }
        cars = list.ToArray();
    }

    void Update()
    {
        foreach (Health h in cars)
        {
            if (h == null || h.IsDead) continue;
            if (h.transform.position.y < fallLimit)
            {
                h.TakeDamage(h.maxHealth * 2f); // force death -> respawn
            }
        }
    }
}
