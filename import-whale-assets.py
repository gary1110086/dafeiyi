"""Lossless action/frame mapping from the installed MIT whale-maid assets to WPF PNG atlases."""
import argparse, hashlib, json, pathlib, shutil
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('plugin', type=pathlib.Path)
parser.add_argument('destination', type=pathlib.Path)
args = parser.parse_args()
source = args.plugin / 'assets'
manifest = json.loads((source / 'pet-manifest.json').read_text(encoding='utf-8'))
out = args.destination
out.mkdir(parents=True, exist_ok=True)
all_frames = [p for clip in manifest['clips'].values() for p in clip['frames']]
bounds = None
for name in all_frames:
    with Image.open(source / 'pet' / name) as im:
        box = im.convert('RGBA').getbbox()
        if box:
            bounds = box if bounds is None else (min(bounds[0],box[0]),min(bounds[1],box[1]),max(bounds[2],box[2]),max(bounds[3],box[3]))
width = 256
height = round((bounds[3]-bounds[1]) * width / (bounds[2]-bounds[0]))
native = {'version':1,'width':width,'height':height,'columns':6,'rows':6,'clips':{}}
provenance = {'plugin':'dsh-dafeiyu@0.1.14','character':manifest['characterId'],'sourceCrop':bounds,'sourceFrames':len(all_frames),'files':{}}
for clip_name, clip in manifest['clips'].items():
    unique, refs, rendered = {}, [], []
    for name in clip['frames']:
        path = source / 'pet' / name
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        provenance['files'][name] = digest
        if digest not in unique:
            unique[digest] = len(rendered)
            with Image.open(path) as im:
                rendered.append(im.convert('RGBA').crop(bounds).resize((width,height), Image.Resampling.LANCZOS))
        index = unique[digest]
        refs.append({'sheet':f'{clip_name}-{index//36:02d}.png','cell':index%36})
    for start in range(0,len(rendered),36):
        page = Image.new('RGBA',(width*6,height*6))
        for index, frame in enumerate(rendered[start:start+36]):
            page.paste(frame,((index%6)*width,(index//6)*height))
        page.save(out / f'{clip_name}-{start//36:02d}.png',compress_level=5)
    native['clips'][clip_name] = {'frameMs':clip['frameMs'],'loop':clip['loop'],'frames':refs}
    print(f'{clip_name}: {len(refs)} source frames, {len(rendered)} unique, {(len(rendered)+35)//36} sheets',flush=True)
(out / 'manifest.json').write_text(json.dumps(native,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
(out / 'provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(source / 'dsh-pet-LICENSE.txt',out / 'dsh-pet-LICENSE.txt')
shutil.copy2(args.plugin / 'LICENSE',out / 'dsh-dafeiyu-LICENSE.txt')
shutil.copy2(args.plugin / 'ASSET_LICENSE.md',out / 'ASSET_LICENSE.md')
print(f'Complete: {len(native["clips"])} clips, {len(all_frames)} frames, crop={bounds}, cell={width}x{height}',flush=True)
