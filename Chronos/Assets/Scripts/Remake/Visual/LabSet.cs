using System.Collections.Generic;
using UnityEngine;

// Runtime set dressing that turns the void around the floating puzzle into an abandoned research lab:
// a lab floor under the tile pillars, two tall back walls with panel seams, light strips, pipes and dead
// machines, an optional big clock in the far corner, overgrowth and light shafts. Everything is built from
// primitives, never collides and is rebuilt whenever a palette is applied.
public static class LabSet
{
    static Material Toon(Color c, string tex = null, float texScale = 0.25f, Color? emission = null, RemakePalette.P p = null)
    {
        var m = new Material(Shader.Find("Remake/Toon")) { name = "LabSet" };
        m.SetColor("_Color", c);
        if (emission.HasValue) m.SetColor("_Emission", emission.Value);
        if (p != null) m.SetColor("_ShadeColor", p.shade);
        m.SetColor("_RimColor", new Color(1, 1, 1, 0.08f));
        m.SetFloat("_Spec", 0.05f);
        if (!string.IsNullOrEmpty(tex)) { m.SetTexture("_DetailTex", RemakePalette.LoadTex(tex)); m.SetFloat("_DetailStrength", 0.8f); m.SetFloat("_DetailScale", texScale); }
        if (p != null) RemakePalette.SetOverlay(m, p, "Set");
        return m;
    }

    static GameObject Box(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null, bool shadows = true)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
        g.transform.localScale = scale;
        var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    public static void Build(RemakePalette.P p, UnityEngine.SceneManagement.Scene focus = default)
    {
        var old = GameObject.Find("LabSet"); if (old) Object.DestroyImmediate(old);
        if (!p.lab) return;
        var root = new GameObject("LabSet").transform;

        // bounds of the puzzle (tiles + their pillars)
        Bounds b = default; bool any = false;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (focus.IsValid() && r.gameObject.scene != focus) continue;
            var mat = r.sharedMaterial; if (!mat || !(mat.name.StartsWith("Tile") || mat.name.StartsWith("Pillar"))) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;
        var cam = Camera.main ? Camera.main : Object.FindFirstObjectByType<Camera>();
        Vector3 f = cam ? cam.transform.forward : new Vector3(1, -1, 1); f.y = 0; f.Normalize();
        float top = b.max.y, floorY = b.min.y - 0.4f;
        var rnd = new System.Random(4242);
        float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);

        var setM = Toon(p.set, p.setTex, 0.25f, null, p);
        var floorM = Toon(p.set * 0.75f, "dt_labfloor", 0.25f, null, p);
        var darkM = Toon(Color.Lerp(p.dark, p.set, 0.5f), "dt_iron", 0.5f, null, p);
        var stripM = Toon(p.strip, null, 1, p.strip * 1.3f);
        var screenM = Toon(p.strip * 0.5f, null, 1, p.strip * 0.55f);
        var warnM = Toon(p.lever, null, 1, p.lever * 1.6f);
        var pipeM = Toon(p.clock ? Color.Lerp(p.clockCol, p.dark, 0.35f) : Color.Lerp(p.dark, p.set, 0.7f), "dt_iron", 0.5f, null, p);

        const float gap = 7f, H = 46f;
        Vector3 c = b.center;
        // lab floor under the pillars
        Box(root, PrimitiveType.Cube, new Vector3(c.x, floorY - 0.5f, c.z), new Vector3(b.size.x + 60, 1, b.size.z + 60), floorM);

        // two back walls on the far side of the camera
        float sx = Mathf.Sign(f.x), sz = Mathf.Sign(f.z);
        float wx = (sx > 0 ? b.max.x : b.min.x) + sx * gap, wz = (sz > 0 ? b.max.z : b.min.z) + sz * gap;
        float lenX = b.size.z + 2 * gap + 30, lenZ = b.size.x + 2 * gap + 30;
        Box(root, PrimitiveType.Cube, new Vector3(wx + sx * 0.5f, floorY + H / 2, c.z - sz * 15), new Vector3(1, H, lenX), setM);
        Box(root, PrimitiveType.Cube, new Vector3(c.x - sx * 15, floorY + H / 2, wz + sz * 0.5f), new Vector3(lenZ, H, 1), setM);

