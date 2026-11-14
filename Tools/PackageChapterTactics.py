"""Create a new, verified art delivery without replacing any earlier package."""
from pathlib import Path
import argparse,datetime,hashlib,json,shutil,zipfile

PROJECT=Path(__file__).resolve().parents[1]
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--build',required=True);args=parser.parse_args()
 build=Path(args.build).resolve()
 if not (build/'LastCellOfPower.exe').is_file():raise ValueError('Expected built Windows player')
 out=PROJECT.parent/('LastCellOfPower-Tactics-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));out.mkdir()
 notices=build/'ThirdPartyNotices';notices.mkdir(exist_ok=True)
 for source,relative in [
  (PROJECT/'Assets/ChapterOneArt/Licenses','ChapterArt'),
  (PROJECT/'Assets/ChapterOneArt/Sources.json','ChapterArt/Sources.json'),
  (PROJECT/'Assets/ChapterOneArt/RealismSources.json','ChapterArt/RealismSources.json'),
  (PROJECT/'Assets/ChapterOneArt/Models/wooden_crate_02/SOURCE.json','ChapterArt/Models/wooden_crate_02-SOURCE.json'),
  (PROJECT/'Assets/ChapterOneArt/Models/old_military_crate/SOURCE.json','ChapterArt/Models/old_military_crate-SOURCE.json'),
  (PROJECT/'Assets/ChapterOneArt/Models/steel_frame_shelves_03/SOURCE.json','ChapterArt/Models/steel_frame_shelves_03-SOURCE.json'),
  (PROJECT/'Assets/ChapterOneArt/Models/RustGuard/LICENSE.txt','ChapterArt/RustGuard/LICENSE.txt'),
  (PROJECT/'Assets/ChapterOneArt/Models/RustGuard/RustGuard-source.json','ChapterArt/RustGuard/SOURCE.json'),
  (PROJECT/'Assets/ChapterOne/Fonts/LICENSE.txt','NotoSansCJK/LICENSE.txt'),
  (PROJECT/'Assets/ChapterOne/Fonts/SOURCE.json','NotoSansCJK/SOURCE.json'),
  (PROJECT/'Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt','TMP/LiberationSans-OFL.txt'),
  (PROJECT/'Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt','TMP/EmojiOne-Attribution.txt')]:
  target=notices/relative;target.parent.mkdir(parents=True,exist_ok=True)
  if source.is_dir():shutil.copytree(source,target,dirs_exist_ok=True)
  elif source.is_file():shutil.copy2(source,target)
  else:raise FileNotFoundError(source)
 evidence=PROJECT/'Evidence/ChapterTactics'
 for source,name in [(PROJECT/'Documents/ChapterTactics-Delivery.md','README.md'),(evidence/'Acceptance.md','Acceptance.md'),(evidence/'gpu/tactics-render-probe.json','Performance.json'),
                     (evidence/'gpu/gpu-spawn-1920x1080.png','After.png'),(evidence/'gpu/gpu-guard-02-1920x1080.png','Guard02.png')]:
  shutil.copy2(source,out/name)
 source_files=[]
 for folder in ['Assets','Packages','ProjectSettings','Documents','Tools','Evidence','SourceArt']:
  source_files.extend(p for p in (PROJECT/folder).rglob('*') if p.is_file() and '__pycache__' not in p.parts)
 source_files.append(PROJECT/'CHANGELOG.md')
 windows_files=sorted(p for p in build.rglob('*') if p.is_file())
 archives=[]
 for name,root,files in [('ChapterTactics-Source.zip',PROJECT,sorted(source_files)),('ChapterTactics-Windows.zip',build,windows_files)]:
  target=out/name
  with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
   for p in files:z.write(p,p.relative_to(root).as_posix())
  with zipfile.ZipFile(target) as z:
   bad=z.testzip()
   if bad:raise ValueError('CRC failed: '+bad)
   records=[]
   for p in files:
    archived=hashlib.sha256(z.read(p.relative_to(root).as_posix())).hexdigest();current=digest(p)
    if archived!=current:raise ValueError('Archive differs from source: '+str(p))
    records.append({'path':p.relative_to(root).as_posix(),'sha256':current,'bytes':p.stat().st_size})
  archives.append({'name':name,'sha256':digest(target),'bytes':target.stat().st_size,'entries':len(files),'crc':'PASS','files':records})
 (out/'SHA256SUMS.txt').write_text('\n'.join(a['sha256']+'  '+a['name'] for a in archives)+'\n',encoding='ascii')
 (out/'delivery-verification.json').write_text(json.dumps({'delivery':str(out),'build':str(build),'archives':archives},ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'delivery':str(out),'archives':[{k:v for k,v in a.items() if k!='files'} for a in archives]},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
