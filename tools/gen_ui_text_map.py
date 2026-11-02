"""扫描项目里「会显示到图形界面上的文字」，按界面区域归类输出清单。"""

import io
import os
import re

ROOT = r'D:\代码\dsh project\Helium todo demo'
OUT = os.path.join(ROOT, 'UI_TEXT_MAP.md')

CJK = re.compile(r'[\u4e00-\u9fff]')
TEXT_ATTRS = (
    'Text=', 'Content=', 'Header=', 'Description=', 'PlaceholderText=',
    'ToolTip.Tip=', 'OnContent=', 'OffContent=', 'Title=',
)

# 不参与「界面文字」统计的（控制台输出 / 测试 / 示例数据）
SKIP_DIRS = {'bin', 'obj', '.git', 'output', 'Data', 'testdata', 'docs'}
SKIP_FILES = {'SeedData.cs'}          # 示例数据，见文档最后一节单独说明
SKIP_PROJECTS = ('HeliumTodo.Tests', 'HeliumTodo.SelfTest', 'HeliumTodo.RenderCheck')


def strip_line_comments(code):
    out, in_str, i = [], False, 0
    while i < len(code):
        c = code[i]
        if c == '"' and (i == 0 or code[i - 1] != '\\'):
            in_str = not in_str
            out.append(c)
            i += 1
            continue
        if not in_str and code.startswith('//', i):
            break
        out.append(c)
        i += 1
    return ''.join(out)


def scan_axaml(path):
    hits = []
    for n, line in enumerate(io.open(path, encoding='utf-8'), 1):
        s = line.strip()
        if s.startswith('<!--'):
            continue
        if any(x in line for x in ('Debug.WriteLine', 'Console.WriteLine')):
            continue
        if any(a in line for a in TEXT_ATTRS) and CJK.search(line):
            hits.append((n, s))
    return hits


def scan_cs(path):
    hits = []
    for n, line in enumerate(io.open(path, encoding='utf-8'), 1):
        s = line.strip()
        if s.startswith('//'):
            continue
        code = strip_line_comments(line)
        if any(x in code for x in ('Debug.WriteLine', 'Console.WriteLine')):
            continue
        # 只要字符串字面量里带中文的
        for m in re.finditer(r'"([^"\\]|\\.)*"', code):
            if CJK.search(m.group(0)):
                hits.append((n, code.strip()))
                break
    return hits


found = {'view': [], 'vm': [], 'core': [], 'style': []}

for base, dirs, files in os.walk(ROOT):
    dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
    for f in files:
        if f in SKIP_FILES:
            continue
        rel = os.path.relpath(os.path.join(base, f), ROOT)
        if any(rel.startswith(p) for p in SKIP_PROJECTS):
            continue
        path = os.path.join(base, f)

        if f.endswith('.axaml'):
            hits = scan_axaml(path)
            if hits:
                bucket = 'style' if f in ('DesignTokens.axaml', 'Controls.axaml') else 'view'
                found[bucket].append((rel, hits))
        elif f.endswith('.cs'):
            top = rel.split(os.sep)[0]
            if top == 'HeliumTodo.Core' or top == 'HeliumTodo.Data':
                bucket = 'core'
            elif 'ViewModels' in rel:
                bucket = 'vm'
            else:
                bucket = 'core'   # App 层的 Service/Converter 等
            hits = scan_cs(path)
            if hits:
                found[bucket].append((rel, hits))

TITLES = {
    'view': '一、界面结构里的文字（.axaml）',
    'style': '二、文字样式与字体（.axaml 资源/样式）',
    'vm': '三、ViewModel 里拼出来的文字（运行时）',
    'core': '四、Core / Data 层拼出来的文字（运行时）',
}

buf = ['# 界面文字一览（代码位置对照）', '',
       '> 由 `tools/gen_ui_text_map.py` 扫描生成。行号是生成时的位置；',
       '> 只统计**会显示在图形界面上**的文字，控制台输出与测试断言不计入。', '']

for key in ('view', 'style', 'vm', 'core'):
    items = found[key]
    total = sum(len(h) for _, h in items)
    buf.append(f'## {TITLES[key]}')
    buf.append('')
    buf.append(f'共 {len(items)} 个文件、{total} 处。')
    buf.append('')
    for rel, hits in sorted(items):
        buf.append(f'### `{rel}`（{len(hits)} 处）')
        buf.append('')
        for n, line in hits:
            buf.append(f'- `{rel}:{n}` — `{line}`')
        buf.append('')

io.open(OUT, 'w', encoding='utf-8', newline='\n').write('\n'.join(buf))

print('写好了', OUT)
for key in ('view', 'style', 'vm', 'core'):
    print(key, len(found[key]), sum(len(h) for _, h in found[key]))
    for rel, hits in sorted(found[key]):
        print('   ', rel, len(hits))
