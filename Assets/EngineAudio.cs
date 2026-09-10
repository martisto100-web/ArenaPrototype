using UnityEngine;

// ATTACH THIS TO: a car root (needs CarController).
// One looping engine sound (the idle clip). Its PITCH and volume are lerped
// between the idle end and the max-RPM end by "load" = CurrentSpeed / moveSpeed:
//   stopped        -> idlePitch / idleVolume   (one steady tone)
//   full speed     -> maxPitch  / maxVolume    (one steady tone, higher)
//   in between     -> a smooth slide as the car accelerates / decelerates
//                     (CarController.acceleration ramps CurrentSpeed;
//                     responseSpeed adds a touch more smoothing).
// 2D on the local player's car (you always hear your own); 3D with distance
// falloff on any other car. The loop auto-loads from Resources/Audio/Engine if
// engineLoop is left empty.
[RequireComponent(typeof(CarController))]
public class EngineAudio : MonoBehaviour
{
    [Header("Clip (empty -> Resources/Audio/Engine)")]
    public AudioClip engineLoop;

    [Header("Idle end (car stopped)")]
    [Range(0f, 1f)] public float idleVolume = 0.45f;
    [Range(0.2f, 2f)] public float idlePitch = 1f;

    [Header("Max-RPM end (full throttle)")]
    [Range(0f, 1f)] public float maxVolume = 0.7f;
    [Range(0.5f, 3f)] public float maxPitch = 1.7f;   // raise for more scream, lower for a lazier engine

    [Header("Response")]
    [Range(0f, 20f)] public float responseSpeed = 6f;  // extra smoothing on top of the car's own accel/decel
    public float spatialMaxDistance = 45f;             // rolloff distance when this isn't the local player

    private CarController controller;
    private AudioSource source;
    private float load;

    void Awake()
    {
        controller = GetComponent<CarController>();

        if (engineLoop == null)
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/Engine");
            if (clips.Length > 0) engineLoop = clips[0];
        }

        source = gameObject.AddComponent<AudioSource>();
        source.clip = engineLoop;
        source.loop = true;
        source.playOnAwake = false;

        if (CompareTag("Player"))
        {
            source.spatialBlend = 0f;
        }
        else
        {
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = spatialMaxDistance;
        }
    }

    void OnEnable()
    {
        load = 0f; // start / respawn at idle
        if (source != null && engineLoop != null) source.Play();
    }

    void OnDisable()
    {
        if (source != null) source.Stop();
    }

    void Update()
    {
        if (source == null) return;

        float target = Mathf.Clamp01(controller.CurrentSpeed / Mathf.Max(0.1f, controller.moveSpeed));
        load = Mathf.Lerp(load, target, 1f - Mathf.Exp(-responseSpeed * Time.deltaTime));

        source.pitch = Mathf.Lerp(idlePitch, maxPitch, load);
        source.volume = Mathf.Lerp(idleVolume, maxVolume, load);
    }
}
