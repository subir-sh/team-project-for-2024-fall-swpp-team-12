"""ASCII map of a dumped level (captures/levels/<name>.json, from LevelDump.Run) for planning solutions.

    python tools/levelmap.py L-001-1

Cells are tiles (pitch 2). Each cell prints the floor top height, plus a marker:
  S start  G goal  B button  L lever  X box  M moving block (hidden spot)  m its visible spot
  * tile toggled by a device (lowercase id = initially off)   | wall/solid between or on cells
"""
import json, os, sys, math
from collections import defaultdict

ROOT = os.path.join(os.path.dirname(__file__), '..')


def load(name):
    return json.load(open(os.path.join(ROOT, 'captures', 'levels', name + '.json'), encoding='utf8'))['items']


def cell(x, z):
    return (int(math.floor(x / 2)) * 2 + 1, int(math.floor(z / 2)) * 2 + 1)


def build(name):
    items = load(name)
    floors = {}            # cell -> (top, name, path, activeChain)
    solids = []
    dev_targets = {}       # path -> (device label, init)
    devices = []
    for it in items:
        if it['kind'] in ('button', 'lever'):
            devices.append(it)
    for i, d in enumerate([d for d in devices if d['kind'] == 'button']):
        d['label'] = f'B{i}'
    for i, d in enumerate([d for d in devices if d['kind'] == 'lever']):
        d['label'] = f'L{i}'
    for d in devices:
        for t in d['targets']:
            dev_targets.setdefault(t['path'], []).append((d['label'], t['init']))
    for it in items:
        if it['kind'] in ('solid', 'start', 'goal') and '/Tilemap/' in it['path'] or ('/Grid/' in it['path'] and it['kind'] in ('solid', 'start', 'goal')) \
                or it['path'].startswith('Tilemap/'):
            mn, mx = it['min'], it['max']
            if mx[0] - mn[0] > 2.5 or mx[2] - mn[2] > 2.5: solids.append(it); continue
            if it['name'].startswith('gear'): continue
            c = cell((mn[0] + mx[0]) / 2, (mn[2] + mx[2]) / 2)
            prev = floors.get(c)
            if prev is None or mx[1] > prev['top']:
                floors[c] = dict(top=round(mx[1], 1), it=it)
        elif it['kind'] == 'solid':
            solids.append(it)
    return items, floors, solids, devices, dev_targets


def render(name):
    items, floors, solids, devices, dev_targets = build(name)
    marks = defaultdict(str)
    for it in items:
        c = cell(it['pos'][0], it['pos'][2])
        k = it['kind']
        if k == 'start' and it['self'] and not it['name'].startswith('gear'): marks[c] += 'S'
        if k == 'goal' and it['self'] and not it['name'].startswith('gear'): marks[c] += 'G'
        if k == 'box': marks[c] += 'X'
        if k == 'moving':
            marks[c] += 'M'
            v = it['visible']; marks[cell(v[0], v[2])] += 'm'
        if k == 'laser': marks[c] += '>'
    for d in devices:
        c = cell(d['pos'][0], d['pos'][2]); marks[c] += d['label']
    tgt_cells = defaultdict(list)
    for d in devices:
        for t in d['targets']:
            if 'pos' in t: tgt_cells[cell(t['pos'][0], t['pos'][2])].append((d['label'], t['init'], t['path'].split('/')[-1]))
    for c, f in floors.items():
        tg = [x for x in tgt_cells.get(c, []) if x[2] == f['it']['name']]
        if tg: marks[c] += '*' + ''.join(l if init else l.lower() for l, init, _ in tg)
        elif not f['it']['active']: marks[c] += '!'
    xs = [c[0] for c in list(floors) + list(marks)]; zs = [c[1] for c in list(floors) + list(marks)]
    out = [f'### {name}   (x left->right, z bottom->top; value = floor top y)']
    W = 9
    for z in range(max(zs), min(zs) - 1, -2):
        row = f'z{z:>4} '
        for x in range(min(xs), max(xs) + 1, 2):
            f = floors.get((x, z)); m = marks.get((x, z), '')
            s = (f'{f["top"]:g}' if f else '.') + m
            row += s[:W].ljust(W)
        out.append(row)
    out.append('      ' + ''.join(f'x{x}'.ljust(W) for x in range(min(xs), max(xs) + 1, 2)))
    for d in devices:
        extra = f' reset={d["reset"]}' if 'reset' in d else f' rotY={d["rotY"]}'
        out.append(f'{d["label"]} {d["kind"]} at {d["pos"]}{extra} -> ' + ', '.join(f'{t["path"].split("/")[-1]}{"(on)" if t["init"] else "(off)"}' for t in d['targets']))
    for it in items:
        if it['kind'] == 'moving': out.append(f'M {it["name"]} hidden {it["pos"]} visible {it["visible"]} cycle {it["cycle"]} active={it["active"]}')
        if it['kind'] == 'laser': out.append(f'laser {it["path"]} at {it["pos"]} dir {it["dir"]} active={it["active"]}')
        if it['kind'] == 'box': out.append(f'box {it["name"]} at {it["pos"]} size y {round(it["max"][1]-it["min"][1],1)}')
    for s in solids:
        out.append(f'solid {s["path"]} min {s["min"]} max {s["max"]} active={s["active"]}')
    return '\n'.join(out)


if __name__ == '__main__':
    for n in sys.argv[1:]: print(render(n)); print()
