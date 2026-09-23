using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using CompilerProject.CodeGeneration;
using CompilerProject.Models;

namespace LanguageEditor
{
    /// <summary>
    /// بيئة التطوير والمحرر الرسومي لمترجم لغة البرمجة العربية
    /// - يدعم التحديث والتحليل اللحظي المباشر أثناء الكتابة (Live Real-Time Analysis)
    /// - يظهر الأخطاء النحوية والدلالية فوراً في شاشة التشغيل وجدول الأخطاء مع تحديد السطر
    /// - يراعي اتجاه النصوص بدقة (RTL للعربية و LTR للغات التجميع)
    /// </summary>
    public class MainForm : Form
    {
        private MenuStrip _menuStrip = null!;
        private ToolStrip _toolStrip = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _statusLabel = null!;
        private ToolStripStatusLabel _fileStatusLabel = null!;
        private ToolStripStatusLabel _lineColStatusLabel = null!;

        private SplitContainer _mainSplitContainer = null!;
        private Panel _editorContainer = null!;
        private RichTextBox _codeEditor = null!;
        private Panel _lineNumbersPanel = null!;
        private TabControl _outputTabControl = null!;

        // تبويبات النتائج
        private DataGridView _tokensGrid = null!;
        private TreeView _astTreeView = null!;
        private DataGridView _symbolTableGrid = null!;
        private RichTextBox _tacTextBox = null!;
        private RichTextBox _asmTextBox = null!;
        private RichTextBox _cilTextBox = null!;
        private DataGridView _errorsGrid = null!;
        private TabPage _tabErrors = null!;
        private RichTextBox _consoleOutputTextBox = null!;
        private TextBox _inputTextBox = null!;

        // مؤقت التحليل اللحظي التلقائي
        private System.Windows.Forms.Timer _liveAnalysisTimer = null!;
        private string? _currentFilePath = null;
        private bool _isLiveAnalysisEnabled = true;
        private ToolStripComboBox _compilerEngineCombo = null!;

        public MainForm()
        {
            InitializeComponent();
            SetupLiveTimer();
            LoadDefaultArabicCode();
            UpdateLineNumbers();
            TriggerLiveAnalysis();
        }

        private void SetupLiveTimer()
        {
            _liveAnalysisTimer = new System.Windows.Forms.Timer
            {
                Interval = 350 // تأخير 350 ميلي ثانية بعد توقف الكتابة
            };
            _liveAnalysisTimer.Tick += (s, e) =>
            {
                _liveAnalysisTimer.Stop();
                if (_isLiveAnalysisEnabled)
                {
                    RunLiveAnalysis();
                }
            };
        }

        private void InitializeComponent()
        {
            this.Text = "بيئة تطوير ومحرر لغة البرمجة العربية - (Arabic Language IDE)";
            this.Size = new Size(1300, 850);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            
            // ضبط الاتجاه العام للنافذة: من اليمين لليسار (RTL)
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;

            // 1. شريط القوائم (Menu Bar)
            _menuStrip = new MenuStrip
            {
                RightToLeft = RightToLeft.Yes,
                Font = new Font("Segoe UI", 10F)
            };

            var fileMenu = new ToolStripMenuItem("ملف (File)");
            fileMenu.DropDownItems.Add("📄 ملف جديد (New)", null, (s, e) => NewFile());
            fileMenu.DropDownItems.Add("📂 فتح ملف... (Open)", null, (s, e) => OpenFile());
            fileMenu.DropDownItems.Add("💾 حفظ (Save)", null, (s, e) => SaveFile());
            fileMenu.DropDownItems.Add("💾 حفظ باسم... (Save As)", null, (s, e) => SaveFileAs());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("🚪 خروج (Exit)", null, (s, e) => Close());

            var editMenu = new ToolStripMenuItem("تحرير (Edit)");
            editMenu.DropDownItems.Add("↩️ تراجع (Undo)", null, (s, e) => _codeEditor.Undo());
            editMenu.DropDownItems.Add("↪️ إعادة (Redo)", null, (s, e) => _codeEditor.Redo());
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("✂️ قص (Cut)", null, (s, e) => _codeEditor.Cut());
            editMenu.DropDownItems.Add("📋 نسخ (Copy)", null, (s, e) => _codeEditor.Copy());
            editMenu.DropDownItems.Add("📥 لصق (Paste)", null, (s, e) => _codeEditor.Paste());
            editMenu.DropDownItems.Add("🔘 تحديد الكل (Select All)", null, (s, e) => _codeEditor.SelectAll());

            var buildMenu = new ToolStripMenuItem("ترجمة وتشغيل (Build & Run)");
            buildMenu.DropDownItems.Add("▶️ ترجمة وتشغيل الكود بالكامل (F5)", null, (s, e) => CompileAndRun());
            buildMenu.DropDownItems.Add("⚙️ فحص القواعد والتحليل اللحظي (F6)", null, (s, e) => RunLiveAnalysis());

            var examplesMenu = new ToolStripMenuItem("أمثلة اللغة (10 أمثلة معتمدة)");
            examplesMenu.DropDownOpening += (s, e) => PopulateExamplesMenu(examplesMenu);
            PopulateExamplesMenu(examplesMenu);

            var viewMenu = new ToolStripMenuItem("عرض (View)");
            viewMenu.DropDownItems.Add("🔄 تحديث الواجهة والتحليل الفوري", null, (s, e) => RefreshInterface());
            viewMenu.DropDownItems.Add("🔤 تبديل اتجاه المحرر (RTL/LTR)", null, (s, e) => ToggleEditorDirection());
            viewMenu.DropDownItems.Add("🧹 مسح كافة المخرجات", null, (s, e) => ClearOutputs());

            var helpMenu = new ToolStripMenuItem("مساعدة (Help)");
            helpMenu.DropDownItems.Add("ℹ️ عن المشروع والمترجم", null, (s, e) => ShowAboutDialog());

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, buildMenu, examplesMenu, viewMenu, helpMenu });

