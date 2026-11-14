from pathlib import Path
import json,concurrent.futures
import PrepareChapterArtTextures as base
import hashlib,numpy as np
from PIL import Image
path=base.ROOT/'RealismSources.json'
if path.exists():base.PINNED+=json.loads(path.read_text(encoding='utf8'))['materials']
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
 records=list(pool.map(base.asset,[('WallConcrete','concrete_wall_003'),('PeelingPaint','painted_concrete_02'),('GuardCorrodedPaint','rusty_painted_metal')]))
source=base.ROOT/'Textures/GuardCorrodedPaint/Packed.png';folder=base.ROOT/'Textures/CoatedMetal';folder.mkdir(exist_ok=True)
pixels=np.array(Image.open(source).convert('RGBA'));pixels[:,:,0]=np.minimum(pixels[:,:,0],38);Image.fromarray(pixels).save(folder/'Packed.png')
derived={'path':'Textures/CoatedMetal/Packed.png','source':'Textures/GuardCorrodedPaint/Packed.png','operation':'R=min(R,38); preserve G/B/A; old paint and oxide are mostly dielectric','sha256':hashlib.sha256((folder/'Packed.png').read_bytes()).hexdigest()}
path.write_text(json.dumps({'materials':records,'derivedData':[derived],'purpose':'Realistic industrial art refinement; no Capcom game assets used'},ensure_ascii=False,indent=2),encoding='utf8')
print('Prepared 2 additional scanned architectural surfaces')
