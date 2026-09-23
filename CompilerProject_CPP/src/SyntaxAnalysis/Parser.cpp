#include "../../include/SyntaxAnalysis/Parser.h"

namespace CompilerCPP {

    static const Token EOF_TOKEN("EOF", TokenType::EndOfFile, 0);

    Parser::Parser(std::vector<Token> tokens)
        : _tokens(std::move(tokens)), _pos(0) {}

    const Token& Parser::Current() const {
        return (_pos < _tokens.size()) ? _tokens[_pos] : EOF_TOKEN;
    }

    const Token& Parser::Peek(size_t offset) const {
        return (_pos + offset < _tokens.size()) ? _tokens[_pos + offset] : EOF_TOKEN;
    }

    bool Parser::IsAtEnd() const {
        return _pos >= _tokens.size() || Current().Type == TokenType::EndOfFile;
    }

    Token Parser::Advance() {
        if (!IsAtEnd()) {
            return _tokens[_pos++];
        }
        return Current();
    }

    Token Parser::Expect(const std::string& expectedValue, const std::string& errorMessage) {
        if (Current().Value == expectedValue) {
            return Advance();
        }
        throw std::runtime_error("خطأ نحوي في السطر " + std::to_string(Current().Line) + ": " + errorMessage + ". وُجد '" + Current().Value + "' بدلاً من ذلك.");
    }

    Token Parser::ExpectType(TokenType expectedType, const std::string& errorMessage) {
        if (Current().Type == expectedType) {
            return Advance();
        }
        throw std::runtime_error("خطأ نحوي في السطر " + std::to_string(Current().Line) + ": " + errorMessage + ". وُجد '" + Current().Value + "' من نوع " + TokenTypeToString(Current().Type) + ".");
    }

    bool Parser::Match(const std::string& val) {
        if (Current().Value == val) {
            Advance();
            return true;
        }
        return false;
    }

    bool Parser::MatchType(TokenType type) {
        if (Current().Type == type) {
            Advance();
            return true;
        }
        return false;
    }

    void Parser::Synchronize() {
        while (!IsAtEnd() && Current().Value != "}" && Current().Value != ".") {
            if (Current().Value == "؛") {
                Advance();
                return;
            }

            if (Current().Value == "اذا" || Current().Value == "طالما" || Current().Value == "اعد" || Current().Value == "كرر" ||
                Current().Value == "اطبع" || Current().Value == "اقرا" || Current().Value == "اقرأ" || Current().Value == "اقرء" || Current().Value == "متغير" || Current().Value == "ثابت" || Current().Value == "نوع") {
                return;
            }

            Advance();
        }
    }

    std::shared_ptr<Node> Parser::ParseProgram() {
        Expect("برنامج", "يجب أن يبدأ البرنامج بالكلمة المحجوزة 'برنامج'");
        auto progName = ExpectType(TokenType::Identifier, "يجب تحديد اسم البرنامج كمعرف بعد كلمة برنامج");
        Expect("؛", "يجب وضع فاصلة منقوطة '؛' بعد اسم البرنامج");

        auto progRoot = std::make_shared<Node>("ProgramRoot", progName.Value, progName.Line);
        progRoot->AddChild(ParseBlock());
        
        if (Current().Value == ".") {
            Advance();
        } else {
            Errors.push_back("خطأ نحوي في السطر " + std::to_string(Current().Line) + ": يجب إنهاء البرنامج بنقطة '.'");
        }

        if (!Errors.empty() && progRoot->Children.empty()) {
            throw std::runtime_error(Errors[0]);
        }

        return progRoot;
    }

    std::shared_ptr<Node> Parser::ParseBlock() {
        auto blockNode = std::make_shared<Node>("Block", Current().Line);
        blockNode->AddChild(ParseDeclarations());

        Expect("{", "يجب فتح قوس مجموعة '{' لبدء قائمة التعليمات");
        blockNode->AddChild(ParseStatementList());
        Expect("}", "يجب إغلاق قوس المجموعة '}' لإنهاء الكتلة");
        return blockNode;
    }

