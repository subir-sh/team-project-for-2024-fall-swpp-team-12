using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Breadth-first exploration of the REAL game from the current state (used to plan level solutions).
// Every move is played through InputManager exactly like a key press; after each move the world is
// restored by Backspace-undo (no phantom present) or by level reset + replaying the script prefix.
// Writes <out>/explore.txt: one line per discovered state with the shortest input path that reaches it.
public static class HarnessExplore
{
    static readonly FieldInfo obstacleTurn = typeof(MovingObstacle).GetField("turnCount", BindingFlags.NonPublic | BindingFlags.Instance);
    static string R(Vector3 v) => $"{Mathf.RoundToInt(v.x)},{Mathf.RoundToInt(v.y)},{Mathf.RoundToInt(v.z)}";

    public static string Key()
    {
        var pl = PlayerController.playerController; var ph = PhantomController.phantomController;
        var tm = TurnManager.turnManager;
        return $"P{R(pl.transform.position)}f{Mathf.RoundToInt(pl.transform.eulerAngles.y / 90f) % 4}" +
               $"|H{(ph.isPhantomExisting ? R(ph.transform.position) + "#" + ph.commandIterator.GetCurrentIndex() : "-")}" +
               $"|B{string.Join(";", tm.boxList.Select(b => b.gameObject.activeInHierarchy ? R(b.transform.position) : "x"))}" +
               $"|L{string.Join("", tm.leverList.Select(l => R(l.canToggleDirection)))}" +
               $"|U{string.Join("", tm.buttonList.Select(b => b.isPressed ? "1" : "0"))}" +
               $"|O{string.Join(";", tm.obstacleList.Select(o => R(o.transform.position) + "t" + obstacleTurn?.GetValue(o)))}";
    }

    public static int Turns => PlayerController.playerController.listCommandLog.Count;

    public static bool AtGoal()
    {
        var g = LevelManager.levelManager.CurrentGoal; var p = PlayerController.playerController.transform.position;
        return g && Mathf.Approximately(p.x, g.position.x) && Mathf.Approximately(p.z, g.position.z) && Mathf.Abs(p.y - (g.position.y + 1.2f)) < 0.05f;
    }

    public static IEnumerator Move(string c)
    {
        var pl = PlayerController.playerController; float w0 = Time.time;
        while ((pl.isBlinking || TurnManager.turnManager.CLOCK || pl.willDropDeath || pl.isTimeRewinding) && Time.time - w0 < 10f) yield return null;
        InputManager.inputManager.OnMovementControl?.Invoke(c);
        yield return null;
        float t0 = Time.time;
        while ((TurnManager.turnManager.CLOCK || PlayerController.playerController.isBlinking) && Time.time - t0 < 10f) yield return null;
        t0 = Time.time; while (Time.time - t0 < 0.12f) yield return null;
        // a death (laser, fall, crushed, box lost) blinks and then resets the level: wait for it to finish
        t0 = Time.time;
        while ((pl.isBlinking || pl.willDropDeath || pl.willLaserKillCharacter || TurnManager.turnManager.CLOCK
                || TurnManager.turnManager.boxList.Any(b => b.willDropDeath)) && Time.time - t0 < 8f) yield return null;
    }

    public static IEnumerator Rewind(int k)
    {
        InputManager.inputManager.OnTimeRewindModeToggle?.Invoke();
        float t0 = Time.time; while (Time.time - t0 < 0.2f) yield return null;
        for (int i = 0; i < k; i++) { InputManager.inputManager.OnTimeRewindControl?.Invoke("q"); t0 = Time.time; while (Time.time - t0 < 0.06f) yield return null; }
        InputManager.inputManager.OnTimeRewindModeToggle?.Invoke();
        t0 = Time.time; while (Time.time - t0 < 0.3f) yield return null;
    }

