from PIL import Image, ImageFilter
import numpy as np
from collections import deque
import os

img = Image.open(r'D:\VPet-main\dogmod_work\design.png').convert('RGB')
arr = np.array(img).astype(int)
bg = np.array([249, 242, 232])
TOL = 30
OUT = r'D:\VPet-main\dogmod_work'

# 手工段边界仅用于"归组"(域的中心落在哪个段), 不会裁切狗身
VIEW_SEGS = [('front', 0, 236), ('threeq', 236, 448), ('side', 448, 714), ('back', 714, 892), ('back34', 892, 1300)]
FACE_SEGS = [('face_dull', 0, 242), ('face_happy', 242, 470), ('face_curious', 470, 668), ('sleep', 668, 880)]

def all_components(alpha):
    h, w = alpha.shape
    solid = alpha > 0
    visited = np.zeros((h, w), dtype=bool)
    comps = []
    for sy in range(h):
        for sx in range(w):
            if solid[sy, sx] and not visited[sy, sx]:
                comp = []
                dq = deque([(sy, sx)])
                visited[sy, sx] = True
                while dq:
                    y, x = dq.popleft()
                    comp.append((y, x))
                    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
                        ny, nx = y+dy, x+dx
                        if 0 <= ny < h and 0 <= nx < w and solid[ny, nx] and not visited[ny, nx]:
                            visited[ny, nx] = True
                            dq.append((ny, nx))
                comps.append(comp)
    return comps

def extract_row(y0, y1, segs, min_area=800):
    region = arr[y0:y1]
    h, w = region.shape[:2]
    dist = np.abs(region - bg).sum(axis=2)
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
    comps = [c for c in all_components(alpha) if len(c) >= 500]
    # 按段归组
    groups = {name: [] for name, a, b in segs}
    for comp in comps:
        xs = [p[1] for p in comp]; ys = [p[0] for p in comp]
        cx = (min(xs) + max(xs)) / 2
        for name, a, b in segs:
            if a <= cx < b:
                groups[name].append((comp, min(xs), max(xs), min(ys), max(ys)))
                break
    for name, a, b in segs:
        items = groups[name]
        if not items:
            print(f"  ⚠ {name}: 未找到内容!")
            continue
        mask = np.zeros((h, w), dtype=np.uint8)
        npx = 0
        for comp, _, _, _, _ in items:
            for y, x in comp:
                mask[y, x] = 255
            npx += len(comp)
        a_img = Image.fromarray(mask, 'L').filter(ImageFilter.GaussianBlur(0.8))
        rgba = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a_img))
        bbox = rgba.getbbox()
        if bbox: rgba = rgba.crop(bbox)
        rgba.save(os.path.join(OUT, name + '.png'))
        print(f"  {name}: {rgba.size[0]}x{rgba.size[1]}, {len(items)}个连通域 {npx}px")

print("=== 五视图 ===")
extract_row(60, 530, VIEW_SEGS)
print()
print("=== 四表情 ===")
extract_row(560, 880, FACE_SEGS, min_area=600)
print('done')