        // dressing along each wall: light strips, pipes, machines with dead screens, warning lamps
        void Dress(Vector3 origin, Vector3 along, Vector3 inward, float len)
        {
            Quaternion face = Quaternion.LookRotation(inward);
            for (float t = 4; t < len - 4; t += 7.5f)
            {
                var at = origin + along * t;
                if (rnd.NextDouble() > 0.25)   // some strips are dead
                    Box(root, PrimitiveType.Cube, at + inward * 0.15f + Vector3.up * (top + R(1.5f, 3.5f) - floorY), new Vector3(0.22f, R(3.5f, 6f), 0.22f), stripM, face, false)
                        .transform.position += Vector3.zero;
                // machine at the floor
                float w = R(2.2f, 4.2f), h = R(2.5f, 6.5f), d = R(1.4f, 2.6f);
                var mpos = at + along * 3.2f + inward * (d / 2 + 0.6f) + Vector3.up * h / 2;
                var mach = Box(root, PrimitiveType.Cube, mpos, Vector3.one, darkM, face);
                mach.transform.localScale = new Vector3(w, h, d);
                if (rnd.NextDouble() > 0.4)
                    Box(root, PrimitiveType.Cube, mpos + inward * (d / 2 + 0.02f) + Vector3.up * (h * 0.2f), new Vector3(w * 0.55f, h * 0.22f, 0.05f), screenM, face, false);
                if (rnd.NextDouble() > 0.7)
                    Box(root, PrimitiveType.Sphere, mpos + Vector3.up * (h / 2 + 0.2f), Vector3.one * 0.35f, warnM, null, false);
            }
            foreach (float hy in new[] { top - floorY - 2.5f, top - floorY + 5.5f, top - floorY + 6.3f })
            {
                var mid = origin + along * (len / 2) + inward * 0.6f + Vector3.up * hy;
                Box(root, PrimitiveType.Cylinder, mid, new Vector3(0.6f, len / 2, 0.6f), pipeM, Quaternion.FromToRotation(Vector3.up, along));
            }
        }
        Vector3 floor0(float x, float z) => new Vector3(x, floorY, z);
        Dress(floor0(wx, c.z - sz * 15 - sz * lenX / 2), new Vector3(0, 0, sz), new Vector3(-sx, 0, 0), lenX);
        Dress(floor0(c.x - sx * 15 - sx * lenZ / 2, wz), new Vector3(sx, 0, 0), new Vector3(0, 0, -sz), lenZ);

