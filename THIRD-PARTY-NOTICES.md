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
| FluentAvaloniaUI | 3.1.0 | MIT | Copyright (c) FluentAvalonia contributors（过渡期仍为未替换的 FA 控件提供样式，P4 完成后删除） |
| Avalonia.Fonts.Inter（Inter 字体） | 12.1.5 | 包本身 MIT；内嵌字体为 OFL-1.1 | Copyright 2013-2026 © The AvaloniaUI Project；Inter 字体版权归 The Inter Project Authors |
| SkiaSharp（含原生 libSkiaSharp） | 3.119.4 | MIT | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp（含原生 libHarfBuzzSharp） | 8.3.1.3 | MIT | © Microsoft Corporation. All rights reserved. |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | (c) .NET Foundation and Contributors. All rights reserved. |
| Microsoft.EntityFrameworkCore、Microsoft.EntityFrameworkCore.Relational、Microsoft.EntityFrameworkCore.Design | 10.0.14 | MIT | © Microsoft Corporation. All rights reserved. |
| Npgsql、Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL License | Copyright (c) 2002-2026, Npgsql |
| Microsoft.Extensions.DependencyInjection、DependencyInjection.Abstractions、Logging、Logging.Debug | 10.0.14 | MIT | © Microsoft Corporation. All rights reserved. |
| Material.Avalonia、Material.Avalonia.Dialogs | 3.21.0 | MIT | Copyright (c) Material.Avalonia contributors |
| Material.Icons.Avalonia | 3.0.2 | MIT（图标本身 Apache-2.0） | Copyright (c) Material.Icons.Avalonia contributors；分发图标时需保留 NOTICE |
| .NET 运行时（自包含发布时） | 10.0.x | MIT | © .NET Foundation and Contributors |

## 仅用于测试、不随产品分发

| 组件 | 版本 | 许可证 | 版权行 |
| --- | --- | --- | --- |
| xunit、xunit.core、xunit.assert 等 | 2.9.2 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 | Copyright (C) .NET Foundation |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT | © Microsoft Corporation. All rights reserved. |

## 0.5.0 计划引入、当前快照尚未引用

以下组件写在 `DEVELOPMENT_SPEC.md` 的目标技术栈里，但当前快照（0.5.0-alpha P3）
**还没有**形成依赖。等 P4 真正加入引用时，必须把它们并入上表，
并确认 `LICENSES/` 中对应原文已就位：

| 组件 | 计划许可证 | 备注 |
| --- | --- | --- |
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