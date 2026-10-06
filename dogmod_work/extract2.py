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

def largest_component_mask(alpha):
    """只保留 alpha>0 的最大连通域"""
    h, w = alpha.shape
    solid = alpha > 0
    visited = np.zeros((h, w), dtype=bool)
    best, best_n = None, 0
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
                if len(comp) > best_n:
                    best, best_n = comp, len(comp)
    mask = np.zeros((h, w), dtype=np.uint8)
    if best:
        for y, x in best:
            mask[y, x] = 255
    return mask

def cut(x0, y0, x1, y1, name):
    crop = arr[y0:y1, x0:x1]
    h, w = crop.shape[:2]
    dist = np.abs(crop - bg).sum(axis=2)
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
    # 只保留最大连通域(清除爱心/爪印/文字等装饰)
    alpha = largest_component_mask(alpha)
    a_img = Image.fromarray(alpha, 'L').filter(ImageFilter.GaussianBlur(0.8))
    rgba = Image.merge('RGBA', (*Image.fromarray(crop.astype(np.uint8)).split(), a_img))
    # 裁剪到有效区域
    bbox = rgba.getbbox()
    if bbox:
        rgba = rgba.crop(bbox)
    rgba.save(os.path.join(OUT, name + '.png'))
    print(f"{name}: {rgba.size[0]}x{rgba.size[1]}")

cut(30, 90, 220, 508, 'front')
cut(222, 90, 432, 512, 'threeq')
cut(436, 103, 718, 508, 'side')
cut(718, 83, 882, 508, 'back')
cut(928, 78, 1142, 508, 'back34')
cut(15, 595, 235, 828, 'face_dull')
cut(245, 585, 470, 828, 'face_happy')
cut(475, 575, 670, 838, 'face_curious')
cut(672, 622, 808, 842, 'sleep')
print('done')
