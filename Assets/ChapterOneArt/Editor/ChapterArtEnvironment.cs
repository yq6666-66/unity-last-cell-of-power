using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.EscapeSample;

/// <summary>Deterministic visual dressing only. Never edits the collision or navigation roots.</summary>
public static class ChapterArtEnvironment
{
    const string MeshFolder = "Assets/ChapterOneArt/Meshes";
    static ChapterArtProfile p;
    static Transform root;
    static TMP_FontAsset font;
    static int boxCount;
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    public static void Apply(ExpandedSession session, ChapterArtProfile profile)
    {
        if (!session || !profile || !session.level) throw new ArgumentException("Art environment requires session, level and profile");
        if (!profile.concrete || !profile.paintedMetal || !profile.rustedMetal || !profile.wood || !profile.cardboard || !profile.fabric || !profile.rubber)
            throw new InvalidOperationException("Art environment materials missing");
        if (session.gameObject.scene.GetRootGameObjects().Any(g => g.name == "ArtWorld"))
            throw new InvalidOperationException("ArtWorld already exists; generate from the preserved baseline");
        p = profile; boxCount = 0; meshes.Clear();
        Directory.CreateDirectory(MeshFolder); AssetDatabase.Refresh();
        font = UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>().font;
        root = new GameObject("ArtWorld").transform;
        ReplaceSurfaceMaterials(session);
        BuildFloor();
        BuildRoof();
        BuildLighting();
        ApplyReadabilityLights(profile);
        foreach (var spec in session.level.coverSpecs) BuildCargo(session, spec);
        BuildWallDetails(session);
        BuildDevices(session);
        BuildExit(session);
        BuildRobot(session);
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
        AssetDatabase.SaveAssets();
        Debug.Log("ART_ENVIRONMENT_CREATED boxes=" + boxCount + "; persistentMeshes=" + meshes.Count + "; collision/nav untouched");
    }

    static Transform Group(string name, Transform parent = null)
    {
        var go = new GameObject(name); go.transform.SetParent(parent ? parent : root, false); return go.transform;
    }

    static Transform Find(ExpandedSession session, string name)
    {
        return session.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
    }

    static void ReplaceSurfaceMaterials(ExpandedSession session)
    {
        var visuals = Find(session, "ExpandedWarehouseVisuals");
        if (!visuals) throw new InvalidOperationException("Accepted visual root missing");
        foreach (var r in visuals.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mapped = r.sharedMaterials.Select(MapMaterial).ToArray(); r.sharedMaterials = mapped;
            RetileImportedMesh(r);
        }
        foreach (var r in session.enemy.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name == "ChapterGuardStatus") continue;
            r.sharedMaterials = r.sharedMaterials.Select(MapMaterial).ToArray(); RetileImportedMesh(r);
        }
        foreach (var t in visuals.GetComponentsInChildren<Transform>(true))
        {
            if (t.parent == visuals && (t.name.StartsWith("Floor_", StringComparison.Ordinal) || t.name.StartsWith("CeilingLamp_", StringComparison.Ordinal)))
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
        // Markers alone are hidden. The anchor object and its original transform remain unchanged.
        foreach (var target in session.interaction.targets)
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
                if (r.name.EndsWith("Marker", StringComparison.Ordinal)) r.enabled = false;
    }

    static Material MapMaterial(Material original)
    {
        if (!original) return p.paintedMetal;
        string name = original.name;
        if (name.Contains("PowerMint")) return p.mintEmission ? p.mintEmission : p.paintedMetal;
        if (name.Contains("Rubber")) return p.rubber;
        if (name.Contains("Concrete") || name.Contains("Mist")) return p.concrete;
        if (name.Contains("ServiceYellow") || name.Contains("Steel")) return p.paintedMetal;
        if (name.Contains("Graphite")) return p.rustedMetal;
        return original;
    }

