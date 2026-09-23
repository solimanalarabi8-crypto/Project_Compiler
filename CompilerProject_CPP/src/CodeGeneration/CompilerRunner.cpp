#include "../../include/CodeGeneration/CompilerRunner.h"
#include "../../include/LexicalAnalysis/Lexer.h"
#include "../../include/SyntaxAnalysis/Parser.h"
#include "../../include/SemanticAnalysis/SemanticAnalyzer.h"
#include "../../include/IntermediateCode/IntermediateCodeGenerator.h"
#include "../../include/CodeGeneration/AssemblyCodeGenerator.h"
#include "../../include/CodeGeneration/TACInterpreter.h"
#include <iostream>

namespace CompilerCPP {

    CompilationResult CompilerRunner::Compile(const std::string& sourceCode, bool isVerbose, const std::string& userInput) {
        CompilationResult result;
        result.SourceCode = sourceCode;

        try {
            // 1. التحليل المعجمي
            if (isVerbose) std::cout << "🔹 [1/6] مرحلة التحليل المعجمي واللغوي (Lexical Analysis)...\n";
            Lexer lexer(sourceCode);
            result.Tokens = lexer.Tokenize();
            if (isVerbose) std::cout << "   ✔️ تم استخراج " << result.Tokens.size() << " رمزاً معجمياً بنجاح.\n";

            // 2. التحليل النحوي وبناء شجرة الإعراب
            if (isVerbose) std::cout << "🔹 [2/6] مرحلة التحليل النحوي وبناء شجرة الإعراب (Syntax Analysis)...\n";
            Parser parser(result.Tokens);
            result.AST = parser.ParseProgram();
            
            if (!parser.Errors.empty()) {
                result.SyntaxErrors = parser.Errors;
                result.IsSuccess = false;
                if (isVerbose) {
                    std::cout << "   ❌ تم اكتشاف أخطاء نحوية في الكود:\n";
                    for (const auto& err : result.SyntaxErrors) {
                        std::cout << "      ⚠️ " << err << "\n";
                    }
                }
                return result;
            }

            if (isVerbose && result.AST) {
                std::cout << "   ✔️ تم بناء شجرة الإعراب المجردة (AST) بنجاح:\n\n";
                result.AST->Print();
                std::cout << "\n";
            }

            // 3 & 4. جدول الرموز والتحليل الدلالي
            if (isVerbose) std::cout << "🔹 [3/6 & 4/6] مرحلة جدول الرموز والتحليل الدلالي (Semantic Analysis)...\n";
            SymbolTable symTable;
            SemanticAnalyzer analyzer(symTable);
            bool semanticOk = analyzer.Analyze(result.AST);

            for (const auto& pair : symTable.GetAllSymbols()) {
                result.Symbols.push_back(pair.second);
            }

            if (!semanticOk) {
                result.SemanticErrors = analyzer.GetErrors();
                result.IsSuccess = false;
                if (isVerbose) {
                    std::cout << "   ❌ تم اكتشاف أخطاء دلالية في الكود:\n";
                    for (const auto& err : result.SemanticErrors) {
                        std::cout << "      ⚠️ " << err << "\n";
                    }
                }
                return result;
            }

            if (isVerbose) {
                symTable.Print();
                std::cout << "   ✔️ التحليل الدلالي سليم 100%.\n\n";
            }

            // 5. الكود الوسيط (TAC)
            if (isVerbose) std::cout << "🔹 [5/6] مرحلة توليد الكود الوسيط ثلاثي العناوين (TAC)...\n";
            IntermediateCodeGenerator tacGen;
            result.TAC = tacGen.Generate(result.AST);
            if (isVerbose) {
                for (const auto& line : result.TAC) {
                    std::cout << "   " << line << "\n";
                }
                std::cout << "\n";
            }

            // 6. توليد كود التجميع والتنفيذ
            if (isVerbose) std::cout << "🔹 [6/6] مرحلة توليد كود التجميع والتنفيذ (Assembly & Runtime)...\n";
            AssemblyCodeGenerator asmGen(result.TAC);
            result.AssemblyCode = asmGen.GenerateX86Assembly();
            result.CILCode = asmGen.GenerateCIL();

            TACInterpreter interpreter(result.TAC, userInput);
            result.ExecutionOutput = interpreter.Execute();

            if (isVerbose) {
                std::cout << "═══════════════════════════════════════════════════════════\n";
                std::cout << "🖥️  مخرجات تشغيل البرنامج (Execution Output):\n";
                std::cout << "═══════════════════════════════════════════════════════════\n";
                std::cout << result.ExecutionOutput;
                std::cout << "═══════════════════════════════════════════════════════════\n";
                std::cout << "\n🎉 اكتملت جميع مراحل الترجمة والتشغيل في C++ بنجاح تام!\n";
            }

            result.IsSuccess = true;
        }
        catch (const std::exception& ex) {
            result.IsSuccess = false;
            result.SyntaxErrors.push_back(ex.what());
            if (isVerbose) {
                std::cout << "\n❌ [فشل الترجمة]: " << ex.what() << "\n";
            }
        }

        return result;
    }

} // namespace CompilerCPP
