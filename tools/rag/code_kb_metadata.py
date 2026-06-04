import re
from typing import List

try:
    from .code_kb_config import CodeKbConfig
except ImportError:
    from code_kb_config import CodeKbConfig


def infer_module(file: str, class_name: str, symbol: str, config: CodeKbConfig) -> str:
    haystack = f"{file} {class_name} {symbol}".lower()

    for keyword, module in config.module_rules:
        if keyword in haystack:
            return module

    return "Unknown"


def extract_matches(content: str, patterns: List[str]) -> List[str]:
    result = set()

    for pattern in patterns:
        for match in re.findall(pattern, content):
            if isinstance(match, tuple):
                result.update(item for item in match if item)
            else:
                result.add(match)

    return sorted(result)


def extract_events_used(content: str, config: CodeKbConfig) -> List[str]:
    return extract_matches(content, list(config.event_patterns))


def extract_config_used(content: str, config: CodeKbConfig) -> List[str]:
    return extract_matches(content, list(config.config_patterns))


def extract_gameplay_tags_used(content: str, config: CodeKbConfig) -> List[str]:
    return extract_matches(content, list(config.gameplay_tag_patterns))


def detect_performance_hints(content: str, config: CodeKbConfig) -> List[str]:
    hints = []

    for name, pattern in config.performance_checks:
        if re.search(pattern, content):
            hints.append(name)

    return hints
