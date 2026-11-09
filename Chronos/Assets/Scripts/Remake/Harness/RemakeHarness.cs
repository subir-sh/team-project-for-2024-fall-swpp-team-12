using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime side of the CLI verification harness. Inert unless the editor was launched
// with -remakeHarness. Drives the real game through InputManager's events (the same
// entry points the keyboard uses), checks state after every turn and writes captures.
public class RemakeHarness : MonoBehaviour
{
    static bool active;
    static string outDir, scenario, levelScene;
    readonly StringBuilder report = new();
    int failures;
    int turn;
    bool cleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void PreSceneAudio()
    {
        if (!HasArg("-remakeHarness")) return;
        active = true;
        // Re-assert audio safety inside Play Mode before the first scene loads.
        AudioListener.volume = 0f;
        AudioListener.pause = true;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.audioMasterMute = true;
        bool mute = UnityEditor.EditorUtility.audioMasterMute;
#else
        bool mute = true;
#endif
        Debug.Log($"[Harness] AUDIO-PRECHECK(play, before scene load) editorMasterMute={mute} " +
                  $"listenerVolume={AudioListener.volume} listenerPause={AudioListener.pause}");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (!active) return;
        outDir = Arg("-harnessOut") ?? Path.Combine(Application.dataPath, "../HarnessOut");
        scenario = Arg("-harnessScenario") ?? "solve";
        levelScene = Arg("-harnessLevel") ?? "L-001-3";
        Directory.CreateDirectory(outDir);
        LevelManager.ForcedSingleLevelIndex = 3;
        LevelManager.ForcedSingleLevelScene = levelScene;
        Time.captureFramerate = 30; // deterministic 1/30 s steps, independent of batch-mode speed
        var go = new GameObject("~RemakeHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<RemakeHarness>();
    }

    void Update()
    {
        if (Time.realtimeSinceStartup > 600f)
        {
            Debug.LogError("[Harness] WATCHDOG: run exceeded 600 s wall clock");
            File.WriteAllText(Path.Combine(outDir, $"report_{scenario}.txt"), report + "\nWATCHDOG TIMEOUT\n");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(3);
#endif
        }
        // Keep the listener silent for the whole run, even if game code toggles pause.
        if (AudioListener.volume != 0f) { Log($"WARN listener volume changed to {AudioListener.volume}; forcing 0"); AudioListener.volume = 0f; }
    }

    IEnumerator Start()
    {
        LevelManager.OnLevelCleared += () => cleared = true;
        Log($"scenario={scenario} level={levelScene} out={outDir} unity={Application.unityVersion}");

        // Wait for the additive level to finish loading and the camera to settle.
        float t0 = Time.time;
        while (!SceneManager.GetSceneByName(levelScene).isLoaded && Time.time - t0 < 20f) yield return null;
        Check(SceneManager.GetSceneByName(levelScene).isLoaded, $"level scene '{levelScene}' loaded");
        yield return Wait(2.0f);
        DismissMonologue();
        yield return Wait(0.5f);

        IEnumerator run = scenario switch
        {
            "solve" => Solve(),
            "shots" => Shots(),
            "lineup" => Lineup(),
            "final" => Solve(true),
            _ => Solve(),
        };
        yield return run;

        File.WriteAllText(Path.Combine(outDir, $"report_{scenario}.txt"), report.ToString());
        Debug.Log($"[Harness] DONE failures={failures}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
#endif
    }

    // ------------------------------------------------------------------ scenarios
    // L-001-3: record the walk to the pressure plate, rewind to the junction, then cross
    // the bridge tile that only exists while the phantom holds the plate.
    static readonly string[] Record = { "w", "w", "w", "d", "d", "w", "w", "w", "r", "r", "r", "r" };
    const int RewindSteps = 9; // back to the junction (turn 3)
    static readonly string[] Cross = { "a", "a", "w", "w", "w", "w", "d", "d", "w", "w", "w" };

    IEnumerator Solve(bool extras = false)
    {
        var plate = FindObjectsByType<Button>().FirstOrDefault();
        Check(plate != null, "pressure plate present");
        var bridge = plate.targetStates.Select(s => s.target).FirstOrDefault();
        Log($"plate at {plate.transform.position} controls {(bridge ? bridge.name + " @" + bridge.transform.position : "nothing")}");
        Check(bridge != null && !bridge.activeSelf, "bridge tile starts hidden");
        yield return Capture("01_start");
        if (extras)
        {
            // asset close-ups in the live scene (idle state): plate, ghost bridge, start, goal, background
            yield return CloseUp("asset_plate_idle", Flat0(plate.transform.position));
            yield return CloseUp("asset_bridge_ghost", Flat0(bridge.transform.position));
            yield return CloseUp("asset_start", new Vector3(1, 0, 1));
            yield return CloseUp("asset_goal", new Vector3(1, 0, 21), 1.0f);
            yield return Background("asset_background");
        }

        // ---- 1. record actions
        Log("== PHASE record");
        for (int i = 0; i < Record.Length; i++)
        {
            yield return Turn(Record[i], i == 4 ? "hop" : null);
            if (i == 1) yield return Capture("02_recording");
        }
        Check(plate.isPressed, "player is holding the plate after recording");
        Check(bridge.activeSelf, "bridge visible while plate held");
        Check(PlayerController.playerController.listCommandLog.Count - 1 == Record.Length, $"command log has {Record.Length} turns");

        // ---- 2. rewind
        Log("== PHASE rewind");
        InputManager.inputManager.OnTimeRewindModeToggle?.Invoke();
        yield return Wait(0.4f);
        Check(PlayerController.playerController.isTimeRewinding, "entered rewind mode");
        for (int i = 0; i < RewindSteps; i++)
        {
            InputManager.inputManager.OnTimeRewindControl?.Invoke("q");
            yield return Wait(0.25f);
            if (i == 4) yield return Capture("03_rewind_mid");
        }
        Check(TurnManager.turnManager.rewindTurnCount == RewindSteps, $"rewound {RewindSteps} turns (count={TurnManager.turnManager.rewindTurnCount})");
        yield return Capture("04_rewind_target");
        InputManager.inputManager.OnTimeRewindModeToggle?.Invoke();
        yield return Wait(0.4f);
        var ph = PhantomController.phantomController;
        Check(ph.isPhantomExisting && ph.gameObject.activeSelf, "phantom spawned on leaving rewind");
        Check(!plate.isPressed && !bridge.activeSelf, "plate released & bridge hidden after rewind");
        Log($"phantom at {ph.transform.position}, player at {PlayerController.playerController.transform.position}");

        // ---- 3. phantom replays, 4. device works, 5. finish
        Log("== PHASE replay");
        bool sawPhantomPress = false, crossedBridge = false;
        for (int i = 0; i < Cross.Length; i++)
        {
            yield return Turn(Cross[i]);
            var p = PlayerController.playerController.transform.position;
            if (plate.isPressed && ph.isPhantomExisting && Flat(ph.transform.position, plate.transform.position)) sawPhantomPress = true;
            if (Flat(p, bridge.transform.position))
            {
                crossedBridge = bridge.activeSelf;
                yield return Capture("05_phantom_holds_plate");
                if (extras)
                {
                    yield return CloseUp("asset_plate_pressed_by_phantom", Flat0(plate.transform.position));
                    yield return CloseUp("asset_bridge_active_player", Flat0(bridge.transform.position));
                }
            }
        }
        Check(sawPhantomPress, "phantom replayed into the plate and pressed it");
        Check(crossedBridge, "player stood on the bridge while phantom held the plate");
        // parity with the original game: the phantom expires once its recorded actions run out
        Check(!ph.isPhantomExisting && !ph.gameObject.activeSelf, "phantom expired after replaying its recorded actions");
        Check(!PlayerController.playerController.playerRenderers.Any(r => r is SkinnedMeshRenderer), "old model renderers excluded from death-blink list");
        yield return Wait(1.0f);
        Check(cleared, "level cleared (LevelManager.OnLevelCleared)");
        yield return Capture("06_clear");
    }

    // Rabbit design review: every variant in the real game camera (hop film + puzzle moment)
    // plus a close studio shot of player/phantom side by side.
    IEnumerator Lineup()
    {
        string[] vs = { "A", "B", "C" };
        foreach (var v in vs)
        {
            SwapRabbits(v);
            InputManager.inputManager.OnReset?.Invoke();
            yield return Wait(1.0f);
            yield return Turn("w", $"hop_{v}");
        }

        // reach the key puzzle moment once: player on the bridge, phantom holding the plate
        InputManager.inputManager.OnReset?.Invoke();
        yield return Wait(1.0f);
        foreach (var c in Record) yield return Turn(c);
        InputManager.inputManager.OnTimeRewindModeToggle?.Invoke();
        yield return Wait(0.3f);
        for (int i = 0; i < RewindSteps; i++) { InputManager.inputManager.OnTimeRewindControl?.Invoke("q"); yield return Wait(0.2f); }
        InputManager.inputManager.OnTimeRewindModeToggle?.Invoke();
        yield return Wait(0.3f);
        for (int i = 0; i < 6; i++) yield return Turn(Cross[i]);
        var plate = FindObjectsByType<Button>().First();
        Check(plate.isPressed && PhantomController.phantomController.isPhantomExisting, "lineup: phantom holds the plate");
        foreach (var v in vs)
        {
            SwapRabbits(v);
            yield return Wait(0.6f);
            yield return Capture($"lineup_{v}", 1920, 1080, true);
            LogVp($"lineup_{v}_player", PlayerController.playerController.transform.position);
            LogVp($"lineup_{v}_phantom", PhantomController.phantomController.transform.position);
        }

        // studio: player + phantom side by side, facing a close camera
        var studioCam = new GameObject("~StudioCam").AddComponent<Camera>();
        studioCam.fieldOfView = 26; studioCam.clearFlags = CameraClearFlags.Skybox;
        studioCam.enabled = false;
        Vector3 o = new(400, 0, 400);
#if UNITY_EDITOR
        var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Remake/Prefabs/Tile_2.prefab");
        Instantiate(tile, o + new Vector3(-1, 0, 0), Quaternion.identity);
        Instantiate(tile, o + new Vector3(1, 0, 0), Quaternion.identity);
#endif
        foreach (var v in vs)
        {
            var a = Instantiate(LoadRabbit(v, false), o + new Vector3(-0.95f, 0, 0), Quaternion.Euler(0, 200, 0));
            var b = Instantiate(LoadRabbit(v, true), o + new Vector3(1.05f, 0, 0), Quaternion.Euler(0, 160, 0));
            studioCam.transform.position = o + new Vector3(1.6f, 2.1f, -5.6f);
            studioCam.transform.LookAt(o + new Vector3(0.05f, 0.62f, 0));
            yield return null; yield return null;
            CaptureNow(Path.Combine(outDir, $"studio_{v}.png"), 1600, 900, false, studioCam);
            Destroy(a); Destroy(b);
            yield return null;
        }
    }

    static GameObject LoadRabbit(string v, bool phantom)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Remake/Prefabs/Rabbits/Rabbit_{v}{(phantom ? "_Phantom" : "")}.prefab");
#else
        return null;
#endif
    }

    void SwapRabbits(string v)
    {
        foreach (var (ch, phantom) in new[] { (PlayerController.playerController.transform, false), (PhantomController.phantomController.transform, true) })
        {
            var old = ch.Find("RabbitVisual");
            if (old) { old.name = "~old"; Destroy(old.gameObject); }
            var inst = Instantiate(LoadRabbit(v, phantom), ch);
            inst.name = "RabbitVisual";
            inst.transform.localPosition = RabbitRig.MountOffset;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * RabbitRig.MountScale;
        }
        var pc = PlayerController.playerController;
        // keep the death-blink renderer list valid (old visual is destroyed at end of frame)
        pc.playerRenderers = pc.GetComponentsInChildren<Renderer>()
            .Where(r => r.GetComponentsInParent<Transform>(true).All(t => t.name != "~old")).ToArray();
        Log($"rabbit design -> {v}");
    }

    void LogVp(string name, Vector3 world)
    {
        var vp = Camera.main.WorldToViewportPoint(world + Vector3.up * 0.2f);
        Log($"VP {name} {vp.x:F4} {vp.y:F4}");
    }

    IEnumerator Shots()
    {
        yield return Capture("shot_start");
    }

    // ------------------------------------------------------------------ helpers
    IEnumerator Turn(string cmd, string filmPrefix = null)
    {
        turn++;
        if (filmPrefix != null) LogVp(filmPrefix + "_start", PlayerController.playerController.transform.position);
        InputManager.inputManager.OnMovementControl?.Invoke(cmd);
        int f = 0;
        yield return null;
        float t0 = Time.time;
        while ((TurnManager.turnManager.CLOCK || PlayerController.playerController.isBlinking) && Time.time - t0 < 10f)
        {
            if (filmPrefix != null && f < 40) yield return Capture($"{filmPrefix}_{f++:00}", 1600, 900, false);
            else yield return null;
        }
        if (filmPrefix != null) for (int k = 0; k < 6; k++) yield return Capture($"{filmPrefix}_{f++:00}", 1600, 900, false);
        yield return Wait(0.15f);
        if (filmPrefix != null) LogVp(filmPrefix + "_end", PlayerController.playerController.transform.position);
        var p = PlayerController.playerController.transform.position;
        var ph = PhantomController.phantomController;
        Log($"turn {turn,2} '{cmd}' player={Fmt(p)} phantom={(ph.isPhantomExisting ? Fmt(ph.transform.position) : "-")} " +
            $"plate={(FindObjectsByType<Button>().FirstOrDefault()?.isPressed)}");
    }

    static Vector3 Flat0(Vector3 v) => new(v.x, 0f, v.z);

    Camera closeCam;
    Camera CloseCam()
    {
        if (closeCam) return closeCam;
        closeCam = new GameObject("~CloseCam").AddComponent<Camera>();
        closeCam.enabled = false; closeCam.clearFlags = CameraClearFlags.Skybox; closeCam.farClipPlane = 400;
        return closeCam;
    }

    // Close-up from the same viewing direction as the game camera (yaw 315), a bit lower.
    IEnumerator CloseUp(string name, Vector3 target, float lift = 0.5f)
    {
        var cam = CloseCam();
        cam.fieldOfView = 32;
        var rot = Quaternion.Euler(38, 315, 0);
        cam.transform.SetPositionAndRotation(target + Vector3.up * lift - rot * Vector3.forward * 7.5f, rot);
        yield return null;
        CaptureNow(Path.Combine(outDir, name + ".png"), 1200, 900, false, cam);
    }

    IEnumerator Background(string name)
    {
        var cam = CloseCam();
        cam.fieldOfView = 60;
        cam.transform.position = new Vector3(-8, 6, 4);
        cam.transform.LookAt(new Vector3(-40, -40, 40));
        yield return null;
        CaptureNow(Path.Combine(outDir, name + ".png"), 1600, 900, false, cam);
    }

    static bool Flat(Vector3 a, Vector3 b) => Mathf.Abs(a.x - b.x) < 0.1f && Mathf.Abs(a.z - b.z) < 0.1f;
    static string Fmt(Vector3 v) => $"({v.x:0.#},{v.y:0.#},{v.z:0.#})";
    static IEnumerator Wait(float s) { float t = Time.time; while (Time.time - t < s) yield return null; }

    void DismissMonologue()
    {
        var sm = ScenarioManager.scenarioManager;
        if (sm != null && sm.isReadingMonologue) { sm.isLockedToRead = false; sm.isReadingMonologue = false; Log("monologue dismissed"); }
    }

    void Check(bool cond, string what)
    {
        if (!cond) failures++;
        Log($"{(cond ? "PASS" : "FAIL")} {what}");
    }

    void Log(string s)
    {
        report.AppendLine(s);
        Debug.Log("[Harness] " + s);
    }

    public IEnumerator Capture(string name, int w = 1600, int h = 900, bool withUI = true)
    {
        yield return null; // WaitForEndOfFrame never fires in batch mode; render manually instead
        CaptureNow(Path.Combine(outDir, name + ".png"), w, h, withUI);
    }

    public static void CaptureNow(string path, int w, int h, bool withUI, Camera cam = null)
    {
        cam ??= Camera.main;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.aspect = (float)w / h;

        var swapped = new List<(Canvas c, RenderMode m, Camera wc, float d)>();
        if (withUI)
        {
            foreach (var c in FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || !c.isActiveAndEnabled || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                swapped.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.1f + (50 - Mathf.Clamp(c.sortingOrder, 0, 49)) * 0.002f;
            }
            Canvas.ForceUpdateCanvases();
            foreach (var hud in FindObjectsByType<RemakeHUD>()) hud.PlaceWorldTags();
            Canvas.ForceUpdateCanvases();
        }
        cam.Render();
        foreach (var s in swapped) { s.c.renderMode = s.m; s.c.worldCamera = s.wc; s.c.planeDistance = s.d; }
        cam.targetTexture = prevTarget;
        cam.ResetAspect();

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
    }

    static bool HasArg(string n) => Environment.GetCommandLineArgs().Contains(n);
    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
