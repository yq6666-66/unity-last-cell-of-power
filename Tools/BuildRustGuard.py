"""Generate the original RustGuard visual only. Blender 5.x, no gameplay assets modified.
Run: blender --background --factory-startup --python Tools/BuildRustGuard.py
"""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/ChapterOneArt/Models/RustGuard'
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'Source~').mkdir(exist_ok=True)
for name in ('RustGuard.fbx','Source~/RustGuard.blend','RustGuard-source.json'):
    if (OUT/name).exists():
        raise RuntimeError('Output already exists; preserve this revision before rebuilding: '+name)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
MATS={}
COLORS={'Paint':(.27,.30,.26,1),'Rust':(.24,.12,.065,1),'Steel':(.34,.37,.38,1),'Rubber':(.038,.043,.041,1),'Glass':(.017,.036,.041,1),'Status':(.25,.55,.65,1),'Warning':(.53,.39,.13,1),'Wire':(.18,.105,.045,1)}
for key,color in COLORS.items():
    m=bpy.data.materials.new('RG_'+key);m.diffuse_color=color;m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=color
    p.inputs['Metallic'].default_value=.75 if key=='Steel' else .35 if key in ('Paint','Rust') else 0
    p.inputs['Roughness'].default_value=.3 if key in ('Steel','Glass') else .72
    if key=='Status':p.inputs['Emission Color'].default_value=color;p.inputs['Emission Strength'].default_value=2
    MATS[key]=m
parts=[]
def finish(obj,name,material,bevel=0):
    obj.name=name;obj.data.materials.append(MATS[material]);parts.append(obj)
    if bevel:
        mod=obj.modifiers.new('Physical edge radius','BEVEL');mod.width=bevel;mod.segments=3
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=mod.name)
        for face in obj.data.polygons:face.use_smooth=True
        weighted=obj.modifiers.new('Weighted machined normals','WEIGHTED_NORMAL');weighted.keep_sharp=True;weighted.weight=40
        bpy.ops.object.modifier_apply(modifier=weighted.name)
    return obj
