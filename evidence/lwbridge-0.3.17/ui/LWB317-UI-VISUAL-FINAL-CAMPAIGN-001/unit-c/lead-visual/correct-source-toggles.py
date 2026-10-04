# Historical rejected broad checkbox patch. Exact variants were independently corrected by lead.
raise SystemExit("Historical patch is retired; inspect source-locators and README instead.")
from pathlib import Path
root=Path(__file__).resolve().parents[6]
page=root/'src/LWBridge.UI-0.3.17/src/AutomationPage.jsx'
source=page.read_text(encoding='utf-8')
# Original imported g = index-BVfnK1wp Bn. Its actual renderer returns a
# toggle-row button even when callers supply an ignored variant='checkbox'.
# Retain the exact existing draft setter and boolean transition for each field.
pairs=[
 ('<label className="automation-checkbox-row"><input type="checkbox" checked={replyEnabled} disabled={!enabled} onChange={(event) => setReplyEnabled(event.target.checked)} /><span>{t("automation.autoReply")}</span></label>', '<ToggleRow label={t("automation.autoReply")} checked={replyEnabled} disabled={!enabled} onChange={setReplyEnabled} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" {...check("treasureSearchEnabled")} disabled={!enabled} /><span>{t("automation.treasureAutoSearch")}</span></label>', '<ToggleRow label={t("automation.treasureAutoSearch")} checked={config.draft.treasureSearchEnabled ?? false} disabled={!enabled} onChange={(value) => check("treasureSearchEnabled").onChange({ target: { checked: value } })} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" checked={treasureDispatchEnabled} disabled={!enabled} onChange={(event) => setTreasureDispatchEnabled(event.target.checked)} /><span>{t("automation.treasureAutoDispatch")}</span></label>', '<ToggleRow label={t("automation.treasureAutoDispatch")} checked={treasureDispatchEnabled} disabled={!enabled} onChange={setTreasureDispatchEnabled} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" checked={ghostJoinEnabled} disabled={!enabled} onChange={(event) => setGhostJoinEnabled(event.target.checked)} /><span>{t("automation.ghost.autoJoinAlliance")}</span></label>', '<ToggleRow label={t("automation.ghost.autoJoinAlliance")} checked={ghostJoinEnabled} disabled={!enabled} onChange={setGhostJoinEnabled} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" {...check("ghostClaimRewards")} disabled={!enabled} /><span>{t("automation.ghost.autoClaimRewards")}</span></label>', '<ToggleRow label={t("automation.ghost.autoClaimRewards")} checked={config.draft.ghostClaimRewards ?? false} disabled={!enabled} onChange={(value) => check("ghostClaimRewards").onChange({ target: { checked: value } })} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" checked={constructionTargetEnabled} disabled={!enabled} onChange={(event) => setConstructionTargetEnabled(event.target.checked)} /><span>{t("automation.construction.targetEnabled")}</span></label>', '<ToggleRow label={t("automation.construction.targetEnabled")} checked={constructionTargetEnabled} disabled={!enabled} onChange={setConstructionTargetEnabled} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" {...check("autoClaimCompleted", true)} disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label>', '<ToggleRow label={t("automation.autoCollectRewards")} checked={config.draft.autoClaimCompleted ?? true} disabled={!enabled} onChange={(value) => check("autoClaimCompleted", true).onChange({ target: { checked: value } })} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" {...check("collectRewards", false)} disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label>', '<ToggleRow label={t("automation.autoCollectRewards")} checked={config.draft.collectRewards ?? false} disabled={!enabled} onChange={(value) => check("collectRewards", false).onChange({ target: { checked: value } })} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" checked={dispatchAssistEnabled} disabled={!enabled || assistFixture.busy} onChange={(event) => setDispatchAssistEnabled(event.target.checked)} /><span>{t("automation.dispatchAssist")}</span></label>', '<ToggleRow label={t("automation.dispatchAssist")} checked={dispatchAssistEnabled} disabled={!enabled || assistFixture.busy} onChange={setDispatchAssistEnabled} />'),
 ('<label className="automation-checkbox-row"><input type="checkbox" {...check("departWhenTicketsInsufficient")} disabled={!enabled} /><span>{t("automation.railwayDepartWhenTicketsInsufficient")}</span></label>', '<ToggleRow label={t("automation.railwayDepartWhenTicketsInsufficient")} checked={config.draft.departWhenTicketsInsufficient ?? false} disabled={!enabled} onChange={(value) => check("departWhenTicketsInsufficient").onChange({ target: { checked: value } })} />'),
]
for old,new in pairs:
 if new in source: continue
 assert source.count(old)==1, old
 source=source.replace(old,new)
page.write_text(source,encoding='utf-8',newline='\n')
print(f'SOURCE_TOGGLE_TRANSFORMS {len(pairs)}')
