using UnityEngine;

// Slow ambient motion for background pieces. The ring far below turns backwards while rewinding.
public class Ambient : MonoBehaviour
{
    public Transform[] spin;
    public float degPerSec = 2f;
    public Transform[] bob;
    public float bobAmp = 0.3f;

    Vector3[] bobBase;
    float dir = 1f;

    void Start()
    {
        bobBase = new Vector3[bob?.Length ?? 0];
        for (int i = 0; i < bobBase.Length; i++) bobBase[i] = bob[i].localPosition;
    }

    void Update()
    {
        bool rw = PlayerController.playerController && PlayerController.playerController.isTimeRewinding;
        dir = Mathf.MoveTowards(dir, rw ? -6f : 1f, Time.deltaTime * 8f);
        if (spin != null) foreach (var t in spin) if (t) t.Rotate(Vector3.up, degPerSec * dir * Time.deltaTime, Space.Self);
        for (int i = 0; i < bobBase.Length; i++)
            if (bob[i]) bob[i].localPosition = bobBase[i] + Vector3.up * Mathf.Sin(Time.time * 0.25f + i * 1.9f) * bobAmp;
    }
}