def box(name,loc,size,mat='Paint',bevel=.008,rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rotation:o.rotation_euler=rotation
    return finish(o,name,mat,bevel)
def cyl(name,loc,radius,depth,mat='Steel',axis='Z',vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
    o=bpy.context.object
    if axis=='X':o.rotation_euler[1]=math.pi/2
    elif axis=='Y':o.rotation_euler[0]=math.pi/2
    return finish(o,name,mat,min(.004,depth*.15))
def panel(name,points,y,thickness,mat='Paint'):
    n=len(points);vertices=[(x,y+offset,z) for offset in (-thickness/2,thickness/2) for x,z in points]
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    return finish(o,name,mat,.004)
def dented_cover(name,center,size,mat='Paint'):
    n=6;vertices=[];cx,cy,cz=center;sx,sy,thickness=size
    for layer in (0,1):
        for j in range(n+1):
            for i in range(n+1):
                u=i/n;v=j/n
                dent=-.016*math.exp(-((u-.62)**2+(v-.32)**2)/.035)*math.sin(math.pi*u)*math.sin(math.pi*v)
                vertices.append((cx+(u-.5)*sx,cy+(v-.5)*sy,cz+dent+(layer-.5)*thickness))
    count=(n+1)**2;faces=[]
    for j in range(n):
        for i in range(n):
            a=j*(n+1)+i;b=a+1;c=b+n+1;d=a+n+1
            faces.extend([(a,d,c,b),(a+count,b+count,c+count,d+count)])
    edge=list(range(n+1))+[j*(n+1)+n for j in range(1,n+1)]+[n*(n+1)+i for i in range(n-1,-1,-1)]+[j*(n+1) for j in range(n-1,0,-1)]
    for i,a in enumerate(edge):
        b=edge[(i+1)%len(edge)];faces.append((a,b,b+count,a+count))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return finish(o,name,mat,.002)
def cable(name,points,radius=.008,mat='Rubber'):
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.resolution_u=12;data.bevel_depth=radius;data.bevel_resolution=2
    spline=data.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
    for point,co in zip(spline.bezier_points,points):point.co=co;point.handle_left_type='AUTO';point.handle_right_type='AUTO'
    o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.select_set(False);return finish(o,name,mat)
def label(text,loc,size=.04,mat='Warning'):
    data=bpy.data.curves.new('Stamped '+text,'FONT');data.body=text;data.size=size;data.extrude=.0003;data.align_x='CENTER'
    o=bpy.data.objects.new('Stamped '+text,data);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(math.pi/2,0,0)
    bpy.context.view_layer.objects.active=o;bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object,'Stamp_'+text,mat)

# A low industrial chassis, within the original 1.076 m x 1.077 m envelope.
box('Chassis structural casting',(0,0,.295),(.78,.91,.19),'Steel',.012)
box('Inset dark mechanism',(0,0,.565),(.67,.64,.42),'Rubber',.028)
box('Rear service panel',(0,.355,.565),(.77,.045,.43),'Paint',.012)
box('Left armor plate',(-.38,0,.56),(.035,.72,.43),'Paint',.009)
box('Right lower armor plate',(.38,.035,.505),(.035,.64,.31),'Paint',.009)
# The missing front-right corner is actual absent shell geometry, with an inset core.
panel('Torn front armor',[(-.38,.355),(.38,.355),(.38,.56),(.30,.585),(.265,.655),(.175,.694),(.135,.765),(-.38,.765)],-.355,.027)
lip=panel('Bent exposed lip',[(.14,.77),(.26,.75),(.30,.705),(.22,.69),(.17,.72)],-.355,.015,'Rust')
for vertex in lip.data.vertices:
    vertex.co.y-=max(0,vertex.co.x-.14)*.38
box('Upper battered cover',(-.025,.025,.789),(.71,.68,.035),'Paint',.014,(-.025,.018,0))
box('Top sunk maintenance seam',(-.06,.03,.810),(.39,.39,.009),'Rubber',.004)
box('Top removable access hatch',(-.06,.03,.817),(.367,.367,.012),'Paint',.012)
box('Underbelly band',(0,0,.36),(.81,.79,.035),'Rust',.009)
box('Front impact bumper',(0,-.488,.29),(.81,.065,.09),'Rubber',.016)
box('Front bumper steel wear face',(0,-.524,.292),(.63,.008,.025),'Steel',.003)
box('Rear impact bumper',(0,.484,.29),(.77,.05,.075),'Rubber',.012)
for x in (-.30,.30):
    for z in (.40,.70):cyl('Front countersunk bolt',(x,-.375,z),.015,.010,'Steel','Y',6)
    for z in (.40,.72):cyl('Rear rusted bolt',(x,.381,z),.014,.010,'Rust','Y',6)
for x in (-.22,.10):
    for y in (-.12,.18):cyl('Hatch recessed fastener',(x,y,.829),.011,.007,'Steel','Z',6)
# Cooling bank: deep black recess with individually shaped metal slats.
box('Rear cooling recess',(0,.383,.58),(.45,.014,.22),'Rubber',.008)
for i in range(9):box('Rear cooling louvre',(0,.398,.49+i*.023),(.415,.022,.010),'Steel',.002,(-.22,0,0))
for side in (-1,1):
    for j in range(4):box('Side cooling slit',(side*.401,.18,.51+j*.032),(.007,.22,.013),'Rubber',.002)
# A punctured panel exposes connector blocks and two slack cables, no gore/creature elements.
box('Exposed connector plate',(.255,-.322,.696),(.20,.025,.15),'Steel',.006)
for x in (.20,.25,.30):cyl('Connector socket',(x,-.346,.70),.019,.02,'Rubber','Y')
cable('Damaged insulated cable',[(.20,-.365,.71),(.14,-.39,.64),(.19,-.398,.57),(.27,-.376,.59)],.009,'Wire')
cable('Exposed braided cable',[(.30,-.358,.72),(.33,-.40,.67),(.29,-.405,.61),(.27,-.375,.59)],.006,'Rubber')
for z in (.61,.625,.64):box('Cable retaining clamp',(.16,-.407,z),(.025,.012,.005),'Steel',.001)

# Two rubber belts with separate metal grousers and inset wheel/hub construction.
def belt_points(radius=.17):
    points=[]
    for i in range(12):points.append((-.27+.54*i/12,.215+radius))
    for i in range(14):
        a=math.pi/2-math.pi*i/14;points.append((.27+radius*math.cos(a),.215+radius*math.sin(a)))
    for i in range(12):points.append((.27-.54*i/12,.215-radius))
    for i in range(14):
        a=-math.pi/2-math.pi*i/14;points.append((-.27+radius*math.cos(a),.215+radius*math.sin(a)))
    return points
path=belt_points();inner=belt_points(.145)
for side in (-1,1):
    xa,xb=side*.325,side*.50;n=len(path)
    vertices=[(x,y,z) for x in (xa,xb) for contour in (path,inner) for y,z in contour]
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,2*n+j,2*n+i),(n+i,3*n+i,3*n+j,n+j),(i,n+i,n+j,j),(2*n+i,2*n+j,3*n+j,3*n+i)])
    mesh=bpy.data.meshes.new('Continuous tread belt');mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new('Continuous tread belt',mesh);bpy.context.collection.objects.link(o);finish(o,'Continuous tread belt','Rubber')
    for i,(y,z) in enumerate(path):
        y2,z2=path[(i+1)%n];angle=math.atan2(z2-z,y2-y)
        box('Track steel grouser',(side*.4125,y,z),(.18,.027,.014),'Steel' if i%4 else 'Rust',.002,(angle,0,0))
    for y in (-.27,0,.27):
        radius=.158 if y else .13
        cyl('Road wheel tire',(side*.408,y,.215),radius,.14,'Rubber','X',32)
        cyl('Wheel stamped steel dish',(side*.492,y,.215),radius*.72,.012,'Paint','X',32)
        cyl('Wheel inner metal lip',(side*.500,y,.215),radius*.51,.012,'Steel','X',32)
        cyl('Greased hub',(side*.508,y,.215),.035,.016,'Rubber','X')
        cyl('Hub hex cap',(side*.518,y,.215),.020,.014,'Rust','X',6)
        for a in range(5):
            angle=a*math.pi*2/5
            cyl('Wheel bolt',(side*.510,y+math.cos(angle)*radius*.55,.215+math.sin(angle)*radius*.55),.006,.008,'Steel','X',6)
    # Thin fixed guards above each belt rather than broad toy-like yellow rails.
    box('Track steel cover',(side*.414,0,.415),(.20,.87,.024),'Paint',.007)
    for y in (-.29,.29):cyl('Skirt rusty fastener',(side*.414,y,.434),.010,.012,'Rust','Z',6)

