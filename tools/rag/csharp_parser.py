import re
from typing import Dict, List, Tuple

try:
    from .code_kb_config import CodeKbConfig
    from .code_kb_io import get_line_number, sha1_short
    from .code_kb_metadata import (
        detect_performance_hints,
        extract_config_used,
        extract_events_used,
        extract_gameplay_tags_used,
        infer_module,
    )
except ImportError:
    from code_kb_config import CodeKbConfig
    from code_kb_io import get_line_number, sha1_short
    from code_kb_metadata import (
        detect_performance_hints,
        extract_config_used,
        extract_events_used,
        extract_gameplay_tags_used,
        infer_module,
    )


NAMESPACE_RE = re.compile(
    r"(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(?P<terminator>\{|;)"
)

CLASS_RE = re.compile(
    r"""
    (?m) ^\s*
    (?:\[[^\n]*\]\s*)*
    (?P<mods>
        (?:
            public|private|protected|internal|
            abstract|sealed|static|partial|readonly|unsafe|new
        )\s+
    )*
    (?P<kind>class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+
    (?P<name>[A-Za-z_]\w*)
    (?P<rest>[^{};]*)
    \{
    """,
    re.VERBOSE,
)


def build_code_mask(text: str) -> List[bool]:
    """Mark positions that are outside comments and string/char literals."""
    mask = [True] * len(text)
    i = 0
    state = "code"
    n = len(text)

    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ""

        if state == "code":
            if ch == "/" and nxt == "/":
                mask[i] = False
                mask[i + 1] = False
                state = "line_comment"
                i += 2
                continue
            if ch == "/" and nxt == "*":
                mask[i] = False
                mask[i + 1] = False
                state = "block_comment"
                i += 2
                continue
            if ch == "@" and nxt == '"':
                mask[i] = False
                mask[i + 1] = False
                state = "verbatim_string"
                i += 2
                continue
            if ch == "$" and nxt == '"':
                mask[i] = False
                mask[i + 1] = False
                state = "string"
                i += 2
                continue
            if ch == '"':
                mask[i] = False
                state = "string"
                i += 1
                continue
            if ch == "'":
                mask[i] = False
                state = "char"
                i += 1
                continue

        elif state == "line_comment":
            mask[i] = False
            if ch == "\n":
                state = "code"

        elif state == "block_comment":
            mask[i] = False
            if ch == "*" and nxt == "/":
                mask[i + 1] = False
                state = "code"
                i += 2
                continue

        elif state == "string":
            mask[i] = False
            if ch == "\\":
                if i + 1 < n:
                    mask[i + 1] = False
                i += 2
                continue
            if ch == '"':
                state = "code"

        elif state == "char":
            mask[i] = False
            if ch == "\\":
                if i + 1 < n:
                    mask[i + 1] = False
                i += 2
                continue
            if ch == "'":
                state = "code"

        elif state == "verbatim_string":
            mask[i] = False
            if ch == '"' and nxt == '"':
                mask[i + 1] = False
                i += 2
                continue
            if ch == '"':
                state = "code"

        i += 1

    return mask


def is_code_position(text: str, index: int, code_mask: List[bool] = None) -> bool:
    """Return true when index is outside comments and string/char literals."""
    if index < 0 or index >= len(text):
        return False
    if code_mask is None:
        code_mask = build_code_mask(text)
    return code_mask[index]


def find_namespace(text: str) -> str:
    namespaces = find_namespaces(text)
    return namespaces[0] if namespaces else ""


def find_namespaces(text: str) -> List[str]:
    code_mask = build_code_mask(text)
    namespaces: List[str] = []

    for match in NAMESPACE_RE.finditer(text):
        if is_code_position(text, match.start(), code_mask):
            namespace = match.group(1)
            if namespace not in namespaces:
                namespaces.append(namespace)

    return namespaces


def build_namespace_spans(text: str, code_mask: List[bool]) -> List[Tuple[int, int, str]]:
    spans: List[Tuple[int, int, str]] = []

    for match in NAMESPACE_RE.finditer(text):
        if not is_code_position(text, match.start(), code_mask):
            continue

        namespace = match.group(1)
        terminator = match.group("terminator")

        if terminator == ";":
            spans.append((match.end(), len(text), namespace))
            continue

        open_brace = match.end() - 1
        close_brace = find_matching_brace(text, open_brace, code_mask)
        if close_brace >= 0:
            spans.append((open_brace + 1, close_brace, namespace))

    return spans


