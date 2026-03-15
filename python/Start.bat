@echo off
setlocal

cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
    echo Виртуальное окружение не найдено.
    echo Сначала выполните InitVenv.bat
    exit /b 1
)

echo Запуск приложения...
".venv\Scripts\python.exe" ".\program.py"
set EXIT_CODE=%ERRORLEVEL%

if not "%EXIT_CODE%"=="0" (
    echo.
    echo Приложение завершилось с кодом %EXIT_CODE%.
)

exit /b %EXIT_CODE%
