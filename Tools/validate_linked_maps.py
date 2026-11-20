"""Validate source-map preservation and ball-clear connectivity, without Unity.

This catches embedded/isolated stars and portal endpoints. Free-space connectivity
does not prove a timed jump route; Unity smoke checks cover triggers and flips.
"""
from pathlib import Path
from collections import deque
import math
import re

ROOT = Path(__file__).resolve().parents[1]


class Scene:
    def __init__(self, name):
        text = (ROOT / 'Assets/Scenes' / (name + '.unity')).read_text()
        self.records = {int(m[2]): (int(m[1]), m[3]) for m in re.finditer(
            r'^--- !u!(\d+) &(\d+)\n([\s\S]*?)(?=^---|\Z)', text, re.M)}
        self.names = {i: re.search(r'  m_Name: (.*)', b)[1]
                      for i, (kind, b) in self.records.items() if kind == 1}
        self.transforms = {i: b for i, (kind, b) in self.records.items() if kind == 4}
        self.by_go = {int(re.search(r'm_GameObject: \{fileID: (\d+)', b)[1]): i
                      for i, b in self.transforms.items()}

    def vector(self, body, key):
        return tuple(map(float, re.search(key + r': \{x: ([^,]+), y: ([^,}]+)', body).groups()))

    def parent(self, transform):
        return int(re.search(r'm_Father: \{fileID: (\d+)', self.transforms[transform])[1])

    def point(self, transform, point=(0, 0)):
        b = self.transforms[transform]
        x, y = self.vector(b, 'm_LocalPosition')
        sx, sy = self.vector(b, 'm_LocalScale')
        rotation = re.search(r'm_LocalRotation:.*z: ([^,]+), w: ([^}]+)', b)
        z, w = map(float, rotation.groups())
        angle = 2 * math.atan2(z, w)
        c, s = math.cos(angle), math.sin(angle)
        px, py = point
        result = (x + c * sx * px - s * sy * py, y + s * sx * px + c * sy * py)
        parent = self.parent(transform)
        return self.point(parent, result) if parent else result

    def descendant(self, transform, root):
        return transform == root or bool(self.parent(transform) and self.descendant(self.parent(transform), root))

    def root(self, name):
        return self.by_go[next(go for go, label in self.names.items() if label == name)]

    def positions(self, class_name, root):
        points = []
        for kind, b in self.records.values():
            if kind != 114 or 'Assembly-CSharp::' + class_name + '\n' not in b:
                continue
            go = int(re.search(r'm_GameObject: \{fileID: (\d+)', b)[1])
            tr = self.by_go[go]
            if self.descendant(tr, root):
                points.append(self.point(tr))
        return points

    def walls(self, root):
        boxes = []
        for kind, b in self.records.values():
            if kind != 61 or '  m_IsTrigger: 1' in b:
                continue
            go = int(re.search(r'm_GameObject: \{fileID: (\d+)', b)[1])
            tr = self.by_go[go]
            if not self.descendant(tr, root):
                continue
            ox, oy = self.vector(b, 'm_Offset')
            sx, sy = self.vector(b, 'm_Size')
            corners = [self.point(tr, (ox + dx * sx / 2, oy + dy * sy / 2))
                       for dx, dy in [(-1, -1), (-1, 1), (1, 1), (1, -1)]]
            boxes.append((min(p[0] for p in corners), min(p[1] for p in corners),
                          max(p[0] for p in corners), max(p[1] for p in corners)))
        return boxes


def check_region(scene, root, source, offset, start):
    boxes = scene.walls(root)
    original = source.walls(source.root('Map'))
    normalized = [(a, b - offset, c, d - offset) for a, b, c, d in boxes]
    round_boxes = lambda items: sorted(tuple(round(v, 3) for v in box) for box in items)
    assert round_boxes(normalized) == round_boxes(original), 'Source wall geometry changed'
    targets = scene.positions('StarCollectible', root) + scene.positions('PortalController', root)
    if offset == 0:
        targets += scene.positions('ExitTrigger', root)
    radius = .275

    def clear(x, y):
        return not any(a - radius + 1e-5 < x < c + radius - 1e-5 and
                       b - radius + 1e-5 < y < d + radius - 1e-5 for a, b, c, d in boxes)

    step = .1
    free = {(i, j) for i in range(341) for j in range(191)
            if clear(-17 + step * i, offset - 9.5 + step * j)}

    def nearest(point):
        x, y = point
        assert clear(x, y), ('Landmark embedded in a wall', point)
        node = (round((x + 17) / step), round((y - offset + 9.5) / step))
        return min((n for n in free if abs(n[0] - node[0]) <= 2 and abs(n[1] - node[1]) <= 2),
                   key=lambda n: (n[0] - node[0]) ** 2 + (n[1] - node[1]) ** 2)

    seed = nearest(start)
    seen = {seed}
    queue = deque([seed])
    while queue:
        x, y = queue.popleft()
        for node in [(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)]:
            if node in free and node not in seen:
                seen.add(node)
                queue.append(node)
    for target in targets:
        assert nearest(target) in seen, ('Landmark is in a sealed region', target)
    print('PASS:', 'upper' if offset else 'lower',
          len(boxes), 'preserved source walls;', len(targets), 'clear connected landmarks')


if __name__ == '__main__':
    linked = Scene('LinkedMaps')
    check_region(linked, linked.root('Lower Map'), Scene('Level1'), 0, (-15, -8))
    check_region(linked, linked.root('Upper Map'), Scene('Level3'), 24, (-14, 16.3))
    assert len(linked.positions('StarCollectible', linked.root('Lower Map'))) == 2
    assert len(linked.positions('StarCollectible', linked.root('Upper Map'))) == 3
    assert not (ROOT / 'Assets/Scenes/ShadowTutorial.unity').exists()
