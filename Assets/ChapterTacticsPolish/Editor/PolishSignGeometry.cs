using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Scope: generated readability signs, their real face spacing, and nearby mesh faces.
// Does not bake navigation, move an existing object, or claim temporal/player acceptance.
public static class PolishSignGeometry
{
    const float PlaneTolerance=.0002f,AreaTolerance=.000001f;
    struct Triangle { public Vector3 a,b,c,n; }
    sealed class Shape { public Renderer renderer; public Triangle[] triangles; }
    public static string Check(Scene scene,Transform additions)
    {
        if(!additions)throw new InvalidOperationException("Polish readability root missing");
        if(additions.GetComponentsInChildren<Collider>(true).Length!=0 || additions.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true).Length!=0 || additions.GetComponentsInChildren<Light>(true).Length!=0)
            throw new InvalidOperationException("Readability signs must not add collisions, navigation obstacles or lights");
        int signCount=0;
        foreach(Transform sign in additions)
        {
            var plate=sign.Find("Plate");if(!plate)continue;signCount++;
            float halfDepth=plate.localScale.z*.5f;
            foreach(string labelName in new[]{"Title","Destination","State"})
            {
                var label=sign.Find(labelName);if(!label)throw new InvalidOperationException("Missing sign label "+sign.name+"/"+labelName);
                float gap=label.localPosition.z-plate.localPosition.z-halfDepth;
                if(gap<.012f)throw new InvalidOperationException("Sign text too close to backing: "+sign.name+"/"+labelName+" gap="+gap);
            }
        }
        if(signCount!=6)throw new InvalidOperationException("Expected six matched control/speaker/gate signs");
        var added=additions.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        var all=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        var cache=new Dictionary<MeshRenderer,Shape>();var pairs=new HashSet<string>();int nearby=0,overlap=0;
        foreach(var a in added)foreach(var b in all)
        {
            if(a==b)continue;
            var bounds=a.bounds;bounds.Expand(PlaneTolerance*2);if(!bounds.Intersects(b.bounds))continue;
            int low=Mathf.Min(a.GetInstanceID(),b.GetInstanceID()),high=Mathf.Max(a.GetInstanceID(),b.GetInstanceID());
            if(!pairs.Add(low+":"+high))continue;nearby++;
            if(!cache.TryGetValue(a,out var sa)){sa=Read(a);cache.Add(a,sa);}
            if(!cache.TryGetValue(b,out var sb)){sb=Read(b);cache.Add(b,sb);}
            if(sa.triangles.Any(ta=>sb.triangles.Any(tb=>CoplanarOverlap(ta,tb))))
            {overlap++;Debug.LogError("POLISH_COPLANAR_PAIR "+PathOf(a.transform)+" <> "+PathOf(b.transform));}
        }
        if(overlap>0)throw new InvalidOperationException("New sign surfaces have coplanar overlap pairs="+overlap);
        return "PASS six signs: text/backing separation >=12 mm, no added collider/nav obstacle/Light, overlapping coplanar face pairs=0; generated renderers="+added.Length+"; nearby renderer pairs checked="+nearby+". Geometry only; visual readability and temporal stability remain separate.";
    }
    static Shape Read(MeshRenderer renderer)
    {
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
        var matrix=renderer.transform.localToWorldMatrix;var triangles=new List<Triangle>();
        for(int i=0;i+2<indices.Length;i+=3)
        {
            var a=matrix.MultiplyPoint3x4(vertices[indices[i]]);var b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]);var c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]);
            var cross=Vector3.Cross(b-a,c-a);if(cross.sqrMagnitude<1e-14f)continue;
            triangles.Add(new Triangle{a=a,b=b,c=c,n=cross.normalized});
        }
        return new Shape{renderer=renderer,triangles=triangles.ToArray()};
    }
    static bool CoplanarOverlap(Triangle a,Triangle b)
    {
        if(Mathf.Abs(Vector3.Dot(a.n,b.n))<.99999f || Mathf.Abs(Vector3.Dot(a.n,b.a-a.a))>PlaneTolerance || Mathf.Abs(Vector3.Dot(a.n,b.b-a.a))>PlaneTolerance || Mathf.Abs(Vector3.Dot(a.n,b.c-a.a))>PlaneTolerance)return false;
        Vector3 axis=new Vector3(Mathf.Abs(a.n.x),Mathf.Abs(a.n.y),Mathf.Abs(a.n.z));int drop=axis.x>axis.y && axis.x>axis.z?0:axis.y>axis.z?1:2;
        var polygon=new List<Vector2>{Project(a.a,drop),Project(a.b,drop),Project(a.c,drop)};
        var clip=new[]{Project(b.a,drop),Project(b.b,drop),Project(b.c,drop)};
        float sign=Cross(clip[1]-clip[0],clip[2]-clip[0])>=0?1:-1;
        for(int i=0;i<3 && polygon.Count>0;i++)
        {
            var start=clip[i];var end=clip[(i+1)%3];var next=new List<Vector2>();
            Vector2 previous=polygon[polygon.Count-1];float dp=Cross(end-start,previous-start)*sign;
            foreach(var current in polygon)
            {
                float dc=Cross(end-start,current-start)*sign;bool pin=dp>=0,cin=dc>=0;
                if(pin!=cin){float t=dp/(dp-dc);next.Add(Vector2.LerpUnclamped(previous,current,t));}
                if(cin)next.Add(current);previous=current;dp=dc;
            }
            polygon=next;
        }
        if(polygon.Count<3)return false;float area=0;
        for(int i=0;i<polygon.Count;i++)area+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);
        return Mathf.Abs(area)*.5f>AreaTolerance;
    }
    static Vector2 Project(Vector3 p,int drop)=>drop==0?new Vector2(p.y,p.z):drop==1?new Vector2(p.x,p.z):new Vector2(p.x,p.y);
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
}
