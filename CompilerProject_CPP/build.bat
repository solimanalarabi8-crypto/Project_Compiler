@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo Building Arabic Language Compiler C++ (CompilerProject_CPP)
echo ========================================================

set "SRC_FILES=src\main.cpp src\LexicalAnalysis\Lexer.cpp src\SyntaxAnalysis\Parser.cpp src\SemanticAnalysis\SemanticAnalyzer.cpp src\IntermediateCode\IntermediateCodeGenerator.cpp src\CodeGeneration\AssemblyCodeGenerator.cpp src\CodeGeneration\TACInterpreter.cpp src\CodeGeneration\CompilerRunner.cpp src\Tests\CompilerTestSuite.cpp"

set "VCVARS="

REM Check if cl.exe is already available in PATH
where cl >nul 2>nul
if %errorlevel% equ 0 (
    echo [1/2] MSVC cl.exe found in PATH, compiling...
    cl /std:c++20 /O2 /EHsc /utf-8 /Iinclude %SRC_FILES% /Fe:CompilerProject_CPP.exe
    goto DONE
)

REM Check Visual Studio 2022 paths
if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat"
    goto COMPILE_MSVC
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
    goto COMPILE_MSVC
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
    goto COMPILE_MSVC
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
    goto COMPILE_MSVC
)

REM Check Visual Studio 2019 paths
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Auxiliary\Build\vcvars64.bat" (
    set "VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Auxiliary\Build\vcvars64.bat"
    goto COMPILE_MSVC
)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars64.bat" (
    set "VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars64.bat"
    goto COMPILE_MSVC
)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\VC\Auxiliary\Build\vcvars64.bat" (
    set "VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
    goto COMPILE_MSVC
)

REM Check g++ in PATH only if it supports -std=c++20
where g++ >nul 2>nul
if %errorlevel% equ 0 (
    g++ -std=c++20 -x c++ -E NUL >nul 2>nul
    if %errorlevel% equ 0 (
        echo [1/2] Modern g++ supporting C++20 found in PATH, compiling...
        g++ -std=c++20 -O2 -Iinclude %SRC_FILES% -o CompilerProject_CPP.exe
        goto DONE
    ) else (
        echo [Notice] g++ was found but it is an older version that does not support C++20.
    )
)

echo [Error] No compatible C++20 compiler found (MSVC 2022/2019 or modern GCC 10+).
echo Please make sure Visual Studio 2022 with C++ Desktop Development is installed.
exit /b 1

:COMPILE_MSVC
echo [1/2] Initializing MSVC x64 from: "%VCVARS%"
call "%VCVARS%" >nul
cl /std:c++20 /O2 /EHsc /utf-8 /Iinclude %SRC_FILES% /Fe:CompilerProject_CPP.exe
goto DONE

:DONE
if exist "CompilerProject_CPP.exe" (
    echo.
    echo ========================================================
    echo Build successful: CompilerProject_CPP.exe created!
    echo ========================================================
    exit /b 0
) else (
    echo.
    echo [Error] Build failed: CompilerProject_CPP.exe was not created.
    exit /b 1
)
