from pathlib import Path
from typing import Dict, List, Optional

try:
    from .code_kb_config import CodeKbConfig
    from .code_kb_io import (
        get_git_commit,
        iter_csharp_files,
        normalize_path,
        read_text,
        sha1_short,
        write_jsonl,
    )
    from .csharp_parser import extract_classes, find_namespace, find_namespaces
except ImportError:
    from code_kb_config import CodeKbConfig
    from code_kb_io import (
        get_git_commit,
        iter_csharp_files,
        normalize_path,
        read_text,
        sha1_short,
        write_jsonl,
    )
    from csharp_parser import extract_classes, find_namespace, find_namespaces


def build_raw_knowledge(
    project_root: Path,
    out_dir: Path,
    include_dirs: Optional[List[str]],
    include_class_content: bool,
    config: CodeKbConfig,
) -> None:
    project_root = project_root.resolve()
    out_dir = out_dir.resolve()

    commit = get_git_commit(project_root)
    code_files: List[Dict] = []
    raw_chunks: List[Dict] = []

    cs_files = list(iter_csharp_files(project_root, config, include_dirs=include_dirs))

    for cs_file in cs_files:
        rel_file = normalize_path(cs_file.relative_to(project_root))
        text = read_text(cs_file, config)

        namespace = find_namespace(text)
        namespaces = find_namespaces(text)
        line_count = text.count("\n") + 1

        code_files.append(
            {
                "file": rel_file,
                "language": "csharp",
                "namespace": namespace,
                "namespaces": namespaces,
                "lineCount": line_count,
                "contentHash": sha1_short(text),
                "commit": commit,
            }
        )

        class_chunks, method_chunks = extract_classes(
            text=text,
            file=rel_file,
            namespace=namespace,
            commit=commit,
            include_class_content=include_class_content,
            config=config,
        )

        raw_chunks.extend(class_chunks)
        raw_chunks.extend(method_chunks)

    write_jsonl(out_dir / "code_files.jsonl", code_files)
    write_jsonl(out_dir / "raw_chunks.jsonl", raw_chunks)

    print_summary(project_root, out_dir, commit, code_files, raw_chunks)


def print_summary(
    project_root: Path,
    out_dir: Path,
    commit: str,
    code_files: List[Dict],
    raw_chunks: List[Dict],
) -> None:
    print("Build raw code knowledge done.")
    print(f"Project root: {project_root}")
    print(f"Output dir:   {out_dir}")
    print(f"Git commit:   {commit}")
    print(f"C# files:     {len(code_files)}")
    print(f"Raw chunks:   {len(raw_chunks)}")

    type_count: Dict[str, int] = {}
    for chunk in raw_chunks:
        chunk_type = chunk.get("type", "unknown")
        type_count[chunk_type] = type_count.get(chunk_type, 0) + 1

    print("Chunk types:")
    for chunk_type, count in sorted(type_count.items()):
        print(f"  {chunk_type}: {count}")
