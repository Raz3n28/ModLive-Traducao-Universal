# Gera os PNGs da interface do menu F8 (visual da proposta B) e as opcoes de logo "PR".
# Uso: python ferramentas/gerar_ui.py <pasta_das_fontes>
#   a pasta precisa de SG-Bold.ttf (Space Grotesk 700), Orb-Bold.ttf (Orbitron 800), Chakra-Bold.ttf (Chakra Petch 700)
# Saida: modelo/v_common/UI/*.png (vai junto com o mod) e ferramentas/logos/*.png (opcoes para escolher).
# Cada peca e desenhada 4x maior e reduzida: cantos suaves. As bordas (9-slice) estao em Visual.cs.
import os
import sys
from PIL import Image, ImageDraw, ImageFont

SS = 4
ESCOLHIDA = "2"   # numero da logo que vai para o menu
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI = os.path.join(RAIZ, "modelo", "v_common", "UI")
LOGOS = os.path.join(RAIZ, "ferramentas", "logos")

BG = "#101418"
LATERAL = "#0b0e11"
CARD = "#161b21"
BORDA = "#262e37"
BORDA_HOVER = "#3a4652"
TEAL = "#2dd4bf"
TEAL_HOVER = "#5eead4"
TEAL_PRESS = "#14b8a6"
TEAL_ESCURO = "#12302d"
TEXTO = "#e6edf3"
MUDO = "#8b98a5"
AMBAR = "#f59e0b"
AMBAR_HOVER = "#fbbf24"
AMBAR_PRESS = "#d97706"


def rgba(c, a=255):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), a)


def peca(nome, w, h, raio, fundo=None, borda=None, larg_borda=1, cantos=(True, True, True, True)):
    """Retangulo arredondado com fundo e/ou borda de 1 px. cantos = (sup-esq, sup-dir, inf-dir, inf-esq)."""
    W, H, R, B = w * SS, h * SS, raio * SS, larg_borda * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if borda:
        d = ImageDraw.Draw(img)
        d.rounded_rectangle((0, 0, W - 1, H - 1), R, fill=rgba(borda), corners=cantos)
        # abre o miolo (transparente) e pinta o fundo por cima
        miolo = Image.new("L", (W, H), 0)
        ImageDraw.Draw(miolo).rounded_rectangle((B, B, W - 1 - B, H - 1 - B), max(0, R - B), fill=255, corners=cantos)
        img.paste((0, 0, 0, 0), (0, 0), miolo)
        if fundo:
            camada = Image.new("RGBA", (W, H), rgba(fundo))
            img.paste(camada, (0, 0), miolo)
    elif fundo:
        ImageDraw.Draw(img).rounded_rectangle((0, 0, W - 1, H - 1), R, fill=rgba(fundo), corners=cantos)
    img = img.resize((w, h), Image.LANCZOS)
    img.save(os.path.join(UI, nome + ".png"))


def check(nome, ligado):
    W = 18 * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if ligado:
        d.rounded_rectangle((0, 0, W - 1, W - 1), 5 * SS, fill=rgba(TEAL))
        d.line([(4.5 * SS, 9.5 * SS), (7.5 * SS, 12.5 * SS), (13.5 * SS, 5.5 * SS)], fill=rgba("#04201c"), width=int(2.2 * SS), joint="curve")
    else:
        d.rounded_rectangle((0, 0, W - 1, W - 1), 5 * SS, fill=rgba(BORDA_HOVER))
        d.rounded_rectangle((SS, SS, W - 1 - SS, W - 1 - SS), 4 * SS, fill=rgba(LATERAL))
    img.resize((18, 18), Image.LANCZOS).save(os.path.join(UI, nome + ".png"))


def alca(nome):
    # pontinhos no canto inferior direito: arrastar para redimensionar
    W = 14 * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for (x, y) in [(11, 11), (7, 11), (11, 7), (3, 11), (7, 7), (11, 3)]:
        d.ellipse(((x - 1.2) * SS, (y - 1.2) * SS, (x + 1.2) * SS, (y + 1.2) * SS), fill=rgba(MUDO))
    img.resize((14, 14), Image.LANCZOS).save(os.path.join(UI, nome + ".png"))


