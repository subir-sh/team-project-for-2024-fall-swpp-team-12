using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class RemakeArtConfig
{
    public const string ChosenRabbit = "O";
    public const string ChosenPalette = "LA";
    public static readonly string[] Variants = { "M", "N", "O" };
    public const string LevelScene = "R-004-2";
    public const string SourceLevel = "Assets/Scenes/L-004/L-004-2.unity";
    public const int LevelIndex = 16; // L-004-2 in the original campaign list
    // the whole campaign, in LevelManager order; each is rebuilt as a remake scene (look comes from StageStyle)
    public static readonly string[] StageLevels = {
        "Prologue", "L-001-1", "L-001-2", "L-001-3", "L-001-4", "L-001-5", "L-002-1", "L-002-2", "L-002-3", "L-002-4",
        "L-003-1", "L-003-2", "L-003-3", "L-003-4", "Event", "L-004-1", "L-004-2", "L-004-3", "Epilogue" };
    public static string SourcePath(string lv) => lv.StartsWith("L-") ? $"Assets/Scenes/{lv.Substring(0, 5)}/{lv}.unity" : $"Assets/Scenes/ScriptScenes/{lv}.unity";
    public static string SceneName(string lv) => "R-" + (lv.StartsWith("L-") ? lv.Substring(2) : lv);
}

// Builds the remake from the Blender models (Assets/Remake/Models) + generated UI sprites:
//   Unity.exe -batchmode -quit -projectPath <p> -executeMethod RemakeBuilder.BuildAll [-rabbit A|B|C]
// Original scenes/prefabs are never modified. RemakeBase + R-004-2 are copies whose original
// gameplay objects keep their colliders/scripts; only visuals are hidden and replaced.
public static class RemakeBuilder
{
    const string R = "Assets/Remake";
    static readonly Dictionary<string, Material> M = new();
    static readonly Dictionary<string, Mesh> Kit = new();
    static TMP_FontAsset font;

    static Color PlateCol => RemakePalette.All[Arg("-palette") ?? RemakeArtConfig.ChosenPalette].plate;
    static Color LeverCol => RemakePalette.All[Arg("-palette") ?? RemakeArtConfig.ChosenPalette].lever;
    static readonly Color PastCol = new(0.62f, 0.86f, 1.0f);
    const float RabbitHeight = 1.5f;

