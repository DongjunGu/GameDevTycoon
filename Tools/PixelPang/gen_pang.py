"""도트 '팡' 이펙트 프레임 생성기 (32x32 도트, 10프레임 @24fps)."""
import math, sys, os
from PIL import Image

N = 32
C = (N - 1) / 2.0  # 15.5 -> 짝수 캔버스 중심
WHITE = (255, 255, 255, 255)

# 역할별 강조색 (HUD 아이콘 색에서 추출)
ROLES = {
    "planning": (250, 226, 130),   # 노란 메모지
    "develop":  (150, 200, 255),   # 모니터 화면 하늘색
    "art":      (240, 160, 80),    # 팔레트 주황
    "bug":      (240, 90, 100),    # 버그 빨강
}

def lighten(c, k):
    return tuple(int(v + (255 - v) * k) for v in c[:3]) + (255,)

def put(img, x, y, col):
    if 0 <= x < N and 0 <= y < N:
        img.putpixel((x, y), col)

def disk(img, r, col):
    for y in range(N):
        for x in range(N):
            if math.hypot(x - C, y - C) <= r:
                put(img, x, y, col)

def ring(img, r, th, col, dash=0, phase=0):
    for y in range(N):
        for x in range(N):
            d = math.hypot(x - C, y - C)
            if r - th / 2 <= d < r + th / 2:
                if dash:
                    a = math.atan2(y - C, x - C)
                    seg = int((a + math.pi) / (2 * math.pi) * 64 + phase)
                    if seg % dash: continue
                put(img, x, y, col)

def plus(img, cx, cy, size, col, core=None):
    """십자 반짝이. size=0 -> 점 하나."""
    cx, cy = int(round(cx)), int(round(cy))
    for i in range(-size, size + 1):
        put(img, cx + i, cy, col); put(img, cx, cy + i, col)
    if core and size > 0: put(img, cx, cy, core)

def spark_pos(r, k, off=45):
    a = math.radians(off + 90 * k)
    return C + math.cos(a) * r, C + math.sin(a) * r

def frames(accent):
    acc = accent + (255,)
    light = lighten(accent, 0.55)
    out = []
    # (코어/링 단계, 반짝이 반경, 반짝이 크기)
    for f in range(10):
        im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        if f == 0:
            ring(im, 5, 2, acc); disk(im, 4, WHITE)
        elif f == 1:
            disk(im, 8, light); disk(im, 7, WHITE)
            for k in range(4):  # 대각 섬광 스파이크
                for s in range(8, 13):
                    x, y = spark_pos(s, k); put(im, int(round(x)), int(round(y)), WHITE)
        elif f == 2:
            disk(im, 9, light); disk(im, 6, WHITE)
            ring(im, 9, 1.5, WHITE)
        elif f == 3:
            ring(im, 10.5, 2.5, WHITE); ring(im, 8.5, 1.2, light)
        elif f == 4:
            ring(im, 12, 2, light); ring(im, 12.6, 1, WHITE)
        elif f == 5:
            ring(im, 13.2, 1.5, acc)
        elif f == 6:
            ring(im, 14, 1.2, acc, dash=2)
        elif f == 7:
            ring(im, 14.8, 1.0, acc, dash=3, phase=1)
        # 반짝이: 대각 4개 + 수직/수평 4개(작게)
        sp = {3: (9, 2), 4: (11, 2), 5: (12.5, 1), 6: (13.5, 1), 7: (14.5, 0), 8: (15, 0)}
        if f in sp:
            r, s = sp[f]
            for k in range(4):
                x, y = spark_pos(r, k); plus(im, x, y, s, WHITE if s else light, acc)
            if f >= 4:
                for k in range(4):
                    x, y = spark_pos(r - 3, k, off=0); plus(im, x, y, max(0, s - 1), light)
        out.append(im)
    return out

if __name__ == "__main__":
    dst = sys.argv[1]
    os.makedirs(dst, exist_ok=True)
    for role, col in ROLES.items():
        fr = frames(col)
        sheet = Image.new("RGBA", (N * len(fr), N), (0, 0, 0, 0))
        for i, im in enumerate(fr):
            sheet.paste(im, (i * N, 0))
        sheet.save(os.path.join(dst, f"pang_{role}.png"))
    print("ok")