# Sensor housing at the existing eye-height region, with glass and a narrow status bar.
cyl('Turret neck',(0,.06,.845),.125,.08,'Steel','Z',32)
cyl('Turret seal',(0,.06,.869),.13,.018,'Rubber','Z',32)
box('Scanner armored head',(0,-.015,.967),(.60,.35,.215),'Paint',.010)
box('Scanner visor rim',(0,-.200,.971),(.51,.030,.115),'Steel',.012)
box('Scanner recessed black seal',(0,-.218,.971),(.47,.014,.083),'Rubber',.009)
box('Scanner optical glass',(0,-.228,.972),(.438,.009,.059),'Glass',.008)
box('Scanner state strip',(0,-.235,.961),(.37,.004,.009),'Status',.002)
for x in (-.26,.26):cyl('Scanner face fastener',(x,-.201,.973),.009,.010,'Rust','Y',6)
for x in (-.18,.18):box('Scanner chipped corner',(x,-.187,1.060),(.065,.018,.007),'Rust',.002)
dented_cover('Dented sensor top cover',(-.025,.018,1.078),(.46,.24,.009),'Paint')
for side in (-1,1):
    box('Sensor side gasket',(side*.301,.025,.963),(.006,.23,.128),'Rubber',.003)
    box('Sensor side removable plate',(side*.305,.025,.963),(.006,.215,.114),'Paint',.003)
    for y in (-.06,.11):
        for z in (.925,1.00):cyl('Sensor side countersunk bolt',(side*.311,y,z),.005,.008,'Steel','X',6)
    for i in range(4):box('Sensor side heat vent',(side*.310,.026,.937+i*.016),(.003,.105,.006),'Rubber',.001)