    std::shared_ptr<Node> Parser::ParseDeclarations() {
        auto declsNode = std::make_shared<Node>("Declarations", Current().Line);

        // أ. تعريف الثوابت
        while (Current().Value == "ثابت") {
            try {
                Advance();
                auto constsNode = std::make_shared<Node>("ConstDeclarations", Current().Line);
                while (Current().Type == TokenType::Identifier) {
                    auto constName = Advance();
                    Expect("=", "يجب وضع علامة '=' بعد اسم الثابت");
                    
                    std::string sign = "";
                    if (Current().Value == "+" || Current().Value == "-") {
                        sign = Advance().Value;
                    }

                    auto constVal = Advance();
                    Expect("؛", "يجب إنهاء تعريف الثابت بفاصلة منقوطة '؛'");

                    auto cNode = std::make_shared<Node>("ConstDecl", constName.Value, sign + constVal.Value, constName.Line);
                    cNode->Val = sign + constVal.Value;
                    constsNode->AddChild(cNode);
                }
                declsNode->AddChild(constsNode);
            } catch (const std::exception& ex) {
                std::string msg = ex.what();
                bool exists = false;
                for (const auto& e : Errors) if (e == msg) { exists = true; break; }
                if (!exists) Errors.push_back(msg);
                Synchronize();
            }
        }

        // ب. تعريف الأنواع
        while (Current().Value == "نوع") {
            try {
                Advance();
                auto typesNode = std::make_shared<Node>("TypeDeclarations", Current().Line);
                while (Current().Type == TokenType::Identifier) {
                    auto typeName = Advance();
                    Expect("=", "يجب وضع علامة '=' بعد اسم النوع");

                    if (Current().Value == "قائمة") {
                        Advance();
                        Expect("[", "يجب فتح قوس مربع '[' لحجم القائمة");
                        auto sizeToken = ExpectType(TokenType::Number, "يجب تحديد حجم القائمة كرقم");
                        Expect("]", "يجب إغلاق القوس المربع ']'");
                        Expect("من", "يجب كتابة كلمة 'من' لتحديد نوع عناصر القائمة");
                        auto elemType = Advance();
                        Expect("؛", "يجب إنهاء تعريف النوع بفاصلة منقوطة '؛'");

                        auto arrayNode = std::make_shared<Node>("TypeDecl_Array", typeName.Value, elemType.Value, typeName.Line);
                        arrayNode->Val = sizeToken.Value;
                        typesNode->AddChild(arrayNode);
                    } else if (Current().Value == "سجل") {
                        Advance();
                        Expect("{", "يجب فتح قوس مجموعة '{' لحقول السجل");
                        auto recordNode = std::make_shared<Node>("TypeDecl_Record", typeName.Value, typeName.Line);

                        while (Current().Type == TokenType::Identifier) {
                            std::vector<std::string> fieldNames;
                            fieldNames.push_back(Advance().Value);
                            while (Match(",")) {
                                fieldNames.push_back(ExpectType(TokenType::Identifier, "يجب كتابة اسم الحقل").Value);
                            }
                            Expect(":", "يجب وضع نقطتين ':' بعد أسماء الحقول");
                            auto fieldType = Advance().Value;

                            for (const auto& fName : fieldNames) {
                                recordNode->AddChild(std::make_shared<Node>("FieldDecl", fName, fieldType, typeName.Line));
                            }

                            if (Current().Value == "؛") Advance();
                        }
                        Expect("}", "يجب إغلاق قوس المجموعة '}' للسجل");
                        Expect("؛", "يجب وضع فاصلة منقوطة '؛' بعد تعريف السجل");
                        typesNode->AddChild(recordNode);
                    }
                }
                declsNode->AddChild(typesNode);
            } catch (const std::exception& ex) {
                std::string msg = ex.what();
                bool exists = false;
                for (const auto& e : Errors) if (e == msg) { exists = true; break; }
                if (!exists) Errors.push_back(msg);
                Synchronize();
            }
        }

        // ج. تعريف المتغيرات
        while (Current().Value == "متغير") {
            try {
                Advance();
                auto varsNode = std::make_shared<Node>("VarDeclarations", Current().Line);
                while (Current().Type == TokenType::Identifier) {
                    std::vector<Token> varNames;
                    varNames.push_back(Advance());
                    while (Match(",")) {
                        varNames.push_back(ExpectType(TokenType::Identifier, "يجب كتابة اسم المتغير بعد الفاصلة"));
                    }

                    Expect(":", "يجب وضع نقطتين ':' بعد أسماء المتغيرات");
                    auto typeToken = Advance();
                    Expect("؛", "يجب إنهاء تعريف المتغير بفاصلة منقوطة '؛'");

                    for (const auto& v : varNames) {
                        varsNode->AddChild(std::make_shared<Node>("VarDecl", v.Value, typeToken.Value, v.Line));
                    }
                }
                declsNode->AddChild(varsNode);
            } catch (const std::exception& ex) {
                std::string msg = ex.what();
                bool exists = false;
                for (const auto& e : Errors) if (e == msg) { exists = true; break; }
                if (!exists) Errors.push_back(msg);
                Synchronize();
            }
        }

        // د. تعريف الإجراءات
        while (Current().Value == "اجراء") {
            try {
                Advance();
                auto procName = ExpectType(TokenType::Identifier, "يجب تحديد اسم الإجراء كمعرف");
                auto procNode = std::make_shared<Node>("ProcedureDecl", procName.Value, procName.Line);

                if (Match("(")) {
                    auto paramsNode = std::make_shared<Node>("Parameters", Current().Line);
                    while (Current().Type == TokenType::Identifier || Current().Value == "بالقيمة" || Current().Value == "بالمرجع") {
                        std::string passMode = "بالقيمة";
                        if (Current().Value == "بالقيمة" || Current().Value == "بالمرجع") {
                            passMode = Advance().Value;
                        }
                        auto pName = ExpectType(TokenType::Identifier, "يجب كتابة اسم المعامل");
                        Expect(":", "يجب وضع نقطتين ':' لتحديد نوع المعامل");
                        auto pType = Advance().Value;

                        auto paramNode = std::make_shared<Node>("ParamDecl", pName.Value, pType, pName.Line);
                        paramNode->Val = passMode;
                        paramsNode->AddChild(paramNode);

                        if (!Match("؛") && !Match(",")) break;
                    }
                    Expect(")", "يجب إغلاق القوس ')' لقائمة المعاملات");
                    procNode->AddChild(paramsNode);
                }

                Expect("؛", "يجب وضع فاصلة منقوطة '؛' بعد ترويسة الإجراء");
                procNode->AddChild(ParseBlock());
                Expect("؛", "يجب إنهاء الإجراء بفاصلة منقوطة '؛'");
                declsNode->AddChild(procNode);
            } catch (const std::exception& ex) {
                std::string msg = ex.what();
                bool exists = false;
                for (const auto& e : Errors) if (e == msg) { exists = true; break; }
                if (!exists) Errors.push_back(msg);
                Synchronize();
            }
        }

        return declsNode;
    }

