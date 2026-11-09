# 界面文字一览（代码位置对照）

> 由 `tools/gen_ui_text_map.py` 扫描生成。行号是生成时的位置；
> 只统计**会显示在图形界面上**的文字，控制台输出与测试断言不计入。

## 一、界面结构里的文字（.axaml）

共 8 个文件、228 处。

### `HeliumTodo.Desktop\Controls\ReminderPicker.axaml`（3 处）

- `HeliumTodo.Desktop\Controls\ReminderPicker.axaml:19` — `<TextBlock Text="提醒" Classes="settingHeader"`
- `HeliumTodo.Desktop\Controls\ReminderPicker.axaml:38` — `<TextBlock Text="小时" VerticalAlignment="Center" Opacity="0.7" />`
- `HeliumTodo.Desktop\Controls\ReminderPicker.axaml:41` — `<TextBlock Text="分钟" VerticalAlignment="Center" Opacity="0.7" />`

### `HeliumTodo.Desktop\Views\DashboardView.axaml`（24 处）

- `HeliumTodo.Desktop\Views\DashboardView.axaml:63` — `<TextBlock Text="今日工作流" Classes="pageHeader" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:71` — `<Button Content="◀" ToolTip.Tip="前一天" Command="{Binding PreviousDayCommand}" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:72` — `<Button Content="今天" Command="{Binding GoTodayCommand}" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:73` — `<Button Content="▶" ToolTip.Tip="后一天" Command="{Binding NextDayCommand}" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:74` — `<Button FontSize="12" Padding="9,4" ToolTip.Tip="重新加载"`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:95` — `<TextBlock Text="24 小时时间轴" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:101` — `<TextBlock Text="日程" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:107` — `<TextBlock Text="待办" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:113` — `<TextBlock Text="专注 / 预订" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:119` — `<TextBlock Text="冲突" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:128` — `<TextBlock Text="今日专注" Classes="caption" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:132` — `<TextBlock Text="待办未完成" Classes="caption" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:136` — `<TextBlock Text="已安排" Classes="caption" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:141` — `<TextBlock Text="冲突" Classes="caption" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:227` — `<TextBlock Grid.Column="0" Text="空闲时段" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:228` — `<TextBlock Grid.Column="1" Text="点「安排」把待办放进时间轴"`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:246` — `<Button Grid.Column="2" Content="安排"`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:268` — `<TextBlock Grid.Column="0" Text="冲突提醒" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:276` — `<TextBlock Text="没有任何冲突，今天的安排很清爽 🎉"`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:312` — `<TextBlock Grid.Row="0" Text="今日重点" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:313` — `<TextBlock Grid.Row="1" Text="按优先级排序" Classes="caption" />`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:315` — `<TextBlock Grid.Row="1" Text="还没有待办，去「待办事项」页加一个吧"`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:337` — `Text="{Binding DueAt, StringFormat='截止 {0:MM-dd HH:mm}'}"`
- `HeliumTodo.Desktop\Views\DashboardView.axaml:340` — `Text="{Binding FocusedDuration, StringFormat='已专注 {0:hh\\:mm}'}" />`

### `HeliumTodo.Desktop\Views\FocusView.axaml`（35 处）

- `HeliumTodo.Desktop\Views\FocusView.axaml:42` — `<TextBlock Text="专注" Classes="pageHeader" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:43` — `<TextBlock Text="今天" Classes="pageCaption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:49` — `<TextBlock Text="次" Classes="pageCaption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:74` — `<TextBlock Text="本次专注" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:78` — `<TextBlock Grid.Row="0" Grid.Column="0" Text="做什么"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:81` — `PlaceholderText="不填就用关联待办的标题" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:83` — `<TextBlock Grid.Row="1" Grid.Column="0" Text="关联待办"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:89` — `PlaceholderText="（可不选）">`
- `HeliumTodo.Desktop\Views\FocusView.axaml:104` — `<TextBlock Text="时长" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:108` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:125` — `<TextBlock Grid.Column="0" Text="预订专注时长" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:126` — `<Button Grid.Column="1" Content="＋ 预订一段" FontSize="12" Padding="10,4"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:131` — `Text="预订好的时段会出现在「今日工作流」的时间轴上；如果那段时间已经安排了日程，时间轴会直接标出冲突。" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:135` — `<TextBox Text="{Binding ReserveTitle}" PlaceholderText="要专注什么？（可不填）" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:142` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" Margin="0,0,8,6" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:143` — `<Button Content="保存预订" Command="{Binding SaveReservationCommand}"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:164` — `<Button Grid.Column="1" Content="开始" FontSize="11" Padding="9,4"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:167` — `<Button Grid.Column="2" Content="取消" FontSize="11" Padding="9,4"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:181` — `<TextBlock Text="今日统计" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:185` — `<TextBlock Text="专注总时长" Classes="caption" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:189` — `<TextBlock Text="完成次数" Classes="caption" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:193` — `<TextBlock Text="平均每次" Classes="caption" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:197` — `<TextBlock Text="被打扰" Classes="caption" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:202` — `<Run Text="累计专注：" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:211` — `<TextBlock Text="今天的专注记录" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:213` — `<TextBlock Text="今天还没有完成任何专注。" Classes="caption"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:234` — `Text="{Binding InterruptionCount, StringFormat='打扰 {0} 次'}" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:299` — `<Run Text="被打扰 " />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:301` — `<Run Text=" 次" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:318` — `<TextBlock Text="开始专注" IsVisible="{Binding IsIdle}" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:319` — `<TextBlock Text="暂停" IsVisible="{Binding IsRunning}" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:320` — `<TextBlock Text="继续" IsVisible="{Binding IsPaused}" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:321` — `<TextBlock Text="跳过休息" IsVisible="{Binding IsBreak}" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\FocusView.axaml:326` — `<Button Grid.Column="0" Content="完成" Command="{Binding FinishNowCommand}"`
- `HeliumTodo.Desktop\Views\FocusView.axaml:329` — `<Button Grid.Column="1" Content="放弃" Command="{Binding AbandonCommand}"`

### `HeliumTodo.Desktop\Views\ScheduleView.axaml`（27 处）

