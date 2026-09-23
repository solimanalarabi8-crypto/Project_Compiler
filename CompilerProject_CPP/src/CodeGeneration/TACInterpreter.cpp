#include "../../include/CodeGeneration/TACInterpreter.h"
#include <iostream>
#include <cmath>
#include <cctype>

namespace CompilerCPP {

    TACInterpreter::TACInterpreter(std::vector<std::string> tac, const std::string& userInput)
        : _tac(std::move(tac)) {
        PrepareInput(userInput);
    }

    void TACInterpreter::PrepareInput(const std::string& rawInput) {
        _inputTokens.clear();
        _inputIndex = 0;
        if (rawInput.empty()) return;

        std::string normalized;
        for (size_t i = 0; i < rawInput.size(); ) {
            unsigned char b1 = static_cast<unsigned char>(rawInput[i]);
            if (b1 == 0xD9 && i + 1 < rawInput.size()) {
                unsigned char b2 = static_cast<unsigned char>(rawInput[i + 1]);
                if (b2 >= 0xA0 && b2 <= 0xA9) {
                    normalized += (char)('0' + (b2 - 0xA0));
                    i += 2;
                    continue;
                }
            }
            normalized += rawInput[i];
            i++;
        }

        std::istringstream iss(normalized);
        std::string token;
        while (iss >> token) {
            _inputTokens.push_back(token);
        }
    }

    double TACInterpreter::EvaluateExpr(const std::string& expr) {
        std::string s = expr;
        while (!s.empty() && s.front() == ' ') s.erase(s.begin());
        while (!s.empty() && s.back() == ' ') s.pop_back();

        if (s.empty()) return 0.0;

        // Try direct number
        char* endPtr = nullptr;
        double val = std::strtod(s.c_str(), &endPtr);
        if (endPtr && *endPtr == '\0') {
            return val;
        }

        // Try boolean
        if (s == "صواب" || s == "true") return 1.0;
        if (s == "خطأ" || s == "false") return 0.0;

        // Try variable
        if (_variables.find(s) != _variables.end()) {
            return _variables[s];
        }

        // Binary operators check
        static const std::vector<std::string> ops = { "==", "!=", "<=", ">=", "<", ">", "&&", "||", "+", "-", "*", "/", "\\", "%", "^" };
        for (const auto& op : ops) {
            std::string opPattern = " " + op + " ";
            size_t pos = s.find(opPattern);
            if (pos != std::string::npos) {
                std::string leftStr = s.substr(0, pos);
                std::string rightStr = s.substr(pos + opPattern.size());
                double leftVal = EvaluateExpr(leftStr);
                double rightVal = EvaluateExpr(rightStr);

                if (op == "+")  return leftVal + rightVal;
                if (op == "-")  return leftVal - rightVal;
                if (op == "*")  return leftVal * rightVal;
                if (op == "/" || op == "\\") return (rightVal != 0.0) ? (leftVal / rightVal) : 0.0;
                if (op == "%")  return std::fmod(leftVal, rightVal != 0.0 ? rightVal : 1.0);
                if (op == "^")  return std::pow(leftVal, rightVal);
                if (op == "==") return (leftVal == rightVal) ? 1.0 : 0.0;
                if (op == "!=") return (leftVal != rightVal) ? 1.0 : 0.0;
                if (op == "<")  return (leftVal < rightVal) ? 1.0 : 0.0;
                if (op == "<=") return (leftVal <= rightVal) ? 1.0 : 0.0;
                if (op == ">")  return (leftVal > rightVal) ? 1.0 : 0.0;
                if (op == ">=") return (leftVal >= rightVal) ? 1.0 : 0.0;
                if (op == "&&") return (leftVal != 0.0 && rightVal != 0.0) ? 1.0 : 0.0;
                if (op == "||") return (leftVal != 0.0 || rightVal != 0.0) ? 1.0 : 0.0;
            }
        }

        // Unary minus
        if (s.front() == '-') {
            return -EvaluateExpr(s.substr(1));
        }

        return 0.0;
    }

    std::string TACInterpreter::Execute() {
        _variables.clear();
        _stringVars.clear();
        _labels.clear();

        // 1. جمع الملصقات Labels
        for (size_t i = 0; i < _tac.size(); ++i) {
            std::string line = _tac[i];
            while (!line.empty() && line.front() == ' ') line.erase(line.begin());
            while (!line.empty() && line.back() == ' ') line.pop_back();

            if (!line.empty() && line.back() == ':') {
                std::string labelName = line.substr(0, line.size() - 1);
                _labels[labelName] = i;
            }
        }

        std::ostringstream output;
        size_t pc = 0;
        int instructionsCount = 0;

        while (pc < _tac.size() && instructionsCount < 100000) {
            instructionsCount++;
            std::string line = _tac[pc];
            while (!line.empty() && line.front() == ' ') line.erase(line.begin());
            while (!line.empty() && line.back() == ' ') line.pop_back();

            if (line.empty() || line.rfind("//", 0) == 0 || line.back() == ':') {
                pc++;
                continue;
            }

            // Print
            if (line.rfind("print ", 0) == 0) {
                std::string arg = line.substr(6);
                while (!arg.empty() && arg.front() == ' ') arg.erase(arg.begin());

                if (arg.size() >= 2 && arg.front() == '"' && arg.back() == '"') {
                    output << arg.substr(1, arg.size() - 2) << "\n";
                } else if (_variables.find(arg) != _variables.end()) {
                    double v = _variables[arg];
                    if (v == std::floor(v)) {
                        output << static_cast<long long>(v) << "\n";
                    } else {
                        output << v << "\n";
                    }
                } else {
                    double v = EvaluateExpr(arg);
                    if (v == std::floor(v)) {
                        output << static_cast<long long>(v) << "\n";
                    } else {
                        output << v << "\n";
                    }
                }
                pc++;
            }
            // Goto
            else if (line.rfind("goto ", 0) == 0) {
                std::string target = line.substr(5);
                while (!target.empty() && target.front() == ' ') target.erase(target.begin());
                if (_labels.find(target) != _labels.end()) {
                    pc = _labels[target];
                } else {
                    pc++;
                }
            }
            // If ... goto
            else if (line.rfind("if ", 0) == 0) {
                size_t gPos = line.find(" goto ");
                std::string cond = line.substr(3, gPos - 3);
                std::string target = line.substr(gPos + 6);
                while (!target.empty() && target.front() == ' ') target.erase(target.begin());

                double condVal = EvaluateExpr(cond);
                if (condVal != 0.0) {
                    if (_labels.find(target) != _labels.end()) {
                        pc = _labels[target];
                    } else {
                        pc++;
                    }
                } else {
                    pc++;
                }
            }
            // Read
            else if (line.rfind("read ", 0) == 0) {
                std::string dest = line.substr(5);
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();

                double val = 0.0;
                if (_inputIndex < _inputTokens.size()) {
                    val = EvaluateExpr(_inputTokens[_inputIndex++]);
                }
                _variables[dest] = val;
                pc++;
            }
            // Assignment
            else if (line.find("=") != std::string::npos) {
                size_t eqPos = line.find("=");
                std::string dest = line.substr(0, eqPos);
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());

                std::string expr = line.substr(eqPos + 1);
                while (!expr.empty() && expr.front() == ' ') expr.erase(expr.begin());
                while (!expr.empty() && expr.back() == ' ') expr.pop_back();

                double res = EvaluateExpr(expr);
                _variables[dest] = res;
                pc++;
            } else {
                pc++;
            }
        }

        return output.str();
    }

} // namespace CompilerCPP
