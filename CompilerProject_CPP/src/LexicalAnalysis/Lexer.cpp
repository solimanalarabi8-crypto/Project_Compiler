#include "../../include/LexicalAnalysis/Lexer.h"
#include <cctype>
#include <sstream>

namespace CompilerCPP {

    const std::unordered_set<std::string> Lexer::Keywords = {
        "برنامج", "ثابت", "نوع", "متغير", "اجراء", "دالة",
        "صحيح", "حقيقي", "منطقي", "حرفي", "خيط_رمزي",
        "قائمة", "سجل", "من",
        "اذا", "فان", "والا", "طالما", "استمر", "اعد", "حتى", "كرر", "الى", "اضف",
        "اقرا", "اقرأ", "اقرء", "اطبع", "صواب", "خطأ", "صح", "خطا", "بالقيمة", "بالمرجع", "ارجع"
    };

    Lexer::Lexer(std::string sourceCode)
        : _src(std::move(sourceCode)), _pos(0), _line(1) {
        // Strip UTF-8 BOM if present
        if (_src.size() >= 3 && static_cast<unsigned char>(_src[0]) == 0xEF && static_cast<unsigned char>(_src[1]) == 0xBB && static_cast<unsigned char>(_src[2]) == 0xBF) {
            _src.erase(0, 3);
        }
    }

    bool Lexer::IsEasternArabicDigit(const std::string& utf8Char) {
        if (utf8Char.size() == 2) {
            unsigned char b1 = static_cast<unsigned char>(utf8Char[0]);
            unsigned char b2 = static_cast<unsigned char>(utf8Char[1]);
            return (b1 == 0xD9 && b2 >= 0xA0 && b2 <= 0xA9);
        }
        return false;
    }

    char Lexer::ConvertEasternDigit(const std::string& utf8Char) {
        if (IsEasternArabicDigit(utf8Char)) {
            unsigned char b2 = static_cast<unsigned char>(utf8Char[1]);
            return static_cast<char>('0' + (b2 - 0xA0));
        }
        return '0';
    }

    bool Lexer::IsArabicLetterStart(unsigned char c) {
        return (c >= 0xD8 && c <= 0xDF);
    }

    Token Lexer::ReadNumber() {
        int startLine = _line;
        std::string num;
        bool hasDot = false;

        while (_pos < _src.size()) {
            char c = Current();
            if (std::isdigit(static_cast<unsigned char>(c))) {
                num += c;
                Advance();
            } else if (_pos + 1 < _src.size()) {
                std::string twoBytes = _src.substr(_pos, 2);
                if (IsEasternArabicDigit(twoBytes)) {
                    num += ConvertEasternDigit(twoBytes);
                    Advance(2);
                } else if (c == '.' && !hasDot && (std::isdigit(static_cast<unsigned char>(Peek(1))) || (_pos + 2 < _src.size() && IsEasternArabicDigit(_src.substr(_pos + 1, 2))))) {
                    hasDot = true;
                    num += '.';
                    Advance();
                } else {
                    break;
                }
            } else {
                break;
            }
        }

        return Token(num, TokenType::Number, startLine);
    }

    Token Lexer::ReadIdentifierOrKeyword() {
        int startLine = _line;
        size_t startPos = _pos;

        while (_pos < _src.size()) {
            unsigned char c = static_cast<unsigned char>(Current());

            // Arabic UTF-8 2-byte sequences (0xD8-0xDF)
            if (IsArabicLetterStart(c) && _pos + 1 < _src.size()) {
                Advance(2);
            }
            // English letters, digits, underscore
            else if (std::isalnum(c) || c == '_') {
                Advance(1);
            } else {
                break;
            }
        }

        std::string val = _src.substr(startPos, _pos - startPos);
        TokenType type = (Keywords.find(val) != Keywords.end()) ? TokenType::Keyword : TokenType::Identifier;
        return Token(val, type, startLine);
    }

    Token Lexer::ReadString(char quoteChar) {
        int startLine = _line;
        Advance(); // Skip opening quote
        std::string str;

        while (_pos < _src.size() && Current() != quoteChar) {
            if (Current() == '\n') _line++;
            str += Current();
            Advance();
        }

        if (_pos < _src.size() && Current() == quoteChar) {
            Advance(); // Skip closing quote
        }

        return Token(str, TokenType::String, startLine);
    }

    Token Lexer::ReadSmartString(const std::string& startQuote, const std::string& endQuote) {
        int startLine = _line;
        Advance(startQuote.size());
        std::string str;

        while (_pos < _src.size()) {
            if (_pos + endQuote.size() <= _src.size() && _src.substr(_pos, endQuote.size()) == endQuote) {
                Advance(endQuote.size());
                break;
            }
            if (Current() == '\n') _line++;
            str += Current();
            Advance();
        }

        return Token(str, TokenType::String, startLine);
    }

