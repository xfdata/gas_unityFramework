import hashlib
import json
import subprocess
from pathlib import Path
from typing import Dict, Iterable, List, Optional

try:
    from .code_kb_config import CodeKbConfig
except ImportError:
    from code_kb_config import CodeKbConfig


def read_text(path: Path, config: CodeKbConfig) -> str:
    """Read Unity source files that may use utf-8, utf-8-sig, or gb18030."""
    for encoding in config.text_encodings:
        try:
            return path.read_text(encoding=encoding)
        except UnicodeDecodeError:
            continue

    return path.read_text(errors="ignore")


def normalize_path(path: Path) -> str:
    return str(path).replace("\\", "/")


def sha1_short(text: str, length: int = 12) -> str:
    return hashlib.sha1(text.encode("utf-8", errors="ignore")).hexdigest()[:length]


def get_git_commit(root: Path) -> str:
    try:
        result = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            cwd=str(root),
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            check=False,
        )
        value = result.stdout.strip()
        return value or "unknown"
    except Exception:
        return "unknown"


def get_line_number(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1


def should_skip_path(path: Path, config: CodeKbConfig) -> bool:
    normalized = normalize_path(path).strip("/").lower()
    normalized_parts = {part.lower() for part in path.parts}
    padded = f"/{normalized}/"

    for excluded in config.exclude_dirs:
        normalized_excluded = excluded.strip("/").lower()
        if "/" in normalized_excluded:
            if f"/{normalized_excluded}/" in padded:
                return True
        elif normalized_excluded in normalized_parts:
            return True

    return False


def iter_csharp_files(
    root: Path,
    config: CodeKbConfig,
    include_dirs: Optional[List[str]] = None,
) -> Iterable[Path]:
    """Yield C# files under root, optionally limited to selected relative dirs."""
    search_roots = [root / include_dir for include_dir in include_dirs] if include_dirs else [root]

    for base in search_roots:
        if not base.exists():
            continue

        for path in base.rglob("*.cs"):
            if should_skip_path(path, config):
                continue
            yield path


def write_jsonl(path: Path, rows: Iterable[Dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)

    with path.open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
