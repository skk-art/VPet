from PIL import Image, ImageFilter
import numpy as np
from collections import deque
import os

img = Image.open(r'D:\VPet-main\dogmod_work\design.png').convert('RGB')
arr = np.array(img).astype(int)
bg = np.array([249, 242, 232])
TOL = 30
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

OUT = r'D:\VPet-main\dogmod_work'

def cut2(x0, x1, y0, y1, name, min_area=2500):
    """按手工x边界裁一段, flood抠背景, 过滤小装饰, 裁bbox"""
    region = arr[y0:y1, x0:x1]
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
    comps = [c for c in all_components(alpha) if len(c) >= min_area]
    mask = np.zeros((h, w), dtype=np.uint8)
    for c in comps:
        for y, x in c:
            mask[y, x] = 255
    a_img = Image.fromarray(mask, 'L').filter(ImageFilter.GaussianBlur(0.8))
    rgba = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a_img))
    bbox = rgba.getbbox()
    if bbox: rgba = rgba.crop(bbox)
    rgba.save(os.path.join(OUT, name + '.png'))
    print(f"{name}: {rgba.size[0]}x{rgba.size[1]}")

print("=== 五视图 ===")
cut2(0, 228, 60, 530, 'front')
cut2(228, 442, 60, 530, 'threeq')
cut2(442, 712, 60, 530, 'side')
cut2(712, 888, 60, 530, 'back')
cut2(888, 1160, 60, 530, 'back34')
print("=== 四表情 ===")
cut2(0, 240, 560, 880, 'face_dull', 2000)
cut2(240, 468, 560, 880, 'face_happy', 2000)
cut2(468, 668, 560, 880, 'face_curious', 2000)
cut2(668, 812, 560, 880, 'sleep', 2000)
print('done')

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
