"""Read-only decoded RGBA comparisons. Saved PASS/sha markers are not assertions."""
import json, pathlib
import numpy as np
from PIL import Image
root=pathlib.Path(__file__).resolve().parent.parent

def compare(a,b):
    with Image.open(a) as x,Image.open(b) as y:
        if x.size!=y.size:return None
        return int(np.any(np.asarray(x.convert('RGBA'))!=np.asarray(y.convert('RGBA')),axis=2).sum())
counts={}
for packet,n in [('equipment',20),('compact-current',16),('compact-baseline',16)]:
    base=root/'unit-d';data=json.loads((base/(packet+'-paired-browser.json')).read_text(encoding='utf-8'))
    assert not data['console']
    assert len(data['pairs'])==n
    errors=[]
    for row in data['pairs']:
        a,b=row['sides']['original'],row['sides']['current']
        changed=compare(base/a['screenshot']['path'],base/b['screenshot']['path'])
        if packet=='compact-baseline':assert changed is None or changed>0
        else:
            assert changed==0,(row['id'],changed)
            assert a['measured']==b['measured'],row['id']+' descendants'
        errors.append(changed)
    counts[packet]={'pairs':n,'changedPixels':errors}
for unit in 'efgh':
    base=root/('unit-'+unit)/'current';data=json.loads((base/'inventory.json').read_text(encoding='utf-8'))
    assert len(data['pairs'])==5
    for row in data['pairs']:
        assert compare(base/(row['id']+'-original.png'),base/(row['id']+'-current.png'))==0,row['id']
    mutation=json.loads((base/'mutation.json').read_text(encoding='utf-8'))
    assert compare(base/mutation['base'],base/mutation['changed'])!=0
    counts[unit]=5
base=root/'unit-c'/'lead-visual'/'current';data=json.loads((base/'pairs.json').read_text(encoding='utf-8'))
assert not data['errors'];assert len(data['pairs'])==56
for row in data['pairs']:
    assert compare(base/row['original']['screenshot'],base/row['current']['screenshot'])==0,row['id']
counts['automation-default-collapsed-expanded']=56
print(json.dumps({'decodedPairs':112,'noMasks':True,'counts':counts}))
