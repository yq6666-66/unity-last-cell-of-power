"""Download three CC0 real-world props with immutable source hashes for art R2."""
from pathlib import Path
import urllib.request,json,hashlib,concurrent.futures
ROOT=Path(__file__).resolve().parents[1]
IDS=['wooden_crate_02','old_military_crate','steel_frame_shelves_03']
HEADER={'User-Agent':'LastCellOfPower-ArtDelivery/1.0'}
def fetch(url):return urllib.request.urlopen(urllib.request.Request(url,headers=HEADER),timeout=90).read()
def run(asset):
 folder=ROOT/'SourceArt/PolyHavenModels'/asset;folder.mkdir(parents=True,exist_ok=True)
 pin=folder/'SOURCE.json'
 if pin.exists():r=json.loads(pin.read_text(encoding='utf8'))
 else:
  info=json.loads(fetch('https://api.polyhaven.com/info/'+asset));f=json.loads(fetch('https://api.polyhaven.com/files/'+asset))['blend']['2k']['blend']
  r={'assetId':asset,'title':info['name'],'authors':info['authors'],'source':'https://polyhaven.com/a/'+asset,'license':'CC0-1.0','files':[{'path':'source.blend','url':f['url'],'md5':f['md5']}]+[{'path':k,'url':v['url'],'md5':v['md5']} for k,v in f.get('include',{}).items()]}
 for entry in r['files']:
  p=folder/entry['path'];p.parent.mkdir(parents=True,exist_ok=True)
  data=p.read_bytes() if p.exists() else fetch(entry['url'])
  if hashlib.md5(data).hexdigest()!=entry['md5']:raise RuntimeError('Checksum mismatch '+str(p))
  if not p.exists():p.write_bytes(data)
  entry['sha256']=hashlib.sha256(data).hexdigest()
 pin.write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf8');print(asset,'downloaded and verified',flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:list(pool.map(run,IDS))
