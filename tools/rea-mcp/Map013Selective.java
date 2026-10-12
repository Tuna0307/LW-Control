// MAP-013 source-identified, bounded ORIGINAL PE Ghidra analysis.
// @category LW-Control
// Only use with the hash-checked original 0.3.17 PE and -noanalysis -readOnly -deleteProject.
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import java.io.PrintWriter;
import java.io.FileWriter;

public class Map013Selective extends GhidraScript {
    public void run() throws Exception {
        String[] arguments = getScriptArgs();
        if (arguments.length < 1) throw new IllegalArgumentException("expected ignored output path [optional hex function addresses]");
        String[] defaultEntries = {
            "14013a8c1", // map_scan Rust literal/pointer refs at 14013a965 and 14013aca5
            "140143d67", // map_scan xref at 140143e0b
            "140427120", // map.scan byte comparison xrefs
            "140159925"  // map_scan xref at 1401599c9
        };
        String[] entries = arguments.length > 1
            ? java.util.Arrays.copyOfRange(arguments, 1, arguments.length)
            : defaultEntries;
        DecompInterface decoder = new DecompInterface();
        try (PrintWriter output = new PrintWriter(new FileWriter(arguments[0], false))) {
            output.println("MAP013 Ghidra -noanalysis selective disassembly/decompilation");
            output.println("IMAGE=" + currentProgram.getName() + " LANGUAGE=" + currentProgram.getLanguageID());
            decoder.openProgram(currentProgram);
            for (String entryText : entries) {
                Address entry = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(entryText);
                output.println("\nENTRY=" + entry.toString());
                boolean disassembled = disassemble(entry);
                output.println("DISASSEMBLE=" + disassembled);
                Function function = currentProgram.getFunctionManager().getFunctionAt(entry);
                if (function == null) function = createFunction(entry, "Map013Candidate_" + entryText);
                output.println("FUNCTION=" + (function == null ? "NONE" : function.getName()));
                Instruction ins = currentProgram.getListing().getInstructionAt(entry);
                for (int i=0; i<64 && ins != null; i++) {
                    output.println("ASM " + ins.getAddress() + " " + ins.toString());
                    ins = ins.getNext();
                }
                if (function != null) {
                    DecompileResults result = decoder.decompileFunction(function, 8, monitor);
                    output.println("DECOMPILE_COMPLETED=" + result.decompileCompleted());
                    output.println("DECOMPILE_ERROR=" + result.getErrorMessage());
                    if (result.getDecompiledFunction() != null) {
                        String pseudo = result.getDecompiledFunction().getC();
                        output.println("PSEUDOCODE=" + pseudo.substring(0, Math.min(10000, pseudo.length())));
                    }
                }
                output.flush();
            }
        } finally { decoder.dispose(); }
    }
}
