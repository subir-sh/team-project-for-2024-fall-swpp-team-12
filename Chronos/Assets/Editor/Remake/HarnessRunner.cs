using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Entry point for CLI verification:
//   Unity.exe -batchmode -projectPath <p> -executeMethod HarnessRunner.Run
//             -remakeHarness -harnessScene <base scene path> -harnessLevel <scene name>
//             -harnessScenario <name> -harnessOut <dir>
// Order is deliberate: audio is muted + verified inside Unity BEFORE any scene is opened.
public static class HarnessRunner
{
    public static void Run()
    {
        // 1) Audio safety, applied in edit mode before any scene is loaded.
        EditorUtility.audioMasterMute = true;
        AudioListener.volume = 0f;
        AudioListener.pause = true;

        bool ok = EditorUtility.audioMasterMute && AudioListener.volume == 0f && AudioListener.pause;
        Debug.Log($"[Harness] AUDIO-PRECHECK(edit, before scene open) editorMasterMute={EditorUtility.audioMasterMute} " +
                  $"listenerVolume={AudioListener.volume} listenerPause={AudioListener.pause} ok={ok}");
        if (!ok)
        {
            Debug.LogError("[Harness] audio pre-check failed; refusing to open scene");
            EditorApplication.Exit(2);
            return;
        }

        // 2) Only now open the scene.
        string scene = Arg("-harnessScene") ?? "Assets/Scenes/LevelBaseScene.unity";
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        Debug.Log($"[Harness] opened scene {scene}");

        // 3) Background Play Mode (the runtime RemakeHarness drives it and exits the editor).
        EditorApplication.EnterPlaymode();
    }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
