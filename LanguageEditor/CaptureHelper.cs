using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CompilerProject.CodeGeneration;
using CompilerProject.Models;

namespace LanguageEditor
{
    public static class CaptureHelper
    {
        public static void RunCapture(string outputDir)
        {
            try
            {
                if (string.IsNullOrEmpty(outputDir))
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    outputDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "تسليم_المشروع_Final_Delivery", "imgs"));
                    if (!Directory.Exists(outputDir))
                    {
                        outputDir = Path.Combine(Directory.GetCurrentDirectory(), "تسليم_المشروع_Final_Delivery", "imgs");
                    }
                }

                var form = new MainForm();
                form.Size = new Size(1300, 820);
                form.Show();
                Application.DoEvents();

                void ForceAll(Control c)
                {
                    var h = c.Handle;
                    c.Visible = true;
                    foreach (Control child in c.Controls) ForceAll(child);
                }
                ForceAll(form);
                form.PerformLayout();
                Application.DoEvents();

                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var codeEditorField = typeof(MainForm).GetField("_codeEditor", flags);
                var tabControlField = typeof(MainForm).GetField("_outputTabControl", flags);
                var displayMethod = typeof(MainForm).GetMethod("DisplayCompilationResult", flags);

                RichTextBox editor = (RichTextBox)codeEditorField!.GetValue(form)!;
                TabControl tabs = (TabControl)tabControlField!.GetValue(form)!;

                string examplesDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Examples"));
                if (!Directory.Exists(examplesDir))
                {
                    examplesDir = Path.Combine(Directory.GetCurrentDirectory(), "Examples");
                }

                void CompileAndRender(string source, int activeTab, string filename)
                {
                    editor.Text = source;
                    var res = CompilerRunner.Compile(source, false);
                    displayMethod!.Invoke(form, new object[] { res, true });
                    tabs.SelectedIndex = activeTab;
                    form.Refresh();
                    Application.DoEvents();

                    string dest = Path.Combine(outputDir, filename);
                    using var bmp = new Bitmap(form.Width, form.Height);
                    using var g = Graphics.FromImage(bmp);
                    g.Clear(Color.FromArgb(240, 240, 240));

                    // Draw form directly
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));

                    // Specifically draw child controls if needed
                    void DrawControlsRecursive(Control parent, Point offset)
                    {
                        foreach (Control c in parent.Controls)
                        {
                            if (!c.Visible) continue;
                            Point pos = new Point(offset.X + c.Left, offset.Y + c.Top);
                            try
                            {
                                using var cbmp = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height));
                                c.DrawToBitmap(cbmp, new Rectangle(0, 0, c.Width, c.Height));
                                g.DrawImage(cbmp, pos);
                            }
                            catch { }
                            if (c.HasChildren) DrawControlsRecursive(c, pos);
                        }
                    }
                    DrawControlsRecursive(form, Point.Empty);

                    bmp.Save(dest, ImageFormat.Png);
                    Console.WriteLine("Rendered: " + filename + " (" + new FileInfo(dest).Length + " bytes)");
                }

                string code08 = File.Exists(Path.Combine(examplesDir, "08_برنامج_شامل_لكافة_القواعد.arb"))
                    ? File.ReadAllText(Path.Combine(examplesDir, "08_برنامج_شامل_لكافة_القواعد.arb")) : editor.Text;
                string code01 = File.Exists(Path.Combine(examplesDir, "01_المتغيرات_والعمليات_الحسابية.arb"))
                    ? File.ReadAllText(Path.Combine(examplesDir, "01_المتغيرات_والعمليات_الحسابية.arb")) : editor.Text;
                string code03 = File.Exists(Path.Combine(examplesDir, "03_الجمل_الشرطية_البسيطة_والمركبة.arb"))
                    ? File.ReadAllText(Path.Combine(examplesDir, "03_الجمل_الشرطية_البسيطة_والمركبة.arb")) : editor.Text;
                string code09 = File.Exists(Path.Combine(examplesDir, "09_اختبار_الاخطاء_النحوية.arb"))
                    ? File.ReadAllText(Path.Combine(examplesDir, "09_اختبار_الاخطاء_النحوية.arb")) : editor.Text;
                string code04 = File.Exists(Path.Combine(examplesDir, "04_حلقة_العداد_كرر.arb"))
                    ? File.ReadAllText(Path.Combine(examplesDir, "04_حلقة_العداد_كرر.arb")) : editor.Text;

                CompileAndRender(code08, 0, "screen01_editor_ui.png");
                CompileAndRender(code01, 0, "screen02_code_editing.png");
                CompileAndRender(code03, 0, "screen03_file_operations.png");
                CompileAndRender(code01, 1, "screen04_tokens_grid.png");
                CompileAndRender(code03, 2, "screen05_ast_tree.png");
                CompileAndRender(code08, 3, "screen06_symbol_table.png");
                CompileAndRender(code01, 3, "screen07_semantic_check.png");
                CompileAndRender(code01, 4, "screen08_tac_code.png");
                CompileAndRender(code01, 5, "screen09_assembly_cil.png");
                CompileAndRender(code09, 7, "screen10_errors_display.png");
                CompileAndRender(code04, 0, "screen11_execution_output.png");

                Console.WriteLine("ALL 11 REAL SCREENSHOTS RENDERED!");
                form.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("CAPTURE EXCEPTION: " + ex);
            }
        }
    }
}