    std::shared_ptr<Node> Parser::ParseStatementList() {
        auto listNode = std::make_shared<Node>("StatementList", Current().Line);
        while (Current().Value != "}" && Current().Type != TokenType::EndOfFile) {
            try {
                auto stmt = ParseStatement();
                if (stmt) {
                    listNode->AddChild(stmt);
                }
            } catch (const std::exception& ex) {
                std::string msg = ex.what();
                bool exists = false;
                for (const auto& e : Errors) {
                    if (e == msg) { exists = true; break; }
                }
                if (!exists) Errors.push_back(msg);
                Synchronize();
            }

            while (Current().Value == "؛") {
                Advance();
            }
        }
        return listNode;
    }

    std::shared_ptr<Node> Parser::ParseStatement() {
        if (Current().Value == "اذا")    return ParseIfStatement();
        if (Current().Value == "طالما")  return ParseWhileStatement();
        if (Current().Value == "اعد")    return ParseRepeatUntilStatement();
        if (Current().Value == "كرر")    return ParseForStatement();
        if (Current().Value == "اطبع")   return ParsePrintStatement();
        if (Current().Value == "اقرا" || Current().Value == "اقرأ" || Current().Value == "اقرء")   return ParseReadStatement();
        if (Current().Value == "{")      return ParseBlock();

        if (Current().Type == TokenType::Identifier) {
            int line = Current().Line;
            auto varToken = Advance();
            std::shared_ptr<Node> target = std::make_shared<Node>("Variable", varToken.Value, line);

            while (Current().Value == "." || Current().Value == "[") {
                if (Match(".")) {
                    auto field = ExpectType(TokenType::Identifier, "يجب تحديد اسم الحقل بعد النقطة");
                    auto access = std::make_shared<Node>("FieldAccess", field.Value, line);
                    access->AddChild(target);
                    target = access;
                } else if (Match("[")) {
                    auto indexExpr = ParseExpression();
                    Expect("]", "يجب إغلاق القوس المربع ']' بعد الفهرس");
                    auto indexed = std::make_shared<Node>("IndexedAccess", line);
                    indexed->AddChild(target);
                    indexed->AddChild(indexExpr);
                    target = indexed;
                }
            }

            if (Match("=")) {
                auto expr = ParseExpression();
                Expect("؛", "يجب إنهاء جملة الإسناد بفاصلة منقوطة '؛'");
                auto assignNode = std::make_shared<Node>("Assign", varToken.Value, line);
                assignNode->AddChild(target);
                assignNode->AddChild(expr);
                return assignNode;
            }

            if (Match("(")) {
                auto callNode = std::make_shared<Node>("CallStatement", varToken.Value, line);
                if (Current().Value != ")") {
                    callNode->AddChild(ParseExpression());
                    while (Match(",")) {
                        callNode->AddChild(ParseExpression());
                    }
                }
                Expect(")", "يجب إغلاق القوس ')' لاستدعاء الإجراء");
                Expect("؛", "يجب إنهاء جملة الاستدعاء بفاصلة منقوطة '؛'");
                return callNode;
            }

            throw std::runtime_error("خطأ نحوي في السطر " + std::to_string(line) + ": تعليمة غير مكتملة بعد المعرف '" + varToken.Value + "'، يجب وضع علامة '=' للإسناد أو '(' للاستدعاء أو إنهاء التعليمة بـ '؛'");
        }

        throw std::runtime_error("خطأ نحوي في السطر " + std::to_string(Current().Line) + ": رمز غير متوقع '" + Current().Value + "' في قائمة التعليمات");
    }

