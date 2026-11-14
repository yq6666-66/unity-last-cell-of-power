using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repairs generated warehouse wall skins without moving scene objects or changing physical geometry.
/// Every output mesh is a separate asset; the accepted source meshes are never edited in place.
/// </summary>
public static class WallSurfaceRepair
{
    const float Epsilon = .00015f;
    const string Version = "wall-surface-v2";
    static readonly string[] BandSuffixes = { "_DampSkirting", "_OldLowerPaint", "_PaintEdge", "_DampUpperConcrete" };

    [Serializable]
    public sealed class RepairItem
    {
        public string objectPath, meshAsset;
        public int sourceTriangles, outputTriangles, sourceFaces, outputFaceFragments, clippedFaces, removedFaces;
        public bool changedMeshReference;
    }
    [Serializable]
    public sealed class RepairReport
    {
        public string scenePath, meshRoot;
        public int wallBands, portalFrames, exitFrames, shutterFrames, railObjects;
        public int changedMeshReferences, createdMeshes, reusedMeshes;
        public int clippedWallFaces, removedWallFaces, omittedBandInterfaceFaces, removedRailEndCaps;
        public int outputWallFaceFragments;
        public int trimmedPortalFrames, portalFacesClippedByShutter, portalFaceFragments;
        public List<RepairItem> objects = new List<RepairItem>();
    }
    public static RepairReport LastReport { get; private set; }

