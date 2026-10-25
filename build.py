"""Build Darkroom.html from src/darkroom.html and keep every version.

    python build.py

Puts the release at Darkroom.html and a copy at Versions/Archive/Darkroom-<VERSION>.html.
Raise VERSION in src/darkroom.html first: a version that's already archived is refused, so no build is ever lost.
"""
import re, shutil, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SRC = HERE / "src" / "darkroom.html"

version = re.search(r'^const VERSION = "(\d+\.\d+\.\d+)";', SRC.read_text("utf-8"), re.M).group(1)
archive = HERE / "Versions" / "Archive" / f"Darkroom-{version}.html"
if archive.exists():
    sys.exit(f"Version {version} is already archived. Raise VERSION in src/darkroom.html, then build again.")

archive.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2(SRC, archive)
shutil.copy2(archive, HERE / "Darkroom.html")
print(f"Built Darkroom {version}: Darkroom.html (copy kept in {archive.relative_to(HERE)})")
