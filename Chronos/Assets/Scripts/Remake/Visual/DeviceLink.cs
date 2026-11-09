using UnityEngine;

// Makes cause -> effect readable on screen: the pressure plate, the energy conduit that
// arcs across the void, and the bridge tile it summons all share the device colour.
// When the bridge is absent a dashed "ghost" slab marks where it will appear.
// Read-only with respect to game state (observes Button.isPressed / target.activeSelf).
public class DeviceLink : MonoBehaviour
{
    public Button button;
    public GameObject target;          // the original TileButton object toggled by the plate
    public GameObject ghost;           // shown while the target is hidden
    public Renderer plateGlyph;        // emissive ring on the plate
    public Renderer[] dots;            // conduit beads along the arc
    public Renderer targetSeams;       // emissive seams on the summoned tile (child of target)
    public Color deviceColor = new(0.78f, 0.45f, 1f);
    public float flowSpeed = 1.6f;

    MaterialPropertyBlock mpb;
    float on;

    void Awake() { mpb = new MaterialPropertyBlock(); }

    void Update()
    {
        bool pressed = button != null && button.isPressed;
        on = Mathf.MoveTowards(on, pressed ? 1f : 0f, Time.deltaTime * 4f);
        if (ghost && target) ghost.SetActive(!target.activeSelf);

        // capped so the device colour stays violet instead of clipping to white (no HDR/bloom here)
        SetEmission(plateGlyph, deviceColor * Mathf.Lerp(0.7f, 1.25f, on));
        SetEmission(targetSeams, deviceColor * (1.05f + 0.2f * Mathf.Sin(Time.time * 4f)));

        if (dots != null)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                float u = (float)i / Mathf.Max(1, dots.Length - 1);
                float wave = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((Time.time * flowSpeed - u * 2.2f) * Mathf.PI * 2f), 3f);
                float k = Mathf.Lerp(0.22f, 0.5f + 0.75f * wave, on);
                mpb.Clear();
                mpb.SetColor("_Color", deviceColor * k);
                dots[i].SetPropertyBlock(mpb);
                dots[i].transform.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.22f + 0.1f * wave, on);
            }
        }
    }

    void SetEmission(Renderer r, Color c)
    {
        if (!r) return;
        mpb.Clear();
        r.GetPropertyBlock(mpb);
        mpb.SetColor("_EmissionColor", c);
        r.SetPropertyBlock(mpb);
    }
}
