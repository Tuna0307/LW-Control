"""Read-only comparison of a native City XLSX export to genuine published MapStore rows.
No workbook edits; ZIP/OOXML parsed using only Python standard library.
"""
from datetime import datetime, timedelta, timezone
from pathlib import Path
import hashlib,json,sqlite3,sys,xml.etree.ElementTree as ET,zipfile

HEADERS={
  "en":["Server","X","Y","Player","UID","UUID","Alliance","Level","HP","Shield Ends","Marked","Updated At"],
  "ja":["サーバー","X","Y","プレイヤー","UID","UUID","連盟","レベル","HP","シールド終了時刻","マーク","更新日時"],
}
NS="{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
BASE=datetime(1899,12,30)
def readval(cell):
    if cell is None:return None
    t=cell.find(NS+"is")
    if t is not None:return "".join(x.text or "" for x in t.iter(NS+"t"))
    v=cell.find(NS+"v")
    if v is None or v.text is None:return None
    if cell.attrib.get("t")=="str":return v.text
    return float(v.text)
def asday(v):
    return (BASE+timedelta(days=float(v))).replace(microsecond=0) if v is not None else None
def utc_from_ms(ms):
    return datetime.fromtimestamp(ms/1000,timezone.utc).replace(tzinfo=None,microsecond=0) if ms else None
def utc_from_sec(sec):
    return datetime.fromtimestamp(sec,timezone.utc).replace(tzinfo=None,microsecond=0) if sec else None
def main():
    if len(sys.argv)!=4:raise SystemExit("usage: verify_export.py <genuine-published-copy.db> <real-native-export.xlsx> <receipt.json>")
    database,workbook,receipt=map(Path,sys.argv[1:])
    with sqlite3.connect("file:"+database.as_posix()+"?mode=ro",uri=True) as con:
        records={}
        for uuid,raw,updated in con.execute("select uuid,data_json,updated_at from map_records where kind='city' and server_id=2212"):
            item=json.loads(raw);item["recordUpdatedAt"]=updated
            records.setdefault(str(uuid),[]).append(item)
    assert sum(len(group) for group in records.values())==7000
    with zipfile.ZipFile(workbook) as z:
        assert z.testzip() is None
        sheet=z.read("xl/worksheets/sheet1.xml")
    root=ET.fromstring(sheet)
    rows=root.find(NS+"sheetData")
    assert rows is not None
    actual=list(rows.findall(NS+"row"))
    assert len(actual)==7001,len(actual)
    seen=set()
    language=None
    mismatch_counts={key:0 for key in ["server","coordinate","name","ownerUid","uuid","alliance","level","health","updatedAt","marked","shield"]}
    for index,element in enumerate(actual):
        cells=[None]*12
        for cell in element.findall(NS+"c"):
            reference=cell.attrib["r"]
            letters="".join(x for x in reference if x.isalpha())
            if len(letters)==1 and "A"<=letters<="L":
                cells[ord(letters)-65]=readval(cell)
        if index==0:
            assert cells in HEADERS.values(),(cells,HEADERS)
            language=next(lang for lang,header in HEADERS.items() if header==cells)
            continue
        server,x,y,player,uid,uuid,alliance,level,hp,shield,marked,updated=cells
        uuid=str(uuid)
        row_identity=(uuid,str(uid),x,y)
        if row_identity in seen:raise RuntimeError("Workbook duplicates a full source identity")
        seen.add(row_identity)
        candidates=records.get(uuid,[])
        matches=[candidate for candidate in candidates if
            str(candidate.get("ownerUid"))==str(uid) and
            candidate.get("x")==x and candidate.get("y")==y]
        if len(matches)!=1:
            mismatch_counts["uuid"]+=1
            continue
        orig=matches[0]
        candidates.remove(orig)
        if server!=2212:mismatch_counts["server"]+=1
        if x!=orig.get("x") or y!=orig.get("y"):mismatch_counts["coordinate"]+=1
        if player!=orig.get("ownerName"):mismatch_counts["name"]+=1
        if str(uid)!=str(orig.get("ownerUid")):mismatch_counts["ownerUid"]+=1
        if (alliance or "")!=(orig.get("allianceName") or ""):mismatch_counts["alliance"]+=1
        if level!=orig.get("level"):mismatch_counts["level"]+=1
        if hp!=orig.get("health"):mismatch_counts["health"]+=1
        if marked!=("いいえ" if language=="ja" else "No"):mismatch_counts["marked"]+=1
        if asday(updated)!=utc_from_ms(orig.get("recordUpdatedAt")):mismatch_counts["updatedAt"]+=1
        # City protectEndTime from original Lua is in seconds; recordUpdatedAt is in milliseconds.
        if asday(shield)!=utc_from_sec(orig.get("protectEndTime")):mismatch_counts["shield"]+=1
    missing=sum(len(group) for group in records.values())
    assert not any(mismatch_counts.values()),mismatch_counts
    assert missing==0,missing
    output={"task":"LWB317-MAP-DATA-DELIVERY-003","sourceKind":"published genuine City copy",
      "serverId":2212,"sourcePublishedRows":7000,"nativeWorkbookDataRows":len(seen),
      "orderedColumns":HEADERS[language],"language":language,"allCellValueMismatchCounts":mismatch_counts,
      "missingRecords":missing,"distinctWorkbookSourceIdentities":len(seen),
      "distinctSourceUuids":6999,"sourceUuidCollisionPreserved":True,
      "xlsxSha256":hashlib.sha256(workbook.read_bytes()).hexdigest(),"xlsxBytes":workbook.stat().st_size,
      "sourceDbSha256":hashlib.sha256(database.read_bytes()).hexdigest(),
      "validation":"PASS"}
    receipt.parent.mkdir(parents=True,exist_ok=True)
    receipt.write_text(json.dumps(output,indent=2),encoding="utf8")
    print(json.dumps(output,indent=2))
if __name__=="__main__":main()
