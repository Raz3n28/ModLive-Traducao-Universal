# Gera modelo/Manifest/preview.png (imagem da Workshop): a arte base (ferramentas/logos/preview-base.png)
# com os cartoes "UI SCALE" e "FONT SIZE" nas laterais. Uso: python ferramentas/gerar_preview.py
# Precisa da fonte Inter (Google Fonts); troque a pasta F se ela estiver em outro lugar.
import os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = os.path.join(RAIZ, "ferramentas", "logos", "preview-base.png")
out = os.path.join(RAIZ, "modelo", "Manifest", "preview.png")
base = Image.open(src).convert("RGBA")
W, H = base.size
S = 3  # supersample
F = "/usr/share/fonts/opentype/inter/"
def fnt(n, s): return ImageFont.truetype(F + n, s * S)
ROSA = (255, 140, 230); BRANCO = (255, 255, 255); LILAS = (205, 190, 240)

def cartao(x, y, w, h, icone, titulo, sub, desc):
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle((x - 8, y - 8, x + w + 8, y + h + 8), 30, fill=(230, 90, 220, 80))
    glow = glow.filter(ImageFilter.GaussianBlur(22))
    base.alpha_composite(glow)
    L = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(L)
    d.rounded_rectangle((0, 0, w * S - 1, h * S - 1), 24 * S, fill=(120, 60, 190, 130), outline=ROSA + (240,), width=3 * S)
    ic = 100
    ix = (w - ic) // 2
    d.rounded_rectangle((ix * S, 24 * S, (ix + ic) * S, (24 + ic) * S), 22 * S, fill=(200, 110, 240, 95), outline=ROSA + (170,), width=2 * S)
    icone(d, ix * S, 24 * S, ic * S)
    cx = w * S / 2
    d.text((cx, 158 * S), titulo, font=fnt("Inter-ExtraBold.otf", 40), fill=BRANCO, anchor="mm")
    d.text((cx, 198 * S), sub, font=fnt("Inter-ExtraBold.otf", 28), fill=ROSA, anchor="mm")
    d.text((cx, 233 * S), desc, font=fnt("Inter-SemiBold.otf", 19), fill=LILAS, anchor="mm")
    L = L.resize((w, h), Image.LANCZOS)
    base.alpha_composite(L, (x, y))
    tw, th = 80, 30
    E = Image.new("RGBA", (tw * S, th * S), (0, 0, 0, 0))
    de = ImageDraw.Draw(E)
    de.rounded_rectangle((0, 0, tw * S - 1, th * S - 1), 15 * S, fill=(255, 120, 220, 255))
    de.text((tw * S / 2, th * S / 2), "NEW", font=fnt("Inter-ExtraBold.otf", 18), fill=(60, 10, 80), anchor="mm")
    E = E.resize((tw, th), Image.LANCZOS)
    base.alpha_composite(E, (x + (w - tw) // 2, y - th // 2))

def ic_ui(d, x, y, t):
    # janela com setas de expandir nos cantos
    m = t * 0.22; lw = int(t * 0.06)
    a, b = x + m, y + m; c, e = x + t - m, y + t - m
    d.rounded_rectangle((a + t*0.12, b + t*0.12, c - t*0.12, e - t*0.12), int(t*0.05), outline=BRANCO, width=lw)
    k = t * 0.16
    for (px, py, sx, sy) in ((a, b, 1, 1), (c, b, -1, 1), (a, e, 1, -1), (c, e, -1, -1)):
        d.line((px, py, px + sx * k, py), fill=ROSA, width=lw)
        d.line((px, py, px, py + sy * k), fill=ROSA, width=lw)

def ic_fonte(d, x, y, t):
    d.text((x + t * 0.20, y + t * 0.80), "A", font=ImageFont.truetype(F + "Inter-Bold.otf", int(t * 0.30)), fill=LILAS, anchor="ls")
    d.text((x + t * 0.40, y + t * 0.80), "A", font=ImageFont.truetype(F + "Inter-ExtraBold.otf", int(t * 0.58)), fill=BRANCO, anchor="ls")
    lw = int(t * 0.05); ax = x + t * 0.86
    d.line((ax, y + t * 0.24, ax, y + t * 0.78), fill=ROSA, width=lw)
    d.polygon([(ax, y + t*0.17), (ax - t*0.07, y + t*0.29), (ax + t*0.07, y + t*0.29)], fill=ROSA)
    d.polygon([(ax, y + t*0.85), (ax - t*0.07, y + t*0.73), (ax + t*0.07, y + t*0.73)], fill=ROSA)

w, h = 300, 260
cartao(32, 212, w, h, ic_ui, "UI SCALE", "50% – 200%", "Resize the game HUD")
cartao(W - 32 - w, 212, w, h, ic_fonte, "FONT SIZE", "50% – 200%", "Resize all game text")
base.convert("RGB").save(out, optimize=True)
