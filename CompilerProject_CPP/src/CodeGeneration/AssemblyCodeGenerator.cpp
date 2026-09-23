#include "../../include/CodeGeneration/AssemblyCodeGenerator.h"
#include <sstream>
#include <iomanip>

namespace CompilerCPP {

    AssemblyCodeGenerator::AssemblyCodeGenerator(std::vector<std::string> tac)
        : _tac(std::move(tac)) {}

    std::string AssemblyCodeGenerator::GenerateX86Assembly() {
        std::ostringstream ss;
        ss << "; ===================================================\n";
        ss << "; كود لغة التجميع x86 Assembly المولد من المترجم العربي (C++)\n";
        ss << "; ===================================================\n";
        ss << ".386\n";
        ss << ".model flat, stdcall\n";
        ss << "option casemap :none\n\n";

        ss << ".data\n";
        int varCount = 0;
        for (const auto& line : _tac) {
            if (line.find("=") != std::string::npos && line.find("print") == std::string::npos && line.find("//") == std::string::npos) {
                size_t eqPos = line.find("=");
                std::string varName = line.substr(0, eqPos);
                while (!varName.empty() && varName.back() == ' ') varName.pop_back();
                while (!varName.empty() && varName.front() == ' ') varName.erase(varName.begin());

                if (_varOffsets.find(varName) == _varOffsets.end()) {
                    std::string symName = "v_" + std::to_string(varCount++);
                    _varOffsets[varName] = symName;
                    ss << "    " << symName << " DD 0\n";
                }
            } else if (line.rfind("read ", 0) == 0) {
                std::string varName = line.substr(5);
                while (!varName.empty() && varName.back() == ' ') varName.pop_back();
                while (!varName.empty() && varName.front() == ' ') varName.erase(varName.begin());
                if (_varOffsets.find(varName) == _varOffsets.end()) {
                    std::string symName = "v_" + std::to_string(varCount++);
                    _varOffsets[varName] = symName;
                    ss << "    " << symName << " DD 0\n";
                }
            }
        }

        ss << "\n.code\nmain PROC\n";
        for (const auto& line : _tac) {
            if (line.rfind("//", 0) == 0 || line.empty()) continue;

            if (line.back() == ':') {
                ss << line << "\n";
            } else if (line.rfind("goto ", 0) == 0) {
                ss << "    JMP " << line.substr(5) << "\n";
            } else if (line.rfind("if ", 0) == 0) {
                size_t gPos = line.find(" goto ");
                std::string cond = line.substr(3, gPos - 3);
                std::string target = line.substr(gPos + 6);
                ss << "    ; Cond: " << cond << "\n";
                ss << "    JNE " << target << "\n";
            } else if (line.rfind("read ", 0) == 0) {
                std::string dest = line.substr(5);
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());
                std::string destVar = _varOffsets.count(dest) ? _varOffsets[dest] : "EAX";
                ss << "    ; read input into " << dest << "\n";
                ss << "    MOV " << destVar << ", EAX\n";
            } else if (line.find("=") != std::string::npos) {
                size_t eqPos = line.find("=");
                std::string dest = line.substr(0, eqPos);
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                std::string expr = line.substr(eqPos + 1);
                while (!expr.empty() && expr.front() == ' ') expr.erase(expr.begin());

                std::string destVar = _varOffsets.count(dest) ? _varOffsets[dest] : "EAX";
                ss << "    MOV EAX, " << expr << "\n";
                ss << "    MOV " << destVar << ", EAX\n";
            }
        }

        ss << "    RET\nmain ENDP\nEND main\n";
        return ss.str();
    }

    std::string AssemblyCodeGenerator::GenerateCIL() {
        std::ostringstream ss;
        ss << "// ===================================================\n";
        ss << "// كود .NET Common Intermediate Language (CIL) المولد (C++)\n";
        ss << "// ===================================================\n";
        ss << ".assembly extern mscorlib {}\n";
        ss << ".assembly ArabicProgram {}\n";
        ss << ".module ArabicProgram.exe\n\n";
        ss << ".class public auto ansi beforefieldinit Program extends [mscorlib]System.Object\n{\n";
        ss << "  .method public static void Main() cil managed\n  {\n";
        ss << "    .entrypoint\n    .maxstack 64\n";
        ss << "    ret\n  }\n}\n";
        return ss.str();
    }

} // namespace CompilerCPP