    std::shared_ptr<Node> Parser::ParseIfStatement() {
        int line = Current().Line;
        Advance(); // اذا
        Expect("(", "يجب فتح قوس '(' لشرط 'اذا'");
        auto cond = ParseExpression();
        Expect(")", "يجب إغلاق القوس ')' لشرط 'اذا'");
        Expect("فان", "يجب وضع كلمة 'فان' بعد شرط 'اذا'");

        auto ifNode = std::make_shared<Node>("IfStatement", line);
        ifNode->AddChild(cond);

        if (Current().Value == "{") {
            Advance();
            ifNode->AddChild(ParseStatementList());
            Expect("}", "يجب إغلاق قوس المجموعة '}' لجملة 'اذا'");
        } else {
            ifNode->AddChild(ParseStatement());
        }

        while (Current().Value == "؛") {
            Advance();
        }

        if (Match("والا")) {
            auto elseNode = std::make_shared<Node>("ElseBlock", line);
            if (Current().Value == "اذا") {
                elseNode->AddChild(ParseIfStatement());
            } else if (Current().Value == "{") {
                Advance();
                elseNode->AddChild(ParseStatementList());
                Expect("}", "يجب إغلاق قوس المجموعة '}' لجملة 'والا'");
            } else {
                elseNode->AddChild(ParseStatement());
            }
            ifNode->AddChild(elseNode);
        }

        while (Current().Value == "؛") {
            Advance();
        }
        return ifNode;
    }

    std::shared_ptr<Node> Parser::ParseWhileStatement() {
        int line = Current().Line;
        Advance(); // طالما
        Expect("(", "يجب فتح قوس '(' لشرط 'طالما'");
        auto cond = ParseExpression();
        Expect(")", "يجب إغلاق القوس ')' لشرط 'طالما'");
        Expect("استمر", "يجب وضع كلمة 'استمر' بعد شرط 'طالما'");

        auto whileNode = std::make_shared<Node>("WhileStatement", line);
        whileNode->AddChild(cond);

        if (Current().Value == "{") {
            Advance();
            whileNode->AddChild(ParseStatementList());
            Expect("}", "يجب إغلاق قوس المجموعة '}' لحلقة 'طالما'");
        } else {
            whileNode->AddChild(ParseStatement());
        }

        if (Current().Value == "؛") Advance();
        return whileNode;
    }

    std::shared_ptr<Node> Parser::ParseRepeatUntilStatement() {
        int line = Current().Line;
        Advance(); // اعد
        auto repeatNode = std::make_shared<Node>("RepeatUntilStatement", line);

        if (Current().Value == "{") {
            Advance();
            repeatNode->AddChild(ParseStatementList());
            Expect("}", "يجب إغلاق قوس المجموعة '}' لحلقة 'اعد'");
        } else {
            repeatNode->AddChild(ParseStatement());
        }

        Expect("حتى", "يجب كتابة كلمة 'حتى' بعد كتلة 'اعد'");
        Expect("(", "يجب فتح قوس '(' لشرط 'حتى'");
        auto cond = ParseExpression();
        Expect(")", "يجب إغلاق القوس ')' لشرط 'حتى'");
        Expect("؛", "يجب إنهاء حلقة 'اعد حتى' بفاصلة منقوطة '؛'");

        repeatNode->AddChild(cond);
        return repeatNode;
    }

