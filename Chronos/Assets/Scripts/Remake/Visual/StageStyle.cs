using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Picks the world look for each stage from the story (see ScenarioManager monologues):
//  Prologue, ch.1 (L-001)  LA  cold dead lab: the master's facility where Model Chronos wakes; records of the failed machine
//  ch.2 (L-002)            LC  rusted clockwork lab: Lucy, the machine's "hot beam", small actuators, the people who left
//  ch.3 (L-003)            LD  dark lab with rusted brass: "time to look down", the lower levels, a hidden passage
//  Event, L-004-1, L-004-2 LB  overgrown lab: the blackout curtain torn open, wind and the sunken land pour in
//  L-004-3, Epilogue       LA  the old facility found intact: back where it all began
// Applied whenever a level scene is loaded (additively, by LevelManager).
public class StageStyle : MonoBehaviour
{
    public static string For(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return "LA";
        string id = scene.StartsWith("L-") || scene.StartsWith("R-") ? scene.Substring(2) : scene;   // "001-3" from L-001-3 / R-001-3
        if (id == "Event") return "LB";
        if (id == "Prologue" || id == "Epilogue") return "LA";
        if (id.StartsWith("001")) return "LA";
        if (id.StartsWith("002")) return "LC";
        if (id.StartsWith("003")) return "LD";
        if (id == "004-1" || id == "004-2") return "LB";
        return "LA";
    }

    public static string Current { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    void Awake()
    {
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowCascades = 0;
        QualitySettings.shadowResolution = ShadowResolution.Low;
    }
#endif

    void OnEnable() => SceneManager.sceneLoaded += OnLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;

    void OnLoaded(Scene s, LoadSceneMode mode)
    {
        if (s.name.StartsWith("Remake") || s.name.StartsWith("LevelBase")) return;
        StartCoroutine(ApplyNextFrames(For(s.name), s));
    }

    IEnumerator ApplyNextFrames(string style, Scene scene)
    {
        yield return null; yield return null;   // let the level offset itself onto the previous goal first
        Current = style;
        RemakePalette.Apply(style, scene);
        Debug.Log($"[Harness] stage style {scene.name} -> {style}");
    }
}
