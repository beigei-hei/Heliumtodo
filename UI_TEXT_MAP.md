# 界面文字一览（代码位置对照）

> 由 `tools/gen_ui_text_map.py` 扫描生成。行号是生成时的位置；
> 只统计**会显示在图形界面上**的文字，控制台输出与测试断言不计入。

## 一、界面结构里的文字（.axaml）

共 8 个文件、231 处。

### `HeliumTodo.App\Controls\ReminderPicker.axaml`（3 处）

- `HeliumTodo.App\Controls\ReminderPicker.axaml:14` — `<TextBlock Text="提醒" Classes="settingHeader"`
- `HeliumTodo.App\Controls\ReminderPicker.axaml:33` — `<TextBlock Text="小时" VerticalAlignment="Center" Opacity="0.7" />`
- `HeliumTodo.App\Controls\ReminderPicker.axaml:36` — `<TextBlock Text="分钟" VerticalAlignment="Center" Opacity="0.7" />`

### `HeliumTodo.App\Views\DashboardView.axaml`（24 处）

- `HeliumTodo.App\Views\DashboardView.axaml:58` — `<TextBlock Text="今日工作流" Classes="pageHeader" />`
- `HeliumTodo.App\Views\DashboardView.axaml:66` — `<Button Content="◀" ToolTip.Tip="前一天" Command="{Binding PreviousDayCommand}" />`
- `HeliumTodo.App\Views\DashboardView.axaml:67` — `<Button Content="今天" Command="{Binding GoTodayCommand}" />`
- `HeliumTodo.App\Views\DashboardView.axaml:68` — `<Button Content="▶" ToolTip.Tip="后一天" Command="{Binding NextDayCommand}" />`
- `HeliumTodo.App\Views\DashboardView.axaml:69` — `<Button FontSize="12" Padding="9,4" ToolTip.Tip="重新加载"`
- `HeliumTodo.App\Views\DashboardView.axaml:90` — `<TextBlock Text="24 小时时间轴" Classes="cardHeader" />`
- `HeliumTodo.App\Views\DashboardView.axaml:96` — `<TextBlock Text="日程" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\DashboardView.axaml:102` — `<TextBlock Text="待办" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\DashboardView.axaml:108` — `<TextBlock Text="专注 / 预订" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\DashboardView.axaml:114` — `<TextBlock Text="冲突" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\DashboardView.axaml:123` — `<TextBlock Text="今日专注" Classes="caption" />`
- `HeliumTodo.App\Views\DashboardView.axaml:127` — `<TextBlock Text="待办未完成" Classes="caption" />`
- `HeliumTodo.App\Views\DashboardView.axaml:131` — `<TextBlock Text="已安排" Classes="caption" />`
- `HeliumTodo.App\Views\DashboardView.axaml:136` — `<TextBlock Text="冲突" Classes="caption" />`
- `HeliumTodo.App\Views\DashboardView.axaml:222` — `<TextBlock Grid.Column="0" Text="空闲时段" Classes="cardHeader" />`
- `HeliumTodo.App\Views\DashboardView.axaml:223` — `<TextBlock Grid.Column="1" Text="点「安排」把待办放进时间轴"`
- `HeliumTodo.App\Views\DashboardView.axaml:241` — `<Button Grid.Column="2" Content="安排"`
- `HeliumTodo.App\Views\DashboardView.axaml:263` — `<TextBlock Grid.Column="0" Text="冲突提醒" Classes="cardHeader" />`
- `HeliumTodo.App\Views\DashboardView.axaml:271` — `<TextBlock Text="没有任何冲突，今天的安排很清爽 🎉"`
- `HeliumTodo.App\Views\DashboardView.axaml:307` — `<TextBlock Grid.Row="0" Text="今日重点" Classes="cardHeader" />`
- `HeliumTodo.App\Views\DashboardView.axaml:308` — `<TextBlock Grid.Row="1" Text="按优先级排序" Classes="caption" />`
- `HeliumTodo.App\Views\DashboardView.axaml:310` — `<TextBlock Grid.Row="1" Text="还没有待办，去「待办事项」页加一个吧"`
- `HeliumTodo.App\Views\DashboardView.axaml:332` — `Text="{Binding DueAt, StringFormat='截止 {0:MM-dd HH:mm}'}"`
- `HeliumTodo.App\Views\DashboardView.axaml:335` — `Text="{Binding FocusedDuration, StringFormat='已专注 {0:hh\\:mm}'}" />`

### `HeliumTodo.App\Views\FocusView.axaml`（35 处）

- `HeliumTodo.App\Views\FocusView.axaml:37` — `<TextBlock Text="专注" Classes="pageHeader" />`
- `HeliumTodo.App\Views\FocusView.axaml:38` — `<TextBlock Text="今天" Classes="pageCaption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:44` — `<TextBlock Text="次" Classes="pageCaption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:69` — `<TextBlock Text="本次专注" Classes="cardHeader" />`
- `HeliumTodo.App\Views\FocusView.axaml:73` — `<TextBlock Grid.Row="0" Grid.Column="0" Text="做什么"`
- `HeliumTodo.App\Views\FocusView.axaml:76` — `PlaceholderText="不填就用关联待办的标题" />`
- `HeliumTodo.App\Views\FocusView.axaml:78` — `<TextBlock Grid.Row="1" Grid.Column="0" Text="关联待办"`
- `HeliumTodo.App\Views\FocusView.axaml:84` — `PlaceholderText="（可不选）">`
- `HeliumTodo.App\Views\FocusView.axaml:99` — `<TextBlock Text="时长" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:103` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:120` — `<TextBlock Grid.Column="0" Text="预订专注时长" Classes="cardHeader" />`
- `HeliumTodo.App\Views\FocusView.axaml:121` — `<Button Grid.Column="1" Content="＋ 预订一段" FontSize="12" Padding="10,4"`
- `HeliumTodo.App\Views\FocusView.axaml:126` — `Text="预订好的时段会出现在「今日工作流」的时间轴上；如果那段时间已经安排了日程，时间轴会直接标出冲突。" />`
- `HeliumTodo.App\Views\FocusView.axaml:130` — `<TextBox Text="{Binding ReserveTitle}" PlaceholderText="要专注什么？（可不填）" />`
- `HeliumTodo.App\Views\FocusView.axaml:137` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" Margin="0,0,8,6" />`
- `HeliumTodo.App\Views\FocusView.axaml:138` — `<Button Content="保存预订" Command="{Binding SaveReservationCommand}"`
- `HeliumTodo.App\Views\FocusView.axaml:159` — `<Button Grid.Column="1" Content="开始" FontSize="11" Padding="9,4"`
- `HeliumTodo.App\Views\FocusView.axaml:162` — `<Button Grid.Column="2" Content="取消" FontSize="11" Padding="9,4"`
- `HeliumTodo.App\Views\FocusView.axaml:176` — `<TextBlock Text="今日统计" Classes="cardHeader" />`
- `HeliumTodo.App\Views\FocusView.axaml:180` — `<TextBlock Text="专注总时长" Classes="caption" />`
- `HeliumTodo.App\Views\FocusView.axaml:184` — `<TextBlock Text="完成次数" Classes="caption" />`
- `HeliumTodo.App\Views\FocusView.axaml:188` — `<TextBlock Text="平均每次" Classes="caption" />`
- `HeliumTodo.App\Views\FocusView.axaml:192` — `<TextBlock Text="被打扰" Classes="caption" />`
- `HeliumTodo.App\Views\FocusView.axaml:197` — `<Run Text="累计专注：" />`
- `HeliumTodo.App\Views\FocusView.axaml:206` — `<TextBlock Text="今天的专注记录" Classes="cardHeader" />`
- `HeliumTodo.App\Views\FocusView.axaml:208` — `<TextBlock Text="今天还没有完成任何专注。" Classes="caption"`
- `HeliumTodo.App\Views\FocusView.axaml:229` — `Text="{Binding InterruptionCount, StringFormat='打扰 {0} 次'}" />`
- `HeliumTodo.App\Views\FocusView.axaml:294` — `<Run Text="被打扰 " />`
- `HeliumTodo.App\Views\FocusView.axaml:296` — `<Run Text=" 次" />`
- `HeliumTodo.App\Views\FocusView.axaml:313` — `<TextBlock Text="开始专注" IsVisible="{Binding IsIdle}" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:314` — `<TextBlock Text="暂停" IsVisible="{Binding IsRunning}" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:315` — `<TextBlock Text="继续" IsVisible="{Binding IsPaused}" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:316` — `<TextBlock Text="跳过休息" IsVisible="{Binding IsBreak}" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\FocusView.axaml:321` — `<Button Grid.Column="0" Content="完成" Command="{Binding FinishNowCommand}"`
- `HeliumTodo.App\Views\FocusView.axaml:324` — `<Button Grid.Column="1" Content="放弃" Command="{Binding AbandonCommand}"`

### `HeliumTodo.App\Views\ScheduleView.axaml`（27 处）

- `HeliumTodo.App\Views\ScheduleView.axaml:34` — `<TextBlock Text="日程" Classes="pageHeader" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:42` — `<TextBlock Text="今天" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.App\Views\ScheduleView.axaml:47` — `<TextBlock Text="未来 7 天" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.App\Views\ScheduleView.axaml:52` — `<TextBlock Text="今天占用" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.App\Views\ScheduleView.axaml:62` — `Content="＋ 新建"`
- `HeliumTodo.App\Views\ScheduleView.axaml:84` — `PlaceholderText="日程名称，例如：高等数学" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:86` — `<Button Content="取消" Command="{Binding CancelEditCommand}"`
- `HeliumTodo.App\Views\ScheduleView.axaml:88` — `<Button Content="保存" Classes="accent" Command="{Binding SaveCommand}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:95` — `<TextBlock Text="开始" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:101` — `<TextBlock Text="时长" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:104` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:108` — `<TextBlock Text="优先级" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:120` — `<TextBlock Text="时段" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:139` — `PlaceholderText="备注 / 地点，例如：三教 305" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:142` — `<TextBlock Text="重复" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:163` — `<TextBlock Text="星期" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:164` — `<CheckBox Content="一" IsChecked="{Binding Monday}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:165` — `<CheckBox Content="二" IsChecked="{Binding Tuesday}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:166` — `<CheckBox Content="三" IsChecked="{Binding Wednesday}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:167` — `<CheckBox Content="四" IsChecked="{Binding Thursday}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:168` — `<CheckBox Content="五" IsChecked="{Binding Friday}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:169` — `<CheckBox Content="六" IsChecked="{Binding Saturday}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:170` — `<CheckBox Content="日" IsChecked="{Binding Sunday}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:184` — `<TextBlock Text="接下来" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:189` — `<TextBlock Text="{Binding StringFormat='{}{0} 天'}" />`
- `HeliumTodo.App\Views\ScheduleView.axaml:244` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="编辑"`
- `HeliumTodo.App\Views\ScheduleView.axaml:249` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="删除整条日程"`

