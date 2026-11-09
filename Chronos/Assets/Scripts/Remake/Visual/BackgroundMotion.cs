using UnityEngine;

// Slow ambient motion for the abyss: the giant clock below turns, islands bob.
// While the player is in rewind mode the clock visibly runs backwards.
public class BackgroundMotion : MonoBehaviour
{
    [System.Serializable] public struct Spin { public Transform t; public Vector3 axis; public float degPerSec; }
    [System.Serializable] public struct Bob { public Transform t; public float amp; public float speed; }
    public Spin[] spins;
    public Bob[] bobs;

    float dir = 1f;
    Vector3[] bobBase;

    void Start()
    {
        bobBase = new Vector3[bobs?.Length ?? 0];
        for (int i = 0; i < bobBase.Length; i++) if (bobs[i].t) bobBase[i] = bobs[i].t.localPosition;
    }

    void Update()
    {
        bool rewinding = PlayerController.playerController != null && PlayerController.playerController.isTimeRewinding;
        dir = Mathf.MoveTowards(dir, rewinding ? -4f : 1f, Time.deltaTime * 6f);
        if (spins != null) foreach (var s in spins) if (s.t) s.t.Rotate(s.axis, s.degPerSec * dir * Time.deltaTime, Space.Self);
        for (int i = 0; i < bobBase.Length; i++)
            if (bobs[i].t) bobs[i].t.localPosition = bobBase[i] + Vector3.up * Mathf.Sin(Time.time * bobs[i].speed + i * 1.7f) * bobs[i].amp;
    }
}