- `HeliumTodo.Desktop\Views\ScheduleView.axaml:39` — `<TextBlock Text="日程" Classes="pageHeader" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:47` — `<TextBlock Text="今天" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:52` — `<TextBlock Text="未来 7 天" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:57` — `<TextBlock Text="今天占用" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:67` — `Content="＋ 新建"`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:89` — `PlaceholderText="日程名称，例如：高等数学" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:91` — `<Button Content="取消" Command="{Binding CancelEditCommand}"`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:93` — `<Button Content="保存" Classes="accent" Command="{Binding SaveCommand}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:100` — `<TextBlock Text="开始" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:106` — `<TextBlock Text="时长" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:109` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:113` — `<TextBlock Text="优先级" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:125` — `<TextBlock Text="时段" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:144` — `PlaceholderText="备注 / 地点，例如：三教 305" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:147` — `<TextBlock Text="重复" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:168` — `<TextBlock Text="星期" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:169` — `<CheckBox Content="一" IsChecked="{Binding Monday}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:170` — `<CheckBox Content="二" IsChecked="{Binding Tuesday}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:171` — `<CheckBox Content="三" IsChecked="{Binding Wednesday}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:172` — `<CheckBox Content="四" IsChecked="{Binding Thursday}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:173` — `<CheckBox Content="五" IsChecked="{Binding Friday}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:174` — `<CheckBox Content="六" IsChecked="{Binding Saturday}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:175` — `<CheckBox Content="日" IsChecked="{Binding Sunday}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:189` — `<TextBlock Text="接下来" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:194` — `<TextBlock Text="{Binding StringFormat='{}{0} 天'}" />`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:249` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="编辑"`
- `HeliumTodo.Desktop\Views\ScheduleView.axaml:254` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="删除整条日程"`

### `HeliumTodo.Desktop\Views\SettingsView.axaml`（72 处）

- `HeliumTodo.Desktop\Views\SettingsView.axaml:27` — `<TextBlock Text="设置" Classes="pageHeader" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:66` — `<fa:FASettingsExpander Background="Transparent" Header="外观" Description="明暗、强调色（含自定义调色板）、功能区玻璃浓度"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:76` — `<fa:FASettingsExpanderItem Content="明暗模式"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:77` — `Description="暗色模式下壁纸会自动压一层灰蒙版">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:101` — `<TextBlock Text="强调色" Classes="section" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:145` — `<TextBlock Text="横轴 = 饱和度　纵轴 = 明度" Classes="caption" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:217` — `<fa:FASettingsExpanderItem Content="功能区不透明度"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:218` — `Description="100% 完全不透明；有壁纸时会自动降到 82%">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:237` — `<fa:FASettingsExpander Background="Transparent" Header="背景壁纸"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:241` — `<fa:FASettingsExpanderItem Content="启用背景壁纸">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:243` — `<ToggleSwitch IsChecked="{Binding WallpaperEnabled}" OnContent="开" OffContent="关" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:247` — `<fa:FASettingsExpanderItem Content="壁纸图片"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:253` — `<Button Content="浏览…" Click="OnBrowseWallpaper" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:254` — `<Button Content="清除" Command="{Binding ClearWallpaperCommand}" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:259` — `<fa:FASettingsExpanderItem Content="蒙版浓度"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:260` — `Description="把壁纸压淡一点，字就看得清。颜色跟着明暗：亮色用白提亮，暗色用黑压暗，暗色下会自动加强。">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:272` — `<fa:FASettingsExpanderItem Content="模糊强度">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:291` — `<fa:FASettingsExpander Background="Transparent" Header="专注" Description="番茄钟时长与休息规则"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:294` — `<fa:FASettingsExpanderItem Content="默认专注时长（分钟）">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:302` — `<fa:FASettingsExpanderItem Content="短休息（分钟）">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:310` — `<fa:FASettingsExpanderItem Content="长休息（分钟）">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:318` — `<fa:FASettingsExpanderItem Content="专注结束后自动进入休息">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:320` — `<ToggleSwitch IsChecked="{Binding Config.FocusCompletionAlert}" OnContent="开" OffContent="关" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:330` — `<fa:FASettingsExpander Background="Transparent" Header="自控模块"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:334` — `<fa:FASettingsExpanderItem Content="启用专注守护">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:336` — `<ToggleSwitch IsChecked="{Binding Config.EnableFocusGuard}" OnContent="开" OffContent="关" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:340` — `<fa:FASettingsExpanderItem Content="容忍次数">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:354` — `<fa:FASettingsExpander Background="Transparent" Header="每日工作流" Description="24 小时时间轴的显示方式"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:357` — `<fa:FASettingsExpanderItem Content="超载阈值（小时）"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:358` — `Description="一天安排超过这么多小时，就在时间轴右边提示“今天有点满”。">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:366` — `<fa:FASettingsExpanderItem Content="时间轴上显示已完成"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:367` — `Description="关掉之后，已完成的日程和待办不再画在 24 小时时间轴上，画面更干净。">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:369` — `<ToggleSwitch IsChecked="{Binding Config.ShowCompletedOnTimeline}" OnContent="开" OffContent="关" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:379` — `<fa:FASettingsExpander Background="Transparent" Header="自习室" Description="昵称与默认专注时长"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:382` — `<fa:FASettingsExpanderItem Content="昵称">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:388` — `<fa:FASettingsExpanderItem Content="自习室默认专注时长（分钟）">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:396` — `<fa:FASettingsExpanderItem Content="局域网联机">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:399` — `OnContent="已开启" OffContent="已关闭" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:409` — `<fa:FASettingsExpander Background="Transparent" Header="数据与文件" Description="配置文件、数据库、备份"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:412` — `<fa:FASettingsExpanderItem Content="配置文件"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:415` — `<Button Content="打开文件夹" Command="{Binding OpenConfigFolderCommand}" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:421` — `<fa:FASettingsExpanderItem Content="启动时自动备份">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:423` — `<ToggleSwitch IsChecked="{Binding Config.AutoBackup}" OnContent="开" OffContent="关" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:427` — `<fa:FASettingsExpanderItem Content="导出 / 导入">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:430` — `<Button Content="导出…" Click="OnExportBackup"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:432` — `<Button Content="导入…" Click="OnImportBackup"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:438` — `<fa:FASettingsExpanderItem Content="备份文件夹"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:441` — `<Button Content="打开文件夹" Command="{Binding OpenBackupFolderCommand}" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:445` — `<fa:FASettingsExpanderItem Content="最近一次备份操作"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:460` — `<fa:FASettingsExpander Background="Transparent" Header="底层设置"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:461` — `Description="如非遇到渲染问题，请勿调整！"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:465` — `<fa:FASettingsExpanderItem Content="渲染后端"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:479` — `<TextBlock Text="改完需要重启程序才生效。"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:493` — `<fa:FASettingsExpander Background="Transparent" Header="插件"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:497` — `<fa:FASettingsExpanderItem Content="状态"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:498` — `Description="插件接口在本版本中暂时关闭。我们正在重新设计插件规范（可暴露的接口类型、权限与版本兼容策略），完成后再开放。已有的插件目录不会被读取，也不会执行。" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:507` — `<fa:FASettingsExpander Background="Transparent" Header="关于" Description="Helium Todo 日程！ 专注！ 自动化！"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:510` — `<fa:FASettingsExpanderItem Content="版本">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:515` — `<fa:FASettingsExpanderItem Content="恢复默认设置">`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:517` — `<Button Content="恢复默认" Command="{Binding ResetToDefaultCommand}" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:526` — `<Button Content="保存设置" Classes="accent" MinWidth="120"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:529` — `<TextBlock Text="修改主题会立刻生效，其它设置点「保存设置」后写入文件。"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:544` — `<TextBlock Text="导入这份备份？" Classes="section" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:559` — `Text="注意，选择替换会覆盖原有数据！" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:562` — `<Button Content="取消" Command="{Binding CancelImportCommand}" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:563` — `<Button Content="合并导入" Classes="accent"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:566` — `<Button Content="替换导入"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:621` — `ToolTip.Tip="关闭"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:658` — `<TextBlock Text="更改此项需要重启，是否立即重启"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:664` — `Text="「确认」会保存选择并立刻重新启动 Helium Todo；「取消更改」则保持现在的设置不变。" />`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:666` — `<Button Content="确认"`
- `HeliumTodo.Desktop\Views\SettingsView.axaml:670` — `<Button Content="取消更改"`

### `HeliumTodo.Desktop\Views\ShellView.axaml`（4 处）

- `HeliumTodo.Desktop\Views\ShellView.axaml:123` — `<Button Classes="windowButton" Click="OnMinimizeClick" ToolTip.Tip="最小化">`
- `HeliumTodo.Desktop\Views\ShellView.axaml:126` — `<Button Classes="windowButton" Click="OnMaximizeClick" ToolTip.Tip="最大化 / 还原">`
- `HeliumTodo.Desktop\Views\ShellView.axaml:129` — `<Button Classes="windowButton close" Click="OnCloseClick" ToolTip.Tip="关闭">`
- `HeliumTodo.Desktop\Views\ShellView.axaml:174` — `ToolTip.Tip="展开 / 收起功能名">`

### `HeliumTodo.Desktop\Views\StudyRoomView.axaml`（31 处）

- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:27` — `<TextBlock Text="自习室" Classes="pageHeader" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:56` — `<TextBlock Text="局域网联机" Classes="cardHeader" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:65` — `<TextBlock Text="我想当房主" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:68` — `PlaceholderText="输入房间名称以创建房间" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:69` — `<Button Grid.Column="1" Content="创建" Classes="accent"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:75` — `<TextBlock Text="我想当房客" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:78` — `PlaceholderText="输入 6 位房间码"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:80` — `<Button Grid.Column="1" Content="加入" Command="{Binding JoinRoomCommand}" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:87` — `<TextBlock Text="输入联机地址" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:90` — `PlaceholderText="例如 192.168.1.5 或 [fe80::1]:47470"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:92` — `<Button Grid.Column="1" Content="连接" Classes="accent"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:106` — `<TextBlock Text="局域网联机默认关闭。要用联机自习，到「设置 → 自习室」把「局域网联机」打开。"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:130` — `<TextBlock Text="房间码" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:142` — `<Button Content="＋ 示例队友" FontSize="11" Padding="9,4"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:143` — `ToolTip.Tip="联机实现前，用来看看多人自习的样子"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:145` — `<Button Content="离开" FontSize="11" Padding="9,4"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:161` — `<TextBlock Classes="caption" Text="向你的同学分享房间IP"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:166` — `<Run Text="端口" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:192` — `<TextBox Text="{Binding FocusTitle}" PlaceholderText="这次专注做什么？" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:194` — `<TextBlock Text="时长" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:198` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:201` — `<Run Text="我今天在自习室的累计专注：" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:207` — `<Button Content="开始专注" Classes="accent" MinWidth="96"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:211` — `<Button Content="结束专注" MinWidth="96"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:222` — `<TextBlock Text="房间成员" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:243` — `Text="{Binding FocusedToday, StringFormat='今日专注 {0:hh\\:mm}'}" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:245` — `Text="{Binding JoinedAt, StringFormat='{}{0:HH:mm} 加入'}" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:253` — `<TextBlock Text="专注中" Foreground="White" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:269` — `<TextBlock Grid.Row="0" Text="房间消息" Classes="cardHeader" />`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:305` — `PlaceholderText="说点什么…（回车发送）"`
- `HeliumTodo.Desktop\Views\StudyRoomView.axaml:307` — `<Button Grid.Column="1" Content="发送" Command="{Binding SendMessageCommand}" />`

### `HeliumTodo.Desktop\Views\TodoView.axaml`（32 处）

