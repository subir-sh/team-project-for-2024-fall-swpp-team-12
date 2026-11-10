using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only: compact per-level summary (walkable tiles, devices and what they toggle)
// used to pick a level that shows the depth of the rewind/phantom mechanic.
public static class LevelSurvey
{
    public static void Run()
    {
        var sb = new StringBuilder();
        var scenes = new[] { "L-004/L-004-2" };
        foreach (var s in scenes)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{s}.unity", OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            sb.AppendLine($"##### {s}");
            string P(Transform t) => $"({t.position.x:0.#},{t.position.y:0.#},{t.position.z:0.#})";
            var start = all.FirstOrDefault(t => t.CompareTag("StartTile") && t.gameObject.activeSelf);
            var goal = all.FirstOrDefault(t => t.CompareTag("GoalTile") && t.gameObject.activeSelf);
            sb.AppendLine($"start {(start ? P(start) : "?")} goal {(goal ? P(goal) : "?")}");
            var tiles = all.Where(t => t.GetComponent<BoxCollider>() && t.GetComponent<MeshRenderer>() && t.parent && t.parent.name == "Tilemap").ToList();
            sb.AppendLine("tiles: " + string.Join(" ", tiles.Select(t => (t.gameObject.activeSelf ? "" : "!") + t.name.Replace(" ", "") + P(t))));
            foreach (var b in all.Select(t => t.GetComponent<Button>()).Where(b => b))
                sb.AppendLine($"BUTTON {P(b.transform)} reset={b.resetTurnCount} -> " + string.Join(", ", b.targetStates.Select(x => $"{x.target.name}{P(x.target.transform)} init={(x.isInitiallyActive ? "on" : "off")}")));
            foreach (var l in all.Select(t => t.GetComponent<Lever>()).Where(l => l))
                sb.AppendLine($"LEVER {P(l.transform)} rotY={l.transform.eulerAngles.y:0} -> " + string.Join(", ", l.targetStates.Select(x => $"{x.target.name}{P(x.target.transform)} init={(x.isInitiallyActive ? "on" : "off")}")));
            foreach (var b in all.Where(t => t.GetComponent<Box>())) sb.AppendLine($"BOX {b.name} {P(b)}");
            foreach (var m in all.Where(t => t.GetComponent<MovingObstacle>())) sb.AppendLine($"MOVING {m.name} {P(m)}");
            foreach (var m in all.Where(t => t.GetComponent<Laser>())) sb.AppendLine($"LASER {m.name} {P(m)} rotY={m.eulerAngles.y:0}");
            foreach (var t in all.Where(t => t.parent && t.parent.name == "Tilemap" && !tiles.Contains(t)))
            {
                var cs = t.GetComponentsInChildren<Collider>(true);
                var bb = cs.Length > 0 ? cs[0].bounds : new Bounds(t.position, Vector3.zero);
                foreach (var c in cs) bb.Encapsulate(c.bounds);
                sb.AppendLine($"WALL {t.name.Replace(" ", "")} active={t.gameObject.activeSelf} n={cs.Length} min=({bb.min.x:0.##},{bb.min.y:0.##},{bb.min.z:0.##}) max=({bb.max.x:0.##},{bb.max.y:0.##},{bb.max.z:0.##})");
            }
            foreach (var t in tiles)
            {
                var bb = t.GetComponent<BoxCollider>().bounds;
                if (t.position.y > 0) sb.AppendLine($"WALLTILE {t.name.Replace(" ", "")} active={t.gameObject.activeSelf} min=({bb.min.x:0.##},{bb.min.y:0.##},{bb.min.z:0.##}) max=({bb.max.x:0.##},{bb.max.y:0.##},{bb.max.z:0.##})");
            }
            foreach (var c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(true)))
            {
                string path = c.name; for (var q = c.transform.parent; q; q = q.parent) path = q.name + "/" + path;
                var bb = c.bounds;
                sb.AppendLine($"ANYCOL {path.Replace(" ", "")} {c.GetType().Name} act={c.gameObject.activeInHierarchy} min=({bb.min.x:0.##},{bb.min.y:0.##},{bb.min.z:0.##}) max=({bb.max.x:0.##},{bb.max.y:0.##},{bb.max.z:0.##})");
            }
            // every other collider (walls, stairs, decorations) with its world bounds
            foreach (var c in all.Select(t => t.GetComponent<Collider>()).Where(c => c && !c.isTrigger && !(c.transform.parent && c.transform.parent.name == "Tilemap")
                         && !c.GetComponentInParent<Button>() && !c.GetComponentInParent<Lever>() && !c.GetComponent<Box>()))
            {
                var b = c.bounds;
                sb.AppendLine($"COLLIDER {c.name} active={c.gameObject.activeInHierarchy} layer={c.gameObject.layer} min=({b.min.x:0.#},{b.min.y:0.#},{b.min.z:0.#}) max=({b.max.x:0.#},{b.max.y:0.#},{b.max.z:0.#})");
            }
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../../captures/level_survey.txt"), sb.ToString());
        Debug.Log("[Survey] done");
    }
}