        // big clock hanging in the far corner, facing the camera
        if (p.clock)
        {
            var clock = new GameObject("LabClock").transform; clock.SetParent(root, false);
            // hang it on whichever back wall shows in the upper-left of the game view
            Vector3 inward = new Vector3(-sx, 0, 0), hit = new Vector3(wx, top + 4, c.z);
            if (cam)
            {
                var ray = cam.ViewportPointToRay(new Vector3(0.16f, 0.64f, 0));
                float best = float.MaxValue;
                foreach (var (pl, nIn) in new[] { (new Plane(new Vector3(-sx, 0, 0), new Vector3(wx, 0, 0)), new Vector3(-sx, 0, 0)),
                                                   (new Plane(new Vector3(0, 0, -sz), new Vector3(0, 0, wz)), new Vector3(0, 0, -sz)) })
                    if (pl.Raycast(ray, out float t) && t > 0 && t < best) { best = t; hit = ray.GetPoint(t); inward = nIn; }
            }
            clock.position = hit + inward * 0.9f;
            clock.rotation = Quaternion.LookRotation(-inward);
            var rimM = Toon(p.clockCol, "dt_brass", 1f, null, p);
            var faceM = Toon(Color.Lerp(p.set, p.dark, 0.4f), "dt_iron", 0.5f, null, p);
            var tickM = Toon(Color.Lerp(p.clockCol, Color.white, 0.25f), null, 1, p.clockCol * 0.25f);
            Quaternion disc = Quaternion.Euler(90, 0, 0);
            float rad = 4.2f;
            GameObject Local(PrimitiveType t, Vector3 lp, Vector3 s, Material m, Quaternion lr)
            {
                var g = Box(clock, t, Vector3.zero, s, m); g.transform.localPosition = lp; g.transform.localRotation = lr; return g;
            }
            Local(PrimitiveType.Cylinder, new Vector3(0, 0, 0.3f), new Vector3(rad * 2.2f, 0.25f, rad * 2.2f), rimM, disc);
            Local(PrimitiveType.Cylinder, new Vector3(0, 0, 0f), new Vector3(rad * 2f, 0.3f, rad * 2f), faceM, disc);
            for (int i = 0; i < 12; i++)
            {
                var q = Quaternion.Euler(0, 0, -i * 30);
                bool major = i % 3 == 0;
                var g = Local(PrimitiveType.Cube, q * new Vector3(0, rad * 0.82f, -0.25f), new Vector3(major ? 0.5f : 0.25f, major ? 1.4f : 0.9f, 0.15f), tickM, q);
            }
            // hands on pivots so they can turn
            Transform Hand(float len, float w, float startDeg)
            {
                var pivot = new GameObject("Hand").transform; pivot.SetParent(clock, false); pivot.localPosition = new Vector3(0, 0, -0.35f);
                pivot.localRotation = Quaternion.Euler(0, 0, -startDeg);
                var g = Box(pivot, PrimitiveType.Cube, Vector3.zero, new Vector3(w, len, 0.12f), tickM); g.transform.localPosition = new Vector3(0, len * 0.4f, 0);
                return pivot;
            }
            var hour = Hand(rad * 0.5f, 0.45f, 300); var minute = Hand(rad * 0.75f, 0.3f, 60);
            Local(PrimitiveType.Cylinder, new Vector3(0, 0, -0.45f), new Vector3(0.9f, 0.15f, 0.9f), rimM, disc);
            // a gear peeking behind the rim
            var gear = Local(PrimitiveType.Cylinder, new Vector3(rad * 0.95f, -rad * 0.75f, 0.6f), new Vector3(rad * 0.9f, 0.2f, rad * 0.9f), rimM, disc);
            for (int i = 0; i < 12; i++)
            {
                var q = Quaternion.Euler(0, 0, i * 30);
                var tooth = Box(clock, PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 0.7f, 0.2f), rimM);
                tooth.transform.localPosition = new Vector3(rad * 0.95f, -rad * 0.75f, 0.6f) + q * new Vector3(0, rad * 0.47f, 0);
                tooth.transform.localRotation = q;
            }
            var spin = clock.gameObject.AddComponent<LabClockSpin>(); spin.hands = new[] { minute, hour }; spin.degPerSec = new[] { 6f, 0.5f };
        }

        // overgrowth: hanging vines + bushes on the floor and on machines
        if (p.vines)
        {
            var vineM = Toon(new Color(0.16f, 0.22f, 0.1f), null, 1, null, p);
            var leafM = Toon(new Color(0.2f, 0.3f, 0.12f), null, 1, null, p);
            var leafM2 = Toon(new Color(0.27f, 0.36f, 0.15f), null, 1, null, p);
            for (int i = 0; i < 70; i++)
            {
                float ang = R(0, Mathf.PI * 2), rr = Mathf.Max(b.extents.x, b.extents.z) + R(1.5f, 9f);
                var at = new Vector3(c.x + Mathf.Cos(ang) * rr, 0, c.z + Mathf.Sin(ang) * rr);
                if (Vector3.Dot(at - c, f) < -4) continue;   // keep the camera side clear
                float len = R(5, 18), y0 = top + 14;
                Box(root, PrimitiveType.Cube, new Vector3(at.x, y0 - len / 2, at.z), new Vector3(0.05f, len, 0.05f), vineM, Quaternion.Euler(0, R(0, 90), 0), false);
                for (float y = y0 - 0.5f; y > y0 - len; y -= R(0.35f, 0.8f))
                    Box(root, PrimitiveType.Cube, new Vector3(at.x + R(-0.12f, 0.12f), y, at.z + R(-0.12f, 0.12f)), new Vector3(R(0.14f, 0.26f), 0.03f, R(0.1f, 0.18f)),
                        rnd.NextDouble() > 0.5 ? leafM : leafM2, Quaternion.Euler(R(-40, 40), R(0, 360), R(-40, 40)), false);
            }
            for (int i = 0; i < 26; i++)   // low fern clumps on the lab floor
            {
                var at = new Vector3(R(b.min.x - 10, b.max.x + 10), floorY, R(b.min.z - 10, b.max.z + 10));
                int blades = 9 + rnd.Next(8); float s = R(0.8f, 1.8f);
                for (int k = 0; k < blades; k++)
                    Box(root, PrimitiveType.Cube, at + new Vector3(R(-0.3f, 0.3f), s * 0.35f, R(-0.3f, 0.3f)), new Vector3(0.18f * s, 0.04f, s),
                        rnd.NextDouble() > 0.5 ? leafM : leafM2, Quaternion.Euler(0, R(0, 360), 0) * Quaternion.Euler(R(-45, -20), 0, 0), true);
            }
        }

        // key light: one cone over the puzzle (Timelie-style pool of light)
        var keyGo = new GameObject("KeyLight"); keyGo.transform.SetParent(root, false);
        keyGo.transform.position = new Vector3(c.x, top + 22, c.z) - f * 4;
        keyGo.transform.rotation = Quaternion.LookRotation((new Vector3(c.x, top, c.z) - keyGo.transform.position).normalized);
        var key = keyGo.AddComponent<Light>(); key.type = LightType.Spot; key.color = p.key; key.intensity = p.keyI;
        key.range = 60; key.spotAngle = 62; key.innerSpotAngle = 30; key.shadows = LightShadows.Soft; key.shadowStrength = 0.7f;

        // light shafts through the broken roof
        if (p.shafts)
        {
            var sun = System.Array.Find(Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude), l => l.type == LightType.Directional);
            Vector3 axis = sun ? -sun.transform.forward : Vector3.up;
            var shaftM = new Material(Shader.Find("Remake/Unlit")) { name = "LabShaft" };
            shaftM.SetTexture("_MainTex", RemakePalette.LoadTex("shaft"));
            shaftM.SetColor("_Color", new Color(p.sunColor.r, p.sunColor.g, p.sunColor.b, 0.05f));
            shaftM.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); shaftM.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            shaftM.renderQueue = 3050;
            for (int i = 0; i < 5; i++)
            {
                var foot = new Vector3(R(b.min.x + 2, b.max.x - 2), top, R(b.min.z + 2, b.max.z - 2));
                float L = 40, w = R(2.5f, 5f);
                var mid = foot + axis * L / 2;
                Vector3 toCam = cam ? (cam.transform.position - mid) : -f;
                Vector3 normal = Vector3.ProjectOnPlane(toCam, axis).normalized;
                var q = Quaternion.LookRotation(-normal, axis);
                Box(root, PrimitiveType.Quad, mid, new Vector3(w, L, 1), shaftM, q, false);
            }
        }
    }
}

public class LabClockSpin : MonoBehaviour
{
    public Transform[] hands; public float[] degPerSec;
    void Update() { for (int i = 0; i < hands.Length; i++) if (hands[i]) hands[i].Rotate(0, 0, -degPerSec[i] * Time.deltaTime, Space.Self); }
}
