// One-time checked mechanical migration of placeholder callsites, reviewed against
// exact original caller-contract.json. Re-running after migration intentionally fails.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import crypto from 'node:crypto';
const here=path.dirname(fileURLToPath(import.meta.url));
const repo=path.resolve(here,'../../../../..');
const ui=path.join(repo,'src/LWBridge.UI-0.3.17/src');
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
const changes=[];
function edit(file,replacements){
 let source=fs.readFileSync(path.join(ui,file),'utf8'),before=source;
 for(const [old,next] of replacements){assert.equal(source.split(old).length-1,1,file+': '+old);source=source.replace(old,next);}
 fs.writeFileSync(path.join(ui,file),source);changes.push({file,before:hash(before),after:hash(source),replacements:replacements.length});
}
edit('MapDataPage.jsx',[
 ['import { useCallback, useEffect, useMemo, useRef, useState } from "react";','import { GameAssetImage } from "./GameAssetImage.jsx";\nimport { useCallback, useEffect, useMemo, useRef, useState } from "react";'],
 ['<span className="map-reward-icon game-asset-placeholder" aria-hidden="true" />','<GameAssetImage assetPath={item.iconPath} alt={name} className="map-reward-icon" />'],
]);
edit('Pages.jsx',[
 ['import { Activity,','import { GameAssetImage } from "./GameAssetImage.jsx";\nimport { Activity,'],
 ['<span className="automation-reward-icon game-asset-placeholder" aria-hidden="true" /> <span>{reward.name}</span>','<GameAssetImage assetPath={reward.iconPath} alt={reward.name || reward.key} className="automation-reward-icon" /> <span>{reward.name || reward.key}</span>'],
 ['<span className="trade-station-currency-icon game-asset-placeholder" aria-hidden="true" /><span>{currency.currencyName}</span>','{currency.currencyIconPath ? <GameAssetImage assetPath={currency.currencyIconPath} alt="" className="trade-station-currency-icon" /> : null}<span>{currency.currencyName}</span>'],
 ['<span className={`trade-station-good-frame quality-${item.quality}`}><span className="trade-station-good-icon game-asset-placeholder" aria-hidden="true" /></span>','<span className={`trade-station-good-frame quality-${item.quality || 0}`}>{item.quality > 0 ? <GameAssetImage spriteName={`cfm_tongyong_daojukuang_${item.quality}`} alt="" className="trade-station-good-frame-image" deferUntilVisible /> : null}<GameAssetImage assetPath={item.iconPath} alt={item.name} className="trade-station-good-icon" deferUntilVisible /></span>'],
 ['{purchase.quality > 0 ? <span className="trade-station-good-frame-image game-asset-placeholder" aria-hidden="true" /> : null}<span className="trade-station-good-icon game-asset-placeholder" aria-hidden="true" />','{purchase.quality > 0 ? <GameAssetImage spriteName={`cfm_tongyong_daojukuang_${purchase.quality}`} alt="" className="trade-station-good-frame-image" deferUntilVisible /> : null}<GameAssetImage assetPath={purchase.iconPath} alt={itemName} className="trade-station-good-icon" deferUntilVisible />'],
 ['{purchase.currencyIconPath ? <span className="trade-station-currency-icon game-asset-placeholder" aria-hidden="true" /> : null}','{purchase.currencyIconPath ? <GameAssetImage assetPath={purchase.currencyIconPath} alt="" className="trade-station-currency-icon" deferUntilVisible /> : null}'],
 ['<span className="equipment-position-hero-icon game-asset-placeholder" role="img" aria-label={hero.name} />','<GameAssetImage assetPath={hero.iconPath} alt={hero.name} className="equipment-position-hero-icon" />'],
 ['<span className="equipment-icon game-asset-placeholder" role="img" aria-label={equip.name || String(equip.equipUuid)} />','<GameAssetImage assetPath={equip.iconPath} alt={equip.name || String(equip.equipUuid)} className="equipment-icon" />'],
]);
fs.writeFileSync(path.join(here,'caller-migration.json'),JSON.stringify({changes},null,2)+'\n');
