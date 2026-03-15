from __future__ import annotations

import json
from pathlib import Path
from typing import Iterator

import requests
from pypdf import PdfReader


MODEL_NAME = "mistral:latest"
DATA_PATH = Path(r"d:\123\pdf")

MAX_TEXT_LEN = 5000
MAX_ATTEMPTS = 3
REQUEST_TIMEOUT_SEC = 30

OLLAMA_URL = "http://localhost:11434/api/generate"


def to_data_len(num_bytes: int) -> tuple[float, str]:
	"""Преобразует размер в байтах в человеко-читаемое значение.

	Параметры:
		num_bytes (int): Размер данных в байтах.

	Возвращаемое значение:
		tuple[float, str]: Кортеж из числового значения размера и единицы измерения
		("Б", "кБ", "МБ" или "ГБ").
	"""
	if num_bytes >= 1 << 30:
		return num_bytes / float(1 << 30), "ГБ"
	if num_bytes >= 1 << 20:
		return num_bytes / float(1 << 20), "МБ"
	if num_bytes >= 1 << 10:
		return num_bytes / float(1 << 10), "кБ"
	return float(num_bytes), "Б"


def ident_lines(text: str, indent: str) -> str:
	"""Добавляет отступ к каждой непустой строке текста.

	Параметры:
		text (str): Текст, который нужно форматировать.
		indent (str): Строка-отступ, добавляемая в начало непустых строк.

	Возвращаемое значение:
		str: Текст с добавленными отступами для непустых строк.
	"""
	out: list[str] = []
	for line in text.splitlines():
		out.append(f"{indent}{line}" if line else "")
	return "\n".join(out)


def cut_text_at_sentence_boundary(text: str, max_len: int) -> str:
	"""Ограничивает длину текста и по возможности обрезает по границе предложения.

	Параметры:
		text (str): Исходный текст.
		max_len (int): Максимально допустимая длина результата.

	Возвращаемое значение:
		str: Обрезанный текст длиной не более max_len символов.
	"""
	if len(text) <= max_len:
		return text
	cut_text = text[:max_len]
	last_sentence_end = max(cut_text.rfind("."), cut_text.rfind("!"), cut_text.rfind("?"))
	if last_sentence_end > max_len // 2:
		return cut_text[: last_sentence_end + 1]
	return cut_text


def extract_pdf_text(pdf_path: Path, max_len: int) -> str:
	"""Извлекает текст из PDF с ограничением максимальной длины.

	Параметры:
		pdf_path (Path): Путь к PDF-файлу.
		max_len (int): Максимальная длина возвращаемого текста.

	Возвращаемое значение:
		str: Текст из PDF, ограниченный max_len и обрезанный по границе предложения.
	"""
	reader = PdfReader(str(pdf_path), strict=False)
	chunks: list[str] = []
	current_len = 0

	for page in reader.pages:
		page_text = page.extract_text() or ""
		if not page_text:
			continue

		chunks.append(page_text)
		current_len += len(page_text)

		if current_len >= max_len:
			break

	return cut_text_at_sentence_boundary("".join(chunks), max_len)


def request_ollama(prompt: str) -> str | None:
	"""Отправляет запрос в Ollama и возвращает текст ответа модели.

	Параметры:
		prompt (str): Подготовленный промпт для модели.

	Возвращаемое значение:
		str | None: Содержимое поля response из ответа Ollama, либо None,
		если запрос не удался после всех попыток.
	"""
	payload = {
		"model": MODEL_NAME,
		"prompt": prompt,
		"format": "json",
		"stream": False,
	}

	for attempt in range(1, MAX_ATTEMPTS + 1):
		try:
			response = requests.post(OLLAMA_URL, json=payload, timeout=REQUEST_TIMEOUT_SEC)
			response.raise_for_status()
			body = response.json()
			return body.get("response")
		except (requests.Timeout, requests.ConnectionError):
			if attempt < MAX_ATTEMPTS:
				print(f"   [{attempt}/{MAX_ATTEMPTS}] Таймаут запроса. Повтор...")
		except requests.RequestException as ex:
			if attempt < MAX_ATTEMPTS:
				print(f"   [{attempt}/{MAX_ATTEMPTS}] Ошибка HTTP: {ex}. Повтор...")

	return None


def build_prompt(book_text: str) -> str:
	"""Формирует промпт для генерации имени файла и краткого описания книги.

	Параметры:
		book_text (str): Извлечённый текст книги из PDF.

	Возвращаемое значение:
		str: Полный текст промпта для отправки в Ollama.
	"""
	return f"""Analyze the PDF book text below and provide a filename and a summary.

Filename requirements:
- Use only Latin letters, digits, and underscores.
- No spaces or special characters.
- No file extension.
- Transliterate non-Latin titles to Latin.
- Avoid generic words like "book", "file", "pdf".
- Maximum 100 characters.

Summary requirements:
- Concise and informative, maximum 500 characters.
- Avoid clichés. Focus on unique aspects of the book content.
- No template phrases like "This book is about...". Provide specific details.
- Write the summary in Russian.

Respond ONLY with a valid JSON object in this exact format:
{{"filename": "suggested_filename", "summary": "краткое описание на русском"}}

Book text:
{book_text}
"""


def main() -> int:
	"""Точка входа: обрабатывает PDF-файлы и запрашивает у модели метаданные.

	Параметры:
		Нет.

	Возвращаемое значение:
		int: Код завершения процесса (0 при успешном завершении, -1 если
		не найдена директория с входными PDF-файлами).
	"""
	if not DATA_PATH.exists() or not DATA_PATH.is_dir():
		print("Директория не существует")
		return -1

	for data_file in DATA_PATH.rglob("*.pdf"):
		rel_path = data_file.relative_to(DATA_PATH)
		size, unit = to_data_len(data_file.stat().st_size)

		print(f"Обработка файла {rel_path}")
		print(f"                   размер: {size:6.2f} {unit}")

		reader = PdfReader(str(data_file), strict=False)
		print(f"            число страниц: {len(reader.pages)}")

		file_text = extract_pdf_text(data_file, MAX_TEXT_LEN)

		print("   Запрос имени и описания у модели...")

		prompt = build_prompt(file_text)
		model_result = request_ollama(prompt)

		if model_result is None:
			print(f"   Не удалось получить ответ от модели за {MAX_ATTEMPTS} попытки(-ок). Файл пропущен.")
			print()
			continue

		suggested_name = ""
		summary = ""

		try:
			parsed = json.loads(model_result)
			suggested_name = str(parsed.get("filename", "")).strip()
			summary = str(parsed.get("summary", "")).strip()
		except json.JSONDecodeError:
			suggested_name = model_result.strip()
			print(f"Ошибка при разборе JSON-ответа модели. Текст ответа: {model_result}")

		print(f"    предложенное название: {suggested_name}{data_file.suffix}")
		print("         краткое описание:")
		print(ident_lines(summary, "        "))
		print()

	return 0


if __name__ == "__main__":
	raise SystemExit(main())