box('Head rear connector cover',(0,.174,.975),(.28,.025,.11),'Rubber',.006)
for i in range(5):box('Sensor rear vent',(0,.193,.942+i*.017),(.24,.014,.005),'Steel',.001)
cable('Neck flexible conduit',[(.18,.11,.88),(.25,.18,.87),(.26,.20,.94),(.18,.195,.96)],.012,'Rubber')
cyl('Antenna socket',(.20,.12,1.086),.028,.03,'Steel','Z')
cable('Bent radio aerial',[(.20,.12,1.097),(.20,.12,1.19),(.215,.13,1.25)],.004,'Rubber')
cyl('Aerial worn tip',(.215,.13,1.25),.006,.018,'Steel','Z',12)

# Surface-scale scars and faded safety markings, kept restrained and asymmetric.
for i in range(5):
    x=-.25+i*.08
    box('Faded diagonal hazard stripe',(x,-.376,.455),(.018,.002,.045),'Warning',.0005,(0,-.45,0))
box('Serial plate',(-.12,-.379,.626),(.29,.005,.074),'Steel',.004)
label('UNIT 07',(-.12,-.384,.613),.039,'Rubber')
label('INSPECTION',(0,.389,.415),.030,'Warning').rotation_euler=(math.pi/2,0,math.pi)
for i,(x,z,w) in enumerate([(-.33,.72,.065),(-.19,.37,.10),(.04,.745,.09),(.32,.52,.04),(-.06,.46,.035)]):
    box('Paint edge spall',(x,-.373,z),(w,.003,.006),'Rust',.001,(0,.14*i,0))

# Uniform world-scale planar UVs on broad faces, explicit UV layer on all geometry.
# Micro-relief is provided by the project's sourced PBR maps, not hidden geometry.
for obj in parts:
    if obj.type!='MESH':continue
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
    for poly in obj.data.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a]))
        for loopid in poly.loop_indices:
            co=obj.matrix_world@obj.data.vertices[obj.data.loops[loopid].vertex_index].co
            uv.data[loopid].uv=((co.y,co.z) if axis==0 else (co.x,co.z) if axis==1 else (co.x,co.y))
    obj.select_set(False)

# Join by material to limit renderer count while keeping status glass independently addressable.
joined=[]
for key,material in MATS.items():
    group=[o for o in bpy.data.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==material]
    if not group:continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in group:o.select_set(True)
    bpy.context.view_layer.objects.active=group[0]
    if len(group)>1:bpy.ops.object.join()
    o=bpy.context.object;o.name='RustGuard_'+key
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Export normals remain explicit; bevels are baked mesh geometry.
    joined.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in joined:o.select_set(True)
bpy.context.view_layer.objects.active=joined[0]
points=[o.matrix_world@Vector(c) for o in joined for c in o.bound_box]
low=[min(v[i] for v in points) for i in range(3)];high=[max(v[i] for v in points) for i in range(3)]
dimensions=[high[i]-low[i] for i in range(3)]
assert dimensions[0]<=1.076 and dimensions[1]<=1.077 and dimensions[2]<=1.323,dimensions
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in joined)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source~'/'RustGuard.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'RustGuard.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
source={'author':'Original procedural model created for LastCellOfPower','license':'CC0-1.0','generator':'Tools/BuildRustGuard.py','blender':bpy.app.version_string,'baseline_world_bounds_m':[1.076,1.077,1.323],'bounds_blender_xyz':{'min':low,'max':high,'size':dimensions},'renderers':len(joined),'triangles':triangles,'notes':'Original industrial inspection-machine design; genre reference only; no Capcom artwork, model, logo or texture copied. Visual meshes only; all gameplay components remain in Unity baseline.'}
source['sha256']={name:hashlib.sha256((OUT/name).read_bytes()).hexdigest() for name in ('RustGuard.fbx','Source~/RustGuard.blend')}
(OUT/'RustGuard-source.json').write_text(json.dumps(source,indent=2,ensure_ascii=False),encoding='utf-8')
print('RUST_GUARD_GENERATED',json.dumps(source))
