from PIL import Image
import numpy as np
from collections import deque

img = Image.open(r'D:\VPet-main\dogmod_work\design.png').convert('RGB')
arr = np.array(img).astype(int)
bg = np.array([249, 242, 232])
TOL = 15
h, w = arr.shape[:2]
dist = np.abs(arr - bg).sum(axis=2)
isbg = dist <= TOL
visited = np.zeros((h, w), dtype=bool)
dq = deque()
for x in range(w):
    for y in (0, h-1):
        if isbg[y, x] and not visited[y, x]:
            visited[y, x] = True; dq.append((y, x))
for y in range(h):
    for x in (0, w-1):
        if isbg[y, x] and not visited[y, x]:
            visited[y, x] = True; dq.append((y, x))
while dq:
    y, x = dq.popleft()
    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
        ny, nx = y+dy, x+dx
        if 0 <= ny < h and 0 <= nx < w and isbg[ny, nx] and not visited[ny, nx]:
            visited[ny, nx] = True; dq.append((ny, nx))
alpha = np.where(visited, 0, 255).astype(np.uint8)

solid = alpha > 0
visited2 = np.zeros((h, w), dtype=bool)
comps = []
for sy in range(h):
    for sx in range(w):
        if solid[sy, sx] and not visited2[sy, sx]:
            comp = []
            dq = deque([(sy, sx)])
            visited2[sy, sx] = True
            while dq:
                y, x = dq.popleft()
                comp.append((y, x))
                for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
                    ny, nx = y+dy, x+dx
                    if 0 <= ny < h and 0 <= nx < w and solid[ny, nx] and not visited2[ny, nx]:
                        visited2[ny, nx] = True
                        dq.append((ny, nx))
            if len(comp) >= 1200:
                xs = [p[1] for p in comp]; ys = [p[0] for p in comp]
                comps.append((min(xs), min(ys), max(xs), max(ys), len(comp)))
comps.sort(key=lambda c: (c[1]//250, c[0]))
for c in comps:
    print(f"x{c[0]}-{c[2]}  y{c[1]}-{c[3]}  ({c[4]}px)")
