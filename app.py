import os
import re
import threading
import subprocess
from pathlib import Path
import tkinter as tk
from tkinter import ttk, filedialog, messagebox

import pymupdf
from docx import Document
from rapidfuzz import fuzz


ARABIC_DIACRITICS = re.compile(r"[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED]")
WHITESPACE = re.compile(r"\s+")

def normalize_arabic(text: str) -> str:
    if not text:
        return ""
    text = ARABIC_DIACRITICS.sub("", text)
    text = text.replace("ـ", "")
    text = (text
            .replace("أ", "ا")
            .replace("إ", "ا")
            .replace("آ", "ا")
            .replace("ٱ", "ا")
            .replace("ى", "ي")
            .replace("ة", "ه"))
    # Normalize Arabic-Indic digits and Persian digits to ASCII.
    trans = str.maketrans(
        "٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹",
        "01234567890123456789"
    )
    text = text.translate(trans)
    text = WHITESPACE.sub(" ", text)
    return text.strip().casefold()

def compact(text: str) -> str:
    return re.sub(r"\s+", "", normalize_arabic(text))

def context(text: str, start: int, end: int, radius=260) -> str:
    a = max(0, start - radius)
    b = min(len(text), end + radius)
    return text[a:b].replace("\n", " ").strip()

def extract_docx(path: Path):
    doc = Document(str(path))
    parts = []
    for p in doc.paragraphs:
        if p.text.strip():
            parts.append(p.text)
    # Include table cells because legal documents may put case data in tables.
    for table in doc.tables:
        for row in table.rows:
            for cell in row.cells:
                if cell.text.strip():
                    parts.append(cell.text)
    return [(1, "\n".join(parts))]

def extract_pdf(path: Path, use_ocr=False, tesseract_path=""):
    pages = []
    doc = pymupdf.open(str(path))
    try:
        if tesseract_path:
            # PyMuPDF uses Tesseract through its OCR support.
            os.environ["PATH"] = str(Path(tesseract_path).parent) + os.pathsep + os.environ.get("PATH", "")
        for i, page in enumerate(doc):
            text = page.get_text("text", sort=True)
            if use_ocr and len(text.strip()) < 20:
                try:
                    tp = page.get_textpage_ocr(language="ara+eng", dpi=200, full=True)
                    text = page.get_text("text", textpage=tp, sort=True)
                except Exception:
                    # Keep extracted text if OCR is unavailable.
                    pass
            pages.append((i + 1, text))
    finally:
        doc.close()
    return pages

def extract(path: Path, use_ocr=False, tesseract_path=""):
    if path.suffix.lower() == ".pdf":
        return extract_pdf(path, use_ocr, tesseract_path)
    if path.suffix.lower() == ".docx":
        return extract_docx(path)
    return []

class SearchApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Arabic PDF + DOCX Search")
        self.geometry("1180x760")
        self.minsize(900, 600)

        self.folder = tk.StringVar()
        self.query = tk.StringVar()
        self.mode = tk.StringVar(value="normalized")
        self.use_ocr = tk.BooleanVar(value=False)
        self.tesseract = tk.StringVar()
        self.status = tk.StringVar(value="Choose a folder and index your documents.")
        self.index = []  # (path, page, original_text, normalized_text)

        self.build_ui()

    def build_ui(self):
        pad = {"padx": 10, "pady": 6}

        top = ttk.Frame(self)
        top.pack(fill="x", **pad)

        ttk.Label(top, text="Documents folder:").pack(side="left")
        ttk.Entry(top, textvariable=self.folder).pack(side="left", fill="x", expand=True, padx=8)
        ttk.Button(top, text="Choose Folder", command=self.choose_folder).pack(side="left")
        ttk.Button(top, text="Index Files", command=self.start_index).pack(side="left", padx=5)

        opts = ttk.Frame(self)
        opts.pack(fill="x", **pad)

        ttk.Label(opts, text="Search:").pack(side="left")
        q = ttk.Entry(opts, textvariable=self.query, font=("Segoe UI", 12))
        q.pack(side="left", fill="x", expand=True, padx=8)
        q.bind("<Return>", lambda e: self.search())

        ttk.Button(opts, text="Search", command=self.search).pack(side="left")

        ttk.Label(opts, text="Mode:").pack(side="left", padx=(12, 4))
        ttk.Combobox(
            opts, textvariable=self.mode, state="readonly", width=18,
            values=("normalized", "fuzzy")
        ).pack(side="left")

        ocr = ttk.Frame(self)
        ocr.pack(fill="x", **pad)
        ttk.Checkbutton(
            ocr, text="OCR scanned PDFs when extracted text is missing",
            variable=self.use_ocr
        ).pack(side="left")
        ttk.Label(ocr, text="Tesseract path (optional):").pack(side="left", padx=(20, 4))
        ttk.Entry(ocr, textvariable=self.tesseract, width=45).pack(side="left")
        ttk.Button(ocr, text="Browse", command=self.choose_tesseract).pack(side="left", padx=5)

        ttk.Label(self, textvariable=self.status).pack(fill="x", **pad)

        columns = ("file", "page", "score", "context")
        self.tree = ttk.Treeview(self, columns=columns, show="headings")
        self.tree.heading("file", text="File")
        self.tree.heading("page", text="Page")
        self.tree.heading("score", text="Match")
        self.tree.heading("context", text="Context")
        self.tree.column("file", width=280)
        self.tree.column("page", width=70, anchor="center")
        self.tree.column("score", width=80, anchor="center")
        self.tree.column("context", width=680)
        self.tree.pack(fill="both", expand=True, padx=10, pady=(4, 0))
        self.tree.bind("<Double-1>", self.open_result)

        scroll = ttk.Scrollbar(self, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        scroll.place(relx=0.985, rely=0.28, relheight=0.62)

    def choose_folder(self):
        p = filedialog.askdirectory(title="Choose folder containing PDFs/DOCX")
        if p:
            self.folder.set(p)

    def choose_tesseract(self):
        p = filedialog.askopenfilename(
            title="Select tesseract.exe",
            filetypes=[("Tesseract", "tesseract.exe"), ("All files", "*.*")]
        )
        if p:
            self.tesseract.set(p)

    def start_index(self):
        if not self.folder.get():
            messagebox.showwarning("Folder required", "Choose a documents folder first.")
            return
        self.tree.delete(*self.tree.get_children())
        threading.Thread(target=self.index_documents, daemon=True).start()

    def index_documents(self):
        folder = Path(self.folder.get())
        paths = [
            p for p in folder.rglob("*")
            if p.is_file() and p.suffix.lower() in {".pdf", ".docx"}
        ]
        self.index = []
        total = len(paths)

        for n, path in enumerate(paths, 1):
            try:
                pages = extract(
                    path,
                    use_ocr=self.use_ocr.get(),
                    tesseract_path=self.tesseract.get().strip()
                )
                for page_no, text in pages:
                    if text.strip():
                        self.index.append((str(path), page_no, text, normalize_arabic(text)))
            except Exception as e:
                print(f"ERROR: {path}: {e}")
            self.after(0, self.status.set, f"Indexing {n}/{total}: {path.name}")

        self.after(
            0, self.status.set,
            f"Indexed {len(self.index)} searchable pages/sections from {total} files."
        )

    def search(self):
        q = self.query.get().strip()
        if not q:
            return
        if not self.index:
            messagebox.showinfo("No index", "Index the document folder first.")
            return

        nq = normalize_arabic(q)
        cq = compact(q)
        mode = self.mode.get()
        results = []

        for path, page, original, norm in self.index:
            score = 0
            pos = -1

            if nq in norm:
                score = 100
                pos = norm.find(nq)
            elif cq and cq in compact(original):
                score = 99
                pos = 0
            elif mode == "fuzzy":
                # Compare against sliding word windows so long legal phrases work.
                words = norm.split()
                qwords = nq.split()
                if qwords and words:
                    window = len(qwords)
                    best = 0
                    best_pos = 0
                    for i in range(max(1, len(words) - window + 1)):
                        candidate = " ".join(words[i:i + window + 4])
                        s = fuzz.partial_ratio(nq, candidate)
                        if s > best:
                            best = s
                            best_pos = i
                    if best >= 72:
                        score = best
                        # Context is generated from original text below.
                        pos = 0

            if score:
                # Map approximate normalized position back to original text.
                if pos >= 0 and pos < len(norm):
                    prefix = norm[:pos]
                    ratio = len(prefix) / max(1, len(norm))
                    orig_pos = int(ratio * len(original))
                else:
                    orig_pos = 0
                results.append((score, path, page, context(original, orig_pos, orig_pos + len(q))))

        results.sort(key=lambda x: (-x[0], x[1], x[2]))
        self.tree.delete(*self.tree.get_children())

        for score, path, page, ctx in results[:500]:
            self.tree.insert("", "end", values=(path, page, f"{score:.0f}%", ctx))

        self.status.set(f"Found {len(results)} matching pages/sections.")

    def open_result(self, _event=None):
        item = self.tree.focus()
        if not item:
            return
        path = self.tree.item(item, "values")[0]
        try:
            os.startfile(path)
        except Exception:
            subprocess.Popen(["explorer", "/select,", path])

if __name__ == "__main__":
    SearchApp().mainloop()
