using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only: dumps every campaign level's geometry and devices as JSON (captures/levels/<scene>.json)
// for planning solutions. Coordinates are world units of the original scene (tile pitch 2).
public static class LevelDump
{
    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";
    static string S(string s) => "\"" + s.Replace("\"", "'") + "\"";

    static Bounds WB(Transform t)
    {
        var mf = t.GetComponent<MeshFilter>(); var bc = t.GetComponent<BoxCollider>();
        Bounds lb = bc ? new Bounds(bc.center, bc.size) : mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
        var wb = new Bounds(t.TransformPoint(lb.center), Vector3.zero);
        for (int i = 0; i < 8; i++) wb.Encapsulate(t.TransformPoint(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return wb;
    }

    public static void Run()
    {
        System.IO.Directory.CreateDirectory("../captures/levels");
        var levels = new[] { "L-001/L-001-1", "L-001/L-001-2", "L-001/L-001-3", "L-001/L-001-4", "L-001/L-001-5", "L-002/L-002-1", "L-002/L-002-2", "L-002/L-002-3", "L-002/L-002-4",
                             "L-003/L-003-1", "L-003/L-003-2", "L-003/L-003-3", "L-003/L-003-4", "L-004/L-004-1", "L-004/L-004-2", "L-004/L-004-3", "ScriptScenes/Event" };
        foreach (var lv in levels)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{lv}.unity", OpenSceneMode.Single);
            foreach (var tm in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))) tm.RefreshAllTiles();
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            string Path(Transform t) { var p = t.name; for (var q = t.parent; q; q = q.parent) p = q.name + "/" + p; return p; }
            bool ActiveChain(Transform t) { for (var q = t; q; q = q.parent) if (!q.gameObject.activeSelf) return false; return true; }
            var sb = new StringBuilder("{\n");
            var items = new List<string>();
            foreach (var t in all)
            {
                var bc = t.GetComponent<Collider>();
                string kind = t.GetComponent<Button>() ? "button" : t.GetComponent<Lever>() ? "lever" : t.GetComponent<Box>() ? "box" : t.GetComponent<MovingObstacle>() ? "moving"
                            : t.GetComponent<Laser>() ? "laser" : t.CompareTag("StartTile") ? "start" : t.CompareTag("GoalTile") ? "goal" : bc ? "solid" : null;
                if (kind == null) continue;
                if (t.GetComponentInParent<Button>(true) && !t.GetComponent<Button>()) continue;
                if (t.GetComponentInParent<Lever>(true) && !t.GetComponent<Lever>()) continue;
                var b = WB(t);
                var e = new StringBuilder($"  {{\"kind\":{S(kind)},\"name\":{S(t.name)},\"path\":{S(Path(t))},\"active\":{(ActiveChain(t) ? "true" : "false")},\"self\":{(t.gameObject.activeSelf ? "true" : "false")},\"pos\":{V(t.position)},\"min\":{V(b.min)},\"max\":{V(b.max)},\"layer\":{t.gameObject.layer},\"tag\":{S(t.tag)},\"trigger\":{(bc && bc.isTrigger ? "true" : "false")}");
                if (t.GetComponent<Button>() is Button bt)
                    e.Append($",\"reset\":{bt.resetTurnCount},\"targets\":[{string.Join(",", bt.targetStates.Select(x => $"{{\"path\":{S(Path(x.target.transform))},\"pos\":{V(x.target.transform.position)},\"init\":{(x.isInitiallyActive ? "true" : "false")}}}"))}]");
                if (t.GetComponent<Lever>() is Lever lv2)
                    e.Append($",\"rotY\":{F(t.eulerAngles.y)},\"targets\":[{string.Join(",", lv2.targetStates.Select(x => $"{{\"path\":{S(Path(x.target.transform))},\"pos\":{V(x.target.transform.position)},\"init\":{(x.isInitiallyActive ? "true" : "false")}}}"))}]");
                if (t.GetComponent<MovingObstacle>() is MovingObstacle mo)
                    e.Append($",\"visible\":{V(mo.visiblePosition)},\"cycle\":{mo.turnCycle}");
                if (t.GetComponent<Laser>()) e.Append($",\"dir\":{V(t.forward)}");
                e.Append("}");
                items.Add(e.ToString());
            }
            sb.Append(string.Join(",\n", items)).Append("\n}");
            var name = lv.Split('/')[1];
            System.IO.File.WriteAllText($"../captures/levels/{name}.json", "{\"items\":[\n" + string.Join(",\n", items) + "\n]}");
        }
        Debug.Log("[LevelDump] done");
    }
}
