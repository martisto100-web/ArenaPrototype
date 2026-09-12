using UnityEngine;

// ATTACH THIS TO: your Main Camera.
// Smooth elevated 3/4 chase view (offset up, back, and to one side). Also owns
// the screen shake that MatchDirector triggers on a kill / respawn landing,
// and the post-match zoom-out MatchModeManager triggers when a match ends.
public class CameraFollow : MonoBehaviour
{
    public Transform target;       // drag your vehicle here in the Inspector
    public Vector3 offset = new Vector3(0f, 28f, -36f); // pulled well back, ~38 deg down: small car, wide view
    public float smoothSpeed = 5f;

    [Header("Post-match zoom")]
    public float zoomOutMultiplier = 1.35f; // offset is scaled by this much at full zoom-out - pulls back and up together

    private float shakeDuration;
    private float shakeTimeLeft;
    private float shakeMagnitude;

    private float zoomFactor = 1f;
    private float zoomFactorTarget = 1f;
    private float zoomTweenSpeed = 1f; // zoomFactor units/sec, derived from the requested duration

    public bool IsZooming => Mathf.Abs(zoomFactor - zoomFactorTarget) > 0.01f;

    // Kick off (or refresh) a screen shake. A weaker shake won't cut a stronger one short.
    public void Shake(float duration, float magnitude)
    {
        if (duration * magnitude < shakeTimeLeft * shakeMagnitude) return;
        shakeDuration = duration;
        shakeTimeLeft = duration;
        shakeMagnitude = magnitude;
    }

    // Smoothly scales the offset up to zoomOutMultiplier over duration seconds -
    // MatchModeManager calls this when a match ends. IsZooming reports when the
    // transition has settled.
    public void ZoomOut(float duration)
    {
        zoomFactorTarget = zoomOutMultiplier;
        zoomTweenSpeed = duration > 0f ? Mathf.Abs(zoomFactorTarget - zoomFactor) / duration : 1000f;
    }

    // Snaps straight back to normal framing - MatchModeManager calls this at
    // the start of every new match so a zoomed-out camera never carries over.
    public void ResetZoomImmediate()
    {
        zoomFactor = 1f;
        zoomFactorTarget = 1f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        zoomFactor = Mathf.MoveTowards(zoomFactor, zoomFactorTarget, zoomTweenSpeed * Time.deltaTime);

        Vector3 desiredPosition = target.position + offset * zoomFactor;
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
