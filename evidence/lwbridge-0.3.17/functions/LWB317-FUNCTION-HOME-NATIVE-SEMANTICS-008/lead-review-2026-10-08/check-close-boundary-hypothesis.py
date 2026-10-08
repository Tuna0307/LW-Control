"""Source-bound boundary hypothesis, not a production or original-runtime parity test."""
import hashlib
import json
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
EXE = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
data = EXE.read_bytes()
expected = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
assert hashlib.sha256(data).hexdigest() == expected
pe = pefile.PE(data=data)
base = pe.OPTIONAL_HEADER.ImageBase
md = Cs(CS_ARCH_X86, CS_MODE_64)
instructions = {i.address-base:(i.mnemonic, i.op_str) for i in md.disasm(pe.get_data(0xE5725,0xE5884-0xE5725),base+0xE5725)}
assert instructions[0xE5794] == ("cmp","eax, ecx")
assert instructions[0xE5796] == ("jge",hex(base+0xE5811))
assert instructions[0xE57AD] == ("call",hex(base+0x41E3CF))
assert instructions[0xE5805] == ("jmp",hex(base+0xE5794))
source_path = REPO / "tools/run_live_resource_probe.py"
source = source_path.read_text(encoding="utf-8")
assert "if(-not $process.WaitForExit($waitMs)){exit 42}" in source
def reconstructed_poll(absent_at_ms):
    # Zero scheduler/process-enumeration overhead, used only to expose the ordering issue.
    now = 0
    for _ in range(100):
        if now >= absent_at_ms:
            return "success"
        now += 100
    return "timeout"
rows = [{"processAbsentAtMs":t, "originalLoopIdealSchedule":reconstructed_poll(t),
         "eventWaitIdealDeadlineModel":"success" if t < 10000 else "deadline-boundary-unresolved"}
        for t in (0, 9890, 9950, 10000)]
assert rows[2]["originalLoopIdealSchedule"] == "timeout"
result = {
    "referenceSha256":expected,
    "fact":"Original cap check precedes process check; exhausted timer resumes at cap check with no final process check.",
    "originalInstructionRvas":["0xE5794","0xE5796","0xE57AD","0xE5805"],
    "productionSourceSha256":hashlib.sha256(source_path.read_bytes()).hexdigest(),
    "productionPrimitive":"Process.WaitForExit(10000) after CloseMainWindow",
    "hypothesis":"Matching a nominal 10s budget does not establish equal terminal outcomes near the final poll; event waiting and capped polling may differ.",
    "models":rows,
    "proofClass":"SOURCE_BACKED_BOUNDARY_HYPOTHESIS_AND_RECONSTRUCTED_IDEAL_CLOCK_MODEL",
    "notProved":["Original process-checker identity/cardinality","Original close trigger and caller result mapping","Actual production versus original boundary outcome"],
    "requiredNext":"Align the operation and input process identity, then execute controlled actual production seam comparisons; no product fix from the model alone."
}
(LEAD / "close-boundary-hypothesis.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
print("LEAD008_CLOSE_BOUNDARY_HYPOTHESIS_RECORDED not full parity or confirmed product defect")
