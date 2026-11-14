"""Fetch pinned CC0 PBR inputs and deterministically pack URP data textures.
Only creates files in Assets/ChapterOneArt. Does not edit original project art.
"""
from pathlib import Path
import concurrent.futures, hashlib, json, urllib.request
import numpy as np
from PIL import Image, ImageFilter

ROOT=Path(__file__).resolve().parents[1]/'Assets/ChapterOneArt'
HEADERS={'User-Agent':'LastCellOfPower-ArtDelivery/1.0 (CC0 source manifest)'}
SOURCES={'Concrete':'concrete_floor_worn_001','PaintedMetal':'green_metal_rust',
         'RustedMetal':'rusty_metal_02','Wood':'rough_wood','Fabric':'hessian_230'}
PINNED=json.loads((ROOT/'Sources.json').read_text(encoding='utf-8'))['materials'] if (ROOT/'Sources.json').exists() else []
def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url,headers=HEADERS),timeout=60) as r:return r.read()
def sha(data):return hashlib.sha256(data).hexdigest()
def asset(item):
    family,asset_id=item
    pin=next((r for r in PINNED if r.get('assetId')==asset_id),None)
    info={'name':pin['title'],'authors':pin['authors'],'dimensions':pin.get('dimensionsMillimetres')} if pin else json.loads(fetch('https://api.polyhaven.com/info/'+asset_id))
    files=None if pin else json.loads(fetch('https://api.polyhaven.com/files/'+asset_id))
    folder=ROOT/'Textures'/family;folder.mkdir(parents=True,exist_ok=True)
    records=[]
    for kind,name,fmt in [('Diffuse','Base.jpg','jpg'),('nor_gl','Normal.png','png'),('arm','SourceARM.bytes','jpg')]:
        record=next((f for f in pin['files'] if f['kind']==kind),None) if pin else None
        entry={'url':record['url'],'md5':record['upstreamMD5']} if record else files[kind]['2k'][fmt]; target=folder/name
        if target.exists(): data=target.read_bytes()
        else: data=fetch(entry['url']);target.write_bytes(data)
        if hashlib.md5(data).hexdigest()!=entry['md5']:raise ValueError('Source checksum mismatch: '+str(target))
        records.append({'kind':kind,'url':entry['url'],'path':str(target.relative_to(ROOT)).replace('\\','/'),
                        'sha256':sha(data),'upstreamMD5':entry['md5'],'bytes':len(data)})
    a=np.array(Image.open(folder/'SourceARM.bytes').convert('RGB'))
    # URP Lit: R metallic, G occlusion, A smoothness. Source ARM: R AO, G rough, B metal.
    packed=np.empty((*a.shape[:2],4),dtype=np.uint8)
    packed[:,:,0]=a[:,:,2];packed[:,:,1]=a[:,:,0];packed[:,:,2]=0;packed[:,:,3]=255-a[:,:,1]
    Image.fromarray(packed).save(folder/'Packed.png')
    records.append({'kind':'DerivedURPPacked','path':str((folder/'Packed.png').relative_to(ROOT)).replace('\\','/'),
                    'sha256':sha((folder/'Packed.png').read_bytes()),'conversion':'R=ARM.B, G=ARM.R, B=0, A=255-ARM.G; linear'})
    return {'family':family,'assetId':asset_id,'title':info['name'],'authors':info['authors'],
            'source':'https://polyhaven.com/a/'+asset_id,'license':'CC0-1.0','licenseURL':'https://polyhaven.com/license',
            'dimensionsMillimetres':info.get('dimensions'),'files':records}

def procedural(family,base,roughness,metallic=0):
    folder=ROOT/'Textures'/family;folder.mkdir(parents=True,exist_ok=True)
    n=1024;rng=np.random.default_rng(1132026+len(family))
    fine=rng.normal(0,1,(n,n)); low=rng.integers(0,255,(32,32),dtype=np.uint8)
    cloud=np.asarray(Image.fromarray(low).resize((n,n),Image.Resampling.BICUBIC),dtype=float)/255-.5
    yy,xx=np.mgrid[:n,:n];grain=fine*2+cloud*20
    if family=='Cardboard':grain+=np.sin(xx*.7)*2
    if family=='Rubber':grain+=np.sin(yy*.18)*3
    rgb=np.clip(np.array(base)[None,None,:]+grain[:,:,None],0,255).astype('uint8')
    if family=='Grime':
        # Soft-edged stain atlas: visual decal only, no displacement or collision.
        radius=((xx/n-.5)/.48)**2+((yy/n-.5)/.42)**2
        alpha=np.clip((1-radius)*100+cloud*90,0,105).astype('uint8')
        alpha[[0,-1],:]=0;alpha[:,[0,-1]]=0
        Image.fromarray(np.dstack((rgb,alpha))).save(folder/'Base.png')
    else:Image.fromarray(rgb).save(folder/'Base.png')
    Image.new('RGB',(n,n),(128,128,255)).save(folder/'Normal.png')
    Image.new('RGBA',(n,n),(metallic,255,0,int(255*(1-roughness)))).save(folder/'Packed.png')
    return {'family':family,'source':'Tools/PrepareChapterArtTextures.py','authors':{'LastCellOfPower project':'Deterministic original texture'},
            'license':'Project-original','seed':1132026+len(family),
            'files':[{'path':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':sha(p.read_bytes())} for p in sorted(folder.glob('*.png'))]}

def main():
    ROOT.mkdir(parents=True,exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:records=list(pool.map(asset,SOURCES.items()))
    records += [procedural('Cardboard',(123,101,70),.91),procedural('Rubber',(42,45,43),.85),procedural('Grime',(44,40,29),.88)]
    (ROOT/'Sources.json').write_text(json.dumps({'textureImport':'Base sRGB; normals NormalMap; packed linear; no height displacement',
      'materials':records},ensure_ascii=False,indent=2),encoding='utf-8')
    license_dir=ROOT/'Licenses';license_dir.mkdir(exist_ok=True)
    if not (license_dir/'PolyHaven-license.html').exists():(license_dir/'PolyHaven-license.html').write_bytes(fetch('https://polyhaven.com/license'))
    if not (license_dir/'CC0-1.0.txt').exists():(license_dir/'CC0-1.0.txt').write_bytes(fetch('https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt'))
    print(json.dumps({'families':len(records),'files':sum(len(x['files']) for x in records),'root':str(ROOT)},ensure_ascii=False))
if __name__=='__main__':main()