### `HeliumTodo.App\Views\SettingsView.axaml`（75 处）

- `HeliumTodo.App\Views\SettingsView.axaml:22` — `<TextBlock Text="设置" Classes="pageHeader" />`
- `HeliumTodo.App\Views\SettingsView.axaml:61` — `<fa:FASettingsExpander Background="Transparent" Header="外观" Description="明暗、强调色（含自定义调色板）、功能区玻璃浓度"`
- `HeliumTodo.App\Views\SettingsView.axaml:71` — `<fa:FASettingsExpanderItem Content="明暗模式"`
- `HeliumTodo.App\Views\SettingsView.axaml:72` — `Description="暗色模式下壁纸会自动压一层灰蒙版">`
- `HeliumTodo.App\Views\SettingsView.axaml:96` — `<TextBlock Text="强调色" Classes="section" />`
- `HeliumTodo.App\Views\SettingsView.axaml:140` — `<TextBlock Text="横轴 = 饱和度　纵轴 = 明度" Classes="caption" />`
- `HeliumTodo.App\Views\SettingsView.axaml:212` — `<fa:FASettingsExpanderItem Content="功能区不透明度"`
- `HeliumTodo.App\Views\SettingsView.axaml:213` — `Description="100% 完全不透明；有壁纸时会自动降到 82%">`
- `HeliumTodo.App\Views\SettingsView.axaml:232` — `<fa:FASettingsExpander Background="Transparent" Header="背景壁纸"`
- `HeliumTodo.App\Views\SettingsView.axaml:236` — `<fa:FASettingsExpanderItem Content="启用背景壁纸">`
- `HeliumTodo.App\Views\SettingsView.axaml:238` — `<ToggleSwitch IsChecked="{Binding WallpaperEnabled}" OnContent="开" OffContent="关" />`
- `HeliumTodo.App\Views\SettingsView.axaml:242` — `<fa:FASettingsExpanderItem Content="壁纸图片"`
- `HeliumTodo.App\Views\SettingsView.axaml:248` — `<Button Content="浏览…" Click="OnBrowseWallpaper" />`
- `HeliumTodo.App\Views\SettingsView.axaml:249` — `<Button Content="清除" Command="{Binding ClearWallpaperCommand}" />`
- `HeliumTodo.App\Views\SettingsView.axaml:254` — `<fa:FASettingsExpanderItem Content="蒙版浓度"`
- `HeliumTodo.App\Views\SettingsView.axaml:255` — `Description="把壁纸压淡一点，字就看得清。颜色跟着明暗：亮色用白提亮，暗色用黑压暗，暗色下会自动加强。">`
- `HeliumTodo.App\Views\SettingsView.axaml:267` — `<fa:FASettingsExpanderItem Content="模糊强度">`
- `HeliumTodo.App\Views\SettingsView.axaml:286` — `<fa:FASettingsExpander Background="Transparent" Header="专注" Description="番茄钟时长与休息规则"`
- `HeliumTodo.App\Views\SettingsView.axaml:289` — `<fa:FASettingsExpanderItem Content="默认专注时长（分钟）">`
- `HeliumTodo.App\Views\SettingsView.axaml:297` — `<fa:FASettingsExpanderItem Content="短休息（分钟）">`
- `HeliumTodo.App\Views\SettingsView.axaml:305` — `<fa:FASettingsExpanderItem Content="长休息（分钟）">`
- `HeliumTodo.App\Views\SettingsView.axaml:313` — `<fa:FASettingsExpanderItem Content="专注结束后自动进入休息">`
- `HeliumTodo.App\Views\SettingsView.axaml:315` — `<ToggleSwitch IsChecked="{Binding Config.FocusCompletionAlert}" OnContent="开" OffContent="关" />`
- `HeliumTodo.App\Views\SettingsView.axaml:325` — `<fa:FASettingsExpander Background="Transparent" Header="自控模块"`
- `HeliumTodo.App\Views\SettingsView.axaml:329` — `<fa:FASettingsExpanderItem Content="启用专注守护">`
- `HeliumTodo.App\Views\SettingsView.axaml:331` — `<ToggleSwitch IsChecked="{Binding Config.EnableFocusGuard}" OnContent="开" OffContent="关" />`
- `HeliumTodo.App\Views\SettingsView.axaml:335` — `<fa:FASettingsExpanderItem Content="容忍次数">`
- `HeliumTodo.App\Views\SettingsView.axaml:349` — `<fa:FASettingsExpander Background="Transparent" Header="每日工作流" Description="24 小时时间轴的显示方式"`
- `HeliumTodo.App\Views\SettingsView.axaml:352` — `<fa:FASettingsExpanderItem Content="超载阈值（小时）"`
- `HeliumTodo.App\Views\SettingsView.axaml:353` — `Description="一天安排超过这么多小时，就在时间轴右边提示“今天有点满”。">`
- `HeliumTodo.App\Views\SettingsView.axaml:361` — `<fa:FASettingsExpanderItem Content="时间轴上显示已完成"`
- `HeliumTodo.App\Views\SettingsView.axaml:362` — `Description="关掉之后，已完成的日程和待办不再画在 24 小时时间轴上，画面更干净。">`
- `HeliumTodo.App\Views\SettingsView.axaml:364` — `<ToggleSwitch IsChecked="{Binding Config.ShowCompletedOnTimeline}" OnContent="开" OffContent="关" />`
- `HeliumTodo.App\Views\SettingsView.axaml:374` — `<fa:FASettingsExpander Background="Transparent" Header="自习室" Description="昵称与默认专注时长"`
- `HeliumTodo.App\Views\SettingsView.axaml:377` — `<fa:FASettingsExpanderItem Content="昵称">`
- `HeliumTodo.App\Views\SettingsView.axaml:383` — `<fa:FASettingsExpanderItem Content="自习室默认专注时长（分钟）">`
- `HeliumTodo.App\Views\SettingsView.axaml:391` — `<fa:FASettingsExpanderItem Content="局域网联机">`
- `HeliumTodo.App\Views\SettingsView.axaml:394` — `OnContent="已开启" OffContent="已关闭" />`
- `HeliumTodo.App\Views\SettingsView.axaml:404` — `<fa:FASettingsExpander Background="Transparent" Header="数据与文件" Description="配置文件、数据库、备份"`
- `HeliumTodo.App\Views\SettingsView.axaml:407` — `<fa:FASettingsExpanderItem Content="配置文件"`
- `HeliumTodo.App\Views\SettingsView.axaml:410` — `<Button Content="打开文件夹" Command="{Binding OpenConfigFolderCommand}" />`
- `HeliumTodo.App\Views\SettingsView.axaml:416` — `<fa:FASettingsExpanderItem Content="启动时自动备份">`
- `HeliumTodo.App\Views\SettingsView.axaml:418` — `<ToggleSwitch IsChecked="{Binding Config.AutoBackup}" OnContent="开" OffContent="关" />`
- `HeliumTodo.App\Views\SettingsView.axaml:422` — `<fa:FASettingsExpanderItem Content="导出 / 导入">`
- `HeliumTodo.App\Views\SettingsView.axaml:425` — `<Button Content="导出…" Click="OnExportBackup"`
- `HeliumTodo.App\Views\SettingsView.axaml:427` — `<Button Content="导入…" Click="OnImportBackup"`
- `HeliumTodo.App\Views\SettingsView.axaml:433` — `<fa:FASettingsExpanderItem Content="备份文件夹"`
- `HeliumTodo.App\Views\SettingsView.axaml:436` — `<Button Content="打开文件夹" Command="{Binding OpenBackupFolderCommand}" />`
- `HeliumTodo.App\Views\SettingsView.axaml:440` — `<fa:FASettingsExpanderItem Content="最近一次备份操作"`
- `HeliumTodo.App\Views\SettingsView.axaml:455` — `<fa:FASettingsExpander Background="Transparent" Header="底层设置"`
- `HeliumTodo.App\Views\SettingsView.axaml:456` — `Description="如非遇到渲染问题，请勿调整！"`
- `HeliumTodo.App\Views\SettingsView.axaml:460` — `<fa:FASettingsExpanderItem Content="渲染后端"`
- `HeliumTodo.App\Views\SettingsView.axaml:474` — `<TextBlock Text="改完需要重启程序才生效。"`
- `HeliumTodo.App\Views\SettingsView.axaml:488` — `<fa:FASettingsExpander Background="Transparent" Header="插件"`
- `HeliumTodo.App\Views\SettingsView.axaml:492` — `<fa:FASettingsExpanderItem Content="插件文件夹"`
- `HeliumTodo.App\Views\SettingsView.axaml:496` — `<Button Content="打开文件夹" Command="{Binding OpenPluginFolderCommand}" />`
- `HeliumTodo.App\Views\SettingsView.axaml:497` — `<Button Content="重新扫描" Command="{Binding ReloadPluginsCommand}" />`
- `HeliumTodo.App\Views\SettingsView.axaml:502` — `<fa:FASettingsExpanderItem Content="已安装的插件">`
- `HeliumTodo.App\Views\SettingsView.axaml:504` — `<TextBlock Text="{Binding Plugins.Count, StringFormat='{}{0} 个'}"`
- `HeliumTodo.App\Views\SettingsView.axaml:536` — `<fa:FASettingsExpander Background="Transparent" Header="关于" Description="Helium Todo 日程！ 专注！ 自动化！"`
- `HeliumTodo.App\Views\SettingsView.axaml:539` — `<fa:FASettingsExpanderItem Content="版本">`
- `HeliumTodo.App\Views\SettingsView.axaml:544` — `<fa:FASettingsExpanderItem Content="恢复默认设置">`
- `HeliumTodo.App\Views\SettingsView.axaml:546` — `<Button Content="恢复默认" Command="{Binding ResetToDefaultCommand}" />`
- `HeliumTodo.App\Views\SettingsView.axaml:555` — `<Button Content="保存设置" Classes="accent" MinWidth="120"`
- `HeliumTodo.App\Views\SettingsView.axaml:558` — `<TextBlock Text="修改主题会立刻生效，其它设置点「保存设置」后写入文件。"`
- `HeliumTodo.App\Views\SettingsView.axaml:573` — `<TextBlock Text="导入这份备份？" Classes="section" />`
- `HeliumTodo.App\Views\SettingsView.axaml:588` — `Text="注意，选择替换会覆盖原有数据！" />`
- `HeliumTodo.App\Views\SettingsView.axaml:591` — `<Button Content="取消" Command="{Binding CancelImportCommand}" />`
- `HeliumTodo.App\Views\SettingsView.axaml:592` — `<Button Content="合并导入" Classes="accent"`
- `HeliumTodo.App\Views\SettingsView.axaml:595` — `<Button Content="替换导入"`
- `HeliumTodo.App\Views\SettingsView.axaml:650` — `ToolTip.Tip="关闭"`
- `HeliumTodo.App\Views\SettingsView.axaml:687` — `<TextBlock Text="更改此项需要重启，是否立即重启"`
- `HeliumTodo.App\Views\SettingsView.axaml:693` — `Text="「确认」会保存选择并立刻重新启动 Helium Todo；「取消更改」则保持现在的设置不变。" />`
- `HeliumTodo.App\Views\SettingsView.axaml:695` — `<Button Content="确认"`
- `HeliumTodo.App\Views\SettingsView.axaml:699` — `<Button Content="取消更改"`

