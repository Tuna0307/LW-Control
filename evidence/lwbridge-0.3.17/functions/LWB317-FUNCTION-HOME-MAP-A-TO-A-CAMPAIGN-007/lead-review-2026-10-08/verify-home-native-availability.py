"""Lead-only exact-original byte availability proof; no original runtime execution."""
import bisect, hashlib, json
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
# Explicit owner-supplied artifact, not credentials or service state.
REFERENCE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
OUT = Path(__file__).with_name("home-native-body-availability.json")
EXPECTED = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
RANGES = {
    "profile_instance_start": (0x20715F, 0x207CA9),
    "profile_instance_stop": (0x199627, 0x19AC62),
    "profile_instance_status": (0x1A0DA4, 0x1A213C),
    "profile_instances_reconcile": (0x203021, 0x2057DC),
    "profile_instances_update_and_restart": (0x2057DC, 0x20715F),
}
data = REFERENCE.read_bytes()
assert hashlib.sha256(data).hexdigest() == EXPECTED
pe = pefile.PE(data=data)
pe.parse_data_directories(
    directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
functions = sorted((e.struct.BeginAddress,e.struct.EndAddress)
                   for e in pe.DIRECTORY_ENTRY_EXCEPTION)
begins = [r[0] for r in functions]
base = pe.OPTIONAL_HEADER.ImageBase
vm = Cs(CS_ARCH_X86,CS_MODE_64)
rows=[]
for label,(begin,end) in RANGES.items():
    body = pe.get_data(begin,end-begin)
    assert len(body)==end-begin
    decoded=list(vm.disasm(body,base+begin))
    calls=[]
    for ins in decoded:
        if ins.mnemonic != "call": continue
        if ins.op_str.startswith("0x"):
            target=int(ins.op_str,16)-base
            i=bisect.bisect_right(begins,target)-1
            fn=functions[i] if i>=0 and target<functions[i][1] else None
            calls.append({"instructionRva":hex(ins.address-base),"targetRva":hex(target),
                          "containingRuntimeFunction": [hex(v) for v in fn] if fn else None})
    rows.append({"command":label,"range":[hex(begin),hex(end)],"bodyBytes":len(body),
                 "bodySha256":hashlib.sha256(body).hexdigest(),
                 "decodedInstructionCount":len(decoded),
                 "directCalls":calls})
OUT.write_text(json.dumps({"referenceSha256":EXPECTED,"scope":"Original native byte availability and direct-call locators only; NO inferred retry/timing semantics",
    "commands":rows,"interpretation":"These native body bytes and direct call targets are present in the supplied executable. Undecoded semantics are unfinished static recovery, not by themselves missing external input."},indent=2)+"\n",encoding="utf-8")
print("LEAD007_HOME_NATIVE_BODY_BYTES_AVAILABLE",[(r["command"],r["bodyBytes"],len(r["directCalls"])) for r in rows])
