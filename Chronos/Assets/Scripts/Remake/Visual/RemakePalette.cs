using System.Collections.Generic;
using UnityEngine;

// World styles. Each borrows only the colour/contrast STRUCTURE of a reference (no assets, no look):
//  S1  Monument Valley structure : flat pastel planes, form separated by face value, pale gradient sky
//  S2  Lara Croft GO structure   : light floor vs dark edges/walls, strong shadows, dark backdrop
//  S3  Captain Toad structure    : warm vivid toy diorama, saturated devices, bright sky
// Rules every style must meet (checked in the review sheet):
//  floor/wall luminance contrast >= 3:1, saturation reserved for devices + characters,
//  the phantom (heat haze from the past) must contrast with the floor.
public static class RemakePalette
{
    public class P
    {
        public string name;
        public Color tile, pillar, wall, dark, plate, lever, start, goal, shade, haze, marker;
        public string floorTex, wallTex, pillarTex;   // material detail (Assets/Remake/Textures/<name>.png)
        // abandoned-lab dressing (null/empty = none)
        public string overlayTex, overlayOn = "";      // moss / rust mask, applied to the listed materials
        public Color overlayColor; public float overlayUp;
        public string setTex; public Color set, strip, key, clockCol; public float keyI;
        public bool lab, vines, shafts, clock;
        // puzzle props (crates, core crate, moving blocks, laser walls, emitters)
        public Color crate = new(0.6f, 0.6f, 0.6f), trim = new(0.15f, 0.15f, 0.15f), mark = new(0.1f, 0.1f, 0.1f),
                     core = new(0.5f, 0.9f, 1f), beam = new(1f, 0.3f, 0.23f), panel = new(0.4f, 0.4f, 0.4f);
        public Color skyTop, skyHorizon, skyBottom, fog;
        public float fogDensity, sun, shadow;
        public Color sunColor, ambSky, ambEq, ambGround;
    }

