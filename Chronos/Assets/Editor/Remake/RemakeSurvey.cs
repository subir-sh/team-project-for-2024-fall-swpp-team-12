using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Lists every renderer in each original level that the remake builder does not restyle yet.
public static class RemakeSurvey
{
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/L-001", "Assets/Scenes/L-002", "Assets/Scenes/L-003", "Assets/Scenes/L-004", "Assets/Scenes/ScriptScenes" })
                     .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine("## " + scene.name);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var t = r.transform; string p = t.name;
                    for (var q = t.parent; q; q = q.parent) p = q.name + "/" + p;
                    bool handled = p.Contains("/Tilemap/") || p.Contains("/Structures/") || r.GetComponentInParent<Button>(true) || r.GetComponentInParent<Lever>(true);
                    if (handled) continue;
                    var comps = string.Join(",", r.GetComponentsInParent<MonoBehaviour>(true).Select(m => m ? m.GetType().Name : "null").Distinct());
                    var mf = r.GetComponent<MeshFilter>();
                    sb.AppendLine($"{(r.gameObject.activeInHierarchy ? "+" : "-")} {p} [{r.GetType().Name} {(mf && mf.sharedMesh ? mf.sharedMesh.name : "")}] mats={string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null"))} comps={comps} pos={t.position} scale={t.lossyScale}");
                }
        }
        System.IO.File.WriteAllText("../captures/survey.txt", sb.ToString());
        var hb = new StringBuilder();
        foreach (var n in new[] { "Prologue", "Event", "Epilogue" })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/ScriptScenes/{n}.unity", OpenSceneMode.Single);
            hb.AppendLine("## " + n);
            void Dump(Transform t, int d)
            {
                var comps = string.Join(",", t.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name));
                var bc = t.GetComponent<BoxCollider>();
                hb.AppendLine($"{new string(' ', d * 2)}{(t.gameObject.activeSelf ? "" : "(off) ")}{t.name} [{comps}] pos={t.position}{(bc ? " top=" + bc.bounds.max.y.ToString("0.0") : "")}");
                if (d < 4) foreach (Transform c in t) Dump(c, d + 1);
            }
            foreach (var r in scene.GetRootGameObjects()) Dump(r.transform, 0);
        }
        System.IO.File.WriteAllText("../captures/survey_story.txt", hb.ToString());
        Debug.Log("[Survey] done");
    }
}