def find_namespace_for_position(
    namespace_spans: List[Tuple[int, int, str]],
    index: int,
    fallback: str,
) -> str:
    matches = [span for span in namespace_spans if span[0] <= index <= span[1]]
    if not matches:
        return fallback

    return min(matches, key=lambda span: span[1] - span[0])[2]


def find_matching_brace(text: str, open_index: int, code_mask: List[bool] = None) -> int:
    """Find the closing brace matching text[open_index], ignoring comments and strings."""
    if open_index < 0 or open_index >= len(text) or text[open_index] != "{":
        return -1
    if code_mask is not None:
        if not is_code_position(text, open_index, code_mask):
            return -1

        depth = 0
        for i in range(open_index, len(text)):
            if not code_mask[i]:
                continue
            if text[i] == "{":
                depth += 1
            elif text[i] == "}":
                depth -= 1
                if depth == 0:
                    return i

        return -1

    depth = 0
    i = open_index
    n = len(text)
    state = "code"

    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ""

        if state == "code":
            if ch == "/" and nxt == "/":
                state = "line_comment"
                i += 2
                continue
            if ch == "/" and nxt == "*":
                state = "block_comment"
                i += 2
                continue
            if ch == "@" and nxt == '"':
                state = "verbatim_string"
                i += 2
                continue
            if ch == "$" and nxt == '"':
                state = "string"
                i += 2
                continue
            if ch == '"':
                state = "string"
                i += 1
                continue
            if ch == "'":
                state = "char"
                i += 1
                continue
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    return i

        elif state == "line_comment":
            if ch == "\n":
                state = "code"

        elif state == "block_comment":
            if ch == "*" and nxt == "/":
                state = "code"
                i += 2
                continue

        elif state == "string":
            if ch == "\\":
                i += 2
                continue
            if ch == '"':
                state = "code"

        elif state == "char":
            if ch == "\\":
                i += 2
                continue
            if ch == "'":
                state = "code"

        elif state == "verbatim_string":
            if ch == '"' and nxt == '"':
                i += 2
                continue
            if ch == '"':
                state = "code"

        i += 1

    return -1


def compact_signature(signature: str) -> str:
    """Clean a C# member signature and compress it into one line."""
    cleaned = []

    for line in signature.splitlines():
        s = line.strip()
        if not s or s.startswith("//") or s.startswith("///"):
            continue
        if s.startswith("[") and s.endswith("]"):
            continue
        cleaned.append(line)

    result = "\n".join(cleaned)
    result = re.sub(r"\s+", " ", result)
    return result.strip()


def find_previous_boundary(text: str, index: int) -> int:
    """Find the likely start of a member signature before an opening brace."""
    semi = text.rfind(";", 0, index)
    brace = text.rfind("}", 0, index)
    boundary = max(semi, brace)
    return boundary + 1 if boundary >= 0 else 0


def find_next_code_open_brace(
    text: str,
    start: int,
    code_mask: List[bool] = None,
) -> int:
    """Find the next opening brace that is outside comments and literals."""
    if code_mask is None:
        code_mask = build_code_mask(text)

    i = start
    while True:
        open_brace = text.find("{", i)
        if open_brace < 0:
            return -1
        if is_code_position(text, open_brace, code_mask):
            return open_brace
        i = open_brace + 1


def is_probably_method_signature(signature: str, config: CodeKbConfig) -> bool:
    sig = compact_signature(signature)

    if not sig or "(" not in sig or ")" not in sig:
        return False

    # Expression-bodied methods are not represented by a method body brace here.
    if "=>" in sig:
        return False

    first_word_match = re.match(r"([A-Za-z_]\w*)", sig)
    if first_word_match and first_word_match.group(1) in config.control_keywords:
        return False

    lowered = sig.lower()
    if re.search(r"\b(class|struct|interface|enum|record|delegate)\b", lowered):
        return False

    if re.search(
        r"\b(public|private|protected|internal|static|virtual|override|abstract|sealed|async|extern|unsafe|partial|new)\b",
        sig,
    ):
        return True

    return bool(re.search(r"[A-Za-z_]\w*\s*\(", sig))


def extract_method_name(signature: str) -> str:
    sig = compact_signature(signature)

    dtor = re.search(r"~\s*([A-Za-z_]\w*)\s*\(", sig)
    if dtor:
        return "~" + dtor.group(1)

    op = re.search(r"\boperator\s+([^\s(]+)\s*\(", sig)
    if op:
        return "operator_" + op.group(1)

    candidates = re.findall(r"([A-Za-z_]\w*)\s*\(", sig)
    if candidates:
        return candidates[-1]

    return "unknown"


