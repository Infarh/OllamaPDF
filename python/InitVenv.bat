@echo off
setlocal
chcp 65001 >nul

cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
    echo [1/3] Создание виртуального окружения...
    py -m venv .venv
    if errorlevel 1 goto :error
) else (
    echo [1/3] Виртуальное окружение уже существует.
)

echo [2/3] Активация виртуального окружения...
call ".venv\Scripts\activate.bat"
if errorlevel 1 goto :error

echo [3/3] Установка зависимостей...
python -m pip install --upgrade pip
if errorlevel 1 goto :error

if exist "requirements.txt" (
    python -m pip install -r requirements.txt
    if errorlevel 1 goto :error
) else (
    echo Файл requirements.txt не найден. Установка зависимостей пропущена.
)

echo.
echo Готово. Окружение инициализировано и активировано для текущего окна.
echo Для запуска используйте: Start.bat
goto :eof

:error
echo.
echo Ошибка при настройке окружения.
exit /b 1