def gerar_ui():
    os.makedirs(UI, exist_ok=True)
    # janela e painel lateral (o lateral so arredonda os cantos da esquerda)
    peca("janela", 48, 48, 14, BG, BORDA)
    peca("lateral", 48, 48, 14, LATERAL, cantos=(True, False, False, True))
    peca("card", 40, 40, 14, CARD)
    peca("linha", 4, 1, 0, BORDA)
    # navegacao lateral
    peca("nav_hover", 32, 32, 10, "#11161b")
    peca("nav_ativo", 32, 32, 10, TEAL_ESCURO)
    # botao contornado (Importar, Exportar, Fechar, Anterior...)
    peca("btn", 32, 32, 10, None, BORDA)
    peca("btn_hover", 32, 32, 10, CARD, BORDA_HOVER)
    peca("btn_press", 32, 32, 10, LATERAL, BORDA_HOVER)
    # botao principal teal (+ Novo termo, OK, Comecar)
    peca("pri", 32, 32, 10, TEAL)
    peca("pri_hover", 32, 32, 10, TEAL_HOVER)
    peca("pri_press", 32, 32, 10, TEAL_PRESS)
    # botao ambar (Pausar traducao)
    peca("amb", 32, 32, 10, AMBAR)
    peca("amb_hover", 32, 32, 10, AMBAR_HOVER)
    peca("amb_press", 32, 32, 10, AMBAR_PRESS)
    # botao vermelho (apagar, limpar cache)
    peca("perigo", 32, 32, 10, "#ef4444")
    peca("perigo_hover", 32, 32, 10, "#f87171")
    peca("perigo_press", 32, 32, 10, "#b91c1c")
    # botao cheio escuro (escolhas, idioma do menu)
    peca("sec", 24, 24, 8, CARD)
    peca("sec_hover", 24, 24, 8, "#1e252d")
    peca("sec_press", 24, 24, 8, LATERAL)
    # pilulas de filtro
    peca("pil", 26, 26, 13, CARD)
    peca("pil_hover", 26, 26, 13, "#1e252d")
    peca("pil_ativa", 26, 26, 13, TEAL)
    # etiqueta tingida pela cor da categoria (branca: a cor vem do codigo)
    peca("etiqueta", 24, 24, 12, "#ffffff")
    # TRAVAR (contorno teal) e TRAVADO (cheio teal escuro)
    peca("trava", 24, 24, 8, None, TEAL)
    peca("trava_hover", 24, 24, 8, TEAL_ESCURO, TEAL)
    peca("travado", 24, 24, 8, TEAL_ESCURO)
    # campo de texto
    peca("campo", 32, 32, 10, LATERAL, BORDA)
    peca("campo_hover", 32, 32, 10, LATERAL, BORDA_HOVER)
    peca("campo_foco", 32, 32, 10, LATERAL, TEAL)
    # barra de rolagem
    peca("rolagem", 8, 24, 4, BORDA)
    peca("rolagem_hover", 8, 24, 4, BORDA_HOVER)
    check("check_off", False)
    check("check_on", True)
    alca("alca")


def texto_centro(d, caixa, txt, fonte, cor):
    x0, y0, x1, y1 = caixa
    l, t, r, b = d.textbbox((0, 0), txt, font=fonte)
    d.text(((x0 + x1 - (r - l)) / 2 - l, (y0 + y1 - (b - t)) / 2 - t), txt, font=fonte, fill=cor)


