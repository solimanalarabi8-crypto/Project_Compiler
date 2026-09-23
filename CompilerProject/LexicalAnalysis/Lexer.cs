using System;
using System.Collections.Generic;
using System.Text;
using CompilerProject.Models;

namespace CompilerProject.LexicalAnalysis
{
    /// <summary>
    /// المحلل اللغوي / المعجمي الذكي (Smart Lexical Analyzer) للغة البرمجة العربية
    /// يدعم:
    /// - الحروف العربية كاملة والأرقام العربية والشرقية (٠-٩) وتطبيعها
    /// - علامات التنصيص بأنواعها: "", '', ‘’, “”
    /// - الفواصل المنقوطة العربية '؛' والإنجليزية ';'
    /// - جميع الكلمات المحجوزة والعمليات الرياضية والمنطقية
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 1 - 9)
    /// </summary>
    public class Lexer
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "برنامج", "ثابت", "نوع", "قائمة", "من", "سجل", "متغير", "اجراء",
            "بالقيمة", "بالمرجع", "صحيح", "حقيقي", "منطقي", "حرفي", "خيط_رمزي",
            "صح", "خطأ", "اقرا", "اقرأ", "اقرء", "اطبع", "اذا", "فان", "والا", "طالما", "استمر",
            "اعد", "حتى", "كرر", "الى", "اضف"
        };

        private readonly string _source;
        private int _index;
        private int _currentLine;
        private readonly int _length;

        public Lexer(string source)
        {
            _source = source ?? string.Empty;
            _index = 0;
            _currentLine = 1;
            _length = _source.Length;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();

            while (_index < _length)
            {
                char c = _source[_index];

                // 1. المسافات البيضاء والأسطر
                if (char.IsWhiteSpace(c))
                {
                    if (c == '\n') _currentLine++;
                    _index++;
                    continue;
                }

                // 2. التعليقات // و /* */
                if (c == '/' && _index + 1 < _length && _source[_index + 1] == '/')
                {
                    _index += 2;
                    while (_index < _length && _source[_index] != '\n') _index++;
                    continue;
                }

                if (c == '/' && _index + 1 < _length && _source[_index + 1] == '*')
                {
                    _index += 2;
                    while (_index + 1 < _length && !(_source[_index] == '*' && _source[_index + 1] == '/'))
                    {
                        if (_source[_index] == '\n') _currentLine++;
                        _index++;
                    }
                    _index = Math.Min(_length, _index + 2);
                    continue;
                }

                // 3. السلاسل النصية "..." أو “...”
                if (c == '"' || c == '“' || c == '”')
                {
                    tokens.Add(ReadString(c));
                    continue;
                }

                // 4. الرموز المفردة '...' أو ’...‘
                if (c == '\'' || c == '’' || c == '‘')
                {
                    tokens.Add(ReadChar(c));
                    continue;
                }

                // 5. الأرقام (أرقام عربية 0-9 أو مشرقية ٠-٩)
                if (IsDigitChar(c))
                {
                    tokens.Add(ReadNumber());
                    continue;
                }

                // 6. المعرفات والكلمات المحجوزة
                if (IsIdentifierStart(c))
                {
                    tokens.Add(ReadIdentifierOrKeyword());
                    continue;
                }

                // 7. العمليات المزدوجة (==, !=, <=, >=, &&, ||)
                if (_index + 1 < _length)
                {
                    string twoChar = _source.Substring(_index, 2);
                    if (twoChar is "==" or "!=" or "<=" or ">=" or "&&" or "||")
                    {
                        tokens.Add(new Token(twoChar, TokenType.Operator, _currentLine));
                        _index += 2;
                        continue;
                    }
                }

                // 8. العمليات الفردية (+, -, *, /, \, %, ^, !, <, >)
                if (c is '+' or '-' or '*' or '/' or '\\' or '%' or '^' or '!' or '<' or '>')
                {
                    tokens.Add(new Token(c.ToString(), TokenType.Operator, _currentLine));
                    _index++;
                    continue;
                }

                // 9. الرموز والمحددات (؛, ;, :, ., ,, =, {, }, [, ], (, ))
                if (c is '؛' or ';' or ':' or '.' or ',' or '=' or '{' or '}' or '[' or ']' or '(' or ')')
                {
                    string sym = c == ';' ? "؛" : c.ToString();
                    tokens.Add(new Token(sym, TokenType.Symbol, _currentLine));
                    _index++;
                    continue;
                }

                // أي رمز غير معروف
                tokens.Add(new Token(c.ToString(), TokenType.Symbol, _currentLine));
                _index++;
            }

            tokens.Add(new Token("EOF", TokenType.EndOfFile, _currentLine));
            return tokens;
        }

        private Token ReadString(char quoteChar)
        {
            int startLine = _currentLine;
            _index++;
            var sb = new StringBuilder();

            while (_index < _length && _source[_index] != quoteChar && _source[_index] != '"' && _source[_index] != '”')
            {
                if (_source[_index] == '\n') _currentLine++;
                sb.Append(_source[_index]);
                _index++;
            }

            if (_index < _length) _index++; // تخطي علامة الإغلاق
            return new Token(sb.ToString(), TokenType.String, startLine);
        }

        private Token ReadChar(char quoteChar)
        {
            int startLine = _currentLine;
            _index++;
            var sb = new StringBuilder();

            while (_index < _length && _source[_index] != quoteChar && _source[_index] != '\'' && _source[_index] != '’' && _source[_index] != '‘')
            {
                if (_source[_index] == '\n') _currentLine++;
                sb.Append(_source[_index]);
                _index++;
            }

            if (_index < _length) _index++;
            return new Token(sb.ToString(), TokenType.Char, startLine);
        }

        private Token ReadNumber()
        {
            int startLine = _currentLine;
            var sb = new StringBuilder();

            while (_index < _length && IsDigitChar(_source[_index]))
            {
                sb.Append(NormalizeDigit(_source[_index]));
                _index++;
            }

            // فحص الرقم العشري الحقيقي
            if (_index < _length && _source[_index] == '.' && _index + 1 < _length && IsDigitChar(_source[_index + 1]))
            {
                sb.Append('.');
                _index++;
                while (_index < _length && IsDigitChar(_source[_index]))
                {
                    sb.Append(NormalizeDigit(_source[_index]));
                    _index++;
                }
            }

            return new Token(sb.ToString(), TokenType.Number, startLine);
        }

        private Token ReadIdentifierOrKeyword()
        {
            int startLine = _currentLine;
            var sb = new StringBuilder();

            while (_index < _length && IsIdentifierPart(_source[_index]))
            {
                sb.Append(_source[_index]);
                _index++;
            }

            string text = sb.ToString();
            TokenType type = Keywords.Contains(text) ? TokenType.Keyword : TokenType.Identifier;

            return new Token(text, type, startLine);
        }

        private static bool IsDigitChar(char c)
        {
            return (c >= '0' && c <= '9') || (c >= '\u0660' && c <= '\u0669');
        }

        private static char NormalizeDigit(char c)
        {
            if (c >= '\u0660' && c <= '\u0669')
            {
                return (char)('0' + (c - '\u0660'));
            }
            return c;
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_' || (c >= '\u0600' && c <= '\u06FF');
        }

        private static bool IsIdentifierPart(char c)
        {
            return IsIdentifierStart(c) || IsDigitChar(c);
        }
    }
}
