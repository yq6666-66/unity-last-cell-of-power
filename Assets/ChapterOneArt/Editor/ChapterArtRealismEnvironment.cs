using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.WarehouseExpanded;

/// <summary>R2 realism layer. Applied after R1 generation; all gameplay geometry is read-only.</summary>
public static class ChapterArtRealismEnvironment
{
    const string MeshRoot = "Assets/ChapterOneArt/Meshes/Realism";
    const string MaterialRoot = "Assets/ChapterOneArt/Materials/";
    static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
    static Transform root;
    static ChapterArtProfile p;
    static Material concrete, peeling, wet, yellow;
    static TMP_FontAsset font;
    static int modelCount;

    public static void Apply(ExpandedSession session, ChapterArtProfile profile)
    {
        if (!session || !profile || !session.level) throw new ArgumentException("R2 art requires an accepted session and profile");
        if (Find(session, "ArtRealism")) throw new InvalidOperationException("R2 already exists; regenerate from the preserved source");
        foreach (string id in new[] { "wooden_crate_02", "old_military_crate", "steel_frame_shelves_03" })
            if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChapterOneArt/Models/" + id + "/Model.fbx"))
                throw new InvalidOperationException("R2_CC0_MODEL_REQUIRED " + id);
        p = profile; modelCount = 0; Meshes.Clear();
        concrete = Material("ART_WallConcrete", p.concrete);
        peeling = Material("ART_PeelingPaint", p.paintedMetal);
        wet = Material("ART_WetConcrete", concrete);
        yellow = Material("ART_WornYellow", peeling);
        font = UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>().font;
        Directory.CreateDirectory(MeshRoot); AssetDatabase.Refresh();
        root = new GameObject("ArtRealism").transform;
        ReplaceWalls(session);
        BuildIndustrialServices();
        DressConnections(session);
        RebuildCargo(session);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
        WallSurfaceRepair.Apply(session.gameObject.scene,"Assets/WallSurfaceFix/Meshes/GeneratedArt");
        AssetDatabase.SaveAssets();
        Debug.Log("ART_REALISM_CREATED CC0Models=" + modelCount + "; walls use unchanged collision dimensions; no new colliders/navigation");
    }

    /// <summary>Repair only generated R2 cargo, retaining lighting, baked probes and all original objects.</summary>
    public static void RepairCargo(ExpandedSession session, ChapterArtProfile profile)
    {
        if(!session || !profile)throw new ArgumentException("Cargo repair requires session and art profile");
        root=Find(session,"ArtRealism");if(!root)throw new InvalidOperationException("R2 art root missing");
        foreach(string id in new[]{"wooden_crate_02","old_military_crate","steel_frame_shelves_03"})
            if(!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChapterOneArt/Models/"+id+"/Model.fbx"))throw new InvalidOperationException("Cargo model missing: "+id);
        var military=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChapterOneArt/Models/old_military_crate/Model.fbx");
        if(military.GetComponentsInChildren<Transform>(true).Any(t=>t.name.EndsWith("_b",StringComparison.Ordinal)))
            throw new InvalidOperationException("Re-export the military crate as closed _a before repairing cargo");
        var generated=root.Cast<Transform>().Where(t=>t.name=="RealismCargo" || t.name.StartsWith("RealCargo_",StringComparison.Ordinal)).ToArray();
        foreach(var child in generated)
            if(child.GetComponentsInChildren<Collider>(true).Length>0 || child.GetComponentsInChildren<NavMeshObstacle>(true).Length>0)
                throw new InvalidOperationException("Refusing to remove cargo layer containing gameplay geometry: "+child.name);
        p=profile;modelCount=0;Meshes.Clear();concrete=Material("ART_WallConcrete",p.concrete);peeling=Material("ART_PeelingPaint",p.paintedMetal);
        wet=Material("ART_WetConcrete",concrete);yellow=Material("ART_WornYellow",peeling);font=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>().font;
        Directory.CreateDirectory(MeshRoot);AssetDatabase.Refresh();
        foreach(var child in generated)UnityEngine.Object.DestroyImmediate(child.gameObject);
        RebuildCargo(session);
        var cargo=root.Find("RealismCargo");
        foreach(var r in cargo.GetComponentsInChildren<MeshRenderer>(true))GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);
        AssetDatabase.SaveAssets();
        Debug.Log("ART_CARGO_REPAIRED models="+modelCount+"; front curtains removed; stacked using actual bounds; other scene roots preserved");
    }