### `HeliumTodo.App\Views\ShellView.axaml`（4 处）

- `HeliumTodo.App\Views\ShellView.axaml:118` — `<Button Classes="windowButton" Click="OnMinimizeClick" ToolTip.Tip="最小化">`
- `HeliumTodo.App\Views\ShellView.axaml:121` — `<Button Classes="windowButton" Click="OnMaximizeClick" ToolTip.Tip="最大化 / 还原">`
- `HeliumTodo.App\Views\ShellView.axaml:124` — `<Button Classes="windowButton close" Click="OnCloseClick" ToolTip.Tip="关闭">`
- `HeliumTodo.App\Views\ShellView.axaml:169` — `ToolTip.Tip="展开 / 收起功能名">`

### `HeliumTodo.App\Views\StudyRoomView.axaml`（31 处）

- `HeliumTodo.App\Views\StudyRoomView.axaml:22` — `<TextBlock Text="自习室" Classes="pageHeader" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:51` — `<TextBlock Text="局域网联机" Classes="cardHeader" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:60` — `<TextBlock Text="我想当房主" Classes="cardHeader" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:63` — `PlaceholderText="输入房间名称以创建房间" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:64` — `<Button Grid.Column="1" Content="创建" Classes="accent"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:70` — `<TextBlock Text="我想当房客" Classes="cardHeader" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:73` — `PlaceholderText="输入 6 位房间码"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:75` — `<Button Grid.Column="1" Content="加入" Command="{Binding JoinRoomCommand}" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:82` — `<TextBlock Text="输入联机地址" Classes="cardHeader" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:85` — `PlaceholderText="例如 192.168.1.5 或 [fe80::1]:47470"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:87` — `<Button Grid.Column="1" Content="连接" Classes="accent"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:101` — `<TextBlock Text="局域网联机默认关闭。要用联机自习，到「设置 → 自习室」把「局域网联机」打开。"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:125` — `<TextBlock Text="房间码" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:137` — `<Button Content="＋ 示例队友" FontSize="11" Padding="9,4"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:138` — `ToolTip.Tip="联机实现前，用来看看多人自习的样子"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:140` — `<Button Content="离开" FontSize="11" Padding="9,4"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:156` — `<TextBlock Classes="caption" Text="向你的同学分享房间IP"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:161` — `<Run Text="端口" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:187` — `<TextBox Text="{Binding FocusTitle}" PlaceholderText="这次专注做什么？" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:189` — `<TextBlock Text="时长" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:193` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:196` — `<Run Text="我今天在自习室的累计专注：" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:202` — `<Button Content="开始专注" Classes="accent" MinWidth="96"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:206` — `<Button Content="结束专注" MinWidth="96"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:217` — `<TextBlock Text="房间成员" Classes="cardHeader" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:238` — `Text="{Binding FocusedToday, StringFormat='今日专注 {0:hh\\:mm}'}" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:240` — `Text="{Binding JoinedAt, StringFormat='{}{0:HH:mm} 加入'}" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:248` — `<TextBlock Text="专注中" Foreground="White" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:264` — `<TextBlock Grid.Row="0" Text="房间消息" Classes="cardHeader" />`
- `HeliumTodo.App\Views\StudyRoomView.axaml:300` — `PlaceholderText="说点什么…（回车发送）"`
- `HeliumTodo.App\Views\StudyRoomView.axaml:302` — `<Button Grid.Column="1" Content="发送" Command="{Binding SendMessageCommand}" />`

### `HeliumTodo.App\Views\TodoView.axaml`（32 处）

- `HeliumTodo.App\Views\TodoView.axaml:47` — `<TextBlock Grid.Column="0" Text="待办事项" Classes="pageHeader"`
- `HeliumTodo.App\Views\TodoView.axaml:54` — `<TextBlock Text="未完成" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.App\Views\TodoView.axaml:59` — `<TextBlock Text="今天完成" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.App\Views\TodoView.axaml:64` — `<TextBlock Text="全部" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.App\Views\TodoView.axaml:84` — `PlaceholderText="要做点什么？例如：写数据结构实验报告"`
- `HeliumTodo.App\Views\TodoView.axaml:93` — `<ToggleButton x:Name="MoreToggle" Content="更多" FontSize="12" Padding="10,4"`
- `HeliumTodo.App\Views\TodoView.axaml:94` — `ToolTip.Tip="优先级 / 分类 / 预计时长 / 截止时间" />`
- `HeliumTodo.App\Views\TodoView.axaml:95` — `<Button Content="取消" Command="{Binding CancelEditCommand}"`
- `HeliumTodo.App\Views\TodoView.axaml:97` — `<Button Content="保存" Classes="accent"`
- `HeliumTodo.App\Views\TodoView.axaml:107` — `<TextBlock Text="优先级" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\TodoView.axaml:119` — `<TextBlock Text="分类" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\TodoView.axaml:126` — `PlaceholderText="分类" ToolTip.Tip="可以自己填，也可以点右面的常用分类" />`
- `HeliumTodo.App\Views\TodoView.axaml:127` — `<Button Content="学习" FontSize="10" Padding="6,2" Tag="学习"`
- `HeliumTodo.App\Views\TodoView.axaml:129` — `<Button Content="生活" FontSize="10" Padding="6,2" Tag="生活"`
- `HeliumTodo.App\Views\TodoView.axaml:131` — `<Button Content="社团" FontSize="10" Padding="6,2" Tag="社团"`
- `HeliumTodo.App\Views\TodoView.axaml:136` — `<TextBlock Text="预计" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\TodoView.axaml:139` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.App\Views\TodoView.axaml:143` — `<CheckBox Content="截止" IsChecked="{Binding NewHasDueTime}"`
- `HeliumTodo.App\Views\TodoView.axaml:166` — `<RadioButton Content="未完成" GroupName="filter" FontSize="12"`
- `HeliumTodo.App\Views\TodoView.axaml:169` — `<RadioButton Content="今天" GroupName="filter" FontSize="12"`
- `HeliumTodo.App\Views\TodoView.axaml:172` — `<RadioButton Content="已完成" GroupName="filter" FontSize="12"`
- `HeliumTodo.App\Views\TodoView.axaml:175` — `<RadioButton Content="全部" GroupName="filter" FontSize="12"`
- `HeliumTodo.App\Views\TodoView.axaml:181` — `PlaceholderText="搜索标题 / 分类 / 备注…" MaxWidth="260" MinHeight="30"`
- `HeliumTodo.App\Views\TodoView.axaml:184` — `<TextBlock Grid.Column="2" Text="{Binding Items.Count, StringFormat='共 {0} 条'}"`
- `HeliumTodo.App\Views\TodoView.axaml:211` — `ToolTip.Tip="点一下调整优先级"`
- `HeliumTodo.App\Views\TodoView.axaml:236` — `<TextBlock Text="已逾期" FontSize="11" FontWeight="SemiBold"`
- `HeliumTodo.App\Views\TodoView.axaml:248` — `Text="{Binding PlannedStart, StringFormat='已安排 {0:MM-dd HH:mm}'}" />`
- `HeliumTodo.App\Views\TodoView.axaml:257` — `Text="{Binding FocusedDuration, StringFormat='已专注 {0:hh\\:mm}'}" />`
- `HeliumTodo.App\Views\TodoView.axaml:271` — `<Button Content="安排时间" FontSize="11" Padding="8,4"`
- `HeliumTodo.App\Views\TodoView.axaml:275` — `<Button Content="取消安排" FontSize="11" Padding="8,4"`
- `HeliumTodo.App\Views\TodoView.axaml:280` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="编辑"`
- `HeliumTodo.App\Views\TodoView.axaml:285` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="删除"`

## 二、文字样式与字体（.axaml 资源/样式）

共 0 个文件、0 处。

## 三、ViewModel 里拼出来的文字（运行时）

共 9 个文件、201 处。

### `HeliumTodo.App\ViewModels\DashboardViewModel.cs`（28 处）

- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:99` — `[ObservableProperty] private string _focusTodayText = "0 分钟";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:104` — `[ObservableProperty] private string _busyText = "0 分钟";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:105` — `[ObservableProperty] private string _freeText = "24 小时";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:302` — `SummaryText = $"加载失败：{ex.Message}";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:393` — `DayOfWeek.Monday => "星期一",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:394` — `DayOfWeek.Tuesday => "星期二",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:395` — `DayOfWeek.Wednesday => "星期三",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:396` — `DayOfWeek.Thursday => "星期四",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:397` — `DayOfWeek.Friday => "星期五",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:398` — `DayOfWeek.Saturday => "星期六",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:399` — `_ => "星期日",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:403` — `var suffix = date == today ? "  ·  今天"`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:404` — `: date == today.AddDays(1) ? "  ·  明天"`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:405` — `: date == today.AddDays(-1) ? "  ·  昨天"`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:408` — `return $"{date.Month}月{date.Day}日  {weekday}{suffix}";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:414` — `if (duration <= TimeSpan.Zero) return "0 分钟";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:419` — `if (hours == 0) return $"{minutes} 分钟";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:420` — `if (minutes == 0) return $"{hours} 小时";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:421` — `return $"{hours} 小时 {minutes} 分";`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:441` — `ConflictKind.ScheduleOverlap => "日程重叠",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:442` — `ConflictKind.FocusOverlapsSchedule => "专注冲突",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:443` — `ConflictKind.TodoDueInBusyTime => "截止风险",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:444` — `ConflictKind.PlannedTodoOverlapsSchedule => "安排冲突",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:445` — `ConflictKind.OverloadedDay => "安排过满",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:446` — `_ => "提示",`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:519` — `public string DurationText => "空闲 " + DashboardViewModel.FormatDuration(Duration);`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:523` — `? "可以安排一段专注"`
- `HeliumTodo.App\ViewModels\DashboardViewModel.cs:524` — `: $"可安排「{Todo.Title}」（预计 {Todo.EstimatedDuration.TotalMinutes:0} 分钟）";`

### `HeliumTodo.App\ViewModels\FocusViewModel.cs`（27 处）

- `HeliumTodo.App\ViewModels\FocusViewModel.cs:77` — `[ObservableProperty] private string _phaseText = "准备开始";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:101` — `[ObservableProperty] private string _progressText = "还没有开始";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:120` — `[ObservableProperty] private string _todayTotalText = "0 分钟";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:122` — `[ObservableProperty] private string _allTimeTotalText = "0 分钟";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:123` — `[ObservableProperty] private string _averageSessionText = "0 分钟";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:182` — `? TargetTodo?.Title ?? "专注"`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:208` — `ShowNotice($"已放弃本次专注（坚持了 {result.ActualDuration.TotalMinutes:0} 分钟）。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:230` — `ShowNotice($"本次专注 {actual.TotalMinutes:0} 分钟已记录。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:249` — `ShowNotice($"已开始「{reservation.Title}」。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:259` — `ShowNotice("已取消该预订。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:274` — `? (ReserveTodo?.Title ?? "预订专注")`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:284` — `ShowNotice(error ?? "预订信息不合法。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:293` — `ShowNotice($"已预订 {session.PlannedStart:MM-dd HH:mm} 的 {minutes} 分钟专注，" +`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:294` — `"它会出现在「今日工作流」的时间轴上。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:345` — `PhaseText = "准备开始";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:348` — `ProgressText = "还没有开始";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:352` — `PhaseText = "专注中";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:355` — `ProgressText = $"已进行 {_timer.Elapsed.TotalMinutes:0.#} / {_timer.PlannedDuration.TotalMinutes:0} 分钟";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:359` — `PhaseText = "已暂停";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:362` — `ProgressText = "点「继续」接着专注";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:366` — `PhaseText = "休息中";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:369` — `ProgressText = "起来走两步，放松一下眼睛";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:387` — `ShowNotice($"完成 {e.ActualDuration.TotalMinutes:0} 分钟专注，休息一下吧。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:403` — `ShowNotice("这次专注结束时的收尾动作出错了，数据可能没记全。");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:465` — `ShowNotice($"保存专注记录失败：{ex.Message}");`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:499` — `: "0 分钟";`
- `HeliumTodo.App\ViewModels\FocusViewModel.cs:544` — `ShowNotice($"加载失败：{ex.Message}");`

### `HeliumTodo.App\ViewModels\ReminderEditState.cs`（8 处）

- `HeliumTodo.App\ViewModels\ReminderEditState.cs:31` — `new ReminderOption(null, "不提醒"),`
- `HeliumTodo.App\ViewModels\ReminderEditState.cs:32` — `new ReminderOption(0, "开始时"),`
- `HeliumTodo.App\ViewModels\ReminderEditState.cs:33` — `new ReminderOption(5, "提前 5 分钟"),`
- `HeliumTodo.App\ViewModels\ReminderEditState.cs:34` — `new ReminderOption(10, "提前 10 分钟"),`
- `HeliumTodo.App\ViewModels\ReminderEditState.cs:35` — `new ReminderOption(30, "提前 30 分钟"),`
- `HeliumTodo.App\ViewModels\ReminderEditState.cs:36` — `new ReminderOption(60, "提前 1 小时"),`
- `HeliumTodo.App\ViewModels\ReminderEditState.cs:37` — `new ReminderOption(120, "提前 2 小时"),`
- `HeliumTodo.App\ViewModels\ReminderEditState.cs:38` — `new ReminderOption(null, "自定义…", IsCustom: true),`

### `HeliumTodo.App\ViewModels\ScheduleViewModel.cs`（22 处）

- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:51` — `[ObservableProperty] private string _busyTodayText = "0 分钟";`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:61` — `public string EmptyHint => Upcoming.Count == 0 ? "接下来还没有安排。" : string.Empty;`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:85` — `new RecurrenceOption("none", "不重复"),`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:86` — `new RecurrenceOption("daily", "每天"),`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:87` — `new RecurrenceOption("weekly", "每周"),`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:146` — `public string EditorTitle => IsEditing ? "编辑日程" : "新建日程";`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:218` — `ErrorMessage = "标题不能为空。";`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:240` — `ErrorMessage = error ?? "输入不合法。";`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:363` — `SummaryText = $"接下来 {LookaheadDays} 天共 {occurrences.Count} 项安排" +`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:364` — `$"，其中今天 {TodayCount} 项。";`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:371` — `ErrorMessage = $"加载失败：{ex.Message}";`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:452` — `DayOfWeek.Monday => "星期一",`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:453` — `DayOfWeek.Tuesday => "星期二",`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:454` — `DayOfWeek.Wednesday => "星期三",`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:455` — `DayOfWeek.Thursday => "星期四",`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:456` — `DayOfWeek.Friday => "星期五",`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:457` — `DayOfWeek.Saturday => "星期六",`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:458` — `_ => "星期日",`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:462` — `var suffix = date == today ? "  ·  今天"`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:463` — `: date == today.AddDays(1) ? "  ·  明天"`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:466` — `HeaderText = $"{date.Month}月{date.Day}日  {weekday}{suffix}";`
- `HeliumTodo.App\ViewModels\ScheduleViewModel.cs:479` — `public string CountText => $"{Items.Count} 项";`

### `HeliumTodo.App\ViewModels\SettingsViewModel.cs`（57 处）

- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:250` — `? "已切换到暗色模式：壁纸会自动压暗一档，浓度可以在「背景壁纸」里调。"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:251` — `: "已切换到亮色模式。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:321` — `AccentSourceDefs.Wallpaper => "强调色改为跟随壁纸：壁纸换一帧，强调色也跟着重新算。",`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:322` — `AccentSourceDefs.Custom => "强调色改为自定义：下面那块调色板现在可以拖了。",`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:323` — `_ => "强调色改回跟随主题。",`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:389` — `[ObservableProperty] private string _wallpaperStatusText = "还没有选壁纸";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:412` — `WallpaperStatusText = "壁纸已关闭";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:420` — `WallpaperStatusText = "请先选一张图片";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:439` — `WallpaperStatusText = "还没有选壁纸";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:490` — `WallpaperStatusText = "正在加载…";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:497` — `: $"打不开这张图：{_wallpaper.Error}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:507` — `WallpaperStatusText = "还没有选壁纸";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:511` — `WallpaperStatusText = "壁纸已关闭";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:519` — `WallpaperStatusText = "还没加载";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:526` — `? $"动图 {_wallpaper.FrameCount}/{_wallpaper.SourceFrameCount} 帧"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:527` — `: "静态图";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:529` — `return $"{_wallpaper.PixelWidth}×{_wallpaper.PixelHeight} · {kind} · 主色调 {_wallpaper.Accent.ToHex()}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:550` — `? "已打开局域网联机：可以去自习室建房，或者填同学的 IP 连过去。"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:551` — `: "已关闭局域网联机：自习室只在本机用，不再监听端口。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:637` — `new(RenderBackendSettings.Auto, "Avalonia 自带渲染器（默认，最流畅）"),`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:638` — `new(RenderBackendSettings.Software, "软件渲染（省内存，动画会掉帧）"),`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:709` — `NoticeText = $"渲染后端已保存为「{backendName}」。检测到正在调试器里运行，"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:710` — `+ "自动重启会把调试器甩掉、还会占住 exe 让下次编译失败；"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:711` — `+ "请用调试器的「重新启动」(Ctrl+Shift+F5)，或停止调试后重新运行。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:717` — `NoticeText = $"渲染后端已保存为「{backendName}」，"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:718` — `+ "但自动重启没成功，请手动关闭后重新打开 Helium Todo。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:754` — `NoticeText = $"已取消更改，渲染后端仍是「{backTo.DisplayText}」。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:804` — `$"当前生效：{activeText}（由环境变量 HELIUM_RENDER 强制指定，这一项暂时不起作用）";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:810` — `? $"当前生效：{activeText}"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:811` — `: $"当前生效：{activeText}；重启程序后改为 {RenderBackendSettings.DisplayName(selectedKey)}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:862` — `NoticeText = $"配置已保存到：{_configService.ConfigFilePath}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:920` — `NoticeText = "已恢复默认设置。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:971` — `[ObservableProperty] private string _backupStatusText = "还没有导出或导入过";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1011` — `BackupStatusText = "正在打包…";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1021` — `BackupStatusText = $"原位置不能写入，已改存到备份文件夹：{Path.GetFileName(result.Path)}"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1023` — `NoticeText = "那个文件夹系统不让写（多半是「受控文件夹访问」），备份已经放到「备份文件夹」里了。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1028` — `BackupStatusText = $"已导出 {Path.GetFileName(result.Path)}"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1035` — `BackupStatusText = $"导出失败：{ex.Message}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1054` — `BackupStatusText = "正在检查这份备份…";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1062` — `BackupStatusText = $"这份文件不能导入：{inspection.Error}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1076` — `BackupStatusText = $"读不了这份备份：{ex.Message}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1108` — `? "正在替换…（会先备份当前数据）"`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1109` — `: "正在合并…";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1124` — `BackupStatusText = $"导入失败：{ex.Message}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1197` — `NoticeText = "插件是程序启动时加载的。装好新插件后请重启 Helium Todo 让它生效。";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1233` — `NoticeText = $"打开文件夹失败：{ex.Message}（{path}）";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1250` — `reasons.Add("Avalonia Launcher：系统没有接受这次打开请求");`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1263` — `("外壳启动资源管理器", new ProcessStartInfo`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1269` — `("系统默认方式", new ProcessStartInfo`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1275` — `("直接启动资源管理器", new ProcessStartInfo`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1287` — `("经 cmd start 打开", new ProcessStartInfo`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1310` — `NoticeText = $"打开文件夹失败（{string.Join("；", reasons)}）路径：{path}";`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1330` — `AccentSourceDefs.Wallpaper => "从壁纸里自动提一个主色调",`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1331` — `AccentSourceDefs.Custom => "用下面的调色板自己调",`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1332` — `_ => "用主题自带的那套颜色",`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1382` — `Author = string.IsNullOrWhiteSpace(info.Author) ? "未知" : info.Author;`
- `HeliumTodo.App\ViewModels\SettingsViewModel.cs:1395` — `public string StatusText => IsLoaded ? "已加载" : "加载失败";`

### `HeliumTodo.App\ViewModels\ShellViewModel.cs`（7 处）

- `HeliumTodo.App\ViewModels\ShellViewModel.cs:177` — `"今日工作流",`
- `HeliumTodo.App\ViewModels\ShellViewModel.cs:182` — `"日程",`
- `HeliumTodo.App\ViewModels\ShellViewModel.cs:187` — `"待办事项",`
- `HeliumTodo.App\ViewModels\ShellViewModel.cs:192` — `"专注",`
- `HeliumTodo.App\ViewModels\ShellViewModel.cs:197` — `"自习室",`
- `HeliumTodo.App\ViewModels\ShellViewModel.cs:203` — `"设置",`
- `HeliumTodo.App\ViewModels\ShellViewModel.cs:237` — `StatusText = $"{item.Title} 加载失败：{ex.Message}";`

### `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs`（30 处）

- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:42` — `_roomName = "期末冲刺自习室";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:125` — `public string CurrentRoomName => CurrentRoom?.Name ?? "还没有加入自习室";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:128` — `public string MemberCountText => $"{Members.Count} 人在线";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:131` — `[ObservableProperty] private string _myFocusTodayText = "0 分钟";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:141` — `[ObservableProperty] private string _focusTitle = "和大家一起专注";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:178` — `if (!IsOnlineAvailable) return "当前是本地模式，没法联机。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:182` — `return "局域网联机默认关闭：到「设置 → 自习室」打开「局域网联机」后，才能和同学一起自习。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:186` — `? $"已连接 · 共 {_roomService.LocalPort} 端口在监听"`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:187` — `: "局域网联机已就绪：填对方的 IP 点「连接」，或者让对方填你的地址。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:216` — `return rtt <= 0 ? "正在测延迟…" : $"延迟 {rtt} 毫秒";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:231` — `var latency = p.RoundTripMs > 0 ? $"{p.RoundTripMs} ms" : "未测";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:248` — `: "未启动";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:266` — `NoticeText = "先填对方的 IP 地址，例如 192.168.1.5 或 [fe80::1]:47470";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:272` — `NoticeText = "局域网联机是关着的。到「设置 → 自习室」打开「局域网联机」再来。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:276` — `NoticeText = "正在连接…";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:283` — `NoticeText = ok ? "连上了，可以让对方开始专注了。" : "没连上，看看地址和房间码对不对。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:315` — `NoticeText = "请输入房间码。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:333` — `NoticeText = $"已回到本地房间「{local.Name}」。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:341` — `? "本地找不到这个房间。联机是关着的，先到「设置 → 自习室」打开「局域网联机」。"`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:342` — `: "本地找不到这个房间，而且当前是本地模式，没法联机。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:349` — `? $"本地没有这个房间。请在下面填上房主的 IP 地址，再点「连接」。"`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:350` — `: "正在连接…";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:373` — `NoticeText = "已离开自习室。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:382` — `NoticeText = "先创建或加入一个自习室吧。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:387` — `var title = string.IsNullOrWhiteSpace(FocusTitle) ? "和大家一起专注" : FocusTitle;`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:444` — `NoticeText = "先创建一个自习室。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:448` — `var names = new[] { "小张", "小李", "小王", "阿伟", "小陈" };`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:454` — `NoticeText = "示例队友已经加满了。";`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:474` — `SenderName = "系统",`
- `HeliumTodo.App\ViewModels\StudyRoomViewModel.cs:475` — `Content = $"「{name}」加入了自习室。",`

### `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs`（12 处）

- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:176` — `TimelineEntryKind.Schedule => "日程",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:177` — `TimelineEntryKind.PlannedTodo => "待办（已安排时间）",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:178` — `TimelineEntryKind.FocusReservation => "预订的专注",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:179` — `TimelineEntryKind.RunningFocus => "正在专注",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:180` — `_ => "条目",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:183` — `var text = $"{Title}\n{kindText} · {TimeRangeText}\n优先级：{PriorityText}";`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:192` — `text += $"\n⚠ 与其它安排重叠（错位显示在第 {Lane + 1} / {LaneCount} 列）";`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:197` — `text += "\n⚠ 存在时间冲突";`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:207` — `Priority.Urgent => "紧急",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:208` — `Priority.High => "较高",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:209` — `Priority.Low => "较低",`
- `HeliumTodo.App\ViewModels\TimelineEntryViewModel.cs:210` — `_ => "普通",`

### `HeliumTodo.App\ViewModels\TodoViewModel.cs`（10 处）

- `HeliumTodo.App\ViewModels\TodoViewModel.cs:54` — `[ObservableProperty] private string _emptyHint = "还没有待办，用下面的输入框加一个吧。";`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:89` — `[ObservableProperty] private string _newCategory = "学习";`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:113` — `public string EditorTitle => IsEditing ? "编辑待办" : "新建待办";`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:133` — `EmptyHint = "标题不能为空哦。";`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:224` — `NewCategory = todo.Category ?? "学习";`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:351` — `EmptyHint = $"加载失败：{ex.Message}";`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:412` — `TodoFilter.Completed => "还没有完成任何待办。",`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:413` — `TodoFilter.Today => "今天没有要做的待办，轻松一下也不错。",`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:414` — `TodoFilter.Active => "没有未完成的待办，全清空了！",`
- `HeliumTodo.App\ViewModels\TodoViewModel.cs:415` — `_ => "还没有待办，用上面的输入框加一个吧。",`

## 四、Core / Data 层拼出来的文字（运行时）

共 39 个文件、260 处。

### `HeliumTodo.App\App.axaml.cs`（27 处）

- `HeliumTodo.App\App.axaml.cs:164` — `LogBackground("[单实例] 已经有一份在跑，已请它把窗口显示出来，本进程退出");`
- `HeliumTodo.App\App.axaml.cs:213` — `LogBackground("[提醒] 后台扫描已启动（每 30 秒一次）");`
- `HeliumTodo.App\App.axaml.cs:228` — `LogBackground($"[环境] 会话={System.Diagnostics.Process.GetCurrentProcess().SessionId}"`
- `HeliumTodo.App\App.axaml.cs:229` — `+ $" 管理员={IsElevated()}"`
- `HeliumTodo.App\App.axaml.cs:230` — `+ $" 资源管理器={DescribeExplorer()}"`
- `HeliumTodo.App\App.axaml.cs:231` — `+ $" 渲染后端={Core.Configuration.RenderBackendSettings.DisplayName(HeliumTodo.App.Services.RenderBackendChoice.ActiveKey)}"`
- `HeliumTodo.App\App.axaml.cs:246` — `LogBackground("[托盘] 启动失败：没有托盘图标，但关闭仍会留在后台"`
- `HeliumTodo.App\App.axaml.cs:247` — `+ "（再双击一次程序即可唤回窗口）");`
- `HeliumTodo.App\App.axaml.cs:253` — `? "[托盘] 自检模式：假装托盘已就绪（没有真的图标）"`
- `HeliumTodo.App\App.axaml.cs:254` — `: "[托盘] 已启动，关闭按钮改为藏窗口");`
- `HeliumTodo.App\App.axaml.cs:277` — `? "[窗口] 关闭按钮被拦截，已隐藏到托盘（进程继续运行）"`
- `HeliumTodo.App\App.axaml.cs:278` — `: "[窗口] 关闭按钮被拦截，已隐藏（没有托盘图标，再双击程序可唤回）");`
- `HeliumTodo.App\App.axaml.cs:323` — `return "没有运行";`
- `HeliumTodo.App\App.axaml.cs:327` — `return $"PID {string.Join(',', processes.Select(p => p.Id))} 会话 {string.Join(',', sessions)}";`
- `HeliumTodo.App\App.axaml.cs:331` — `return "查不到";`
- `HeliumTodo.App\App.axaml.cs:395` — `? "[备份] 这次没做成"`
- `HeliumTodo.App\App.axaml.cs:396` — `: $"[备份] 已生成 {path}");`
- `HeliumTodo.App\App.axaml.cs:641` — `? $"[启动] 首次运行，已写入示例数据：{databasePath}"`
- `HeliumTodo.App\App.axaml.cs:642` — `: $"[启动] 数据库就绪：{databasePath}");`
- `HeliumTodo.App\App.axaml.cs:650` — `Console.Error.WriteLine($"[启动] 数据库初始化失败：{ex.Message}（库路径：{configService.ResolveDatabasePath()}）");`
- `HeliumTodo.App\App.axaml.cs:684` — `LogBackground("[重启] 检测到调试器，放弃自动重启："`
- `HeliumTodo.App\App.axaml.cs:685` — `+ "请用调试器的「重新启动」(Ctrl+Shift+F5)，"`
- `HeliumTodo.App\App.axaml.cs:686` — `+ "否则新进程不受调试器管理，还会占住 exe 让下次编译失败");`
- `HeliumTodo.App\App.axaml.cs:693` — `LogBackground("[重启] 拿不到当前可执行文件路径，放弃自动重启");`
- `HeliumTodo.App\App.axaml.cs:706` — `LogBackground($"[重启] 释放单实例锁失败（不影响继续重启）：{ex.Message}");`
- `HeliumTodo.App\App.axaml.cs:720` — `LogBackground($"[重启] 已启动新进程：{exePath}");`
- `HeliumTodo.App\App.axaml.cs:724` — `LogBackground($"[重启] 启动新进程失败：{ex.Message}");`

### `HeliumTodo.App\Converters\CommonConverters.cs`（13 处）

- `HeliumTodo.App\Converters\CommonConverters.cs:28` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.App\Converters\CommonConverters.cs:47` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.App\Converters\CommonConverters.cs:92` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.App\Converters\CommonConverters.cs:111` — `HeliumTodo.Core.Models.Priority.Urgent => "紧急",`
- `HeliumTodo.App\Converters\CommonConverters.cs:112` — `HeliumTodo.Core.Models.Priority.High => "较高",`
- `HeliumTodo.App\Converters\CommonConverters.cs:113` — `HeliumTodo.Core.Models.Priority.Low => "较低",`
- `HeliumTodo.App\Converters\CommonConverters.cs:114` — `_ => "普通",`
- `HeliumTodo.App\Converters\CommonConverters.cs:119` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.App\Converters\CommonConverters.cs:139` — `HeliumTodo.Core.Models.TimeBlock.Morning => "上午",`
- `HeliumTodo.App\Converters\CommonConverters.cs:140` — `HeliumTodo.Core.Models.TimeBlock.Afternoon => "下午",`
- `HeliumTodo.App\Converters\CommonConverters.cs:141` — `HeliumTodo.Core.Models.TimeBlock.Evening => "晚上",`
- `HeliumTodo.App\Converters\CommonConverters.cs:142` — `_ => "不限",`
- `HeliumTodo.App\Converters\CommonConverters.cs:147` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`

### `HeliumTodo.App\Services\MediaFoundationVideo.cs`（18 处）

- `HeliumTodo.App\Services\MediaFoundationVideo.cs:288` — `error = "这台电脑上没有可用的 Media Foundation，解不了视频。";`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:300` — `ThrowIfFailed(MFCreateAttributes(out attributes, 4), "创建属性袋");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:303` — `ThrowIfFailed(attributes!.SetUINT32(ref ReaderEnableVideoProcessing, enable), "打开视频处理器");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:317` — `ThrowIfFailed(MFCreateSourceReaderFromURL(path, attributes, out reader), "打开视频文件");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:320` — `ThrowIfFailed(reader!.SetStreamSelection(AllStreams, 0), "关掉所有流");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:321` — `ThrowIfFailed(reader.SetStreamSelection(FirstVideoStream, 1), "打开视频流");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:324` — `ThrowIfFailed(reader.GetNativeMediaType(FirstVideoStream, 0, out var nativeType), "读取视频原始格式");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:325` — `ThrowIfFailed(nativeType.GetUINT64(ref AttributeFrameSize, out var packed), "读取原始分辨率");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:331` — `error = "这个视频没有读到有效的画面尺寸。";`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:361` — `+ $"A(改当前+缩放)=0x{resultA:X8} B(新建+缩放)=0x{resultB:X8} C(不缩放)=0x{resultC:X8}");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:376` — `error = "这个视频的编码方式这台电脑解不了"`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:377` — `+ $"（常见于 HEVC / AV1，需要装系统解码器）。错误码 0x{lastResult:X8}";`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:463` — `error = "这个视频里没有解出任何一帧。";`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:506` — `+ $"时间戳 {string.Join(",", timestamps.Take(4))}；"`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:507` — `+ $"间隔 {string.Join(",", delays.Take(4))} ms；MinFrameGap={MinFrameGap}");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:516` — `error = $"解析视频失败：{ex.Message}";`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:537` — `ThrowIfFailed(sample.ConvertToContiguousBuffer(out var buffer), "取出帧数据");`
- `HeliumTodo.App\Services\MediaFoundationVideo.cs:541` — `ThrowIfFailed(buffer.Lock(out var source, out _, out _), "锁定帧内存");`

### `HeliumTodo.App\Services\PluginHost.cs`（14 处）

- `HeliumTodo.App\Services\PluginHost.cs:90` — `_logger.LogWarning(ex, "无法创建插件目录 {Directory}", PluginDirectory);`
- `HeliumTodo.App\Services\PluginHost.cs:108` — `_logger.LogWarning(ex, "加载插件文件失败：{File}", Path.GetFileName(dllPath));`
- `HeliumTodo.App\Services\PluginHost.cs:114` — `Description: $"加载失败：{ex.Message}",`
- `HeliumTodo.App\Services\PluginHost.cs:155` — `_logger.LogInformation("插件 {Id} 已经加载过，跳过", plugin.Id);`
- `HeliumTodo.App\Services\PluginHost.cs:167` — `_logger.LogInformation("已加载插件 {Name} ({Id}) v{Version}",`
- `HeliumTodo.App\Services\PluginHost.cs:173` — `_logger.LogWarning(ex, "初始化插件 {Type} 失败", type.FullName);`
- `HeliumTodo.App\Services\PluginHost.cs:180` — `$"初始化失败：{ex.Message}",`
- `HeliumTodo.App\Services\PluginHost.cs:199` — `_logger.LogWarning(ex, "关闭插件 {Id} 时出错", plugin.Id);`
- `HeliumTodo.App\Services\PluginHost.cs:224` — `_logger.LogWarning(ex, "读取插件配置 {Key} 失败，使用默认值", key);`
- `HeliumTodo.App\Services\PluginHost.cs:240` — `_logger.LogWarning(ex, "保存插件配置 {Key} 失败", key);`
- `HeliumTodo.App\Services\PluginHost.cs:244` — `public void Log(string message) => _logger.LogInformation("[插件] {Message}", message);`
- `HeliumTodo.App\Services\PluginHost.cs:287` — `_logger.LogWarning(ex, "插件事件 {Event} 处理时出错", eventName);`
- `HeliumTodo.App\Services\PluginHost.cs:309` — `public string Name => "我的插件";`
- `HeliumTodo.App\Services\PluginHost.cs:314` — `host.Log("我的插件启动了！");`

### `HeliumTodo.App\Services\ReminderService.cs`（2 处）

- `HeliumTodo.App\Services\ReminderService.cs:112` — `Trace?.Invoke($"[提醒] 弹出「{item.Title}」：{item.Message}");`
- `HeliumTodo.App\Services\ReminderService.cs:118` — `Trace?.Invoke($"[提醒] 本轮扫到 {due.Count} 条");`

### `HeliumTodo.App\Services\TrayService.cs`（12 处）

- `HeliumTodo.App\Services\TrayService.cs:161` — `Trace?.Invoke($"托盘窗口创建失败，错误码 {Marshal.GetLastWin32Error()}");`
- `HeliumTodo.App\Services\TrayService.cs:192` — `Trace?.Invoke($"托盘图标：window=0x{_window.ToInt64():X} icon=0x{_icon.ToInt64():X} "`
- `HeliumTodo.App\Services\TrayService.cs:198` — `Trace?.Invoke($"托盘图标加不上（错误码 {lastError}）：{DescribeShellError(lastError)}");`
- `HeliumTodo.App\Services\TrayService.cs:259` — `AppendMenuW(menu, MF_STRING, ID_SHOW, "显示主界面");`
- `HeliumTodo.App\Services\TrayService.cs:260` — `AppendMenuW(menu, MF_STRING, ID_BACKUP, "立即备份一次");`
- `HeliumTodo.App\Services\TrayService.cs:262` — `AppendMenuW(menu, MF_STRING, ID_EXIT, "退出 Helium Todo");`
- `HeliumTodo.App\Services\TrayService.cs:482` — `0 => "系统没给出错误码（多半是这次调用根本没到 shell）。",`
- `HeliumTodo.App\Services\TrayService.cs:483` — `5 => "拒绝访问：当前会话没有可用的通知区域（远程桌面 / 无交互会话常见），"`
- `HeliumTodo.App\Services\TrayService.cs:484` — `+ "或者程序以管理员身份运行而资源管理器不是。",`
- `HeliumTodo.App\Services\TrayService.cs:485` — `87 => "参数不对：结构体大小和系统期望的不一致。",`
- `HeliumTodo.App\Services\TrayService.cs:486` — `1400 => "窗口句柄无效。",`
- `HeliumTodo.App\Services\TrayService.cs:487` — `_ => "请把这一行连同错误码一起反馈。",`

### `HeliumTodo.App\Services\WallpaperDecoder.cs`（7 处）

- `HeliumTodo.App\Services\WallpaperDecoder.cs:121` — `if (bytes.Length == 0) throw new InvalidOperationException("壁纸文件是空的。");`
- `HeliumTodo.App\Services\WallpaperDecoder.cs:133` — `?? throw new InvalidOperationException("认不出这张图的格式（支持 PNG / JPG / APNG / GIF）。");`
- `HeliumTodo.App\Services\WallpaperDecoder.cs:184` — `throw new PlatformNotSupportedException("视频壁纸目前只支持 Windows 系统。");`
- `HeliumTodo.App\Services\WallpaperDecoder.cs:194` — `throw new InvalidOperationException(error ?? "这个视频解不出来。");`
- `HeliumTodo.App\Services\WallpaperDecoder.cs:319` — `if (frames.Count == 0) throw new InvalidOperationException("这张 APNG 里没有能显示的帧。");`
- `HeliumTodo.App\Services\WallpaperDecoder.cs:410` — `throw new InvalidOperationException("这张图没有有效的尺寸。");`
- `HeliumTodo.App\Services\WallpaperDecoder.cs:466` — `if (frames.Count == 0) throw new InvalidOperationException("这张图解不出任何一帧。");`

### `HeliumTodo.App\Services\WallpaperService.cs`（1 处）

- `HeliumTodo.App\Services\WallpaperService.cs:120` — `Error = $"找不到这个文件：{path}";`

### `HeliumTodo.App\ViewLocator.cs`（2 处）

- `HeliumTodo.App\ViewLocator.cs:42` — `return new TextBlock { Text = $"找不到界面：{viewName}" };`
- `HeliumTodo.App\ViewLocator.cs:51` — `return new TextBlock { Text = $"创建界面失败：{viewName}\n{ex.Message}" };`

### `HeliumTodo.App\Views\MainWindow.axaml.cs`（5 处）

- `HeliumTodo.App\Views\MainWindow.axaml.cs:267` — `App.LogBackground($"[图标轨] 第 {frame} 帧 间隔 {elapsed - last:0.0} ms 已用 {elapsed:0.0} ms");`
- `HeliumTodo.App\Views\MainWindow.axaml.cs:311` — `App.LogBackground("[窗口] 状态 → 最小化，动画就地收尾");`
- `HeliumTodo.App\Views\MainWindow.axaml.cs:319` — `App.LogBackground($"[窗口] 状态 → {state}（托盘唤回，出现动画由 App 播）");`
- `HeliumTodo.App\Views\MainWindow.axaml.cs:326` — `App.LogBackground($"[窗口] 状态 → {state}，从最小化恢复，补播出现动画");`
- `HeliumTodo.App\Views\MainWindow.axaml.cs:330` — `App.LogBackground($"[窗口] 状态 → {state}");`

### `HeliumTodo.App\Views\SettingsView.axaml.cs`（8 处）

- `HeliumTodo.App\Views\SettingsView.axaml.cs:50` — `Title = "选一张背景壁纸",`
- `HeliumTodo.App\Views\SettingsView.axaml.cs:57` — `new FilePickerFileType("图片（PNG / GIF / APNG / JPG / WebP）")`
- `HeliumTodo.App\Views\SettingsView.axaml.cs:62` — `new FilePickerFileType("视频（MP4 / MOV / WMV 等）")`
- `HeliumTodo.App\Views\SettingsView.axaml.cs:67` — `new FilePickerFileType("所有文件")`
- `HeliumTodo.App\Views\SettingsView.axaml.cs:93` — `Title = "导出全部数据",`
- `HeliumTodo.App\Views\SettingsView.axaml.cs:99` — `new FilePickerFileType("Helium Todo 备份")`
- `HeliumTodo.App\Views\SettingsView.axaml.cs:124` — `Title = "选择一份 Helium Todo 备份",`
- `HeliumTodo.App\Views\SettingsView.axaml.cs:128` — `new FilePickerFileType("Helium Todo 备份")`

### `HeliumTodo.App\Windowing\WindowAnimator.cs`（2 处）

- `HeliumTodo.App\Windowing\WindowAnimator.cs:154` — `trace($"[开场动画] 第 {_introFrame} 帧 progress={progress:0.00} 遮罩={_veilBrush.Color.A}");`
- `HeliumTodo.App\Windowing\WindowAnimator.cs:170` — `trace("[开场动画] 等不到下一帧，强制收尾");`

### `HeliumTodo.App\Windowing\WindowChrome.cs`（1 处）

- `HeliumTodo.App\Windowing\WindowChrome.cs:95` — `Trace($"[窗口] 没能显式开启 DWM 过渡（hr=0x{hr:X8}），沿用系统默认");`

### `HeliumTodo.Core\Backup\BackupInspection.cs`（9 处）

- `HeliumTodo.Core\Backup\BackupInspection.cs:18` — `BackupImportMode.Merge => "合并（保留现有数据）",`
- `HeliumTodo.Core\Backup\BackupInspection.cs:19` — `_ => "替换（清空现有数据）",`
- `HeliumTodo.Core\Backup\BackupInspection.cs:24` — `BackupImportMode.Merge => "现有数据全部保留，备份里的记录以新编号追加进来。",`
- `HeliumTodo.Core\Backup\BackupInspection.cs:25` — `_ => "现有数据会被清空，完全变成备份里的内容。导入前会自动存一份当前数据的备份。",`
- `HeliumTodo.Core\Backup\BackupInspection.cs:78` — `return Error ?? "这个文件不能导入。";`
- `HeliumTodo.Core\Backup\BackupInspection.cs:83` — `ExportedAt.ToString("yyyy-MM-dd HH:mm") + " 导出",`
- `HeliumTodo.Core\Backup\BackupInspection.cs:84` — `string.IsNullOrWhiteSpace(AppVersion) ? "版本未知" : "v" + AppVersion.TrimStart('v', 'V'),`
- `HeliumTodo.Core\Backup\BackupInspection.cs:89` — `parts.Add(HasConfig ? "含配置" : "无配置");`
- `HeliumTodo.Core\Backup\BackupInspection.cs:90` — `parts.Add(HasWallpaper ? $"含壁纸（{WallpaperFileName}）" : "无壁纸");`

### `HeliumTodo.Core\Backup\BackupManifest.cs`（4 处）

- `HeliumTodo.Core\Backup\BackupManifest.cs:41` — `$"{Schedules} 条日程 · {Todos} 条待办 · {FocusSessions} 条专注记录" +`
- `HeliumTodo.Core\Backup\BackupManifest.cs:42` — `(StudyRooms > 0 ? $" · {StudyRooms} 个自习室" : string.Empty);`
- `HeliumTodo.Core\Backup\BackupManifest.cs:105` — `error = "清单内容为空。";`
- `HeliumTodo.Core\Backup\BackupManifest.cs:114` — `error = $"清单不是合法的 JSON：{ex.Message}";`

### `HeliumTodo.Core\Backup\BackupPackageReader.cs`（14 处）

- `HeliumTodo.Core\Backup\BackupPackageReader.cs:48` — `throw new BackupPackageException($"找不到备份文件：{filePath}");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:60` — `throw new BackupPackageException("这个文件不是有效的 .zip，可能下载不完整或已经损坏。");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:64` — `throw new BackupPackageException($"打不开这个文件：{ex.Message}");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:71` — `"这个 zip 里没有 manifest.json，不是 Helium Todo 导出的备份。");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:75` — `?? throw new BackupPackageException(error ?? "清单解析失败。");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:99` — `error = $"备份里缺少文件：{entry.Entry}";`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:106` — `error = $"文件校验失败：{entry.Entry}（可能传输过程中损坏了）";`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:120` — `?? throw new BackupPackageException($"备份里缺少文件：{entryName}");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:130` — `?? throw new BackupPackageException($"备份里缺少文件：{entryName}");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:166` — `$"这个 zip 不是 Helium Todo 的备份（清单里的格式标识是「{manifest.Format}」）。");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:172` — `$"这份备份来自更新版本的程序（备份格式 v{manifest.FormatVersion}），" +`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:173` — `$"当前程序最多支持 v{BackupFormat.CurrentVersion}。请先升级程序再导入。");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:178` — `throw new BackupPackageException($"备份格式版本无效（v{manifest.FormatVersion}）。");`
- `HeliumTodo.Core\Backup\BackupPackageReader.cs:183` — `throw new BackupPackageException("清单里没有数据库条目，这个备份是空的。");`

### `HeliumTodo.Core\Backup\BackupPackageWriter.cs`（5 处）

- `HeliumTodo.Core\Backup\BackupPackageWriter.cs:75` — `throw new ArgumentException("数据库内容是空的，不能导出。", nameof(content));`
- `HeliumTodo.Core\Backup\BackupPackageWriter.cs:182` — `$"系统不允许往「{directory}」里写文件。常见原因：Windows 安全中心的「受控文件夹访问」把这个程序拦住了，"`
- `HeliumTodo.Core\Backup\BackupPackageWriter.cs:183` — `+ "或者这个文件夹本身只读 / 需要管理员权限。",`
- `HeliumTodo.Core\Backup\BackupPackageWriter.cs:184` — `DirectoryNotFoundException => $"目录「{directory}」不存在，也建不出来。",`
- `HeliumTodo.Core\Backup\BackupPackageWriter.cs:185` — `_ => $"写不进「{directory}」：{ex.Message}",`

### `HeliumTodo.Core\Configuration\AccentSources.cs`（3 处）

- `HeliumTodo.Core\Configuration\AccentSources.cs:54` — `Wallpaper => "跟随壁纸",`
- `HeliumTodo.Core\Configuration\AccentSources.cs:55` — `Custom => "自定义颜色",`
- `HeliumTodo.Core\Configuration\AccentSources.cs:56` — `_ => "跟随主题",`

### `HeliumTodo.Core\Configuration\AppConfig.cs`（2 处）

- `HeliumTodo.Core\Configuration\AppConfig.cs:287` — `FallbackReason = $"{reason}；备用目录也不可用：{secondReason}";`
- `HeliumTodo.Core\Configuration\AppConfig.cs:317` — `reason = $"{directory} 不可写（{ex.GetType().Name}: {ex.Message}）";`

### `HeliumTodo.Core\Configuration\ColorSchemes.cs`（3 处）

- `HeliumTodo.Core\Configuration\ColorSchemes.cs:48` — `"深色" or "夜间" or "night" or "dim" => Dark,`
- `HeliumTodo.Core\Configuration\ColorSchemes.cs:49` — `"浅色" or "日间" or "day" => Light,`
- `HeliumTodo.Core\Configuration\ColorSchemes.cs:56` — `Normalize(value) == Dark ? "暗色" : "亮色";`

### `HeliumTodo.Core\Configuration\RenderBackendSettings.cs`（2 处）

- `HeliumTodo.Core\Configuration\RenderBackendSettings.cs:91` — `Auto => "Avalonia 自带渲染器",`
- `HeliumTodo.Core\Configuration\RenderBackendSettings.cs:94` — `_ => "软件渲染",`

### `HeliumTodo.Core\Ipc\IIpcChannel.cs`（1 处）

- `HeliumTodo.Core\Ipc\IIpcChannel.cs:58` — `=> Task.FromResult(IpcResponse.Failure("没有可用的进程间通道。"));`

### `HeliumTodo.Core\Models\FocusSession.cs`（2 处）

- `HeliumTodo.Core\Models\FocusSession.cs:92` — `error = "专注时长必须大于 0。";`
- `HeliumTodo.Core\Models\FocusSession.cs:98` — `error = "单次专注不能超过 8 小时（请拆成几次）。";`

### `HeliumTodo.Core\Models\ReminderPresets.cs`（5 处）

- `HeliumTodo.Core\Models\ReminderPresets.cs:47` — `if (minutes is not { } value) return "不提醒";`
- `HeliumTodo.Core\Models\ReminderPresets.cs:48` — `if (value <= 0) return "开始时";`
- `HeliumTodo.Core\Models\ReminderPresets.cs:53` — `if (hours == 0) return $"提前 {rest} 分钟";`
- `HeliumTodo.Core\Models\ReminderPresets.cs:54` — `if (rest == 0) return $"提前 {hours} 小时";`
- `HeliumTodo.Core\Models\ReminderPresets.cs:55` — `return $"提前 {hours} 小时 {rest} 分钟";`

### `HeliumTodo.Core\Models\ScheduleItem.cs`（3 处）

- `HeliumTodo.Core\Models\ScheduleItem.cs:76` — `error = "日程标题不能为空。";`
- `HeliumTodo.Core\Models\ScheduleItem.cs:82` — `error = "日程时长必须大于 0。";`
- `HeliumTodo.Core\Models\ScheduleItem.cs:88` — `error = "单条日程不能超过 24 小时（跨天请拆成两条）。";`

### `HeliumTodo.Core\Models\StudyRoom.cs`（3 处）

- `HeliumTodo.Core\Models\StudyRoom.cs:28` — `public string HostName { get; set; } = "我";`
- `HeliumTodo.Core\Models\StudyRoom.cs:78` — `public string DisplayName { get; set; } = "我";`
- `HeliumTodo.Core\Models\StudyRoom.cs:109` — `public string SenderName { get; set; } = "我";`

### `HeliumTodo.Core\Models\TodoItem.cs`（2 处）

- `HeliumTodo.Core\Models\TodoItem.cs:100` — `error = "待办标题不能为空。";`
- `HeliumTodo.Core\Models\TodoItem.cs:106` — `error = "预计时长必须大于 0。";`

### `HeliumTodo.Core\Notifications\ReminderPlanner.cs`（4 处）

- `HeliumTodo.Core\Notifications\ReminderPlanner.cs:118` — `var prefix = lead <= 0 ? "现在开始" : $"{ReminderPresets.Describe(lead)}开始";`
- `HeliumTodo.Core\Notifications\ReminderPlanner.cs:126` — `var label = isDue ? "截止" : "计划开始";`
- `HeliumTodo.Core\Notifications\ReminderPlanner.cs:127` — `var leadText = lead <= 0 ? "就在现在" : ReminderPresets.Describe(lead);`
- `HeliumTodo.Core\Notifications\ReminderPlanner.cs:128` — `return $"{label}时间 {anchor:HH:mm} · {leadText}";`

### `HeliumTodo.Core\Services\Conflict.cs`（3 处）

- `HeliumTodo.Core\Services\Conflict.cs:65` — `ConflictSeverity.Error => "严重",`
- `HeliumTodo.Core\Services\Conflict.cs:66` — `ConflictSeverity.Warning => "警告",`
- `HeliumTodo.Core\Services\Conflict.cs:67` — `_ => "提示",`

### `HeliumTodo.Core\Services\DayTimeline.cs`（4 处）

- `HeliumTodo.Core\Services\DayTimeline.cs:140` — `? $"{(int)BusyDuration.TotalHours} 小时 {BusyDuration.Minutes} 分"`
- `HeliumTodo.Core\Services\DayTimeline.cs:141` — `: $"{BusyDuration.Minutes} 分钟";`
- `HeliumTodo.Core\Services\DayTimeline.cs:143` — `var conflictText = Conflicts.Count == 0 ? "无冲突" : $"{Conflicts.Count} 处冲突";`
- `HeliumTodo.Core\Services\DayTimeline.cs:144` — `return $"{Entries.Count} 项安排 · 共 {busy} · {conflictText}";`

### `HeliumTodo.Core\Services\DayTimelineBuilder.cs`（10 处）

- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:180` — `Title = string.IsNullOrWhiteSpace(f.Title) ? "专注时段" : f.Title,`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:181` — `Notes = f.IsStudyRoomSession ? $"自习室 · {f.StudyRoomCode}" : null,`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:371` — `Message = $"今天安排了 {(int)busy.TotalHours} 小时 {(busy.Minutes)} 分，几乎没有休息时间，建议留出空档。",`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:390` — `TimelineEntryKind.FocusReservation or TimelineEntryKind.RunningFocus => "的专注时段与",`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:391` — `TimelineEntryKind.PlannedTodo => "的计划时间与",`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:392` — `_ => " 与",`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:396` — `? $"「{a.Title}」与「{b.Title}」时间重叠 {minutes} 分钟"`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:397` — `: $"「{a.Title}」{relation}「{b.Title}」重叠 {minutes} 分钟";`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:438` — `Message = $"「{todo.Title}」截止时间 {FormatTime(due)} 落在「{blocking.Title}」期间" +`
- `HeliumTodo.Core\Services\DayTimelineBuilder.cs:439` — `$"（{blocking.TimeRangeText}），可能来不及完成。",`

### `HeliumTodo.Core\Services\RecurrenceExpander.cs`（10 处）

- `HeliumTodo.Core\Services\RecurrenceExpander.cs:85` — `if (days.Count == 0) return "单次";`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:86` — `if (days.Count == 7) return "每天";`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:90` — `DayOfWeek.Monday => "一",`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:91` — `DayOfWeek.Tuesday => "二",`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:92` — `DayOfWeek.Wednesday => "三",`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:93` — `DayOfWeek.Thursday => "四",`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:94` — `DayOfWeek.Friday => "五",`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:95` — `DayOfWeek.Saturday => "六",`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:96` — `_ => "日",`
- `HeliumTodo.Core\Services\RecurrenceExpander.cs:101` — `return "每周" + string.Join("、", ordered);`

### `HeliumTodo.Core\Services\StudyRoomService.cs`（23 处）

- `HeliumTodo.Core\Services\StudyRoomService.cs:104` — `Name = string.IsNullOrWhiteSpace(name) ? "我的自习室" : name,`
- `HeliumTodo.Core\Services\StudyRoomService.cs:105` — `HostName = string.IsNullOrWhiteSpace(hostName) ? "我" : hostName,`
- `HeliumTodo.Core\Services\StudyRoomService.cs:129` — `AddSystemMessage($"房间「{room.Name}」已创建，房间码 {room.Code}。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:147` — `AddSystemMessage("当前是本地模式，没法连到别人的房间。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:151` — `AddSystemMessage($"准备加入房间 {roomCode.Trim().ToUpperInvariant()}，请填写房主地址后点「连接」。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:169` — `AddSystemMessage("当前是本地模式，没法联机。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:177` — `AddSystemMessage("请先填房间码。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:183` — `AddSystemMessage("请先填对方的 IP 地址。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:193` — `AddSystemMessage("没连上，检查一下地址、房间码，以及对方的防火墙是否放行了这个端口。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:201` — `Name = $"{displayName} 的自习室",`
- `HeliumTodo.Core\Services\StudyRoomService.cs:210` — `DisplayName = string.IsNullOrWhiteSpace(displayName) ? "我" : displayName,`
- `HeliumTodo.Core\Services\StudyRoomService.cs:216` — `AddSystemMessage($"已连上 {host.Trim()}，房间码 {code}。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:227` — `AddSystemMessage("已离开房间。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:246` — `AddSystemMessage($"「{(me?.DisplayName ?? "我")}」开始了 {duration.TotalMinutes:0} 分钟的专注：{title}");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:250` — `me?.DisplayName ?? "我",`
- `HeliumTodo.Core\Services\StudyRoomService.cs:271` — `AddSystemMessage($"专注结束，本次 {actualDuration.TotalMinutes:0} 分钟。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:275` — `me?.DisplayName ?? "我",`
- `HeliumTodo.Core\Services\StudyRoomService.cs:284` — `public void SendMessage(string content, string senderName = "我")`
- `HeliumTodo.Core\Services\StudyRoomService.cs:312` — `SenderName = "系统",`
- `HeliumTodo.Core\Services\StudyRoomService.cs:346` — `DisplayName = string.IsNullOrWhiteSpace(presence.DisplayName) ? "同学" : presence.DisplayName,`
- `HeliumTodo.Core\Services\StudyRoomService.cs:354` — `AddSystemMessage($"「{(string.IsNullOrWhiteSpace(presence.DisplayName) ? "同学" : presence.DisplayName)}」进来了。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:363` — `AddSystemMessage($"「{member.DisplayName}」离开了。");`
- `HeliumTodo.Core\Services\StudyRoomService.cs:380` — `SenderName = string.IsNullOrWhiteSpace(message.DisplayName) ? "同学" : message.DisplayName,`

### `HeliumTodo.Core\Theming\BuiltInThemes.cs`（9 处）

- `HeliumTodo.Core\Theming\BuiltInThemes.cs:22` — `displayName: "Fluent 浅色 · 蓝",`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:31` — `new SimpleTheme("fluent-light-teal",   "Fluent 浅色 · 青", ThemeKind.Light, FluentTeal),`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:32` — `new SimpleTheme("fluent-light-purple", "Fluent 浅色 · 紫", ThemeKind.Light, FluentPurple),`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:33` — `new SimpleTheme("fluent-light-green",  "Fluent 浅色 · 绿", ThemeKind.Light, FluentGreen),`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:35` — `new SimpleTheme("fluent-dark-blue",    "Fluent 深色 · 蓝", ThemeKind.Dark,  FluentBlue),`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:36` — `new SimpleTheme("fluent-dark-teal",    "Fluent 深色 · 青", ThemeKind.Dark,  FluentTeal),`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:37` — `new SimpleTheme("fluent-dark-purple",  "Fluent 深色 · 紫", ThemeKind.Dark,  FluentPurple),`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:38` — `new SimpleTheme("fluent-dark-orange",  "Fluent 深色 · 橙", ThemeKind.Dark,  FluentOrange),`
- `HeliumTodo.Core\Theming\BuiltInThemes.cs:41` — `new SimpleTheme("high-contrast", "高对比度（预览）", ThemeKind.Dark, "#FFD700", isExperimental: true),`

### `HeliumTodo.Data\BackupExportResult.cs`（1 处）

- `HeliumTodo.Data\BackupExportResult.cs:42` — `return $"已导出 {Path}（{sizeText}，{Manifest.Counts.DescribeText}）";`

### `HeliumTodo.Data\BackupImportResult.cs`（4 处）

- `HeliumTodo.Data\BackupImportResult.cs:34` — `var text = $"{BackupImportModeInfo.DisplayName(Mode)}：已导入 {Counts.DescribeText}";`
- `HeliumTodo.Data\BackupImportResult.cs:38` — `text += $"；导入前的数据已备份到 {SafetyBackupPath}";`
- `HeliumTodo.Data\BackupImportResult.cs:43` — `text += "；配置已同步";`
- `HeliumTodo.Data\BackupImportResult.cs:48` — `text += "；壁纸已恢复";`

### `HeliumTodo.Data\DataBackupService.cs`（11 处）

- `HeliumTodo.Data\DataBackupService.cs:99` — `$"[DataBackupService] 目标位置不可写，改存到 {fallbackPath}；原因：{denied.Message}");`
- `HeliumTodo.Data\DataBackupService.cs:192` — `return BackupInspection.Failed(zipPath, checksumError ?? "备份文件校验失败。");`
- `HeliumTodo.Data\DataBackupService.cs:219` — `return BackupInspection.Failed(zipPath, $"读取备份时出错：{ex.Message}");`
- `HeliumTodo.Data\DataBackupService.cs:246` — `throw new BackupPackageException(checksumError ?? "备份文件校验失败。");`
- `HeliumTodo.Data\DataBackupService.cs:274` — `throw new BackupPackageException($"导入失败：{ex.Message}");`
- `HeliumTodo.Data\DataBackupService.cs:403` — `return new WallpaperPayload(null, info.Name, $"壁纸超过 {limit} MB，没有打进备份");`
- `HeliumTodo.Data\DataBackupService.cs:410` — `return new WallpaperPayload(null, Path.GetFileName(path), $"壁纸读取失败：{ex.Message}");`
- `HeliumTodo.Data\DataBackupService.cs:553` — `$"备份里的数据库打不开（可能不是有效的 SQLite 文件）：{ex.Message}");`
- `HeliumTodo.Data\DataBackupService.cs:582` — `throw new BackupPackageException("备份里的数据库结构不认识（找不到日程/待办这些表）。");`
- `HeliumTodo.Data\DataBackupService.cs:878` — `warnings.Add("这份备份没带配置，导入后界面设置保持本机的");`
- `HeliumTodo.Data\DataBackupService.cs:889` — `warnings.Add("这份备份里没有日程/待办/专注记录，导入后本地数据会被清空");`

### `HeliumTodo.Data\LanPeerTransport.cs`（10 处）

- `HeliumTodo.Data\LanPeerTransport.cs:117` — `if (addresses.Count == 0) return "（没找到可用网卡）";`
- `HeliumTodo.Data\LanPeerTransport.cs:366` — `LastError = $"地址看不懂：{host}";`
- `HeliumTodo.Data\LanPeerTransport.cs:426` — `LastError = $"连接 {endpoint} 超时";`
- `HeliumTodo.Data\LanPeerTransport.cs:430` — `LastError = $"连不上 {endpoint}（{ex.SocketErrorCode}）";`
- `HeliumTodo.Data\LanPeerTransport.cs:434` — `LastError = $"连接 {endpoint} 失败：{ex.Message}";`
- `HeliumTodo.Data\LanPeerTransport.cs:476` — `LastError = $"端口 {_port} 被占用（{ex.SocketErrorCode}），联机功能不可用";`
- `HeliumTodo.Data\LanPeerTransport.cs:606` — `Text = "房间码不对",`
- `HeliumTodo.Data\LanPeerTransport.cs:710` — `string.IsNullOrWhiteSpace(message.DisplayName) ? "同学" : message.DisplayName,`
- `HeliumTodo.Data\LanPeerTransport.cs:858` — `LastError = $"广播端口 {_port} 不可用（{ex.SocketErrorCode}），只能手动填 IP";`
- `HeliumTodo.Data\LanPeerTransport.cs:932` — `$"{_displayName} 的自习室",`

### `HeliumTodo.Data\LocalOnlyPeerTransport.cs`（1 处）

- `HeliumTodo.Data\LocalOnlyPeerTransport.cs:63` — `public string LocalAddressText => "（本地模式）";`