- `HeliumTodo.Desktop\Views\TodoView.axaml:52` — `<TextBlock Grid.Column="0" Text="待办事项" Classes="pageHeader"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:59` — `<TextBlock Text="未完成" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:64` — `<TextBlock Text="今天完成" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:69` — `<TextBlock Text="全部" Classes="pageCaption" VerticalAlignment="Center"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:89` — `PlaceholderText="要做点什么？例如：写数据结构实验报告"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:98` — `<ToggleButton x:Name="MoreToggle" Content="更多" FontSize="12" Padding="10,4"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:99` — `ToolTip.Tip="优先级 / 分类 / 预计时长 / 截止时间" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:100` — `<Button Content="取消" Command="{Binding CancelEditCommand}"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:102` — `<Button Content="保存" Classes="accent"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:112` — `<TextBlock Text="优先级" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:124` — `<TextBlock Text="分类" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:131` — `PlaceholderText="分类" ToolTip.Tip="可以自己填，也可以点右面的常用分类" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:132` — `<Button Content="学习" FontSize="10" Padding="6,2" Tag="学习"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:134` — `<Button Content="生活" FontSize="10" Padding="6,2" Tag="生活"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:136` — `<Button Content="社团" FontSize="10" Padding="6,2" Tag="社团"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:141` — `<TextBlock Text="预计" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:144` — `<TextBlock Text="分钟" Classes="caption" VerticalAlignment="Center" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:148` — `<CheckBox Content="截止" IsChecked="{Binding NewHasDueTime}"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:171` — `<RadioButton Content="未完成" GroupName="filter" FontSize="12"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:174` — `<RadioButton Content="今天" GroupName="filter" FontSize="12"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:177` — `<RadioButton Content="已完成" GroupName="filter" FontSize="12"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:180` — `<RadioButton Content="全部" GroupName="filter" FontSize="12"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:186` — `PlaceholderText="搜索标题 / 分类 / 备注…" MaxWidth="260" MinHeight="30"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:189` — `<TextBlock Grid.Column="2" Text="{Binding Items.Count, StringFormat='共 {0} 条'}"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:216` — `ToolTip.Tip="点一下调整优先级"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:241` — `<TextBlock Text="已逾期" FontSize="11" FontWeight="SemiBold"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:253` — `Text="{Binding PlannedStart, StringFormat='已安排 {0:MM-dd HH:mm}'}" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:262` — `Text="{Binding FocusedDuration, StringFormat='已专注 {0:hh\\:mm}'}" />`
- `HeliumTodo.Desktop\Views\TodoView.axaml:276` — `<Button Content="安排时间" FontSize="11" Padding="8,4"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:280` — `<Button Content="取消安排" FontSize="11" Padding="8,4"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:285` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="编辑"`
- `HeliumTodo.Desktop\Views\TodoView.axaml:290` — `<Button FontSize="12" Padding="8,4" ToolTip.Tip="删除"`

## 二、文字样式与字体（.axaml 资源/样式）

共 0 个文件、0 处。

## 三、ViewModel 里拼出来的文字（运行时）

共 9 个文件、198 处。

### `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs`（28 处）

- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:100` — `[ObservableProperty] private string _focusTodayText = "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:105` — `[ObservableProperty] private string _busyText = "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:106` — `[ObservableProperty] private string _freeText = "24 小时";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:297` — `SummaryText = $"加载失败：{ex.Message}";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:388` — `DayOfWeek.Monday => "星期一",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:389` — `DayOfWeek.Tuesday => "星期二",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:390` — `DayOfWeek.Wednesday => "星期三",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:391` — `DayOfWeek.Thursday => "星期四",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:392` — `DayOfWeek.Friday => "星期五",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:393` — `DayOfWeek.Saturday => "星期六",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:394` — `_ => "星期日",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:398` — `var suffix = date == today ? "  ·  今天"`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:399` — `: date == today.AddDays(1) ? "  ·  明天"`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:400` — `: date == today.AddDays(-1) ? "  ·  昨天"`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:403` — `return $"{date.Month}月{date.Day}日  {weekday}{suffix}";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:409` — `if (duration <= TimeSpan.Zero) return "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:414` — `if (hours == 0) return $"{minutes} 分钟";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:415` — `if (minutes == 0) return $"{hours} 小时";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:416` — `return $"{hours} 小时 {minutes} 分";`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:436` — `ConflictKind.ScheduleOverlap => "日程重叠",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:437` — `ConflictKind.FocusOverlapsSchedule => "专注冲突",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:438` — `ConflictKind.TodoDueInBusyTime => "截止风险",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:439` — `ConflictKind.PlannedTodoOverlapsSchedule => "安排冲突",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:440` — `ConflictKind.OverloadedDay => "安排过满",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:441` — `_ => "提示",`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:514` — `public string DurationText => "空闲 " + DashboardViewModel.FormatDuration(Duration);`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:518` — `? "可以安排一段专注"`
- `HeliumTodo.Desktop\ViewModels\DashboardViewModel.cs:519` — `: $"可安排「{Todo.Title}」（预计 {Todo.EstimatedDuration.TotalMinutes:0} 分钟）";`

### `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs`（27 处）

- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:78` — `[ObservableProperty] private string _phaseText = "准备开始";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:102` — `[ObservableProperty] private string _progressText = "还没有开始";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:121` — `[ObservableProperty] private string _todayTotalText = "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:123` — `[ObservableProperty] private string _allTimeTotalText = "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:124` — `[ObservableProperty] private string _averageSessionText = "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:183` — `? TargetTodo?.Title ?? "专注"`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:209` — `ShowNotice($"已放弃本次专注（坚持了 {result.ActualDuration.TotalMinutes:0} 分钟）。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:231` — `ShowNotice($"本次专注 {actual.TotalMinutes:0} 分钟已记录。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:250` — `ShowNotice($"已开始「{reservation.Title}」。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:260` — `ShowNotice("已取消该预订。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:275` — `? (ReserveTodo?.Title ?? "预订专注")`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:285` — `ShowNotice(error ?? "预订信息不合法。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:294` — `ShowNotice($"已预订 {session.PlannedStart:MM-dd HH:mm} 的 {minutes} 分钟专注，" +`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:295` — `"它会出现在「今日工作流」的时间轴上。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:346` — `PhaseText = "准备开始";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:349` — `ProgressText = "还没有开始";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:353` — `PhaseText = "专注中";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:356` — `ProgressText = $"已进行 {_timer.Elapsed.TotalMinutes:0.#} / {_timer.PlannedDuration.TotalMinutes:0} 分钟";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:360` — `PhaseText = "已暂停";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:363` — `ProgressText = "点「继续」接着专注";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:367` — `PhaseText = "休息中";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:370` — `ProgressText = "起来走两步，放松一下眼睛";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:388` — `ShowNotice($"完成 {e.ActualDuration.TotalMinutes:0} 分钟专注，休息一下吧。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:404` — `ShowNotice("这次专注结束时的收尾动作出错了，数据可能没记全。");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:452` — `ShowNotice($"保存专注记录失败：{ex.Message}");`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:486` — `: "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\FocusViewModel.cs:531` — `ShowNotice($"加载失败：{ex.Message}");`

### `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs`（8 处）

- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:34` — `new ReminderOption(null, "不提醒"),`
- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:35` — `new ReminderOption(0, "开始时"),`
- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:36` — `new ReminderOption(5, "提前 5 分钟"),`
- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:37` — `new ReminderOption(10, "提前 10 分钟"),`
- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:38` — `new ReminderOption(30, "提前 30 分钟"),`
- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:39` — `new ReminderOption(60, "提前 1 小时"),`
- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:40` — `new ReminderOption(120, "提前 2 小时"),`
- `HeliumTodo.Desktop\ViewModels\ReminderEditState.cs:41` — `new ReminderOption(null, "自定义…", IsCustom: true),`

### `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs`（22 处）

- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:54` — `[ObservableProperty] private string _busyTodayText = "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:64` — `public string EmptyHint => Upcoming.Count == 0 ? "接下来还没有安排。" : string.Empty;`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:88` — `new RecurrenceOption("none", "不重复"),`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:89` — `new RecurrenceOption("daily", "每天"),`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:90` — `new RecurrenceOption("weekly", "每周"),`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:149` — `public string EditorTitle => IsEditing ? "编辑日程" : "新建日程";`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:221` — `ErrorMessage = "标题不能为空。";`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:243` — `ErrorMessage = error ?? "输入不合法。";`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:366` — `SummaryText = $"接下来 {LookaheadDays} 天共 {occurrences.Count} 项安排" +`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:367` — `$"，其中今天 {TodayCount} 项。";`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:374` — `ErrorMessage = $"加载失败：{ex.Message}";`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:455` — `DayOfWeek.Monday => "星期一",`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:456` — `DayOfWeek.Tuesday => "星期二",`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:457` — `DayOfWeek.Wednesday => "星期三",`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:458` — `DayOfWeek.Thursday => "星期四",`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:459` — `DayOfWeek.Friday => "星期五",`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:460` — `DayOfWeek.Saturday => "星期六",`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:461` — `_ => "星期日",`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:465` — `var suffix = date == today ? "  ·  今天"`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:466` — `: date == today.AddDays(1) ? "  ·  明天"`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:469` — `HeaderText = $"{date.Month}月{date.Day}日  {weekday}{suffix}";`
- `HeliumTodo.Desktop\ViewModels\ScheduleViewModel.cs:482` — `public string CountText => $"{Items.Count} 项";`

### `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs`（54 处）

- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:245` — `? "已切换到暗色模式：壁纸会自动压暗一档，浓度可以在「背景壁纸」里调。"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:246` — `: "已切换到亮色模式。";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:316` — `AccentSourceDefs.Wallpaper => "强调色改为跟随壁纸：壁纸换一帧，强调色也跟着重新算。",`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:317` — `AccentSourceDefs.Custom => "强调色改为自定义：下面那块调色板现在可以拖了。",`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:318` — `_ => "强调色改回跟随主题。",`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:384` — `[ObservableProperty] private string _wallpaperStatusText = "还没有选壁纸";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:407` — `WallpaperStatusText = "壁纸已关闭";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:415` — `WallpaperStatusText = "请先选一张图片";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:434` — `WallpaperStatusText = "还没有选壁纸";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:485` — `WallpaperStatusText = "正在加载…";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:492` — `: $"打不开这张图：{_wallpaper.Error}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:502` — `WallpaperStatusText = "还没有选壁纸";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:506` — `WallpaperStatusText = "壁纸已关闭";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:514` — `WallpaperStatusText = "还没加载";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:521` — `? $"动图 {_wallpaper.FrameCount}/{_wallpaper.SourceFrameCount} 帧"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:522` — `: "静态图";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:524` — `return $"{_wallpaper.PixelWidth}×{_wallpaper.PixelHeight} · {kind} · 主色调 {_wallpaper.Accent.ToHex()}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:545` — `? "已打开局域网联机：可以去自习室建房，或者填同学的 IP 连过去。"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:546` — `: "已关闭局域网联机：自习室只在本机用，不再监听端口。";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:632` — `new(RenderBackendSettings.Auto, "Avalonia 自带渲染器（默认，最流畅）"),`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:633` — `new(RenderBackendSettings.Software, "软件渲染（省内存，动画会掉帧）"),`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:704` — `NoticeText = $"渲染后端已保存为「{backendName}」。检测到正在调试器里运行，"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:705` — `+ "自动重启会把调试器甩掉、还会占住 exe 让下次编译失败；"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:706` — `+ "请用调试器的「重新启动」(Ctrl+Shift+F5)，或停止调试后重新运行。";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:712` — `NoticeText = $"渲染后端已保存为「{backendName}」，"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:713` — `+ "但自动重启没成功，请手动关闭后重新打开 Helium Todo。";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:749` — `NoticeText = $"已取消更改，渲染后端仍是「{backTo.DisplayText}」。";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:799` — `$"当前生效：{activeText}（由环境变量 HELIUM_RENDER 强制指定，这一项暂时不起作用）";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:805` — `? $"当前生效：{activeText}"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:806` — `: $"当前生效：{activeText}；重启程序后改为 {RenderBackendSettings.DisplayName(selectedKey)}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:844` — `NoticeText = $"配置已保存到：{_configService.ConfigFilePath}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:902` — `NoticeText = "已恢复默认设置。";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:937` — `[ObservableProperty] private string _backupStatusText = "还没有导出或导入过";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:977` — `BackupStatusText = "正在打包…";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:987` — `BackupStatusText = $"原位置不能写入，已改存到备份文件夹：{Path.GetFileName(result.Path)}"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:989` — `NoticeText = "那个文件夹系统不让写（多半是「受控文件夹访问」），备份已经放到「备份文件夹」里了。";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:994` — `BackupStatusText = $"已导出 {Path.GetFileName(result.Path)}"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1001` — `BackupStatusText = $"导出失败：{ex.Message}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1020` — `BackupStatusText = "正在检查这份备份…";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1028` — `BackupStatusText = $"这份文件不能导入：{inspection.Error}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1042` — `BackupStatusText = $"读不了这份备份：{ex.Message}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1074` — `? "正在替换…（会先备份当前数据）"`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1075` — `: "正在合并…";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1090` — `BackupStatusText = $"导入失败：{ex.Message}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1191` — `NoticeText = $"打开文件夹失败：{ex.Message}（{path}）";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1208` — `reasons.Add("Avalonia Launcher：系统没有接受这次打开请求");`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1221` — `("外壳启动资源管理器", new ProcessStartInfo`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1227` — `("系统默认方式", new ProcessStartInfo`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1233` — `("直接启动资源管理器", new ProcessStartInfo`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1245` — `("经 cmd start 打开", new ProcessStartInfo`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1268` — `NoticeText = $"打开文件夹失败（{string.Join("；", reasons)}）路径：{path}";`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1288` — `AccentSourceDefs.Wallpaper => "从壁纸里自动提一个主色调",`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1289` — `AccentSourceDefs.Custom => "用下面的调色板自己调",`
- `HeliumTodo.Desktop\ViewModels\SettingsViewModel.cs:1290` — `_ => "用主题自带的那套颜色",`

### `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs`（7 处）

- `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs:170` — `"今日工作流",`
- `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs:175` — `"日程",`
- `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs:180` — `"待办事项",`
- `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs:185` — `"专注",`
- `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs:190` — `"自习室",`
- `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs:196` — `"设置",`
- `HeliumTodo.Desktop\ViewModels\ShellViewModel.cs:230` — `StatusText = $"{item.Title} 加载失败：{ex.Message}";`

### `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs`（30 处）

- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:46` — `_roomName = "期末冲刺自习室";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:129` — `public string CurrentRoomName => CurrentRoom?.Name ?? "还没有加入自习室";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:132` — `public string MemberCountText => $"{Members.Count} 人在线";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:135` — `[ObservableProperty] private string _myFocusTodayText = "0 分钟";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:145` — `[ObservableProperty] private string _focusTitle = "和大家一起专注";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:182` — `if (!IsOnlineAvailable) return "当前是本地模式，没法联机。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:186` — `return "局域网联机默认关闭：到「设置 → 自习室」打开「局域网联机」后，才能和同学一起自习。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:190` — `? $"已连接 · 共 {_roomService.LocalPort} 端口在监听"`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:191` — `: "局域网联机已就绪：填对方的 IP 点「连接」，或者让对方填你的地址。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:220` — `return rtt <= 0 ? "正在测延迟…" : $"延迟 {rtt} 毫秒";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:235` — `var latency = p.RoundTripMs > 0 ? $"{p.RoundTripMs} ms" : "未测";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:252` — `: "未启动";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:270` — `NoticeText = "先填对方的 IP 地址，例如 192.168.1.5 或 [fe80::1]:47470";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:276` — `NoticeText = "局域网联机是关着的。到「设置 → 自习室」打开「局域网联机」再来。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:280` — `NoticeText = "正在连接…";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:287` — `NoticeText = ok ? "连上了，可以让对方开始专注了。" : "没连上，看看地址和房间码对不对。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:319` — `NoticeText = "请输入房间码。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:337` — `NoticeText = $"已回到本地房间「{local.Name}」。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:345` — `? "本地找不到这个房间。联机是关着的，先到「设置 → 自习室」打开「局域网联机」。"`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:346` — `: "本地找不到这个房间，而且当前是本地模式，没法联机。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:353` — `? $"本地没有这个房间。请在下面填上房主的 IP 地址，再点「连接」。"`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:354` — `: "正在连接…";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:377` — `NoticeText = "已离开自习室。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:386` — `NoticeText = "先创建或加入一个自习室吧。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:391` — `var title = string.IsNullOrWhiteSpace(FocusTitle) ? "和大家一起专注" : FocusTitle;`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:448` — `NoticeText = "先创建一个自习室。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:452` — `var names = new[] { "小张", "小李", "小王", "阿伟", "小陈" };`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:458` — `NoticeText = "示例队友已经加满了。";`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:478` — `SenderName = "系统",`
- `HeliumTodo.Desktop\ViewModels\StudyRoomViewModel.cs:479` — `Content = $"「{name}」加入了自习室。",`

### `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs`（12 处）

- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:179` — `TimelineEntryKind.Schedule => "日程",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:180` — `TimelineEntryKind.PlannedTodo => "待办（已安排时间）",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:181` — `TimelineEntryKind.FocusReservation => "预订的专注",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:182` — `TimelineEntryKind.RunningFocus => "正在专注",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:183` — `_ => "条目",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:186` — `var text = $"{Title}\n{kindText} · {TimeRangeText}\n优先级：{PriorityText}";`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:195` — `text += $"\n⚠ 与其它安排重叠（错位显示在第 {Lane + 1} / {LaneCount} 列）";`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:200` — `text += "\n⚠ 存在时间冲突";`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:210` — `Priority.Urgent => "紧急",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:211` — `Priority.High => "较高",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:212` — `Priority.Low => "较低",`
- `HeliumTodo.Desktop\ViewModels\TimelineEntryViewModel.cs:213` — `_ => "普通",`

### `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs`（10 处）

- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:55` — `[ObservableProperty] private string _emptyHint = "还没有待办，用下面的输入框加一个吧。";`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:90` — `[ObservableProperty] private string _newCategory = "学习";`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:114` — `public string EditorTitle => IsEditing ? "编辑待办" : "新建待办";`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:134` — `EmptyHint = "标题不能为空哦。";`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:222` — `NewCategory = todo.Category ?? "学习";`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:344` — `EmptyHint = $"加载失败：{ex.Message}";`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:405` — `TodoFilter.Completed => "还没有完成任何待办。",`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:406` — `TodoFilter.Today => "今天没有要做的待办，轻松一下也不错。",`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:407` — `TodoFilter.Active => "没有未完成的待办，全清空了！",`
- `HeliumTodo.Desktop\ViewModels\TodoViewModel.cs:408` — `_ => "还没有待办，用上面的输入框加一个吧。",`

## 四、Core / Data 层拼出来的文字（运行时）

共 39 个文件、250 处。

### `HeliumTodo.Application\Configuration\AccentSources.cs`（3 处）

- `HeliumTodo.Application\Configuration\AccentSources.cs:57` — `Wallpaper => "跟随壁纸",`
- `HeliumTodo.Application\Configuration\AccentSources.cs:58` — `Custom => "自定义颜色",`
- `HeliumTodo.Application\Configuration\AccentSources.cs:59` — `_ => "跟随主题",`

### `HeliumTodo.Application\Configuration\AppConfig.cs`（2 处）

- `HeliumTodo.Application\Configuration\AppConfig.cs:290` — `FallbackReason = $"{reason}；备用目录也不可用：{secondReason}";`
- `HeliumTodo.Application\Configuration\AppConfig.cs:320` — `reason = $"{directory} 不可写（{ex.GetType().Name}: {ex.Message}）";`

### `HeliumTodo.Application\Configuration\ColorSchemes.cs`（3 处）

- `HeliumTodo.Application\Configuration\ColorSchemes.cs:51` — `"深色" or "夜间" or "night" or "dim" => Dark,`
- `HeliumTodo.Application\Configuration\ColorSchemes.cs:52` — `"浅色" or "日间" or "day" => Light,`
- `HeliumTodo.Application\Configuration\ColorSchemes.cs:59` — `Normalize(value) == Dark ? "暗色" : "亮色";`

### `HeliumTodo.Application\Configuration\RenderBackendSettings.cs`（2 处）

- `HeliumTodo.Application\Configuration\RenderBackendSettings.cs:94` — `Auto => "Avalonia 自带渲染器",`
- `HeliumTodo.Application\Configuration\RenderBackendSettings.cs:97` — `_ => "软件渲染",`

### `HeliumTodo.Application\Ipc\IIpcChannel.cs`（1 处）

- `HeliumTodo.Application\Ipc\IIpcChannel.cs:61` — `=> Task.FromResult(IpcResponse.Failure("没有可用的进程间通道。"));`

### `HeliumTodo.Application\Plugins\IHeliumPlugin.cs`（2 处）

- `HeliumTodo.Application\Plugins\IHeliumPlugin.cs:20` — `[Obsolete("0.5.0-alpha 起暂时禁用，见 DEVELOPMENT_SPEC.md §9")]`
- `HeliumTodo.Application\Plugins\IHeliumPlugin.cs:82` — `[Obsolete("0.5.0-alpha 起暂时禁用，见 DEVELOPMENT_SPEC.md §9")]`

### `HeliumTodo.Application\Services\StudyRoomService.cs`（23 处）

- `HeliumTodo.Application\Services\StudyRoomService.cs:108` — `Name = string.IsNullOrWhiteSpace(name) ? "我的自习室" : name,`
- `HeliumTodo.Application\Services\StudyRoomService.cs:109` — `HostName = string.IsNullOrWhiteSpace(hostName) ? "我" : hostName,`
- `HeliumTodo.Application\Services\StudyRoomService.cs:133` — `AddSystemMessage($"房间「{room.Name}」已创建，房间码 {room.Code}。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:151` — `AddSystemMessage("当前是本地模式，没法连到别人的房间。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:155` — `AddSystemMessage($"准备加入房间 {roomCode.Trim().ToUpperInvariant()}，请填写房主地址后点「连接」。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:173` — `AddSystemMessage("当前是本地模式，没法联机。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:181` — `AddSystemMessage("请先填房间码。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:187` — `AddSystemMessage("请先填对方的 IP 地址。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:197` — `AddSystemMessage("没连上，检查一下地址、房间码，以及对方的防火墙是否放行了这个端口。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:205` — `Name = $"{displayName} 的自习室",`
- `HeliumTodo.Application\Services\StudyRoomService.cs:214` — `DisplayName = string.IsNullOrWhiteSpace(displayName) ? "我" : displayName,`
- `HeliumTodo.Application\Services\StudyRoomService.cs:220` — `AddSystemMessage($"已连上 {host.Trim()}，房间码 {code}。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:231` — `AddSystemMessage("已离开房间。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:250` — `AddSystemMessage($"「{(me?.DisplayName ?? "我")}」开始了 {duration.TotalMinutes:0} 分钟的专注：{title}");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:254` — `me?.DisplayName ?? "我",`
- `HeliumTodo.Application\Services\StudyRoomService.cs:275` — `AddSystemMessage($"专注结束，本次 {actualDuration.TotalMinutes:0} 分钟。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:279` — `me?.DisplayName ?? "我",`
- `HeliumTodo.Application\Services\StudyRoomService.cs:288` — `public void SendMessage(string content, string senderName = "我")`
- `HeliumTodo.Application\Services\StudyRoomService.cs:316` — `SenderName = "系统",`
- `HeliumTodo.Application\Services\StudyRoomService.cs:350` — `DisplayName = string.IsNullOrWhiteSpace(presence.DisplayName) ? "同学" : presence.DisplayName,`
- `HeliumTodo.Application\Services\StudyRoomService.cs:358` — `AddSystemMessage($"「{(string.IsNullOrWhiteSpace(presence.DisplayName) ? "同学" : presence.DisplayName)}」进来了。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:367` — `AddSystemMessage($"「{member.DisplayName}」离开了。");`
- `HeliumTodo.Application\Services\StudyRoomService.cs:384` — `SenderName = string.IsNullOrWhiteSpace(message.DisplayName) ? "同学" : message.DisplayName,`

### `HeliumTodo.Desktop\App.axaml.cs`（29 处）

- `HeliumTodo.Desktop\App.axaml.cs:149` — `LogBackground("[插件] 检测到 plugins 目录，本版本不加载");`
- `HeliumTodo.Desktop\App.axaml.cs:173` — `LogBackground("[单实例] 已经有一份在跑，已请它把窗口显示出来，本进程退出");`
- `HeliumTodo.Desktop\App.axaml.cs:210` — `LogBackground($"[退出] 进程正常结束，退出码 {e.ApplicationExitCode}");`
- `HeliumTodo.Desktop\App.axaml.cs:236` — `LogBackground("[提醒] 后台扫描已启动（每 30 秒一次）");`
- `HeliumTodo.Desktop\App.axaml.cs:251` — `LogBackground($"[环境] 会话={System.Diagnostics.Process.GetCurrentProcess().SessionId}"`
- `HeliumTodo.Desktop\App.axaml.cs:252` — `+ $" 管理员={IsElevated()}"`
- `HeliumTodo.Desktop\App.axaml.cs:253` — `+ $" 资源管理器={DescribeExplorer()}"`
- `HeliumTodo.Desktop\App.axaml.cs:254` — `+ $" 渲染后端={Application.Configuration.RenderBackendSettings.DisplayName(HeliumTodo.Desktop.Services.RenderBackendChoice.ActiveKey)}"`
- `HeliumTodo.Desktop\App.axaml.cs:269` — `LogBackground("[托盘] 启动失败：没有托盘图标，但关闭仍会留在后台"`
- `HeliumTodo.Desktop\App.axaml.cs:270` — `+ "（再双击一次程序即可唤回窗口）");`
- `HeliumTodo.Desktop\App.axaml.cs:276` — `? "[托盘] 自检模式：假装托盘已就绪（没有真的图标）"`
- `HeliumTodo.Desktop\App.axaml.cs:277` — `: "[托盘] 已启动，关闭按钮改为藏窗口");`
- `HeliumTodo.Desktop\App.axaml.cs:300` — `? "[窗口] 关闭按钮被拦截，已隐藏到托盘（进程继续运行）"`
- `HeliumTodo.Desktop\App.axaml.cs:301` — `: "[窗口] 关闭按钮被拦截，已隐藏（没有托盘图标，再双击程序可唤回）");`
- `HeliumTodo.Desktop\App.axaml.cs:346` — `return "没有运行";`
- `HeliumTodo.Desktop\App.axaml.cs:350` — `return $"PID {string.Join(',', processes.Select(p => p.Id))} 会话 {string.Join(',', sessions)}";`
- `HeliumTodo.Desktop\App.axaml.cs:354` — `return "查不到";`
- `HeliumTodo.Desktop\App.axaml.cs:418` — `? "[备份] 这次没做成"`
- `HeliumTodo.Desktop\App.axaml.cs:419` — `: $"[备份] 已生成 {path}");`
- `HeliumTodo.Desktop\App.axaml.cs:653` — `? $"[启动] 首次运行，已写入示例数据：{databasePath}"`
- `HeliumTodo.Desktop\App.axaml.cs:654` — `: $"[启动] 数据库就绪：{databasePath}");`
- `HeliumTodo.Desktop\App.axaml.cs:662` — `Console.Error.WriteLine($"[启动] 数据库初始化失败：{ex.Message}（库路径：{configService.ResolveDatabasePath()}）");`
- `HeliumTodo.Desktop\App.axaml.cs:696` — `LogBackground("[重启] 检测到调试器，放弃自动重启："`
- `HeliumTodo.Desktop\App.axaml.cs:697` — `+ "请用调试器的「重新启动」(Ctrl+Shift+F5)，"`
- `HeliumTodo.Desktop\App.axaml.cs:698` — `+ "否则新进程不受调试器管理，还会占住 exe 让下次编译失败");`
- `HeliumTodo.Desktop\App.axaml.cs:705` — `LogBackground("[重启] 拿不到当前可执行文件路径，放弃自动重启");`
- `HeliumTodo.Desktop\App.axaml.cs:718` — `LogBackground($"[重启] 释放单实例锁失败（不影响继续重启）：{ex.Message}");`
- `HeliumTodo.Desktop\App.axaml.cs:732` — `LogBackground($"[重启] 已启动新进程：{exePath}");`
- `HeliumTodo.Desktop\App.axaml.cs:736` — `LogBackground($"[重启] 启动新进程失败：{ex.Message}");`

### `HeliumTodo.Desktop\Converters\CommonConverters.cs`（13 处）

- `HeliumTodo.Desktop\Converters\CommonConverters.cs:31` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:50` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:95` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:114` — `HeliumTodo.Domain.Models.Priority.Urgent => "紧急",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:115` — `HeliumTodo.Domain.Models.Priority.High => "较高",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:116` — `HeliumTodo.Domain.Models.Priority.Low => "较低",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:117` — `_ => "普通",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:122` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:142` — `HeliumTodo.Domain.Models.TimeBlock.Morning => "上午",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:143` — `HeliumTodo.Domain.Models.TimeBlock.Afternoon => "下午",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:144` — `HeliumTodo.Domain.Models.TimeBlock.Evening => "晚上",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:145` — `_ => "不限",`
- `HeliumTodo.Desktop\Converters\CommonConverters.cs:150` — `=> throw new NotSupportedException("这个转换器只支持单向绑定。");`

### `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs`（18 处）

- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:291` — `error = "这台电脑上没有可用的 Media Foundation，解不了视频。";`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:303` — `ThrowIfFailed(MFCreateAttributes(out attributes, 4), "创建属性袋");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:306` — `ThrowIfFailed(attributes!.SetUINT32(ref ReaderEnableVideoProcessing, enable), "打开视频处理器");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:320` — `ThrowIfFailed(MFCreateSourceReaderFromURL(path, attributes, out reader), "打开视频文件");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:323` — `ThrowIfFailed(reader!.SetStreamSelection(AllStreams, 0), "关掉所有流");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:324` — `ThrowIfFailed(reader.SetStreamSelection(FirstVideoStream, 1), "打开视频流");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:327` — `ThrowIfFailed(reader.GetNativeMediaType(FirstVideoStream, 0, out var nativeType), "读取视频原始格式");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:328` — `ThrowIfFailed(nativeType.GetUINT64(ref AttributeFrameSize, out var packed), "读取原始分辨率");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:334` — `error = "这个视频没有读到有效的画面尺寸。";`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:364` — `+ $"A(改当前+缩放)=0x{resultA:X8} B(新建+缩放)=0x{resultB:X8} C(不缩放)=0x{resultC:X8}");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:379` — `error = "这个视频的编码方式这台电脑解不了"`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:380` — `+ $"（常见于 HEVC / AV1，需要装系统解码器）。错误码 0x{lastResult:X8}";`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:466` — `error = "这个视频里没有解出任何一帧。";`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:509` — `+ $"时间戳 {string.Join(",", timestamps.Take(4))}；"`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:510` — `+ $"间隔 {string.Join(",", delays.Take(4))} ms；MinFrameGap={MinFrameGap}");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:519` — `error = $"解析视频失败：{ex.Message}";`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:540` — `ThrowIfFailed(sample.ConvertToContiguousBuffer(out var buffer), "取出帧数据");`
- `HeliumTodo.Desktop\Services\MediaFoundationVideo.cs:544` — `ThrowIfFailed(buffer.Lock(out var source, out _, out _), "锁定帧内存");`

### `HeliumTodo.Desktop\Services\ReminderService.cs`（2 处）

- `HeliumTodo.Desktop\Services\ReminderService.cs:117` — `Trace?.Invoke($"[提醒] 弹出「{item.Title}」：{item.Message}");`
- `HeliumTodo.Desktop\Services\ReminderService.cs:123` — `Trace?.Invoke($"[提醒] 本轮扫到 {due.Count} 条");`

### `HeliumTodo.Desktop\Services\TrayService.cs`（12 处）

- `HeliumTodo.Desktop\Services\TrayService.cs:164` — `Trace?.Invoke($"托盘窗口创建失败，错误码 {Marshal.GetLastWin32Error()}");`
- `HeliumTodo.Desktop\Services\TrayService.cs:195` — `Trace?.Invoke($"托盘图标：window=0x{_window.ToInt64():X} icon=0x{_icon.ToInt64():X} "`
- `HeliumTodo.Desktop\Services\TrayService.cs:201` — `Trace?.Invoke($"托盘图标加不上（错误码 {lastError}）：{DescribeShellError(lastError)}");`
- `HeliumTodo.Desktop\Services\TrayService.cs:262` — `AppendMenuW(menu, MF_STRING, ID_SHOW, "显示主界面");`
- `HeliumTodo.Desktop\Services\TrayService.cs:263` — `AppendMenuW(menu, MF_STRING, ID_BACKUP, "立即备份一次");`
- `HeliumTodo.Desktop\Services\TrayService.cs:265` — `AppendMenuW(menu, MF_STRING, ID_EXIT, "退出 Helium Todo");`
- `HeliumTodo.Desktop\Services\TrayService.cs:485` — `0 => "系统没给出错误码（多半是这次调用根本没到 shell）。",`
- `HeliumTodo.Desktop\Services\TrayService.cs:486` — `5 => "拒绝访问：当前会话没有可用的通知区域（远程桌面 / 无交互会话常见），"`
- `HeliumTodo.Desktop\Services\TrayService.cs:487` — `+ "或者程序以管理员身份运行而资源管理器不是。",`
- `HeliumTodo.Desktop\Services\TrayService.cs:488` — `87 => "参数不对：结构体大小和系统期望的不一致。",`
- `HeliumTodo.Desktop\Services\TrayService.cs:489` — `1400 => "窗口句柄无效。",`
- `HeliumTodo.Desktop\Services\TrayService.cs:490` — `_ => "请把这一行连同错误码一起反馈。",`

### `HeliumTodo.Desktop\Services\WallpaperDecoder.cs`（7 处）

- `HeliumTodo.Desktop\Services\WallpaperDecoder.cs:124` — `if (bytes.Length == 0) throw new InvalidOperationException("壁纸文件是空的。");`
- `HeliumTodo.Desktop\Services\WallpaperDecoder.cs:136` — `?? throw new InvalidOperationException("认不出这张图的格式（支持 PNG / JPG / APNG / GIF）。");`
- `HeliumTodo.Desktop\Services\WallpaperDecoder.cs:187` — `throw new PlatformNotSupportedException("视频壁纸目前只支持 Windows 系统。");`
- `HeliumTodo.Desktop\Services\WallpaperDecoder.cs:197` — `throw new InvalidOperationException(error ?? "这个视频解不出来。");`
- `HeliumTodo.Desktop\Services\WallpaperDecoder.cs:322` — `if (frames.Count == 0) throw new InvalidOperationException("这张 APNG 里没有能显示的帧。");`
- `HeliumTodo.Desktop\Services\WallpaperDecoder.cs:413` — `throw new InvalidOperationException("这张图没有有效的尺寸。");`
- `HeliumTodo.Desktop\Services\WallpaperDecoder.cs:469` — `if (frames.Count == 0) throw new InvalidOperationException("这张图解不出任何一帧。");`

### `HeliumTodo.Desktop\Services\WallpaperService.cs`（1 处）

- `HeliumTodo.Desktop\Services\WallpaperService.cs:123` — `Error = $"找不到这个文件：{path}";`

### `HeliumTodo.Desktop\ViewLocator.cs`（2 处）

- `HeliumTodo.Desktop\ViewLocator.cs:45` — `return new TextBlock { Text = $"找不到界面：{viewName}" };`
- `HeliumTodo.Desktop\ViewLocator.cs:54` — `return new TextBlock { Text = $"创建界面失败：{viewName}\n{ex.Message}" };`

### `HeliumTodo.Desktop\Views\MainWindow.axaml.cs`（5 处）

- `HeliumTodo.Desktop\Views\MainWindow.axaml.cs:270` — `App.LogBackground($"[图标轨] 第 {frame} 帧 间隔 {elapsed - last:0.0} ms 已用 {elapsed:0.0} ms");`
- `HeliumTodo.Desktop\Views\MainWindow.axaml.cs:314` — `App.LogBackground("[窗口] 状态 → 最小化，动画就地收尾");`
- `HeliumTodo.Desktop\Views\MainWindow.axaml.cs:322` — `App.LogBackground($"[窗口] 状态 → {state}（托盘唤回，出现动画由 App 播）");`
- `HeliumTodo.Desktop\Views\MainWindow.axaml.cs:329` — `App.LogBackground($"[窗口] 状态 → {state}，从最小化恢复，补播出现动画");`
- `HeliumTodo.Desktop\Views\MainWindow.axaml.cs:333` — `App.LogBackground($"[窗口] 状态 → {state}");`

### `HeliumTodo.Desktop\Views\SettingsView.axaml.cs`（8 处）

- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:53` — `Title = "选一张背景壁纸",`
- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:60` — `new FilePickerFileType("图片（PNG / GIF / APNG / JPG / WebP）")`
- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:65` — `new FilePickerFileType("视频（MP4 / MOV / WMV 等）")`
- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:70` — `new FilePickerFileType("所有文件")`
- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:96` — `Title = "导出全部数据",`
- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:102` — `new FilePickerFileType("Helium Todo 备份")`
- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:127` — `Title = "选择一份 Helium Todo 备份",`
- `HeliumTodo.Desktop\Views\SettingsView.axaml.cs:131` — `new FilePickerFileType("Helium Todo 备份")`

### `HeliumTodo.Desktop\Windowing\WindowAnimator.cs`（2 处）

- `HeliumTodo.Desktop\Windowing\WindowAnimator.cs:157` — `trace($"[开场动画] 第 {_introFrame} 帧 progress={progress:0.00} 遮罩={_veilBrush.Color.A}");`
- `HeliumTodo.Desktop\Windowing\WindowAnimator.cs:173` — `trace("[开场动画] 等不到下一帧，强制收尾");`

### `HeliumTodo.Desktop\Windowing\WindowChrome.cs`（1 处）

- `HeliumTodo.Desktop\Windowing\WindowChrome.cs:98` — `Trace($"[窗口] 没能显式开启 DWM 过渡（hr=0x{hr:X8}），沿用系统默认");`

### `HeliumTodo.Domain\Backup\BackupInspection.cs`（9 处）

- `HeliumTodo.Domain\Backup\BackupInspection.cs:21` — `BackupImportMode.Merge => "合并（保留现有数据）",`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:22` — `_ => "替换（清空现有数据）",`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:27` — `BackupImportMode.Merge => "现有数据全部保留，备份里的记录以新编号追加进来。",`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:28` — `_ => "现有数据会被清空，完全变成备份里的内容。导入前会自动存一份当前数据的备份。",`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:81` — `return Error ?? "这个文件不能导入。";`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:86` — `ExportedAt.ToString("yyyy-MM-dd HH:mm") + " 导出",`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:87` — `string.IsNullOrWhiteSpace(AppVersion) ? "版本未知" : "v" + AppVersion.TrimStart('v', 'V'),`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:92` — `parts.Add(HasConfig ? "含配置" : "无配置");`
- `HeliumTodo.Domain\Backup\BackupInspection.cs:93` — `parts.Add(HasWallpaper ? $"含壁纸（{WallpaperFileName}）" : "无壁纸");`

### `HeliumTodo.Domain\Backup\BackupManifest.cs`（4 处）

- `HeliumTodo.Domain\Backup\BackupManifest.cs:44` — `$"{Schedules} 条日程 · {Todos} 条待办 · {FocusSessions} 条专注记录" +`
- `HeliumTodo.Domain\Backup\BackupManifest.cs:45` — `(StudyRooms > 0 ? $" · {StudyRooms} 个自习室" : string.Empty);`
- `HeliumTodo.Domain\Backup\BackupManifest.cs:108` — `error = "清单内容为空。";`
- `HeliumTodo.Domain\Backup\BackupManifest.cs:117` — `error = $"清单不是合法的 JSON：{ex.Message}";`

### `HeliumTodo.Domain\Backup\BackupPackageReader.cs`（14 处）

- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:51` — `throw new BackupPackageException($"找不到备份文件：{filePath}");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:63` — `throw new BackupPackageException("这个文件不是有效的 .zip，可能下载不完整或已经损坏。");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:67` — `throw new BackupPackageException($"打不开这个文件：{ex.Message}");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:74` — `"这个 zip 里没有 manifest.json，不是 Helium Todo 导出的备份。");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:78` — `?? throw new BackupPackageException(error ?? "清单解析失败。");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:102` — `error = $"备份里缺少文件：{entry.Entry}";`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:109` — `error = $"文件校验失败：{entry.Entry}（可能传输过程中损坏了）";`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:123` — `?? throw new BackupPackageException($"备份里缺少文件：{entryName}");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:133` — `?? throw new BackupPackageException($"备份里缺少文件：{entryName}");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:169` — `$"这个 zip 不是 Helium Todo 的备份（清单里的格式标识是「{manifest.Format}」）。");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:175` — `$"这份备份来自更新版本的程序（备份格式 v{manifest.FormatVersion}），" +`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:176` — `$"当前程序最多支持 v{BackupFormat.CurrentVersion}。请先升级程序再导入。");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:181` — `throw new BackupPackageException($"备份格式版本无效（v{manifest.FormatVersion}）。");`
- `HeliumTodo.Domain\Backup\BackupPackageReader.cs:186` — `throw new BackupPackageException("清单里没有数据库条目，这个备份是空的。");`

### `HeliumTodo.Domain\Backup\BackupPackageWriter.cs`（5 处）

- `HeliumTodo.Domain\Backup\BackupPackageWriter.cs:78` — `throw new ArgumentException("数据库内容是空的，不能导出。", nameof(content));`
- `HeliumTodo.Domain\Backup\BackupPackageWriter.cs:185` — `$"系统不允许往「{directory}」里写文件。常见原因：Windows 安全中心的「受控文件夹访问」把这个程序拦住了，"`
- `HeliumTodo.Domain\Backup\BackupPackageWriter.cs:186` — `+ "或者这个文件夹本身只读 / 需要管理员权限。",`
- `HeliumTodo.Domain\Backup\BackupPackageWriter.cs:187` — `DirectoryNotFoundException => $"目录「{directory}」不存在，也建不出来。",`
- `HeliumTodo.Domain\Backup\BackupPackageWriter.cs:188` — `_ => $"写不进「{directory}」：{ex.Message}",`

### `HeliumTodo.Domain\Models\FocusSession.cs`（2 处）

- `HeliumTodo.Domain\Models\FocusSession.cs:95` — `error = "专注时长必须大于 0。";`
- `HeliumTodo.Domain\Models\FocusSession.cs:101` — `error = "单次专注不能超过 8 小时（请拆成几次）。";`

### `HeliumTodo.Domain\Models\ReminderPresets.cs`（5 处）

- `HeliumTodo.Domain\Models\ReminderPresets.cs:50` — `if (minutes is not { } value) return "不提醒";`
- `HeliumTodo.Domain\Models\ReminderPresets.cs:51` — `if (value <= 0) return "开始时";`
- `HeliumTodo.Domain\Models\ReminderPresets.cs:56` — `if (hours == 0) return $"提前 {rest} 分钟";`
- `HeliumTodo.Domain\Models\ReminderPresets.cs:57` — `if (rest == 0) return $"提前 {hours} 小时";`
- `HeliumTodo.Domain\Models\ReminderPresets.cs:58` — `return $"提前 {hours} 小时 {rest} 分钟";`

### `HeliumTodo.Domain\Models\ScheduleItem.cs`（3 处）

- `HeliumTodo.Domain\Models\ScheduleItem.cs:79` — `error = "日程标题不能为空。";`
- `HeliumTodo.Domain\Models\ScheduleItem.cs:85` — `error = "日程时长必须大于 0。";`
- `HeliumTodo.Domain\Models\ScheduleItem.cs:91` — `error = "单条日程不能超过 24 小时（跨天请拆成两条）。";`

### `HeliumTodo.Domain\Models\StudyRoom.cs`（3 处）

- `HeliumTodo.Domain\Models\StudyRoom.cs:31` — `public string HostName { get; set; } = "我";`
- `HeliumTodo.Domain\Models\StudyRoom.cs:81` — `public string DisplayName { get; set; } = "我";`
- `HeliumTodo.Domain\Models\StudyRoom.cs:112` — `public string SenderName { get; set; } = "我";`

### `HeliumTodo.Domain\Models\TodoItem.cs`（2 处）

- `HeliumTodo.Domain\Models\TodoItem.cs:103` — `error = "待办标题不能为空。";`
- `HeliumTodo.Domain\Models\TodoItem.cs:109` — `error = "预计时长必须大于 0。";`

### `HeliumTodo.Domain\Services\Conflict.cs`（3 处）

- `HeliumTodo.Domain\Services\Conflict.cs:68` — `ConflictSeverity.Error => "严重",`
- `HeliumTodo.Domain\Services\Conflict.cs:69` — `ConflictSeverity.Warning => "警告",`
- `HeliumTodo.Domain\Services\Conflict.cs:70` — `_ => "提示",`

### `HeliumTodo.Domain\Services\DayTimeline.cs`（4 处）

- `HeliumTodo.Domain\Services\DayTimeline.cs:143` — `? $"{(int)BusyDuration.TotalHours} 小时 {BusyDuration.Minutes} 分"`
- `HeliumTodo.Domain\Services\DayTimeline.cs:144` — `: $"{BusyDuration.Minutes} 分钟";`
- `HeliumTodo.Domain\Services\DayTimeline.cs:146` — `var conflictText = Conflicts.Count == 0 ? "无冲突" : $"{Conflicts.Count} 处冲突";`
- `HeliumTodo.Domain\Services\DayTimeline.cs:147` — `return $"{Entries.Count} 项安排 · 共 {busy} · {conflictText}";`

### `HeliumTodo.Domain\Services\DayTimelineBuilder.cs`（10 处）

- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:183` — `Title = string.IsNullOrWhiteSpace(f.Title) ? "专注时段" : f.Title,`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:184` — `Notes = f.IsStudyRoomSession ? $"自习室 · {f.StudyRoomCode}" : null,`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:374` — `Message = $"今天安排了 {(int)busy.TotalHours} 小时 {(busy.Minutes)} 分，几乎没有休息时间，建议留出空档。",`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:393` — `TimelineEntryKind.FocusReservation or TimelineEntryKind.RunningFocus => "的专注时段与",`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:394` — `TimelineEntryKind.PlannedTodo => "的计划时间与",`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:395` — `_ => " 与",`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:399` — `? $"「{a.Title}」与「{b.Title}」时间重叠 {minutes} 分钟"`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:400` — `: $"「{a.Title}」{relation}「{b.Title}」重叠 {minutes} 分钟";`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:441` — `Message = $"「{todo.Title}」截止时间 {FormatTime(due)} 落在「{blocking.Title}」期间" +`
- `HeliumTodo.Domain\Services\DayTimelineBuilder.cs:442` — `$"（{blocking.TimeRangeText}），可能来不及完成。",`

### `HeliumTodo.Domain\Services\RecurrenceExpander.cs`（10 处）

- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:88` — `if (days.Count == 0) return "单次";`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:89` — `if (days.Count == 7) return "每天";`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:93` — `DayOfWeek.Monday => "一",`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:94` — `DayOfWeek.Tuesday => "二",`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:95` — `DayOfWeek.Wednesday => "三",`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:96` — `DayOfWeek.Thursday => "四",`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:97` — `DayOfWeek.Friday => "五",`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:98` — `DayOfWeek.Saturday => "六",`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:99` — `_ => "日",`
- `HeliumTodo.Domain\Services\RecurrenceExpander.cs:104` — `return "每周" + string.Join("、", ordered);`

### `HeliumTodo.Domain\Services\ReminderPlanner.cs`（4 处）

- `HeliumTodo.Domain\Services\ReminderPlanner.cs:121` — `var prefix = lead <= 0 ? "现在开始" : $"{ReminderPresets.Describe(lead)}开始";`
- `HeliumTodo.Domain\Services\ReminderPlanner.cs:129` — `var label = isDue ? "截止" : "计划开始";`
- `HeliumTodo.Domain\Services\ReminderPlanner.cs:130` — `var leadText = lead <= 0 ? "就在现在" : ReminderPresets.Describe(lead);`
- `HeliumTodo.Domain\Services\ReminderPlanner.cs:131` — `return $"{label}时间 {anchor:HH:mm} · {leadText}";`

### `HeliumTodo.Domain\Theming\BuiltInThemes.cs`（9 处）

- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:25` — `displayName: "Fluent 浅色 · 蓝",`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:34` — `new SimpleTheme("fluent-light-teal",   "Fluent 浅色 · 青", ThemeKind.Light, FluentTeal),`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:35` — `new SimpleTheme("fluent-light-purple", "Fluent 浅色 · 紫", ThemeKind.Light, FluentPurple),`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:36` — `new SimpleTheme("fluent-light-green",  "Fluent 浅色 · 绿", ThemeKind.Light, FluentGreen),`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:38` — `new SimpleTheme("fluent-dark-blue",    "Fluent 深色 · 蓝", ThemeKind.Dark,  FluentBlue),`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:39` — `new SimpleTheme("fluent-dark-teal",    "Fluent 深色 · 青", ThemeKind.Dark,  FluentTeal),`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:40` — `new SimpleTheme("fluent-dark-purple",  "Fluent 深色 · 紫", ThemeKind.Dark,  FluentPurple),`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:41` — `new SimpleTheme("fluent-dark-orange",  "Fluent 深色 · 橙", ThemeKind.Dark,  FluentOrange),`
- `HeliumTodo.Domain\Theming\BuiltInThemes.cs:44` — `new SimpleTheme("high-contrast", "高对比度（预览）", ThemeKind.Dark, "#FFD700", isExperimental: true),`

### `HeliumTodo.Infrastructure\BackupExportResult.cs`（1 处）

- `HeliumTodo.Infrastructure\BackupExportResult.cs:45` — `return $"已导出 {Path}（{sizeText}，{Manifest.Counts.DescribeText}）";`

### `HeliumTodo.Infrastructure\BackupImportResult.cs`（4 处）

- `HeliumTodo.Infrastructure\BackupImportResult.cs:37` — `var text = $"{BackupImportModeInfo.DisplayName(Mode)}：已导入 {Counts.DescribeText}";`
- `HeliumTodo.Infrastructure\BackupImportResult.cs:41` — `text += $"；导入前的数据已备份到 {SafetyBackupPath}";`
- `HeliumTodo.Infrastructure\BackupImportResult.cs:46` — `text += "；配置已同步";`
- `HeliumTodo.Infrastructure\BackupImportResult.cs:51` — `text += "；壁纸已恢复";`

### `HeliumTodo.Infrastructure\DataBackupService.cs`（11 处）

- `HeliumTodo.Infrastructure\DataBackupService.cs:102` — `$"[DataBackupService] 目标位置不可写，改存到 {fallbackPath}；原因：{denied.Message}");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:195` — `return BackupInspection.Failed(zipPath, checksumError ?? "备份文件校验失败。");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:222` — `return BackupInspection.Failed(zipPath, $"读取备份时出错：{ex.Message}");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:249` — `throw new BackupPackageException(checksumError ?? "备份文件校验失败。");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:277` — `throw new BackupPackageException($"导入失败：{ex.Message}");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:406` — `return new WallpaperPayload(null, info.Name, $"壁纸超过 {limit} MB，没有打进备份");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:413` — `return new WallpaperPayload(null, Path.GetFileName(path), $"壁纸读取失败：{ex.Message}");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:556` — `$"备份里的数据库打不开（可能不是有效的 SQLite 文件）：{ex.Message}");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:585` — `throw new BackupPackageException("备份里的数据库结构不认识（找不到日程/待办这些表）。");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:881` — `warnings.Add("这份备份没带配置，导入后界面设置保持本机的");`
- `HeliumTodo.Infrastructure\DataBackupService.cs:892` — `warnings.Add("这份备份里没有日程/待办/专注记录，导入后本地数据会被清空");`

### `HeliumTodo.Infrastructure\LanPeerTransport.cs`（10 处）

- `HeliumTodo.Infrastructure\LanPeerTransport.cs:119` — `if (addresses.Count == 0) return "（没找到可用网卡）";`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:368` — `LastError = $"地址看不懂：{host}";`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:428` — `LastError = $"连接 {endpoint} 超时";`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:432` — `LastError = $"连不上 {endpoint}（{ex.SocketErrorCode}）";`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:436` — `LastError = $"连接 {endpoint} 失败：{ex.Message}";`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:478` — `LastError = $"端口 {_port} 被占用（{ex.SocketErrorCode}），联机功能不可用";`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:608` — `Text = "房间码不对",`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:712` — `string.IsNullOrWhiteSpace(message.DisplayName) ? "同学" : message.DisplayName,`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:860` — `LastError = $"广播端口 {_port} 不可用（{ex.SocketErrorCode}），只能手动填 IP";`
- `HeliumTodo.Infrastructure\LanPeerTransport.cs:934` — `$"{_displayName} 的自习室",`

### `HeliumTodo.Infrastructure\LocalOnlyPeerTransport.cs`（1 处）

- `HeliumTodo.Infrastructure\LocalOnlyPeerTransport.cs:67` — `public string LocalAddressText => "（本地模式）";`
