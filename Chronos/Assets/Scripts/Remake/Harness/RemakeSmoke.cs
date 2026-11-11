using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Smoke test for the PLAYER build, inert unless launched with -remakeSmoke <outDir>:
//   ChronosRemake.exe -batchmode -remakeSmoke <dir> [-remakeSmokeStart <campaign index>] -logFile <log>
// Audio is silenced before the first scene loads; the game then runs normally, and after a few
// seconds the main camera is rendered off-screen to <dir>/player.png and the app quits.
public class RemakeSmoke : MonoBehaviour
{
    static string outDir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void PreSceneAudio()
    {
        outDir = Arg("-remakeSmoke");
        if (outDir == null) return;
        AudioListener.volume = 0f;
        AudioListener.pause = true;
        Debug.Log($"[Smoke] AUDIO-PRECHECK(player, before scene load) listenerVolume={AudioListener.volume} listenerPause={AudioListener.pause} ok={AudioListener.volume == 0f && AudioListener.pause}");
        if (int.TryParse(Arg("-remakeSmokeStart"), out var i)) LevelManager.ForcedCampaignStart = i;   // also keeps the real save untouched
        else LevelManager.ForcedCampaignStart = 1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (outDir == null) return;
        var go = new GameObject("~RemakeSmoke"); DontDestroyOnLoad(go); go.AddComponent<RemakeSmoke>();
    }

    void Update()
    {
        if (AudioListener.volume != 0f || !AudioListener.pause) { AudioListener.volume = 0f; AudioListener.pause = true; }
    }

    IEnumerator Start()
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 10f) yield return null;
        var scenes = string.Join(",", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name));
        bool tex = RemakePalette.LoadTex("dt_labfloor") != null && RemakePalette.LoadTex("ov_moss") != null;
        Debug.Log($"[Smoke] scenes={scenes} style={StageStyle.Current} runtimeTextures={tex} labSet={(GameObject.Find("LabSet") != null)}");
        var cam = Camera.main;
        if (cam)
        {
            var rt = new RenderTexture(1920, 1080, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var img = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); img.Apply();
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "player.png"), img.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null;
            Debug.Log($"[Smoke] captured {Path.Combine(outDir, "player.png")}");
        }
        Application.Quit(tex ? 0 : 1);
    }

    static string Arg(string n)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1];
        return null;
    }
}
