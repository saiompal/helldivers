# Rough reachability check for a level layout, using the same numbers as
# PlayerMovement and Level0Builder (keep them in sync if you retune).
# It simulates running, jumping and the 180-degree map flip, then reports
# whether the exit and stars can be reached, whether the exit needs a flip,
# and whether any reachable spot can no longer reach the exit (a soft-lock).
# The exit only opens once every star is collected, as in the game.
#
#   python3 Tools/check_level.py Assets/Editor/Level0Builder.cs
#
# Layout chars: # solid, . open, S start, E exit, * star.
import sys, math, collections
G = 9.81 * 3.0      # gravityScale 3
JUMP = 13.2
SPEED = 7.5
MAXFALL = 22.0
HALF = 0.275        # half the player box (0.55 world units)
DT = 0.02
STEPS = 3           # physics steps per decision

def load(path):
    text = open(path).read()
    if path.endswith('.cs'):
        import re
        rows = re.findall(r'^\s*"([#.SE*]+)",$', text, re.M)
    else:
        rows = [l for l in text.splitlines() if l.strip()]
    assert all(len(r) == len(rows[0]) for r in rows), "ragged"
    return rows

class Grid:
    def __init__(s, rows):
        s.rows = rows; s.H = len(rows); s.W = len(rows[0])
    def solid(s, c, r):
        if c < 0 or r < 0 or c >= s.W or r >= s.H: return True
        return s.rows[r][c] == '#'
    # world y up; tile (c,r) spans x[c,c+1] y[H-1-r, H-r]
    def solid_xy(s, cx, cy):
        return s.solid(cx, s.H - 1 - cy)
    def box_hits(s, x, y):
        x0 = math.floor(x - HALF + 1e-6); x1 = math.floor(x + HALF - 1e-6)
        y0 = math.floor(y - HALF + 1e-6); y1 = math.floor(y + HALF - 1e-6)
        for cx in range(x0, x1 + 1):
            for cy in range(y0, y1 + 1):
                if s.solid_xy(cx, cy): return True
        return False
    def markers(s, ch):
        return [(c + 0.5, s.H - 1 - r + 0.5) for r in range(s.H) for c in range(s.W) if s.rows[r][c] == ch]

def rotated(rows):
    return [r[::-1] for r in rows[::-1]]

def step(g, x, y, vx, vy):
    vy = max(vy - G * DT, -MAXFALL)
    nx = x + vx * DT
    if g.box_hits(nx, y):
        nx = (math.floor(nx + HALF) - HALF - 1e-4) if vx > 0 else (math.floor(nx - HALF) + 1 + HALF + 1e-4)
        if g.box_hits(nx, y): nx = x
    x = nx
    ny = y + vy * DT
    if g.box_hits(x, ny):
        if vy > 0: ny = math.floor(ny + HALF) - HALF - 1e-4
        else: ny = math.floor(ny - HALF) + 1 + HALF + 1e-4
        if g.box_hits(x, ny): ny = y
        vy = 0.0
    return x, ny, vy

def grounded(g, x, y):
    return g.box_hits(x, y - 0.02)

def key(o, x, y, vy, got):
    return (o, round(x * 8), round(y * 8), round(vy / 2), got)

def touches(x, y, pts, r=0.5):
    return any(abs(x - px) < HALF + r and abs(y - py) < HALF + r for px, py in pts)

def run(rows, flips=True):
    grids = [Grid(rows), Grid(rotated(rows))]
    W, H = grids[0].W, grids[0].H
    sx, sy = [sum(v) / len(v) for v in zip(*grids[0].markers('S'))]
    sy = math.floor(sy) + HALF + 1e-3
    exits = [grids[0].markers('E'), grids[1].markers('E')]
    # Stars by map position; the flipped view sees them rotated.
    stars = grids[0].markers('*')
    star_pos = [stars, [(W - x, H - y) for x, y in stars]]
    all_stars = (1 << len(stars)) - 1
    start = (0, sx, sy, 0.0, 0)
    seen = {key(*start): start}
    edges = collections.defaultdict(set)
    q = collections.deque([start])
    win = set(); star_hits = collections.defaultdict(set)
    visit = [set(), set()]
    while q:
        o, x, y, vy, got = q.popleft()
        k0 = key(o, x, y, vy, got)
        g = grids[o]
        for i, (px, py) in enumerate(star_pos[o]):
            if not got >> i & 1 and touches(x, y, [(px, py)], 0.35):
                star_hits[(o, i)].add(k0)
                got |= 1 << i
        if got == all_stars and touches(x, y, exits[o]):
            win.add(k0); continue          # the run ends at the exit
        gnd = grounded(g, x, y) and vy <= 0.05
        acts = []
        for d in (-1, 0, 1):
            acts.append((d, False, False))
            if gnd: acts.append((d, True, False))
            if vy > 0: acts.append((d, False, True))
        nexts = []
        for d, jump, cut in acts:
            nx, ny, nvy = x, y, vy
            if jump: nvy = JUMP
            if cut: nvy *= 0.5
            for _ in range(STEPS):
                nx, ny, nvy = step(g, nx, ny, d * SPEED, nvy)
                visit[o].add((math.floor(nx), H - 1 - math.floor(ny)))
            nexts.append((o, nx, ny, nvy, got))
        if flips and gnd:
            nexts.append((1 - o, W - x, H - y, 0.0, got))
        for n in nexts:
            k = key(*n)
            edges[k0].add(k)
            if k not in seen:
                seen[k] = n; q.append(n)
    return grids, seen, edges, win, star_hits, visit

def report(path):
    rows = load(path)
    grids, seen, edges, win, star_hits, visit = run(rows)
    print(f"states {len(seen)}  exit reachable: {bool(win)}")
    _, _, _, win0, _, _ = run(rows, flips=False)
    print(f"exit reachable WITHOUT flipping: {bool(win0)}")
    for i, (x, y) in enumerate(grids[0].markers('*')):
        ways = [o for o in (0, 1) if star_hits.get((o, i))]
        where = f"col {math.floor(x)}, row {grids[0].H - 1 - math.floor(y)}"
        print(f"star at {where}: collected {'as built' if 0 in ways else ''}"
              f"{' and ' if len(ways) == 2 else ''}{'flipped' if 1 in ways else ''}"
              f"{'NEVER' if not ways else ''}")
    # soft-lock check: every reached state must still be able to reach the exit
    rev = collections.defaultdict(set)
    for a, bs in edges.items():
        for b in bs: rev[b].add(a)
    good = set(win); q = collections.deque(win)
    while q:
        k = q.popleft()
        for a in rev[k]:
            if a not in good: good.add(a); q.append(a)
    stuck = [k for k in seen if k not in good]
    print(f"soft-lock states: {len(stuck)}")
    if stuck:
        tiles = collections.Counter()
        for o, xq, yq, _, _ in stuck:
            x, y = xq / 8, yq / 8
            if o == 1: x, y = grids[0].W - x, grids[0].H - y
            tiles[(math.floor(x), grids[0].H - 1 - math.floor(y))] += 1
        print("  stuck near (col,row in map):", tiles.most_common(8))
    for o in (0, 1):
        print(f"--- reachable area, orientation {o} ({'as built' if o == 0 else 'flipped'})")
        g = grids[o]
        for r in range(g.H):
            print(''.join(('o' if (c, r) in visit[o] and g.rows[r][c] == '.' else g.rows[r][c]) for c in range(g.W)))

if __name__ == '__main__':
    report(sys.argv[1])