    static Color H(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Structure taken from real screenshots (see captures/research.md):
    //  floor = one low-saturation family; walls = a darker value of the SAME family;
    //  void/background = the complementary hue; saturation only on devices + characters (complementary).
    // Tile combinations grounded in the world: floors and walls are mid-value and separated by
    // HUE and MATERIAL (not an extreme light/dark split); saturation stays on devices + characters.
    public static readonly Dictionary<string, P> All = new()
    {
        // T1 the sunken laboratory: rust-stained iron deck plates, patinated copper partitions, amber lamps
        ["T1"] = new P { name = "Sunken laboratory", tile = H("#7A746B"), pillar = H("#3E3933"), wall = H("#4A7468"), dark = H("#23201D"),
                         floorTex = "dt_iron", wallTex = "dt_patina", pillarTex = "dt_iron",
                         plate = H("#FFB347"), lever = H("#E8603F"), start = H("#D8D0C0"), goal = H("#FFE9B0"), shade = H("#6E6878"),
                         haze = H("#FFC46B"), marker = H("#FFFFFF"),
                         skyTop = H("#2A231D"), skyHorizon = H("#2A231D"), skyBottom = H("#110E0C"), fog = H("#1A1512"), fogDensity = 0.013f,
                         sun = 1.1f, shadow = 0.65f, sunColor = H("#FFEBD2"), ambSky = H("#7D7466"), ambEq = H("#655E55"), ambGround = H("#2E2925") },
        // T2 bones of the sunken land: weathered sandstone floor, terracotta walls, turquoise relics
        ["T2"] = new P { name = "Bones of the land", tile = H("#A69377"), pillar = H("#5E4A3A"), wall = H("#9A5A42"), dark = H("#2B201A"),
                         floorTex = "dt_sandstone", wallTex = "dt_clay", pillarTex = "dt_sandstone",
                         plate = H("#36B9A6"), lever = H("#6FC6E8"), start = H("#E6D8BE"), goal = H("#FFF0C8"), shade = H("#7A6578"),
                         haze = H("#FFB45A"), marker = H("#FFFFFF"),
                         skyTop = H("#3A2A30"), skyHorizon = H("#3A2A30"), skyBottom = H("#140E12"), fog = H("#1E1519"), fogDensity = 0.013f,
                         sun = 1.1f, shadow = 0.6f, sunColor = H("#FFE6CC"), ambSky = H("#8A7A80"), ambEq = H("#776A60"), ambGround = H("#33282A") },
        // T3 the master's study: aged concrete floor, tarnished brass walls, red signal lamps
        ["T3"] = new P { name = "Master's study", tile = H("#8A877F"), pillar = H("#4A4843"), wall = H("#94733F"), dark = H("#22211E"),
                         floorTex = "dt_concrete", wallTex = "dt_brass", pillarTex = "dt_concrete",
                         plate = H("#E8553D"), lever = H("#4FC3D9"), start = H("#D9D6CE"), goal = H("#FFF1C4"), shade = H("#666C80"),
                         haze = H("#FFC46B"), marker = H("#FFFFFF"),
                         skyTop = H("#26303B"), skyHorizon = H("#26303B"), skyBottom = H("#0E1217"), fog = H("#151A20"), fogDensity = 0.013f,
                         sun = 1.1f, shadow = 0.6f, sunColor = H("#FFF0DE"), ambSky = H("#7A8290"), ambEq = H("#666A6E"), ambGround = H("#2A2C30") },
        // ---- abandoned research lab (references: captures/review10/lab_moodboard.png)
        // LA Timelie: dark teal room, one cold key light, glass-teal partitions, only devices + warning lamps carry colour
        ["LA"] = new P { crate = H("#8FA3A6"), trim = H("#22313A"), mark = H("#1A262C"), core = H("#7FE6FF"), beam = H("#FF4A3A"), panel = H("#4A616A"),
                         name = "Cold dead lab", lab = true, clock = true,
                         tile = H("#5B808A"), pillar = H("#22363E"), wall = H("#8CC4CC"), dark = H("#0E161A"),
                         floorTex = "dt_labfloor", wallTex = "dt_labwall", pillarTex = "dt_iron",
                         plate = H("#FFB347"), lever = H("#FF5A4E"), start = H("#CFE3E8"), goal = H("#FFF2C4"), shade = H("#34405E"),
                         haze = H("#FFD08A"), marker = H("#FFFFFF"),
                         skyTop = H("#0A1115"), skyHorizon = H("#0A1115"), skyBottom = H("#05090B"), fog = H("#0C161B"), fogDensity = 0.016f,
                         sun = 0.8f, shadow = 0.8f, sunColor = H("#BFE3F0"), ambSky = H("#5E8492"), ambEq = H("#3E5A64"), ambGround = H("#14222A"),
                         set = H("#1C2B32"), setTex = "dt_labwall", strip = H("#7FE6FF"), key = H("#E6F7FF"), keyI = 3.0f, clockCol = H("#3B5A64") },
        // LB Portal 2 overgrown: grey concrete panels taken by moss, warm sun through a broken roof, dust
        ["LB"] = new P { crate = H("#C4BCA6"), trim = H("#43443D"), mark = H("#E0782E"), core = H("#8FE07A"), beam = H("#FF5533"), panel = H("#8C8C82"),
                         name = "Overgrown lab", lab = true, vines = true, shafts = true,
                         tile = H("#7A7A72"), pillar = H("#45453F"), wall = H("#A2A298"), dark = H("#2A2A26"),
                         floorTex = "dt_concrete", wallTex = "dt_labwall", pillarTex = "dt_concrete",
                         overlayTex = "ov_moss", overlayOn = "Tile,Pillar,Wall,Set", overlayColor = new Color(0.33f, 0.45f, 0.2f, 0.6f), overlayUp = 0.85f,
                         plate = H("#3FA9F5"), lever = H("#FF8A2A"), start = H("#E4E2D8"), goal = H("#FFF4D0"), shade = H("#56606E"),
                         haze = H("#FFC46B"), marker = H("#FFFFFF"),
                         skyTop = H("#3A4238"), skyHorizon = H("#3A4238"), skyBottom = H("#1A1E18"), fog = H("#2C3329"), fogDensity = 0.009f,
                         sun = 1.35f, shadow = 0.85f, sunColor = H("#FFE0A8"), ambSky = H("#8A9682"), ambEq = H("#5C6456"), ambGround = H("#2A2E26"),
                         set = H("#5A5C54"), setTex = "dt_labwall", strip = H("#7E8076"), key = H("#FFD99A"), keyI = 1.6f },
        // LC Machinarium: rusted iron, patina and faded checker tiles, warm bulbs, a brass clock
        ["LC"] = new P { crate = H("#9A6E44"), trim = H("#3A2A1E"), mark = H("#D9B46A"), core = H("#FFC878"), beam = H("#FF5A3A"), panel = H("#7A5A40"),
                         name = "Rusted clockwork lab", lab = true, clock = true,
                         tile = H("#9A927A"), pillar = H("#4E3E2E"), wall = H("#8E5B3A"), dark = H("#2C2018"),
                         floorTex = "dt_checker", wallTex = "dt_iron", pillarTex = "dt_iron",
                         overlayTex = "ov_rust", overlayOn = "Wall,Pillar,Set", overlayColor = new Color(0.45f, 0.22f, 0.11f, 0.7f), overlayUp = 0f,
                         plate = H("#4CC3B0"), lever = H("#E8553D"), start = H("#EDE3C8"), goal = H("#FFF0C0"), shade = H("#6E5F70"),
                         haze = H("#FFB45A"), marker = H("#FFFFFF"),
                         skyTop = H("#4A4436"), skyHorizon = H("#4A4436"), skyBottom = H("#221E16"), fog = H("#4A4232"), fogDensity = 0.02f,
                         sun = 1.15f, shadow = 0.75f, sunColor = H("#FFE6C0"), ambSky = H("#A0947A"), ambEq = H("#7A6E58"), ambGround = H("#3A3226"),
                         set = H("#685A4A"), setTex = "dt_clay", strip = H("#FFC878"), key = H("#FFD9A0"), keyI = 1.4f, clockCol = H("#B08A4A") },
        // LD A + C: the dark cold room of A, with C's rusted brass machinery and clock
        ["LD"] = new P { crate = H("#6F7E7B"), trim = H("#262220"), mark = H("#B08A4A"), core = H("#FFC878"), beam = H("#FF4A3A"), panel = H("#3E4C4C"),
                         name = "Dark lab, rusted brass", lab = true, clock = true,
                         tile = H("#5A7476"), pillar = H("#283A3C"), wall = H("#B08A50"), dark = H("#101819"),
                         floorTex = "dt_labfloor", wallTex = "dt_brass", pillarTex = "dt_iron",
                         overlayTex = "ov_rust", overlayOn = "Wall,Pillar,Set", overlayColor = new Color(0.4f, 0.2f, 0.1f, 0.55f), overlayUp = 0f,
                         plate = H("#FFB347"), lever = H("#FF5A4E"), start = H("#D6E4E2"), goal = H("#FFF2C4"), shade = H("#34405E"),
                         haze = H("#FFD08A"), marker = H("#FFFFFF"),
                         skyTop = H("#0B1213"), skyHorizon = H("#0B1213"), skyBottom = H("#050909"), fog = H("#0E1718"), fogDensity = 0.016f,
                         sun = 0.8f, shadow = 0.8f, sunColor = H("#CFE6EE"), ambSky = H("#5C7E84"), ambEq = H("#3C5458"), ambGround = H("#142022"),
                         set = H("#22302F"), setTex = "dt_labwall", strip = H("#FFC878"), key = H("#E6F7FF"), keyI = 2.4f, clockCol = H("#B08A4A") },
    };

    public static float Luma(Color c) { var l = c.linear; return 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b; }
    public static float Contrast(Color a, Color b) { float x = Luma(a) + 0.05f, y = Luma(b) + 0.05f; return Mathf.Max(x, y) / Mathf.Min(x, y); }

    public static bool Colours(P p, string mat, out Color baseCol, out Color emission)
    {
        emission = Color.black; baseCol = Color.white;
        switch (mat)
        {
            case "Tile": baseCol = p.tile; return true;
            case "Pillar": baseCol = p.pillar; return true;
            case "Wall": baseCol = p.wall; return true;
            case "Dark": baseCol = p.dark; return true;
            case "PlatePad": baseCol = p.plate; emission = p.plate * 0.12f; return true;
            case "PlateRim": baseCol = p.plate; emission = p.plate * 0.4f; return true;
            case "LeverRim": baseCol = p.lever; emission = p.lever * 0.4f; return true;
            case "LeverHandle": baseCol = p.lever; emission = p.lever * 0.12f; return true;
            case "Start": baseCol = p.start; emission = p.start * 0.15f; return true;
            case "Goal": baseCol = p.goal; emission = p.goal * 0.8f; return true;
            case "GhostPlate": baseCol = new Color(p.plate.r, p.plate.g, p.plate.b, 0.42f); return true;
            case "GhostLever": baseCol = new Color(p.lever.r, p.lever.g, p.lever.b, 0.42f); return true;
            case "Barrier": baseCol = p.plate; return true;
            case "LeverArrow": baseCol = new Color(p.lever.r, p.lever.g, p.lever.b, 0.95f); return true;
            case "Afterimage": baseCol = new Color(p.haze.r, p.haze.g, p.haze.b, 0.42f); return true;
            case "Crate": baseCol = p.crate; return true;
            case "CrateTrim": baseCol = p.trim; return true;
            case "CrateMark": baseCol = p.mark; emission = Luma(p.mark) > 0.25f ? p.mark * 0.1f : Color.black; return true;
            case "Core": baseCol = p.core; emission = p.core * 0.9f; return true;
            case "Piston": baseCol = Color.Lerp(p.trim, p.dark, 0.3f); return true;
            case "Hazard": baseCol = Color.Lerp(p.mark, p.crate, Luma(p.mark) > 0.25f ? 0f : 0.85f); return true;
            case "Panel": baseCol = p.panel; return true;
            case "Lens": baseCol = p.beam; emission = p.beam * 1.4f; return true;
            case "Beam": baseCol = new Color(p.beam.r, p.beam.g, p.beam.b, 1f); return true;
            case "BeamGlow": baseCol = new Color(p.beam.r, p.beam.g, p.beam.b, 0.35f); return true;
            case "AfterimageEcho": baseCol = new Color(p.haze.r, p.haze.g, p.haze.b, 0.5f); return true;
        }
        return false;
    }

    public static Texture2D LoadTex(string n)
    {
        // builder copies every detail texture into Resources/RemakeTex so the player build has them too
        return string.IsNullOrEmpty(n) ? null : Resources.Load<Texture2D>("RemakeTex/" + n);
    }

    public static void SetOverlay(Material m, P p, string matName)
    {
        if (!m.HasProperty("_OverlayColor")) return;
        bool on = !string.IsNullOrEmpty(p.overlayTex) && System.Array.IndexOf(p.overlayOn.Split(','), matName) >= 0;
        var oc = p.overlayColor; if (matName == "Tile") oc.a *= 0.5f;   // keep the walkable floor readable
        m.SetColor("_OverlayColor", on ? oc : Color.clear);
        if (on) { m.SetTexture("_OverlayTex", LoadTex(p.overlayTex)); m.SetFloat("_OverlayUp", p.overlayUp); m.SetFloat("_OverlayScale", 0.22f); }
    }

    public static void ApplyLighting(P p)
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            if (l.type == LightType.Directional) { l.color = p.sunColor; l.intensity = p.sun; l.shadowStrength = p.shadow; }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambSky; RenderSettings.ambientEquatorColor = p.ambEq; RenderSettings.ambientGroundColor = p.ambGround;
        RenderSettings.fog = true; RenderSettings.fogColor = p.fog; RenderSettings.fogDensity = p.fogDensity;
    }

