using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

// Which rabbit design is applied to the playable remake scene. Changed after the design review.
public static class RemakeArtConfig
{
    public const string ChosenRabbit = "A";
    public static readonly string[] Variants = { "A", "B", "C" };
}

// Generates the remake art kit + scenes from code (deterministic, re-runnable):
//   Unity.exe -batchmode -quit -projectPath <p> -executeMethod RemakeBuilder.BuildAll [-rabbit A|B|C]
// Original assets/scenes are never modified: the remake lives in Assets/Remake and new scenes
// (RemakeBase + R-001-3) that copy the originals and layer new visuals onto the original
// gameplay objects (colliders, scripts and their wiring are untouched).
public static class RemakeBuilder
{
    const string R = "Assets/Remake";
    static readonly Dictionary<string, Material> M = new();
    static readonly Dictionary<string, Mesh> Meshes = new();

    // palette
    static readonly Color Device = new(0.78f, 0.45f, 1f);
    static readonly Color Amber = new(1.0f, 0.66f, 0.26f);
    static readonly Color Cyan = new(0.36f, 0.9f, 1f);

    public static void BuildAll()
    {
        string rabbit = Arg("-rabbit") ?? RemakeArtConfig.ChosenRabbit;
        Folders();
        BuildTextures();
        BuildFont();
        BuildMaterials();
        BuildKit();
        foreach (var v in RemakeArtConfig.Variants) { BuildRabbit(v, false); BuildRabbit(v, true); }
        BuildBaseScene(rabbit);
        BuildLevelScene();
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Builder] done (rabbit={rabbit})");
    }

    // ===================================================================== folders / io
    static void Folders()
    {
        foreach (var f in new[] { "Textures", "Sprites", "Materials", "Meshes", "Prefabs", "Prefabs/Rabbits", "Scenes", "Fonts" })
            Directory.CreateDirectory($"{R}/{f}");
        AssetDatabase.Refresh();
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    static Mesh SaveMesh(string name, Mesh mesh)
    {
        string p = $"{R}/Meshes/{name}.asset";
        mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Meshes[name] = existing; return existing; }
        AssetDatabase.CreateAsset(mesh, p);
        Meshes[name] = mesh;
        return mesh;
    }

    static Material SaveMat(string name, Material m)
    {
        string p = $"{R}/Materials/{name}.mat";
        m.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (existing != null)
        {
            existing.shader = m.shader;
            existing.CopyPropertiesFromMaterial(m);
            existing.shaderKeywords = m.shaderKeywords;
            existing.renderQueue = m.renderQueue;
            existing.globalIlluminationFlags = m.globalIlluminationFlags;
            EditorUtility.SetDirty(existing);
            M[name] = existing; return existing;
        }
        AssetDatabase.CreateAsset(m, p);
        M[name] = m; return m;
    }

    // ===================================================================== textures
    static Sprite Spr(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{R}/Sprites/{n}.png");
    static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{R}/Textures/{n}.png");

    static void BuildTextures()
    {
        string S(string n) => $"{R}/Sprites/{n}.png";
        string T(string n) => $"{R}/Textures/{n}.png";

        // --- UI sprites
        TextureKit.Shape(S("chip"), 64, (x, y) => TextureKit.RoundRect(x, y, 32, 32, 31, 31, 14));
        TextureKit.ImportSprite(S("chip"), new Vector4(18, 18, 18, 18));
        TextureKit.Shape(S("chipOutline"), 64, (x, y) => Mathf.Abs(TextureKit.Circle(x, y, 32, 32, 28)) - 2.5f);
        TextureKit.ImportSprite(S("chipOutline"), Vector4.zero);
        TextureKit.Shape(S("pill"), 32, (x, y) => TextureKit.RoundRect(x, y, 16, 16, 15, 15, 15));
        TextureKit.ImportSprite(S("pill"), new Vector4(15, 15, 15, 15));
        TextureKit.Shape(S("keycap"), 64, (x, y) => TextureKit.RoundRect(x, y, 32, 32, 31, 31, 9));
        TextureKit.ImportSprite(S("keycap"), new Vector4(12, 12, 12, 12));
        TextureKit.Shape(S("softDot"), 64, (x, y) => TextureKit.Circle(x, y, 32, 32, 30));
        TextureKit.ImportSprite(S("softDot"), Vector4.zero);
        TextureKit.Shape(S("arrow"), 64, (x, y) => TextureKit.Union(
            TextureKit.RoundRect(x, y, 24, 32, 15, 6, 3),
            TextureKit.Tri(x, y, new Vector2(30, 52), new Vector2(58, 32), new Vector2(30, 12)) - 1.5f));
        TextureKit.ImportSprite(S("arrow"), Vector4.zero);
        TextureKit.Shape(S("waitDot"), 64, (x, y) => TextureKit.Union(TextureKit.Circle(x, y, 32, 32, 9),
            Mathf.Abs(TextureKit.Circle(x, y, 32, 32, 22)) - 3f));
        TextureKit.ImportSprite(S("waitDot"), Vector4.zero);
        TextureKit.Shape(S("rewind"), 64, (x, y) => TextureKit.Union(
            TextureKit.Tri(x, y, new Vector2(32, 52), new Vector2(6, 32), new Vector2(32, 12)) - 1f,
            TextureKit.Tri(x, y, new Vector2(58, 52), new Vector2(32, 32), new Vector2(58, 12)) - 1f));
        TextureKit.ImportSprite(S("rewind"), Vector4.zero);
        TextureKit.Shape(S("marker"), 64, (x, y) => TextureKit.Tri(x, y, new Vector2(8, 54), new Vector2(56, 54), new Vector2(32, 14)) - 3f);
        TextureKit.ImportSprite(S("marker"), Vector4.zero);
        TextureKit.Save(S("vignette"), 256, 256, (x, y) =>
        {
            float u = (x + 0.5f) / 256f * 2 - 1, v = (y + 0.5f) / 256f * 2 - 1;
            float d = Mathf.Sqrt(u * u * 0.8f + v * v * 1.1f);
            return new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - 0.55f) / 0.75f)));
        });
        TextureKit.ImportSprite(S("vignette"), Vector4.zero);

        // --- world textures
        TextureKit.Save(T("ringSolid"), 256, 256, (x, y) =>
        {
            float d = Mathf.Abs(TextureKit.Circle(x + 0.5f, y + 0.5f, 128, 128, 104));
            float a = Mathf.Clamp01(1 - d / 7f) + 0.35f * Mathf.Exp(-d * d / 300f);
            return new Color(1, 1, 1, Mathf.Clamp01(a));
        });
        TextureKit.ImportTexture(T("ringSolid"), true, TextureWrapMode.Clamp);
        TextureKit.Save(T("ringDashed"), 256, 256, (x, y) =>
        {
            float dx = x + 0.5f - 128, dy = y + 0.5f - 128;
            float ang = Mathf.Atan2(dy, dx) / (2 * Mathf.PI) + 0.5f;
            float dash = Mathf.Clamp01(Mathf.Abs(Mathf.Repeat(ang * 14, 1) - 0.5f) * 8f - 1.2f);
            float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 104);
            float a = Mathf.Clamp01(1 - d / 6f) * (1 - dash) + 0.25f * Mathf.Exp(-d * d / 300f);
            return new Color(1, 1, 1, Mathf.Clamp01(a));
        });
        TextureKit.ImportTexture(T("ringDashed"), true, TextureWrapMode.Clamp);
        TextureKit.Save(T("glowDot"), 64, 64, (x, y) =>
        {
            float d = TextureKit.Circle(x + 0.5f, y + 0.5f, 32, 32, 0) / 32f;
            return new Color(1, 1, 1, Mathf.Clamp01(Mathf.Exp(-d * d * 7f)));
        });
        TextureKit.ImportTexture(T("glowDot"), true, TextureWrapMode.Clamp);
        TextureKit.Save(T("ghostTile"), 256, 256, (x, y) =>
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float box = Mathf.Abs(TextureKit.RoundRect(fx, fy, 128, 128, 116, 116, 14)) - 3.5f;
            float per = (fx + fy) * 0.5f;
            float dashOn = Mathf.Repeat((Mathf.Abs(fx - 128) > Mathf.Abs(fy - 128) ? fy : fx) / 28f, 1f) < 0.6f ? 1 : 0;
            float edge = Mathf.Clamp01(0.5f - box) * dashOn;
            float hatch = (Mathf.Repeat((fx - fy) / 22f, 1f) < 0.12f ? 0.16f : 0.06f) * (TextureKit.RoundRect(fx, fy, 128, 128, 112, 112, 12) < 0 ? 1 : 0);
            return new Color(1, 1, 1, Mathf.Clamp01(edge + hatch));
        });
        TextureKit.ImportTexture(T("ghostTile"), true, TextureWrapMode.Clamp);
        TextureKit.Save(T("stone"), 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f;
            float n = TextureKit.Fbm(u, v, 3);
            float speck = TextureKit.ValueNoise(u * 96, v * 96, 96, 9) > 0.86f ? -0.06f : 0f;
            float g = 0.82f + (n - 0.5f) * 0.32f + speck;
            return new Color(g, g * 0.985f, g * 0.96f, 1);
        });
        TextureKit.ImportTexture(T("stone"), false);
        TextureKit.Save(T("dial"), 1024, 1024, (x, y) =>
        {
            float dx = x + 0.5f - 512, dy = y + 0.5f - 512;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) / (2 * Mathf.PI) + 0.5f;
            float a = 0;
            a += Mathf.Clamp01(1 - Mathf.Abs(r - 490) / 3f);
            a += Mathf.Clamp01(1 - Mathf.Abs(r - 452) / 1.6f) * 0.7f;
            a += Mathf.Clamp01(1 - Mathf.Abs(r - 300) / 1.4f) * 0.5f;
            float tickMinor = Mathf.Abs(Mathf.Repeat(ang * 60 + 0.5f, 1) - 0.5f) * (2 * Mathf.PI * r / 60f);
            if (r > 456 && r < 486) a += Mathf.Clamp01(1.6f - tickMinor) * 0.8f;
            float tickMajor = Mathf.Abs(Mathf.Repeat(ang * 12 + 0.5f, 1) - 0.5f) * (2 * Mathf.PI * r / 12f);
            if (r > 400 && r < 486) a += Mathf.Clamp01(5f - tickMajor);
            a += 0.06f * Mathf.Exp(-Mathf.Pow((r - 470) / 60f, 2));
            return new Color(1, 1, 1, Mathf.Clamp01(a));
        });
        TextureKit.ImportTexture(T("dial"), true, TextureWrapMode.Clamp);
        AssetDatabase.Refresh();
    }

    // ===================================================================== font
    static TMP_FontAsset font;
    static void BuildFont()
    {
        string p = $"{R}/Fonts/RemakeKR SDF.asset";
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
        if (font != null) return;
        var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/NotoSansKR-Medium.ttf");
        font = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        font.name = "RemakeKR SDF";
        AssetDatabase.CreateAsset(font, p);
        font.atlasTextures[0].name = "RemakeKR Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        font.material.name = "RemakeKR Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.SaveAssets();
    }

    // ===================================================================== materials
    static Material Std(string name, Color c, float metal, float smooth, Texture2D tex = null, Color? emission = null, Vector2? tiling = null)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Glossiness", smooth);
        if (tex) { m.mainTexture = tex; m.mainTextureScale = tiling ?? Vector2.one; }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return SaveMat(name, m);
    }

    static Material Unlit(string name, Texture2D tex, Color c, bool additive, float pulse = 0, float pulseSpeed = 1.2f, int queueOffset = 0)
    {
        var m = new Material(Shader.Find("Remake/Unlit")) { mainTexture = tex };
        m.SetColor("_Color", c);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_Pulse", pulse);
        m.SetFloat("_PulseSpeed", pulseSpeed);
        m.renderQueue = 3000 + queueOffset;
        return SaveMat(name, m);
    }

    static void BuildMaterials()
    {
        var stone = Tex("stone");
        // environment kit
        Std("StoneTop", new Color(0.86f, 0.82f, 0.75f), 0f, 0.22f, stone, null, new Vector2(1.3f, 1.3f));
        Std("StoneSide", new Color(0.63f, 0.58f, 0.55f), 0f, 0.15f, stone, null, new Vector2(1.7f, 1.7f));
        Std("Brass", new Color(0.80f, 0.58f, 0.27f), 0.85f, 0.58f);
        Std("Iron", new Color(0.17f, 0.19f, 0.24f), 0.55f, 0.42f);
        Std("Rock", new Color(0.33f, 0.27f, 0.33f), 0f, 0.08f, stone, null, new Vector2(2.2f, 2.2f));
        Std("RockFar", new Color(0.22f, 0.19f, 0.27f), 0f, 0.05f, stone, null, new Vector2(1f, 1f));
        Std("IslandTop", new Color(0.42f, 0.38f, 0.44f), 0f, 0.1f, stone, null, new Vector2(0.6f, 0.6f));
        Std("DeviceGlow", new Color(0.18f, 0.1f, 0.24f), 0f, 0.5f, null, Device * 1.6f);
        Std("DeviceCrystal", new Color(0.45f, 0.25f, 0.6f), 0f, 0.8f, null, Device * 1.1f);
        Std("GoalGold", new Color(0.95f, 0.8f, 0.5f), 0.4f, 0.6f, null, new Color(1f, 0.82f, 0.5f) * 1.4f);
        Unlit("GoalPortal", Tex("glowDot"), new Color(1f, 0.86f, 0.55f, 0.9f), true, 0.25f);
        Unlit("Ghost", Tex("ghostTile"), new Color(Device.r * 1.6f, Device.g * 1.6f, Device.b * 1.6f, 1f), true, 0.3f, 0.9f);
        Unlit("Bead", null, Color.white, true, 0, 1, 2);
        Unlit("Mote", Tex("glowDot"), new Color(1f, 0.85f, 0.6f, 0.75f), true);
        Unlit("Dial", Tex("dial"), new Color(1f, 0.8f, 0.55f, 0.24f), true, 0, 1, -50);
        Unlit("DialHand", null, new Color(1f, 0.78f, 0.5f, 0.09f), true, 0, 1, -49);
        Unlit("OrbitRing", null, new Color(0.7f, 0.6f, 1f, 0.06f), true, 0, 1, -48);
        Unlit("RingPlayer", Tex("ringSolid"), new Color(Amber.r, Amber.g, Amber.b, 0.95f), false, 0, 1, 5);
        Unlit("RingPhantom", Tex("ringDashed"), new Color(Cyan.r, Cyan.g, Cyan.b, 1f), true, 0.15f, 1.5f, 5);

        // phantom hologram (shared by every rabbit design: "past" is always cyan)
        var holo = new Material(Shader.Find("Remake/Holo"));
        holo.SetColor("_Color", new Color(0.22f, 0.78f, 1f, 0.38f));
        holo.SetColor("_RimColor", new Color(0.65f, 1f, 1f, 1f));
        holo.renderQueue = 3010;
        SaveMat("Holo", holo);

        // rabbit A: brass clockwork
        Std("A_Brass", new Color(0.87f, 0.62f, 0.28f), 0.8f, 0.55f);
        Std("A_Iron", new Color(0.18f, 0.2f, 0.25f), 0.5f, 0.4f);
        Std("A_Eye", new Color(1f, 0.85f, 0.5f), 0f, 0.9f, null, new Color(1f, 0.76f, 0.35f) * 2.2f);
        // rabbit B: porcelain chronometer
        Std("B_Porcelain", new Color(0.95f, 0.93f, 0.89f), 0f, 0.86f);
        Std("B_Gold", new Color(0.93f, 0.72f, 0.33f), 1f, 0.72f);
        Std("B_Ink", new Color(0.1f, 0.12f, 0.2f), 0.1f, 0.85f);
        Std("B_Face", new Color(0.99f, 0.96f, 0.88f), 0f, 0.6f, null, new Color(1f, 0.92f, 0.75f) * 0.25f);
        // rabbit C: lantern scout
        Std("C_Slate", new Color(0.26f, 0.29f, 0.36f), 0.2f, 0.35f);
        Std("C_Rust", new Color(0.66f, 0.36f, 0.22f), 0.3f, 0.3f);
        Std("C_Glow", new Color(1f, 0.6f, 0.25f), 0f, 0.8f, null, new Color(1f, 0.55f, 0.18f) * 3.0f);
        Std("C_Brass", new Color(0.8f, 0.6f, 0.3f), 0.85f, 0.55f);
    }

    // ===================================================================== kit meshes & prefabs
    static Mesh Facet(Vector3 s) => MeshKit.Facet();
    static MeshKit.Part P(Mesh m, Vector3 p, Vector3 e, Vector3 s, int mat) => new(m, p, e, s, mat);

    static void BuildKit()
    {
        var facet = SaveMesh("facet", MeshKit.Facet(6, 8));
        var facetSmooth = SaveMesh("facetSmooth", MeshKit.Facet(12, 16, true));
        var box = SaveMesh("box", MeshKit.Box(Vector3.one));
        var bevel = MeshKit.Frustum(1.3435f, 1.2728f, 0.04f, 4, 45);
        var slab = MeshKit.Box(new Vector3(1.9f, 0.30f, 1.9f));
        var cap = MeshKit.Box(new Vector3(0.2f, 0.07f, 0.2f));
        var inlay = MeshKit.Torus(0.45f, 0.018f, 40, 6);
        var tileMats = new[] { M["StoneTop"], M["StoneSide"], M["Brass"], M["Rock"] };

        // --- path tiles (6 rock variations)
        for (int i = 0; i < 6; i++)
        {
            var parts = TileParts(bevel, slab, cap, inlay).ToList();
            parts.Add(P(MeshKit.Rock(100 + i * 31, 1.05f, 0.9f + (i % 3) * 0.45f, 7), new Vector3(0, -0.33f, 0), new Vector3(0, i * 37, 0), Vector3.one, 3));
            var mesh = SaveMesh($"tile_{i}", MeshKit.Combine(parts, 4));
            var go = new GameObject($"Tile_{i}");
            MeshNode("Mesh", go.transform, mesh, tileMats, Vector3.zero);
            SavePrefab(go, $"{R}/Prefabs/Tile_{i}.prefab");
        }

        // --- summoned bridge tile: no rock, a device crystal below, emissive seams on top
        {
            var go = new GameObject("Tile_Bridge");
            var mesh = SaveMesh("tile_bridge", MeshKit.Combine(TileParts(bevel, slab, cap, inlay), 4));
            MeshNode("Mesh", go.transform, mesh, tileMats, Vector3.zero);
            var seams = SaveMesh("bridge_seams", MeshKit.Combine(new[]
            {
                P(box, new Vector3(0, 0.006f, 0.9f), Vector3.zero, new Vector3(1.7f, 0.02f, 0.05f), 0),
                P(box, new Vector3(0, 0.006f, -0.9f), Vector3.zero, new Vector3(1.7f, 0.02f, 0.05f), 0),
                P(box, new Vector3(0.9f, 0.006f, 0), Vector3.zero, new Vector3(0.05f, 0.02f, 1.7f), 0),
                P(box, new Vector3(-0.9f, 0.006f, 0), Vector3.zero, new Vector3(0.05f, 0.02f, 1.7f), 0),
                P(MeshKit.Torus(0.45f, 0.03f, 40, 6), new Vector3(0, 0.004f, 0), Vector3.zero, Vector3.one, 0),
            }, 1));
            MeshNode("Seams", go.transform, seams, new[] { M["DeviceGlow"] }, Vector3.zero, default, null, false);
            var crystal = SaveMesh("crystal", MeshKit.Combine(new[]
            {
                P(MeshKit.Frustum(0.4f, 0f, 1.1f, 6), new Vector3(0, -0.34f, 0), new Vector3(180, 0, 0), Vector3.one, 0),
                P(MeshKit.Frustum(0.18f, 0f, 0.6f, 5), new Vector3(0.45f, -0.34f, 0.2f), new Vector3(180, 20, 0), Vector3.one, 0),
            }, 1));
            MeshNode("Crystal", go.transform, crystal, new[] { M["DeviceCrystal"] }, Vector3.zero);
            SavePrefab(go, $"{R}/Prefabs/Tile_Bridge.prefab");
        }

        // --- start dais
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Tile_0.prefab"));
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Start";
            var gear = SaveMesh("gear_start", MeshKit.Gear(16, 0.62f, 0.74f, 0.07f, 0.2f));
            MeshNode("Gear", go.transform, gear, new[] { M["Brass"] }, new Vector3(0, -0.05f, 0));
            SavePrefab(go, $"{R}/Prefabs/Start.prefab");
        }

        // --- goal gate: tile + arch + portal glow (faces -z, i.e. the arrival direction)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Tile_3.prefab"));
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Goal";
            var arch = SaveMesh("goal_arch", MeshKit.Combine(new[]
            {
                P(box, new Vector3(-0.82f, 1.1f, 0.8f), Vector3.zero, new Vector3(0.22f, 2.2f, 0.22f), 0),
                P(box, new Vector3(0.82f, 1.1f, 0.8f), Vector3.zero, new Vector3(0.22f, 2.2f, 0.22f), 0),
                P(box, new Vector3(0, 2.28f, 0.8f), Vector3.zero, new Vector3(2.05f, 0.2f, 0.3f), 0),
                P(box, new Vector3(-0.82f, 0.06f, 0.8f), Vector3.zero, new Vector3(0.34f, 0.12f, 0.34f), 1),
                P(box, new Vector3(0.82f, 0.06f, 0.8f), Vector3.zero, new Vector3(0.34f, 0.12f, 0.34f), 1),
                P(MeshKit.Torus(0.62f, 0.05f, 40, 6), new Vector3(0, 1.25f, 0.8f), new Vector3(90, 0, 0), Vector3.one, 1),
                P(MeshKit.Gear(12, 0.14f, 0.2f, 0.06f, 0.05f), new Vector3(0, 2.28f, 0.64f), new Vector3(90, 0, 0), Vector3.one, 1),
            }, 2));
            MeshNode("Arch", go.transform, arch, new[] { M["StoneSide"], M["Brass"] }, Vector3.zero);
            var portal = MeshNode("Portal", go.transform, SaveMesh("quad1", MeshKit.Quad(1f, 1f)), new[] { M["GoalPortal"] },
                new Vector3(0, 1.25f, 0.8f), new Vector3(-90, 0, 0), new Vector3(1.25f, 1, 1.25f), false);
            var rune = MeshNode("FloorRune", go.transform, SaveMesh("rune", MeshKit.Torus(0.55f, 0.025f, 40, 4)), new[] { M["GoalGold"] }, new Vector3(0, 0.005f, 0), default, null, false);
            SavePrefab(go, $"{R}/Prefabs/Goal.prefab");
        }

        // --- pressure plate (base ring + glyph, moving disc)
        {
            var go = new GameObject("PlateBase");
            MeshNode("Ring", go.transform, SaveMesh("plate_base", MeshKit.Frustum(0.84f, 0.78f, 0.08f, 8, 22.5f)), new[] { M["Iron"] }, Vector3.zero);
            MeshNode("Glyph", go.transform, SaveMesh("plate_glyph", MeshKit.Torus(0.68f, 0.03f, 48, 6)), new[] { M["DeviceGlow"] }, new Vector3(0, 0.085f, 0), default, null, false);
            SavePrefab(go, $"{R}/Prefabs/PlateBase.prefab");
            var top = new GameObject("PlateTop");
            MeshNode("Disc", top.transform, SaveMesh("plate_disc", MeshKit.Combine(new[]
            {
                P(MeshKit.Frustum(0.6f, 0.55f, 0.12f, 8, 22.5f), Vector3.zero, Vector3.zero, Vector3.one, 0),
                P(MeshKit.Torus(0.32f, 0.02f, 32, 4), new Vector3(0, 0.12f, 0), Vector3.zero, Vector3.one, 1),
            }, 2)), new[] { M["StoneTop"], M["Brass"] }, Vector3.zero);
            MeshNode("Gem", top.transform, facet, new[] { M["DeviceGlow"] }, new Vector3(0, 0.13f, 0), default, new Vector3(0.22f, 0.07f, 0.22f), false);
            SavePrefab(top, $"{R}/Prefabs/PlateTop.prefab");
        }

        // --- bridge ghost
        {
            var go = new GameObject("BridgeGhost");
            MeshNode("Outline", go.transform, SaveMesh("quad_tile", MeshKit.Quad(1.92f, 1.92f)), new[] { M["Ghost"] }, new Vector3(0, 0.015f, 0), default, null, false);
            SavePrefab(go, $"{R}/Prefabs/BridgeGhost.prefab");
        }
        SaveMesh("bead", MeshKit.Facet(5, 6));
        SaveMesh("ring_quad", MeshKit.Quad(1.7f, 1.7f));
    }

    static IEnumerable<MeshKit.Part> TileParts(Mesh bevel, Mesh slab, Mesh cap, Mesh inlay)
    {
        yield return P(bevel, new Vector3(0, -0.04f, 0), Vector3.zero, Vector3.one, 0);
        yield return P(slab, new Vector3(0, -0.19f, 0), Vector3.zero, Vector3.one, 1);
        foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
            yield return P(cap, new Vector3(0.86f * sx, -0.045f, 0.86f * sz), Vector3.zero, Vector3.one, 2);
        yield return P(inlay, new Vector3(0, -0.006f, 0), Vector3.zero, Vector3.one, 2);
    }

    // ===================================================================== rabbits
    // Built facing +Z with feet at y=0, ~1.5 m tall. Hierarchy drives RabbitRig:
    //   Rabbit_X (RabbitRig) ─ Body ─ BodyMesh, Spinner, Head ─ HeadMesh, EarL, EarR, EyeL, EyeR
    //                       └ GroundRing
    static void BuildRabbit(string v, bool phantom)
    {
        var fa = Meshes["facet"]; var fs = Meshes["facetSmooth"]; var bx = Meshes["box"];
        var root = new GameObject($"Rabbit_{v}{(phantom ? "_Phantom" : "")}");
        var rig = root.AddComponent<RabbitRig>();
        var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
        Transform head, earL, earR, spinner; Transform[] eyes;
        Material[] Mats(params string[] n) => n.Select(x => M[x]).ToArray();

        switch (v)
        {
            case "A": // Brass Clockwork — faithful to "model Chronos", a wind-up machine rabbit
            {
                var mats = Mats("A_Brass", "A_Iron");
                MeshNode("BodyMesh", body, SaveMesh($"rab{v}_body", MeshKit.Combine(new[]
                {
                    P(fa, new Vector3(0, 0.44f, -0.06f), Vector3.zero, new Vector3(0.78f, 0.66f, 1.0f), 0),
                    P(fa, new Vector3(0, 0.30f, 0.04f), Vector3.zero, new Vector3(0.62f, 0.42f, 0.8f), 1),
                    P(fa, new Vector3(-0.3f, 0.27f, -0.22f), Vector3.zero, new Vector3(0.3f, 0.42f, 0.52f), 0),
                    P(fa, new Vector3(0.3f, 0.27f, -0.22f), Vector3.zero, new Vector3(0.3f, 0.42f, 0.52f), 0),
                    P(bx, new Vector3(-0.3f, 0.04f, -0.02f), Vector3.zero, new Vector3(0.18f, 0.08f, 0.36f), 1),
                    P(bx, new Vector3(0.3f, 0.04f, -0.02f), Vector3.zero, new Vector3(0.18f, 0.08f, 0.36f), 1),
                    P(fa, new Vector3(-0.15f, 0.1f, 0.34f), Vector3.zero, new Vector3(0.13f, 0.22f, 0.15f), 1),
                    P(fa, new Vector3(0.15f, 0.1f, 0.34f), Vector3.zero, new Vector3(0.13f, 0.22f, 0.15f), 1),
                    P(fa, new Vector3(0, 0.78f, -0.25f), Vector3.zero, Vector3.one * 0.08f, 1),
                    P(fa, new Vector3(0, 0.79f, -0.05f), Vector3.zero, Vector3.one * 0.08f, 1),
                    P(fa, new Vector3(0, 0.76f, 0.15f), Vector3.zero, Vector3.one * 0.08f, 1),
                    P(MeshKit.Gear(8, 0.09f, 0.14f, 0.05f), new Vector3(0, 0.42f, -0.57f), new Vector3(90, 0, 0), Vector3.one, 1),
                }, 2)), mats, Vector3.zero);
                head = Node("Head", body, new Vector3(0, 0.78f, 0.34f));
                MeshNode("HeadMesh", head, SaveMesh($"rab{v}_head", MeshKit.Combine(new[]
                {
                    P(fa, new Vector3(0, 0.12f, 0.06f), Vector3.zero, new Vector3(0.5f, 0.46f, 0.54f), 0),
                    P(fa, new Vector3(0, 0.04f, 0.29f), Vector3.zero, new Vector3(0.26f, 0.18f, 0.2f), 1),
                    P(MeshKit.Torus(0.075f, 0.016f, 20, 5), new Vector3(0.15f, 0.16f, 0.29f), new Vector3(70, 0, 0), Vector3.one, 0),
                }, 2)), mats, Vector3.zero);
                earL = Ear(head, "EarL", new Vector3(-0.11f, 0.3f, 0.02f), new Vector3(-8, 0, 14), fa, new Vector3(0.16f, 0.66f, 0.08f), new Vector3(0.09f, 0.48f, 0.03f), mats, $"rab{v}_ear");
                earR = Ear(head, "EarR", new Vector3(0.11f, 0.3f, 0.02f), new Vector3(-8, 0, -14), fa, new Vector3(0.16f, 0.66f, 0.08f), new Vector3(0.09f, 0.48f, 0.03f), mats, $"rab{v}_ear");
                eyes = new[]
                {
                    MeshNode("EyeL", head, fa, Mats("A_Eye"), new Vector3(-0.15f, 0.16f, 0.3f), default, new Vector3(0.08f, 0.09f, 0.05f)).transform,
                    MeshNode("EyeR", head, fa, Mats("A_Eye"), new Vector3(0.15f, 0.16f, 0.3f), default, new Vector3(0.08f, 0.09f, 0.05f)).transform,
                };
                spinner = Node("Spinner", body, new Vector3(0, 0.62f, -0.5f));
                MeshNode("Key", spinner, SaveMesh($"rab{v}_key", MeshKit.Combine(new[]
                {
                    P(MeshKit.Frustum(0.03f, 0.03f, 0.18f, 6), Vector3.zero, new Vector3(-90, 0, 0), Vector3.one, 0),
                    P(MeshKit.Torus(0.09f, 0.028f, 20, 6), new Vector3(-0.11f, 0, -0.2f), new Vector3(90, 0, 0), Vector3.one, 1),
                    P(MeshKit.Torus(0.09f, 0.028f, 20, 6), new Vector3(0.11f, 0, -0.2f), new Vector3(90, 0, 0), Vector3.one, 1),
                    P(fa, new Vector3(0, 0, -0.2f), Vector3.zero, Vector3.one * 0.08f, 1),
                }, 2)), Mats("A_Iron", "A_Brass"), Vector3.zero);
                rig.spinAxis = Vector3.forward;
                break;
            }
            case "B": // Porcelain Chronometer — glossy ceramic, gold kintsugi seams, a clock on the chest
            {
                var mats = Mats("B_Porcelain", "B_Gold", "B_Ink", "B_Face");
                MeshNode("BodyMesh", body, SaveMesh($"rab{v}_body", MeshKit.Combine(new[]
                {
                    P(fs, new Vector3(0, 0.43f, -0.05f), Vector3.zero, new Vector3(0.74f, 0.7f, 0.9f), 0),
                    P(fs, new Vector3(-0.28f, 0.26f, -0.2f), Vector3.zero, new Vector3(0.3f, 0.44f, 0.5f), 0),
                    P(fs, new Vector3(0.28f, 0.26f, -0.2f), Vector3.zero, new Vector3(0.3f, 0.44f, 0.5f), 0),
                    P(fs, new Vector3(-0.28f, 0.05f, 0f), Vector3.zero, new Vector3(0.17f, 0.1f, 0.34f), 0),
                    P(fs, new Vector3(0.28f, 0.05f, 0f), Vector3.zero, new Vector3(0.17f, 0.1f, 0.34f), 0),
                    P(fs, new Vector3(-0.14f, 0.1f, 0.33f), Vector3.zero, new Vector3(0.13f, 0.2f, 0.14f), 0),
                    P(fs, new Vector3(0.14f, 0.1f, 0.33f), Vector3.zero, new Vector3(0.13f, 0.2f, 0.14f), 0),
                    P(MeshKit.Torus(0.27f, 0.014f, 40, 5), new Vector3(0, 0.66f, 0.2f), new Vector3(-58, 0, 0), Vector3.one, 1),
                    P(MeshKit.Torus(0.16f, 0.012f, 32, 5), new Vector3(-0.43f, 0.28f, -0.2f), new Vector3(0, 0, 90), Vector3.one, 1),
                    P(MeshKit.Torus(0.16f, 0.012f, 32, 5), new Vector3(0.43f, 0.28f, -0.2f), new Vector3(0, 0, 90), Vector3.one, 1),
                    P(MeshKit.Frustum(0.17f, 0.17f, 0.025f, 28), new Vector3(0, 0.48f, 0.36f), new Vector3(70, 0, 0), Vector3.one, 3),
                    P(MeshKit.Torus(0.17f, 0.016f, 32, 5), new Vector3(0, 0.48f, 0.36f), new Vector3(70, 0, 0), Vector3.one, 1),
                    P(fs, new Vector3(0, 0.42f, -0.52f), Vector3.zero, Vector3.one * 0.17f, 1),
                }, 4)), mats, Vector3.zero);
                head = Node("Head", body, new Vector3(0, 0.78f, 0.3f));
                MeshNode("HeadMesh", head, SaveMesh($"rab{v}_head", MeshKit.Combine(new[]
                {
                    P(fs, new Vector3(0, 0.12f, 0.06f), Vector3.zero, new Vector3(0.5f, 0.46f, 0.52f), 0),
                    P(fs, new Vector3(0, 0.04f, 0.28f), Vector3.zero, new Vector3(0.22f, 0.15f, 0.16f), 0),
                    P(fs, new Vector3(0, 0.08f, 0.36f), Vector3.zero, Vector3.one * 0.05f, 1),
                }, 4)), mats, Vector3.zero);
                earL = Ear(head, "EarL", new Vector3(-0.1f, 0.32f, 0f), new Vector3(-6, 0, 10), fs, new Vector3(0.15f, 0.78f, 0.07f), new Vector3(0.08f, 0.58f, 0.02f), Mats("B_Porcelain", "B_Gold"), $"rab{v}_ear");
                earR = Ear(head, "EarR", new Vector3(0.1f, 0.32f, 0f), new Vector3(-6, 0, -10), fs, new Vector3(0.15f, 0.78f, 0.07f), new Vector3(0.08f, 0.58f, 0.02f), Mats("B_Porcelain", "B_Gold"), $"rab{v}_ear");
                eyes = new[]
                {
                    MeshNode("EyeL", head, fs, Mats("B_Ink"), new Vector3(-0.14f, 0.16f, 0.28f), default, new Vector3(0.08f, 0.1f, 0.05f)).transform,
                    MeshNode("EyeR", head, fs, Mats("B_Ink"), new Vector3(0.14f, 0.16f, 0.28f), default, new Vector3(0.08f, 0.1f, 0.05f)).transform,
                };
                spinner = Node("Spinner", body, new Vector3(0, 0.48f, 0.36f), new Vector3(70, 0, 0));
                MeshNode("Hands", spinner, SaveMesh($"rab{v}_hands", MeshKit.Combine(new[]
                {
                    P(bx, new Vector3(0, 0.032f, 0.05f), Vector3.zero, new Vector3(0.022f, 0.012f, 0.12f), 0),
                    P(bx, new Vector3(0.035f, 0.032f, 0), Vector3.zero, new Vector3(0.08f, 0.012f, 0.02f), 0),
                }, 1)), Mats("B_Ink"), Vector3.zero);
                rig.spinAxis = Vector3.up;
                break;
            }
            default: // "C" Lantern Scout — chunky field robot, glowing lantern core and visor, hourglass pack
            {
                var mats = Mats("C_Slate", "C_Rust", "C_Glow", "C_Brass");
                var fc = SaveMesh("facetChunky", MeshKit.Facet(4, 6));
                MeshNode("BodyMesh", body, SaveMesh($"rab{v}_body", MeshKit.Combine(new[]
                {
                    P(fc, new Vector3(0, 0.42f, -0.04f), Vector3.zero, new Vector3(0.86f, 0.68f, 0.94f), 0),
                    P(fa, new Vector3(0, 0.46f, 0.37f), Vector3.zero, new Vector3(0.32f, 0.32f, 0.2f), 2),
                    P(MeshKit.Torus(0.17f, 0.028f, 16, 4), new Vector3(0, 0.46f, 0.43f), new Vector3(90, 0, 0), Vector3.one, 3),
                    P(fc, new Vector3(-0.31f, 0.25f, -0.18f), Vector3.zero, new Vector3(0.32f, 0.4f, 0.5f), 0),
                    P(fc, new Vector3(0.31f, 0.25f, -0.18f), Vector3.zero, new Vector3(0.32f, 0.4f, 0.5f), 0),
                    P(bx, new Vector3(-0.3f, 0.05f, 0f), Vector3.zero, new Vector3(0.2f, 0.1f, 0.36f), 1),
                    P(bx, new Vector3(0.3f, 0.05f, 0f), Vector3.zero, new Vector3(0.2f, 0.1f, 0.36f), 1),
                    P(fc, new Vector3(-0.16f, 0.11f, 0.32f), Vector3.zero, new Vector3(0.14f, 0.22f, 0.16f), 1),
                    P(fc, new Vector3(0.16f, 0.11f, 0.32f), Vector3.zero, new Vector3(0.14f, 0.22f, 0.16f), 1),
                    P(bx, new Vector3(0, 0.62f, -0.42f), Vector3.zero, new Vector3(0.38f, 0.42f, 0.12f), 1),
                }, 4)), mats, Vector3.zero);
                head = Node("Head", body, new Vector3(0, 0.78f, 0.3f));
                MeshNode("HeadMesh", head, SaveMesh($"rab{v}_head", MeshKit.Combine(new[]
                {
                    P(fc, new Vector3(0, 0.12f, 0.06f), Vector3.zero, new Vector3(0.56f, 0.48f, 0.52f), 0),
                    P(fc, new Vector3(0, 0.02f, 0.27f), Vector3.zero, new Vector3(0.26f, 0.16f, 0.2f), 1),
                    P(bx, new Vector3(0, 0.15f, 0.29f), Vector3.zero, new Vector3(0.46f, 0.11f, 0.04f), 0),
                }, 4)), mats, Vector3.zero);
                earL = Ear(head, "EarL", new Vector3(-0.14f, 0.3f, 0f), new Vector3(-10, 0, 22), fc, new Vector3(0.2f, 0.48f, 0.09f), new Vector3(0.11f, 0.3f, 0.03f), Mats("C_Slate", "C_Rust"), $"rab{v}_ear", Mats("C_Glow")[0]);
                earR = Ear(head, "EarR", new Vector3(0.14f, 0.3f, 0f), new Vector3(-10, 0, -22), fc, new Vector3(0.2f, 0.48f, 0.09f), new Vector3(0.11f, 0.3f, 0.03f), Mats("C_Slate", "C_Rust"), $"rab{v}_ear", Mats("C_Glow")[0]);
                eyes = new[]
                {
                    MeshNode("EyeL", head, bx, Mats("C_Glow"), new Vector3(-0.1f, 0.15f, 0.315f), default, new Vector3(0.16f, 0.06f, 0.03f)).transform,
                    MeshNode("EyeR", head, bx, Mats("C_Glow"), new Vector3(0.1f, 0.15f, 0.315f), default, new Vector3(0.16f, 0.06f, 0.03f)).transform,
                };
                spinner = Node("Spinner", body, new Vector3(0, 0.66f, -0.56f));
                MeshNode("Hourglass", spinner, SaveMesh($"rab{v}_hourglass", MeshKit.Combine(new[]
                {
                    P(MeshKit.Frustum(0.13f, 0.13f, 0.03f, 8), new Vector3(0, 0.14f, 0), Vector3.zero, Vector3.one, 0),
                    P(MeshKit.Frustum(0.13f, 0.13f, 0.03f, 8), new Vector3(0, -0.17f, 0), Vector3.zero, Vector3.one, 0),
                    P(MeshKit.Frustum(0.02f, 0.1f, 0.14f, 8), new Vector3(0, 0f, 0), Vector3.zero, Vector3.one, 1),
                    P(MeshKit.Frustum(0.1f, 0.02f, 0.14f, 8), new Vector3(0, -0.14f, 0), Vector3.zero, Vector3.one, 1),
                }, 2)), Mats("C_Brass", "C_Glow"), Vector3.zero);
                rig.spinAxis = Vector3.right;
                break;
            }
        }

        var ring = MeshNode("GroundRing", root.transform, Meshes["ring_quad"], new[] { M[phantom ? "RingPhantom" : "RingPlayer"] }, new Vector3(0, 0.02f, 0), default, null, false);

        rig.body = body; rig.head = head; rig.earL = earL; rig.earR = earR; rig.spinner = spinner; rig.eyes = eyes;
        rig.groundRing = ring.transform; rig.ringSpin = phantom ? -30f : 0f;

        if (phantom)
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.gameObject != ring) { r.sharedMaterials = Enumerable.Repeat(M["Holo"], r.sharedMaterials.Length).ToArray(); r.shadowCastingMode = ShadowCastingMode.Off; }

        SavePrefab(root, $"{R}/Prefabs/Rabbits/{root.name}.prefab");
    }

    static Transform Ear(Transform head, string name, Vector3 pos, Vector3 euler, Mesh m, Vector3 outer, Vector3 inner, Material[] mats, string meshName, Material tipGlow = null)
    {
        var ear = Node(name, head, pos, euler);
        var parts = new List<MeshKit.Part>
        {
            P(m, new Vector3(0, outer.y * 0.47f, 0), Vector3.zero, outer, 0),
            P(m, new Vector3(0, outer.y * 0.47f, outer.z * 0.42f), Vector3.zero, inner, 1),
        };
        MeshNode("Mesh", ear, SaveMesh(meshName, MeshKit.Combine(parts, 2)), mats, Vector3.zero);
        if (tipGlow != null) MeshNode("Tip", ear, Meshes["facet"], new[] { tipGlow }, new Vector3(0, outer.y * 0.95f, 0), default, Vector3.one * 0.09f);
        return ear;
    }

    // ===================================================================== scene: base
    static void BuildBaseScene(string rabbit)
    {
        string path = $"{R}/Scenes/RemakeBase.unity";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset("Assets/Scenes/LevelBaseScene.unity", path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject Find(string n) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == n)?.gameObject;

        // single-level mode (original campaign flow untouched)
        var lm = Find("LevelManager").GetComponent<LevelManager>();
        lm.singleLevelMode = true; lm.singleLevelBaseScene = "RemakeBase"; lm.singleLevelIndex = 3; lm.singleLevelScene = "R-001-3";
        EditorUtility.SetDirty(lm);

        // retire the old rewind strip + overlay (TurnManager's calls to them are null-safe)
        var rm = Find("RewindManager");
        UnityEngine.Object.DestroyImmediate(rm.GetComponent<RewindSliderUI>());
        UnityEngine.Object.DestroyImmediate(rm.GetComponent<RewindToggleEffect>());
        UnityEngine.Object.DestroyImmediate(Find("ScaleCanvas"));
        UnityEngine.Object.DestroyImmediate(Find("ConstantCanvas"));
        var mono = Find("MonologueText")?.GetComponent<TextMeshProUGUI>();
        if (mono) { mono.font = font; EditorUtility.SetDirty(mono); }

        // characters: hide the original model, mount the new rabbit (logic objects untouched)
        MountRabbit(Find("Player"), rabbit, false);
        MountRabbit(Find("Phantom"), rabbit, true);

        // light + atmosphere
        var sun = Find("Directional Light").GetComponent<Light>();
        sun.color = new Color(1f, 0.87f, 0.72f); sun.intensity = 1.15f;
        sun.transform.rotation = Quaternion.Euler(44, 22, 0);
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.62f; sun.shadowBias = 0.03f; sun.shadowNormalBias = 0.3f;

        var sky = new Material(Shader.Find("Remake/Sky"));
        SaveMat("Sky", sky);
        RenderSettings.skybox = M["Sky"];
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.46f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.33f, 0.43f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.12f, 0.19f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.015f; RenderSettings.fogColor = new Color(0.25f, 0.21f, 0.38f);
        var cam = Find("Camera").GetComponent<Camera>();
        cam.farClipPlane = 400; cam.allowMSAA = true; cam.allowHDR = false; cam.fieldOfView = 50;

        BuildAbyss(new Vector3(1, 0, 11));

        // HUD
        var hudGo = new GameObject("RemakeHUD");
        var hud = hudGo.AddComponent<RemakeHUD>();
        hud.font = font;
        hud.chip = Spr("chip"); hud.chipOutline = Spr("chipOutline"); hud.arrow = Spr("arrow"); hud.waitDot = Spr("waitDot");
        hud.rewindIcon = Spr("rewind"); hud.keycap = Spr("keycap"); hud.vignette = Spr("vignette"); hud.pill = Spr("pill");
        hud.softDot = Spr("softDot"); hud.marker = Spr("marker");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void MountRabbit(GameObject character, string v, bool phantom)
    {
        // Keep the original Animator alive (PhantomController.KillPhantom only runs when an animator
        // exists), but hide the old meshes by deactivating their GameObjects. Inactive renderers are
        // not collected by PlayerController's death-blink list, so the old model never reappears.
        var old = character.transform.Find("MainCharacter");
        if (old)
        {
            old.gameObject.SetActive(true);
            foreach (var smr in old.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.gameObject.SetActive(false);
        }
        var prev = character.transform.Find("RabbitVisual");
        if (prev) UnityEngine.Object.DestroyImmediate(prev.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Rabbits/Rabbit_{v}{(phantom ? "_Phantom" : "")}.prefab");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, character.transform);
        inst.name = "RabbitVisual";
        inst.transform.localPosition = RabbitRig.MountOffset; // root sits 1 m above the tile top, scale 2
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one * RabbitRig.MountScale;
    }

    static void BuildAbyss(Vector3 c)
    {
        var root = new GameObject("Abyss").transform;
        var motion = root.gameObject.AddComponent<BackgroundMotion>();
        var spins = new List<BackgroundMotion.Spin>(); var bobs = new List<BackgroundMotion.Bob>();

        // giant clock face far below — it runs backwards while rewinding
        var dial = Node("Dial", root, c + new Vector3(0, -55, 0));
        MeshNode("Face", dial, Meshes["quad1"], new[] { M["Dial"] }, Vector3.zero, default, new Vector3(110, 1, 110), false);
        var hands = Node("Hands", dial, new Vector3(0, 0.05f, 0));
        MeshNode("Hour", hands, Meshes["box"], new[] { M["DialHand"] }, new Vector3(0, 0, 13), default, new Vector3(0.7f, 0.05f, 26), false);
        MeshNode("Minute", hands, Meshes["box"], new[] { M["DialHand"] }, new Vector3(19, 0, 0), new Vector3(0, 0, 0), new Vector3(38, 0.05f, 0.35f), false);
        spins.Add(new BackgroundMotion.Spin { t = hands, axis = Vector3.up, degPerSec = 4f });
        spins.Add(new BackgroundMotion.Spin { t = dial, axis = Vector3.up, degPerSec = -0.6f });

        // orbit rings (gyroscope feel)
        var torusMesh = SaveMesh("orbit_ring", MeshKit.Torus(1f, 0.004f, 96, 4));
        float[] radii = { 30, 42 };
        for (int i = 0; i < radii.Length; i++)
        {
            var ringT = Node($"Orbit{i}", root, c + new Vector3(0, -36 - i * 8, 0), new Vector3(8 * (i - 1), 0, 6 * (1 - i)));
            MeshNode("Ring", ringT, torusMesh, new[] { M["OrbitRing"] }, Vector3.zero, default, new Vector3(radii[i], radii[i] * 3, radii[i]), false);
            spins.Add(new BackgroundMotion.Spin { t = ringT, axis = Vector3.up, degPerSec = (i % 2 == 0 ? 1 : -1) * (2.5f - i * 0.6f) });
        }

        // drifting dust motes (islands are placed by the level build, which knows the camera)
        BuildMotes(root, c);
        motion.spins = spins.ToArray();
        motion.bobs = bobs.ToArray();
    }

    // Floating ruins, placed after the camera is fitted: each candidate is projected through the
    // game camera and rejected if it would sit over the puzzle, the HUD corners, or too close.
    static void BuildIslands(Transform parent, Vector3 c, Vector3 camPos, Quaternion camRot, List<Vector3> puzzleWorld)
    {
        var root = Node("Islands", parent, Vector3.zero);
        var motion = root.gameObject.AddComponent<BackgroundMotion>();
        var bobs = new List<BackgroundMotion.Bob>();
        var camGo = new GameObject("~islandcam", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 50; cam.aspect = 16f / 9f;
        camGo.transform.SetPositionAndRotation(camPos, camRot);
        var puzzleVp = puzzleWorld.Select(w => (Vector2)cam.WorldToViewportPoint(w)).ToList();
        float px0 = puzzleVp.Min(v => v.x) - 0.03f, px1 = puzzleVp.Max(v => v.x) + 0.03f, py0 = puzzleVp.Min(v => v.y) - 0.03f, py1 = puzzleVp.Max(v => v.y) + 0.03f;

        var rnd = new System.Random(7);
        float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        var islandMats = new[] { M["IslandTop"], M["RockFar"] };
        int made = 0;
        for (int k = 0; made < 13 && k < 600; k++)
        {
            float ang = Rf(0, Mathf.PI * 2), dist = Rf(14, 52);
            var p = c + new Vector3(Mathf.Cos(ang) * dist, Rf(-34, -12), Mathf.Sin(ang) * dist * 1.1f);
            float s = Rf(2.0f, 5.0f);
            var vp = cam.WorldToViewportPoint(p);
            float rad = s * 1.1f / (vp.z * Mathf.Tan(25f * Mathf.Deg2Rad) * 2f); // approx viewport-height radius
            if (vp.z < 30f) continue;                                               // too close: would loom
            bool overPuzzle = vp.x > px0 - rad && vp.x < px1 + rad && vp.y > py0 - rad && vp.y < py1 + rad && PuzzleHit(puzzleVp, vp, rad + 0.05f);
            bool hudBL = vp.x < 0.26f + rad && vp.y < 0.2f + rad, hudBR = vp.x > 0.74f - rad && vp.y < 0.27f + rad;
            bool hudTop = vp.y > 0.76f - rad && vp.x > 0.18f - rad && vp.x < 0.82f + rad;
            bool offscreen = vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f;
            if (overPuzzle || hudBL || hudBR || hudTop || offscreen) continue;
            var isl = Node($"Island{made}", root, p, new Vector3(0, Rf(0, 360), 0), Vector3.one * s);
            var mesh = SaveMesh($"island_{made}", MeshKit.Combine(new[]
            {
                P(MeshKit.Frustum(1.0f, 0.92f, 0.18f, 7, Rf(0, 50)), new Vector3(0, -0.18f, 0), Vector3.zero, Vector3.one, 0),
                P(MeshKit.Rock(500 + made, 1.0f, Rf(1.2f, 2.4f), 7), new Vector3(0, -0.17f, 0), Vector3.zero, Vector3.one, 1),
            }, 2));
            MeshNode("Rock", isl, mesh, islandMats, Vector3.zero);
            if (made % 3 == 0) // ruined pillars
                for (int q = 0; q < 1 + made % 2 + 1; q++)
                    MeshNode($"Pillar{q}", isl, Meshes["box"], new[] { M["IslandTop"] }, new Vector3(Rf(-0.6f, 0.6f), 0.3f, Rf(-0.6f, 0.6f)), new Vector3(Rf(-6, 6), Rf(0, 90), Rf(-6, 6)), new Vector3(0.16f, Rf(0.4f, 1.1f), 0.16f));
            if (made % 4 == 1) // a stray gear stuck in the rock
                MeshNode("Gear", isl, Meshes["gear_start"], new[] { M["Brass"] }, new Vector3(0.3f, 0.1f, 0), new Vector3(70, 0, 10), Vector3.one * 0.7f);
            bobs.Add(new BackgroundMotion.Bob { t = isl, amp = Rf(0.15f, 0.5f), speed = Rf(0.15f, 0.35f) });
            made++;
        }
        motion.bobs = bobs.ToArray();
        motion.spins = new BackgroundMotion.Spin[0];
        UnityEngine.Object.DestroyImmediate(camGo);
        Debug.Log($"[Builder] islands placed: {made}");
    }

    static bool PuzzleHit(List<Vector2> pts, Vector3 vp, float r)
    {
        var v = new Vector2(vp.x, vp.y);
        return pts.Any(p => (p - v).sqrMagnitude < r * r);
    }

    static void BuildMotes(Transform root, Vector3 c)
    {
        var motes = new GameObject("Motes");
        motes.transform.SetParent(root, false);
        motes.transform.position = c + new Vector3(0, -6, 0);
        var ps = motes.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.startLifetime = 14f; main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.6f, 0.8f), new Color(0.8f, 0.7f, 1f, 0.6f));
        main.maxParticles = 320; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 22f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(46, 18, 50);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
        vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
        col.color = g;
        motes.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["Mote"];
    }

    // ===================================================================== scene: level
    static void BuildLevelScene()
    {
        string path = $"{R}/Scenes/R-001-3.unity";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset("Assets/Scenes/L-001/L-001-3.unity", path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();

        var art = new GameObject("RemakeArt").transform;
        var tilePrefabs = Enumerable.Range(0, 6).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Tile_{i}.prefab")).ToArray();
        GameObject bridgeTile = null;
        var fitPoints = new List<Vector3>();

        foreach (var t in all)
        {
            var go = t.gameObject;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null || go.GetComponent<BoxCollider>() == null) continue;
            if (t.parent == null || t.parent.name != "Tilemap") continue;

            bool isStart = go.CompareTag("StartTile") && go.activeSelf;
            bool isGoal = go.CompareTag("GoalTile") && go.activeSelf;
            if (!go.activeSelf) continue; // the inactive StartTile/GoalTile slabs; their gear siblings are the real ones
            mr.enabled = false;

            Vector3 top = new(t.position.x, 0f, t.position.z);
            GameObject prefab;
            if (isStart) prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Start.prefab");
            else if (isGoal) prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Goal.prefab");
            else if (go.name == "TileButton") { prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Tile_Bridge.prefab"); bridgeTile = go; }
            else prefab = tilePrefabs[Mathf.Abs(Mathf.RoundToInt(top.x * 7 + top.z * 13)) % 6];

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "Visual";
            inst.transform.SetPositionAndRotation(top, Quaternion.identity);
            inst.transform.SetParent(t, true); // follows SetActive of the gameplay tile (bridge!)
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            {
                fitPoints.Add(top + new Vector3(sx, 0f, sz));
                fitPoints.Add(top + new Vector3(sx * 0.6f, -0.7f, sz * 0.6f)); // upper part of the hanging rock
            }
            fitPoints.Add(top + Vector3.up * 1.6f); // a standing rabbit
            if (isGoal) fitPoints.Add(top + new Vector3(0, 2.45f, 0.8f)); // gate lintel
        }

        // pressure plate
        var button = all.Select(t => t.GetComponent<Button>()).First(b => b != null);
        foreach (var r in button.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
        var platePos = new Vector3(button.transform.position.x, 0f, button.transform.position.z);
        var baseInst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/PlateBase.prefab"));
        baseInst.transform.position = platePos;
        baseInst.transform.SetParent(button.transform.GetChild(0), true);
        var topInst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/PlateTop.prefab"));
        topInst.transform.position = platePos + new Vector3(0, 0.05f, 0);
        topInst.transform.SetParent(button.transform.GetChild(1), true); // Plate: moved by Button.cs when pressed

        // bridge ghost + energy conduit
        var target = button.targetStates[0].target;
        var bpos = new Vector3(target.transform.position.x, 0f, target.transform.position.z);
        var ghost = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/BridgeGhost.prefab"));
        ghost.transform.position = bpos; ghost.transform.SetParent(art, true);

        var linkGo = new GameObject("DeviceLink"); linkGo.transform.SetParent(art, false);
        var link = linkGo.AddComponent<DeviceLink>();
        link.button = button; link.target = target; link.ghost = ghost; link.deviceColor = Device;
        link.plateGlyph = baseInst.transform.Find("Glyph").GetComponent<MeshRenderer>();
        link.targetSeams = target.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Seams");
        var beads = new List<Renderer>();
        Vector3 a = platePos + new Vector3(0, 0.35f, 0), b = bpos + new Vector3(0, 0.35f, 0);
        Vector3 mid = (a + b) * 0.5f + new Vector3(0, 2.4f, 0);
        const int N = 22;
        for (int i = 1; i < N; i++)
        {
            float u = (float)i / N;
            Vector3 p = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * mid + u * u * b;
            beads.Add(MeshNode($"Bead{i}", linkGo.transform, Meshes["bead"], new[] { M["Bead"] }, p, default, Vector3.one * 0.18f, false).GetComponent<MeshRenderer>());
        }
        link.dots = beads.ToArray();

        // the level's white spot light washed out one tile; the remake lights the level from the base scene
        var spot = all.FirstOrDefault(t => t.name == "Spot Light");
        if (spot) spot.gameObject.SetActive(false);

        // camera: fit the whole puzzle (start, plate, bridge, goal gate) between the HUD bands
        var anchor = all.First(t => t.name == "CameraOffsetAnchor");
        fitPoints.Add(bpos + Vector3.up * 2.6f); // top of the conduit arc
        FitCamera(anchor, fitPoints, 50f, 16f / 9f, 50f, 315f);
        var centre = fitPoints.Aggregate(Vector3.zero, (s, p) => s + p) / fitPoints.Count;
        BuildIslands(art, new Vector3(centre.x, 0, centre.z), anchor.position, anchor.rotation, fitPoints);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void FitCamera(Transform anchor, List<Vector3> corners, float fov, float aspect, float pitch, float yaw)
    {
        var camGo = new GameObject("~fit", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = fov; cam.aspect = aspect;
        var rot = Quaternion.Euler(pitch, yaw, 0);
        camGo.transform.rotation = rot;
        // safe area (viewport) between the top timeline/phantom rows and the bottom status/legend
        // (the level runs bottom-left -> top-right at this yaw, so the HUD corners stay free)
        float xMin = 0.05f, xMax = 0.95f, yMin = 0.07f, yMax = 0.745f; // top stays clear of the phantom-queue row
        Vector3 target = corners.Aggregate(Vector3.zero, (s, p) => s + p) / corners.Count;
        float dist = 30;
        for (int it = 0; it < 40; it++)
        {
            camGo.transform.position = target - rot * Vector3.forward * dist;
            var vps = corners.Select(cam.WorldToViewportPoint).ToList();
            float vx0 = vps.Min(p => p.x), vx1 = vps.Max(p => p.x), vy0 = vps.Min(p => p.y), vy1 = vps.Max(p => p.y);
            float sx = (vx1 - vx0) / (xMax - xMin), sy = (vy1 - vy0) / (yMax - yMin);
            dist *= Mathf.Lerp(1f, Mathf.Max(sx, sy), 0.7f);
            // recentre: shift the look target so the projected box centre lands in the safe-area centre
            float cx = (vx0 + vx1) * 0.5f - (xMin + xMax) * 0.5f, cy = (vy0 + vy1) * 0.5f - (yMin + yMax) * 0.5f;
            float halfH = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * dist;
            target += rot * Vector3.right * (cx * 2 * halfH * aspect) * 0.7f + rot * Vector3.up * (cy * 2 * halfH) * 0.7f;
        }
        anchor.SetPositionAndRotation(camGo.transform.position, rot);
        UnityEngine.Object.DestroyImmediate(camGo);
        Debug.Log($"[Builder] camera anchor {anchor.position} rot {rot.eulerAngles} dist {dist:0.0}");
    }

    static void AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        foreach (var p in new[] { $"{R}/Scenes/RemakeBase.unity", $"{R}/Scenes/R-001-3.unity" })
            if (!list.Any(s => s.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ===================================================================== go helpers
    static Transform Node(string name, Transform parent, Vector3 pos, Vector3 euler = default, Vector3? scale = null)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos; t.localEulerAngles = euler; t.localScale = scale ?? Vector3.one;
        return t;
    }

    static GameObject MeshNode(string name, Transform parent, Mesh mesh, Material[] mats, Vector3 pos, Vector3 euler = default, Vector3? scale = null, bool shadows = true)
    {
        var t = Node(name, parent, pos, euler, scale);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = t.gameObject.AddComponent<MeshRenderer>();
        r.sharedMaterials = mats;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.receiveShadows = shadows;
        return t.gameObject;
    }

    static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
