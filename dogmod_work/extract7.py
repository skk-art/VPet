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

def mask_from(alpha, comp):
    m = np.zeros(alpha.shape, dtype=np.uint8)
    for y, x in comp:
        m[y, x] = 255
    return m

def finish(rgba, name):
    bbox = rgba.getbbox()
    if bbox: rgba = rgba.crop(bbox)
    rgba.save(os.path.join(OUT, name + '.png'))
    print(f"  {name}: {rgba.size[0]}x{rgba.size[1]}")

# ============ 段定义: (name, x0, x1, y0, y1, 谷值窗口或None) ============
SEGMENTS = [
    ('front',  10, 305,  85, 530, None),
    ('threeq', 315, 620,  90, 530, None),
    ('side',   560, 760,  90, 530, (690, 790)),
    ('back',   760, 970,  90, 530, None),
    ('back34', 1200, 1530, 90, 540, None),
    ('face_dull',    10, 245, 600, 866, None),
    ('face_happy',  242, 480, 600, 866, (430, 500)),
    ('face_curious',480, 648, 600, 866, None),
    ('sleep',       630, 878, 600, 866, None),
]

for name, x0, x1, y0, y1, win in SEGMENTS:
    region = arr[y0:y1, x0:x1]
    alpha = flood_alpha(region)
    comps = all_components(alpha, min_area=1200)
    if not comps:
        print(f"  ⚠ {name}: 无内容"); continue
    if win:
        # 合并域: 在谷值窗口内按列投影最低谷竖切成两部分
        w0, w1 = win[0]-x0, win[1]-x0
        colsum = (alpha[:, w0:w1] > 0).sum(axis=0)
        cut_rel = w0 + int(np.argmin(colsum))
        parts = []
        for lo, hi in ((0, cut_rel), (cut_rel, alpha.shape[1])):
            m = np.zeros_like(alpha); m[:, lo:hi] = alpha[:, lo:hi]
            comps2 = [c for c in all_components(m, min_area=1200)]
            for c in comps2:
                mm = mask_from(alpha, c)
                a2 = Image.fromarray(mm, 'L').filter(ImageFilter.GaussianBlur(0.8))
                r2 = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a2))
                parts.append(r2)
        for i, r2 in enumerate(parts):
            finish(r2, f"{name}_{i+1}")
    else:
        mask = np.zeros_like(alpha)
        for c in comps:
            for y, x in c:
                mask[y, x] = 255
        a_img = Image.fromarray(mask, 'L').filter(ImageFilter.GaussianBlur(0.8))
        rgba = Image.merge('RGBA', (*Image.fromarray(region.astype(np.uint8)).split(), a_img))
        finish(rgba, name)
print('done')
