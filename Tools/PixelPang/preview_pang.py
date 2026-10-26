"""시안 미리보기: 역할별 프레임 스트립 + 실제 HUD 위 합성 + 레퍼런스 비교 + GIF."""
import sys, os
from PIL import Image, ImageDraw

sheets_dir, hud_frame, ref_png, out_dir = sys.argv[1:5]
N, FR = 32, 10
BG = (22, 25, 28, 255)
S = 6  # 스트립 확대 배율

def load(role):
    sh = Image.open(os.path.join(sheets_dir, f"pang_{role}.png"))
    return [sh.crop((i * N, 0, (i + 1) * N, N)) for i in range(FR)]

roles = ["planning", "develop", "art", "bug"]

# 1) 역할별 프레임 스트립 (6x)
strip = Image.new("RGBA", (N * S * FR + 10 * (FR + 1), (N * S + 10) * len(roles) + 10), BG)
for r, role in enumerate(roles):
    for i, im in enumerate(load(role)):
        strip.alpha_composite(im.resize((N * S, N * S), Image.NEAREST),
                              (10 + i * (N * S + 10), 10 + r * (N * S + 10)))
strip.save(os.path.join(out_dir, "strip.png"))

# 2) 실제 HUD 위 합성 (게임 표시 배율 ~3.3x -> 3x 근사), 기획(planning) 아이콘 중심
hud = Image.open(hud_frame).convert("RGBA").crop((0, 0, 560, 140))
cx, cy = 98, 60
gif = []
ctx_tiles = []
for im in load("planning"):
    f = hud.copy()
    big = im.resize((N * 3, N * 3), Image.NEAREST)
    f.alpha_composite(big, (cx - N * 3 // 2, cy - N * 3 // 2))
    gif.append(f.resize((f.width * 2, f.height * 2), Image.NEAREST))
    ctx_tiles.append(f.crop((20, 0, 180, 130)).resize((320, 260), Image.NEAREST))
ctx = Image.new("RGBA", (320 * 5 + 4 * 6, 260 * 2 + 4 * 3), BG)
for i, t in enumerate(ctx_tiles):
    ctx.alpha_composite(t, (4 + (i % 5) * 324, 4 + (i // 5) * 264))
ctx.save(os.path.join(out_dir, "in_hud.png"))
hold = [gif[0].copy()]
hud2 = hud.resize((hud.width * 2, hud.height * 2), Image.NEAREST)
seq = [hud2] * 8 + gif + [hud2] * 8
seq[0].save(os.path.join(out_dir, "in_hud.gif"), save_all=True, append_images=seq[1:],
            duration=42, loop=0, disposal=2)
print("ok")