def gerar_logos(pasta_fontes):
    os.makedirs(LOGOS, exist_ok=True)
    sg = os.path.join(pasta_fontes, "SG-Bold.ttf")
    orb = os.path.join(pasta_fontes, "Orb-Bold.ttf")
    chakra = os.path.join(pasta_fontes, "Chakra-Bold.ttf")
    N = 128 * SS
    logos = []

    # 1 - quadrado teal, PR escuro
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, N - 1, N - 1), 30 * SS, fill=rgba(TEAL))
    texto_centro(d, (0, 0, N, N), "PR", ImageFont.truetype(sg, 64 * SS), rgba("#04201c"))
    logos.append(("1-quadrado", img))

    # 2 - hexagono contornado
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    import math
    pts = [(N / 2 + (N / 2 - 6 * SS) * math.cos(math.radians(60 * i - 90)), N / 2 + (N / 2 - 6 * SS) * math.sin(math.radians(60 * i - 90))) for i in range(6)]
    d.polygon(pts, fill=rgba(LATERAL))
    d.line(pts + [pts[0]], fill=rgba(TEAL), width=7 * SS, joint="curve")
    texto_centro(d, (0, 0, N, N), "PR", ImageFont.truetype(orb, 40 * SS), rgba(TEXTO))
    logos.append(("2-hexagono", img))

    # 3 - balao de fala (traducao)
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.rounded_rectangle((4 * SS, 8 * SS, N - 4 * SS, N - 30 * SS), 26 * SS, fill=rgba(TEAL))
    d.polygon([(30 * SS, N - 34 * SS), (58 * SS, N - 34 * SS), (26 * SS, N - 6 * SS)], fill=rgba(TEAL))
    texto_centro(d, (4 * SS, 8 * SS, N - 4 * SS, N - 30 * SS), "PR", ImageFont.truetype(sg, 56 * SS), rgba("#04201c"))
    logos.append(("3-balao", img))

    # 4 - orbita (espaco)
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.ellipse((6 * SS, 6 * SS, N - 6 * SS, N - 6 * SS), fill=rgba(LATERAL), outline=rgba(TEAL), width=6 * SS)
    d.ellipse((N - 34 * SS, 10 * SS, N - 10 * SS, 34 * SS), fill=rgba(AMBAR), outline=rgba(LATERAL), width=4 * SS)
    texto_centro(d, (0, 0, N, N), "PR", ImageFont.truetype(sg, 52 * SS), rgba(TEXTO))
    logos.append(("4-orbita", img))

    # 5 - dois baloes (idioma A -> B): P no escuro, R no teal
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.rounded_rectangle((4 * SS, 4 * SS, 78 * SS, 70 * SS), 18 * SS, fill=rgba(CARD), outline=rgba(BORDA_HOVER), width=3 * SS)
    d.polygon([(18 * SS, 66 * SS), (36 * SS, 66 * SS), (14 * SS, 84 * SS)], fill=rgba(CARD))
    d.rounded_rectangle((50 * SS, 50 * SS, 124 * SS, 116 * SS), 18 * SS, fill=rgba(TEAL))
    d.polygon([(92 * SS, 112 * SS), (110 * SS, 112 * SS), (114 * SS, 126 * SS)], fill=rgba(TEAL))
    f = ImageFont.truetype(sg, 46 * SS)
    texto_centro(d, (4 * SS, 4 * SS, 70 * SS, 66 * SS), "P", f, rgba(TEXTO))
    texto_centro(d, (54 * SS, 50 * SS, 124 * SS, 116 * SS), "R", f, rgba("#04201c"))
    logos.append(("5-dois-baloes", img))

    # 6 - emblema angular sci-fi (cantos cortados), estilo HUD do jogo
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    c = 26 * SS
    corte = [(c, 4 * SS), (N - 4 * SS, 4 * SS), (N - 4 * SS, N - c), (N - c, N - 4 * SS), (4 * SS, N - 4 * SS), (4 * SS, c)]
    d.polygon(corte, fill=rgba(LATERAL))
    d.line(corte + [corte[0]], fill=rgba(TEAL), width=5 * SS, joint="curve")
    d.line([(N - 40 * SS, N - 14 * SS), (N - 14 * SS, N - 40 * SS)], fill=rgba(AMBAR), width=5 * SS)
    texto_centro(d, (0, 0, N, N), "PR", ImageFont.truetype(chakra, 58 * SS), rgba(TEAL))
    logos.append(("6-emblema", img))

    # nome do mod em Space Grotesk (a fonte da proposta B), em 2x para ficar nitido
    f1 = ImageFont.truetype(sg, 34)
    marca = Image.new("RGBA", (240, 84), (0, 0, 0, 0))
    dm = ImageDraw.Draw(marca)
    dm.text((0, -4), "Universal", font=f1, fill=rgba(TEAL))
    dm.text((0, 36), "Translator", font=f1, fill=rgba(TEAL))
    marca = marca.crop(marca.getbbox())
    fundo = Image.new("RGBA", (marca.width, marca.height), (0, 0, 0, 0))
    fundo.paste(marca, (0, 0))
    fundo.save(os.path.join(UI, "marca.png"))

    finais = []
    for nome, im in logos:
        im = im.resize((128, 128), Image.LANCZOS)
        im.save(os.path.join(LOGOS, "logo-" + nome + ".png"))
        # a escolhida vira modelo/v_common/UI/logo.png (por enquanto a opcao 1)
        if nome.startswith(ESCOLHIDA + "-"):
            im.save(os.path.join(UI, "logo.png"))
        finais.append((nome, im))

    # folha de comparacao: cada logo sobre o fundo do menu, grande e no tamanho real (40 px)
    fonte_rot = ImageFont.truetype(sg, 20)
    larg = 260
    folha = Image.new("RGBA", (larg * len(finais), 300), rgba(BG))
    d = ImageDraw.Draw(folha)
    for i, (nome, im) in enumerate(finais):
        x = i * larg
        folha.paste(im, (x + (larg - 128) // 2, 24), im)
        peq = im.resize((40, 40), Image.LANCZOS)
        d.rounded_rectangle((x + 30, 176, x + larg - 30, 236), 10, fill=rgba(LATERAL))
        folha.paste(peq, (x + 40, 186), peq)
        d.text((x + 90, 196), "Universal", font=ImageFont.truetype(sg, 16), fill=rgba(TEAL))
        d.text((x + 90, 214), "Translator", font=ImageFont.truetype(sg, 12), fill=rgba(MUDO))
        texto_centro(d, (x, 250, x + larg, 290), "Opcao " + nome.split("-")[0], fonte_rot, rgba(TEXTO))
    folha.save(os.path.join(LOGOS, "opcoes-logo.png"))


if __name__ == "__main__":
    gerar_ui()
    if len(sys.argv) > 1:
        gerar_logos(sys.argv[1])
    print("ok")
