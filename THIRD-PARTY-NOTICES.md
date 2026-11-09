# 第三方声明（THIRD-PARTY NOTICES）

Helium Todo 本身以 **GPL-3.0-only** 发布（见 [`LICENSE`](LICENSE)）。
本文列出随程序分发或参与构建的第三方组件及其许可证。许可证原文放在 [`LICENSES/`](LICENSES/)：

| 文件 | 许可证 |
| --- | --- |
| [`LICENSES/MIT.txt`](LICENSES/MIT.txt) | MIT |
| [`LICENSES/Apache-2.0.txt`](LICENSES/Apache-2.0.txt) | Apache License 2.0 |
| [`LICENSES/PostgreSQL.txt`](LICENSES/PostgreSQL.txt) | PostgreSQL License |
| [`LICENSES/OFL-1.1.txt`](LICENSES/OFL-1.1.txt) | SIL Open Font License 1.1 |

MIT 是模板文本（`<year>` / `<copyright holders>` 为占位符）；各组件实际的版权行见下表。
本文件必须随每个发布包一起分发，且在任何依赖变化时更新。

## 随程序分发的组件

| 组件 | 版本 | 许可证 | 版权行 |
| --- | --- | --- | --- |
| Avalonia、Avalonia.Desktop、Avalonia.Themes.Fluent、Avalonia.HarfBuzz、Avalonia.Headless、Avalonia.Controls.DataGrid、Avalonia.Controls.ColorPicker | 12.1.2 | MIT | Copyright 2013-2026 © The AvaloniaUI Project |
| FluentAvaloniaUI | 3.1.0 | MIT | Copyright (c) FluentAvalonia contributors（0.3.x–0.4.x 的界面库；0.5.0 起不再引用，仅保留本行以说明历史版本） |
| Avalonia.Fonts.Inter（Inter 字体） | 12.1.2 | 包本身 MIT；内嵌字体为 OFL-1.1 | Copyright 2013-2026 © The AvaloniaUI Project；Inter 字体版权归 The Inter Project Authors |
| SkiaSharp（含原生 libSkiaSharp） | 3.119.4 | MIT | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp（含原生 libHarfBuzzSharp） | 8.3.1.3 | MIT | © Microsoft Corporation. All rights reserved. |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | (c) .NET Foundation and Contributors. All rights reserved. |
| Microsoft.EntityFrameworkCore、Microsoft.EntityFrameworkCore.Sqlite、Microsoft.EntityFrameworkCore.Design | 8.0.11 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.DependencyInjection、DependencyInjection.Abstractions、Logging、Logging.Debug | 8.0.1 / 8.0.2 | MIT | © Microsoft Corporation. All rights reserved. |
| SQLitePCLRaw.bundle_e_sqlite3、SQLitePCLRaw.core、SQLitePCLRaw.provider.e_sqlite3 | 2.1.6 | Apache-2.0 | Copyright 2014-2023 SourceGear, LLC |
| SQLite（原生库 e_sqlite3） | 随 SQLitePCLRaw 打包 | Public Domain | SQLite 作者已声明其进入公有领域 |
| .NET 运行时（自包含发布时） | 10.0.x | MIT | © .NET Foundation and Contributors |

## 仅用于测试、不随产品分发

| 组件 | 版本 | 许可证 | 版权行 |
| --- | --- | --- | --- |
| xunit、xunit.core、xunit.assert 等 | 2.9.2 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 | Copyright (C) .NET Foundation |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT | © Microsoft Corporation. All rights reserved. |

## 0.5.0 计划引入、当前快照尚未引用

以下组件写在 `DEVELOPMENT_SPEC.md` 的目标技术栈里，但因为离线环境无法安装，
本快照（0.5.0-alpha P0）**还没有**形成依赖。等 P2/P3/P4 真正加入引用时，
必须把它们并入上表，并确认 `LICENSES/` 中对应原文已就位：

| 组件 | 计划许可证 | 备注 |
| --- | --- | --- |
| Material.Avalonia / Material.Avalonia.Dialogs | MIT | P3 主题骨架 |
| Material.Icons.Avalonia | MIT（图标本身 Apache-2.0） | 分发图标时需保留 NOTICE |
| Npgsql / Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL License | P2 数据库迁移；届时 PostgreSQL 版权行以其发布包为准 |
| Avalonia.Xaml.Behaviors（自建 12.1.5 构建） | MIT | P4 控件替换 |

## 字体

- **Segoe Fluent Icons / Segoe MDL2 Assets** 是微软专有字体，本项目**不打包**字体文件，
  只在界面里按名称引用系统已安装的字体。没有系统字体时图标会缺失，但不会分发字体副本。
- Inter 字体由 `Avalonia.Fonts.Inter` 以资源形式嵌入，按 OFL-1.1 随包分发；
  OFL 允许嵌入与再分发，但不允许单独售卖字体本身。

## 许可证兼容性说明

以上组件均为宽松许可证（MIT / Apache-2.0 / PostgreSQL / OFL-1.1），
与 GPL-3.0-only 兼容，可以一起分发。若将来引入 GPL 不兼容的组件（例如专有 SDK、
仅限非商业使用的库），必须在合并前停止并重新评估，而不是事后补声明。