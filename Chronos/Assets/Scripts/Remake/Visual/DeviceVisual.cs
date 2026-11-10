using UnityEngine;

// Visual state for one original device. Reads game state only (Button.isPressed,
// Lever.canToggleDirection, target.activeSelf); never writes it.
//  - Plate: the pad glows while pressed.
//  - Plate/lever targets: while a controlled tile is absent, a faint outline marks its spot;
//    controlled walls are drawn as a translucent barrier in the device colour.
//  - Lever: an arrow on the lever tile always points the way the lever can be pushed next.
public class DeviceVisual : MonoBehaviour
{
    public Button button;
    public Lever lever;
    public Renderer pad;                 // plate pad (emission driven)
    public GameObject[] targets;         // original target objects
    public GameObject[] ghosts;          // outline shown while the matching target is inactive
    public Transform arrow;              // lever push arrow
    public Transform handle;             // lever handle visual (tilted from the original hinge state)
    public Color color = Color.white;

    MaterialPropertyBlock mpb;
    float glow;

    void Awake() { mpb = new MaterialPropertyBlock(); }

    void LateUpdate()
    {
        if (targets != null && ghosts != null)
            for (int i = 0; i < targets.Length && i < ghosts.Length; i++)
                if (ghosts[i] && targets[i]) ghosts[i].SetActive(!targets[i].activeSelf);

        if (button && pad)
        {
            glow = Mathf.MoveTowards(glow, button.isPressed ? 1f : 0f, Time.deltaTime * 6f);
            pad.GetPropertyBlock(mpb);
            mpb.SetColor("_Emission", color * Mathf.Lerp(0.08f, 0.9f, glow));
            pad.SetPropertyBlock(mpb);
        }

        if (lever && handle)
        {
            // original hinge sits at 50 deg (back) or 130 deg (forward); show it as -40 / +40 from vertical
            float tilt = lever.transform.GetChild(1).localEulerAngles.x - 90f;
            handle.rotation = Quaternion.Slerp(handle.rotation, lever.transform.rotation * Quaternion.Euler(tilt, 0f, 0f), 1f - Mathf.Exp(-Time.deltaTime * 18f));
        }
        if (lever && arrow)
        {
            Vector3 d = lever.canToggleDirection; d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
            {
                d.Normalize();
                // sits on the side you push from, pointing into the lever
                float floorY = arrow.parent ? arrow.parent.position.y : 0f;   // RemakeArt root moves with the level offset
                arrow.position = lever.transform.position - d * 0.62f + Vector3.up * (floorY + 0.012f - lever.transform.position.y);
                arrow.rotation = Quaternion.LookRotation(-d, Vector3.up); // arrow mesh tip points along local -Z
            }
        }
    }
}
