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
| Avalonia、Avalonia.Desktop、Avalonia.HarfBuzz、Avalonia.Headless、Avalonia.Controls.ColorPicker | 12.1.5 | MIT | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.Fonts.Inter（Inter 字体） | 12.1.5 | 包本身 MIT；内嵌字体为 OFL-1.1 | Copyright 2013-2026 © The AvaloniaUI Project；Inter 字体版权归 The Inter Project Authors |
| SkiaSharp（含原生 libSkiaSharp） | 3.119.4 | MIT | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp（含原生 libHarfBuzzSharp） | 8.3.1.3 | MIT | © Microsoft Corporation. All rights reserved. |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | (c) .NET Foundation and Contributors. All rights reserved. |
| Microsoft.EntityFrameworkCore、Microsoft.EntityFrameworkCore.Relational、Microsoft.EntityFrameworkCore.Design | 10.0.14 | MIT | © Microsoft Corporation. All rights reserved. |
| Npgsql、Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL License | Copyright (c) 2002-2026, Npgsql |
| Microsoft.Extensions.DependencyInjection、DependencyInjection.Abstractions、Logging、Logging.Debug | 10.0.14 | MIT | © Microsoft Corporation. All rights reserved. |
| Material.Avalonia、Material.Avalonia.Dialogs | 3.21.0 | MIT | Copyright (c) Material.Avalonia contributors |
| Xaml.Behaviors.Avalonia（及 Interactivity/Interactions/Animations 子包） | 12.1.5 | MIT | Copyright (c) Wiesław Šoltés |
| Material.Icons.Avalonia | 3.0.2 | MIT（图标本身 Apache-2.0） | Copyright (c) Material.Icons.Avalonia contributors；分发图标时需保留 NOTICE |
| .NET 运行时（自包含发布时） | 10.0.x | MIT | © .NET Foundation and Contributors |
| Microsoft Visual C++ Redistributable（随发布包附带 vcruntime140.dll、vcruntime140_1.dll、msvcp140.dll） | 14.x | 微软可再分发运行库（非开源许可） | © Microsoft Corporation. All rights reserved. |

## 仅用于测试、不随产品分发

| 组件 | 版本 | 许可证 | 版权行 |
| --- | --- | --- | --- |
| xunit、xunit.core、xunit.assert 等 | 2.9.2 | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 | Copyright (C) .NET Foundation |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT | © Microsoft Corporation. All rights reserved. |
