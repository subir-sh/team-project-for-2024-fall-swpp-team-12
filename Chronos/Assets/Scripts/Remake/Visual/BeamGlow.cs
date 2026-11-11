using UnityEngine;

// Soft halo around a laser beam: follows the original LineRenderer that Laser.cs updates every frame.
// Lives under the beam's StartPoint, so it switches off together with the laser.
[RequireComponent(typeof(LineRenderer))]
public class BeamGlow : MonoBehaviour
{
    public LineRenderer src;
    LineRenderer lr;

    void Awake() { lr = GetComponent<LineRenderer>(); }

    void LateUpdate()
    {
        if (!src) return;
        lr.enabled = src.enabled;
        src.startWidth = src.endWidth = 0.14f;   // Laser.cs sets 0.1 once at Start; slightly thicker core
        lr.startWidth = lr.endWidth = 0.6f;
        lr.positionCount = 2;
        lr.SetPosition(0, src.GetPosition(0));
        lr.SetPosition(1, src.GetPosition(1));
    }
}