    public static void Apply(string id) => Apply(id, default);

    // focus: the stage scene the lab set is built around (others still loaded during a level transition are ignored)
    public static void Apply(string id, UnityEngine.SceneManagement.Scene focus)
    {
        if (!All.TryGetValue(id, out var p)) return;
        var cache = new Dictionary<Material, Material>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            var mats = r.sharedMaterials; bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (!m) continue;
                string n = m.name.Replace(" (Instance)", "").Replace(" (Palette)", "");
                if (!Colours(p, n, out var c, out var e)) continue;
                if (!cache.TryGetValue(m, out var inst))
                {
                    inst = new Material(m) { name = n + " (Palette)" };
                    if (inst.HasProperty("_Color")) inst.SetColor("_Color", c);
                    if (inst.HasProperty("_Emission")) inst.SetColor("_Emission", e);
                    if (inst.HasProperty("_ShadeColor")) inst.SetColor("_ShadeColor", p.shade);
                    string dt = n == "Tile" ? p.floorTex : n == "Wall" ? p.wallTex : n == "Pillar" ? p.pillarTex : null;
                    if (dt != null && inst.HasProperty("_DetailTex"))
                    {
                        inst.SetTexture("_DetailTex", LoadTex(dt));
                        inst.SetFloat("_DetailStrength", 0.85f); inst.SetFloat("_DetailScale", 0.5f);
                    }
                    SetOverlay(inst, p, n);
                    if (inst.HasProperty("_SinkFade")) inst.SetFloat("_SinkFade", p.lab && n == "Pillar" ? 0.16f : 0f);
                    cache[m] = inst;
                }
                mats[i] = inst; changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
        foreach (var t in Object.FindObjectsByType<AfterimageTrail>(FindObjectsInactive.Include))
            if (t.echoMaterial) { var em = new Material(t.echoMaterial); em.SetColor("_Color", new Color(p.haze.r, p.haze.g, p.haze.b, 0.5f)); t.echoMaterial = em; }
        foreach (var dv in Object.FindObjectsByType<DeviceVisual>(FindObjectsInactive.Include)) dv.color = dv.lever ? p.lever : p.plate;
        foreach (var hud in Object.FindObjectsByType<RemakeHUD>(FindObjectsInactive.Include)) { hud.you = p.marker; hud.past = p.haze; }
        var shafts = GameObject.Find("LightShafts"); if (shafts) shafts.SetActive(p.shafts);
        var ring = GameObject.Find("FarRing"); if (ring) ring.SetActive(!p.lab);
        LabSet.Build(p, focus);
        if (RenderSettings.skybox)
        {
            var sky = new Material(RenderSettings.skybox);
            sky.SetColor("_Zenith", p.skyTop); sky.SetColor("_Horizon", p.skyHorizon);
            sky.SetColor("_Mid", Color.Lerp(p.skyHorizon, p.skyBottom, 0.5f)); sky.SetColor("_Nadir", p.skyBottom);
            sky.SetFloat("_StarDensity", p.lab || Luma(p.skyBottom) > 0.2f ? 0f : 0.25f);
            RenderSettings.skybox = sky;
        }
        ApplyLighting(p);
    }
}
