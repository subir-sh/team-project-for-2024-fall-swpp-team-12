using UnityEditor;
using UnityEngine;

// Batch-mode helper used once for the 2021.3 -> Unity 6 upgrade:
// re-serializes every scene / prefab / material so the engine-format diff
// lands in the migration commit instead of leaking into later art commits.
public static class MigrationTools
{
    public static void ReserializeAll()
    {
        AssetDatabase.ForceReserializeAssets();
        AssetDatabase.SaveAssets();
        Debug.Log("[Remake] ForceReserializeAssets done");
    }
}
