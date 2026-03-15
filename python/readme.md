# OllamaPDF (Python)

## Требования

- Windows
- Python 3.12+
- Запущенный Ollama на `http://localhost:11434`
- Модель `mistral:latest` (или измените в `program.py`)

## Быстрый старт

1. Выполните `InitVenv.bat` (создаст `.venv`, активирует окружение и установит зависимости).
2. Выполните `Start.bat` для запуска приложения.

## Ручной запуск

Из каталога `python`:

```bat
.\.venv\Scripts\python.exe .\program.py
```

## Настройка данных

В `program.py` путь к PDF задаётся константой `DATA_PATH`.
По умолчанию:

```python
DATA_PATH = Path(r"d:\123\pdf")
```

Если папка не существует, приложение завершится с сообщением `Директория не существует`.
