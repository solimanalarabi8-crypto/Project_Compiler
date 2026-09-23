using System;
using System.Collections.Generic;
using CompilerProject.Models;

namespace CompilerProject.SyntaxAnalysis
{
    /// <summary>
    /// المحلل النحوي (Syntax Analyzer / Parser) للغة البرمجة العربية
    /// يطبق خوارزمية الإعراب التنازلي العودية (Recursive Descent Parsing)
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 1 - 6)
    /// </summary>
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public List<string> Errors { get; } = new List<string>();

        public Parser(List<Token> tokens)
        {
            _tokens = tokens ?? new List<Token>();
            _pos = 0;
        }

        private Token Current => _pos < _tokens.Count ? _tokens[_pos] : new Token("EOF", TokenType.EndOfFile, 0);
        private Token PeekNext() => _pos + 1 < _tokens.Count ? _tokens[_pos + 1] : new Token("EOF", TokenType.EndOfFile, 0);

        private Token Advance()
        {
            var token = Current;
            if (_pos < _tokens.Count) _pos++;
            return token;
        }

        private bool Match(string val)
        {
            if (Current.Value == val)
            {
                Advance();
                return true;
            }
            return false;
        }

        private Token Expect(string val, string errorMessage)
        {
            if (Current.Value == val)
            {
                return Advance();
            }
            throw new Exception($"خطأ نحوي في السطر {Current.Line}: {errorMessage}. وُجد '{Current.Value}' بدلاً من ذلك.");
        }

        private Token ExpectType(TokenType type, string errorMessage)
        {
            if (Current.Type == type)
            {
                return Advance();
            }
            throw new Exception($"خطأ نحوي في السطر {Current.Line}: {errorMessage}. وُجد '{Current.Value}' من نوع {Current.Type}.");
        }

        public void Synchronize()
        {
            while (Current.Type != TokenType.EndOfFile && Current.Value != "}" && Current.Value != ".")
            {
                if (Current.Value == "؛")
                {
                    Advance();
                    return;
                }

                if (Current.Value is "اذا" or "طالما" or "اعد" or "كرر" or "اطبع" or "اقرا" or "اقرأ" or "اقرء" or "متغير" or "ثابت" or "نوع" or "اجراء")
                {
                    return;
                }

                Advance();
            }
        }

        /// <summary>
        /// القاعدة الأولى: <برنامج> := برنامج <اسم_برنامج> ؛ <كتلة_برمجية> .
        /// </summary>
        public Node ParseProgram()
        {
            int line = Current.Line;
            Expect("برنامج", "يجب أن يبدأ البرنامج بالكلمة المحجوزة 'برنامج'");
            
            var progNameToken = ExpectType(TokenType.Identifier, "يجب تحديد اسم للبرنامج بعد كلمة 'برنامج'");
            Expect("؛", "يجب وضع فاصلة منقوطة '؛' بعد اسم البرنامج");

            var rootNode = new Node("ProgramRoot", progNameToken.Value, line);

            // إعراب الكتلة البرمجية
            var blockNode = ParseBlock();
            rootNode.AddChild(blockNode);

            // يجب أن ينتهي البرنامج بنقطة .
            if (Current.Value == ".")
            {
                Advance();
            }
            else
            {
                Errors.Add($"خطأ نحوي في السطر {Current.Line}: يجب أن ينتهي البرنامج بنقطة '.'");
            }

            if (Errors.Count > 0 && rootNode.Children.Count == 0)
            {
                throw new Exception(Errors[0]);
            }

            return rootNode;
        }

        /// <summary>
        /// القاعدة الثانية: <كتلة_برمجية> := [<جزء_التعريفات>] <قائمة_التعليمات>
        /// </summary>
        private Node ParseBlock()
        {
            var blockNode = new Node("Block", Current.Line);

            // 1. جزء التعريفات (اختياري)
            if (IsDeclarationStart())
            {
                var declsNode = ParseDeclarations();
                blockNode.AddChild(declsNode);
            }

            // 2. قائمة التعليمات
            var stmtListNode = ParseStatementList();
            blockNode.AddChild(stmtListNode);

            return blockNode;
        }

        private bool IsDeclarationStart()
        {
            return Current.Value is "ثابت" or "نوع" or "متغير" or "اجراء";
        }

        /// <summary>
        /// <جزء_التعريفات> := [<تعريف_الثوابت>] [<تعريف_الانواع>] [<تعريف_المتغيرات>] [<تعريف_الاجراءات>]
        /// </summary>
        private Node ParseDeclarations()
        {
            var declsNode = new Node("Declarations", Current.Line);

            // أ. تعريف الثوابت
            while (Current.Value == "ثابت")
            {
                try
                {
                    Advance();
                    var constsNode = new Node("ConstDeclarations", Current.Line);
                    while (Current.Type == TokenType.Identifier)
                    {
                        var constName = Advance();
                        Expect("=", "يجب وضع علامة '=' بعد اسم الثابت");
                        
                        string sign = "";
                        if (Current.Value is "+" or "-") sign = Advance().Value;

                        var constVal = Advance();
                        Expect("؛", "يجب إنهاء تعريف الثابت بفاصلة منقوطة '؛'");

                        var cNode = new Node("ConstDecl", constName.Value, sign + constVal.Value, constName.Line);
                        cNode.Val = sign + constVal.Value;
                        constsNode.AddChild(cNode);
                    }
                    declsNode.AddChild(constsNode);
                }
                catch (Exception ex)
                {
                    if (!Errors.Contains(ex.Message)) Errors.Add(ex.Message);
                    Synchronize();
                }
            }

            // ب. تعريف الأنواع (قوائم وسجلات)
            while (Current.Value == "نوع")
            {
                try
                {
                    Advance();
                    var typesNode = new Node("TypeDeclarations", Current.Line);
                    while (Current.Type == TokenType.Identifier)
                    {
                        var typeName = Advance();
                        Expect("=", "يجب وضع علامة '=' بعد اسم النوع");

                        if (Current.Value == "قائمة")
                        {
                            Advance();
                            Expect("[", "يجب فتح قوس مربع '[' لحجم القائمة");
                            var sizeToken = ExpectType(TokenType.Number, "يجب تحديد حجم القائمة كرقم");
                            Expect("]", "يجب إغلاق القوس المربع ']'");
                            Expect("من", "يجب كتابة كلمة 'من' لتحديد نوع عناصر القائمة");
                            var elemType = Advance();
                            Expect("؛", "يجب إنهاء تعريف النوع بفاصلة منقوطة '؛'");

                            var arrayNode = new Node("TypeDecl_Array", typeName.Value, elemType.Value, typeName.Line);
                            arrayNode.Val = sizeToken.Value;
                            typesNode.AddChild(arrayNode);
                        }
                        else if (Current.Value == "سجل")
                        {
                            Advance();
                            Expect("{", "يجب فتح قوس مجموعة '{' لحقول السجل");
                            var recordNode = new Node("TypeDecl_Record", typeName.Value, typeName.Line);

                            while (Current.Type == TokenType.Identifier)
                            {
                                var fieldNames = new List<string> { Advance().Value };
                                while (Match(","))
                                {
                                    fieldNames.Add(ExpectType(TokenType.Identifier, "يجب كتابة اسم الحقل").Value);
                                }
                                Expect(":", "يجب وضع نقطتين ':' بعد أسماء الحقول");
                                var fieldType = Advance().Value;

                                foreach (var fName in fieldNames)
                                {
                                    recordNode.AddChild(new Node("FieldDecl", fName, fieldType, typeName.Line));
                                }

                                if (Current.Value == "؛") Advance();
                            }
                            Expect("}", "يجب إغلاق قوس المجموعة '}' للسجل");
                            Expect("؛", "يجب وضع فاصلة منقوطة '؛' بعد تعريف السجل");
                            typesNode.AddChild(recordNode);
                        }
                    }
                    declsNode.AddChild(typesNode);
                }
                catch (Exception ex)
                {
                    if (!Errors.Contains(ex.Message)) Errors.Add(ex.Message);
                    Synchronize();
                }
            }

            // ج. تعريف المتغيرات
            while (Current.Value == "متغير")
            {
                try
                {
                    Advance();
                    var varsNode = new Node("VarDeclarations", Current.Line);
                    while (Current.Type == TokenType.Identifier)
                    {
                        var varNames = new List<Token> { Advance() };
                        while (Match(","))
                        {
                            varNames.Add(ExpectType(TokenType.Identifier, "يجب كتابة اسم المتغير بعد الفاصلة"));
                        }

                        Expect(":", "يجب وضع نقطتين ':' بعد أسماء المتغيرات");
                        var typeToken = Advance();
                        Expect("؛", "يجب إنهاء تعريف المتغير بفاصلة منقوطة '؛'");

                        foreach (var v in varNames)
                        {
                            varsNode.AddChild(new Node("VarDecl", v.Value, typeToken.Value, v.Line));
                        }
                    }
                    declsNode.AddChild(varsNode);
                }
                catch (Exception ex)
                {
                    if (!Errors.Contains(ex.Message)) Errors.Add(ex.Message);
                    Synchronize();
                }
            }

            // د. تعريف الإجراءات
            while (Current.Value == "اجراء")
            {
                Advance();
                var procName = ExpectType(TokenType.Identifier, "يجب كتابة اسم الإجراء بعد كلمة 'اجراء'");
                Expect("(", "يجب فتح قوس '(' لمعلمات الإجراء");

                var procNode = new Node("ProcDecl", procName.Value, procName.Line);
                var paramsNode = new Node("FormalParams", procName.Line);

                if (Current.Value != ")")
                {
                    do
                    {
                        string passingMode = "بالقيمة";
                        if (Current.Value is "بالقيمة" or "بالمرجع")
                        {
                            passingMode = Advance().Value;
                        }

                        var paramNames = new List<string> { ExpectType(TokenType.Identifier, "اسم المعلمة").Value };
                        while (Match(","))
                        {
                            paramNames.Add(ExpectType(TokenType.Identifier, "اسم المعلمة").Value);
                        }
                        Expect(":", "نقطتين بعد المعلمات");
                        var pType = Advance().Value;

                        foreach (var pn in paramNames)
                        {
                            var pNode = new Node("Param", pn, pType, procName.Line);
                            pNode.Val = passingMode;
                            paramsNode.AddChild(pNode);
                        }
                    } while (Match("؛"));
                }
                Expect(")", "يجب إغلاق قوس المعلمات ')'");
                Expect("؛", "يجب وضع فاصلة منقوطة '؛' بعد رأس الإجراء");

                procNode.AddChild(paramsNode);
                procNode.AddChild(ParseBlock());
                Expect("؛", "يجب إنهاء كتلة الإجراء بفاصلة منقوطة '؛'");

                declsNode.AddChild(procNode);
            }

            return declsNode;
        }

        /// <summary>
        /// <قائمة_تعليمات> := { <تعليمة> (؛ <تعليمة>)* }
        /// </summary>
        private Node ParseStatementList()
        {
            Expect("{", "يجب فتح قوس مجموعة '{' لبدء قائمة التعليمات");
            var stmtListNode = new Node("StatementList", Current.Line);

            while (Current.Value != "}" && Current.Type != TokenType.EndOfFile)
            {
                try
                {
                    var stmt = ParseStatement();
                    if (stmt != null)
                    {
                        stmtListNode.AddChild(stmt);
                    }
                }
                catch (Exception ex)
                {
                    if (!Errors.Contains(ex.Message))
                    {
                        Errors.Add(ex.Message);
                    }
                    Synchronize();
                }

                // الفاصلة المنقوطة تفصل بين التعليمات
                while (Current.Value == "؛")
                {
                    Advance();
                }
            }

            Expect("}", "يجب إغلاق قوس المجموعة '}' لقائمة التعليمات");
            return stmtListNode;
        }

        /// <summary>
        /// إعراب تعليمة واحدة
        /// </summary>
        private Node? ParseStatement()
        {
            if (Current.Value == "}") return null;

            // 1. جملة الإدخال: اقرا ( س ) أو اقرا س
            if (Current.Value is "اقرا" or "اقرأ" or "اقرء")
            {
                int line = Current.Line;
                string kw = Advance().Value;
                bool hasParen = Match("(");
                var varNode = ParseAccessVariable();
                if (hasParen)
                {
                    Expect(")", $"يجب إغلاق القوس ')' بعد متغير الإدخال لتعليمة '{kw}'");
                }

                var readNode = new Node("ReadStatement", line);
                readNode.AddChild(varNode);
                return readNode;
            }

            // 2. جملة الإخراج: اطبع ( عنصر , عنصر )
            if (Current.Value == "اطبع")
            {
                int line = Current.Line;
                Advance();
                Expect("(", "يجب فتح قوس '(' بعد كلمة 'اطبع'");
                var printNode = new Node("PrintStatement", line);

                do
                {
                    if (Current.Type == TokenType.String)
                    {
                        var strToken = Advance();
                        var strNode = new Node("StringLiteral", line);
                        strNode.Val = strToken.Value;
                        printNode.AddChild(strNode);
                    }
                    else
                    {
                        printNode.AddChild(ParseExpression());
                    }
                } while (Match(","));

                Expect(")", "يجب إغلاق القوس ')' بعد جملة الإخراج");
                return printNode;
            }

            // 3. جملة الشرط: اذا ( شرط ) فان تعليمة [ والا تعليمة ]
            if (Current.Value == "اذا")
            {
                return ParseIfStatement();
            }

            // 4. حلقة طالما: طالما ( شرط ) استمر تعليمة
            if (Current.Value == "طالما")
            {
                int line = Current.Line;
                Advance();
                Expect("(", "يجب فتح قوس '(' لشرط طالما");
                var condNode = ParseExpression();
                Expect(")", "يجب إغلاق قوس شرط طالما ')'");
                Expect("استمر", "يجب كتابة كلمة 'استمر' بعد شرط طالما");
                var bodyNode = ParseStatementOrBlock();

                var whileNode = new Node("WhileStatement", line);
                whileNode.AddChild(condNode);
                whileNode.AddChild(bodyNode);
                return whileNode;
            }

            // 5. حلقة أعد حتى: اعد تعليمة حتى ( شرط )
            if (Current.Value == "اعد")
            {
                int line = Current.Line;
                Advance();
                var bodyNode = ParseStatementOrBlock();
                Expect("حتى", "يجب كتابة كلمة 'حتى' بعد تعليمة اعد");
                Expect("(", "يجب فتح قوس '(' لشرط حتى");
                var condNode = ParseExpression();
                Expect(")", "يجب إغلاق قوس شرط حتى ')'");

                var repeatNode = new Node("RepeatUntilStatement", line);
                repeatNode.AddChild(bodyNode);
                repeatNode.AddChild(condNode);
                return repeatNode;
            }

            // 6. حلقة كرر: كرر ( س = 1 الى 10 اضف 1 ) تعليمة
            if (Current.Value == "كرر")
            {
                int line = Current.Line;
                Advance();
                Expect("(", "يجب فتح قوس '(' لحلقة كرر");
                var loopVar = ExpectType(TokenType.Identifier, "اسم متغير العداد في حلقة كرر");
                Expect("=", "يجب وضع علامة '=' لتحديد القيمة الابتدائية");
                var startExpr = ParseExpression();
                Expect("الى", "يجب كتابة كلمة 'الى' لتحديد القيمة النهائية للعداد");
                var endExpr = ParseExpression();

                Node? stepExpr = null;
                if (Current.Value == "اضف")
                {
                    Advance();
                    stepExpr = ParseExpression();
                }

                Expect(")", "يجب إغلاق قوس حلقة كرر ')'");
                var bodyNode = ParseStatementOrBlock();

                var forNode = new Node("ForStatement", loopVar.Value, line);
                forNode.AddChild(startExpr);
                forNode.AddChild(endExpr);
                if (stepExpr != null) forNode.AddChild(stepExpr);
                forNode.AddChild(bodyNode);
                return forNode;
            }

            // 7. كتلة متداخلة { ... }
            if (Current.Value == "{")
            {
                return ParseStatementList();
            }

            // 8. جملة الإسناد أو استدعاء إجراء
            if (Current.Type == TokenType.Identifier)
            {
                int line = Current.Line;
                var varAccess = ParseAccessVariable();

                // إذا تلتها علامة = فهي جملة إسناد
                if (Match("="))
                {
                    var expr = ParseExpression();
                    var assignNode = new Node("Assign", varAccess.Name, line);
                    assignNode.AddChild(varAccess);
                    assignNode.AddChild(expr);
                    return assignNode;
                }

                // إذا تلاها قوس ( فهو استدعاء إجراء
                if (Match("("))
                {
                    var callNode = new Node("CallStatement", varAccess.Name, line);
                    if (Current.Value != ")")
                    {
                        do
                        {
                            callNode.AddChild(ParseExpression());
                        } while (Match(","));
                    }
                    Expect(")", "يجب إغلاق قوس استدعاء الإجراء ')'");
                    return callNode;
                }

                // إذا لم تكن إسناداً ولا استدعاء، فهو خطأ نحوي قطعي
                throw new Exception($"خطأ نحوي في السطر {line}: تعليمة غير مكتملة بعد المعرف '{varAccess.Name}'، يجب وضع علامة '=' للإسناد أو '(' للاستدعاء أو إنهاء التعليمة بـ '؛'");
            }

            throw new Exception($"خطأ نحوي في السطر {Current.Line}: رمز غير متوقع '{Current.Value}' في قائمة التعليمات");
        }

        private Node ParseStatementOrBlock()
        {
            if (Current.Value == "{")
            {
                return ParseStatementList();
            }
            var singleStmt = ParseStatement();
            var listNode = new Node("StatementList", Current.Line);
            if (singleStmt != null) listNode.AddChild(singleStmt);
            return listNode;
        }

        private Node ParseIfStatement()
        {
            int line = Current.Line;
            Advance(); // تخطي 'اذا'
            Expect("(", "يجب فتح قوس '(' لشرط اذا");
            var condNode = ParseExpression();
            Expect(")", "يجب إغلاق قوس شرط اذا ')'");
            Expect("فان", "يجب كتابة كلمة 'فان' بعد شرط اذا");

            var thenBody = ParseStatementOrBlock();
            var ifNode = new Node("IfStatement", line);
            ifNode.AddChild(condNode);
            ifNode.AddChild(thenBody);

            // فحص والا اذا أو والا
            if (Current.Value == "؛" && PeekNext().Value == "والا")
            {
                Advance(); // تخطي ؛
            }

            if (Match("والا"))
            {
                if (Current.Value == "اذا")
                {
                    // والا اذا متداخلة
                    var elseIfNode = ParseIfStatement();
                    ifNode.AddChild(elseIfNode);
                }
                else
                {
                    // والا عادية
                    var elseBody = ParseStatementOrBlock();
                    var elseNode = new Node("ElseBlock", line);
                    elseNode.AddChild(elseBody);
                    ifNode.AddChild(elseNode);
                }
            }

            return ifNode;
        }

        /// <summary>
        /// متغير وصول: س أو س[1] أو سجل.حقل
        /// </summary>
        private Node ParseAccessVariable()
        {
            var idToken = ExpectType(TokenType.Identifier, "يجب تحديد اسم المتغير");
            var varNode = new Node("Variable", idToken.Value, idToken.Line);

            while (Current.Value is "[" or ".")
            {
                if (Match("["))
                {
                    var idxExpr = ParseExpression();
                    Expect("]", "إغلاق القوس المربع ']' للمصفوفة");
                    var indexNode = new Node("IndexedAccess", varNode.Name, idToken.Line);
                    indexNode.AddChild(varNode);
                    indexNode.AddChild(idxExpr);
                    varNode = indexNode;
                }
                else if (Match("."))
                {
                    var fieldToken = ExpectType(TokenType.Identifier, "اسم الحقل بعد النقطة '.'");
                    var fieldNode = new Node("FieldAccess", fieldToken.Value, idToken.Line);
                    fieldNode.AddChild(varNode);
                    varNode = fieldNode;
                }
            }

            return varNode;
        }

        // ==========================================
        // قواعد التعابير الحسابية والمنطقية (ص 4 - 5)
        // ==========================================

        /// <summary>
        /// <تعبير> := <تعبير_بسيط> [<معامل_ربط> <تعبير_بسيط>]
        /// </summary>
        public Node ParseExpression()
        {
            var left = ParseSimpleExpression();

            if (IsRelationalOperator(Current.Value))
            {
                var opToken = Advance();
                var right = ParseSimpleExpression();

                var relNode = new Node("BinaryExpr", opToken.Line);
                relNode.Val = opToken.Value;
                relNode.AddChild(left);
                relNode.AddChild(right);
                return relNode;
            }

            return left;
        }

        /// <summary>
        /// <تعبير_بسيط> := [<معامل_اشارة>] <حد> (<معامل_جمع> <حد>)*
        /// </summary>
        private Node ParseSimpleExpression()
        {
            string unaryOp = "";
            int line = Current.Line;
            if (Current.Value is "+" or "-")
            {
                unaryOp = Advance().Value;
            }

            var left = ParseTerm();
            if (!string.IsNullOrEmpty(unaryOp))
            {
                var uNode = new Node("UnaryExpr", line);
                uNode.Val = unaryOp;
                uNode.AddChild(left);
                left = uNode;
            }

            while (Current.Value is "+" or "-" or "||")
            {
                var opToken = Advance();
                var right = ParseTerm();

                var binNode = new Node("BinaryExpr", opToken.Line);
                binNode.Val = opToken.Value;
                binNode.AddChild(left);
                binNode.AddChild(right);
                left = binNode;
            }

            return left;
        }

        /// <summary>
        /// <حد> := <عامل> (<معامل_ضرب> <عامل>)*
        /// </summary>
        private Node ParseTerm()
        {
            var left = ParseFactor();

            while (Current.Value is "^" or "*" or "/" or "\\" or "%" or "&&")
            {
                var opToken = Advance();
                var right = ParseFactor();

                var binNode = new Node("BinaryExpr", opToken.Line);
                binNode.Val = opToken.Value;
                binNode.AddChild(left);
                binNode.AddChild(right);
                left = binNode;
            }

            return left;
        }

        /// <summary>
        /// <عامل> := <قيمة_ثابت> | <متغير_وصول> | ( <تعبير> ) | ! <عامل>
        /// </summary>
        private Node ParseFactor()
        {
            int line = Current.Line;

            // نفي منطقي !
            if (Match("!"))
            {
                var subFactor = ParseFactor();
                var notNode = new Node("UnaryExpr", line);
                notNode.Val = "!";
                notNode.AddChild(subFactor);
                return notNode;
            }

            // تعبير محصور بين أقواس ( تعبير )
            if (Match("("))
            {
                var expr = ParseExpression();
                Expect(")", "يجب إغلاق القوس ')'");
                return expr;
            }

            // عدد
            if (Current.Type == TokenType.Number)
            {
                var numToken = Advance();
                var numNode = new Node("Number", line);
                numNode.Val = numToken.Value;
                return numNode;
            }

            // نص
            if (Current.Type == TokenType.String)
            {
                var strToken = Advance();
                var strNode = new Node("StringLiteral", line);
                strNode.Val = strToken.Value;
                return strNode;
            }

            // حرف
            if (Current.Type == TokenType.Char)
            {
                var charToken = Advance();
                var charNode = new Node("CharLiteral", line);
                charNode.Val = charToken.Value;
                return charNode;
            }

            // قيمة منطقية (صح / خطأ)
            if (Current.Value is "صح" or "خطأ")
            {
                var boolToken = Advance();
                var boolNode = new Node("BooleanLiteral", line);
                boolNode.Val = boolToken.Value;
                return boolNode;
            }

            // متغير وصول
            if (Current.Type == TokenType.Identifier)
            {
                return ParseAccessVariable();
            }

            throw new Exception($"خطأ نحوي في السطر {line}: تعبير غير صالح عند الرمز '{Current.Value}'");
        }

        private static bool IsRelationalOperator(string op)
        {
            return op is "==" or "!=" or "<" or ">" or "<=" or ">=";
        }
    }
}
