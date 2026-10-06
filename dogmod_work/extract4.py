from PIL import Image, ImageFilter
import numpy as np
from collections import deque
import os

img = Image.open(r'D:\VPet-main\dogmod_work\design.png').convert('RGB')
W, H = img.size
arr = np.array(img).astype(int)
bg = np.array([249, 242, 232])
TOL = 30
OUT = r'D:\VPet-main\dogmod_work'

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

def extract_row(y0, y1, names, min_area):
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
    # 连通域, 过滤小装饰
    comps = [c for c in all_components(alpha) if len(c) >= min_area]
    # 列投影切分
    colmask = np.zeros(w, dtype=bool)
    for c in comps:
        for y, x in c:
            colmask[x] = True
    segs = []; s = None
    for x in range(w):
        if colmask[x] and s is None: s = x
        if not colmask[x] and s is not None: segs.append((s, x-1)); s = None
    if s is not None: segs.append((s, w-1))
    print(f"  检测到 {len(segs)} 段, 期望 {len(names)} 段")
    results = []
    for (x0, x1), name in zip(segs, names):
        mask = np.zeros((h, w), dtype=np.uint8)
        for c in comps:
            for y, x in c:
                if x0 <= x <= x1:
                    mask[y, x] = 255
        a_img = Image.fromarray(mask, 'L').filter(ImageFilter.GaussianBlur(0.8))
        rgba = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a_img))
        bbox = rgba.getbbox()
        if bbox: rgba = rgba.crop(bbox)
        rgba.save(os.path.join(OUT, name + '.png'))
        results.append((name, rgba.size))
        print(f"  {name}: {rgba.size[0]}x{rgba.size[1]}")
    return results

print("=== 五视图行 (y 60-530) ===")
extract_row(60, 530, ['front', 'threeq', 'side', 'back', 'back34'], min_area=2500)
print()
print("=== 表情行 (y 615-880, 跳过标签) ===")
extract_row(615, 880, ['face_dull', 'face_happy', 'face_curious', 'sleep'], min_area=12000)
print('done')
