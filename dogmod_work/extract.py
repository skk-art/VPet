from PIL import Image, ImageFilter
import numpy as np
from collections import deque
import os

img = Image.open(r'D:\VPet-main\dogmod_work\design.png').convert('RGB')
W, H = img.size
arr = np.array(img).astype(int)

# 背景色(采样多点取均值)
bg = np.array([249, 242, 232])
TOL = 30  # 色距容差

def cut(x0, y0, x1, y1, name, bgcolor=None):
    """裁剪区域并抠掉从边缘连通的背景色, 保存透明PNG"""
    crop = arr[y0:y1, x0:x1]
    h, w = crop.shape[:2]
    # 候选背景: 色距<=TOL
    dist = np.abs(crop - (bgcolor if bgcolor is not None else bg)).sum(axis=2)
    isbg = dist <= TOL
    # 从边缘 BFS, 只清除与边缘连通的背景(保护狗身上的浅色)
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
    a_img = Image.fromarray(alpha, 'L').filter(ImageFilter.GaussianBlur(0.8))  # 轻微羽化
    rgba = Image.merge('RGBA', (*Image.fromarray(crop.astype(np.uint8)).split(), a_img))
    out = os.path.join(r'D:\VPet-main\dogmod_work', name + '.png')
    rgba.save(out)
    # 统计不透明像素占比
    print(f"{name}: {w}x{h}, 不透明 {round((alpha>0).mean()*100,1)}%")
    return rgba

cut(8, 75, 222, 508, 'front')       # 正面(坐姿)
cut(222, 68, 432, 512, 'threeq')    # 3/4视角(坐姿)
cut(436, 103, 718, 508, 'side')     # 侧面(站姿)
cut(718, 83, 882, 508, 'back')      # 背面
cut(928, 78, 1142, 508, 'back34')   # 3/4背面
cut(12, 500, 178, 668, 'face_dull')     # 呆萌
cut(178, 500, 348, 668, 'face_happy')   # 开心
cut(348, 500, 502, 668, 'face_curious') # 好奇
cut(462, 500, 650, 688, 'sleep')        # 睡觉(趴姿)
print('done')
