using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CompilerProject.CodeGeneration
{
    /// <summary>
    /// مولد كود لغة التجميع (x86 Assembly & .NET CIL)
    /// ينتج ملفات output.asm و output.il ويقوم بالتجميع التلقائي إلى output.exe وتشغيله
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 7 - 13)
    /// </summary>
    public class AssemblyCodeGenerator
    {
        private readonly List<string> _tacInstructions;
        private readonly Dictionary<string, string> _varMap = new Dictionary<string, string>();
        private int _varIndex = 0;

        public AssemblyCodeGenerator(List<string> tacInstructions)
        {
            _tacInstructions = tacInstructions ?? new List<string>();
        }

        private string GetVarName(string raw)
        {
            if (!_varMap.TryGetValue(raw, out var mapped))
            {
                mapped = $"v_{_varIndex++}";
                _varMap[raw] = mapped;
            }
            return mapped;
        }

        /// <summary>
        /// توليد كود x86 Assembly وحفظه في output.asm
        /// </summary>
        public string GenerateX86Assembly(string outputPath = "output.asm")
        {
            var sb = new StringBuilder();
            sb.AppendLine("; ===================================================");
            sb.AppendLine("; كود لغة التجميع x86 Assembly المولد من المترجم العربي");
            sb.AppendLine("; ===================================================");
            sb.AppendLine(".386");
            sb.AppendLine(".model flat, stdcall");
            sb.AppendLine("option casemap :none");
            sb.AppendLine();
            sb.AppendLine(".data");

            var vars = new HashSet<string>();
            foreach (var line in _tacInstructions)
            {
                var match = Regex.Match(line, @"^(\S+)\s*=");
                if (match.Success)
                {
                    vars.Add(match.Groups[1].Value);
                }
                var readMatch = Regex.Match(line, @"^read\s+(\S+)");
                if (readMatch.Success)
                {
                    vars.Add(readMatch.Groups[1].Value);
                }
            }

            foreach (var v in vars)
            {
                sb.AppendLine($"    {GetVarName(v)} DD 0");
            }

            sb.AppendLine();
            sb.AppendLine(".code");
            sb.AppendLine("main PROC");

            foreach (var line in _tacInstructions)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

                // 1. Labels (L1:)
                if (line.EndsWith(":"))
                {
                    sb.AppendLine($"{line}");
                    continue;
                }

                // 2. Goto
                if (line.StartsWith("goto "))
                {
                    sb.AppendLine($"    JMP {line.Substring(5)}");
                    continue;
                }

                // 3. Conditional Jump (if a > b goto L1)
                var condMatch = Regex.Match(line, @"^if\s+(\S+)\s*(==|!=|<=|>=|<|>)\s*(\S+)\s+goto\s+(\S+)");
                if (condMatch.Success)
                {
                    string left = condMatch.Groups[1].Value;
                    string op = condMatch.Groups[2].Value;
                    string right = condMatch.Groups[3].Value;
                    string label = condMatch.Groups[4].Value;

                    sb.AppendLine($"    MOV EAX, {FormatOperand(left)}");
                    sb.AppendLine($"    CMP EAX, {FormatOperand(right)}");

                    string jmpInst = op switch
                    {
                        "==" => "JE",
                        "!=" => "JNE",
                        ">" => "JG",
                        "<" => "JL",
                        ">=" => "JGE",
                        "<=" => "JLE",
                        _ => "JNE"
                    };

                    sb.AppendLine($"    {jmpInst} {label}");
                    continue;
                }

                // 4. Binary Assignment (t1 = a + b)
                var binMatch = Regex.Match(line, @"^(\S+)\s*=\s*(\S+)\s*([\+\-\*\/\%\^\&\|\<\>\=\!]+)\s*(\S+)");
                if (binMatch.Success)
                {
                    string target = binMatch.Groups[1].Value;
                    string left = binMatch.Groups[2].Value;
                    string op = binMatch.Groups[3].Value;
                    string right = binMatch.Groups[4].Value;

                    sb.AppendLine($"    MOV EAX, {FormatOperand(left)}");
                    switch (op)
                    {
                        case "+": sb.AppendLine($"    ADD EAX, {FormatOperand(right)}"); break;
                        case "-": sb.AppendLine($"    SUB EAX, {FormatOperand(right)}"); break;
                        case "*": sb.AppendLine($"    IMUL EAX, {FormatOperand(right)}"); break;
                        case "/":
                        case "\\":
                            sb.AppendLine("    CDQ");
                            sb.AppendLine($"    MOV EBX, {FormatOperand(right)}");
                            sb.AppendLine("    IDIV EBX");
                            break;
                        case "%":
                            sb.AppendLine("    CDQ");
                            sb.AppendLine($"    MOV EBX, {FormatOperand(right)}");
                            sb.AppendLine("    IDIV EBX");
                            sb.AppendLine("    MOV EAX, EDX");
                            break;
                        case "&&": sb.AppendLine($"    AND EAX, {FormatOperand(right)}"); break;
                        case "||": sb.AppendLine($"    OR EAX, {FormatOperand(right)}"); break;
                    }
                    sb.AppendLine($"    MOV {FormatOperand(target)}, EAX");
                    continue;
                }

                // 5. Simple Assignment (a = 5)
                var assignMatch = Regex.Match(line, @"^(\S+)\s*=\s*(\S+)");
                if (assignMatch.Success)
                {
                    string target = assignMatch.Groups[1].Value;
                    string src = assignMatch.Groups[2].Value;
                    sb.AppendLine($"    MOV EAX, {FormatOperand(src)}");
                    sb.AppendLine($"    MOV {FormatOperand(target)}, EAX");
                    continue;
                }
            }

            sb.AppendLine("    RET");
            sb.AppendLine("main ENDP");
            sb.AppendLine("END main");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return sb.ToString();
        }

        /// <summary>
        /// توليد كود .NET CIL وحفظه في output.il
        /// </summary>
        public string GenerateCIL(string outputPath = "output.il")
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ===================================================");
            sb.AppendLine("// كود .NET Common Intermediate Language (CIL) المولد");
            sb.AppendLine("// ===================================================");
            sb.AppendLine(".assembly extern mscorlib {}");
            sb.AppendLine(".assembly ArabicProgram {}");
            sb.AppendLine(".module ArabicProgram.exe");
            sb.AppendLine();
            sb.AppendLine(".class public auto ansi beforefieldinit Program extends [mscorlib]System.Object");
            sb.AppendLine("{");
            sb.AppendLine("  .method public static void Main() cil managed");
            sb.AppendLine("  {");
            sb.AppendLine("    .entrypoint");
            sb.AppendLine("    .maxstack 64");

            // جمع وتصريح المتغيرات المحلية
            var localVars = new List<string>();
            var varIndexMap = new Dictionary<string, int>();

            foreach (var line in _tacInstructions)
            {
                var match = Regex.Match(line, @"^(\S+)\s*=");
                if (match.Success)
                {
                    string varName = match.Groups[1].Value;
                    if (!varIndexMap.ContainsKey(varName))
                    {
                        varIndexMap[varName] = localVars.Count;
                        localVars.Add(varName);
                    }
                }
                var readMatch = Regex.Match(line, @"^read\s+(\S+)");
                if (readMatch.Success)
                {
                    string varName = readMatch.Groups[1].Value;
                    if (!varIndexMap.ContainsKey(varName))
                    {
                        varIndexMap[varName] = localVars.Count;
                        localVars.Add(varName);
                    }
                }
            }

            if (localVars.Count > 0)
            {
                var decls = new List<string>();
                for (int i = 0; i < localVars.Count; i++)
                {
                    decls.Add($"[{i}] int32 {GetVarName(localVars[i])}");
                }
                sb.AppendLine($"    .locals init ({string.Join(", ", decls)})");
            }

            sb.AppendLine();

            foreach (var line in _tacInstructions)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

                // Labels
                if (line.EndsWith(":"))
                {
                    sb.AppendLine($"  {line}");
                    continue;
                }

                // Goto
                if (line.StartsWith("goto "))
                {
                    sb.AppendLine($"    br {line.Substring(5)}");
                    continue;
                }

                // Read statement: read x
                var readMatch = Regex.Match(line, @"^read\s+(\S+)");
                if (readMatch.Success)
                {
                    string varName = readMatch.Groups[1].Value;
                    if (varIndexMap.TryGetValue(varName, out int targetIdx))
                    {
                        sb.AppendLine("    call string [mscorlib]System.Console::ReadLine()");
                        sb.AppendLine("    call int32 [mscorlib]System.Int32::Parse(string)");
                        sb.AppendLine($"    stloc {targetIdx}");
                    }
                    continue;
                }

                // Print string literal: print "..."
                var printStrMatch = Regex.Match(line, @"^print\s+""(.*)""");
                if (printStrMatch.Success)
                {
                    string text = printStrMatch.Groups[1].Value;
                    string byteStr = EncodeILString(text);
                    sb.AppendLine($"    ldstr {byteStr}");
                    sb.AppendLine("    call void [mscorlib]System.Console::WriteLine(string)");
                    continue;
                }

                // Print variable or expression: print x
                var printMatch = Regex.Match(line, @"^print\s+(\S+)");
                if (printMatch.Success)
                {
                    EmitLoad(sb, printMatch.Groups[1].Value, varIndexMap);
                    sb.AppendLine("    call void [mscorlib]System.Console::WriteLine(int32)");
                    continue;
                }

                // Conditional Jump
                var condMatch = Regex.Match(line, @"^if\s+(\S+)\s*(==|!=|<=|>=|<|>)\s*(\S+)\s+goto\s+(\S+)");
                if (condMatch.Success)
                {
                    string left = condMatch.Groups[1].Value;
                    string op = condMatch.Groups[2].Value;
                    string right = condMatch.Groups[3].Value;
                    string label = condMatch.Groups[4].Value;

                    EmitLoad(sb, left, varIndexMap);
                    EmitLoad(sb, right, varIndexMap);

                    string branchInst = op switch
                    {
                        "==" => "beq",
                        "!=" => "bne.un",
                        ">" => "bgt",
                        "<" => "blt",
                        ">=" => "bge",
                        "<=" => "ble",
                        _ => "bne.un"
                    };

                    sb.AppendLine($"    {branchInst} {label}");
                    continue;
                }

                // Binary Assignment
                var binMatch = Regex.Match(line, @"^(\S+)\s*=\s*(\S+)\s*([\+\-\*\/\%\^\&\|\<\>\=\!]+)\s*(\S+)");
                if (binMatch.Success)
                {
                    string target = binMatch.Groups[1].Value;
                    string left = binMatch.Groups[2].Value;
                    string op = binMatch.Groups[3].Value;
                    string right = binMatch.Groups[4].Value;

                    EmitLoad(sb, left, varIndexMap);
                    EmitLoad(sb, right, varIndexMap);

                    switch (op)
                    {
                        case "+": sb.AppendLine("    add"); break;
                        case "-": sb.AppendLine("    sub"); break;
                        case "*": sb.AppendLine("    mul"); break;
                        case "/":
                        case "\\": sb.AppendLine("    div"); break;
                        case "%": sb.AppendLine("    rem"); break;
                        case "&&": sb.AppendLine("    and"); break;
                        case "||": sb.AppendLine("    or"); break;
                        case "<": sb.AppendLine("    clt"); break;
                        case ">": sb.AppendLine("    cgt"); break;
                        case "==": sb.AppendLine("    ceq"); break;
                    }

                    if (varIndexMap.TryGetValue(target, out int targetIdx))
                    {
                        sb.AppendLine($"    stloc {targetIdx}");
                    }
                    continue;
                }

                // Simple Assignment
                var assignMatch = Regex.Match(line, @"^(\S+)\s*=\s*(\S+)");
                if (assignMatch.Success)
                {
                    string target = assignMatch.Groups[1].Value;
                    string src = assignMatch.Groups[2].Value;

                    EmitLoad(sb, src, varIndexMap);
                    if (varIndexMap.TryGetValue(target, out int targetIdx))
                    {
                        sb.AppendLine($"    stloc {targetIdx}");
                    }
                    continue;
                }
            }

            sb.AppendLine("    ret");
            sb.AppendLine("  }");
            sb.AppendLine("}");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return sb.ToString();
        }

        private void EmitLoad(StringBuilder sb, string operand, Dictionary<string, int> varIndexMap)
        {
            if (int.TryParse(operand, out int num))
            {
                sb.AppendLine($"    ldc.i4 {num}");
            }
            else if (varIndexMap.TryGetValue(operand, out int idx))
            {
                sb.AppendLine($"    ldloc {idx}");
            }
            else
            {
                sb.AppendLine($"    ldc.i4 0");
            }
        }

        private string FormatOperand(string op)
        {
            return int.TryParse(op, out _) ? op : GetVarName(op);
        }

        private static string EncodeILString(string str)
        {
            var bytes = Encoding.Unicode.GetBytes(str);
            var sb = new StringBuilder("bytearray (");
            foreach (var b in bytes)
            {
                sb.Append($" {b:X2}");
            }
            sb.Append(" )");
            return sb.ToString();
        }

        /// <summary>
        /// تجميع ملف .il إلى ملف تنفيذي .exe وتشغيله
        /// </summary>
        public bool CompileToExe(string ilPath = "output.il", string exePath = "output.exe")
        {
            string ilasmPath = FindIlasm();
            if (string.IsNullOrEmpty(ilasmPath))
            {
                Console.WriteLine("⚠️ لم يتم العثور على أداة ilasm.exe في النظام.");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ilasmPath,
                    Arguments = $"/quiet /output:\"{exePath}\" \"{ilPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    return process.ExitCode == 0 && File.Exists(exePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ أثناء التجميع: {ex.Message}");
            }

            return false;
        }

        public string RunExe(string exePath = "output.exe", string input = "")
        {
            if (!File.Exists(exePath)) return "الملف التنفيذي غير موجود.";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string normalized = NormalizeDigits(input);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        var lines = normalized.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        foreach (var rawLine in lines)
                        {
                            string trimmed = rawLine.Trim();
                            process.StandardInput.WriteLine(string.IsNullOrEmpty(trimmed) ? "0" : trimmed);
                        }
                    }
                    else
                    {
                        process.StandardInput.WriteLine("0");
                    }
                    process.StandardInput.Close();

                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();

                    if (process.WaitForExit(3500))
                    {
                        string outStr = stdoutTask.Result;
                        string errStr = stderrTask.Result;
                        return !string.IsNullOrEmpty(errStr) ? $"{outStr}\n{errStr}" : outStr;
                    }
                    else
                    {
                        try { process.Kill(); } catch { }
                        return "⚠️ انتهت مهلة التشغيل (3.5 ثانية) - تأكد من إدخال القيمة المطلوبة في حقل الإدخال.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"خطأ أثناء التشغيل: {ex.Message}";
            }

            return "";
        }

        private static string NormalizeDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (c >= '٠' && c <= '٩') sb.Append((char)('0' + (c - '٠')));
                else if (c >= '۰' && c <= '۹') sb.Append((char)('0' + (c - '۰')));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string FindIlasm()
        {
            string[] possiblePaths = new[]
            {
                @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\ilasm.exe",
                @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe",
            };

            foreach (var p in possiblePaths)
            {
                if (File.Exists(p)) return p;
            }

            return "";
        }
    }
}