    static void RetileImportedMesh(MeshRenderer renderer)
    {
        var filter = renderer.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh) return;
        Mesh original = filter.sharedMesh;
        // Imported FBX meshes can be non-readable. Editor mesh access supports the serialized data,
        // but skip safely if a platform importer cannot expose it; new visible shells carry their own UVs.
        try
        {
            string signature = "retile-v2:" + AssetDatabase.GetAssetPath(original) + ":" + original.name + ":" +
                renderer.transform.lossyScale.ToString("F5") + ":" + renderer.transform.rotation.eulerAngles.ToString("F3");
            string path = MeshFolder + "/UV_" + Hash(signature) + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { filter.sharedMesh = existing; return; }
            var mesh = UnityEngine.Object.Instantiate(original); mesh.name = "ArtUV_" + original.name;
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
            var matrix = renderer.transform.localToWorldMatrix; matrix.SetColumn(3, new Vector4(0, 0, 0, 1));
            for (int i = 0; i < vertices.Length; i++)
            {
                var position = matrix.MultiplyPoint3x4(vertices[i]);
                var n = i < normals.Length ? matrix.inverse.transpose.MultiplyVector(normals[i]).normalized : Vector3.up;
                uv[i] = Project(position, n);
            }
            mesh.uv = uv; mesh.RecalculateTangents(); AssetDatabase.CreateAsset(mesh, path); filter.sharedMesh = mesh;
        }
        catch (Exception e) { Debug.LogWarning("ART_UV_RETAINED " + renderer.name + ": " + e.Message); }
    }

    static void BuildFloor()
    {
        var g = Group("ArtFloor");
        Box(g, "ContinuousWornConcrete", new Vector3(0, -.025f, 0), new Vector3(24, .05f, 24), p.concrete);
        // Flush expansion joints replace the dense prototype grid. All are visual and only millimetres high.
        for (int i = -8; i <= 8; i += 4)
        {
            Box(g, "ExpansionJointX", new Vector3(i, .001f, 0), new Vector3(.012f, .002f, 24), p.rubber, false);
            Box(g, "ExpansionJointZ", new Vector3(0, .001f, i), new Vector3(24, .002f, .012f), p.rubber, false);
        }
        if (p.grime)
        {
            foreach (var item in new[] { new Vector4(-10, -5, 2.1f, 1.1f), new Vector4(10, -9.5f, 1.9f, 1.2f), new Vector4(-9, 10, 2.2f, 1.1f), new Vector4(10, 5, 1.3f, 2.2f), new Vector4(-1.4f, 10, 1, 2.5f) })
                Box(g, "ShallowOilAndDampStain", new Vector3(item.x, .003f, item.y), new Vector3(item.z, .001f, item.w), p.grime, false);
        }
        // Paint remains flush with the floor and follows, rather than narrows, the established exits.
        for (int i = 0; i < 8; i++)
        {
            var stripe = Box(g, "WornLoadingStripe", new Vector3(6.95f + i * .3f, .004f, 10.95f), new Vector3(.12f, .003f, .7f), p.wood, false);
            stripe.transform.rotation = Quaternion.Euler(0, -25, 0);
        }
    }

    static void BuildRoof()
    {
        var g = Group("ArtRoof");
        Box(g, "SouthUpperWall", new Vector3(0, 3.3f, -12.16f), new Vector3(24.64f, 1.8f, .32f), p.concrete);
        Box(g, "NorthUpperWall", new Vector3(0, 3.3f, 12.16f), new Vector3(24.64f, 1.8f, .32f), p.concrete);
        Box(g, "WestUpperWall", new Vector3(-12.16f, 3.3f, 0), new Vector3(.32f, 1.8f, 24), p.concrete);
        Box(g, "EastUpperWall", new Vector3(12.16f, 3.3f, 0), new Vector3(.32f, 1.8f, 24), p.concrete);
        for (int x = -9; x <= 9; x += 6)
            for (int z = -9; z <= 9; z += 6)
                Box(g, "ConcreteRoofBay", new Vector3(x, 4.28f, z), new Vector3(6, .16f, 6), p.concrete);
        for (int z = -10; z <= 10; z += 4)
        {
            Box(g, "RoofSteelWeb", new Vector3(0, 3.96f, z), new Vector3(24, .40f, .10f), p.rustedMetal);
            Box(g, "RoofSteelLowerFlange", new Vector3(0, 3.76f, z), new Vector3(24, .06f, .30f), p.rustedMetal);
            Box(g, "RoofSteelUpperFlange", new Vector3(0, 4.14f, z), new Vector3(24, .06f, .30f), p.rustedMetal);
        }
        foreach (float x in new[] { -10f, -6f, -2f, 2f, 6f, 10f })
            Box(g, "RoofLongitudinalRail", new Vector3(x, 4.13f, 0), new Vector3(.08f, .10f, 24), p.rustedMetal);
        // Opaque, dirty glazing with local lights below it represents sealed roof lights, not walkable holes.
        foreach (var position in new[] { new Vector3(-7, 4.16f, 7), new Vector3(7, 4.16f, -7), new Vector3(7, 4.16f, 7) })
        {
            Box(g, "DustyRoofLightFrame", position, new Vector3(2.9f, .12f, 1.25f), p.rustedMetal);
            Box(g, "DustyRoofLightGlazing", position + Vector3.down * .071f, new Vector3(2.7f, .02f, 1.05f), p.coolEmission, false);
            for (int i = -1; i <= 1; i++)
                Box(g, "RoofLightMullion", position + new Vector3(i * .82f, -.09f, 0), new Vector3(.045f, .05f, 1.1f), p.rustedMetal);
        }
        Box(g, "WestServicePipe", new Vector3(-11.75f, 3.62f, 0), new Vector3(.12f, .12f, 24), p.rustedMetal);
        Box(g, "EastServiceTrunk", new Vector3(11.73f, 3.69f, 0), new Vector3(.28f, .18f, 24), p.paintedMetal);
        Box(g, "CableTray", new Vector3(0, 3.5f, -11.7f), new Vector3(24, .10f, .24f), p.rustedMetal);
        for (int z = -10; z <= 10; z += 4)
        {
            Box(g, "PipeWallBracket", new Vector3(-11.94f, 3.53f, z), new Vector3(.35f, .07f, .08f), p.rustedMetal);
            Box(g, "TrunkWallBracket", new Vector3(11.94f, 3.57f, z), new Vector3(.35f, .07f, .08f), p.rustedMetal);
        }
    }

    static void BuildLighting()
    {
        var g = Group("ArtLighting");
        Vector2[] locations = { new Vector2(-8,-8), new Vector2(0,-7), new Vector2(-7,0), new Vector2(7,0),
            new Vector2(0,7), new Vector2(-8,7), new Vector2(8,10), new Vector2(8,-7), new Vector2(-7,10), new Vector2(5,4), new Vector2(4,-10), new Vector2(10,4) };
        for (int i = 0; i < locations.Length; i++)
        {
            var loc = locations[i]; var center = new Vector3(loc.x, 3.3f, loc.y);
            bool warm = i == 0 || i == 5 || i == 10;
            Box(g, "HangingWorkLightHousing_" + i, center, new Vector3(1.1f, .12f, .25f), p.paintedMetal);
            Box(g, "WorkLightDiffuser_" + i, center + Vector3.down * .068f, new Vector3(.92f, .018f, .17f), warm ? p.warmEmission : p.coolEmission, false);
            foreach (float x in new[] { -.43f, .43f })
                Box(g, "LampSuspension_" + i, center + new Vector3(x, .40f, 0), new Vector3(.016f, .72f, .016f), p.rustedMetal, false);
            bool shadows = i == 0 || i == 4 || i == 5 || i == 6;
            var light = Light(g, "StableWorkLight_" + i, center + Vector3.down * .12f, warm ? p.warmLight : p.coolLight,
                p.workLightIntensity * (i == 0 ? 1.12f : .86f), 8.2f, shadows);
            light.type = LightType.Spot; light.spotAngle = 112; light.innerSpotAngle = 76;
            light.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
        foreach (var loc in new[] { new Vector3(-7, 3.98f, 7), new Vector3(7, 3.98f, -7), new Vector3(7, 3.98f, 7) })
        {
            var light = Light(g, "RoofLeakFill", loc, p.coolLight, p.workLightIntensity * .7f, 9, false);
            light.type = LightType.Spot; light.spotAngle = 115; light.innerSpotAngle = 85; light.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
        // Decorative flickers do not provide the sole illumination for any gameplay route.
        Vector3[] secondary = { new Vector3(11.7f, 2.8f, -10.8f), new Vector3(-11.7f, 2.8f, 10.8f) };
        for (int i = 0; i < secondary.Length; i++)
        {
            var location = secondary[i];
            Box(g, "FailingWallFixture_" + i, location, new Vector3(.12f, .3f, .16f), p.rustedMetal);
            Light(g, "ArtFlicker_" + i, location + (i == 0 ? Vector3.left : Vector3.right) * .15f, p.warmLight, .55f, 2.4f, false);
        }
    }

    public static void ApplyReadabilityLights(ChapterArtProfile profile)
    {
        var parent=GameObject.Find("ArtLighting").transform;
        Vector3[] points={new Vector3(-8,2.8f,-8),new Vector3(8,2.8f,-7),new Vector3(-8,2.8f,7),new Vector3(8,2.8f,10)};
        for(int i=0;i<points.Length;i++)
        {
            string name="WorkLightBounce_"+i;
            if(GameObject.Find(name))continue;
            Light(parent,name,points[i],i==0||i==2?profile.warmLight:profile.coolLight,profile.workLightIntensity*.25f,8,false);
        }
    }

    static Light Light(Transform parent, string name, Vector3 position, Color color, float intensity, float range, bool shadows)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None; light.shadowStrength = .75f; light.shadowBias = .035f; light.shadowNormalBias = .15f;
        light.renderMode = LightRenderMode.Auto; return light;
    }

    static void BuildCargo(ExpandedSession session, ExpandedLevelDefinition.CoverSpec spec)
    {
        Transform old = Find(session, spec.name); if (!old) throw new InvalidOperationException("Accepted cover missing: " + spec.name);
        foreach (var r in old.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var g = Group("ArtCargo_" + spec.name); Vector3 c = spec.position, size = spec.size;
        bool tarped = spec.name.Contains("Observation") || spec.name.Contains("Loading");
        bool cabinet = spec.name.Contains("Equipment");
        // The continuous opaque core maintains the full accepted occluder silhouette, including at floor level.
        Box(g, "ClosedOpaqueCargoBacking", c, size - new Vector3(.035f,.035f,.035f), cabinet ? p.paintedMetal : tarped ? p.fabric : p.wood);
        if (tarped)
        {
            for (int i = -1; i <= 1; i++)
            {
                Box(g, "TarpBindingFront", c + new Vector3(i * size.x * .27f, 0, -size.z * .5f + .006f), new Vector3(.035f, size.y * .98f, .014f), p.rubber, false);
                Box(g, "TarpBindingTop", c + new Vector3(i * size.x * .27f, size.y * .5f - .006f, 0), new Vector3(.035f, .014f, size.z * .98f), p.rubber, false);
            }
        }
        else
        {
            int cols = Mathf.Max(2, Mathf.CeilToInt(size.x / .8f)); int rows = Mathf.Max(2, Mathf.FloorToInt(size.y / .55f));
            float w = (size.x - .12f) / cols, h = (size.y - .14f) / rows;
            for (int face = 0; face < 2; face++) for (int row = 0; row < rows; row++) for (int col = 0; col < cols; col++)
            {
                float sign = face == 0 ? -1 : 1;
                var position = new Vector3(c.x - size.x * .5f + .06f + (col + .5f) * w, .07f + (row + .5f) * h, c.z + sign * (size.z * .5f - .027f));
                Box(g, "StoredCargoBox", position, new Vector3(w - .018f, h - .018f, .052f), cabinet ? p.paintedMetal : (col + row + face) % 3 == 0 ? p.wood : p.cardboard);
                if (!cabinet)
                    Box(g, "BoxPackingTape", position + new Vector3(0, 0, sign * .027f), new Vector3(w * .13f, h - .02f, .002f), p.wood, false);
                else
                    Box(g, "CabinetLatch", position + new Vector3(w * .31f, 0, sign * .029f), new Vector3(.025f, .12f, .007f), p.rubber, false);
            }
        }
        foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { -.5f, .5f })
            Box(g, "RustSteelCorner", c + new Vector3(x * (size.x - .055f), 0, z * (size.z - .055f)), new Vector3(.055f, size.y, .055f), p.rustedMetal);
        Box(g, "DustySteelTop", c + Vector3.up * (size.y * .5f - .015f), new Vector3(size.x, .03f, size.z), tarped ? p.fabric : p.rustedMetal);
        foreach (float y in new[] { .10f, size.y * .5f, size.y - .08f })
            foreach (float z in new[] { -.5f, .5f })
                Box(g, "CargoHorizontalRail", new Vector3(c.x, y, c.z + z * (size.z - .032f)), new Vector3(size.x, .055f, .032f), p.rustedMetal);
        Plate(g, "货物 " + (Array.IndexOf(session.level.coverSpecs, spec) + 1).ToString("00") + "\nSEALED / STORAGE", new Vector3(c.x, Mathf.Min(1.35f,size.y-.2f), c.z - size.z*.5f - .002f), Vector3.back, .46f, .18f);
    }

    static void BuildWallDetails(ExpandedSession session)
    {
        var g = Group("ArtZoneDressing");
        // Thin wall dressings remain within the clearance already blocked by the wall/controller radius.
        for (int z = -10; z <= 10; z += 4)
        {
            Box(g, "WestRustedWallRib", new Vector3(-11.98f, 1.2f, z), new Vector3(.045f, 2.4f, .10f), p.rustedMetal);
            Box(g, "EastRustedWallRib", new Vector3(11.98f, 1.2f, z), new Vector3(.045f, 2.4f, .10f), p.rustedMetal);
        }
        Box(g, "MaintenanceToolboard", new Vector3(-11.94f, 1.25f, -8.5f), new Vector3(.08f,.8f,1.45f), p.wood);
        for (int i = 0; i < 5; i++)
        {
            float z = -9.03f + i*.25f;
            Box(g, "ToolBoardHook", new Vector3(-11.88f,1.48f,z), new Vector3(.045f,.035f,.035f), p.rustedMetal,false);
            if (i == 1 || i == 3) continue;
            Box(g, "AbandonedToolHandle", new Vector3(-11.88f,1.27f,z), new Vector3(.025f,.30f,.045f),p.rubber,false);
            Box(g, "AbandonedToolHead", new Vector3(-11.88f,1.44f,z), new Vector3(.035f,.07f,.13f),p.rustedMetal,false);
        }
        Plate(g,"01 / 维修站\n设备已停用",new Vector3(-11.87f,2.0f,-8.5f),Vector3.right,1.3f,.38f);
        Plate(g,"02 / 仓储\n货物封存  NO ENTRY",new Vector3(9.5f,1.6f,-11.96f),Vector3.forward,1.7f,.46f);
        Plate(g,"03 / 配电设备\n检修延期",new Vector3(-11.96f,1.9f,7.8f),Vector3.right,1.5f,.42f);
        Plate(g,"04 / 装卸站\nDOCK 04",new Vector3(11.96f,1.75f,9.5f),Vector3.left,1.4f,.45f);
        // Wall wiring runs above head height and leaves every existing opening clear.
        for (int i = 0; i < 3; i++)
        {
            Box(g,"EquipmentConduit",new Vector3(-11.92f,2.16f+i*.07f,6),new Vector3(.045f,.025f,10),p.rustedMetal,false);
            Box(g,"ConduitDrop",new Vector3(-11.92f,1.75f,9+i*.09f),new Vector3(.045f,.9f,.025f),p.rustedMetal,false);
        }
        Box(g,"OldJunctionBox",new Vector3(-11.94f,1.38f,9.1f),new Vector3(.10f,.40f,.38f),p.paintedMetal);
        Plate(g,"高压 / 仅限维护",new Vector3(-11.878f,1.4f,9.1f),Vector3.right,.31f,.16f);
        if(p.grime)
        {
            for(int i=0;i<7;i++)
            {
                float z=-10+i*3.1f;
                Box(g,"WestDampWallFoot",new Vector3(-11.993f,.23f,z),new Vector3(.004f,.46f,1.5f),p.grime,false);
                Box(g,"EastRustRun",new Vector3(11.993f,1.1f,z+.8f),new Vector3(.004f,1.8f,.34f),p.grime,false);
            }
        }
    }

    static void BuildDevices(ExpandedSession session)
    {
        foreach (var target in session.interaction.targets)
        {
            bool charger=target.kind==InteractionKind.Charger;
            string modelName=charger?"ChargingDock_"+Array.IndexOf(session.interaction.targets,target):"SupplyTerminal";
            // Target arrays are type ordered in the accepted chapter; parse the actual charger name as the source of truth.
            if(charger && target.name.StartsWith("Charger_",StringComparison.Ordinal))modelName="ChargingDock_"+target.name.Substring("Charger_".Length);
            var model=Find(session,modelName);if(!model)throw new InvalidOperationException("Device model missing "+modelName);
            var rs=model.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();if(rs.Length==0)continue;
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
            var facing=charger?Vector3.forward:Vector3.back;
            var front=new Vector3(b.center.x,Mathf.Min(b.max.y-.09f,charger?.66f:.88f),charger?b.max.z-.003f:b.min.z+.003f);
            var panel=Box(model,"ArtDevicePanel",front,new Vector3(Mathf.Min(.32f,b.size.x*.7f),.18f,.015f),p.rubber,false);
            Box(model,"ArtDeviceStatusStrip",front+new Vector3(0,-.052f,facing.z*.011f),new Vector3(.22f,.018f,.006f),p.mintEmission,false);
            Plate(model,charger?"应急电源\nCHARGE":"出口电源\nCONTROL",front+facing*.014f+Vector3.up*.023f,facing,.24f,.10f);
            foreach(float x in new[]{-.13f,.13f})foreach(float y in new[]{-.067f,.067f})
                Box(model,"PanelFastener",front+new Vector3(x,y,facing.z*.013f),new Vector3(.012f,.012f,.008f),p.rustedMetal,false);
            Light(model,"DevicePracticalLight",front+facing*.13f,p.accentColor,.32f,1.7f,false);
        }
    }

    static void BuildExit(ExpandedSession session)
    {
        var door=session.door;
        foreach(var leaf in new[]{door.leftLeaf,door.rightLeaf})
        {
            leaf.GetComponent<Renderer>().sharedMaterial=p.paintedMetal;
            RetileImportedMesh(leaf.GetComponent<MeshRenderer>());
            // Build at world scale first, then keep world transforms while parenting under scaled moving leaves.
            Vector3 c=leaf.position;var trim=Group("ArtMovingDoorSkin");
            for(int i=-1;i<=1;i++)Box(trim,"DoorReinforcingRib",c+new Vector3(0,i*.65f,-.078f),new Vector3(1.08f,.045f,.018f),p.rustedMetal);
            foreach(float x in new[]{-.49f,.49f})Box(trim,"DoorVerticalRail",c+new Vector3(x,0,-.078f),new Vector3(.04f,2.02f,.018f),p.rustedMetal);
            Box(trim,"RecessedDoorHandle",c+new Vector3(leaf==door.leftLeaf?.38f:-.38f,-.1f,-.082f),new Vector3(.045f,.25f,.025f),p.rubber,false);
            trim.SetParent(leaf,true);
        }
        var g=Group("ArtExitDetails");var exit=session.level.exitPosition;
        Box(g,"HeaderDoorTrack",exit+new Vector3(0,2.55f,-.08f),new Vector3(5.3f,.14f,.16f),p.rustedMetal);
        Box(g,"ExitLightHousing",exit+new Vector3(0,2.8f,-.16f),new Vector3(.9f,.15f,.22f),p.paintedMetal);
        Box(g,"ExitLightDiffuser",exit+new Vector3(0,2.79f,-.28f),new Vector3(.75f,.065f,.015f),p.coolEmission,false);
        Light(g,"ExitPlatformFill",new Vector3(exit.x,2.9f,13.4f),p.coolLight,1.2f,4,false);
    }

    static void BuildRobot(ExpandedSession session)
    {
        Transform enemy=session.enemy.transform;
        var g=Group("ArtGuardShell");g.SetPositionAndRotation(enemy.position,enemy.rotation);g.SetParent(enemy,true);
        // All details fit inside the original compact robot silhouette and travel with its existing root.
        LocalBox(g,"RearAccessCover",new Vector3(0,.58f,-.255f),new Vector3(.36f,.28f,.025f),p.paintedMetal);
        for(int i=-2;i<=2;i++)LocalBox(g,"CoolingLouvre",new Vector3(i*.055f,.58f,-.271f),new Vector3(.018f,.17f,.012f),p.rubber);
        foreach(float x in new[]{-.19f,.19f})foreach(float y in new[]{.46f,.70f})
            LocalBox(g,"RobotFastener",new Vector3(x,y,-.272f),new Vector3(.018f,.018f,.014f),p.rustedMetal);
        LocalBox(g,"ScannerVisor",new Vector3(0,.85f,.222f),new Vector3(.26f,.11f,.045f),p.rubber);
        LocalBox(g,"ScannerGlass",new Vector3(0,.85f,.249f),new Vector3(.20f,.065f,.012f),p.coolEmission);
    }

    static void LocalBox(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=Box(root,name,Vector3.zero,size,material,false);go.transform.SetParent(parent,false);go.transform.localPosition=position;
    }

    static void Plate(Transform parent,string label,Vector3 position,Vector3 viewer,float width,float height)
    {
        var g=Group("ArtPlate_"+label.Replace('\n','_'),parent);g.position=position;g.rotation=Quaternion.LookRotation(-viewer,Vector3.up);
        var back=Box(root,"PlateBacking",Vector3.zero,new Vector3(width,height,.006f),p.paintedMetal,false);
        back.transform.SetParent(g,false);back.transform.localPosition=Vector3.zero;
        if(!font)return;
        var textObject=new GameObject("Inscription",typeof(TextMeshPro));textObject.transform.SetParent(g,false);textObject.transform.localPosition=new Vector3(0,0,-.005f);
        var text=textObject.GetComponent<TextMeshPro>();text.font=font;text.text=label;text.fontSize=height*30; text.enableAutoSizing=true;text.fontSizeMin=.7f;text.fontSizeMax=height*30;
        text.rectTransform.sizeDelta=new Vector2(width*.90f,height*.85f);text.alignment=TextAlignmentOptions.Center;text.color=new Color(.77f,.76f,.67f);text.textWrappingMode=TextWrappingModes.NoWrap;
        text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }

    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool shadows=true)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.position=position;
        bool bevel=name=="StoredCargoBox"||name=="ClosedOpaqueCargoBacking"||name=="ArtDevicePanel"||name=="RearAccessCover"||name=="ScannerVisor"||name=="OldJunctionBox";
        go.GetComponent<MeshFilter>().sharedMesh=material==p.grime?DecalMesh(size,position):bevel?BeveledBoxMesh(size):BoxMesh(size);
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;
        boxCount++;return go;
    }

    static Mesh BoxMesh(Vector3 size)
    {
        string key="box-v3:"+size.ToString("F5");if(meshes.TryGetValue(key,out var found))return found;
        string path=MeshFolder+"/Box_"+Hash(key)+".asset";found=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(found){meshes[key]=found;return found;}
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        var h=size*.5f;
        void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        {
            int start=vertices.Count;vertices.AddRange(new[]{a,b,c,d});normals.AddRange(new[]{normal,normal,normal,normal});
            foreach(var v in new[]{a,b,c,d})uv.Add(Project(v,normal));
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        Face(new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z),Vector3.forward);
        Face(new Vector3(h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z),Vector3.back);
        Face(new Vector3(h.x,-h.y,h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z),Vector3.right);
        Face(new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z),Vector3.left);
        Face(new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),Vector3.up);
        Face(new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z),Vector3.down);
        var mesh=new Mesh{name="ArtBox_"+Hash(key)};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh,path);meshes[key]=mesh;return mesh;
    }

    static Mesh DecalMesh(Vector3 size,Vector3 location)
    {
        Vector3 normal;Vector3 horizontal,vertical;float width,height;
        if(size.y<=size.x&&size.y<=size.z){normal=Vector3.up;horizontal=Vector3.right;vertical=Vector3.forward;width=size.x;height=size.z;}
        else if(size.x<=size.z){normal=location.x<0?Vector3.right:Vector3.left;horizontal=Vector3.forward;vertical=Vector3.up;width=size.z;height=size.y;}
        else{normal=location.z<0?Vector3.forward:Vector3.back;horizontal=Vector3.right;vertical=Vector3.up;width=size.x;height=size.y;}
        string key="decal-v1:"+size.ToString("F5")+normal.ToString("F0");
        if(meshes.TryGetValue(key,out var result))return result;
        string path=MeshFolder+"/Decal_"+Hash(key)+".asset";result=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(result){meshes[key]=result;return result;}
        var a=horizontal*width*.5f;var b=vertical*height*.5f;
        var vertices=new[]{-a-b,a-b,a+b,-a+b};
        bool forward=Vector3.Dot(Vector3.Cross(vertices[1]-vertices[0],vertices[2]-vertices[0]),normal)>0;
        result=new Mesh{name="ArtDecal_"+Hash(key)};result.vertices=vertices;result.normals=new[]{normal,normal,normal,normal};
        result.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
        result.triangles=forward?new[]{0,1,2,0,2,3}:new[]{0,2,1,0,3,2};result.RecalculateBounds();result.RecalculateTangents();
        AssetDatabase.CreateAsset(result,path);meshes[key]=result;return result;
    }

    static Mesh BeveledBoxMesh(Vector3 size)
    {
        float bevel=Mathf.Min(.022f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.22f);
        string key="bevel-v1:"+size.ToString("F5");if(meshes.TryGetValue(key,out var result))return result;
        string path=MeshFolder+"/Bevel_"+Hash(key)+".asset";result=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(result){meshes[key]=result;return result;}
        var h=size*.5f;var inner=h-Vector3.one*bevel;
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        void Polygon(Vector3 normal,params Vector3[] points)
        {
            if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)Array.Reverse(points);
            int start=vertices.Count;
            foreach(var point in points){vertices.Add(point);normals.Add(normal);uv.Add(Project(point,normal));}
            for(int i=1;i<points.Length-1;i++)triangles.AddRange(new[]{start,start+i,start+i+1});
        }
        foreach(float sign in new[]{-1f,1f})
        {
            Polygon(Vector3.right*sign,new Vector3(sign*h.x,-inner.y,-inner.z),new Vector3(sign*h.x,inner.y,-inner.z),new Vector3(sign*h.x,inner.y,inner.z),new Vector3(sign*h.x,-inner.y,inner.z));
            Polygon(Vector3.up*sign,new Vector3(-inner.x,sign*h.y,-inner.z),new Vector3(inner.x,sign*h.y,-inner.z),new Vector3(inner.x,sign*h.y,inner.z),new Vector3(-inner.x,sign*h.y,inner.z));
            Polygon(Vector3.forward*sign,new Vector3(-inner.x,-inner.y,sign*h.z),new Vector3(inner.x,-inner.y,sign*h.z),new Vector3(inner.x,inner.y,sign*h.z),new Vector3(-inner.x,inner.y,sign*h.z));
        }
        foreach(float a in new[]{-1f,1f})foreach(float b in new[]{-1f,1f})
        {
            Polygon(new Vector3(a,b,0).normalized,new Vector3(a*h.x,b*inner.y,-inner.z),new Vector3(a*h.x,b*inner.y,inner.z),new Vector3(a*inner.x,b*h.y,inner.z),new Vector3(a*inner.x,b*h.y,-inner.z));
            Polygon(new Vector3(a,0,b).normalized,new Vector3(a*h.x,-inner.y,b*inner.z),new Vector3(a*h.x,inner.y,b*inner.z),new Vector3(a*inner.x,inner.y,b*h.z),new Vector3(a*inner.x,-inner.y,b*h.z));
            Polygon(new Vector3(0,a,b).normalized,new Vector3(-inner.x,a*h.y,b*inner.z),new Vector3(inner.x,a*h.y,b*inner.z),new Vector3(inner.x,a*inner.y,b*h.z),new Vector3(-inner.x,a*inner.y,b*h.z));
        }
        foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
            Polygon(new Vector3(x,y,z).normalized,new Vector3(x*h.x,y*inner.y,z*inner.z),new Vector3(x*inner.x,y*h.y,z*inner.z),new Vector3(x*inner.x,y*inner.y,z*h.z));
        result=new Mesh{name="ArtBeveledBox_"+Hash(key)};result.SetVertices(vertices);result.SetNormals(normals);result.SetUVs(0,uv);result.SetTriangles(triangles,0);
        result.RecalculateBounds();result.RecalculateTangents();AssetDatabase.CreateAsset(result,path);meshes[key]=result;return result;
    }

    static Vector2 Project(Vector3 point,Vector3 normal)
    {
        var n=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
        if(n.y>=n.x&&n.y>=n.z)return new Vector2(point.x,point.z);
        if(n.x>=n.z)return new Vector2(point.z,point.y);
        return new Vector2(point.x,point.y);
    }

    static string Hash(string value)
    {
        using(var algorithm=SHA256.Create())return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").Substring(0,18).ToLowerInvariant();
    }
}
