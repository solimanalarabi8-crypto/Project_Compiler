#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include <filesystem>
#include "../include/CodeGeneration/CompilerRunner.h"
#include "../include/Tests/CompilerTestSuite.h"

#ifdef _WIN32
#include <windows.h>
#include <shellapi.h>
#pragma comment(lib, "shell32.lib")

static std::string WideToUtf8(const std::wstring& wstr) {
    if (wstr.empty()) return "";
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &strTo[0], size_needed, NULL, NULL);
    return strTo;
}
#endif

int main(int argc, char* argv[]) {
#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
#endif

    std::vector<std::string> args;
    std::filesystem::path inputFilePath;

#ifdef _WIN32
    int wargc = 0;
    LPWSTR* wargv = CommandLineToArgvW(GetCommandLineW(), &wargc);
    if (wargv) {
        for (int i = 0; i < wargc; ++i) {
            args.push_back(WideToUtf8(wargv[i]));
        }
        if (wargc > 1) {
            inputFilePath = std::filesystem::path(wargv[1]);
        }
        LocalFree(wargv);
    } else {
        for (int i = 0; i < argc; ++i) {
            args.push_back(argv[i]);
        }
        if (argc > 1) {
            inputFilePath = std::filesystem::u8path(argv[1]);
        }
    }
#else
    for (int i = 0; i < argc; ++i) {
        args.push_back(argv[i]);
    }
    if (argc > 1) {
        inputFilePath = std::filesystem::u8path(argv[1]);
    }
#endif

    if (args.size() > 1) {
        std::string firstArg = args[1];

        // تشغيل الاختبارات
        if (firstArg == "--test" || firstArg == "-t") {
            CompilerCPP::CompilerTestSuite::RunAllTests();
            return 0;
        }

        // قراءة الملف الممرر
        std::ifstream file(inputFilePath);
        if (!file.is_open()) {
            std::cerr << "❌ خطأ: تعذر فتح الملف: " << firstArg << "\n";
            return 1;
        }

        std::ostringstream ss;
        ss << file.rdbuf();
        std::string sourceCode = ss.str();

        bool jsonMode = (args.size() > 2 && (args[2] == "--json" || args[2] == "-j"));

        std::string userInput = "";
#ifdef _WIN32
        HANDLE hStdin = GetStdHandle(STD_INPUT_HANDLE);
        DWORD fileType = GetFileType(hStdin);
        if (fileType == FILE_TYPE_PIPE || fileType == FILE_TYPE_DISK) {
            char buffer[2048];
            DWORD bytesRead = 0;
            while (ReadFile(hStdin, buffer, sizeof(buffer), &bytesRead, NULL) && bytesRead > 0) {
                userInput.append(buffer, bytesRead);
            }
        }
#endif

        auto result = CompilerCPP::CompilerRunner::Compile(sourceCode, !jsonMode, userInput);

        if (jsonMode) {
            std::cout << result.ToJson() << "\n";
        }

        return result.IsSuccess ? 0 : 1;
    }

    // التشغيل التلقائي الافتراضي: عرض الاختبارات الشاملة ثم تشغيل مثال توضيحي
    CompilerCPP::CompilerTestSuite::RunAllTests();

    std::cout << "\n=========================================================================================\n";
    std::cout << "                  بدء تشغيل المترجم (C++) على برنامج حساب_العمليات التجريبي                 \n";
    std::cout << "=========================================================================================\n";

    std::string sampleProgram = 
        "برنامج حساب_العمليات ؛\n"
        "ثابت\n"
        "    الحد_الاقصى = 100 ؛\n"
        "متغير\n"
        "    س , ص , مجموع : صحيح ؛\n"
        "{\n"
        "    س = 20 ؛\n"
        "    ص = 79 ؛\n"
        "    مجموع = س + ص ؛\n"
        "\n"
        "    اذا ( مجموع > 50 ) فان {\n"
        "        اطبع ( \"المجموع اكبر من خمسين وهو:\" , مجموع ) ؛\n"
        "    } والا {\n"
        "        اطبع ( \"المجموع اقل من او يساوي خمسين\" ) ؛\n"
        "    } ؛\n"
        "\n"
        "    اطبع ( \"نهاية البرنامج بنجاح من مترجم C++\" ) ؛\n"
        "} .\n";

    CompilerCPP::CompilerRunner::Compile(sampleProgram, true);

    return 0;
}
