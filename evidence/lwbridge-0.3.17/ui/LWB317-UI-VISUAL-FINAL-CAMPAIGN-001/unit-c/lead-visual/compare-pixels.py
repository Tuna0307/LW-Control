from pathlib import Path
import json, sys
from PIL import Image, ImageChops
import numpy as np
root=Path(__file__).resolve().parent / ('baseline' if '--baseline' in sys.argv else 'current')
packet=json.loads((root/'pairs.json').read_text(encoding='utf-8'))
results=[]
for pair in packet['pairs']:
 a=Image.open(root/pair['original']['screenshot']).convert('RGB');b=Image.open(root/pair['current']['screenshot']).convert('RGB')
 size=(max(a.width,b.width),max(a.height,b.height));aa=Image.new('RGB',size,(255,0,255));bb=Image.new('RGB',size,(0,255,255));aa.paste(a,(0,0));bb.paste(b,(0,0))
 diff=ImageChops.difference(aa,bb);changed=int(np.any(np.asarray(diff),axis=2).sum())
 original=json.loads((root/pair['original']['measurement']).read_text(encoding='utf-8'));current=json.loads((root/pair['current']['measurement']).read_text(encoding='utf-8'))
 card_diff=[]
 for index in range(max(len(original['cards']),len(current['cards']))):
  o=original['cards'][index] if index<len(original['cards']) else None;c=current['cards'][index] if index<len(current['cards']) else None
  if o!=c:card_diff.append({'index':index,'original':o,'current':c})
 text_differences=[]
 for index in range(max(len(original['textRuns']),len(current['textRuns']))):
  o=original['textRuns'][index] if index<len(original['textRuns']) else None;c=current['textRuns'][index] if index<len(current['textRuns']) else None
  if o!=c:text_differences.append({'index':index,'original':o,'current':c})
 results.append({'id':pair['id'],'category':pair['category'],'expanded':pair['expanded'],'originalSize':a.size,'currentSize':b.size,'changedPixels':changed,'outsideMasks':changed,'masks':[],'rawDomIdentical':original['html']==current['html'],'cardDifferences':card_diff,'textRunDifferences':text_differences})
output={'pairs':len(results),'exactPixels':sum(r['changedPixels']==0 for r in results),'cases':results,'classification':'Unmasked differences are findings requiring explanation/correction. This checker never converts saved PASS markers into parity. No availability masks applied.'}
(root/'decoded-pixel-results.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'pairs':len(results),'pixelExact':output['exactPixels'],'totalChangedPixels':sum(r['changedPixels'] for r in results)}))
