using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CompilerProject.CodeGeneration;
using CompilerProject.Tests;

namespace CompilerProject
{
    /// <summary>
    /// نقطة الانطلاق الرئيسية لمترجم لغة البرمجة العربية (CLI & Test Runner)
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // إذا تم تمرير مسار ملف كمعامل سطر أوامر من المحرر (Language Editor)
            if (args.Length > 0)
            {
                string filePath = args[0];
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"❌ خطأ: الملف غير موجود في المسار '{filePath}'");
                    return;
                }

                string sourceCode = File.ReadAllText(filePath, Encoding.UTF8);
                bool jsonMode = args.Length > 1 && (args[1] == "--json" || args[1] == "-j");

                string userInput = "";
                if (Console.IsInputRedirected)
                {
                    try { userInput = Console.In.ReadToEnd(); } catch { }
                }

                var result = CompilerRunner.Compile(sourceCode, !jsonMode, userInput);

                if (jsonMode)
                {
                    string jsonOutput = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(jsonOutput);
                }

                return;
            }

            // في حال التشغيل المباشر بدون معاملات: تشغيل الاختبارات الشاملة والبرنامج التجريبي
            CompilerTestSuite.RunAllTests();

            Console.WriteLine();
            Console.WriteLine("=========================================================================================");
            Console.WriteLine("                  بدء تشغيل المترجم على البرنامج التجريبي المتكامل                       ");
            Console.WriteLine("=========================================================================================");
            Console.WriteLine();

            string arabicSourceCode =
@"برنامج حساب_العمليات ؛
ثابت
    الحد_الاقصى = 100 ؛
متغير
    س , ص , مجموع : صحيح ؛
{
    س = 20 ؛
    ص = 79 ؛
    مجموع = س + ص ؛

    اذا ( مجموع > 50 ) فان {
        اطبع ( ""المجموع اكبر من خمسين وهو:"" , مجموع ) ؛
    } والا {
        اطبع ( ""المجموع اقل من او يساوي خمسين"" ) ؛
    } ؛

    اطبع ( ""نهاية البرنامج بنجاح"" ) ؛
} .";

            CompilerRunner.Compile(arabicSourceCode, true);
        }
    }
}
