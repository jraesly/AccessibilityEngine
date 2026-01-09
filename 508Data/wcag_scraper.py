import requests
from bs4 import BeautifulSoup
import re
import json

WCAG_URL = "https://www.w3.org/TR/WCAG22/"

def clean(text: str) -> str:
    return re.sub(r"\s+", " ", text or "").strip()

def extract_success_criteria():
    resp = requests.get(WCAG_URL)
    resp.raise_for_status()
    soup = BeautifulSoup(resp.text, "html.parser")

    # Prefer main content; fall back to body
    main = soup.find("main") or soup.find(id="main") or soup.body or soup

    # Flatten to text with newlines so headings / paragraphs break nicely
    full_text = main.get_text("\n", strip=True)

    # Split on "Success Criterion X.X.X Title" boundaries
    # This keeps the heading tokens as separate list items.
    pattern = r"(Success Criterion\s+\d\.\d\.\d+\s+[^\n]+)"
    parts = re.split(pattern, full_text)

    # parts[0] is preface before the first criterion; skip it
    it = iter(parts[1:])

    chunks = []

    for header, body in zip(it, it):
        header = clean(header)
        body = body.strip()

        # Parse "Success Criterion 1.1.1 Non-text Content"
        m = re.match(r"Success Criterion\s+(\d\.\d\.\d+)\s+(.*)", header)
        if not m:
            continue

        section_id = m.group(1)
        title = m.group(2)

        # Extract level: look for "(Level A)", "(Level AA)", "(Level AAA)"
        level_match = re.search(r"\(Level\s+([A]{1,3})\)", body)
        level = level_match.group(1) if level_match else None

        # Optionally remove the level line itself from the text
        body_clean = re.sub(r"\(Level\s+[A]{1,3}\)", "", body)
        body_clean = clean(body_clean)

        chunk = {
            "id": section_id,
            "section": section_id,
            "title": title,
            "level": level,
            "text": body_clean,
            "source": WCAG_URL,
        }

        chunks.append(chunk)

    return chunks

if __name__ == "__main__":
    sc_chunks = extract_success_criteria()
    print(f"Extracted {len(sc_chunks)} success criteria chunks.")

    with open("wcag22_html_chunks.json", "w", encoding="utf-8") as f:
        json.dump(sc_chunks, f, ensure_ascii=False, indent=2)

    # Quick sanity check
    for c in sc_chunks[:5]:
        print(f"{c['id']} {c['title']} [Level {c['level']}]")
        print(c["text"][:200], "...\n")
