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

def components(alpha, min_area):
    """alpha>0 的所有连通域, 按面积降序"""
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
                if len(comp) >= min_area:
                    comps.append(comp)
    comps.sort(key=len, reverse=True)
    return comps

def extract_row(y0, y1, names, min_area=2500):
    """在 y0-y1 行内自动提取各视图, 按 x 从左到右命名"""
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
    comps = components(alpha, min_area)
    # 按 bbox 的 x 中心排序
    infos = []
    for comp in comps:
        xs = [p[1] for p in comp]; ys = [p[0] for p in comp]
        infos.append((min(xs), max(xs), min(ys), max(ys), comp))
    infos.sort(key=lambda t: t[0])
    results = []
    for (x0, x1, yy0, yy1, comp), name in zip(infos, names):
        mask = np.zeros((h, w), dtype=np.uint8)
        for y, x in comp:
            mask[y, x] = 255
        a_img = Image.fromarray(mask, 'L').filter(ImageFilter.GaussianBlur(0.8))
        rgba = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a_img)).crop((x0, yy0, x1+1, yy1+1))
        rgba.save(os.path.join(OUT, name + '.png'))
        results.append((name, rgba.size, len(comp)))
        print(f"{name}: {rgba.size[0]}x{rgba.size[1]}, {len(comp)}px")
    return results

print("=== 五视图行 (y 60-530) ===")
extract_row(60, 530, ['front', 'threeq', 'side', 'back', 'back34'])
print()
print("=== 表情行 (y 560-870) ===")
extract_row(560, 870, ['face_dull', 'face_happy', 'face_curious', 'sleep'], min_area=2000)
print('done')
