using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using CompilerProject.Common;
using CompilerProject.LexicalAnalysis;
using CompilerProject.SyntaxAnalysis;
using CompilerProject.SemanticAnalysis;
using CompilerProject.IntermediateCode;
using CompilerProject.Models;

namespace CompilerProject.CodeGeneration
{
    /// <summary>
    /// منسق مراحل المترجم العربي (Arabic Compiler Pipeline Runner)
    /// يقوم بتشغيل المراحل الست وإنتاج كائن CompilationResult الشامل
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 1 - 13)
    /// </summary>
    public class CompilerRunner
    {
        public static CompilationResult Compile(string sourceCode, bool isVerbose = true, string userInput = "")
        {
            Console.OutputEncoding = Encoding.UTF8;
            var result = new CompilationResult { SourceCode = sourceCode };

            if (isVerbose)
            {
                Console.WriteLine("╔═════════════════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                 مشروع مترجم لغة البرمجة العربية المتكامل (CS 351)                        ║");
                Console.WriteLine("║                        إشراف: د. خالد الكحصة - جامعة إب 2025                             ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
            }

            try
            {
                // 1. مرحلة التحليل المعجمي
                if (isVerbose) Console.WriteLine("🔹 [1/6] مرحلة التحليل اللغوي والمعجمي (Lexical Analysis)...");
                var lexer = new Lexer(sourceCode);
                var tokens = lexer.Tokenize();
                foreach (var t in tokens)
                {
                    result.Tokens.Add(new TokenDto { Value = t.Value, Type = t.Type.ToString(), Line = t.Line });
                }
                if (isVerbose) Console.WriteLine($"   ✔️ تم استخراج {tokens.Count} رمزاً معجمياً (Tokens) بنجاح.\n");

                // 2. مرحلة التحليل النحوي
                if (isVerbose) Console.WriteLine("🔹 [2/6] مرحلة التحليل النحوي وبناء شجرة الإعراب (Syntax Analysis & AST)...");
                var parser = new Parser(tokens);
                Node? astRoot = null;
                try
                {
                    astRoot = parser.ParseProgram();
                    result.AST = MapNode(astRoot);
                }
                catch (Exception ex)
                {
                    if (!parser.Errors.Contains(ex.Message)) parser.Errors.Add(ex.Message);
                }

                if (parser.Errors.Count > 0)
                {
                    result.SyntaxErrors.AddRange(parser.Errors);
                    result.IsSuccess = false;
                    if (isVerbose)
                    {
                        foreach (var err in parser.Errors) Console.WriteLine($"❌ {err}");
                    }
                    return result;
                }

                if (isVerbose && astRoot != null)
                {
                    Console.WriteLine("   ✔️ تم بناء شجرة الإعراب المجردة (AST) بنجاح:\n");
                    astRoot.Print();
                    Console.WriteLine();
                }

                // 3 & 4. جدول الرموز والتحليل الدلالي
                if (isVerbose) Console.WriteLine("🔹 [3/6 & 4/6] مرحلة جدول الرموز والتحليل الدلالي (Semantic Analysis)...");
                var symbolTable = new SymbolTable();
                var semanticAnalyzer = new SemanticAnalyzer(symbolTable);
                bool isSemanticallyValid = semanticAnalyzer.Analyze(astRoot);

                foreach (var sym in symbolTable.GetAll())
                {
                    result.SymbolTable.Add(new SymbolDto
                    {
                        Name = sym.Name,
                        DataType = sym.DataType,
                        Kind = sym.Kind,
                        Value = string.IsNullOrEmpty(sym.Value) ? "-" : sym.Value,
                        DeclaredLine = sym.DeclaredLine,
                        ReferencedLines = sym.ReferencedLines.Count > 0 ? string.Join(", ", sym.ReferencedLines) : "-"
                    });
                }

                if (isVerbose) symbolTable.Print();

                if (!isSemanticallyValid)
                {
                    result.SemanticErrors.AddRange(semanticAnalyzer.Errors);
                    result.IsSuccess = false;
                    if (isVerbose)
                    {
                        Console.WriteLine("\n❌ فشل التحليل الدلالي:");
                        foreach (var err in semanticAnalyzer.Errors) Console.WriteLine($"   ⚠️ {err}");
                    }
                    return result;
                }

                if (isVerbose) Console.WriteLine("   ✔️ التحليل الدلالي سليم 100%.\n");

                // 5. الكود الوسيط
                if (isVerbose) Console.WriteLine("🔹 [5/6] مرحلة توليد الكود الوسيط ثلاثي العناوين (TAC)...");
                var tacGenerator = new IntermediateCodeGenerator();
                var tacList = tacGenerator.Generate(astRoot);
                result.TAC.AddRange(tacList);
                if (isVerbose)
                {
                    foreach (var tac in tacList) Console.WriteLine($"   {tac}");
                    Console.WriteLine();
                }

                // 6. توليد كود التجميع والتنفيذ
                if (isVerbose) Console.WriteLine("🔹 [6/6] مرحلة توليد كود التجميع والتنفيذ (Assembly & Execution)...");
                var asmGen = new AssemblyCodeGenerator(tacList);
                result.AssemblyCode = asmGen.GenerateX86Assembly("output.asm");
                result.CILCode = asmGen.GenerateCIL("output.il");

                if (isVerbose)
                {
                    Console.WriteLine("   ✔️ تم توليد ملف output.asm و output.il بنجاح.");
                    Console.WriteLine("\n🚀 جاري تجميع ملف output.il إلى ملف تنفيذي output.exe عبر ilasm...");
                }

                bool compiled = asmGen.CompileToExe("output.il", "output.exe");
                if (compiled)
                {
                    result.ExecutionOutput = asmGen.RunExe("output.exe", userInput);
                    result.IsSuccess = true;
                    if (isVerbose)
                    {
                        Console.WriteLine("   ✔️ تم إنتاج الملف التنفيذي output.exe بنجاح!\n");
                        Console.WriteLine("═══════════════════════════════════════════════════════════");
                        Console.WriteLine("🖥️  مخرجات تشغيل البرنامج التنفيذي (Actual Execution Output):");
                        Console.WriteLine("═══════════════════════════════════════════════════════════");
                        Console.WriteLine(result.ExecutionOutput);
                        Console.WriteLine("═══════════════════════════════════════════════════════════\n");
                        Console.WriteLine("🎉 اكتملت جميع مراحل الترجمة والتشغيل بنجاح تام!");
                    }
                }
                else
                {
                    result.IsSuccess = false;
                    if (isVerbose) Console.WriteLine("⚠️ تعذر التجميع المباشر عبر ilasm.");
                }
            }
            catch (Exception ex)
            {
                result.SyntaxErrors.Add(ex.Message);
                result.IsSuccess = false;
                if (isVerbose) Console.WriteLine($"❌ خطأ: {ex.Message}");
            }

            return result;
        }

        private static NodeDto MapNode(Node node)
        {
            var dto = new NodeDto
            {
                Value = node.Value,
                Name = node.Name,
                DataType = node.DataType,
                Val = node.Val,
                Line = node.Line
            };

            foreach (var child in node.Children)
            {
                dto.Children.Add(MapNode(child));
            }

            return dto;
        }
    }
}
