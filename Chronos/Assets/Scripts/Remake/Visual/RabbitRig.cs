using UnityEngine;

// Procedural secondary animation for the remake rabbits. It only *reads* the motion the
// original CharacterMove/CharacterHop states produce (transform deltas) and layers
// squash & stretch, ear drag, a spinning wind-up part and blinking on top of it.
public class RabbitRig : MonoBehaviour
{
    // Mount transform under the original Player/Phantom root (root scale 2, root 1 m above tile top).
    public const float MountScale = 0.575f;            // 1.15x world size: readable at game-camera distance
    public static readonly Vector3 MountOffset = new(0f, -0.5f, 0f);

    public Transform body;
    public Transform head;
    public Transform earL, earR;
    public Transform spinner;            // wind-up key / gear / hourglass depending on design
    public Vector3 spinAxis = Vector3.forward;
    public Transform[] eyes;
    public Transform groundRing;         // rotates slowly (dashed for phantom)
    public float ringSpin = 0f;

    Vector3 lastPos, bodyScale0, headPos0, eyeScale0;
    Quaternion earL0, earR0;
    float squash, earDrag, landT, blinkT = 2f, seed;
    bool wasAir;

    void Start()
    {
        lastPos = transform.position;
        if (body) bodyScale0 = body.localScale;
        if (head) headPos0 = head.localPosition;
        if (earL) earL0 = earL.localRotation;
        if (earR) earR0 = earR.localRotation;
        if (eyes != null && eyes.Length > 0 && eyes[0]) eyeScale0 = eyes[0].localScale;
        seed = Random.value * 10f;
    }

    void OnEnable() { lastPos = transform.position; }

    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        Vector3 p = transform.position;
        Vector3 v = (p - lastPos) / dt;
        lastPos = p;
        if (v.sqrMagnitude > 2500f) v = Vector3.zero; // teleports (reset / rewind restore)

        float hs = new Vector2(v.x, v.z).magnitude;
        bool air = hs > 0.4f || Mathf.Abs(v.y) > 0.25f;
        if (wasAir && !air) landT = 0.2f;
        wasAir = air;

        float target = air ? 0.05f + Mathf.Clamp(v.y * 0.05f, -0.1f, 0.16f) : 0f;
        if (landT > 0f) { landT -= dt; target = -0.17f * Mathf.Clamp01(landT / 0.2f); }
        squash = Mathf.Lerp(squash, target, 1f - Mathf.Exp(-dt * 20f));
        if (body) body.localScale = Vector3.Scale(bodyScale0, new Vector3(1f - squash * 0.55f, 1f + squash, 1f - squash * 0.55f));
        if (head) head.localPosition = headPos0 + Vector3.up * squash * 0.35f;

        earDrag = Mathf.Lerp(earDrag, air ? 30f + Mathf.Clamp(-v.y * 6f, -15f, 25f) : (landT > 0 ? -12f : 0f), 1f - Mathf.Exp(-dt * 12f));
        float idle = Mathf.Sin(Time.time * 2.3f + seed) * 3f;
        float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 0.9f + seed * 3f)), 40f) * 18f;
        if (earL) earL.localRotation = earL0 * Quaternion.Euler(-earDrag - idle, 0f, -twitch * 0.3f);
        if (earR) earR.localRotation = earR0 * Quaternion.Euler(-earDrag + idle * 0.6f - twitch, 0f, 0f);

        if (spinner) spinner.Rotate(spinAxis, (air ? 420f : 35f) * dt, Space.Self);
        if (groundRing) groundRing.Rotate(Vector3.up, ringSpin * dt, Space.Self);

        blinkT -= dt;
        float eyeY = 1f;
        if (blinkT < 0f) { eyeY = 0.15f; if (blinkT < -0.09f) blinkT = 2.2f + Random.value * 2.5f; }
        if (eyes != null) foreach (var e in eyes) if (e) e.localScale = new Vector3(eyeScale0.x, eyeScale0.y * eyeY, eyeScale0.z);
    }
}
