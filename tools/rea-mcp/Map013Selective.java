// MAP-013 source-identified, bounded ORIGINAL PE Ghidra analysis.
// @category LW-Control
// Only use with the hash-checked original 0.3.17 PE and -noanalysis -readOnly -deleteProject.
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.address.AddressSet;
import java.io.PrintWriter;
import java.io.FileWriter;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

public class Map013Selective extends GhidraScript {
    public void run() throws Exception {
        String[] arguments = getScriptArgs();
        if (arguments.length < 3) throw new IllegalArgumentException("expected report path, ignored raw directory, timeout seconds, [hex entry:end-exclusive ...]");
        Path rawDir = Path.of(arguments[1]);
        Files.createDirectories(rawDir);
        int budgetSeconds = Integer.parseInt(arguments[2]);
        if (budgetSeconds < 10 || budgetSeconds > 300) throw new IllegalArgumentException("bounded decompile budget 10..300 seconds required");
        String[] defaultEntries = {
            "14013a8c1:14013b390", // map_scan Rust literal/pointer refs at 14013a965 and 14013aca5
            "140143d67:140144c41", // map_scan xref at 140143e0b
            "140427120:140428a92", // map.scan byte comparison xrefs
            "140159925:14015a27b"  // map_scan xref at 1401599c9
        };
        String[] entries = arguments.length > 3
            ? java.util.Arrays.copyOfRange(arguments, 3, arguments.length)
            : defaultEntries;
        DecompInterface decoder = new DecompInterface();
        try (PrintWriter output = new PrintWriter(new FileWriter(arguments[0], false))) {
            output.println("MAP013 Ghidra -noanalysis selective disassembly/decompilation");
            output.println("IMAGE=" + currentProgram.getName() + " LANGUAGE=" + currentProgram.getLanguageID());
            output.println("DECOMPILER_BUDGET_SECONDS_PER_FUNCTION=" + budgetSeconds);
            decoder.openProgram(currentProgram);
            for (String specification : entries) {
                String[] parts = specification.split(":", -1);
                if (parts.length != 2 || parts[0].isEmpty() || parts[1].isEmpty())
                    throw new IllegalArgumentException("require source-verified unwind start:end-exclusive: " + specification);
                String entryText = parts[0];
                Address entry = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(entryText);
                Address sourceEndExclusive = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(parts[1]);
                if (entry.compareTo(sourceEndExclusive) >= 0) throw new IllegalArgumentException("invalid source function range " + specification);
                output.println("\nENTRY=" + entry.toString());
                output.println("SOURCE_UNWIND_START=" + entry + " SOURCE_UNWIND_END_EXCLUSIVE=" + sourceEndExclusive);
                output.println("SOURCE_UNWIND_BYTE_LENGTH=" + sourceEndExclusive.subtract(entry));
                boolean disassembled = disassemble(entry);
                output.println("DISASSEMBLE=" + disassembled);
                Function function = currentProgram.getFunctionManager().getFunctionAt(entry);
                if (function == null) function = createFunction(entry, "Map013Candidate_" + entryText);
                output.println("FUNCTION=" + (function == null ? "NONE" : function.getName()));
                if (function != null) {
                    // Ghidra -noanalysis can auto-create a one-address stub even
                    // when PE .pdata establishes a much larger actual body.
                    // Constrain only this disposable/read-only project function
                    // to the caller-supplied, independently verified unwind range.
                    try {
                        function.setBody(new AddressSet(entry, sourceEndExclusive.subtract(1)));
                        output.println("SOURCE_RANGE_BODY_APPLIED=true");
                    } catch (Exception bodyError) {
                        output.println("SOURCE_RANGE_BODY_APPLIED=false ERROR=" + bodyError);
                    }
                    output.println("GHIDRA_BODY_START=" + function.getBody().getMinAddress());
                    output.println("GHIDRA_BODY_END_INCLUSIVE=" + function.getBody().getMaxAddress());
                    output.println("GHIDRA_BODY_ADDRESS_COUNT=" + function.getBody().getNumAddresses());
                    output.println("GHIDRA_BODY_MATCHES_SOURCE_RANGE=" +
                        (entry.equals(function.getBody().getMinAddress()) &&
                         sourceEndExclusive.subtract(1).equals(function.getBody().getMaxAddress())));
                }
                Instruction ins = currentProgram.getListing().getInstructionAt(entry);
                int shown = 0;
                while (shown < 64 && ins != null && ins.getAddress().compareTo(sourceEndExclusive) < 0) {
                    output.println("ASM " + ins.getAddress() + " " + ins.toString());
                    ins = ins.getNext();
                    shown++;
                }
                output.println("ASM_EXCERPT_INSTRUCTIONS=" + shown + " ASM_MORE_NOT_RETAINED=" + (ins != null && ins.getAddress().compareTo(sourceEndExclusive) < 0));
                if (function != null) {
                    DecompileResults result = decoder.decompileFunction(function, budgetSeconds, monitor);
                    output.println("DECOMPILE_COMPLETED=" + result.decompileCompleted());
                    output.println("DECOMPILE_ERROR=" + result.getErrorMessage());
                    output.println("DECOMPILE_WARNINGS=" + result.getErrorMessage());
                    if (result.getDecompiledFunction() != null) {
                        String pseudo = result.getDecompiledFunction().getC();
                        byte[] bytes = pseudo.getBytes(StandardCharsets.UTF_8);
                        Path raw = rawDir.resolve("original-" + entryText + ".pseudo.c");
                        Files.write(raw, bytes);
                        byte[] digest = MessageDigest.getInstance("SHA-256").digest(bytes);
                        StringBuilder hex = new StringBuilder();
                        for (byte b : digest) hex.append(String.format("%02x", b & 0xff));
                        String retained = Files.readString(raw, StandardCharsets.UTF_8);
                        output.println("PSEUDOCODE_RAW_PATH=" + raw.toAbsolutePath());
                        output.println("PSEUDOCODE_ORIGINAL_CHARS=" + pseudo.length());
                        output.println("PSEUDOCODE_RETAINED_CHARS=" + retained.length());
                        output.println("PSEUDOCODE_ORIGINAL_UTF8_BYTES=" + bytes.length);
                        output.println("PSEUDOCODE_RETAINED_UTF8_BYTES=" + Files.size(raw));
                        output.println("PSEUDOCODE_SHA256=" + hex);
                        output.println("PSEUDOCODE_TRUNCATED=false");
                        int excerptEnd = Math.min(1500, pseudo.length());
                        output.println("PSEUDO_EXCERPT_BEGIN_CHAR=0 PSEUDO_EXCERPT_END_CHAR_EXCLUSIVE=" + excerptEnd);
                        output.println("PSEUDO_EXCERPT_TRUNCATED=" + (excerptEnd < pseudo.length()));
                        output.println("PSEUDO_EXCERPT=" + pseudo.substring(0, excerptEnd));
                    }
                }
                output.flush();
            }
        } finally { decoder.dispose(); }
    }
}