def extract_inherits(rest: str) -> str:
    rest = rest.strip()
    if ":" not in rest:
        return ""

    return rest.split(":", 1)[1].strip()


def extract_methods_from_class(
    full_text: str,
    class_body_start: int,
    class_body_end: int,
    file: str,
    namespace: str,
    class_name: str,
    commit: str,
    config: CodeKbConfig,
) -> List[Dict]:
    body = full_text[class_body_start:class_body_end]
    body_code_mask = build_code_mask(body)
    methods: List[Dict] = []
    i = 0

    while i < len(body):
        open_brace = find_next_code_open_brace(body, i, body_code_mask)
        if open_brace < 0:
            break

        close_brace = find_matching_brace(body, open_brace, body_code_mask)
        if close_brace < 0:
            break

        sig_start = find_previous_boundary(body, open_brace)
        raw_signature = body[sig_start:open_brace]
        signature = compact_signature(raw_signature)

        if is_probably_method_signature(signature, config):
            method_name = extract_method_name(signature)
            global_start = class_body_start + sig_start
            global_end = class_body_start + close_brace

            content = full_text[global_start : global_end + 1]
            start_line = get_line_number(full_text, global_start)
            end_line = get_line_number(full_text, global_end)

            symbol = f"{class_name}.{method_name}"
            module = infer_module(file, class_name, symbol, config)
            chunk_id = f"raw.method.{symbol}.{sha1_short(file + symbol + str(start_line))}"

            methods.append(
                {
                    "id": chunk_id,
                    "type": "raw_method",
                    "language": "csharp",
                    "module": module,
                    "file": file,
                    "namespace": namespace,
                    "class": class_name,
                    "symbol": symbol,
                    "method": method_name,
                    "signature": signature,
                    "startLine": start_line,
                    "endLine": end_line,
                    "content": content,
                    "contentHash": sha1_short(content),
                    "commit": commit,
                    "eventsUsed": extract_events_used(content, config),
                    "configsUsed": extract_config_used(content, config),
                    "gameplayTagsUsed": extract_gameplay_tags_used(content, config),
                    "performanceHints": detect_performance_hints(content, config),
                    "sourceRefs": [
                        {
                            "file": file,
                            "startLine": start_line,
                            "endLine": end_line,
                        }
                    ],
                }
            )

        # Skip the full brace block so nested if/for blocks are not treated as methods.
        i = close_brace + 1

    return methods


def extract_classes(
    text: str,
    file: str,
    namespace: str,
    commit: str,
    include_class_content: bool,
    config: CodeKbConfig,
) -> Tuple[List[Dict], List[Dict]]:
    class_chunks: List[Dict] = []
    method_chunks: List[Dict] = []
    code_mask = build_code_mask(text)
    namespace_spans = build_namespace_spans(text, code_mask)

    for match in CLASS_RE.finditer(text):
        if not is_code_position(text, match.start(), code_mask):
            continue

        kind = match.group("kind").replace(" ", "_")
        class_name = match.group("name")
        rest = match.group("rest") or ""
        class_namespace = find_namespace_for_position(namespace_spans, match.start(), namespace)

        open_brace = match.end() - 1
        close_brace = find_matching_brace(text, open_brace, code_mask)
        if close_brace < 0:
            continue

        start_line = get_line_number(text, match.start())
        end_line = get_line_number(text, close_brace)
        class_content = text[match.start() : close_brace + 1]

        inherits = extract_inherits(rest)
        module = infer_module(file, class_name, class_name, config)

        class_chunk = {
            "id": f"raw.{kind}.{class_name}.{sha1_short(file + class_name + str(start_line))}",
            "type": f"raw_{kind}",
            "language": "csharp",
            "module": module,
            "file": file,
            "namespace": class_namespace,
            "class": class_name,
            "symbol": class_name,
            "kind": kind,
            "inherits": inherits,
            "startLine": start_line,
            "endLine": end_line,
            "contentHash": sha1_short(class_content),
            "commit": commit,
            "sourceRefs": [
                {
                    "file": file,
                    "startLine": start_line,
                    "endLine": end_line,
                }
            ],
            "content": class_content if include_class_content else "",
        }

        class_chunks.append(class_chunk)

        if kind in {"class", "struct", "record", "record_class", "record_struct"}:
            method_chunks.extend(
                extract_methods_from_class(
                    full_text=text,
                    class_body_start=open_brace + 1,
                    class_body_end=close_brace,
                    file=file,
                    namespace=class_namespace,
                    class_name=class_name,
                    commit=commit,
                    config=config,
                )
            )

    return class_chunks, method_chunks