    std::shared_ptr<Node> Parser::ParseForStatement() {
        int line = Current().Line;
        Advance(); // كرر
        Expect("(", "يجب فتح قوس '(' لحلقة 'كرر'");
        auto varToken = ExpectType(TokenType::Identifier, "يجب تحديد متغير العداد لحلقة 'كرر'");
        Expect("=", "يجب وضع '=' لإسناد القيمة الأولية للعداد");
        auto startExpr = ParseExpression();
        Expect("الى", "يجب كتابة كلمة 'الى' لتحديد نهاية العداد");
        auto endExpr = ParseExpression();

        std::shared_ptr<Node> stepExpr = nullptr;
        if (Match("اضف")) {
            stepExpr = ParseExpression();
        }

        Expect(")", "يجب إغلاق القوس ')' لتحديدات حلقة 'كرر'");

        auto forNode = std::make_shared<Node>("ForStatement", varToken.Value, line);
        forNode->AddChild(startExpr);
        forNode->AddChild(endExpr);
        if (stepExpr) forNode->AddChild(stepExpr);

        if (Current().Value == "{") {
            Advance();
            forNode->AddChild(ParseStatementList());
            Expect("}", "يجب إغلاق قوس المجموعة '}' لحلقة 'كرر'");
        } else {
            forNode->AddChild(ParseStatement());
        }

        if (Current().Value == "؛") Advance();
        return forNode;
    }

    std::shared_ptr<Node> Parser::ParsePrintStatement() {
        int line = Current().Line;
        Advance(); // اطبع
        Expect("(", "يجب فتح قوس '(' بعد تعليمة 'اطبع'");
        auto printNode = std::make_shared<Node>("PrintStatement", line);

        if (Current().Value != ")") {
            printNode->AddChild(ParseExpression());
            while (Match(",")) {
                printNode->AddChild(ParseExpression());
            }
        }

        Expect(")", "يجب إغلاق القوس ')' لتعليمة 'اطبع'");
        Expect("؛", "يجب إنهاء تعليمة 'اطبع' بفاصلة منقوطة '؛'");
        return printNode;
    }

    std::shared_ptr<Node> Parser::ParseReadStatement() {
        int line = Current().Line;
        Advance(); // اقرا / اقرأ / اقرء
        bool hasParen = Match("(");
        auto varToken = ExpectType(TokenType::Identifier, "يجب كتابة اسم المتغير المراد القراءة إليه");
        if (hasParen) {
            Expect(")", "يجب إغلاق القوس ')' لتعليمة 'اقرا'");
        }
        Expect("؛", "يجب إنهاء تعليمة 'اقرا' بفاصلة منقوطة '؛'");

        auto readNode = std::make_shared<Node>("ReadStatement", varToken.Value, line);
        return readNode;
    }

    // Expressions
    std::shared_ptr<Node> Parser::ParseExpression() {
        return ParseLogicalOr();
    }

    std::shared_ptr<Node> Parser::ParseLogicalOr() {
        auto node = ParseLogicalAnd();
        while (Current().Value == "||") {
            std::string op = Advance().Value;
            auto bin = std::make_shared<Node>("BinaryExpr", op, node->Line);
            bin->AddChild(node);
            bin->AddChild(ParseLogicalAnd());
            node = bin;
        }
        return node;
    }

    std::shared_ptr<Node> Parser::ParseLogicalAnd() {
        auto node = ParseEquality();
        while (Current().Value == "&&") {
            std::string op = Advance().Value;
            auto bin = std::make_shared<Node>("BinaryExpr", op, node->Line);
            bin->AddChild(node);
            bin->AddChild(ParseEquality());
            node = bin;
        }
        return node;
    }

    std::shared_ptr<Node> Parser::ParseEquality() {
        auto node = ParseRelational();
        while (Current().Value == "==" || Current().Value == "!=") {
            std::string op = Advance().Value;
            auto bin = std::make_shared<Node>("BinaryExpr", op, node->Line);
            bin->AddChild(node);
            bin->AddChild(ParseRelational());
            node = bin;
        }
        return node;
    }