    // exploration only: a crate pushed into the void keeps falling forever and holds the turn clock;
    // stop it so the level reset can put it back (never used in solution runs)
    static void FreeLostBoxes()
    {
        foreach (var b in TurnManager.turnManager.boxList)
        {
            if (b.transform.position.y > -12f && b.isFallComplete) continue;
            var rb = b.GetComponent<Rigidbody>(); rb.linearVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            b.isFallComplete = true; b.willDropDeath = false; b.isMoveComplete = true;
        }
        TurnManager.turnManager.CLOCK = false;
    }

    public static bool BoxLost() => TurnManager.turnManager.boxList.Any(b => b.gameObject.activeInHierarchy && b.transform.position.y < -12f);

    static IEnumerator Replay(List<string> prefix, string path)
    {
        if (BoxLost() || TurnManager.turnManager.CLOCK) FreeLostBoxes();
        InputManager.inputManager.OnReset?.Invoke();
        float t0 = Time.time; while (Time.time - t0 < 1.0f) yield return null;
        foreach (var line in prefix)
        {
            var parts = line.Split(' ');
            if (parts[0] == "m") foreach (var c in parts[1]) yield return Move(c.ToString());
            else if (parts[0] == "rw") yield return Rewind(int.Parse(parts[1]));
        }
        foreach (var c in path) yield return Move(c.ToString());
    }

    // explore <maxStates> <maxDepth> [moves]
    public static IEnumerator Run(List<string> prefix, int maxStates, int maxDepth, string moves, string outDir, System.Action<string> log)
    {
        var sb = new List<string>();
        var rootKey = Key(); int rootTurns = Turns;
        var seen = new Dictionary<string, string> { [rootKey] = "" };
        var keyOf = new Dictionary<string, string> { [""] = rootKey };
        var q = new Queue<string>(); q.Enqueue("");
        string cur = ""; int deaths = 0, restores = 0, undoWarn = 0;
        sb.Add($"ROOT {rootKey}");
        while (q.Count > 0 && seen.Count < maxStates)
        {
            var path = q.Dequeue();
            if (path.Length >= maxDepth) continue;
            foreach (var c in moves)
            {
                if (cur != path)
                {   // undo back if possible, else reset + replay
                    bool ok = false;
                    if (cur != null && cur.StartsWith(path) && !BoxLost() && !PhantomController.phantomController.isPhantomExisting && Turns - (cur.Length - path.Length) >= rootTurns)
                    {
                        for (int i = 0; i < cur.Length - path.Length; i++) { InputManager.inputManager.OnUndo?.Invoke(); float t0 = Time.time; while (Time.time - t0 < 0.15f) yield return null; }
                        ok = Key() == keyOf[path];
                        if (!ok && undoWarn++ < 5) sb.Add($"UNDO mismatch '{cur}'->'{path}': got {Key()} want {keyOf[path]}");
                    }
                    if (!ok) { restores++; yield return Replay(prefix, path); if (Key() != keyOf[path]) sb.Add($"WARN restore mismatch for '{path}': {Key()}"); }
                    cur = path;
                }
                int before = Turns;
                yield return Move(c.ToString());
                var np = path + c;
                if (Turns < before)   // the level was reset: death
                {
                    deaths++; sb.Add($"DEATH {np}"); cur = null; continue;
                }
                cur = np;
                if (Turns == before) { cur = path; continue; }   // no turn passed
                if (BoxLost()) { sb.Add($"BOXLOST {np}"); cur = null; continue; }
                var k = Key();
                if (AtGoal()) { sb.Add($"GOAL {np} {k}"); log($"explore GOAL via '{np}'"); }
                if (!seen.ContainsKey(k))
                {
                    seen[k] = np; keyOf[np] = k; q.Enqueue(np); sb.Add($"S {np} {k}");
                    if (seen.Count % 50 == 0) { File.WriteAllLines(Path.Combine(outDir, "explore.txt"), sb); log($"explore progress states={seen.Count} queue={q.Count} depth={np.Length}"); }
                }
            }
        }
        sb.Add($"END states={seen.Count} deaths={deaths} restores={restores} queue={q.Count}");
        File.WriteAllLines(Path.Combine(outDir, "explore.txt"), sb);
        log($"explore done states={seen.Count} deaths={deaths} restores={restores}");
    }
}