    static Material Material(string name, Material fallback) => AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + name + ".mat") ?? fallback;
    static Transform Find(ExpandedSession s, string name) => s.gameObject.scene.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
    static Transform Group(string name, Transform parent = null)
    {
        var g = new GameObject(name).transform; g.SetParent(parent ? parent : root, false); return g;
    }

    static bool IsWallName(string name) => name.StartsWith("South", StringComparison.Ordinal) ||
        name.StartsWith("NorthWest", StringComparison.Ordinal) || name.StartsWith("NorthEast", StringComparison.Ordinal) ||
        name.StartsWith("West", StringComparison.Ordinal) || name.StartsWith("East", StringComparison.Ordinal) ||
        name.StartsWith("Divider", StringComparison.Ordinal);

    static void ReplaceWalls(ExpandedSession session)
    {
        var visuals = Find(session, "ExpandedWarehouseVisuals");
        var collisions = Find(session, "ExpandedCollisionWorld");
        if (!visuals || !collisions) throw new InvalidOperationException("Accepted wall/collision roots missing");
        foreach (Transform child in visuals)
            if (IsWallName(child.name)) foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var walls = Group("ConcreteWallSkins");
        foreach (var collider in collisions.GetComponentsInChildren<BoxCollider>(true))
        {
            if (!IsWallName(collider.name)) continue;
            Vector3 center = collider.transform.TransformPoint(collider.center);
            Vector3 size = Vector3.Scale(collider.size, collider.transform.lossyScale);
            // Wall transforms in the accepted map are axis aligned. Refuse a future incompatible map.
            if (Quaternion.Angle(collider.transform.rotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException("R2 expects axis-aligned source wall: " + collider.name);
            float bottom = center.y - size.y * .5f;
            float paintHeight = Mathf.Min(1.02f, size.y);
            Box(walls, collider.name + "_OldLowerPaint", new Vector3(center.x, bottom + paintHeight * .5f, center.z),
                new Vector3(size.x, paintHeight, size.z), peeling);
            Box(walls, collider.name + "_DampUpperConcrete", new Vector3(center.x, bottom + paintHeight + (size.y - paintHeight) * .5f, center.z),
                new Vector3(size.x, size.y - paintHeight, size.z), concrete);
            Box(walls, collider.name + "_DampSkirting", new Vector3(center.x, bottom + .065f, center.z), new Vector3(size.x + .002f, .13f, size.z + .002f), wet);
            // Tiny mortar reveal is a surface seam, not a deep hole or extra obstacle.
            Box(walls, collider.name + "_PaintEdge", new Vector3(center.x, bottom + paintHeight, center.z), new Vector3(size.x + .001f, .012f, size.z + .001f), wet);
        }
        var oldArt = Find(session, "ArtWorld");
        if (oldArt)
            foreach (var r in oldArt.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.EndsWith("UpperWall", StringComparison.Ordinal)) r.sharedMaterial = concrete;
                if (r.name == "WestRustedWallRib" || r.name == "EastRustedWallRib" || r.name == "EquipmentConduit" || r.name == "ConduitDrop" || r.name == "ConduitWallBracket") r.enabled = false;
                if (r.name == "WornLoadingStripe") r.sharedMaterial = yellow;
            }
        Label(walls, "MAINTENANCE / 01", new Vector3(-11.992f, 1.83f, -4.8f), Vector3.right, 1.5f, .18f);
        Label(walls, "WAREHOUSE / 02", new Vector3(11.992f, 1.83f, -6.2f), Vector3.left, 1.5f, .18f);
        Label(walls, "POWER CONTROL / 03", new Vector3(-11.992f, 1.83f, 4.6f), Vector3.right, 1.7f, .18f);
    }

    static void BuildIndustrialServices()
    {
        var pipes = Group("AgedIndustrialPipework");
        // Main wall pipes sit above actor height; vertical risers stay in the inaccessible wall margin.
        Pipe(pipes, "WestSupplyPipe", new Vector3(-11.89f, 2.88f, -10.5f), new Vector3(-11.89f, 2.88f, 10.5f), .065f);
        Pipe(pipes, "WestPipeRiser", new Vector3(-11.89f, .45f, 10.5f), new Vector3(-11.89f, 2.88f, 10.5f), .065f);
        Pipe(pipes, "EastReturnPipe", new Vector3(11.89f, 2.7f, -10.5f), new Vector3(11.89f, 2.7f, 10.5f), .052f);
        Pipe(pipes, "SouthFeedPipe", new Vector3(-10.5f, 2.88f, -11.89f), new Vector3(10.5f, 2.88f, -11.89f), .065f);
        Pipe(pipes, "EquipmentInstrumentLine", new Vector3(-11.91f, 2.2f, 2), new Vector3(-11.91f, 2.2f, 9.8f), .021f);
        for (int z = -9; z <= 9; z += 6)
        {
            Flange(pipes, new Vector3(-11.89f, 2.88f, z), Vector3.forward, .096f);
            Flange(pipes, new Vector3(11.89f, 2.7f, z), Vector3.forward, .082f);
            Box(pipes, "PipeWallBracket", new Vector3(-11.96f, 2.78f, z + 1.4f), new Vector3(.08f, .04f, .15f), p.rustedMetal, false);
        }
        Flange(pipes, new Vector3(-11.89f, 1.23f, 10.5f), Vector3.up, .096f);
        // Small relief rings imply utility infrastructure without adding a walk-through valve wheel.
        Pipe(pipes, "ValveStem", new Vector3(-11.89f, 1.65f, 10.5f), new Vector3(-11.76f, 1.65f, 10.5f), .018f);
        Flange(pipes, new Vector3(-11.77f, 1.65f, 10.5f), Vector3.right, .06f);
    }

    static void DressConnections(ExpandedSession session)
    {
        var g = Group("PortalWearAndFloorMarks");
        int number = 0;
        foreach (var c in session.level.connectionCenters)
        {
            bool inXDivider = Mathf.Abs(c.x) < .01f;
            Vector3 along = inXDivider ? Vector3.forward : Vector3.right;
            Vector3 across = inXDivider ? Vector3.right : Vector3.forward;
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 pos = c + along * sign * (session.level.connectionWidth * .5f + .026f) + Vector3.up * 1.18f;
                Box(g, "PortalRustJamb_" + number, pos, inXDivider ? new Vector3(.324f, 2.36f, .052f) : new Vector3(.052f, 2.36f, .324f), p.rustedMetal);
                Box(g, "PortalSafetyPaint_" + number, pos + Vector3.up * -.65f,
                    inXDivider ? new Vector3(.326f, .38f, .05f) : new Vector3(.05f, .38f, .326f), yellow, false);
            }
            Box(g, "PortalTopFlashing_" + number, c + Vector3.up * 2.45f,
                inXDivider ? new Vector3(.32f, .10f, session.level.connectionWidth + .104f) : new Vector3(session.level.connectionWidth + .104f, .10f, .32f), p.rustedMetal);
            // Flat broken guide paint is placed inside the already-clear connection, not at eye level.
            foreach (float side in new[] { -1f, 1f })
                for (int j = -2; j <= 2; j++)
                {
                    Vector3 mark = c + along * side * 1.25f + across * j * .52f + Vector3.up * .0045f;
                    Box(g, "FadedPortalGuide_" + number, mark,
                        inXDivider ? new Vector3(.34f, .001f, .045f) : new Vector3(.045f, .001f, .34f), yellow, false);
                }
            number++;
        }
        // Fixed loading marks frame the exit approach without adding new geometry to walk around.
        for (int i = 0; i < 9; i++)
            Box(g, "WornDockCenterLine", new Vector3(8, .0045f, 7.7f + i * .4f), new Vector3(.055f, .001f, .22f), yellow, false);
    }

    static void RebuildCargo(ExpandedSession session)
    {
        var cargoRoot=Group("RealismCargo");
        for (int index = 0; index < session.level.coverSpecs.Length; index++)
        {
            var spec = session.level.coverSpecs[index]; var old = Find(session, "ArtCargo_" + spec.name);
            if (!old) throw new InvalidOperationException("R1 cargo missing " + spec.name);
            // R1's boxes are our own generated visual layer; preserve the actual cover Collider separately.
            foreach (var r in old.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var g = Group("RealCargo_" + spec.name,cargoRoot);
            bool sideways = spec.size.z > spec.size.x;
            float width = sideways ? spec.size.z : spec.size.x;
            float depth = sideways ? spec.size.x : spec.size.z;
            float height = spec.size.y;
            Vector3 right = sideways ? Vector3.forward : Vector3.right;
            Vector3 front = sideways ? Vector3.left : Vector3.back;
            Vector3 back = -front;
            float yaw = sideways ? 90 : 0;
            // A weathered closed back and sides communicate the same solid volume as the accepted cover.
            // All broad opaque backing belongs BEHIND the real props, never in front of their bodies.
            Box(g, "ClosedCargoRear", spec.position + back * (depth * .5f - .016f),
                sideways ? new Vector3(.032f,height,width) : new Vector3(width,height,.032f), index == 5 ? peeling : p.wood);
            foreach (float sign in new[] { -1f, 1f })
                Box(g, "ClosedCargoSide", spec.position + right * sign * (width*.5f-.016f),
                    sideways ? new Vector3(depth,height,.032f) : new Vector3(.032f,height,depth), index % 3 == 0 ? p.fabric : p.wood);
            Box(g, "RearCargoLining", spec.position + back * (depth*.5f-.050f),
                sideways ? new Vector3(.024f,height,width-.035f) : new Vector3(width-.035f,height,.024f), index % 2 == 0 ? p.fabric : p.cardboard);
            Box(g, "CoveredCargoTop", spec.position + Vector3.up*(height*.5f-.012f),
                new Vector3(spec.size.x,.024f,spec.size.z), index % 2 == 0 ? p.fabric : p.wood);
            for (int i=0;i<3;i++)
            {
                float side=-width*.5f+.11f+i*(width-.22f)*.5f;
                var band=spec.position+right*side+back*(depth*.5f-.002f);
                Box(g,"CrateRearBand",band,sideways?new Vector3(.008f,height,.045f):new Vector3(.045f,height,.008f),p.rustedMetal,false);
            }
            bool shelf = index == 2 || index == 3 || index == 5;
            Vector3 fullCenter = spec.position;
            Bounds volume = new Bounds(fullCenter, spec.size - new Vector3(.08f,.06f,.08f));
            if (shelf)
            {
                FitModel(g,"steel_frame_shelves_03",volume,yaw,"RealSteelRack",front);
                var loadSize = sideways ? new Vector3(depth*.86f,height*.36f,width*.78f) : new Vector3(width*.78f,height*.36f,depth*.86f);
                var loadCenter=spec.position+Vector3.down*(height*.26f)+front*(depth*.06f);
                var load=FitModel(g,index==2?"wooden_crate_02":"old_military_crate",new Bounds(loadCenter,loadSize),yaw,"LoadedCrate",front);
                AddSupport(g,load);
                FillAbove(g,load,Mathf.Min(height-.04f,load.max.y+.40f),index,1);
            }
            else
            {
                bool single=index==6;
                var lowerSize = sideways ? new Vector3(depth*.96f,height*(single?.94f:.56f),width*.94f) : new Vector3(width*.94f,height*(single?.94f:.56f),depth*.96f);
                var lowerCenter = new Vector3(spec.position.x,lowerSize.y*.5f+.06f,spec.position.z);
                var lower=FitModel(g,index==6?"old_military_crate":"wooden_crate_02",new Bounds(lowerCenter,lowerSize),yaw,"WeatheredCargo",front);
                AddSupport(g,lower);
                Bounds top=lower;
                if(!single)
                {
                    float remaining=height-.035f-lower.max.y-.004f;
                    if(remaining<=.05f)throw new InvalidOperationException("No room for supported upper case "+spec.name);
                    // Upper props rest on the lower prop's actual top and fit inside its support footprint.
                    var upperSize=new Vector3(lower.size.x*.94f,remaining,lower.size.z*.94f);
                    var upperCenter=new Vector3(lower.center.x,lower.max.y+.004f+remaining*.5f,lower.center.z);
                    top=FitModel(g,"old_military_crate",new Bounds(upperCenter,upperSize),yaw,"StackedMilitaryCase",front);
                    if(Mathf.Abs(top.min.y-lower.max.y-.004f)>.002f)throw new InvalidOperationException("Upper case is not supported "+spec.name);
                }
                FillAbove(g,top,height-.025f,index,2);
            }
            Label(g,"SEALED  /  "+(index+1).ToString("00"),spec.position+back*(depth*.5f+.001f)+Vector3.up*(height*.2f),back,.50f,.10f);
        }
        if(modelCount!=17)throw new InvalidOperationException("Unexpected R2 model count: "+modelCount);
    }

    static Bounds FitModel(Transform parent,string id,Bounds allowed,float yaw,string name,Vector3 visibleFront)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChapterOneArt/Models/"+id+"/Model.fbx");
        var wrapper=Group(name+"_"+id,parent);wrapper.rotation=Quaternion.Euler(0,yaw,0);
        var model=UnityEngine.Object.Instantiate(prefab,wrapper,false);model.name="CC0_"+id;model.SetActive(true);
        if(id=="old_military_crate" && model.GetComponentsInChildren<Transform>(true).Any(t=>t.name.EndsWith("_b",StringComparison.Ordinal)))
            throw new InvalidOperationException("Military crate must be exported as the closed _a variant, without the separate open _b variant");
        foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var n in model.GetComponentsInChildren<NavMeshObstacle>(true))UnityEngine.Object.DestroyImmediate(n);
        foreach(var l in model.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(l);
        foreach(var c in model.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(c);
        var renderers=model.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if(renderers.Length==0)throw new InvalidOperationException("No renderers in "+id);
        Bounds BoundsOf(){var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}
        var source=BoundsOf();
        float factor=Mathf.Min(allowed.size.x/Mathf.Max(.001f,source.size.x),Mathf.Min(allowed.size.y/Mathf.Max(.001f,source.size.y),allowed.size.z/Mathf.Max(.001f,source.size.z)));
        wrapper.localScale=Vector3.one*factor;
        var scaled=BoundsOf();
        var position=allowed.center;
        // Align to the player's visible face and the allowed floor; never stretch imported models.
        position.y=allowed.min.y+scaled.extents.y;
        if(Mathf.Abs(visibleFront.x)>.5f)position.x=visibleFront.x<0?allowed.min.x+scaled.extents.x:allowed.max.x-scaled.extents.x;
        else position.z=visibleFront.z<0?allowed.min.z+scaled.extents.z:allowed.max.z-scaled.extents.z;
        wrapper.position+=position-scaled.center;
        var final=BoundsOf();
        if(!Contains(allowed,final,.002f))throw new InvalidOperationException("Uniform fit escaped allowed volume: "+id);
        foreach(var r in renderers)r.shadowCastingMode=ShadowCastingMode.On;
        modelCount++;
        return final;
    }

    static void AddSupport(Transform parent,Bounds cargo)
    {
        if(cargo.min.y<=.005f)return;
        Box(parent,"CargoSupportSkid",new Vector3(cargo.center.x,cargo.min.y*.5f,cargo.center.z),
            new Vector3(cargo.size.x*.99f,cargo.min.y,cargo.size.z*.99f),p.wood);
    }

    static void FillAbove(Transform parent,Bounds support,float ceiling,int variant,int maximumParcels)
    {
        float remaining=ceiling-support.max.y-.004f;if(remaining<.10f)return;
        int count=Mathf.Clamp(Mathf.CeilToInt(remaining/.5f),1,maximumParcels);
        float gap=.004f;float parcelHeight=(remaining-gap*(count-1))/count;
        Bounds previous=support;
        for(int i=0;i<count;i++)
        {
            var size=new Vector3(previous.size.x*.97f,parcelHeight,previous.size.z*.97f);
            var center=new Vector3(previous.center.x,previous.max.y+gap+size.y*.5f,previous.center.z);
            MeshObject(parent,"WrappedClothParcel",center,ParcelMesh(size,variant+i),p.fabric);
            // Bindings lie only over parcels above the crates; no broad plane crosses a real prop's face.
            for(int strip=0;strip<2;strip++)
            {
                float x=center.x+size.x*(strip==0?-.25f:.25f);
                Box(parent,"ParcelPackingStrap",new Vector3(x,center.y,center.z-size.z*.5f+.005f),new Vector3(.026f,size.y*.9f,.006f),p.rubber,false);
                Box(parent,"ParcelTopStrap",new Vector3(x,center.y+size.y*.5f-.002f,center.z),new Vector3(.026f,.004f,size.z*.90f),p.rubber,false);
            }
            previous=new Bounds(center,size);
        }
    }

    static Mesh ParcelMesh(Vector3 size,int variant)
    {
        string key="r2-parcel-v1:"+size.ToString("F5")+":"+variant;
        if(LoadMesh(key,out var cached))return cached;
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        Vector3 half=size*.5f;float round=Mathf.Min(.055f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.18f);Vector3 inner=half-Vector3.one*round;
        const int divisions=6;
        foreach(var face in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
        {
            Vector3 u=Mathf.Abs(face.x)>.5f?Vector3.forward:Vector3.right;
            Vector3 v=Mathf.Abs(face.y)>.5f?Vector3.forward:Vector3.up;
            int start=vertices.Count;
            for(int y=0;y<=divisions;y++)for(int x=0;x<=divisions;x++)
            {
                float fu=x/(float)divisions,fv=y/(float)divisions;
                Vector3 local=Vector3.Scale(face+u*(fu*2-1)+v*(fv*2-1),half);
                Vector3 core=new Vector3(Mathf.Clamp(local.x,-inner.x,inner.x),Mathf.Clamp(local.y,-inner.y,inner.y),Mathf.Clamp(local.z,-inner.z,inner.z));
                Vector3 rounded=core+(local-core).normalized*round;
                float wrinkle=(.5f+.5f*Mathf.Sin(fu*28+fv*5+variant))*Mathf.Sin(fu*Mathf.PI)*Mathf.Sin(fv*Mathf.PI)*.015f;
                rounded-=face*wrinkle;
                vertices.Add(rounded);normals.Add((local-core).normalized);uv.Add(Project(local,face));
            }
            bool reverse=Vector3.Dot(Vector3.Cross(u,v),face)<0;
            for(int y=0;y<divisions;y++)for(int x=0;x<divisions;x++)
            {
                int a=start+y*(divisions+1)+x,b=a+1,c=a+divisions+2,d=a+divisions+1;
                triangles.AddRange(reverse?new[]{a,c,b,a,d,c}:new[]{a,b,c,a,c,d});
            }
        }
        var mesh=SaveMesh(key,vertices,normals,uv,triangles);mesh.RecalculateNormals();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);return mesh;
    }

    static bool Contains(Bounds outer,Bounds inner,float epsilon)
    {
        var a=outer.min-Vector3.one*epsilon;var b=outer.max+Vector3.one*epsilon;
        return inner.min.x>=a.x&&inner.min.y>=a.y&&inner.min.z>=a.z&&inner.max.x<=b.x&&inner.max.y<=b.y&&inner.max.z<=b.z;
    }

    static void Pipe(Transform parent,string name,Vector3 a,Vector3 b,float radius)
    {
        var vector=b-a;var go=MeshObject(parent,name,(a+b)*.5f,CylinderMesh(radius,vector.magnitude),p.rustedMetal);
        go.transform.rotation=Quaternion.FromToRotation(Vector3.up,vector.normalized);
    }
    static void Flange(Transform parent,Vector3 center,Vector3 normal,float radius)
    {
        var rotation=Quaternion.FromToRotation(Vector3.up,normal);
        var flange=MeshObject(parent,"CorrodedPipeFlange",center,CylinderMesh(radius,.032f),p.rustedMetal);flange.transform.rotation=rotation;
        for(int i=0;i<4;i++)
        {
            float angle=i*Mathf.PI*.5f;
            var offset=rotation*new Vector3(Mathf.Cos(angle)*radius*.78f,.021f,Mathf.Sin(angle)*radius*.78f);
            var bolt=MeshObject(parent,"FlangeBolt",center+offset,CylinderMesh(.009f,.012f),p.rustedMetal,false);bolt.transform.rotation=rotation;
        }
    }

    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool shadows=true) =>
        MeshObject(parent,name,position,BoxMesh(size),material,shadows);
    static GameObject MeshObject(Transform parent,string name,Vector3 position,Mesh mesh,Material material,bool shadows=true)
    {
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.position=position;
        g.GetComponent<MeshFilter>().sharedMesh=mesh;var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;return g;
    }

    static Mesh BoxMesh(Vector3 size)
    {
        string key="r2-box-v1:"+size.ToString("F5");if(LoadMesh(key,out var cached))return cached;
        Vector3 h=size*.5f;var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        void Face(Vector3 normal,params Vector3[] points)
        {
            if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)Array.Reverse(points);
            int start=v.Count;foreach(var point in points){v.Add(point);n.Add(normal);uv.Add(Project(point,normal));}
            tris.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        foreach(float s in new[]{-1f,1f})
        {
            Face(Vector3.right*s,new Vector3(h.x*s,-h.y,-h.z),new Vector3(h.x*s,h.y,-h.z),new Vector3(h.x*s,h.y,h.z),new Vector3(h.x*s,-h.y,h.z));
            Face(Vector3.up*s,new Vector3(-h.x,h.y*s,-h.z),new Vector3(h.x,h.y*s,-h.z),new Vector3(h.x,h.y*s,h.z),new Vector3(-h.x,h.y*s,h.z));
            Face(Vector3.forward*s,new Vector3(-h.x,-h.y,h.z*s),new Vector3(h.x,-h.y,h.z*s),new Vector3(h.x,h.y,h.z*s),new Vector3(-h.x,h.y,h.z*s));
        }
        return SaveMesh(key,v,n,uv,tris);
    }

    static Mesh CylinderMesh(float radius,float length)
    {
        string key="r2-pipe-v1:"+radius.ToString("F5")+":"+length.ToString("F5");if(LoadMesh(key,out var cached))return cached;
        const int segments=16;var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int i=0;i<=segments;i++)
        {
            float angle=i*Mathf.PI*2/segments;Vector3 normal=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            v.Add(normal*radius+Vector3.down*length*.5f);v.Add(normal*radius+Vector3.up*length*.5f);n.Add(normal);n.Add(normal);
            uv.Add(new Vector2(i/(float)segments*Mathf.PI*2*radius,0));uv.Add(new Vector2(i/(float)segments*Mathf.PI*2*radius,length));
            if(i<segments){int a=i*2;tris.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3});}
        }
        foreach(float sign in new[]{-1f,1f})
        {
            int center=v.Count;v.Add(Vector3.up*sign*length*.5f);n.Add(Vector3.up*sign);uv.Add(new Vector2(.5f,.5f));
            for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2/segments;var point=new Vector3(Mathf.Cos(a)*radius,sign*length*.5f,Mathf.Sin(a)*radius);
                v.Add(point);n.Add(Vector3.up*sign);uv.Add(new Vector2(point.x,point.z));
                if(i<segments)tris.AddRange(sign>0?new[]{center,center+i+2,center+i+1}:new[]{center,center+i+1,center+i+2});
            }
        }
        return SaveMesh(key,v,n,uv,tris);
    }

    static bool LoadMesh(string key,out Mesh mesh)
    {
        if(Meshes.TryGetValue(key,out mesh))return true;
        mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot+"/"+Hash(key)+".asset");if(mesh)Meshes[key]=mesh;return mesh;
    }
    static Mesh SaveMesh(string key,List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> indices)
    {
        var mesh=new Mesh{name="R2_"+Hash(key)};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh,MeshRoot+"/"+Hash(key)+".asset");Meshes[key]=mesh;return mesh;
    }
    static Vector2 Project(Vector3 v,Vector3 normal)
    {
        if(Mathf.Abs(normal.y)>.5f)return new Vector2(v.x,v.z);
        return Mathf.Abs(normal.x)>.5f?new Vector2(v.z,v.y):new Vector2(v.x,v.y);
    }
    static string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").Substring(0,18).ToLowerInvariant();}

    static void Label(Transform parent,string content,Vector3 position,Vector3 viewer,float width,float height)
    {
        if(!font)return;var g=new GameObject("StencilledLabel",typeof(TextMeshPro));g.transform.SetParent(parent,false);g.transform.SetPositionAndRotation(position,Quaternion.LookRotation(-viewer,Vector3.up));
        var text=g.GetComponent<TextMeshPro>();text.font=font;text.text=content;text.rectTransform.sizeDelta=new Vector2(width,height);text.fontSize=height*30;
        text.enableAutoSizing=true;text.fontSizeMin=.8f;text.fontSizeMax=height*30;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.69f,.65f,.48f);text.textWrappingMode=TextWrappingModes.NoWrap;
        text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }
}