            // 2. شريط الأدوات السريع (Tool Strip)
            _toolStrip = new ToolStrip
            {
                RightToLeft = RightToLeft.Yes,
                ImageScalingSize = new Size(22, 22),
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(5)
            };

            var btnNew = new ToolStripButton("📄 جديد", null, (s, e) => NewFile());
            var btnOpen = new ToolStripButton("📂 فتح", null, (s, e) => OpenFile());
            var btnSave = new ToolStripButton("💾 حفظ", null, (s, e) => SaveFile());
            
            var btnRun = new ToolStripButton("▶️ ترجمة وتشغيل (F5)", null, (s, e) => CompileAndRun())
            {
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(8, 2, 8, 2)
            };

            var btnCompile = new ToolStripButton("⚙️ فحص وترجمة (F6)", null, (s, e) => RunLiveAnalysis())
            {
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(2, 2, 8, 2)
            };

            var btnRefresh = new ToolStripButton("🔄 تحديث الواجهة", null, (s, e) => RefreshInterface())
            {
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(2, 2, 8, 2)
            };

            var btnDirToggle = new ToolStripButton("🔤 تبديل الاتجاه", null, (s, e) => ToggleEditorDirection());
            var btnClear = new ToolStripButton("🧹 مسح المخرجات", null, (s, e) => ClearOutputs());

            var engineLabel = new ToolStripLabel("🛠️ المحرك:") { Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _compilerEngineCombo = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 170,
                Font = new Font("Segoe UI", 9.5F)
            };
            _compilerEngineCombo.Items.AddRange(new object[] { "🚀 مترجم ++Modern C", "⚡ مترجم C# (.NET 9)" });
            _compilerEngineCombo.SelectedIndex = 0; // C++ by default
            _compilerEngineCombo.SelectedIndexChanged += (s, e) =>
            {
                _statusLabel.Text = $"تم تحويل محرك الترجمة إلى: {_compilerEngineCombo.SelectedItem}";
            };

            _toolStrip.Items.AddRange(new ToolStripItem[]
            {
                btnNew, btnOpen, btnSave,
                new ToolStripSeparator(),
                btnRun, btnCompile, btnRefresh,
                new ToolStripSeparator(),
                engineLabel, _compilerEngineCombo,
                new ToolStripSeparator(),
                btnDirToggle, btnClear
            });

