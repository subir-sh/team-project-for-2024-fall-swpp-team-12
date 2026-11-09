using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only: dumps hierarchy + components of the scenes the remake builds on.
public static class SceneInspector
{
    public static void Dump()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { "Assets/Scenes/LevelBaseScene.unity", "Assets/Scenes/L-001/L-001-3.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine("##### " + path);
            sb.AppendLine($"skybox={RenderSettings.skybox?.name} ambient={RenderSettings.ambientMode} fog={RenderSettings.fog} {RenderSettings.fogColor}");
            foreach (var root in scene.GetRootGameObjects()) Walk(root.transform, 0, sb);
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../../captures/scene_dump.txt"), sb.ToString());
        Debug.Log("[Inspector] wrote scene_dump.txt");
    }

    static void Walk(Transform t, int d, StringBuilder sb)
    {
        if (d > 6) return;
        var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform))
            .Select(c =>
            {
                string s = c.GetType().Name;
                if (c is Renderer r) s += "[" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name + ":" + m.shader.name : "null")) + "]";
                if (c is Camera cam) s += $"(fov={cam.fieldOfView} ortho={cam.orthographic} clear={cam.clearFlags})";
                if (c is Light l) s += $"({l.type} i={l.intensity} c={l.color})";
                if (c is RectTransform rt) s += $"({rt.anchorMin}-{rt.anchorMax} pos={rt.anchoredPosition} size={rt.sizeDelta})";
                return s;
            });
        sb.AppendLine($"{new string(' ', d * 2)}{(t.gameObject.activeSelf ? "" : "(off) ")}{t.name} tag={t.tag} layer={t.gameObject.layer} " +
                      $"p={t.localPosition} r={t.localEulerAngles} s={t.localScale} :: {string.Join(" ", comps)}");
        foreach (Transform c in t) Walk(c, d + 1, sb);
    }
}
