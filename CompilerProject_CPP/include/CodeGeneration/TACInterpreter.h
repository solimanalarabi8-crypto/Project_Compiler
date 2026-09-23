#pragma once
#include <string>
#include <vector>
#include <unordered_map>
#include <sstream>

namespace CompilerCPP {

    class TACInterpreter {
    private:
        std::vector<std::string> _tac;
        std::unordered_map<std::string, double> _variables;
        std::unordered_map<std::string, std::string> _stringVars;
        std::unordered_map<std::string, size_t> _labels;
        std::vector<std::string> _inputTokens;
        size_t _inputIndex = 0;

        double EvaluateExpr(const std::string& expr);
        void PrepareInput(const std::string& rawInput);

    public:
        explicit TACInterpreter(std::vector<std::string> tac, const std::string& userInput = "");
        std::string Execute();
    };

} // namespace CompilerCPP
