# Arabic PDF + DOCX Search

A local Windows desktop application for searching recursively through PDF and DOCX files.

## Features
- Searches `.pdf` and `.docx`
- Recursive folder scanning
- Arabic normalization:
  - أ / إ / آ -> ا
  - ة -> ه
  - ى -> ي
  - removes tashkeel and tatweel
  - normalizes whitespace
- Exact/normalized matching
- Fuzzy matching for OCR/spelling differences
- PDF page numbers in results
- Shows surrounding context
- Opens the source file from the result
- Re-indexes with one click
- Shows indexing progress while scanning the folder
- No cloud upload: documents stay on your computer

## Important
Text PDFs are searched directly. Scanned/image-only PDFs need OCR.

PyMuPDF supports PDF text extraction and OCR through `get_textpage_ocr()`.
For reliable Arabic OCR, install Tesseract and the Arabic language data (`ara`), then enable OCR in the application.

## Windows setup

1. Install Python 3.11+.
2. Open CMD in this folder.
3. Create a virtual environment:

   `py -m venv .venv`

4. Activate it:

   `.venv\Scripts\activate`

5. Install packages:

   `pip install -r requirements.txt`

6. Run:

   `python app.py`

## OCR setup

Install Tesseract OCR and make sure `tesseract.exe` is available in PATH.

Check:

`tesseract --version`

Check Arabic:

`tesseract --list-langs`

You should see:

`ara`

If Tesseract is installed elsewhere, put its executable path in the application's Settings/OCR field.

## Usage

1. Click "Choose Folder".
2. Click "Index Files".
3. Enter a phrase such as:

   `صدر الحكم الاتى في القضية رقم 6871 لسنة 2025`

4. Choose:
   - Exact / normalized search for normal documents.
   - Fuzzy search if OCR contains small errors.
5. Double-click a result to open the file.
