"""Independent decoded RGBA comparison: no tolerance, masks or ignored regions."""
import json,sys,pathlib
from PIL import Image,ImageChops
root=pathlib.Path(__file__).resolve().parent.parent
version='baseline' if '--baseline' in sys.argv else 'current'
for unit in 'efgh':
    packet=root/f'unit-{unit}'/version
    inventory=json.loads((packet/'inventory.json').read_text(encoding='utf-8'))
    records=[]
    for pair in inventory['pairs']:
        a=Image.open(packet/f"{pair['id']}-original.png").convert('RGBA')
        b=Image.open(packet/f"{pair['id']}-current.png").convert('RGBA')
        if a.size!=b.size:
            record={'id':pair['id'],'originalSize':a.size,'currentSize':b.size,'pixelIdentical':False,'changedPixels':None,'reason':'size differs'}
        else:
            changed=sum(x!=y for x,y in zip(a.getdata(),b.getdata()))
            record={'id':pair['id'],'size':a.size,'changedPixels':changed,'pixelIdentical':changed==0}
        records.append(record)
    (packet/'pixel-results.json').write_text(json.dumps({'comparison':'Decoded RGBA, zero tolerance, no masks','pairs':records},indent=2)+'\n',encoding='utf-8')
    print(unit,version,[(r['id'],r['changedPixels']) for r in records])
    if version=='current':assert all(r['pixelIdentical'] for r in records),f'{unit}: unexplained pixel difference'
