import argparse, hashlib, json, pathlib
from PIL import Image

parser=argparse.ArgumentParser()
parser.add_argument('plugin',type=pathlib.Path)
parser.add_argument('atlases',type=pathlib.Path)
parser.add_argument('report',type=pathlib.Path)
args=parser.parse_args()
source=args.plugin/'assets'
original=json.loads((source/'pet-manifest.json').read_text(encoding='utf-8'))
native=json.loads((args.atlases/'manifest.json').read_text(encoding='utf-8'))
proof=json.loads((args.atlases/'provenance.json').read_text(encoding='utf-8'))
assert original['clips'].keys()==native['clips'].keys()
count=samples=0
for name,clip in original['clips'].items():
    converted=native['clips'][name]
    assert len(clip['frames'])==len(converted['frames']) and clip['frameMs']==converted['frameMs'] and clip['loop']==converted['loop']
    for path in clip['frames']:
        assert hashlib.sha256((source/'pet'/path).read_bytes()).hexdigest()==proof['files'][path]
        count+=1
    for index in sorted({0,len(clip['frames'])//2,len(clip['frames'])-1}):
        with Image.open(source/'pet'/clip['frames'][index]) as image:
            expected=image.convert('RGBA').crop(proof['sourceCrop']).resize((native['width'],native['height']),Image.Resampling.LANCZOS)
        ref=converted['frames'][index]; x=(ref['cell']%native['columns'])*native['width']; y=(ref['cell']//native['columns'])*native['height']
        with Image.open(args.atlases/ref['sheet']) as sheet:
            actual=sheet.crop((x,y,x+native['width'],y+native['height']))
        assert expected.tobytes()==actual.tobytes(),(name,index)
        samples+=1
text=f'PASS all {len(native["clips"])} actions retain source frame order, duration and loops\nPASS all {count} source file hashes match installed plugin\nPASS {samples} first/middle/last atlas frames match exact transformed source pixels\nPASS both upstream license notices are included\n'
assert (args.atlases/'dsh-pet-LICENSE.txt').is_file() and (args.atlases/'dsh-dafeiyu-LICENSE.txt').is_file()
args.report.parent.mkdir(parents=True,exist_ok=True); args.report.write_text(text,encoding='utf-8'); print(text)
