import pdfplumber
import re
import json

PDF_PATH = "wcag22.pdf"  # change to your actual file name

def clean(txt):
    return re.sub(r"\s+", " ", txt).strip()

chunks = []

with pdfplumber.open(PDF_PATH) as pdf:
    full_text = ""
    for page in pdf.pages:
        full_text += "\n" + page.extract_text()

# Split by success criteria like "1.1.1"
# Keep headings like "1.1.1 Non-text Content"
pattern = r"(\d\.\d\.\d+\s+[^\n]+)"

parts = re.split(pattern, full_text)

# parts will alternate: [before-first, "1.1.1 Title", text, "1.2.1 Title", text, ...]
it = iter(parts)
_ = next(it)  # skip content before first match

for header, body in zip(it, it):
    header_txt = clean(header)
    # header like "1.1.1 Non-text Content"
    m = re.match(r"(\d\.\d\.\d+)\s+(.*)", header_txt)
    if not m:
        continue
    section = m.group(1)
    title = m.group(2)
    text = clean(body)

    chunks.append({
        "id": section,
        "section": section,
        "title": title,
        "text": text,
        "source": "WCAG 2.2 PDF"
    })

with open("wcag22_pdf_chunks.json", "w", encoding="utf-8") as f:
    json.dump(chunks, f, ensure_ascii=False, indent=2)

print(f"Extracted {len(chunks)} chunks from WCAG 2.2 PDF")
