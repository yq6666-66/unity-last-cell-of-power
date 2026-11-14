from pathlib import Path
import argparse,datetime,hashlib,shutil,json,zipfile
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--build',required=True);args=parser.parse_args();build=Path(args.build)
out=root.parent/('LastCellOfPower-WallFix-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));out.mkdir()
shutil.copytree(root.parent/'Builds/ChapterTactics-20261113-221423/ThirdPartyNotices',build/'ThirdPartyNotices',dirs_exist_ok=True)
evidence=root/'Evidence/WallSurfaceFix'
for src,name in [(root/'Documents/WallSurfaceFix-Delivery.md','README.md'),(evidence/'Acceptance.md','Acceptance.md'),(evidence/'before.mp4','Before.mp4'),(evidence/'after.mp4','After.mp4'),(evidence/'comparison.mp4','Comparison.mp4')]:shutil.copy2(src,out/name)
patch=list((root/'Assets/WallSurfaceFix').rglob('*'))+[root/'Assets/WallSurfaceFix.meta',root/'Assets/ChapterOneArt/Editor/ChapterArtRealismEnvironment.cs',root/'Assets/ChapterTactics/Editor/TacticsDelivery.cs',root/'Documents/WallSurfaceFix-Delivery.md',root/'Tools/PackageWallFix.py']
patch += [p for p in evidence.rglob('*') if p.suffix in ['.txt','.json','.md'] and p.stat().st_size<2000000 and not any(x.startswith('Failed') for x in p.parts)]
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
archives=[]
for name,base,files in [('WallFix-SourcePatch.zip',root,patch),('WallFix-Windows.zip',build,list(build.rglob('*')))]:
 files=sorted(set(p for p in files if p.is_file()));dest=out/name
 with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for p in files:z.write(p,p.relative_to(base).as_posix())
 with zipfile.ZipFile(dest) as z:
  assert z.testzip() is None
  for p in files:assert hashlib.sha256(z.read(p.relative_to(base).as_posix())).hexdigest()==sha(p)
 archives.append({'name':name,'sha256':sha(dest),'bytes':dest.stat().st_size,'entries':len(files),'crc':'PASS'})
(out/'SHA256SUMS.txt').write_text('\n'.join(a['sha256']+'  '+a['name'] for a in archives)+'\n',encoding='ascii')
(out/'verification.json').write_text(json.dumps({'build':str(build),'archives':archives},indent=2),encoding='utf8')
print(json.dumps({'delivery':str(out),'archives':archives},ensure_ascii=False,indent=2))
