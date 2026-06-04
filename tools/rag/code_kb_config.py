import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple


DEFAULT_CONFIG_PATH = Path(__file__).with_name("config")
CONFIG_FILES = (
    "project.json",
    "parser.json",
    "metadata.json",
)


@dataclass(frozen=True)
class CodeKbConfig:
    default_project_root: Optional[str] = None
    default_out_dir: str = "ProjectKnowledge/raw"
    include_dirs: Optional[List[str]] = None
    text_encodings: Sequence[str] = ("utf-8-sig", "utf-8", "gb18030")
    exclude_dirs: Sequence[str] = field(default_factory=tuple)
    control_keywords: Sequence[str] = field(default_factory=tuple)
    module_rules: Sequence[Tuple[str, str]] = field(default_factory=tuple)
    event_patterns: Sequence[str] = field(default_factory=tuple)
    config_patterns: Sequence[str] = field(default_factory=tuple)
    gameplay_tag_patterns: Sequence[str] = field(default_factory=tuple)
    performance_checks: Sequence[Tuple[str, str]] = field(default_factory=tuple)


def _as_tuple_pairs(values: Sequence[Sequence[str]]) -> Tuple[Tuple[str, str], ...]:
    pairs = []
    for value in values:
        if len(value) != 2:
            raise ValueError(f"Expected a pair, got: {value}")
        pairs.append((value[0], value[1]))
    return tuple(pairs)


def _validate_patterns(name: str, patterns: Sequence[str]) -> None:
    for pattern in patterns:
        try:
            re.compile(pattern)
        except re.error as exc:
            raise ValueError(f"Invalid regex in {name}: {pattern!r}: {exc}") from exc


def _load_config_data(path: Path) -> Dict:
    if path.is_file():
        return json.loads(path.read_text(encoding="utf-8"))

    if not path.is_dir():
        raise FileNotFoundError(f"Config path does not exist: {path}")

    data: Dict = {}
    for file_name in CONFIG_FILES:
        file_path = path / file_name
        if not file_path.exists():
            raise FileNotFoundError(f"Missing config file: {file_path}")
        data.update(json.loads(file_path.read_text(encoding="utf-8")))

    return data


def load_config(path: Path = DEFAULT_CONFIG_PATH) -> CodeKbConfig:
    data = _load_config_data(path)

    include_dirs = data.get("include_dirs")
    if include_dirs is not None:
        include_dirs = [str(x).strip().replace("\\", "/") for x in include_dirs if str(x).strip()]

    event_patterns = tuple(data.get("event_patterns", ()))
    config_patterns = tuple(data.get("config_patterns", ()))
    gameplay_tag_patterns = tuple(data.get("gameplay_tag_patterns", ()))
    performance_checks = _as_tuple_pairs(data.get("performance_checks", ()))

    _validate_patterns("event_patterns", event_patterns)
    _validate_patterns("config_patterns", config_patterns)
    _validate_patterns("gameplay_tag_patterns", gameplay_tag_patterns)
    _validate_patterns("performance_checks", [pattern for _, pattern in performance_checks])

    return CodeKbConfig(
        default_project_root=data.get("default_project_root"),
        default_out_dir=data.get("default_out_dir", "ProjectKnowledge/raw"),
        include_dirs=include_dirs,
        text_encodings=tuple(data.get("text_encodings", ("utf-8-sig", "utf-8", "gb18030"))),
        exclude_dirs=tuple(str(x).strip().replace("\\", "/") for x in data.get("exclude_dirs", ()) if str(x).strip()),
        control_keywords=tuple(data.get("control_keywords", ())),
        module_rules=_as_tuple_pairs(data.get("module_rules", ())),
        event_patterns=event_patterns,
        config_patterns=config_patterns,
        gameplay_tag_patterns=gameplay_tag_patterns,
        performance_checks=performance_checks,
    )