    struct Rect2
    {
        public float minU, maxU, minV, maxV;
        public Rect2(float a, float b, float c, float d) { minU = a; maxU = b; minV = c; maxV = d; }
    }
    struct Face
    {
        public int axis, u, v;
        public float plane, sign;
        public Rect2 rect;
        public Vector3 Normal { get { var n = Vector3.zero; n[axis] = sign; return n; } }
        public Vector3 Point(float a, float b)
        { var p = Vector3.zero; p[axis] = plane; p[u] = a; p[v] = b; return p; }
    }
    sealed class MeshData
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();
    }

    public static void Apply(Scene scene, string meshRoot)
    {
        if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("Wall repair requires a loaded scene");
        meshRoot = (meshRoot ?? "").Replace('\\', '/').TrimEnd('/');
        if (!meshRoot.StartsWith("Assets/", StringComparison.Ordinal) || meshRoot.Contains("/../") || meshRoot.EndsWith("/..", StringComparison.Ordinal))
            throw new ArgumentException("Wall repair meshes must be saved below Assets");
        Directory.CreateDirectory(meshRoot);
        AssetDatabase.Refresh();

        var report = new RepairReport { scenePath = scene.path, meshRoot = meshRoot };
        var roots = scene.GetRootGameObjects();
        var renderers = roots.SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh)
            .OrderBy(r => ObjectPath(r.transform), StringComparer.Ordinal).ToArray();
        if (!renderers.Any(r => BandSuffixes.Any(suffix => r.name.EndsWith(suffix, StringComparison.Ordinal))))
            throw new InvalidOperationException("No generated realism wall skins found in the requested scene");
        var physicalRoot = roots.SingleOrDefault(g => g.name == "ExpandedCollisionWorld");
        if (!physicalRoot) throw new InvalidOperationException("Accepted collision root missing; wall dimensions cannot be established");
        var physicalColliders = physicalRoot.GetComponentsInChildren<BoxCollider>(true);
        var colliders = physicalColliders.GroupBy(c => c.name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var portal = renderers.Where(r => r.name.StartsWith("PortalRustJamb_", StringComparison.Ordinal)).ToArray();
        var exit = renderers.Where(r => r.name == "ExitJamb").ToArray();
        var shutters = renderers.Where(r => r.name == "ShutterRail").ToArray();
        report.portalFrames = portal.Length; report.exitFrames = exit.Length; report.shutterFrames = shutters.Length;
        var frames = portal.Concat(exit).Concat(shutters).ToArray();
        foreach (var frame in frames) RequireAxisAligned(frame.transform);
        var frameBounds = frames.Select(CanonicalFrameBounds).ToArray();

        foreach (var renderer in renderers)
        {
            int band = Array.FindIndex(BandSuffixes, suffix => renderer.name.EndsWith(suffix, StringComparison.Ordinal));
            if (band < 0) continue;
            RequireAxisAligned(renderer.transform);
            string colliderName = renderer.name.Substring(0, renderer.name.Length - BandSuffixes[band].Length);
            if (!colliders.TryGetValue(colliderName, out var candidates) || candidates.Length != 1)
                throw new InvalidOperationException("No original wall collider for generated skin " + renderer.name);
            var collider = candidates[0];
            RequireAxisAligned(collider.transform);
            Bounds fullWall = WorldBox(collider);
            if (fullWall.size.y < 1.04f) throw new InvalidOperationException("Wall is too short for the established paint bands");
            float bottom = fullWall.min.y;
            // One closed external skin partitioned into adjacent material bands. Their inner horizontal
            // interfaces have no render faces, so there is no paint/skirting overlap at those heights.
            float[] edges = { bottom, bottom + .13f, bottom + 1.014f, bottom + 1.026f, fullWall.max.y };
            Bounds bandBounds = fullWall;
            var bandMin = fullWall.min; var bandMax = fullWall.max;
            bandMin.y = edges[band]; bandMax.y = edges[band + 1]; bandBounds.SetMinMax(bandMin, bandMax);
            var data = new MeshData();
            var item = NewItem(renderer);
            foreach (var face in BoxFaces(bandBounds))
            {
                item.sourceFaces++;
                if (face.axis == 1 && (face.sign < 0 ? band > 0 : band < BandSuffixes.Length - 1))
                { report.omittedBandInterfaceFaces++; continue; }
                var fragments = new List<Rect2> { face.rect };
                bool clipped = false;
                foreach (var block in frameBounds)
                {
                    if (face.plane < block.min[face.axis] - Epsilon || face.plane > block.max[face.axis] + Epsilon) continue;
                    var cut = new Rect2(block.min[face.u], block.max[face.u], block.min[face.v], block.max[face.v]);
                    var next = new List<Rect2>();
                    foreach (var fragment in fragments) clipped |= Subtract(fragment, cut, next);
                    fragments = next;
                    if (fragments.Count == 0) break;
                }
                if (clipped) { item.clippedFaces++; report.clippedWallFaces++; }
                if (fragments.Count == 0) { item.removedFaces++; report.removedWallFaces++; }
                foreach (var fragment in fragments) AddFace(data, renderer.transform, face, fragment);
                item.outputFaceFragments += fragments.Count;
            }
            report.wallBands++; report.outputWallFaceFragments += item.outputFaceFragments;
            AssignMesh(renderer, data, item, report, meshRoot, "Band" + band);
        }

        // The tactics shutter adds two larger rails over the east portal's earlier decorative jambs.
        // Keep the new rails intact and retain only the earlier jambs' exposed strips (12 mm per side).
        // This is a mesh difference, not a door-width or object-position adjustment.
        var shutterBounds = shutters.Select(CanonicalFrameBounds).ToArray();
        foreach (var renderer in portal)
        {
            Bounds original = CanonicalFrameBounds(renderer);
            var covers = shutterBounds.Where(b => b.Intersects(original)).ToArray();
            if (covers.Length == 0) continue;
            var data = new MeshData(); var item = NewItem(renderer);
            foreach (var face in BoxFaces(original))
            {
                item.sourceFaces++;
                var fragments = new List<Rect2> { face.rect }; bool clipped = false;
                foreach (var cover in covers)
                {
                    if (face.plane < cover.min[face.axis] - Epsilon || face.plane > cover.max[face.axis] + Epsilon) continue;
                    var cut = new Rect2(cover.min[face.u], cover.max[face.u], cover.min[face.v], cover.max[face.v]);
                    var next = new List<Rect2>();
                    foreach (var fragment in fragments) clipped |= Subtract(fragment, cut, next);
                    fragments = next;
                    if (fragments.Count == 0) break;
                }
                if (clipped) { item.clippedFaces++; report.portalFacesClippedByShutter++; }
                if (fragments.Count == 0) item.removedFaces++;
                foreach (var fragment in fragments) AddFace(data, renderer.transform, face, fragment);
                item.outputFaceFragments += fragments.Count;
            }
            if (item.clippedFaces == 0) continue;
            report.trimmedPortalFrames++; report.portalFaceFragments += item.outputFaceFragments;
            AssignMesh(renderer, data, item, report, meshRoot, "PortalExposedStrips");
        }

        foreach (var renderer in renderers.Where(r => r.name == "ExitGuardSideVisual"))
        {
            RequireAxisAligned(renderer.transform);
            // Recover dimensions from the unchanged matching physical rail, rather than feeding the
            // newly calculated renderer AABB back through float transforms on a second Apply call.
            var railCandidates = physicalColliders.Where(c => c.name == "ExitGuardSide" &&
                Vector3.Distance(c.transform.TransformPoint(c.center), renderer.transform.position) < .01f).ToArray();
            if (railCandidates.Length != 1) throw new InvalidOperationException("Matching original rail collider missing: " + ObjectPath(renderer.transform));
            Bounds bounds = WorldBox(railCandidates[0]);
            var data = new MeshData(); var item = NewItem(renderer);
            foreach (var face in BoxFaces(bounds))
            {
                item.sourceFaces++;
                // This is the rail's -Z end cap embedded in the north wall. Retain its other five faces,
                // including the outside end, side faces and top; do not alter the wall or rail transform.
                bool buriedFront = face.axis == 2 && face.sign < 0 && Mathf.Abs(face.plane - 12f) <= .002f;
                if (buriedFront)
                { item.clippedFaces++; item.removedFaces++; report.removedRailEndCaps++; continue; }
                AddFace(data, renderer.transform, face, face.rect); item.outputFaceFragments++;
            }
            if (item.removedFaces != 1) throw new InvalidOperationException("Expected one embedded z=12 rail cap: " + ObjectPath(renderer.transform));
            report.railObjects++;
            AssignMesh(renderer, data, item, report, meshRoot, "RailNoBuriedCap");
        }
        LastReport = report;
        if (report.changedMeshReferences > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("WALL_SURFACE_REPAIR " + JsonUtility.ToJson(report));
    }

    static RepairItem NewItem(MeshRenderer renderer)
    {
        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        return new RepairItem { objectPath = ObjectPath(renderer.transform), sourceTriangles = (int)(mesh.GetIndexCount(0) / 3) };
    }

    static void AssignMesh(MeshRenderer renderer, MeshData data, RepairItem item, RepairReport report, string meshRoot, string purpose)
    {
        string fingerprint = Fingerprint(renderer.transform, data, purpose);
        string path = meshRoot + "/WallFix_" + fingerprint + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh)
        {
            if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite a non-mesh asset: " + path);
            mesh = new Mesh { name = "WallFix_" + purpose + "_" + fingerprint };
            if (data.vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(data.vertices); mesh.SetNormals(data.normals); mesh.SetUVs(0, data.uv); mesh.SetTriangles(data.triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path); report.createdMeshes++;
        }
        else report.reusedMeshes++;
        var filter = renderer.GetComponent<MeshFilter>();
        item.changedMeshReference = filter.sharedMesh != mesh;
        if (item.changedMeshReference)
        { filter.sharedMesh = mesh; EditorUtility.SetDirty(filter); report.changedMeshReferences++; }
        item.meshAsset = path; item.outputTriangles = data.triangles.Count / 3; report.objects.Add(item);
    }

    static IEnumerable<Face> BoxFaces(Bounds bounds)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            int u = axis == 0 ? 2 : 0;
            int v = axis == 1 ? 2 : 1;
            foreach (float sign in new[] { -1f, 1f })
                yield return new Face { axis = axis, u = u, v = v, sign = sign,
                    plane = sign < 0 ? bounds.min[axis] : bounds.max[axis],
                    rect = new Rect2(bounds.min[u], bounds.max[u], bounds.min[v], bounds.max[v]) };
        }
    }

    static bool Subtract(Rect2 source, Rect2 cut, List<Rect2> output)
    {
        float a = Mathf.Max(source.minU, cut.minU), b = Mathf.Min(source.maxU, cut.maxU);
        float c = Mathf.Max(source.minV, cut.minV), d = Mathf.Min(source.maxV, cut.maxV);
        if (b - a <= Epsilon || d - c <= Epsilon) { output.Add(source); return false; }
        AddRect(output, source.minU, a, source.minV, source.maxV);
        AddRect(output, b, source.maxU, source.minV, source.maxV);
        AddRect(output, a, b, source.minV, c);
        AddRect(output, a, b, d, source.maxV);
        return true;
    }
    static void AddRect(List<Rect2> list, float a, float b, float c, float d)
    { if (b - a > Epsilon && d - c > Epsilon) list.Add(new Rect2(a, b, c, d)); }

    static void AddFace(MeshData data, Transform owner, Face face, Rect2 rect)
    {
        Vector3[] world = { face.Point(rect.minU, rect.minV), face.Point(rect.maxU, rect.minV),
            face.Point(rect.maxU, rect.maxV), face.Point(rect.minU, rect.maxV) };
        Vector3 normal = face.Normal;
        if (Vector3.Dot(Vector3.Cross(world[1] - world[0], world[2] - world[0]), normal) < 0) Array.Reverse(world);
        Vector3 localNormal = owner.localToWorldMatrix.transpose.MultiplyVector(normal).normalized;
        int first = data.vertices.Count;
        foreach (var point in world)
        {
            data.vertices.Add(owner.InverseTransformPoint(point)); data.normals.Add(localNormal);
            // Existing R2 box skins and R1 retiled rails use metre-scaled UV coordinates relative to
            // the unchanged object's origin. Preserve that phase while clipping/resizing faces.
            Vector3 relative = point - owner.position;
            data.uv.Add(new Vector2(relative[face.u], relative[face.v]));
        }
        data.triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
    }

    static Bounds WorldBox(BoxCollider collider)
    {
        Vector3 h = collider.size * .5f;
        var bounds = new Bounds(collider.transform.TransformPoint(collider.center - h), Vector3.zero);
        foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
            bounds.Encapsulate(collider.transform.TransformPoint(collider.center + Vector3.Scale(h, new Vector3(x, y, z))));
        return bounds;
    }
    static Bounds CanonicalFrameBounds(MeshRenderer renderer)
    {
        var measured = renderer.bounds;
        if (Vector3.Distance(measured.center, renderer.transform.position) > .0001f)
            throw new InvalidOperationException("Generated frame mesh is not centered on its original transform: " + ObjectPath(renderer.transform));
        // Generated jamb/rail dimensions are exact centimetre/millimetre values. Normalize only the
        // measurement to 10 micrometres, so inverse-transform roundoff after clipping cannot change
        // a later fingerprint. The original Transform remains the source of the world-space center.
        Vector3 size = measured.size;
        for (int axis = 0; axis < 3; axis++) size[axis] = (float)Math.Round(size[axis], 5, MidpointRounding.AwayFromZero);
        return new Bounds(renderer.transform.position, size);
    }
    static void RequireAxisAligned(Transform transform)
    {
        foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
        {
            Vector3 v = transform.TransformVector(axis).normalized;
            if (Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)) < .99999f)
                throw new InvalidOperationException("Wall repair only supports axis-aligned generated geometry: " + ObjectPath(transform));
        }
    }
    static string ObjectPath(Transform transform)
    {
        var parts = new List<string>();
        for (var t = transform; t; t = t.parent) parts.Add(t.name + "[" + t.GetSiblingIndex() + "]");
        parts.Reverse(); return string.Join("/", parts);
    }
    static string Fingerprint(Transform owner, MeshData data, string purpose)
    {
        var value = new StringBuilder(Version).Append('|').Append(purpose).Append('|').Append(ObjectPath(owner));
        foreach (var vertex in data.vertices) Append(value, vertex);
        foreach (var normal in data.normals) Append(value, normal);
        foreach (var uv in data.uv) value.Append('|').Append(uv.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(uv.y.ToString("R", CultureInfo.InvariantCulture));
        foreach (var index in data.triangles) value.Append('|').Append(index);
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToString()))).Replace("-", "").Substring(0, 24).ToLowerInvariant();
    }
    static void Append(StringBuilder text, Vector3 value)
    {
        text.Append('|').Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
            .Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(value.z.ToString("R", CultureInfo.InvariantCulture));
    }
}