    Token Lexer::ReadChar() {
        int startLine = _line;
        Advance(); // Skip '
        std::string ch;

        while (_pos < _src.size() && Current() != '\'') {
            if (Current() == '\n') _line++;
            ch += Current();
            Advance();
        }

        if (_pos < _src.size() && Current() == '\'') {
            Advance();
        }

        return Token(ch, TokenType::Char, startLine);
    }

    std::vector<Token> Lexer::Tokenize() {
        std::vector<Token> tokens;

        while (_pos < _src.size()) {
            char c = Current();
            unsigned char uc = static_cast<unsigned char>(c);

            // 1. مسافات وسطور
            if (c == ' ' || c == '\t' || c == '\r') {
                Advance();
            } else if (c == '\n') {
                _line++;
                Advance();
            }
            // 2. تعليقات
            else if (c == '/' && Peek(1) == '/') {
                Advance(2);
                while (_pos < _src.size() && Current() != '\n') {
                    Advance();
                }
            } else if (c == '/' && Peek(1) == '*') {
                Advance(2);
                while (_pos + 1 < _src.size() && !(Current() == '*' && Peek(1) == '/')) {
                    if (Current() == '\n') _line++;
                    Advance();
                }
                if (_pos + 1 < _src.size()) {
                    Advance(2); // Skip */
                }
            }
            // 3. علامات التنصيص المنحنية الذكية “”
            else if (_pos + 2 < _src.size() && _src.substr(_pos, 3) == "\xE2\x80\x9C") { // “
                tokens.push_back(ReadSmartString("\xE2\x80\x9C", "\xE2\x80\x9D"));
            }
            // 4. السلاسل النصية العادية
            else if (c == '"') {
                tokens.push_back(ReadString('"'));
            }
            // 5. المحارف
            else if (c == '\'') {
                tokens.push_back(ReadChar());
            }
            // 6. الفاصلة المنقوطة العربية ؛ (D8 BB) أو الإنجليزية ;
            else if (_pos + 1 < _src.size() && static_cast<unsigned char>(_src[_pos]) == 0xD8 && static_cast<unsigned char>(_src[_pos + 1]) == 0xBB) {
                tokens.emplace_back("؛", TokenType::Identifier, _line); // terminal semicolon
                Advance(2);
            } else if (c == ';') {
                tokens.emplace_back("؛", TokenType::Identifier, _line);
                Advance();
            }
            // 7. الرموز البسيطة
            else if (c == '(' || c == ')' || c == '{' || c == '}' || c == '[' || c == ']' || c == ':' || c == ',' || c == '.') {
                tokens.emplace_back(std::string(1, c), TokenType::Symbol, _line);
                Advance();
            }
            // 8. المعاملات المركبة والبسيطة
            else if (c == '=' && Peek(1) == '=') {
                tokens.emplace_back("==", TokenType::Operator, _line);
                Advance(2);
            } else if (c == '!' && Peek(1) == '=') {
                tokens.emplace_back("!=", TokenType::Operator, _line);
                Advance(2);
            } else if (c == '<' && Peek(1) == '=') {
                tokens.emplace_back("<=", TokenType::Operator, _line);
                Advance(2);
            } else if (c == '>' && Peek(1) == '=') {
                tokens.emplace_back(">=", TokenType::Operator, _line);
                Advance(2);
            } else if (c == '&' && Peek(1) == '&') {
                tokens.emplace_back("&&", TokenType::Operator, _line);
                Advance(2);
            } else if (c == '|' && Peek(1) == '|') {
                tokens.emplace_back("||", TokenType::Operator, _line);
                Advance(2);
            } else if (c == '=' || c == '+' || c == '-' || c == '*' || c == '/' || c == '\\' || c == '%' || c == '^' || c == '<' || c == '>' || c == '!') {
                tokens.emplace_back(std::string(1, c), (c == '=') ? TokenType::Symbol : TokenType::Operator, _line);
                Advance();
            }
            // 9. الأرقام
            else if (std::isdigit(uc) || (_pos + 1 < _src.size() && IsEasternArabicDigit(_src.substr(_pos, 2)))) {
                tokens.push_back(ReadNumber());
            }
            // 10. المعرفات والكلمات المحجوزة
            else if (std::isalpha(uc) || uc == '_' || IsArabicLetterStart(uc)) {
                tokens.push_back(ReadIdentifierOrKeyword());
            }
            // 11. رمز مجهول
            else {
                tokens.emplace_back(std::string(1, c), TokenType::Unknown, _line);
                Advance();
            }
        }

        tokens.emplace_back("EOF", TokenType::EndOfFile, _line);
        return tokens;
    }

} // namespace CompilerCPP
