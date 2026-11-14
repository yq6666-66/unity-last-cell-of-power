using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using LastCellOfPower.StealthSample;
using LastCellOfPower.EscapeSample;

public static class EscapeSampleBuilder
{
    public const string ScenePath = "Assets/EscapeSample/Scenes/EscapeSample.unity";
    public const string NavigationPath = "Assets/EscapeSample/Navigation/EscapeSample.asset";
    const string SourceScene = "Assets/StealthSample/Scenes/StealthSample.unity";

    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Create requires edit mode");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Unsaved current scene; inspect before switching");
        if (File.Exists(ScenePath) || File.Exists(NavigationPath)) throw new Exception("Escape output already exists; inspect and repair explicitly, do not overwrite");
        Directory.CreateDirectory("Assets/EscapeSample/Scenes");
        Directory.CreateDirectory("Assets/EscapeSample/Navigation");
        AssetDatabase.Refresh();
        var source = EditorSceneManager.OpenScene(SourceScene);
        if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new Exception("Save-as-copy failed");
        EditorSceneManager.OpenScene(ScenePath);
        WireSavedCopy();
    }

    // Recovery is restricted to the saved, still-unwired copy created by Create.
    public static void ResumeSavedCopy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(NavigationPath))
            throw new Exception("Recovery requires edit mode and no navigation output");
        EditorSceneManager.OpenScene(ScenePath);
        WireSavedCopy();
    }

    static void WireSavedCopy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var old = UnityEngine.Object.FindFirstObjectByType<StealthSession>();
        if (!old || !old.player || !old.enemy) throw new Exception("Copied StealthSession references missing");
        var player = old.player;
        var enemy = old.enemy;
        var pathLine = old.pathLine; var sightLine = old.sightLine; var lastSeenLine = old.lastSeenLine;
        var font = old.uiFont;
        // Only this new scene's session is replaced; all source assets remain untouched.
        UnityEngine.Object.DestroyImmediate(old);
        var collision = GameObject.Find("CollisionWorld");
        if (!collision) throw new Exception("CollisionWorld missing");
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Transform Find(string name)
        {
            var matches = all.Where(t => t && t.name == name).ToArray();
            if (matches.Length != 1) throw new Exception("Expected one scene node: " + name + ", found " + matches.Length);
            return matches[0];
        }

        var doorRoot = Find("ENV_ExitDoor__N_1");
        var exchangedWall = Find("ENV_Wall_2m__N_-1");
        if (Mathf.Abs(doorRoot.position.x + 1) > .05f || Mathf.Abs(exchangedWall.position.x - 1) > .05f)
            throw new Exception("Unexpected imported north layout; do not reapply coordinate conversion");
        doorRoot.position += Vector3.right * 2;
        exchangedWall.position -= Vector3.right * 2;
        // Original decorative posts are centered around x=0, not the new door.
        // Place them outside the x=0..2 opening, including their copied colliders.
        var posts = new[] { Find("PROP_SafetyBollard__door_L"), Find("PROP_SafetyBollard__door_R") }.OrderBy(t => t.position.x).ToArray();
        for (int i = 0; i < posts.Length; i++)
        {
            var oldPosition = posts[i].position;
            var shift = Vector3.right * ((i == 0 ? -.4f : 2.4f) - oldPosition.x);
            foreach (var col in collision.GetComponentsInChildren<Collider>().Where(c =>
                c.name.StartsWith("Collision_PROP_SafetyBollard") && Mathf.Abs(c.bounds.center.x - oldPosition.x) < .3f && Mathf.Abs(c.bounds.center.z - oldPosition.z) < .3f))
                col.transform.position += shift;
            posts[i].position += shift;
        }
        var leaves = new[] { Find("ENV_ExitDoor__DoorLeaf_L"), Find("ENV_ExitDoor__DoorLeaf_R") }.OrderBy(t => t.position.x).ToArray();

        UnityEngine.Object.DestroyImmediate(Find("NorthWallWithClosedDoor").gameObject);
        Box(collision.transform, "NorthWallWest", new Vector3(-3.16f, 1.2f, 6.16f), new Vector3(6.32f, 2.4f, .32f));
        Box(collision.transform, "NorthWallEast", new Vector3(4.16f, 1.2f, 6.16f), new Vector3(4.32f, 2.4f, .32f));
        Box(collision.transform, "DoorJambWest", new Vector3(.1f, 1.1f, 6.16f), new Vector3(.2f, 2.2f, .32f));
        Box(collision.transform, "DoorJambEast", new Vector3(1.9f, 1.1f, 6.16f), new Vector3(.2f, 2.2f, .32f));
        Box(collision.transform, "DoorLintel", new Vector3(1, 2.3f, 6.16f), new Vector3(2, .2f, .32f));
        Box(collision.transform, "ExitPlatformFloor", new Vector3(1, -.06f, 7.2f), new Vector3(2, .12f, 2.4f));
        Box(collision.transform, "ExitPlatformWest", new Vector3(-.1f, .6f, 7.2f), new Vector3(.2f, 1.2f, 2.4f));
        Box(collision.transform, "ExitPlatformEast", new Vector3(2.1f, .6f, 7.2f), new Vector3(.2f, 1.2f, 2.4f));
        Box(collision.transform, "ExitPlatformEnd", new Vector3(1, .6f, 8.5f), new Vector3(2.4f, 1.2f, .2f));
        VisualBox("ExitPlatformVisual", new Vector3(1, -.06f, 7.2f), new Vector3(2, .12f, 2.4f), Material("MAT_Concrete"));
        VisualBox("ExitGuardWestVisual", new Vector3(-.1f, .6f, 7.2f), new Vector3(.2f, 1.2f, 2.4f), Material("MAT_Graphite"));
        VisualBox("ExitGuardEastVisual", new Vector3(2.1f, .6f, 7.2f), new Vector3(.2f, 1.2f, 2.4f), Material("MAT_Graphite"));
        VisualBox("ExitGuardEndVisual", new Vector3(1, .6f, 8.5f), new Vector3(2.4f, 1.2f, .2f), Material("MAT_Graphite"));

        var terminal = Find("PROP_Terminal__east");
        foreach (var col in collision.GetComponentsInChildren<Collider>().Where(c => c.name.StartsWith("Collision_PROP_Terminal")).ToArray())
            UnityEngine.Object.DestroyImmediate(col.gameObject);
        terminal.SetPositionAndRotation(new Vector3(2.8f, 0, 5), Quaternion.AngleAxis(90, Vector3.up) * terminal.rotation);
        var terminalBounds = BoundsOf(terminal);
        var terminalCollider = Box(collision.transform, "EscapeTerminalCollision", terminalBounds.center, terminalBounds.size);

        var setup = new GameObject("EscapeSession");
        var rules = setup.AddComponent<EscapeRules>();
        var battery = setup.AddComponent<EscapeBattery>(); battery.rules = rules;
        var door = new GameObject("EscapeDoorController").AddComponent<EscapeDoor>();
        door.rules = rules; door.leftLeaf = leaves[0]; door.rightLeaf = leaves[1];
        door.leftOpenOffset = new Vector3(-.85f, 0, 0); door.rightOpenOffset = new Vector3(.85f, 0, 0);
        var movingBarrier = Box(door.transform, "EscapeDoorBarrier", new Vector3(1, 1.1f, 6.16f), new Vector3(1.6f, 2.2f, .16f));
        door.doorCollider = movingBarrier;
        var obstacle = movingBarrier.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = new Vector3(1.6f, 2.2f, .22f);
        obstacle.carving = true; obstacle.carveOnlyStationary = false;
        obstacle.enabled = false; movingBarrier.enabled = false;
        door.doorObstacle = obstacle;
        door.exitBoundsCenter = new Vector3(1, 0, 7.2f); door.exitBoundsSize = new Vector3(1.6f, 2, 2.4f); door.exitZ = 6.8f;

        // Fresh surface => a fresh data object. Never rebake into the source's shared NavMeshData.
        foreach (var previous in collision.GetComponents<NavMeshSurface>())
        {
            previous.RemoveData();
            UnityEngine.Object.DestroyImmediate(previous);
        }
        var surface = collision.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1;
        surface.overrideVoxelSize = true; surface.voxelSize = .06f;
        Physics.SyncTransforms();
        surface.BuildNavMesh();
        if (!surface.navMeshData) throw new Exception("Escape navigation bake produced no data");
        AssetDatabase.CreateAsset(surface.navMeshData, NavigationPath);
        EditorUtility.SetDirty(surface);
        foreach (var point in enemy.patrolPoints)
            if (!NavMesh.SamplePosition(point, out _, .3f, enemy.agent.areaMask)) throw new Exception("Patrol point outside rebuilt navigation: " + point);
        var throughDoor = new NavMeshPath();
        if (!NavMesh.SamplePosition(new Vector3(1, 0, 5.4f), out var inside, .3f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(new Vector3(1, 0, 7.2f), out var outside, .3f, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(inside.position, outside.position, NavMesh.AllAreas, throughDoor) || throughDoor.status != NavMeshPathStatus.PathComplete)
            throw new Exception("Open doorway must have a complete path to exit platform");

        FinishSetup();
    }

    public static void FinishSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Finish requires edit mode");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath || UnityEngine.Object.FindFirstObjectByType<EscapeSession>()) throw new Exception("Expected new partially wired scene only");
        Transform Find(string name) { return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t => t && t.name == name); }
        var collision = GameObject.Find("CollisionWorld");
        var setup = GameObject.Find("EscapeSession");
        var rules = setup.GetComponent<EscapeRules>(); var battery = setup.GetComponent<EscapeBattery>();
        var player = UnityEngine.Object.FindFirstObjectByType<StealthPlayerController>();
        var enemy = UnityEngine.Object.FindFirstObjectByType<StealthEnemy>();
        var door = UnityEngine.Object.FindFirstObjectByType<EscapeDoor>();
        var surface = collision.GetComponent<NavMeshSurface>();
        var terminal = Find("PROP_Terminal__east"); var terminalBounds = BoundsOf(terminal);
        var terminalCollider = GameObject.Find("EscapeTerminalCollision").GetComponent<BoxCollider>();
        var doorRoot = Find("ENV_ExitDoor__N_1");
        var pathLine = GameObject.Find("NavigationPath").GetComponent<LineRenderer>();
        var sightLine = GameObject.Find("SightRay").GetComponent<LineRenderer>();
        var lastSeenLine = GameObject.Find("LastSeen").GetComponent<LineRenderer>();
        Font font = null;
        var movingBarrier = door.doorCollider; var obstacle = door.doorObstacle;
        var chargers = new[] { Find("PROP_ChargingDock__SW"), Find("PROP_ChargingDock__SE") }.OrderBy(t => t.position.x).ToArray();
        var targets = new EscapeInteractable[3];
        for (int i = 0; i < chargers.Length; i++)
        {
            var dock = chargers[i]; var bounds = BoundsOf(dock);
            var own = collision.GetComponentsInChildren<Collider>().Where(c => c.name.StartsWith("Collision_PROP_Charging") &&
                Mathf.Abs(c.bounds.center.x - dock.position.x) < 1.4f && Mathf.Abs(c.bounds.center.z - dock.position.z) < 1.4f).ToArray();
            targets[i] = Target("ChargingStation" + (i + 1), InteractionKind.Charger,
                new Vector3(bounds.center.x, .75f, bounds.max.z + .1f), own, "充电座 " + (i + 1));
        }
        targets[2] = Target("NorthDoorTerminal", InteractionKind.Terminal,
            new Vector3(terminalBounds.center.x, .9f, terminalBounds.min.z - .1f), new Collider[] { terminalCollider }, "北门供电终端");
        var interaction = setup.AddComponent<EscapeInteraction>();
        interaction.player = player; interaction.battery = battery; interaction.door = door; interaction.rules = rules;
        interaction.targets = targets; interaction.obstructionMask = 1;
        var session = setup.AddComponent<EscapeSession>();
        session.player = player; session.enemy = enemy; session.rules = rules; session.battery = battery;
        session.interaction = interaction; session.door = door; session.uiFont = font;
        session.pathLine = pathLine; session.sightLine = sightLine; session.lastSeenLine = lastSeenLine;
        movingBarrier.enabled = true; obstacle.enabled = true;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("ESCAPE_SCENE_CREATED door=" + doorRoot.position.ToString("F3") + " terminal=" + terminal.position.ToString("F3") +
            " chargers=" + string.Join(";", chargers.Select(t => t.position.ToString("F3"))) + " navigation=" + NavigationPath);
        Debug.Log("ESCAPE_INTERACTION_ANCHORS " + string.Join(";", targets.Select(t => t.label + "=" + t.Position.ToString("F3"))));
    }

    static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = center;
        var collider = go.AddComponent<BoxCollider>(); collider.size = size; return collider;
    }
    static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new Exception("No renderers under " + root.name);
        var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
    }
    static Material Material(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage4/Materials/" + name + ".mat");
        if (!material) throw new Exception("Existing material missing: " + name); return material;
    }
    static GameObject VisualBox(string name, Vector3 center, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.position = center; go.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    static EscapeInteractable Target(string name, InteractionKind kind, Vector3 position, Collider[] ignored, string label)
    {
        var go = new GameObject(name); go.transform.position = position;
        var interactable = go.AddComponent<EscapeInteractable>();
        interactable.kind = kind; interactable.anchor = go.transform; interactable.label = label; interactable.ignoredColliders = ignored;
        var marker = VisualBox(name + "Marker", position, new Vector3(.1f, .1f, .035f), Material("MAT_PowerMint"));
        marker.transform.SetParent(go.transform, true);
        return interactable;
    }
}
