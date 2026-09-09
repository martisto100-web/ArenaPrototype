using UnityEngine;

// ATTACH THIS TO: your Main Camera.
// Smooth elevated 3/4 chase view (offset up, back, and to one side). Also owns
// the screen shake that MatchDirector triggers on a kill / respawn landing.
public class CameraFollow : MonoBehaviour
{
    public Transform target;       // drag your vehicle here in the Inspector
    public Vector3 offset = new Vector3(0f, 13f, -16f); // behind + above, ~35 deg down: an action chase view
    public float smoothSpeed = 5f;

    private float shakeDuration;
    private float shakeTimeLeft;
    private float shakeMagnitude;

    // Kick off (or refresh) a screen shake. A weaker shake won't cut a stronger one short.
    public void Shake(float duration, float magnitude)
    {
        if (duration * magnitude < shakeTimeLeft * shakeMagnitude) return;
        shakeDuration = duration;
        shakeTimeLeft = duration;
        shakeMagnitude = magnitude;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        if (shakeTimeLeft > 0f)
        {
            shakeTimeLeft -= Time.deltaTime;
            float falloff = shakeDuration > 0f ? Mathf.Max(0f, shakeTimeLeft / shakeDuration) : 0f;
            transform.position += Random.insideUnitSphere * (shakeMagnitude * falloff);
        }

        // Look slightly above the vehicle's base so the framing feels natural.
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}
