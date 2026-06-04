import argparse
from pathlib import Path
from typing import List, Optional

try:
    from .code_kb_builder import build_raw_knowledge
    from .code_kb_config import DEFAULT_CONFIG_PATH, load_config
except ImportError:
    from code_kb_builder import build_raw_knowledge
    from code_kb_config import DEFAULT_CONFIG_PATH, load_config


def parse_include_dirs(value: Optional[str]) -> Optional[List[str]]:
    if not value:
        return None

    parts = [x.strip().replace("\\", "/") for x in value.split(",")]
    return [x for x in parts if x]


def is_unity_project(path: Path) -> bool:
    return (path / "Assets").is_dir() and (path / "ProjectSettings").is_dir()


def resolve_config_project_root(value: str, tools_dir: Path) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path
    return tools_dir / path


def find_sibling_unity_project(tools_dir: Path) -> Optional[Path]:
    parent = tools_dir.parent
    candidates = [
        path
        for path in parent.iterdir()
        if path.is_dir() and path.resolve() != tools_dir.resolve() and is_unity_project(path)
    ]

    if len(candidates) == 1:
        return candidates[0]

    if is_unity_project(parent):
        return parent

    return None


def resolve_project_root(project_root_arg: Optional[str], config_project_root: Optional[str]) -> Path:
    tools_dir = Path(__file__).resolve().parents[1]

    if project_root_arg:
        return Path(project_root_arg)

    if config_project_root:
        return resolve_config_project_root(config_project_root, tools_dir)

    sibling_project = find_sibling_unity_project(tools_dir)
    if sibling_project:
        return sibling_project

    raise SystemExit(
        "Unity project root was not provided and no sibling Unity project was found. "
        "Pass project_root, or set default_project_root in config/project.json."
    )


def resolve_output_dir(value: str, project_root: Path) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path
    return project_root / path


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Scan Unity C# project and build raw code knowledge jsonl files."
    )

    parser.add_argument(
        "project_root",
        nargs="?",
        help="Unity project root path.",
    )

    parser.add_argument(
        "--config",
        default=str(DEFAULT_CONFIG_PATH),
        help=f"Config directory or json file path. Default: {DEFAULT_CONFIG_PATH}",
    )

    parser.add_argument(
        "--out",
        default=None,
        help=(
            "Output directory. Relative paths are resolved under the Unity project root. "
            "Overrides config default_out_dir."
        ),
    )

    parser.add_argument(
        "--include",
        default=None,
        help=(
            "Comma separated include directories relative to project root. "
            "Overrides config include_dirs. Example: Assets/HotUpdate,Assets/Scripts"
        ),
    )

    parser.add_argument(
        "--include-class-content",
        action="store_true",
        help=(
            "Include full class content in raw_class chunks. "
            "Default false to avoid huge jsonl."
        ),
    )

    args = parser.parse_args()

    config = load_config(Path(args.config))
    include_dirs = parse_include_dirs(args.include)
    if include_dirs is None:
        include_dirs = config.include_dirs

    project_root = resolve_project_root(args.project_root, config.default_project_root)
    out_dir = resolve_output_dir(args.out or config.default_out_dir, project_root)

    build_raw_knowledge(
        project_root=project_root,
        out_dir=out_dir,
        include_dirs=include_dirs,
        include_class_content=args.include_class_content,
        config=config,
    )


if __name__ == "__main__":
    main()
