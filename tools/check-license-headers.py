#!/usr/bin/env python3
"""检查每个 .cs / .axaml 源文件头部的两行 SPDX 标识。

为什么要有这个门禁：0.5.0 起项目以 GPL-3.0-only 发布，
许可证声明只写在仓库根目录的 LICENSE 里是不够的——
单个源文件被复制出去时必须还能看出它的许可证。
DEVELOPMENT_SPEC.md §13.3 要求所有 .cs / .axaml 都带两行头，
本脚本让「漏写头」在 CI 里直接变红，而不是等到发布后才发现。

用法：
    python tools/check-license-headers.py [仓库根目录]

退出码：0 全部合规；1 有缺失或未找到源文件。
"""

from __future__ import annotations

import sys
from pathlib import Path

COPYRIGHT = "SPDX-FileCopyrightText: 2025 Helium Todo contributors"
LICENSE = "SPDX-License-Identifier: GPL-3.0-only"
SKIP_DIRS = {"bin", "obj", "dist", "output", ".git", ".vs", ".idea", ".tmp-build", "node_modules"}
HEAD_BYTES = 600


def iter_sources(root: Path):
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        if any(part in SKIP_DIRS for part in path.relative_to(root).parts):
            continue
        if path.suffix in {".cs", ".axaml"}:
            yield path


def main(argv: list[str]) -> int:
    root = Path(argv[1]).resolve() if len(argv) > 1 else Path(__file__).resolve().parent.parent
    sources = sorted(iter_sources(root))
    if not sources:
        print(f"[FAIL] 在 {root} 下没有找到任何 .cs / .axaml 文件")
        return 1

    missing: list[tuple[Path, list[str]]] = []
    for path in sources:
        head = path.read_bytes()[:HEAD_BYTES].decode("utf-8", "replace")
        problems = []
        if COPYRIGHT not in head:
            problems.append("缺少 SPDX-FileCopyrightText")
        if LICENSE not in head:
            problems.append("缺少 SPDX-License-Identifier: GPL-3.0-only")
        if problems:
            missing.append((path, problems))

    for path, problems in missing:
        rel = path.relative_to(root)
        print(f"[FAIL] {rel}: {'；'.join(problems)}")

    total = len(sources)
    ok = total - len(missing)
    print(f"[SPDX] 扫描 {total} 个文件，合规 {ok}，缺失 {len(missing)}")
    if missing:
        print("[SPDX] 在每个文件最上方补两行头（.axaml 用 XML 注释包裹），见 DEVELOPMENT_SPEC.md §13.3")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))