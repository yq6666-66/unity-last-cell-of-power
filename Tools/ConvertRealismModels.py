"""Run via Blender --background --factory-startup --disable-autoexec --python.
Exports only CC0 source mesh geometry and correctly packed PBR material data.
"""
import bpy,sys,json,re,hashlib,shutil
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
IDS=['wooden_crate_02','old_military_crate','steel_frame_shelves_03']
def clean(s):return re.sub(r'[^a-zA-Z0-9_-]','_',s)
def node_images(tree,seen=None):
 seen=set() if seen is None else seen
 if not tree or tree.name in seen:return []
 seen.add(tree.name);result=[]
 for node in tree.nodes:
  if node.type=='TEX_IMAGE' and node.image:result.append(node.image)
  elif node.type=='GROUP':result.extend(node_images(node.node_tree,seen))
 return result
def pixels(path):
 im=bpy.data.images.load(str(path),check_existing=False);im.colorspace_settings.name='Non-Color'
 a=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(a);a=a.reshape(im.size[1],im.size[0],4)
 return im,a
def png(path,data):
 h,w=data.shape[:2];im=bpy.data.images.new('ExportData',width=w,height=h,alpha=True,float_buffer=False)
 im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(data.ravel());im.filepath_raw=str(path);im.file_format='PNG';im.save();bpy.data.images.remove(im)
for asset in ([sys.argv[-1]] if '--asset' in sys.argv else IDS):
 source=ROOT/'SourceArt/PolyHavenModels'/asset;out=ROOT/'Assets/ChapterOneArt/Models'/asset;out.mkdir(parents=True,exist_ok=True);(out/'Textures').mkdir(exist_ok=True)
 bpy.ops.wm.open_mainfile(filepath=str(source/'source.blend'),load_ui=False,use_scripts=False)
 mesh_objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and (asset!='old_military_crate' or o.name.endswith('_a'))]
 materials={s.material.name:s.material for o in mesh_objects for s in o.material_slots if s.material}
 manifest=[]
 for name,mat in materials.items():
  images=node_images(mat.node_tree)
  # Blender's //textures\file is a relative path, not a Windows UNC share.
  names={im.filepath.replace('\\','/').rsplit('/',1)[-1] for im in images}
  maps={}
  for kind,fragment in [('base','_diff_'),('normal','_nor_gl_'),('rough','_rough_'),('metal','_metal_')]:
   found=[n for n in names if fragment in n]
   if found:maps[kind]=source/'textures'/found[0]
  if 'base' not in maps:continue
  tag=clean(name);base=out/'Textures'/(tag+'_Base'+maps['base'].suffix);shutil.copy2(maps['base'],base)
  if 'normal' in maps:
   im,a=pixels(maps['normal']);png(out/'Textures'/(tag+'_Normal.png'),a);bpy.data.images.remove(im)
  im,rough=pixels(maps['rough']);bpy.data.images.remove(im)
  packed=np.ones(rough.shape,dtype=np.float32);packed[:,:,0]=0;packed[:,:,2]=0;packed[:,:,3]=1-np.clip(rough[:,:,0],0,1)
  if 'metal' in maps:
   im,metal=pixels(maps['metal']);packed[:,:,0]=np.clip(metal[:,:,0],0,1);bpy.data.images.remove(im)
  png(out/'Textures'/(tag+'_Packed.png'),packed)
  manifest.append({'name':name,'base':base.name,'normal':tag+'_Normal.png' if 'normal' in maps else '', 'packed':tag+'_Packed.png'})
 bpy.ops.object.select_all(action='DESELECT')
 for obj in mesh_objects:
  effective=[s.material for s in obj.material_slots]
  obj.data=obj.data.copy()
  obj.data.materials.clear()
  for material in effective:obj.data.materials.append(material)
  obj.select_set(True)
 if mesh_objects:bpy.context.view_layer.objects.active=mesh_objects[0]
 bpy.ops.export_scene.fbx(filepath=str(out/'Model.fbx'),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,path_mode='STRIP',mesh_smooth_type='FACE')
 (out/'Materials.json').write_text(json.dumps({'assetId':asset,'materials':manifest},indent=2),encoding='utf8')
 generated=[out/'Model.fbx',out/'Materials.json']+sorted(p for p in (out/'Textures').iterdir() if p.suffix.lower() in ['.png','.jpg'])
 record=json.loads((source/'SOURCE.json').read_text(encoding='utf8'));record['export']={'tool':'Blender '+bpy.app.version_string,'script':'Tools/ConvertRealismModels.py','files':[{'path':p.relative_to(out).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in generated]}
 if asset=='old_military_crate':record['export']['selectedVariant']='closed _a only; exclude _b open variant'
 (out/'SOURCE.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8')
 print('EXPORTED',asset,'meshObjects',len(mesh_objects),'materials',len(manifest),flush=True)