    std::shared_ptr<Node> Parser::ParseRelational() {
        auto node = ParseSimpleExpression();
        while (Current().Value == "<" || Current().Value == "<=" || Current().Value == ">" || Current().Value == ">=") {
            std::string op = Advance().Value;
            auto bin = std::make_shared<Node>("BinaryExpr", op, node->Line);
            bin->AddChild(node);
            bin->AddChild(ParseSimpleExpression());
            node = bin;
        }
        return node;
    }

    std::shared_ptr<Node> Parser::ParseSimpleExpression() {
        std::string prefixSign = "";
        if (Current().Value == "+" || Current().Value == "-") {
            prefixSign = Advance().Value;
        }

        auto node = ParseTerm();
        if (!prefixSign.empty()) {
            auto uNode = std::make_shared<Node>("UnaryExpr", prefixSign, node->Line);
            uNode->AddChild(node);
            node = uNode;
        }

        while (Current().Value == "+" || Current().Value == "-") {
            std::string op = Advance().Value;
            auto bin = std::make_shared<Node>("BinaryExpr", op, node->Line);
            bin->AddChild(node);
            bin->AddChild(ParseTerm());
            node = bin;
        }

        return node;
    }

    std::shared_ptr<Node> Parser::ParseTerm() {
        auto node = ParseFactor();
        while (Current().Value == "*" || Current().Value == "/" || Current().Value == "\\" || Current().Value == "%" || Current().Value == "^") {
            std::string op = Advance().Value;
            auto bin = std::make_shared<Node>("BinaryExpr", op, node->Line);
            bin->AddChild(node);
            bin->AddChild(ParseFactor());
            node = bin;
        }
        return node;
    }

    std::shared_ptr<Node> Parser::ParseFactor() {
        int line = Current().Line;

        if (Match("!")) {
            auto uNode = std::make_shared<Node>("UnaryExpr", "!", line);
            uNode->AddChild(ParseFactor());
            return uNode;
        }

        if (Match("(")) {
            auto node = ParseExpression();
            Expect(")", "يجب إغلاق القوس ')' في التعبير");
            return node;
        }

        if (Current().Type == TokenType::Number) {
            auto t = Advance();
            auto numNode = std::make_shared<Node>("Number", line);
            numNode->Val = t.Value;
            return numNode;
        }

        if (Current().Type == TokenType::String) {
            auto t = Advance();
            auto strNode = std::make_shared<Node>("StringLiteral", line);
            strNode->Val = t.Value;
            return strNode;
        }

        if (Current().Type == TokenType::Char) {
            auto t = Advance();
            auto charNode = std::make_shared<Node>("CharLiteral", line);
            charNode->Val = t.Value;
            return charNode;
        }

        if (Current().Value == "صواب" || Current().Value == "خطأ" || Current().Value == "صح" || Current().Value == "خطا") {
            auto t = Advance();
            auto boolNode = std::make_shared<Node>("BooleanLiteral", line);
            boolNode->Val = t.Value;
            return boolNode;
        }

        if (Current().Type == TokenType::Identifier) {
            auto t = Advance();
            std::shared_ptr<Node> target = std::make_shared<Node>("Variable", t.Value, line);

            while (Current().Value == "." || Current().Value == "[") {
                if (Match(".")) {
                    auto field = ExpectType(TokenType::Identifier, "يجب تحديد اسم الحقل بعد النقطة");
                    auto access = std::make_shared<Node>("FieldAccess", field.Value, line);
                    access->AddChild(target);
                    target = access;
                } else if (Match("[")) {
                    auto idx = ParseExpression();
                    Expect("]", "يجب إغلاق القوس المربع ']' بعد الفهرس");
                    auto indexed = std::make_shared<Node>("IndexedAccess", line);
                    indexed->AddChild(target);
                    indexed->AddChild(idx);
                    target = indexed;
                }
            }

            if (Match("(")) {
                auto call = std::make_shared<Node>("FunctionCall", t.Value, line);
                if (Current().Value != ")") {
                    call->AddChild(ParseExpression());
                    while (Match(",")) {
                        call->AddChild(ParseExpression());
                    }
                }
                Expect(")", "يجب إغلاق القوس ')' لاستدعاء الدالة");
                return call;
            }

            return target;
        }

        throw std::runtime_error("خطأ نحوي في السطر " + std::to_string(line) + ": تعبير غير متوقع أو غير صالح عند الرمز '" + Current().Value + "'");
    }

} // namespace CompilerCPP
