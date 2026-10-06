from PIL import Image, ImageFilter
import numpy as np
from collections import deque
import os

img = Image.open(r'D:\VPet-main\dogmod_work\design.png').convert('RGB')
arr = np.array(img).astype(int)
bg = np.array([249, 242, 232])
TOL = 15
OUT = r'D:\VPet-main\dogmod_work'

def flood_alpha(region):
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
    return np.where(visited, 0, 255).astype(np.uint8)

def all_components(alpha, min_area=1200):
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
    return comps

def finish(rgba, name):
    bbox = rgba.getbbox()
    if bbox: rgba = rgba.crop(bbox)
    rgba.save(os.path.join(OUT, name + '.png'))
    print(f"  {name}: {rgba.size[0]}x{rgba.size[1]}")

# side: 合并域 (574-959), 谷值窗口收窄到两狗之间的空隙 (698-724)
x0, x1, y0, y1 = 560, 970, 90, 530
region = arr[y0:y1, x0:x1]
alpha = flood_alpha(region)
w0, w1 = 698-x0, 724-x0
colsum = (alpha[:, w0:w1] > 0).sum(axis=0)
cut_rel = w0 + int(np.argmin(colsum))
cut_abs = cut_rel + x0
print(f"side/back 竖切线: x={cut_abs}")
for i, (lo, hi) in enumerate(((0, cut_rel), (cut_rel, alpha.shape[1]))):
    m = np.zeros_like(alpha); m[:, lo:hi] = alpha[:, lo:hi]
    for c in all_components(m, min_area=1200):
        mm = np.zeros_like(alpha)
        for y, x in c: mm[y, x] = 255
        a_img = Image.fromarray(mm, 'L').filter(ImageFilter.GaussianBlur(0.8))
        rgba = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a_img))
        finish(rgba, f"{'side' if i==0 else 'back'}_v{i+1}")

# 开心+好奇: 合并域 (248-635), 谷值窗口 (455-492)
x0, x1, y0, y1 = 242, 648, 600, 866
region = arr[y0:y1, x0:x1]
alpha = flood_alpha(region)
w0, w1 = 455-x0, 492-x0
colsum = (alpha[:, w0:w1] > 0).sum(axis=0)
cut_rel = w0 + int(np.argmin(colsum))
cut_abs = cut_rel + x0
print(f"happy/curious 竖切线: x={cut_abs}")
for i, (lo, hi) in enumerate(((0, cut_rel), (cut_rel, alpha.shape[1]))):
    m = np.zeros_like(alpha); m[:, lo:hi] = alpha[:, lo:hi]
    for c in all_components(m, min_area=1200):
        mm = np.zeros_like(alpha)
        for y, x in c: mm[y, x] = 255
        a_img = Image.fromarray(mm, 'L').filter(ImageFilter.GaussianBlur(0.8))
        rgba = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a_img))
        finish(rgba, f"face_{'happy' if i==0 else 'curious'}_v{i+1}")
print('done')