    public static void BuildAll()
    {
        string rabbit = Arg("-rabbit") ?? RemakeArtConfig.ChosenRabbit;
        foreach (var f in new[] { "Sprites", "Textures", "Materials", "Prefabs", "Prefabs/Rabbits", "Scenes" }) Directory.CreateDirectory($"{R}/{f}");
        AssetDatabase.Refresh();
        ImportModels();
        BuildSprites();
        CopyRuntimeTextures();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{R}/Fonts/RemakeKR SDF.asset");
        BuildMaterials();
        LoadKit();
        foreach (var v in RemakeArtConfig.Variants) { BuildRabbit(v, false); BuildRabbit(v, true); }
        BuildBase(rabbit);
        BuildGame();
        foreach (var lv in RemakeArtConfig.StageLevels)
            BuildLevel(RemakeArtConfig.SourcePath(lv), RemakeArtConfig.SceneName(lv));
        foreach (var f in Directory.GetFiles($"{R}/Scenes", "*.unity"))   // drop remake scenes that are no longer built
        {
            var n = Path.GetFileNameWithoutExtension(f);
            if (n != "RemakeBase" && n != "RemakeGame" && !RemakeArtConfig.StageLevels.Any(l => RemakeArtConfig.SceneName(l) == n)) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        }
        var list = EditorBuildSettings.scenes.Where(s => !s.path.StartsWith($"{R}/Scenes/")).ToList();
        list.Add(new EditorBuildSettingsScene($"{R}/Scenes/RemakeBase.unity", true));
        list.Add(new EditorBuildSettingsScene($"{R}/Scenes/RemakeGame.unity", true));
        foreach (var lv in RemakeArtConfig.StageLevels) list.Add(new EditorBuildSettingsScene($"{R}/Scenes/{RemakeArtConfig.SceneName(lv)}.unity", true));
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Builder] done (rabbit={rabbit})");
    }

    // Unity.exe -batchmode -quit -projectPath <p> -executeMethod RemakeBuilder.BuildPlayer  ->  <repo>/Build/ChronosRemake/
    public static void BuildPlayer()
    {
        var scenes = new List<string> { $"{R}/Scenes/RemakeGame.unity" };
        scenes.AddRange(RemakeArtConfig.StageLevels.Select(l => $"{R}/Scenes/{RemakeArtConfig.SceneName(l)}.unity"));
        string exe = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Build/ChronosRemake/ChronosRemake.exe"));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(), locationPathName = exe, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        Debug.Log($"[Builder] player {report.summary.result} {exe} size={report.summary.totalSize / (1024 * 1024)}MB errors={report.summary.totalErrors}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // Unity.exe -batchmode -quit -projectPath <p> -executeMethod RemakeBuilder.BuildWebGL  ->  <repo>/Build/ChronosRemakeWeb/ (itch.io upload)
    public static void BuildWebGL()
    {
        var scenes = new List<string> { $"{R}/Scenes/RemakeGame.unity" };
        scenes.AddRange(RemakeArtConfig.StageLevels.Select(l => $"{R}/Scenes/{RemakeArtConfig.SceneName(l)}.unity"));
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Build/ChronosRemakeWeb"));
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = false;  // itch.io serves *.gz with Content-Encoding: gzip; the fallback build fails there
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.stripEngineCode = false;              // scene colliders were stripped on WebGL ("class 'CapsuleCollider' doesn't exist")
        PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(), locationPathName = dir, target = BuildTarget.WebGL, options = BuildOptions.None
        });
        Debug.Log($"[Builder] webgl {report.summary.result} {dir} size={report.summary.totalSize / (1024 * 1024)}MB errors={report.summary.totalErrors}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
            return;
        }

        // Keep the embedding size, but avoid rendering the entire scene at HiDPI density.
        string indexPath = Path.Combine(dir, "index.html");
        string html = File.ReadAllText(indexPath);
        const string loadPlayer = "createUnityInstance(canvas, config,";
        if (!html.Contains(loadPlayer)) throw new InvalidOperationException("WebGL template no longer uses the expected loader.");
        File.WriteAllText(indexPath, html.Replace(loadPlayer,
            "config.devicePixelRatio = 1;\n        " + loadPlayer));
    }

    static string Arg(string n) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

    // ===================================================================== models
    static void ImportModels()
    {
        foreach (var p in Directory.GetFiles($"{R}/Models", "*.fbx"))  // Rabbit_D/E/F + kit
        {
            var path = p.Replace('\\', '/');
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.globalScale = 1f; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.importNormals = ModelImporterNormals.Import;
            mi.bakeAxisConversion = true; // Blender Z-up -> Unity Y-up baked into the hierarchy
            mi.SaveAndReimport();
        }
    }

    static void LoadKit()
    {
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath($"{R}/Models/kit.fbx").OfType<Mesh>()) Kit[m.name] = m;
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath($"{R}/Models/props.fbx").OfType<Mesh>()) Kit[m.name] = m;
        var pillar = MeshKit.Frustum(0.62f * 1.4142f, 0.86f * 1.4142f, 9.5f, 4, 45);
        var verts = pillar.vertices; for (int i = 0; i < verts.Length; i++) verts[i].y -= 10f; pillar.vertices = verts; pillar.RecalculateBounds();
        pillar.name = "Pillar";
        {   // make sure the side faces point outward (inverted faces show the rim light and read as pale grey)
            var tv = pillar.vertices; var ti = pillar.triangles; int outward = 0, inward = 0;
            for (int i = 0; i < ti.Length; i += 3)
            {
                Vector3 a0 = tv[ti[i]], a1 = tv[ti[i + 1]], a2 = tv[ti[i + 2]];
                var n = Vector3.Cross(a1 - a0, a2 - a0); var cen = (a0 + a1 + a2) / 3f; cen.y = 0;
                if (Mathf.Abs(n.y) > 0.5f * n.magnitude) continue;
                if (Vector3.Dot(n, cen) > 0) outward++; else inward++;
            }
            Debug.Log($"[Builder] pillar faces outward={outward} inward={inward}");
            if (inward > outward)
            {
                for (int i = 0; i < ti.Length; i += 3) (ti[i + 1], ti[i + 2]) = (ti[i + 2], ti[i + 1]);
                pillar.triangles = ti; pillar.RecalculateNormals();
            }
        }
        var pPath = $"{R}/Materials/Pillar.mesh.asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(pPath);
        if (ex) { EditorUtility.CopySerialized(pillar, ex); pillar = ex; } else AssetDatabase.CreateAsset(pillar, pPath);
        Kit["Pillar"] = pillar;
        foreach (var k in Kit) Debug.Log($"[Builder] kit {k.Key} bounds {k.Value.bounds}");
    }

    // ===================================================================== sprites / textures
    // stage styles are applied at runtime, so their textures must also exist in a player build
    static void CopyRuntimeTextures()
    {
        string dst = $"{R}/Resources/RemakeTex";
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles($"{R}/Textures", "*.png"))
        {
            var n = Path.GetFileName(f);
            if (!(n.StartsWith("dt_") || n.StartsWith("ov_") || n == "shaft.png")) continue;
            AssetDatabase.DeleteAsset($"{dst}/{n}");
            AssetDatabase.CopyAsset(f.Replace('\\', '/'), $"{dst}/{n}");
        }
        AssetDatabase.Refresh();
    }

    static Sprite Spr(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{R}/Sprites/{n}.png");
    static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{R}/Textures/{n}.png");

    static void BuildSprites()
    {
        string S(string n) => $"{R}/Sprites/{n}.png";
        string T(string n) => $"{R}/Textures/{n}.png";
        void Sp(string n, int size, TextureKit.Sdf f, Vector4 border = default) { TextureKit.Shape(S(n), size, f); TextureKit.ImportSprite(S(n), border); }
        Sp("pill", 48, (x, y) => TextureKit.RoundRect(x, y, 24, 24, 23, 23, 23), new Vector4(23, 23, 23, 23));
        Sp("keycap", 48, (x, y) => TextureKit.RoundRect(x, y, 24, 24, 23, 23, 8), new Vector4(10, 10, 10, 10));
        Sp("softDot", 64, (x, y) => TextureKit.Circle(x, y, 32, 32, 30));
        Sp("ring", 64, (x, y) => Mathf.Abs(TextureKit.Circle(x, y, 32, 32, 28)) - 2.2f);
        Sp("arrow", 64, (x, y) => TextureKit.Union(TextureKit.RoundRect(x, y, 22, 32, 14, 6, 3),
            TextureKit.Tri(x, y, new Vector2(30, 54), new Vector2(60, 32), new Vector2(30, 10)) - 2f));
        Sp("waitDot", 64, (x, y) => TextureKit.Circle(x, y, 32, 32, 9));
        Sp("caret", 32, (x, y) => TextureKit.Tri(x, y, new Vector2(4, 4), new Vector2(28, 4), new Vector2(16, 26)) - 1.5f);
        Sp("marker", 64, (x, y) => TextureKit.Tri(x, y, new Vector2(8, 56), new Vector2(56, 56), new Vector2(32, 12)) - 3f);
        Sp("check", 128, (x, y) => TextureKit.Union(Mathf.Abs(TextureKit.Circle(x, y, 64, 64, 56)) - 4f,
            Mathf.Min(SegD(x, y, 38, 66, 56, 46) - 7f, SegD(x, y, 56, 46, 92, 84) - 7f)));
        Sp("pause", 128, (x, y) => TextureKit.Union(TextureKit.RoundRect(x, y, 48, 64, 12, 40, 6), TextureKit.RoundRect(x, y, 80, 64, 12, 40, 6)));
        Sp("restart", 64, (x, y) =>
        {
            float ring = Mathf.Abs(TextureKit.Circle(x, y, 32, 32, 20)) - 4f;
            float ang = Mathf.Atan2(y - 32, x - 32) * Mathf.Rad2Deg;
            if (ang > 50 && ang < 110) ring = 99;
            return TextureKit.Union(ring, TextureKit.Tri(x, y, new Vector2(40, 58), new Vector2(54, 44), new Vector2(36, 40)) - 1f);
        });
        TextureKit.Save(S("vignette"), 256, 256, (x, y) =>
        {
            float u = (x + 0.5f) / 128f - 1, v = (y + 0.5f) / 128f - 1;
            return new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Sqrt(u * u * 0.8f + v * v * 1.1f) - 0.6f) / 0.7f)));
        });
        TextureKit.ImportSprite(S("vignette"), Vector4.zero);
        TextureKit.Save(T("glowDot"), 64, 64, (x, y) => { float d = TextureKit.Circle(x + .5f, y + .5f, 32, 32, 0) / 32f; return new Color(1, 1, 1, Mathf.Exp(-d * d * 7)); });
        TextureKit.ImportTexture(T("glowDot"), true, TextureWrapMode.Clamp);
        // ---- material detail (greyscale, tileable; one texture spans 2 m = one tile at scale 0.5)
        float N(float u, float v, int per, int seed) => TextureKit.ValueNoise(u * per, v * per, per, seed);
        float F(float u, float v, int seed) => TextureKit.Fbm(u, v, seed, 5);
        void Detail(string n, Func<float, float, float> g)
        {
            TextureKit.Save(T(n), 256, 256, (x, y) => { float v = Mathf.Clamp01(g((x + .5f) / 256f, (y + .5f) / 256f)); return new Color(v, v, v, 1); });
            TextureKit.ImportTexture(T(n), false);
        }
        Detail("dt_iron", (u, v) =>
        {
            float b = 0.86f + (F(u, v, 11) - 0.5f) * 0.18f;
            float seam = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)) < 0.012f ? 0.6f : 1f;
            float mid = Mathf.Abs(u - 0.5f) < 0.006f ? 0.78f : 1f;
            float rivet = 1f; foreach (var cu in new[] { 0.07f, 0.93f }) foreach (var cv in new[] { 0.07f, 0.93f })
                if ((u - cu) * (u - cu) + (v - cv) * (v - cv) < 0.0004f) rivet = 1.12f;
            float scratch = N(u * 0.3f, v * 9f, 24, 5) > 0.83f ? 0.88f : 1f;
            float rust = Mathf.SmoothStep(0.55f, 0.75f, F(u, v, 41)) * 0.18f;
            return b * seam * mid * rivet * scratch - rust;
        });
        Detail("dt_sandstone", (u, v) =>
        {
            float strata = 0.5f + 0.5f * Mathf.Sin((v * 7f + F(u, v, 7) * 1.4f) * Mathf.PI * 2f);
            float speck = N(u, v, 96, 3) > 0.9f ? -0.08f : 0f;
            return 0.84f + strata * 0.1f + (F(u, v, 9) - 0.5f) * 0.14f + speck;
        });
        Detail("dt_concrete", (u, v) =>
        {
            float speck = (N(u, v, 128, 1) - 0.5f) * 0.12f;
            float blot = (F(u, v, 23) - 0.5f) * 0.2f;
            float crack = Mathf.Abs(F(u * 0.7f, v * 0.7f, 61) - 0.5f) < 0.006f ? -0.2f : 0f;
            return 0.86f + speck + blot + crack;
        });
        Detail("dt_patina", (u, v) =>
        {
            float p2 = Mathf.SmoothStep(0.42f, 0.62f, F(u, v, 31));
            float streak = (N(u * 0.4f, v * 6f, 16, 8) - 0.5f) * 0.1f;
            return 0.78f + p2 * 0.22f + streak;
        });
        Detail("dt_clay", (u, v) =>
        {
            float row = Mathf.Repeat(v * 6f, 1f); float brickU = Mathf.Repeat(u * 3f + (Mathf.Floor(v * 6f) % 2) * 0.5f, 1f);
            float mortar = row < 0.06f || brickU < 0.03f ? 0.7f : 1f;
            return (0.86f + (F(u, v, 17) - 0.5f) * 0.18f) * mortar;
        });
        Detail("dt_brass", (u, v) =>
        {
            float brushed = (N(u * 0.2f, v * 40f, 64, 2) - 0.5f) * 0.12f;
            float tarnish = Mathf.SmoothStep(0.55f, 0.75f, F(u, v, 51)) * 0.15f;
            float panel = Mathf.Abs(v - 0.5f) < 0.008f ? 0.75f : 1f;
            return (0.9f + brushed - tarnish) * panel;
        });
        // ---- abandoned-lab materials
        Detail("dt_labfloor", (u, v) =>   // dark deck panel: seam, inner bevel, small corner marks (Timelie-like floor dots)
        {
            float e = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
            float seam = e < 0.01f ? 0.55f : e < 0.03f ? 1.08f : 1f;
            float mark = 1f; foreach (var cu in new[] { 0.16f, 0.84f }) foreach (var cv in new[] { 0.16f, 0.84f })
                if (Mathf.Abs(u - cu) < 0.018f && Mathf.Abs(v - cv) < 0.018f) mark = 1.35f;
            float grime = (F(u, v, 71) - 0.5f) * 0.16f + (N(u, v, 128, 4) - 0.5f) * 0.05f;
            return (0.84f + grime) * seam * mark;
        });
        Detail("dt_labwall", (u, v) =>    // tall wall panels with a vent band
        {
            float pu = Mathf.Repeat(u * 2f, 1f), pv = Mathf.Repeat(v * 2f, 1f);
            float seam = Mathf.Min(pu, 1 - pu) < 0.012f || Mathf.Min(pv, 1 - pv) < 0.012f ? 0.6f : 1f;
            float vent = pv > 0.12f && pv < 0.24f && Mathf.Repeat(u * 32f, 1f) < 0.5f ? 0.78f : 1f;
            float stain = Mathf.SmoothStep(0.5f, 0.8f, F(u * 0.6f, v * 0.3f, 13)) * 0.18f;
            return (0.88f + (F(u, v, 19) - 0.5f) * 0.12f - stain) * seam * vent;
        });
        Detail("dt_checker", (u, v) =>    // faded two-tone tiles, 4x4 per tile, chipped
        {
            int cx = (int)(u * 2f), cy = (int)(v * 2f);
            float t = ((cx + cy) & 1) == 0 ? 1f : 0.84f;
            float gu = Mathf.Repeat(u * 2f, 1f), gv = Mathf.Repeat(v * 2f, 1f);
            float grout = Mathf.Min(Mathf.Min(gu, 1 - gu), Mathf.Min(gv, 1 - gv)) < 0.025f ? 0.6f : 1f;
            float wear = (F(u, v, 83) - 0.5f) * 0.22f;
            return (t + wear) * (grout < 1f ? 0.7f : 1f) * 0.92f;
        });
        Detail("ov_moss", (u, v) =>       // clumped overgrowth mask
            Mathf.SmoothStep(0.46f, 0.66f, F(u, v, 91)) * (0.75f + 0.5f * N(u, v, 64, 9)));
        Detail("ov_rust", (u, v) =>       // rust blooms + downward streaks
        {
            float bloom = Mathf.SmoothStep(0.5f, 0.72f, F(u, v, 101));
            float streak = Mathf.SmoothStep(0.55f, 0.9f, N(u * 0.25f, v * 3f, 24, 12)) * Mathf.SmoothStep(0.3f, 0.6f, F(u, v * 0.3f, 7));
            return Mathf.Max(bloom, streak * 0.85f);
        });
        TextureKit.Save(T("shaft"), 64, 256, (x, y) =>
        {
            float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 256f;
            float across = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2.5f);
            return new Color(1, 1, 1, across * Mathf.SmoothStep(0f, 1f, v));
        });
        TextureKit.ImportTexture(T("shaft"), true, TextureWrapMode.Clamp);
        // laser beam: bright core across the line width (LineRenderer v runs across the width)
        TextureKit.Save(T("beam"), 8, 64, (x, y) => { float v = (y + 0.5f) / 64f - 0.5f; float a = Mathf.Exp(-v * v * 160f) + 0.35f * Mathf.Exp(-v * v * 18f); return new Color(1, 1, 1, Mathf.Clamp01(a)); });
        TextureKit.ImportTexture(T("beam"), true, TextureWrapMode.Clamp);
        TextureKit.Save(T("barrier"), 64, 128, (x, y) =>
        {
            float v = (y + 0.5f) / 128f;
            float lines = Mathf.Repeat(v * 9f, 1f) < 0.12f ? 0.35f : 0f;
            return new Color(1, 1, 1, Mathf.Clamp01(0.22f + lines) * Mathf.SmoothStep(1f, 0.15f, v));
        });
        TextureKit.ImportTexture(T("barrier"), true);
        AssetDatabase.Refresh();
    }

    static float SegD(float x, float y, float ax, float ay, float bx, float by)
    {
        Vector2 p = new(x, y), a = new(ax, ay), b = new(bx, by), pa = p - a, ba = b - a;
        return (pa - ba * Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude)).magnitude;
    }

    // ===================================================================== materials
    static Material Save(string name, Material m)
    {
        string p = $"{R}/Materials/{name}.mat"; m.name = name;
        var ex = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (ex) { ex.shader = m.shader; ex.CopyPropertiesFromMaterial(m); ex.renderQueue = m.renderQueue; ex.enableInstancing = m.enableInstancing; EditorUtility.SetDirty(ex); return M[name] = ex; }
        AssetDatabase.CreateAsset(m, p); return M[name] = m;
    }

    static Material Toon(string name, Color c, float spec = 0.2f, Color? emission = null, Color? shade = null, float rim = 0.3f)
    {
        var m = new Material(Shader.Find("Remake/Toon"));
        m.SetColor("_Color", c); m.SetFloat("_Spec", spec);
        m.SetColor("_ShadeColor", shade ?? new Color(0.66f, 0.64f, 0.82f));
        m.SetColor("_RimColor", new Color(1, 1, 1, rim));
        m.SetColor("_Emission", emission ?? Color.black);
        return Save(name, m);
    }

    static Material Unlit(string name, Texture2D tex, Color c, bool additive, float pulse = 0, int queue = 3000)
    {
        var m = new Material(Shader.Find("Remake/Unlit")) { mainTexture = tex };
        m.SetColor("_Color", c);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_Pulse", pulse); m.SetFloat("_PulseSpeed", 0.8f); m.renderQueue = queue;
        return Save(name, m);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static readonly Dictionary<string, Dictionary<string, (string hex, float spec, bool glow)>> Palette = new()
    {
        ["A"] = new() { ["Body"] = ("#F3EEE4", .25f, false), ["Accent"] = ("#FF7B47", .2f, false), ["Visor"] = ("#1B2033", .9f, false), ["Glow"] = ("#FFE7A8", 0, true), ["Metal"] = ("#D9A35A", .6f, false) },
        ["B"] = new() { ["Body"] = ("#8C97B5", .25f, false), ["Accent"] = ("#EEE6D6", .2f, false), ["Visor"] = ("#141826", .9f, false), ["Glow"] = ("#FFF3CC", 0, true), ["Metal"] = ("#D7A452", .6f, false), ["Inner"] = ("#F2A08F", .15f, false) },
        ["C"] = new() { ["Body"] = ("#FBF4E8", .2f, false), ["Accent"] = ("#E8704E", .2f, false), ["Visor"] = ("#1E1A22", .9f, false), ["Glow"] = ("#FFD27A", 0, true), ["Metal"] = ("#C98F45", .6f, false) },
        ["D"] = new() { ["Body"] = ("#3B404C", .35f, false), ["Accent"] = ("#E7E0D2", .15f, false), ["Glow"] = ("#FFB547", 0, true), ["Metal"] = ("#C99A55", .6f, false), ["Inner"] = ("#E7E0D2", .1f, false) },
        ["E"] = new() { ["Body"] = ("#EDE7DA", .45f, false), ["Accent"] = ("#F7F3EA", .3f, false), ["Visor"] = ("#1A1C24", .9f, false), ["Metal"] = ("#D3A24E", .6f, false), ["Inner"] = ("#E9C2B2", .1f, false) },
        ["M"] = new() { ["Body"] = ("#F1E6D0", .35f, false), ["Accent"] = ("#D2483B", .25f, false), ["Metal"] = ("#CFA453", .8f, false), ["Visor"] = ("#1E2230", .9f, false), ["Glow"] = ("#FFFFFF", 0, true) },
        ["N"] = new() { ["Body"] = ("#DCD1C1", .05f, false), ["Accent"] = ("#E9B9A8", .05f, false), ["Metal"] = ("#CFA453", .8f, false), ["Visor"] = ("#22202A", .9f, false), ["Glow"] = ("#FFFFFF", 0, true) },
        ["O"] = new() { ["Body"] = ("#EEE8DC", .35f, false), ["Accent"] = ("#3A3E48", .4f, false), ["Metal"] = ("#CFA453", .8f, false), ["Visor"] = ("#1C2030", .9f, false), ["Glow"] = ("#FFD48A", 0, true), ["Inner"] = ("#E6BFAE", .1f, false) },
        ["J"] = new() { ["Body"] = ("#A8483A", .35f, false), ["Accent"] = ("#E6DCC6", .2f, false), ["Metal"] = ("#C9A15A", .8f, false), ["Visor"] = ("#4A4D55", .6f, false), ["Glow"] = ("#FFC46A", 0, true) },
        ["K"] = new() { ["Body"] = ("#5B6470", .4f, false), ["Accent"] = ("#2F333B", .3f, false), ["Metal"] = ("#C9A15A", .8f, false), ["Visor"] = ("#2A2C31", .6f, false), ["Glow"] = ("#FFB547", 0, true) },
        ["L"] = new() { ["Body"] = ("#B87452", .5f, false), ["Accent"] = ("#4E7D72", .3f, false), ["Metal"] = ("#D8B06A", .8f, false), ["Visor"] = ("#3B3F45", .6f, false), ["Glow"] = ("#F7E3A8", 0, true) },
        ["G"] = new() { ["Body"] = ("#3B404C", .35f, false), ["Accent"] = ("#E3DCCD", .15f, false), ["Glow"] = ("#FFB547", 0, true), ["Metal"] = ("#D0A050", .7f, false), ["Inner"] = ("#E3DCCD", .1f, false) },
        ["H"] = new() { ["Body"] = ("#ECE5D8", .45f, false), ["Accent"] = ("#F7F3EA", .3f, false), ["Visor"] = ("#1C1E26", .9f, false), ["Metal"] = ("#D0A050", .7f, false), ["Inner"] = ("#E6BFAE", .1f, false) },
        ["I"] = new() { ["Body"] = ("#4F7D74", .35f, false), ["Accent"] = ("#E6DDC8", .15f, false), ["Glow"] = ("#FFD27A", 0, true), ["Metal"] = ("#D0A050", .7f, false), ["Inner"] = ("#E6DDC8", .1f, false) },
        ["F"] = new() { ["Body"] = ("#2F333D", .35f, false), ["Accent"] = ("#D9D2C3", .15f, false), ["Glow"] = ("#FFC46B", 0, true), ["Metal"] = ("#E8664A", .1f, false), ["Inner"] = ("#D9D2C3", .1f, false) },
    };

    static void BuildMaterials()
    {
        var pal = RemakePalette.All[Arg("-palette") ?? RemakeArtConfig.ChosenPalette];
        foreach (var n in new[] { "Tile", "Pillar", "Wall", "Dark", "PlatePad", "PlateRim", "LeverRim", "LeverHandle", "Goal", "Start",
                                  "Crate", "CrateTrim", "CrateMark", "Core", "Piston", "Hazard", "Panel", "Lens" })
        {
            RemakePalette.Colours(pal, n, out var c, out var e);
            float spec = n == "Tile" ? 0.05f : n.Contains("Handle") || n == "Dark" || n == "CrateTrim" || n == "Piston" ? 0.4f : n == "Core" || n == "Lens" ? 0.6f : 0.15f;
            var tm = Toon(n, c, spec, e, pal.shade, n == "Tile" ? 0.15f : 0.25f);
            string dt = n == "Tile" ? pal.floorTex : n == "Wall" ? pal.wallTex : n == "Pillar" ? pal.pillarTex : null;
            if (!string.IsNullOrEmpty(dt)) { tm.SetTexture("_DetailTex", Tex(dt)); tm.SetFloat("_DetailStrength", 0.85f); tm.SetFloat("_DetailScale", 0.5f); EditorUtility.SetDirty(tm); }
        }
        RemakePalette.Colours(pal, "GhostPlate", out var gp, out _); Unlit("GhostPlate", null, gp, true, 0.35f);
        RemakePalette.Colours(pal, "GhostLever", out var gl, out _); Unlit("GhostLever", null, gl, true, 0.35f);
        Unlit("Barrier", Tex("barrier"), pal.plate, true, 0.15f);
        RemakePalette.Colours(pal, "LeverArrow", out var la, out _); Unlit("LeverArrow", null, la, false, 0.25f, 3001);

        Unlit("Mote", Tex("glowDot"), new Color(0.9f, 0.85f, 1f, 0.5f), true);
        RemakePalette.Colours(pal, "Beam", out var bm, out _); Unlit("Beam", Tex("beam"), bm, true, 0.08f, 3002);
        RemakePalette.Colours(pal, "BeamGlow", out var bg, out _); Unlit("BeamGlow", Tex("beam"), bg, true, 0.2f, 3001);
        Unlit("FarRing", null, new Color(0.55f, 0.5f, 0.9f, 0.05f), true, 0, 2900);
        var after = new Material(Shader.Find("Remake/Afterimage")) { enableInstancing = true, renderQueue = 3010 };
        RemakePalette.Colours(pal, "Afterimage", out var hz, out _); after.SetColor("_Color", hz);
        Save("Afterimage", after);
        var echo = new Material(Shader.Find("Remake/Afterimage")) { enableInstancing = true, renderQueue = 3005 };
        RemakePalette.Colours(pal, "AfterimageEcho", out var hz2, out _); echo.SetColor("_Color", hz2);
        Save("AfterimageEcho", echo);
        foreach (var v in Palette)
            foreach (var p in v.Value)
            {
                var c = Hex(p.Value.hex);
                Toon($"{v.Key}_{p.Key}", c, p.Value.spec, p.Value.glow ? c * 1.3f : (Color?)null, null, 0.35f);
            }
        var sky = new Material(Shader.Find("Remake/Sky"));
        sky.SetColor("_Zenith", pal.skyTop); sky.SetColor("_Horizon", pal.skyHorizon);
        sky.SetColor("_Mid", Color.Lerp(pal.skyHorizon, pal.skyBottom, 0.5f)); sky.SetColor("_Nadir", pal.skyBottom);
        sky.SetFloat("_StarDensity", 0.25f); sky.SetColor("_SunColor", new Color(0, 0, 0));
        Save("Sky", sky);
    }

    // ===================================================================== rabbits
    static void BuildRabbit(string v, bool phantom)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Models/Rabbit_{v}.fbx");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var root = new GameObject($"Rabbit_{v}{(phantom ? "_Phantom" : "")}");
        inst.transform.SetParent(root.transform, false);
        Transform F(string n) => inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);

        // orientation: eyes must face +Z
        var eye = F("EyeL");
        if (eye && root.transform.InverseTransformPoint(eye.position).z < 0) inst.transform.localRotation = Quaternion.Euler(0, 180, 0) * inst.transform.localRotation;
        // scale to a common height, feet on y=0
        var rs = inst.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float s = RabbitHeight / b.size.y;
        inst.transform.localScale *= s;
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        inst.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);

        foreach (var r in rs)
        {
            r.sharedMaterials = r.sharedMaterials.Select(m =>
            {
                string key = m ? m.name.Split(' ')[0] : "Body";
                if (phantom) return M["Afterimage"];
                return M.TryGetValue($"{v}_{key}", out var tm) ? tm : M[$"{v}_Body"];
            }).ToArray();
            r.shadowCastingMode = phantom ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.receiveShadows = !phantom;
        }

        var rig = root.AddComponent<RabbitRig>();
        rig.body = F("Body"); rig.head = F("Head"); rig.earL = F("EarL"); rig.earR = F("EarR"); rig.spinner = F("Spinner");
        rig.eyes = new[] { F("EyeL"), F("EyeR") }.Where(t => t).ToArray();
        rig.spinAxis = v == "C" || v == "D" ? Vector3.right : "GHIJKLMNO".Contains(v) ? Vector3.up : Vector3.forward;
        if (phantom)
        {
            var trail = root.AddComponent<AfterimageTrail>();
            trail.echoMaterial = M["AfterimageEcho"];
        }
        Directory.CreateDirectory($"{R}/Prefabs/Rabbits");
        PrefabUtility.SaveAsPrefabAsset(root, $"{R}/Prefabs/Rabbits/{root.name}.prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }

    // ===================================================================== base scene
    static void BuildBase(string rabbit)
    {
        string path = $"{R}/Scenes/RemakeBase.unity";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset("Assets/Scenes/LevelBaseScene.unity", path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject Find(string n) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == n)?.gameObject;

        var lm = Find("LevelManager").GetComponent<LevelManager>();
        lm.singleLevelMode = true; lm.singleLevelBaseScene = "RemakeBase";
        lm.singleLevelIndex = RemakeArtConfig.LevelIndex; lm.singleLevelScene = RemakeArtConfig.LevelScene;
        EditorUtility.SetDirty(lm);

        var rm = Find("RewindManager");
        UnityEngine.Object.DestroyImmediate(rm.GetComponent<RewindSliderUI>());
        UnityEngine.Object.DestroyImmediate(rm.GetComponent<RewindToggleEffect>());
        UnityEngine.Object.DestroyImmediate(Find("ScaleCanvas"));
        UnityEngine.Object.DestroyImmediate(Find("ConstantCanvas"));
        foreach (var root in scene.GetRootGameObjects())
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.font = font;
            label.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(label);
        }

        MountRabbit(Find("Player"), rabbit, false);
        MountRabbit(Find("Phantom"), rabbit, true);
        var tre = Find("Player").GetComponent<TimeRewindEffect>();
        if (tre) UnityEngine.Object.DestroyImmediate(tre); // old particle burst; the HUD + afterimage carry rewind now

        var sun = Find("Directional Light").GetComponent<Light>();
        sun.color = new Color(1f, 0.95f, 0.88f); sun.intensity = 1.05f;
        sun.transform.rotation = Quaternion.Euler(52, 330, 0);
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.45f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.35f;
        RenderSettings.skybox = M["Sky"];
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.54f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.4f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.28f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f; RenderSettings.fogColor = RemakePalette.All[Arg("-palette") ?? RemakeArtConfig.ChosenPalette].fog;
        var cam = Find("Camera").GetComponent<Camera>();
        cam.fieldOfView = 40; cam.farClipPlane = 400; cam.allowMSAA = true; cam.allowHDR = false;

        // background: a faint ring of time far below + sparse motes
        var bg = new GameObject("Background");
        var amb = bg.AddComponent<Ambient>();
        var ring = new GameObject("FarRing"); ring.transform.SetParent(bg.transform, false);
        ring.transform.position = new Vector3(1, -46, 13);
        var ringMesh = MeshKit.Torus(1f, 0.0035f, 128, 4); ringMesh.name = "FarRing";
        AssetDatabase.CreateAsset(ringMesh, $"{R}/Materials/FarRing.mesh.asset");
        foreach (float rad in new[] { 34f, 46f })
        {
            var r = new GameObject("R" + rad); r.transform.SetParent(ring.transform, false); r.transform.localScale = new Vector3(rad, rad * 2, rad);
            r.AddComponent<MeshFilter>().sharedMesh = ringMesh; var mr = r.AddComponent<MeshRenderer>(); mr.sharedMaterial = M["FarRing"]; mr.shadowCastingMode = ShadowCastingMode.Off;
        }
        amb.spin = new[] { ring.transform }; amb.degPerSec = 1.5f;
        var motes = new GameObject("Motes"); motes.transform.SetParent(bg.transform, false); motes.transform.position = new Vector3(1, -8, 13);
        var ps = motes.AddComponent<ParticleSystem>();
        var main = ps.main; main.prewarm = true; main.startLifetime = 16; main.startSpeed = 0.15f; main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.maxParticles = 160; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new Color(1, 1, 1, 0.6f);
        var em = ps.emission; em.rateOverTime = 9;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(50, 14, 50);
        var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) });
        col.color = g;
        motes.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["Mote"];

        new GameObject("StageStyle").AddComponent<StageStyle>();
        var hud = new GameObject("RemakeHUD").AddComponent<RemakeHUD>();
        var palB = RemakePalette.All[Arg("-palette") ?? RemakeArtConfig.ChosenPalette];
        hud.you = palB.marker; hud.past = palB.haze;
        RemakePalette.ApplyLighting(palB);
        hud.font = font;
        hud.pill = Spr("pill"); hud.arrow = Spr("arrow"); hud.waitDot = Spr("waitDot"); hud.caret = Spr("caret"); hud.keycap = Spr("keycap");
        hud.check = Spr("check"); hud.pause = Spr("pause"); hud.restart = Spr("restart"); hud.vignette = Spr("vignette");
        hud.ring = Spr("ring"); hud.softDot = Spr("softDot"); hud.marker = Spr("marker");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // RemakeGame: the full campaign base. Same as RemakeBase but plays every remade level in order.
    static void BuildGame()
    {
        string path = $"{R}/Scenes/RemakeGame.unity";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset($"{R}/Scenes/RemakeBase.unity", path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var lm = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelManager>(true)).First();
        lm.singleLevelMode = false; lm.singleLevelBaseScene = ""; lm.singleLevelScene = "";
        lm.remakeCampaign = true; lm.remakeBaseScene = "RemakeGame"; lm.chaseDistance = 1.9f;
        EditorUtility.SetDirty(lm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void MountRabbit(GameObject ch, string v, bool phantom)
    {
        var old = ch.transform.Find("MainCharacter");
        if (old)   // keep the Animator alive (PhantomController.KillPhantom needs it); hide old meshes only
        {
            old.gameObject.SetActive(true);
            foreach (var smr in old.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.gameObject.SetActive(false);
        }
        var prev = ch.transform.Find("RabbitVisual"); if (prev) UnityEngine.Object.DestroyImmediate(prev.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/Rabbits/Rabbit_{v}{(phantom ? "_Phantom" : "")}.prefab");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ch.transform);
        inst.name = "RabbitVisual";
        inst.transform.localPosition = RabbitRig.MountOffset; inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one * 0.5f; // character roots are scaled x2
    }

    // ===================================================================== level scene
    static GameObject Piece(string name, Transform parent, Vector3 worldPos, Quaternion rot, Vector3 scale, params (string mesh, string mat)[] parts)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(worldPos, rot);
        if (parent) go.transform.SetParent(parent, true);
        go.transform.localScale = Vector3.Scale(go.transform.localScale, scale);
        foreach (var (mesh, mat) in parts)
        {
            var c = new GameObject(mesh); c.transform.SetParent(go.transform, false);
            c.AddComponent<MeshFilter>().sharedMesh = Kit[mesh];
            var r = c.AddComponent<MeshRenderer>(); r.sharedMaterial = M[mat];
            bool transparent = M[mat].renderQueue >= 2900;
            r.shadowCastingMode = transparent ? ShadowCastingMode.Off : ShadowCastingMode.On; r.receiveShadows = !transparent;
        }
        return go;
    }

    // child of a (possibly non-uniformly scaled) gameplay object but sized in world units
    static GameObject Attach(GameObject go, Transform parent)
    {
        var s = go.transform.lossyScale;
        go.transform.SetParent(parent, true);
        return go;
    }

    static void BuildLevel(string source, string sceneName)
    {
        string path = $"{R}/Scenes/{sceneName}.unity";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(source, path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        var art = new GameObject("RemakeArt").transform;
        var buttons = all.Select(t => t.GetComponent<Button>()).Where(b => b).ToList();
        var levers = all.Select(t => t.GetComponent<Lever>()).Where(l => l).ToList();
        var ctrl = new Dictionary<GameObject, (string kind, Component dev)>();
        foreach (var b in buttons) foreach (var ts in b.targetStates) ctrl[ts.target] = ("plate", b);
        foreach (var l in levers) foreach (var ts in l.targetStates) ctrl[ts.target] = ("lever", l);
        var fit = new List<Vector3>();
        var ghostsByDev = new Dictionary<Component, List<(GameObject target, GameObject ghost)>>();

        var original = all.SelectMany(t => t.GetComponents<Renderer>()).ToList();
        bool UnderGrid(Transform t) { for (var q = t.parent; q; q = q.parent) if (q.name == "Grid" || q.name == "Tilemap") return true; return false; }
        bool IsDevice(Transform t) => t.GetComponentInParent<Button>(true) || t.GetComponentInParent<Lever>(true) || t.GetComponentInParent<Box>(true) || t.GetComponentInParent<MovingObstacle>(true);

        // ---- tiles (anything with a mesh under Grid/Tilemap, at any depth: FirstFloor, wrongPath0, shortcut0/child, psuedoTile)
        foreach (var t in all.Where(t => UnderGrid(t) && t.GetComponent<MeshFilter>() && t.GetComponent<MeshRenderer>() && !t.name.StartsWith("gear") && !IsDevice(t)))
        {
            var go = t.gameObject;
            if (!go.activeSelf && (go.CompareTag("StartTile") || go.CompareTag("GoalTile") || go.name.StartsWith("Start") || go.name.StartsWith("Goal"))) continue;
            if (go.name.StartsWith("Stair"))
            {   // stairs keep their stepped mesh, drawn in the floor material of the stage
                foreach (var r in go.GetComponents<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(_ => M["Tile"]).ToArray();
                foreach (var q in go.GetComponentsInChildren<Transform>(true)) fit.Add(q.position);
                continue;
            }
            foreach (var r in go.GetComponents<Renderer>()) r.enabled = false;
            var wb = WorldBounds(t.GetComponent<MeshFilter>());
            var top = new Vector3(Mathf.Round(wb.center.x), Mathf.Round(wb.max.y * 10f) / 10f, Mathf.Round(wb.center.z));
            var parts = new List<(string, string)> { ("Tile", "Tile"), ("Pillar", "Pillar") };
            if (go.CompareTag("StartTile")) parts.Add(("StartDisc", "Start"));
            if (go.CompareTag("GoalTile")) { parts.Add(("GoalDisc", "Goal")); parts.Add(("GoalRing", "Goal")); }
            if (ctrl.TryGetValue(go, out var c)) parts.Add(("TileEdge", c.kind == "plate" ? "PlateRim" : "LeverRim"));
            Attach(Piece("Visual", null, top, Quaternion.identity, Vector3.one, parts.ToArray()), t);
            if (ctrl.TryGetValue(go, out var c2))
            {
                var gh = Piece($"Ghost_{go.name}", art, top + Vector3.up * 0.01f, Quaternion.identity, Vector3.one, ("TileEdge", c2.kind == "plate" ? "GhostPlate" : "GhostLever"));
                if (!ghostsByDev.ContainsKey(c2.dev)) ghostsByDev[c2.dev] = new();
                ghostsByDev[c2.dev].Add((go, gh));
            }
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f }) fit.Add(top + new Vector3(sx, 0, sz));
        }

        // ---- start / goal: the original stands the rabbit on a gear with a collider (its tile is inactive);
        //      draw that gear as a floor tile with the start or goal marker
        foreach (var t in all.Where(t => t.name.StartsWith("gear") && t.gameObject.activeSelf && t.GetComponent<BoxCollider>() && t.GetComponent<MeshFilter>()))
        {
            bool isStart = t.CompareTag("StartTile"), isGoal = t.CompareTag("GoalTile");
            var wb = WorldBounds(t.GetComponent<MeshFilter>());
            var top = new Vector3(Mathf.Round(wb.center.x), Mathf.Round(wb.max.y * 10f) / 10f, Mathf.Round(wb.center.z));
            var parts = new List<(string, string)> { ("Tile", "Tile"), ("Pillar", "Pillar") };
            if (isStart) parts.Add(("StartDisc", "Start"));
            if (isGoal) { parts.Add(("GoalDisc", "Goal")); parts.Add(("GoalRing", "Goal")); }
            foreach (var r in t.GetComponents<Renderer>()) r.enabled = false;
            Attach(Piece("Visual", null, top, Quaternion.identity, Vector3.one, parts.ToArray()), t);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f }) fit.Add(top + new Vector3(sx, 0, sz));
        }

        // ---- walls (Level/Structures): low visual bars; plate-controlled walls are barriers
        foreach (var t in all.Where(t => t.parent && t.parent.name == "Structures"))
        {
            var bc = t.GetComponent<BoxCollider>(); if (!bc) continue;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            var b = bc.bounds;
            if (t.name.StartsWith("Pillar"))
            {   // standing columns (L-004-1) stay columns: a capped lab pillar instead of a low wall bar
                var cap = new Vector3(Mathf.Round(b.center.x), b.max.y, Mathf.Round(b.center.z));
                float fw = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) / 1.9f, 0.4f, 1f);
                Attach(Piece("Visual", null, cap, Quaternion.identity, new Vector3(fw, 1, fw), ("Tile", "Wall"), ("Pillar", "Pillar")), t);
                fit.Add(cap);
                continue;
            }
            bool alongX = b.size.x >= b.size.z;
            float len = alongX ? b.size.x : b.size.z;
            var centre = new Vector3(b.center.x, 0f, b.center.z);
            var rot = alongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
            bool controlled = ctrl.ContainsKey(t.gameObject);
            var vis = Piece("Visual", null, centre, rot, new Vector3(len / 2f, 1, 1), (controlled ? "Wall" : "Wall", controlled ? "Barrier" : "Wall"));
            if (controlled)
            {
                // a glowing barrier taller than the bars so it reads as "raised by a plate"
                vis.transform.localScale = new Vector3(len / 2f, 1.6f, 0.5f);
            }
            Attach(vis, t);
            fit.Add(centre + Vector3.up * 0.7f);
        }

        // ---- plates
        foreach (var b in buttons)
        {
            foreach (var r in b.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            var p = new Vector3(b.transform.position.x, 0f, b.transform.position.z);
            Attach(Piece("RingVisual", null, p, Quaternion.identity, Vector3.one, ("PlateRing", "Dark")), b.transform.GetChild(0));
            var pad = Attach(Piece("PadVisual", null, p + Vector3.up * 0.09f, Quaternion.identity, Vector3.one, ("PlatePad", "PlatePad")), b.transform.GetChild(1));
            var dv = new GameObject($"Device_{b.name}").AddComponent<DeviceVisual>(); dv.transform.SetParent(art, false);
            dv.button = b; dv.pad = pad.GetComponentInChildren<MeshRenderer>(); dv.color = PlateCol;
            if (ghostsByDev.TryGetValue(b, out var gl)) { dv.targets = gl.Select(x => x.target).ToArray(); dv.ghosts = gl.Select(x => x.ghost).ToArray(); }
        }

        // ---- levers: base + handle (rides the original hinge child, so its tilt shows the state) + push arrow
        foreach (var l in levers)
        {
            foreach (var r in l.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var r in l.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false;
            var p = new Vector3(l.transform.position.x, 0f, l.transform.position.z);
            Attach(Piece("BaseVisual", null, p, Quaternion.identity, Vector3.one, ("LeverBase", "Wall")), l.transform);
            var handle = Piece("HandleVisual", null, new Vector3(p.x, 0.18f, p.z), l.transform.rotation, Vector3.one, ("LeverHandle", "LeverHandle"));
            handle.transform.SetParent(l.transform, true);
            var arrow = Piece("PushArrow", art, p, Quaternion.identity, Vector3.one * 1.4f, ("LeverArrow", "LeverArrow"));
            var dv = new GameObject($"Device_{l.name}").AddComponent<DeviceVisual>(); dv.transform.SetParent(art, false);
            dv.lever = l; dv.arrow = arrow.transform; dv.handle = handle.transform; dv.color = LeverCol;
            if (ghostsByDev.TryGetValue(l, out var gl)) { dv.targets = gl.Select(x => x.target).ToArray(); dv.ghosts = gl.Select(x => x.ghost).ToArray(); }
        }

        // ---- boxes: crate / half crate / core crate (BoxB), sized from the original mesh
        foreach (var box in all.Select(t => t.GetComponent<Box>()).Where(b => b))
        {
            var mf = box.GetComponent<MeshFilter>(); if (!mf) continue;
            var wb = WorldBounds(mf);
            bool core = box.GetComponent<MeshRenderer>().sharedMaterials.Any(m => m && m.name.Contains("Crystal"));
            bool half = wb.size.y < 1.5f;
            foreach (var r in box.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            var size = new Vector3(wb.size.x / 1.8f, wb.size.y / (half ? 1f : 2f), wb.size.z / 1.8f);
            var parts = core ? new[] { ("CoreCage", "CrateTrim"), ("CoreCrystal", "Core"), ("CoreCollar", "Crate") }
                      : half ? new[] { ("CrateHalfBody", "Crate"), ("CrateHalfFrame", "CrateTrim"), ("CrateHalfMark", "CrateMark") }
                             : new[] { ("CrateBody", "Crate"), ("CrateFrame", "CrateTrim"), ("CrateMark", "CrateMark") };
            Attach(Piece("Visual", null, wb.center, Quaternion.identity, size, parts), box.transform);
            fit.Add(wb.center);
        }

        // ---- moving blocks: heavy piston blocks with a hazard band
        foreach (var mo in all.Select(t => t.GetComponent<MovingObstacle>()).Where(m => m))
        {
            var mf = mo.GetComponent<MeshFilter>(); if (!mf) continue;
            var wb = WorldBounds(mf);
            foreach (var r in mo.GetComponents<MeshRenderer>()) r.enabled = false;
            Attach(Piece("Visual", null, wb.center, Quaternion.identity, wb.size / 2f, ("PistonBody", "Piston"), ("PistonStripe", "Hazard"), ("PistonTrim", "CrateTrim")), mo.transform);
        }

        // ---- walls that carry a laser: vented panels
        foreach (var t in all.Where(t => t.name.StartsWith("WallWithLaser") && t.GetComponent<MeshFilter>()))
        {
            var wb = WorldBounds(t.GetComponent<MeshFilter>());
            foreach (var r in t.GetComponents<MeshRenderer>()) r.enabled = false;
            bool alongX = wb.size.x >= wb.size.z;
            var rot = alongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
            var size = alongX ? new Vector3(wb.size.x / 2f, wb.size.y / 2f, wb.size.z / 0.5f) : new Vector3(wb.size.z / 2f, wb.size.y / 2f, wb.size.x / 0.5f);
            Attach(Piece("Visual", null, wb.center, rot, size, ("LaserWallBody", "Panel"), ("LaserWallFrame", "CrateTrim"), ("LaserWallVent", "CrateMark")), t);
        }

        // ---- lasers: emitter head where the original model was, glowing beam with a soft halo
        foreach (var laser in all.Select(t => t.GetComponent<Laser>()).Where(l => l))
        {
            var lr = laser.GetComponent<LineRenderer>();
            lr.sharedMaterial = M["Beam"]; lr.startColor = lr.endColor = Color.white;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false; lr.textureMode = LineTextureMode.Stretch;
            var glow = new GameObject("BeamGlow"); glow.transform.SetParent(laser.transform, false);
            var glr = glow.AddComponent<LineRenderer>(); glr.sharedMaterial = M["BeamGlow"]; glr.positionCount = 2;
            glr.startWidth = glr.endWidth = 0.42f; glr.startColor = glr.endColor = Color.white; glr.useWorldSpace = true;
            glr.shadowCastingMode = ShadowCastingMode.Off; glr.receiveShadows = false; glr.textureMode = LineTextureMode.Stretch;
            glow.AddComponent<BeamGlow>().src = lr;

            var root = laser.transform.parent;
            if (!root) continue;
            var model = root.GetComponentsInChildren<MeshFilter>(true).Where(f => !f.transform.IsChildOf(laser.transform) && f.transform != root && f.sharedMesh).ToList();
            bool shown = model.Any(f => f.gameObject.activeInHierarchy);
            foreach (var f in model) { var r = f.GetComponent<MeshRenderer>(); if (r) r.enabled = false; }
            if (!shown || model.Count == 0) continue;
            var mb = WorldBounds(model[0]); foreach (var f in model.Skip(1)) mb.Encapsulate(WorldBounds(f));
            float sc = Mathf.Clamp(Mathf.Max(mb.size.x, mb.size.y, mb.size.z) / 0.62f, 0.7f, 1.4f);
            var rot = Quaternion.FromToRotation(Vector3.up, laser.transform.forward);
            Attach(Piece("EmitterVisual", null, mb.center, rot, Vector3.one * sc, ("EmitterBody", "CrateTrim"), ("EmitterCollar", "Piston"), ("EmitterLens", "Lens")), root);
        }

        // keep the epilogue's scripted spot light (LevelManager finds it by name); drop the other point/spot lights
        foreach (var t in all.Where(t => t.GetComponent<Light>() && t.GetComponent<Light>().type != LightType.Directional && t.name != "SpotLight")) t.gameObject.SetActive(false);
        // the scientist in the epilogue keeps his model, drawn with the remake's toon shading
        foreach (var r in original.Where(r => r && r.name == "DeadScientist"))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = Toon($"Scientist{i}", mats[i] && mats[i].HasProperty("_Color") ? mats[i].color : Color.grey, 0.15f, null, new Color(0.5f, 0.55f, 0.65f), 0.2f);
            r.sharedMaterials = mats;
        }
        // anything original still drawn (tilemap sprites, decorative pillars, gears) is hidden
        foreach (var r in original.Where(r => r && r.enabled && !(r is LineRenderer) && r.name != "DeadScientist" && !r.name.StartsWith("Stair")))
        {
            string rp = r.name; for (var q = r.transform.parent; q; q = q.parent) rp = q.name + "/" + rp;
            Debug.Log($"[Builder] {sceneName}: hid leftover {rp} ({r.GetType().Name}) active={r.gameObject.activeInHierarchy}");
            r.enabled = false;
        }
        foreach (var t in all.Where(t => t.name.StartsWith("gear"))) foreach (var r in t.GetComponents<MeshRenderer>()) r.enabled = false;

        var anchor = all.First(t => t.name == "CameraOffsetAnchor");
        // the prologue is a tall tower; frame its upper part (where the phantom and the plates are) instead of the whole drop
        if (sceneName == "R-Prologue") fit = fit.Where(q => q.y >= 6f).ToList();
        FitCamera(anchor, fit, 40f, 16f / 9f, 56f, 315f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // world AABB of a mesh, valid for inactive objects too (Renderer.bounds is empty while inactive)
    static Bounds WorldBounds(MeshFilter mf)
    {
        var b = mf.sharedMesh.bounds; var t = mf.transform;
        var wb = new Bounds(t.TransformPoint(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            wb.Encapsulate(t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return wb;
    }

    static void FitCamera(Transform anchor, List<Vector3> pts, float fov, float aspect, float pitch, float yaw)
    {
        var go = new GameObject("~fit", typeof(Camera)); var cam = go.GetComponent<Camera>();
        cam.fieldOfView = fov; cam.aspect = aspect;
        var rot = Quaternion.Euler(pitch, yaw, 0); go.transform.rotation = rot;
        float xMin = 0.05f, xMax = 0.95f, yMin = 0.05f, yMax = 0.86f;   // only the slim timeline at the top
        Vector3 target = pts.Aggregate(Vector3.zero, (s, p) => s + p) / pts.Count; float dist = 30;
        for (int it = 0; it < 50; it++)
        {
            go.transform.position = target - rot * Vector3.forward * dist;
            var v = pts.Select(cam.WorldToViewportPoint).ToList();
            float x0 = v.Min(p => p.x), x1 = v.Max(p => p.x), y0 = v.Min(p => p.y), y1 = v.Max(p => p.y);
            dist *= Mathf.Lerp(1f, Mathf.Max((x1 - x0) / (xMax - xMin), (y1 - y0) / (yMax - yMin)), 0.7f);
            float halfH = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * dist;
            target += rot * Vector3.right * (((x0 + x1) - (xMin + xMax)) * halfH * aspect) * 0.7f + rot * Vector3.up * (((y0 + y1) - (yMin + yMax)) * halfH) * 0.7f;
        }
        anchor.SetPositionAndRotation(go.transform.position, rot);
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log($"[Builder] camera {anchor.position} dist {dist:0.0}");
    }
}
