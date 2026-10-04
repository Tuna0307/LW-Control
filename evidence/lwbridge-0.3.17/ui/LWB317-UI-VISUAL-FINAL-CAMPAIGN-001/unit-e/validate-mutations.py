import json,pathlib
from PIL import Image
root=pathlib.Path(__file__).resolve().parent.parent
for unit in 'efgh':
    folder=root/f'unit-{unit}'/'current'
    meta=json.loads((folder/'mutation.json').read_text())
    a=Image.open(folder/meta['base']).convert('RGBA');b=Image.open(folder/meta['changed']).convert('RGBA')
    changed=sum(x!=y for x,y in zip(a.getdata(),b.getdata())) if a.size==b.size else None
    assert changed is None or changed>0,'A plausible visual mutation was not detected'
    (folder/'mutation-result.json').write_text(json.dumps({'detected':True,'changedPixels':changed,'sizeChanged':a.size!=b.size,'mutation':meta},indent=2)+'\n')
    print(unit,'plausible mutation detected',changed)
