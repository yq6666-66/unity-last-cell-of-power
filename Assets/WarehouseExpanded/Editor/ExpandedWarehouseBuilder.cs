using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using LastCellOfPower.StealthSample;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

// Deterministic, metre-based assembly. Source scenes and imported models are never saved over.
public static class ExpandedWarehouseBuilder
{
    public const string ScenePath = "Assets/WarehouseExpanded/Scenes/WarehouseExpanded.unity";
    public const string NavPath = "Assets/WarehouseExpanded/Navigation/WarehouseExpanded.asset";
    public const string LevelPath = "Assets/WarehouseExpanded/Config/WarehouseLevel.asset";
    public const string ProfilePath = "Assets/WarehouseExpanded/Config/RelaxedProfile.asset";
    public static string LastScenePath { get; private set; }
    const string SourceScene = "Assets/EscapeSample/Scenes/EscapeSample.unity";

    public static void Create() { CreateAt(ScenePath, NavPath, LevelPath, ProfilePath); }

    // Failed generation is retained for inspection. A revised build can use a NEW output set.
    public static void CreateRevision(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision) || revision.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
            throw new ArgumentException("Revision must contain only letters, digits, underscore or hyphen");
        string root = "Assets/WarehouseExpanded/Revisions/" + revision;
        CreateAt(root + "/WarehouseExpanded.unity", root + "/WarehouseExpanded.asset", root + "/WarehouseLevel.asset", root + "/RelaxedProfile.asset");
    }

    static void CreateAt(string scenePath, string navPath, string levelPath, string profilePath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Generation requires edit mode");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Unsaved scene changes; preserve them before generation");
        foreach (var path in new[] { scenePath, navPath, levelPath, profilePath })
        {
            if (File.Exists(path)) throw new Exception("Output exists: " + path + ". Inspect it; use CreateRevision with a new name instead of overwriting.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        }
        AssetDatabase.Refresh();
        var source = EditorSceneManager.OpenScene(SourceScene);
        if (!EditorSceneManager.SaveScene(source, scenePath, true)) throw new Exception("Could not save independent source copy");
        var scene = EditorSceneManager.OpenScene(scenePath);
        LastScenePath = scenePath;
        var old = UnityEngine.Object.FindFirstObjectByType<EscapeSession>();
        if (!old || !old.player || !old.enemy) throw new Exception("Source EscapeSession references missing");
        var player = old.player; var oldEnemy = old.enemy; var agent = oldEnemy.agent; var perception = oldEnemy.perception;
        var pathLine = old.pathLine; var sightLine = old.sightLine; var lastSeenLine = old.lastSeenLine; var font = old.uiFont;
        var templates = new GameObject("ExpandedSourceTemplates");
        var sourceTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Transform Find(string name) => sourceTransforms.Single(t => t && t.name == name);
        // Capture complete instance roots, including their import matrices, BEFORE removing copied room roots.
        var floor = Template(Find("ENV_Floor_2m__1_1"), templates.transform, "Floor");
        var wall = Template(Find("ENV_Wall_2m__N_3"), templates.transform, "Wall");
        var shelf = Template(Find("PROP_Shelf__occluder_S"), templates.transform, "Shelf");
        var crate = Template(Find("PROP_CrateLarge__occluder_top"), templates.transform, "Crate");
        var dock = Template(Find("PROP_ChargingDock__SW"), templates.transform, "Dock");
        var terminal = Template(Find("PROP_Terminal__east"), templates.transform, "Terminal");
        var lamp = Template(Find("PROP_CeilingLamp__NE"), templates.transform, "Lamp");
        templates.SetActive(false);

        var keep = new HashSet<GameObject> { player.transform.root.gameObject, oldEnemy.transform.root.gameObject,
            pathLine.transform.root.gameObject, sightLine.transform.root.gameObject, lastSeenLine.transform.root.gameObject, templates };
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) keep.Add(light.transform.root.gameObject);
        agent.enabled = false;
        foreach (var root in scene.GetRootGameObjects()) if (!keep.Contains(root)) UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(oldEnemy);

        var profile = ScriptableObject.CreateInstance<ExpandedGameplayProfile>();
        var level = ScriptableObject.CreateInstance<ExpandedLevelDefinition>();
        profile.Validate(); level.Validate();
        AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.CreateAsset(level, levelPath);
        var visuals = new GameObject("ExpandedWarehouseVisuals").transform;
        var collision = new GameObject("ExpandedCollisionWorld").transform;
        var anchors = new GameObject("ExpandedGameplayAnchors").transform;
        var box = level.minimum; var max = level.maximum;
        for (float x = box.x + 1; x < max.x; x += 2)
            for (float z = box.y + 1; z < max.y; z += 2) Place(floor, visuals, "Floor_" + x + "_" + z, new Vector3(x, 0, z));
        Box(collision, "WarehouseFloor", new Vector3(0, -.06f, 0), new Vector3(24, .12f, 24));
        WallRun(visuals, collision, wall, "South", -12.16f, -12.16f, 12.16f, false);
        WallRun(visuals, collision, wall, "West", -12.16f, -12, 12, true);
        WallRun(visuals, collision, wall, "East", 12.16f, -12, 12, true);
        float exitX = level.exitPosition.x;
        WallRun(visuals, collision, wall, "NorthWest", 12.16f, -12.16f, exitX - 1.2f, false);
        WallRun(visuals, collision, wall, "NorthEast", 12.16f, exitX + 1.2f, 12.16f, false);
        // Four openings centered on the configured ring connections; exact clear width is 3.2 m.
        float halfOpening = level.connectionWidth * .5f;
        foreach (bool vertical in new[] { true, false })
        {
            var centers = level.connectionCenters.Where(p => vertical ? Mathf.Abs(p.x) < .001f : Mathf.Abs(p.z) < .001f)
                .Select(p => vertical ? p.z : p.x).OrderBy(v => v).ToArray();
            if (centers.Length != 2) throw new Exception("Each central divider must have two connections");
            float start = -12;
            for (int i = 0; i < centers.Length; i++)
            {
                WallRun(visuals, collision, wall, (vertical ? "DividerX" : "DividerZ") + i, 0, start, centers[i] - halfOpening, vertical);
                start = centers[i] + halfOpening;
            }
            WallRun(visuals, collision, wall, vertical ? "DividerXEnd" : "DividerZEnd", 0, start, 12, vertical);
        }
        foreach (var cover in level.coverSpecs) Cover(visuals, collision, shelf, crate, cover);
        for (int x = -9; x <= 9; x += 6) for (int z = -9; z <= 9; z += 6)
            Place(lamp, visuals, "CeilingLamp_" + x + "_" + z, new Vector3(x, 2.35f, z));

        var targetList = new List<EscapeInteractable>();
        for (int i = 0; i < level.chargerPositions.Length; i++)
        {
            var instance = Place(dock, visuals, "ChargingDock_" + i, level.chargerPositions[i]);
            var bounds = BoundsOf(instance.transform);
            var own = Box(collision, "ChargingDockCollision_" + i, bounds.center, bounds.size);
            var anchorPosition = new Vector3(bounds.center.x, .75f, bounds.max.z + .1f);
            targetList.Add(Target(anchors, "Charger_" + i, InteractionKind.Charger, anchorPosition, own, "充电座 " + (i + 1)));
        }
        var terminalInstance = Place(terminal, visuals, "SupplyTerminal", level.terminal);
        var terminalBounds = BoundsOf(terminalInstance.transform);
        var terminalCollision = Box(collision, "TerminalCollision", terminalBounds.center, terminalBounds.size);
        targetList.Add(Target(anchors, "SupplyTerminalAnchor", InteractionKind.Terminal,
            new Vector3(terminalBounds.center.x, .9f, terminalBounds.min.z - .1f), terminalCollision, "出口供电终端"));

        var host = new GameObject("ExpandedSession");
        var rules = host.AddComponent<EscapeRules>(); profile.ApplyTo(rules);
        var battery = host.AddComponent<EscapeBattery>(); battery.rules = rules;
        var door = CreateExit(visuals, collision, anchors, level, rules);
        player.controller.enabled = false;
        player.transform.SetPositionAndRotation(level.playerSpawn, Quaternion.identity);
        player.controller.enabled = true; player.speed = profile.playerSpeed; player.HorizontalMovementLocked = false;
        player.viewCamera.transform.SetParent(player.controller.transform, false);
        player.viewCamera.transform.localPosition = new Vector3(0, .9f, 0); player.viewCamera.transform.localRotation = Quaternion.identity;
        player.viewCamera.orthographic = false; player.viewCamera.cullingMask &= ~(1 << 8); player.viewCamera.farClipPlane = 90;
        agent.transform.SetPositionAndRotation(level.enemySpawn, Quaternion.Euler(0, level.enemyYaw, 0));
        var enemy = agent.gameObject.AddComponent<ExpandedEnemy>();
        enemy.profile = profile; enemy.level = level; enemy.agent = agent; enemy.perception = perception;
        perception.observer = agent.transform; perception.target = player.transform; perception.obstructionMask = 1;
        perception.viewDistance = profile.viewDistance; perception.viewAngle = profile.viewAngle;
        var interaction = host.AddComponent<EscapeInteraction>();
        interaction.rules = rules; interaction.player = player; interaction.battery = battery; interaction.door = door;
        interaction.targets = targetList.ToArray(); interaction.obstructionMask = 1; interaction.cancelOnMovement = true;
        var session = host.AddComponent<ExpandedSession>();
        session.profile = profile; session.level = level; session.player = player; session.enemy = enemy;
        session.rules = rules; session.battery = battery; session.interaction = interaction; session.door = door;
        session.uiFont = font; session.pathLine = pathLine; session.sightLine = sightLine; session.lastSeenLine = lastSeenLine;

        // The only collected geometry is this explicit world, not render meshes or character parts.
        var surface = collision.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1; surface.overrideVoxelSize = true; surface.voxelSize = .06f;
        Physics.SyncTransforms(); surface.BuildNavMesh();
        if (!surface.navMeshData) throw new Exception("Expanded NavMesh bake produced no data");
        AssetDatabase.CreateAsset(surface.navMeshData, navPath); EditorUtility.SetDirty(surface);
        CheckPoint(level.playerSpawn, "spawn"); CheckPoint(level.enemySpawn, "enemy");
        foreach (var stop in level.patrolStops) CheckPoint(stop.position, "patrol");
        foreach (var connection in level.connectionCenters) CheckPoint(connection, "connection");
        CheckPoint(door.exitAnchor.position + Vector3.forward * .2f, "exit platform");
        agent.enabled = true;
        door.doorCollider.gameObject.SetActive(true); door.doorCollider.enabled = true; door.doorObstacle.enabled = true;
        UnityEngine.Object.DestroyImmediate(templates);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("EXPANDED_SCENE_CREATED scene=" + scenePath + " nav=" + navPath + " profile=" + profilePath + " level=" + levelPath);
        Debug.Log("EXPANDED_ANCHORS " + string.Join(";", targetList.Select(t => t.label + "=" + t.Position.ToString("F3"))));
    }

    static void CheckPoint(Vector3 point, string label)
    {
        if (!NavMesh.SamplePosition(point, out var hit, .4f, NavMesh.AllAreas) || Vector3.Distance(hit.position, point) > .41f)
            throw new Exception("Expanded navigation point invalid: " + label + " " + point);
    }

    static GameObject Template(Transform source, Transform parent, string name)
    {
        var wrapper = new GameObject(name); wrapper.transform.SetParent(parent, false);
        var child = UnityEngine.Object.Instantiate(source.gameObject, wrapper.transform, true);
        child.transform.position = Vector3.zero;
        foreach (var collider in child.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        if (wrapper.GetComponentsInChildren<Renderer>().Length == 0) throw new Exception("Empty template: " + name);
        return wrapper;
    }
    static GameObject Place(GameObject template, Transform parent, string name, Vector3 origin, float yaw = 0)
    {
        var go = UnityEngine.Object.Instantiate(template, parent, false); go.name = name; go.SetActive(true);
        go.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0)); return go;
    }
    static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new Exception("No visual bounds: " + root.name);
        var result = renderers[0].bounds; foreach (var r in renderers.Skip(1)) result.Encapsulate(r.bounds); return result;
    }
    static Material Mat(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage4/Materials/" + name + ".mat");
        if (!material) throw new Exception("Missing existing material: " + name); return material;
    }
    static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = center;
        var collider = go.AddComponent<BoxCollider>(); collider.size = size; return collider;
    }
    static GameObject VisualBox(Transform parent, string name, Vector3 center, Vector3 size, string material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = center; go.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = Mat(material); return go;
    }

    static void WallRun(Transform visuals, Transform collision, GameObject template, string name, float fixedAxis, float start, float end, bool vertical)
    {
        if (end - start <= .001f) return;
        float length = end - start, middle = (start + end) * .5f;
        var center = vertical ? new Vector3(fixedAxis, 1.2f, middle) : new Vector3(middle, 1.2f, fixedAxis);
        Box(collision, name + "Collision", center, vertical ? new Vector3(.32f, 2.4f, length) : new Vector3(length, 2.4f, .32f));
        float cursor = start;
        while (end - cursor >= 1.999f)
        {
            var origin = vertical ? new Vector3(fixedAxis, 0, cursor + 1) : new Vector3(cursor + 1, 0, fixedAxis);
            Place(template, visuals, name + "Module_" + cursor.ToString("F2"), origin, vertical ? 90 : 0); cursor += 2;
        }
        float remainder = end - cursor;
        if (remainder > .01f)
        {
            var last = vertical ? new Vector3(fixedAxis, 1.2f, cursor + remainder * .5f) : new Vector3(cursor + remainder * .5f, 1.2f, fixedAxis);
            VisualBox(visuals, name + "EndPanel", last, vertical ? new Vector3(.32f, 2.4f, remainder) : new Vector3(remainder, 2.4f, .32f), "MAT_Steel");
        }
    }

    static void Cover(Transform visuals, Transform collision, GameObject shelf, GameObject crate, ExpandedLevelDefinition.CoverSpec spec)
    {
        var root = new GameObject(spec.name); root.transform.SetParent(visuals, false);
        // Closed cargo backing makes the declared cover physically and visually solid at eye height.
        // Detailed imported racks/crates are placed without stretching their meshes.
        VisualBox(root.transform, spec.name + "CargoCore", spec.position, spec.size - new Vector3(.32f, 0, .32f), "MAT_Graphite");
        Box(collision, spec.name + "Collision", spec.position, spec.size);
        var shelfSize = BoundsOf(shelf.transform).size;
        var crateSize = BoundsOf(crate.transform).size;
        bool alongZ = spec.size.z > spec.size.x;
        var module = spec.name.Contains("Crate") || spec.size.y < 1.8f ? crate : shelf;
        float moduleWidth = module == crate ? crateSize.x : shelfSize.x;
        float longSide = alongZ ? spec.size.z : spec.size.x;
        int count = Mathf.Max(1, Mathf.FloorToInt(longSide / Mathf.Max(.25f, moduleWidth)));
        float spacing = longSide / count;
        for (int i = 0; i < count; i++)
        {
            var detail = Place(module, root.transform, spec.name + "ImportedModule_" + i, Vector3.zero, alongZ ? 90 : 0);
            var b = BoundsOf(detail.transform);
            // Fit only whole, original-sized modules. Oversized shelf is replaced by a whole crate.
            if (b.size.y > spec.size.y + .2f || b.size.x > spec.size.x + .1f || b.size.z > spec.size.z + .1f)
            {
                UnityEngine.Object.DestroyImmediate(detail);
                detail = Place(crate, root.transform, spec.name + "ImportedCrate_" + i, Vector3.zero, alongZ ? 90 : 0);
                b = BoundsOf(detail.transform);
            }
            var desired = spec.position;
            desired.y = b.size.y * .5f;
            if (alongZ) desired.z = spec.position.z - longSide * .5f + spacing * (i + .5f);
            else desired.x = spec.position.x - longSide * .5f + spacing * (i + .5f);
            // Put the visible face flush against the cover face; collision matches the solid cargo core.
            if (alongZ) desired.x = spec.position.x + spec.size.x * .5f - b.size.x * .5f;
            else desired.z = spec.position.z - spec.size.z * .5f + b.size.z * .5f;
            detail.transform.position += desired - b.center;
        }
        // Small steel uprights and top trim retain the same industrial visual language on larger groups.
        foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { -.5f, .5f })
            VisualBox(root.transform, spec.name + "Upright", spec.position + new Vector3(x * (spec.size.x - .08f), 0, z * (spec.size.z - .08f)), new Vector3(.08f, spec.size.y, .08f), "MAT_Steel");
        VisualBox(root.transform, spec.name + "Cap", spec.position + Vector3.up * (spec.size.y * .5f - .04f), new Vector3(spec.size.x, .08f, spec.size.z), "MAT_ServiceYellow");
    }

    static EscapeInteractable Target(Transform parent, string name, InteractionKind kind, Vector3 position, Collider own, string label)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        var target = go.AddComponent<EscapeInteractable>(); target.kind = kind; target.anchor = go.transform;
        target.label = label; target.ignoredColliders = new[] { own };
        VisualBox(go.transform, name + "Marker", position, new Vector3(.1f, .1f, .035f), "MAT_PowerMint"); return target;
    }

    static EscapeDoor CreateExit(Transform visuals, Transform collision, Transform anchors, ExpandedLevelDefinition level, EscapeRules rules)
    {
        Vector3 origin = level.exitPosition;
        var door = new GameObject("ExpandedExitDoor").AddComponent<EscapeDoor>(); door.rules = rules;
        door.leftLeaf = VisualBox(door.transform, "LeftSlidingLeaf", origin + new Vector3(-.6f, 1.1f, 0), new Vector3(1.2f, 2.2f, .14f), "MAT_Steel").transform;
        door.rightLeaf = VisualBox(door.transform, "RightSlidingLeaf", origin + new Vector3(.6f, 1.1f, 0), new Vector3(1.2f, 2.2f, .14f), "MAT_Steel").transform;
        door.leftOpenOffset = Vector3.left * 1.3f; door.rightOpenOffset = Vector3.right * 1.3f;
        foreach (float x in new[] { -1.3f, 1.3f })
        {
            VisualBox(visuals, "ExitJamb", origin + new Vector3(x, 1.15f, 0), new Vector3(.2f, 2.3f, .36f), "MAT_Graphite");
            Box(collision, "ExitJambCollision", origin + new Vector3(x, 1.15f, 0), new Vector3(.2f, 2.3f, .36f));
        }
        VisualBox(visuals, "ExitLintel", origin + new Vector3(0, 2.35f, 0), new Vector3(2.8f, .3f, .36f), "MAT_ServiceYellow");
        Box(collision, "ExitLintelCollision", origin + new Vector3(0, 2.35f, 0), new Vector3(2.8f, .3f, .36f));
        var barrier = Box(door.transform, "ExpandedDoorBarrier", origin + Vector3.up * 1.1f, new Vector3(2.4f, 2.2f, .16f));
        barrier.gameObject.SetActive(false); barrier.enabled = false;
        var obstacle = barrier.gameObject.AddComponent<NavMeshObstacle>(); obstacle.enabled = false;
        obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = new Vector3(2.4f, 2.2f, .22f); obstacle.carving = true; obstacle.carveOnlyStationary = false;
        door.doorCollider = barrier; door.doorObstacle = obstacle;
        var exitAnchor = new GameObject("ExitThreshold").transform; exitAnchor.SetParent(anchors, false);
        exitAnchor.position = origin + Vector3.forward * .64f; exitAnchor.rotation = Quaternion.identity;
        door.exitAnchor = exitAnchor; door.exitLocalSize = new Vector3(2.4f, 2, 2.4f);
        var platform = new Vector3(origin.x, -.06f, 13.2f);
        Box(collision, "ExitPlatformFloor", platform, new Vector3(3.2f, .12f, 2.4f));
        VisualBox(visuals, "ExitPlatformVisual", platform, new Vector3(3.2f, .12f, 2.4f), "MAT_Concrete");
        foreach (float x in new[] { -1.7f, 1.7f })
        {
            var center = new Vector3(origin.x + x, .6f, 13.2f);
            Box(collision, "ExitGuardSide", center, new Vector3(.2f, 1.2f, 2.4f));
            VisualBox(visuals, "ExitGuardSideVisual", center, new Vector3(.2f, 1.2f, 2.4f), "MAT_Graphite");
        }
        Box(collision, "ExitGuardEnd", new Vector3(origin.x, .6f, 14.5f), new Vector3(3.6f, 1.2f, .2f));
        VisualBox(visuals, "ExitGuardEndVisual", new Vector3(origin.x, .6f, 14.5f), new Vector3(3.6f, 1.2f, .2f), "MAT_Graphite");
        return door;
    }
}