            // 3. شريط الحالة (Status Bar)
            _statusStrip = new StatusStrip { RightToLeft = RightToLeft.Yes };
            _statusLabel = new ToolStripStatusLabel("جاهز") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
            _lineColStatusLabel = new ToolStripStatusLabel("السطر: 1 | العمود: 1");
            _fileStatusLabel = new ToolStripStatusLabel("ملف جديد");
            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _lineColStatusLabel, _fileStatusLabel });

            // 4. الحاوية المقسمة الرئيسية (Split Container)
            _mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 420,
                RightToLeft = RightToLeft.Yes
            };

            // أ. حاوية المحرر مع أرقام الأسطر
            _editorContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            _codeEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 13F, FontStyle.Regular),
                RightToLeft = RightToLeft.Yes,
                AcceptsTab = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(235, 235, 235),
                BorderStyle = BorderStyle.None
            };

            _lineNumbersPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 50,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            _lineNumbersPanel.Paint += LineNumbersPanel_Paint;

            _codeEditor.VScroll += (s, e) => _lineNumbersPanel.Invalidate();
            _codeEditor.TextChanged += (s, e) =>
            {
                _fileStatusLabel.Text = "تم التعديل *";
                _lineNumbersPanel.Invalidate();
                TriggerLiveAnalysis(); // تشغيل المؤقت للتحليل اللحظي المباشر
            };
            _codeEditor.SelectionChanged += (s, e) => UpdateCaretPosition();

            _editorContainer.Controls.Add(_codeEditor);
            _editorContainer.Controls.Add(_lineNumbersPanel);
            _mainSplitContainer.Panel1.Controls.Add(_editorContainer);

            // ب. تبويبات النتائج (Tab Control)
            _outputTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular)
            };

            // 1. تبويب الشاشة التنفيذية
            var tabConsole = new TabPage("🖥️ شاشة التشغيل (Console Output)");
            _consoleOutputTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 15),
                ForeColor = Color.FromArgb(0, 255, 120),
                Font = new Font("Consolas", 11.5F, FontStyle.Bold),
                RightToLeft = RightToLeft.Yes,
                ReadOnly = true
            };

            // لوحة إدخال المستخدم للتعليمة اقرا (User Keyboard Input)
            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                BackColor = Color.FromArgb(28, 28, 30),
                Padding = new Padding(6, 6, 6, 6)
            };

            var btnSendInput = new Button
            {
                Dock = DockStyle.Left,
                Width = 150,
                Text = "📤 إدخال وتشغيل (Enter)",
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSendInput.FlatAppearance.BorderSize = 0;
            btnSendInput.Click += (s, e) => CompileAndRun();

            var lblInput = new Label
            {
                Dock = DockStyle.Right,
                Width = 180,
                Text = "⌨️ مدخلات البرنامج (اقرا):",
                ForeColor = Color.FromArgb(230, 230, 230),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };

            _inputTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11F, FontStyle.Regular),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "اكتب هنا الأرقام أو القيم للإدخال عند تنفيذ تعليمة اقرا (مثل: 50)... ثم اضغط Enter أو F5"
            };
            _inputTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CompileAndRun();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            inputPanel.Controls.Add(_inputTextBox);
            inputPanel.Controls.Add(btnSendInput);
            inputPanel.Controls.Add(lblInput);

            tabConsole.Controls.Add(_consoleOutputTextBox);
            tabConsole.Controls.Add(inputPanel);

            // 2. تبويب الرموز المعجمية
            var tabTokens = new TabPage("📑 الرموز المعجمية (Tokens)");
            _tokensGrid = CreateArabicDataGrid();
            _tokensGrid.Columns.Add("Num", "#");
            _tokensGrid.Columns.Add("Value", "قيمة الرمز (Token Value)");
            _tokensGrid.Columns.Add("Type", "النوع المعجمي (Token Type)");
            _tokensGrid.Columns.Add("Line", "رقم السطر");
            _tokensGrid.Columns[0].Width = 60;
            _tokensGrid.Columns[1].Width = 300;
            _tokensGrid.Columns[2].Width = 250;
            _tokensGrid.Columns[3].Width = 100;
            tabTokens.Controls.Add(_tokensGrid);

            // 3. تبويب شجرة الإعراب المجردة
            var tabAst = new TabPage("🌳 شجرة الإعراب (AST)");
            _astTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                BackColor = Color.FromArgb(250, 252, 255),
                ForeColor = Color.FromArgb(20, 20, 20),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };
            tabAst.Controls.Add(_astTreeView);

            // 4. تبويب جدول الرموز
            var tabSymbols = new TabPage("📋 جدول الرموز (Symbol Table)");
            _symbolTableGrid = CreateArabicDataGrid();
            _symbolTableGrid.Columns.Add("Name", "اسم الرمز");
            _symbolTableGrid.Columns.Add("DataType", "نوع البيانات");
            _symbolTableGrid.Columns.Add("Kind", "التصنيف");
            _symbolTableGrid.Columns.Add("Value", "القيمة");
            _symbolTableGrid.Columns.Add("DeclaredLine", "سطر التعريف");
            _symbolTableGrid.Columns.Add("Refs", "أسطر الاستخدام");
            tabSymbols.Controls.Add(_symbolTableGrid);

            // 5. تبويب الكود الوسيط (TAC) - اتجاه LTR
            var tabTac = new TabPage("⚙️ الكود الوسيط (TAC)");
            _tacTextBox = CreateTechnicalViewer();
            tabTac.Controls.Add(_tacTextBox);

            // 6. تبويب لغة التجميع x86 - اتجاه LTR
            var tabAsm = new TabPage("💻 لغة التجميع (x86 Assembly)");
            _asmTextBox = CreateTechnicalViewer();
            tabAsm.Controls.Add(_asmTextBox);

            // 7. تبويب .NET CIL - اتجاه LTR
            var tabCil = new TabPage("📜 .NET CIL");
            _cilTextBox = CreateTechnicalViewer();
            tabCil.Controls.Add(_cilTextBox);

            // 8. تبويب قائمة الأخطاء - RTL
            _tabErrors = new TabPage("⚠️ قائمة الأخطاء (Error List)");
            _errorsGrid = CreateArabicDataGrid();
            _errorsGrid.Columns.Add("Index", "#");
            _errorsGrid.Columns.Add("Line", "السطر");
            _errorsGrid.Columns.Add("Type", "نوع الخطأ");
            _errorsGrid.Columns.Add("Message", "نص وتفاصيل الخطأ وموقعه في الكود");
            _errorsGrid.Columns[0].Width = 50;
            _errorsGrid.Columns[1].Width = 80;
            _errorsGrid.Columns[2].Width = 180;
            _errorsGrid.Columns[3].Width = 800;
            
            // عند النقر على سطر الخطأ: الانتقال إلى السطر في المحرر وتظليله
            _errorsGrid.CellDoubleClick += (s, e) => NavigateToErrorLine();
            _errorsGrid.CellClick += (s, e) => NavigateToErrorLine();

            _tabErrors.Controls.Add(_errorsGrid);

            _outputTabControl.TabPages.AddRange(new TabPage[]
            {
                tabConsole, tabTokens, tabAst, tabSymbols, tabTac, tabAsm, tabCil, _tabErrors
            });

            _mainSplitContainer.Panel2.Controls.Add(_outputTabControl);

            // تجميع العناصر على النموذج
            this.Controls.Add(_mainSplitContainer);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_menuStrip);
            this.Controls.Add(_statusStrip);
            this.MainMenuStrip = _menuStrip;

            // اختصارات لوحة المفاتيح
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F5) { CompileAndRun(); e.Handled = true; }
                if (e.KeyCode == Keys.F6) { RunLiveAnalysis(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.S) { SaveFile(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.O) { OpenFile(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.N) { NewFile(); e.Handled = true; }
            };
        }

        private DataGridView CreateArabicDataGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RightToLeft = RightToLeft.Yes,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5F)
            };

            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 246);
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.EnableHeadersVisualStyles = false;

            return grid;
        }

        private RichTextBox CreateTechnicalViewer()
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10.5F, FontStyle.Regular),
                RightToLeft = RightToLeft.No,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(33, 37, 41),
                BorderStyle = BorderStyle.None
            };
        }

        private void LineNumbersPanel_Paint(object? sender, PaintEventArgs e)
        {
            int totalLines = Math.Max(1, _codeEditor.Lines.Length);
            int digits = Math.Max(3, totalLines.ToString().Length);
            int requiredWidth = 20 + digits * 10;
            if (_lineNumbersPanel.Width != requiredWidth)
            {
                _lineNumbersPanel.Width = requiredWidth;
            }

            int firstIndex = _codeEditor.GetCharIndexFromPosition(new Point(0, 0));
            int firstLine = _codeEditor.GetLineFromCharIndex(firstIndex);

            int lastIndex = _codeEditor.GetCharIndexFromPosition(new Point(0, _codeEditor.Height));
            int lastLine = _codeEditor.GetLineFromCharIndex(lastIndex);

            using var brush = new SolidBrush(Color.FromArgb(140, 140, 140));
            using var font = new Font("Consolas", 10F, FontStyle.Bold);

            for (int i = firstLine; i <= lastLine + 1; i++)
            {
                int charIndex = _codeEditor.GetFirstCharIndexFromLine(i);
                if (charIndex < 0 && i > 0) break;

                Point pos = _codeEditor.GetPositionFromCharIndex(charIndex >= 0 ? charIndex : 0);
                e.Graphics.DrawString((i + 1).ToString(), font, brush, 5, pos.Y + 2);
            }
        }

        private void UpdateLineNumbers()
        {
            _lineNumbersPanel.Invalidate();
        }

        private void UpdateCaretPosition()
        {
            int index = _codeEditor.SelectionStart;
            int line = _codeEditor.GetLineFromCharIndex(index) + 1;
            int col = index - _codeEditor.GetFirstCharIndexOfCurrentLine() + 1;
            _lineColStatusLabel.Text = $"السطر: {line} | العمود: {col}";
        }

        private void ToggleEditorDirection()
        {
            if (_codeEditor.RightToLeft == RightToLeft.Yes)
            {
                _codeEditor.RightToLeft = RightToLeft.No;
                _statusLabel.Text = "تم تغيير اتجاه المحرر إلى: يسار إلى يمين (LTR)";
            }
            else
            {
                _codeEditor.RightToLeft = RightToLeft.Yes;
                _statusLabel.Text = "تم تغيير اتجاه المحرر إلى: يمين إلى يسار (RTL)";
            }
        }

        private void TriggerLiveAnalysis()
        {
            _liveAnalysisTimer.Stop();
            _liveAnalysisTimer.Start();
        }

        /// <summary>
        /// التحليل اللحظي المباشر الذي يعمل فور كتابة أو تعديل الكود
        /// </summary>
        private void RunLiveAnalysis()
        {
            string code = _codeEditor.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                ClearOutputs();
                _statusLabel.Text = "المحرر فارغ";
                return;
            }

            try
            {
                // تنفيذ التحليل المباشر اللحظي
                var result = CompilerRunner.Compile(code, isVerbose: false);
                DisplayCompilationResult(result, isExecutionRun: false);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"خطأ في التحليل: {ex.Message}";
            }
        }

        private void LoadDefaultArabicCode()
        {
            _codeEditor.Text =
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
        }

        private void PopulateExamplesMenu(ToolStripMenuItem menu)
        {
            menu.DropDownItems.Clear();

            string appDir = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? AppDomain.CurrentDomain.BaseDirectory;

            string[] searchPaths = new[]
            {
                Path.Combine(appDir, "Examples"),
                Path.Combine(Directory.GetCurrentDirectory(), "Examples"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "Examples"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Examples"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Examples"),
                @"d:\IT FILES\level 4\مترجمات\نظري\مشروع\Examples",
                @"d:\IT FILES\level 4\مترجمات\نظري\مشروع\تسليم_المشروع_Final_Delivery\PREXE_SingleFile\Examples"
            };

            string? foundDir = null;
            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*.arb");
                    if (files.Length > 0)
                    {
                        foundDir = path;
                        break;
                    }
                }
            }

            if (foundDir != null)
            {
                var files = Directory.GetFiles(foundDir, "*.arb");
                Array.Sort(files);

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    menu.DropDownItems.Add(fileName, null, (s, e) =>
                    {
                        _isLiveAnalysisEnabled = false;
                        _codeEditor.Text = File.ReadAllText(file, Encoding.UTF8);
                        _currentFilePath = file;
                        _fileStatusLabel.Text = Path.GetFileName(file);
                        _statusLabel.Text = $"تم فتح المثال: {fileName}";
                        UpdateLineNumbers();
                        _isLiveAnalysisEnabled = true;
                        RunLiveAnalysis();
                    });
                }
            }
            else
            {
                menu.DropDownItems.Add("⚠️ تعذر العثور على المجلد، انقر هنا لفتح ملف", null, (s, e) => OpenFile());
            }
        }

        private void NewFile()
        {
            _isLiveAnalysisEnabled = false;
            _codeEditor.Clear();
            _currentFilePath = null;
            _fileStatusLabel.Text = "ملف جديد";
            _statusLabel.Text = "تم إنشاء ملف جديد";
            ClearOutputs();
            UpdateLineNumbers();
            _isLiveAnalysisEnabled = true;
        }

        private void OpenFile()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "ملفات لغة البرمجة العربية (*.arb;*.txt)|*.arb;*.txt|جميع الملفات (*.*)|*.*",
                Title = "فتح ملف كود عربي"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _isLiveAnalysisEnabled = false;
                _codeEditor.Text = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                _currentFilePath = ofd.FileName;
                _fileStatusLabel.Text = Path.GetFileName(ofd.FileName);
                _statusLabel.Text = $"تم فتح: {ofd.FileName}";
                UpdateLineNumbers();
                _isLiveAnalysisEnabled = true;
                RunLiveAnalysis();
            }
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveFileAs();
            }
            else
            {
                File.WriteAllText(_currentFilePath, _codeEditor.Text, Encoding.UTF8);
                _fileStatusLabel.Text = Path.GetFileName(_currentFilePath);
                _statusLabel.Text = "تم الحفظ بنجاح";
            }
        }

        private void SaveFileAs()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "ملفات لغة البرمجة العربية (*.arb)|*.arb|ملفات نصية (*.txt)|*.txt",
                Title = "حفظ الكود المصدري"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                _currentFilePath = sfd.FileName;
                SaveFile();
            }
        }

        private void ClearOutputs()
        {
            _tokensGrid.Rows.Clear();
            _astTreeView.Nodes.Clear();
            _symbolTableGrid.Rows.Clear();
            _tacTextBox.Clear();
            _asmTextBox.Clear();
            _cilTextBox.Clear();
            _errorsGrid.Rows.Clear();
            _consoleOutputTextBox.Clear();
        }

        private void CompileAndRun()
        {
            _statusLabel.Text = "جاري الترجمة والتشغيل عبر المترجم...";

            string tempSourceFile = Path.Combine(Path.GetTempPath(), $"source_{Guid.NewGuid():N}.arb");
            File.WriteAllText(tempSourceFile, _codeEditor.Text, Encoding.UTF8);

            string compilerExe = FindCompilerExe();
            CompilationResult? result = null;

            // 1. محاولة استدعاء المترجم الخارجي المنفصل CompilerProject.exe
            if (!string.IsNullOrEmpty(compilerExe))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = compilerExe,
                        Arguments = $"\"{tempSourceFile}\" --json",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        if (!string.IsNullOrEmpty(_inputTextBox.Text))
                        {
                            process.StandardInput.WriteLine(_inputTextBox.Text);
                        }
                        process.StandardInput.Close();

                        string jsonOutput = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(5000);

                        if (!string.IsNullOrWhiteSpace(jsonOutput) && jsonOutput.Contains("IsSuccess"))
                        {
                            result = JsonSerializer.Deserialize<CompilationResult>(jsonOutput);
                        }
                    }
                }
                catch
                {
                    // Fallback to internal engine if process call encountered an OS issue
                }
                finally
                {
                    try { if (File.Exists(tempSourceFile)) File.Delete(tempSourceFile); } catch { }
                }
            }

            // 2. استخدام المترجم المدمج مباشرة لضمان أعلى دقة وتزامن لحظي
            if (result == null)
            {
                result = CompilerRunner.Compile(_codeEditor.Text, isVerbose: false, userInput: _inputTextBox?.Text ?? "");
            }

            DisplayCompilationResult(result, isExecutionRun: true);
        }

        private void DisplayCompilationResult(CompilationResult result, bool isExecutionRun)
        {
            ClearOutputs();

            // 1. عرض الرموز المعجمية (Tokens)
            int tokenIdx = 1;
            foreach (var t in result.Tokens)
            {
                _tokensGrid.Rows.Add(tokenIdx++, t.Value, t.Type, t.Line);
            }

            // 2. عرض شجرة الإعراب (AST)
            if (result.AST != null)
            {
                _astTreeView.Nodes.Clear();
                var rootTreeNode = BuildTreeNodeFromDto(result.AST);
                _astTreeView.Nodes.Add(rootTreeNode);
                _astTreeView.ExpandAll();
            }

            // 3. عرض جدول الرموز (Symbol Table)
            foreach (var s in result.SymbolTable)
            {
                _symbolTableGrid.Rows.Add(s.Name, s.DataType, s.Kind, s.Value, s.DeclaredLine, s.ReferencedLines);
            }

            // 4. عرض الكود الوسيط (TAC)
            var tacSb = new StringBuilder();
            foreach (var line in result.TAC)
            {
                tacSb.AppendLine(line);
            }
            _tacTextBox.Text = tacSb.ToString();

            // 5. عرض لغة التجميع (x86 Assembly)
            _asmTextBox.Text = result.AssemblyCode;

            // 6. عرض .NET CIL
            _cilTextBox.Text = result.CILCode;

            // 7. عرض الأخطاء (Errors)
            bool hasErrors = false;
            var consoleSb = new StringBuilder();
            int errorNumber = 1;

            if (result.SyntaxErrors.Count > 0)
            {
                hasErrors = true;
                foreach (var err in result.SyntaxErrors)
                {
                    int line = ExtractLineNumber(err);
                    _errorsGrid.Rows.Add(errorNumber++, line > 0 ? line.ToString() : "-", "خطأ نحوي (Syntax)", err);
                    consoleSb.AppendLine($"⚠️ [خطأ نحوي]: {err}");
                }
            }

            if (result.SemanticErrors.Count > 0)
            {
                hasErrors = true;
                foreach (var err in result.SemanticErrors)
                {
                    int line = ExtractLineNumber(err);
                    _errorsGrid.Rows.Add(errorNumber++, line > 0 ? line.ToString() : "-", "خطأ دلالي (Semantic)", err);
                    consoleSb.AppendLine($"⚠️ [خطأ دلالي]: {err}");
                }
            }

            int totalErrors = result.SyntaxErrors.Count + result.SemanticErrors.Count;
            _tabErrors.Text = hasErrors ? $"⚠️ قائمة الأخطاء ({totalErrors})" : "⚠️ قائمة الأخطاء";

            // 8. التعامل مع شاشة التشغيل (Console Output)
            if (hasErrors)
            {
                var errorBanner = new StringBuilder();
                errorBanner.AppendLine("=======================================================================");
                errorBanner.AppendLine($"❌ فشلت الترجمة: تم اكتشاف ({totalErrors}) أخطاء في الكود المصدري:");
                errorBanner.AppendLine("=======================================================================");
                errorBanner.Append(consoleSb);
                errorBanner.AppendLine("=======================================================================");
                errorBanner.AppendLine("💡 تلميح: انقر على أي خطأ في قائمة الأخطاء للانتقال الفوري وتظليل السطر.");

                _consoleOutputTextBox.ForeColor = Color.FromArgb(255, 90, 90);
                _consoleOutputTextBox.Text = errorBanner.ToString();

                _statusLabel.Text = $"❌ تم اكتشاف ({totalErrors}) أخطاء في الكود (انظر تبويب قائمة الأخطاء)";

                if (isExecutionRun)
                {
                    _outputTabControl.SelectedTab = _tabErrors; // الانتقال التلقائي لتبويب قائمة الأخطاء
                }
            }
            else
            {
                _consoleOutputTextBox.ForeColor = Color.FromArgb(0, 255, 120);
                if (isExecutionRun)
                {
                    var runSb = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(_inputTextBox?.Text))
                    {
                        runSb.AppendLine($"[📥 مدخلات المستخدم المسندة لتعليمة اقرا: {_inputTextBox.Text.Trim()}]");
                        runSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
                    }
                    runSb.Append(string.IsNullOrEmpty(result.ExecutionOutput) 
                        ? "(اكتملت الترجمة بنجاح ولم ينتج البرنامج مخرجات طباعة)" 
                        : result.ExecutionOutput);

                    _consoleOutputTextBox.Text = runSb.ToString();

                    _statusLabel.Text = "✔️ تمت الترجمة بنجاح وتم تشغيل البرنامج التنفيذي";
                    _outputTabControl.SelectedTab = _outputTabControl.TabPages[0]; // شاشة التشغيل
                }
                else
                {
                    _statusLabel.Text = "✔️ الكود البرمجي سليم نحوياً ودلالياً 100%";
                }
            }
        }

        private void NavigateToErrorLine()
        {
            if (_errorsGrid.SelectedRows.Count == 0) return;

            var row = _errorsGrid.SelectedRows[0];
            string lineStr = row.Cells[1].Value?.ToString() ?? "";
            string msg = row.Cells[3].Value?.ToString() ?? "";

            int line = int.TryParse(lineStr, out int parsedLine) ? parsedLine : ExtractLineNumber(msg);
            if (line > 0 && line <= _codeEditor.Lines.Length)
            {
                int charIndex = _codeEditor.GetFirstCharIndexFromLine(line - 1);
                if (charIndex >= 0)
                {
                    _codeEditor.Focus();
                    _codeEditor.Select(charIndex, _codeEditor.Lines[line - 1].Length);
                    _codeEditor.ScrollToCaret();
                }
            }
        }

        private static int ExtractLineNumber(string text)
        {
            // البحث عن نمط "السطر X"
            int idx = text.IndexOf("السطر");
            if (idx >= 0)
            {
                var sb = new StringBuilder();
                for (int i = idx + 5; i < text.Length; i++)
                {
                    if (char.IsDigit(text[i])) sb.Append(text[i]);
                    else if (sb.Length > 0) break;
                }
                if (int.TryParse(sb.ToString(), out int line)) return line;
            }
            return -1;
        }

        private TreeNode BuildTreeNodeFromDto(NodeDto nodeDto)
        {
            string label = nodeDto.Value;
            if (!string.IsNullOrEmpty(nodeDto.Name)) label += $" [{nodeDto.Name}]";
            if (!string.IsNullOrEmpty(nodeDto.DataType)) label += $" ({nodeDto.DataType})";
            if (!string.IsNullOrEmpty(nodeDto.Val)) label += $" = {nodeDto.Val}";
            if (nodeDto.Line > 0) label += $" (السطر {nodeDto.Line})";

            var treeNode = new TreeNode(label);
            foreach (var child in nodeDto.Children)
            {
                treeNode.Nodes.Add(BuildTreeNodeFromDto(child));
            }
            return treeNode;
        }

        private void RefreshInterface()
        {
            _statusLabel.Text = "جاري تحديث الواجهة والتحليل اللحظي...";
            UpdateLineNumbers();
            _lineNumbersPanel.Invalidate();
            _codeEditor.Invalidate();
            RunLiveAnalysis();
            _statusLabel.Text = "تم تحديث الواجهة والتحليل اللحظي بنجاح ✔️";
        }

        private string FindCompilerExe()
        {
            bool isCpp = (_compilerEngineCombo == null || _compilerEngineCombo.SelectedIndex == 0);
            string targetExe = isCpp ? "CompilerProject_CPP.exe" : "CompilerProject.exe";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string currDir = Directory.GetCurrentDirectory();

            string[] possiblePaths = new[]
            {
                Path.Combine(baseDir, targetExe),
                Path.Combine(baseDir, "..", "..", "..", "..", isCpp ? "CompilerProject_CPP" : "CompilerProject\\bin\\Debug\\net9.0", targetExe),
                Path.Combine(baseDir, "..", "..", isCpp ? "CompilerProject_CPP" : "CompilerProject\\bin\\Debug\\net9.0", targetExe),
                Path.Combine(currDir, targetExe),
                Path.Combine(currDir, isCpp ? "CompilerProject_CPP" : "CompilerProject\\bin\\Debug\\net9.0", targetExe),
                $@"C:\Users\ZAINON\Desktop\المترجمات عملي\project_combilar\arabic-language-compiler\{(isCpp ? "CompilerProject_CPP" : "CompilerProject\\bin\\Debug\\net9.0")}\{targetExe}"
            };

            foreach (var p in possiblePaths)
            {
                try
                {
                    if (File.Exists(p)) return Path.GetFullPath(p);
                }
                catch { }
            }

            return "";
        }

        private void ShowAboutDialog()
        {
            MessageBox.Show(
                "مشروع مقرر بناء المترجمات (CS 351)\n" +
                "بيئة التطوير ومترجم لغة البرمجة العربية المتكامل\n\n" +
                "إشراف: أ/ عقيل الشرفي & د. خالد الكحصة\n" +
                "جامعة إب - كلية الحاسوب وتكنولوجيا المعلومات (2025/2026)\n\n" +
                "المميزات:\n" +
                "- تحديث وتحليل لحظي مباشر أثناء الكتابة (Live Analysis)\n" +
                "- محرر كود عربي بالكامل (RTL) مع شريط أرقام الأسطر\n" +
                "- مترجم منفصل بـ 6 مراحل للترجمة\n" +
                "- دعم 10 أمثلة اختبارية متنوعة\n" +
                "- كشف فوري للأخطاء النحوية والدلالية والانتقال التلقائي للسطر\n" +
                "- توليد كود تنفيذي حقيقي x86 و .NET",
                "عن المشروع",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
