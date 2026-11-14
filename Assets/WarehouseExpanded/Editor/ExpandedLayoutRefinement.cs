using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using Unity.Pipeline.Commands;
using LastCellOfPower.WarehouseExpanded;

// A narrowly scoped, repeat-safe migration for the first generated expanded layout.
public static class ExpandedLayoutRefinement
{
    sealed class MovePlan
    {
        public int index;
        public Transform visual;
        public BoxCollider collider;
        public Vector3 delta;
        public float targetZ;
    }

    [CliCommand("warehouse", "refine", "Widen the two expanded-map cargo corners and rebake its own navigation")]
    public static string Refine()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Refinement requires edit mode");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene changes; preserve them before refinement");
        // Reload the exact new scene; revisions and legacy samples are intentionally unsupported.
        var scene = EditorSceneManager.OpenScene(ExpandedWarehouseBuilder.ScenePath);
        var sessions = UnityEngine.Object.FindObjectsByType<ExpandedSession>(FindObjectsSortMode.None);
        if (sessions.Length != 1) throw new Exception("Expected one expanded session");
        var session = sessions[0];
        var surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces.Length != 1 || !surfaces[0].navMeshData || !session.level || !session.door)
            throw new Exception("Expanded level/navigation/door references missing");
        var surface = surfaces[0];
        var level = session.level;
        var navigation = surface.navMeshData;
        if (AssetDatabase.GetAssetPath(level) != ExpandedWarehouseBuilder.LevelPath ||
            AssetDatabase.GetAssetPath(navigation) != ExpandedWarehouseBuilder.NavPath)
            throw new Exception("Refusing to modify navigation or level outside the default expanded output");
        if (EditorUtility.IsDirty(level) || EditorUtility.IsDirty(navigation))
            throw new Exception("Unsaved level/navigation asset changes; save and back them up first");
        if (!session.door.doorCollider || !session.door.doorObstacle)
            throw new Exception("Door barriers missing");

        var moves = new[] { Plan(level, "NW_NorthCrates", 9.4f), Plan(level, "NE_CrateStack", 8.6f) };
        string backup = Path.Combine(Path.GetTempPath(), "LastCell-Expanded-refine-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(backup);
        var backedUp = new List<string>();
        foreach (var path in new[] { ExpandedWarehouseBuilder.ScenePath, ExpandedWarehouseBuilder.NavPath, ExpandedWarehouseBuilder.LevelPath })
        {
            if (!File.Exists(path)) throw new Exception("Expected saved output missing: " + path);
            File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
            backedUp.Add(Path.GetFullPath(path));
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", Path.Combine(backup, Path.GetFileName(path) + ".meta"));
        }
        File.WriteAllLines(Path.Combine(backup, "backed-up-paths.txt"), backedUp);
        Debug.Log("EXPANDED_REFINEMENT_BACKUP " + backup);

        UnityEngine.AI.NavMeshData generated = null;
        var barrierObject = session.door.doorCollider.gameObject;
        try
        {
            var specs = level.coverSpecs.ToArray();
            foreach (var move in moves)
            {
                move.visual.position += move.delta;
                move.collider.transform.position += move.delta;
                var spec = specs[move.index]; spec.position.z = move.targetZ; specs[move.index] = spec;
            }
            level.coverSpecs = specs;
            level.Validate();
            // The generated base mesh contains the open passage; the closed runtime obstacle carves it.
            session.door.doorObstacle.enabled = false;
            session.door.doorCollider.enabled = false;
            barrierObject.SetActive(false);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            generated = surface.navMeshData;
            if (!generated) throw new Exception("Refinement bake returned no NavMeshData");
            surface.RemoveData();
            if (generated != navigation) EditorUtility.CopySerialized(generated, navigation);
            surface.navMeshData = navigation;
            surface.AddData();
            RestoreClosedDoor(session);
            EditorUtility.SetDirty(navigation); EditorUtility.SetDirty(surface); EditorUtility.SetDirty(level);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save refined expanded scene");
            string report = ExpandedDelivery.Inspect();
            Debug.Log("EXPANDED_REFINEMENT_PASS NW_NorthCrates.z=9.4 NE_CrateStack.z=8.6 backup=" + backup + "\n" + report);
            return "Refined expanded cargo clearance. Backup: " + backup + "\n" + report;
        }
        finally
        {
            RestoreClosedDoor(session);
            // BuildNavMesh creates a transient object; after CopySerialized the surface uses the saved asset.
            if (generated && generated != navigation && surface.navMeshData != generated && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(generated)))
                UnityEngine.Object.DestroyImmediate(generated);
        }
    }

    static MovePlan Plan(ExpandedLevelDefinition level, string name, float targetZ)
    {
        var indices = Enumerable.Range(0, level.coverSpecs.Length).Where(i => level.coverSpecs[i].name == name).ToArray();
        if (indices.Length != 1) throw new Exception("Expected one cover definition: " + name);
        int index = indices[0];
        var spec = level.coverSpecs[index];
        bool original = Mathf.Abs(spec.position.z - 9) < .001f;
        bool refined = Mathf.Abs(spec.position.z - targetZ) < .001f;
        if (!original && !refined) throw new Exception("Unknown cover Z; refusing migration: " + name + "=" + spec.position.z);
        var visual = GameObject.Find(name);
        var colliderObject = GameObject.Find(name + "Collision");
        if (!visual || !colliderObject) throw new Exception("Cover visual/collision missing: " + name);
        var collider = colliderObject.GetComponent<BoxCollider>();
        var core = visual.transform.Find(name + "CargoCore");
        if (!collider || !core || Mathf.Abs(collider.bounds.center.z - spec.position.z) > .01f || Mathf.Abs(core.position.z - spec.position.z) > .01f)
            throw new Exception("Cover geometry does not match its saved definition: " + name);
        return new MovePlan { index = index, visual = visual.transform, collider = collider,
            delta = Vector3.forward * (targetZ - spec.position.z), targetZ = targetZ };
    }

    static void RestoreClosedDoor(ExpandedSession session)
    {
        if (!session || !session.door || !session.door.doorCollider || !session.door.doorObstacle) return;
        session.door.doorCollider.gameObject.SetActive(true);
        session.door.doorCollider.enabled = true;
        session.door.doorObstacle.carving = true;
        session.door.doorObstacle.enabled = true;
    }
}
