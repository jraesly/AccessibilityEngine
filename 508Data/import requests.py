import requests
from bs4 import BeautifulSoup
import json

# 1) Fetch the page
url = "https://www.access-board.gov/ict/"
resp = requests.get(url)
resp.raise_for_status()

# 2) Parse the HTML
soup = BeautifulSoup(resp.text, "html.parser")

# 3) Find the main article/content section
#    This selector may vary; adjust if needed
content = soup.find("main") or soup.body

# 4) Extract visible text paragraphs
paragraphs = []
for el in content.find_all(["h1","h2","h3","p","li"]):
    text = el.get_text(separator=" ", strip=True)
    if text:
        paragraphs.append(text)

# 5) Build a JSON structure for indexing
#    You can modify chunking strategy as needed
chunks = []
for i, para in enumerate(paragraphs):
    chunk = {
        "id": f"access-board-ict-{i}",
        "source": url,
        "text": para,
        "type": el.name if el else "p"
    }
    chunks.append(chunk)

# 6) Write JSON file for upload to Azure AI Search
with open("508_ict_chunks.json", "w", encoding="utf-8") as f:
    json.dump(chunks, f, indent=2, ensure_ascii=False)

print(f"Extracted {len(chunks)} chunks.")
