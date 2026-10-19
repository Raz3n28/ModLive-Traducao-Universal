# Gera os PNGs da interface do menu F8 (visual da proposta B), um conjunto por tema.
# Uso: python ferramentas/gerar_ui.py <pasta_das_fontes>
#   a pasta precisa de SG-Bold.ttf (Space Grotesk 700) e Orb-Bold.ttf (Orbitron 800), do Google Fonts
# Saida:
#   modelo/v_common/UI/<tema>/*.png + tema.txt   pecas e cores de cada tema (o menu le o tema.txt)
#   modelo/v_common/UI/comum/*.png               icones das abas, sombra e pecas brancas (tingidas pelo codigo)
#   modelo/v_common/UI/temas.txt                 lista de temas na ordem do menu
#   ferramentas/logos/temas.png                  previa de todos os temas lado a lado
# Cada peca e desenhada 4x maior e reduzida: cantos suaves. As bordas (9-slice) estao em Menu.cs / Png.cs.
import math
import os
import sys
from PIL import Image, ImageDraw, ImageFont

SS = 4
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI = os.path.join(RAIZ, "modelo", "v_common", "UI")
LOGOS = os.path.join(RAIZ, "ferramentas", "logos")

# cores de cada tema. destaque = botao principal, selecionado e logo; sec = botao Pausar (segunda cor).
TEMAS = {
    "teal": dict(nome="Teal", fundo="#101418", lateral="#0b0e11", card="#161b21", borda="#262e37", borda_hover="#3a4652",
                 texto="#e6edf3", mudo="#8b98a5", destaque="#2dd4bf", destaque_texto="#04201c", destaque_hover="#5eead4",
                 destaque_press="#14b8a6", nav_ativo="#12302d", sec="#f59e0b", sec_texto="#231500",
                 ok="#86efac", aviso="#fcd34d", erro="#fca5a5", campo="#0b0e11", faixa=None),
    "brasil": dict(nome="Brasil", fundo="#0c1a12", lateral="#08130d", card="#12261a", borda="#1f3d2a", borda_hover="#2f5a3e",
                   texto="#eef6ee", mudo="#93ab9a", destaque="#ffdf00", destaque_texto="#002776", destaque_hover="#ffe94d",
                   destaque_press="#e6c800", nav_ativo="#2a3312", sec="#2f6fe0", sec_texto="#ffffff",
                   ok="#86efac", aviso="#ffdf00", erro="#fca5a5", campo="#08130d", faixa=["#009c3b", "#ffdf00", "#002776"]),
    "eua": dict(nome="EUA", fundo="#0e1322", lateral="#0a0e1a", card="#151c30", borda="#253050", borda_hover="#34426b",
                texto="#f1f3f9", mudo="#9aa3bd", destaque="#d0293d", destaque_texto="#ffffff", destaque_hover="#e2485a",
                destaque_press="#a81f31", nav_ativo="#2e1722", sec="#3c5aa6", sec_texto="#ffffff",
                ok="#86efac", aviso="#fcd34d", erro="#fca5a5", campo="#0a0e1a", faixa=["#b22234", "#ffffff", "#b22234", "#ffffff", "#3c3b6e"]),
    "roxo": dict(nome="Roxo Neon", fundo="#120f1c", lateral="#0d0b15", card="#1a1628", borda="#2c2542", borda_hover="#3d3460",
                 texto="#ece9f7", mudo="#9c95b8", destaque="#a78bfa", destaque_texto="#1a0b3d", destaque_hover="#c4b5fd",
                 destaque_press="#8b6cf0", nav_ativo="#241c3d", sec="#f472b6", sec_texto="#3b0a24",
                 ok="#86efac", aviso="#fcd34d", erro="#fca5a5", campo="#0d0b15", faixa=None),
    "sol": dict(nome="Por do Sol", fundo="#17110d", lateral="#110c09", card="#211812", borda="#3a2a1e", borda_hover="#523b2a",
                texto="#f7ede4", mudo="#b29c88", destaque="#fb923c", destaque_texto="#2a1205", destaque_hover="#fdba74",
                destaque_press="#ea7a1f", nav_ativo="#3a2416", sec="#f43f5e", sec_texto="#ffffff",
                ok="#86efac", aviso="#fcd34d", erro="#fca5a5", campo="#110c09", faixa=None),
    "claro": dict(nome="Claro", fundo="#f5f6fa", lateral="#ffffff", card="#ffffff", borda="#dfe2ec", borda_hover="#c5cad8",
                  texto="#1d2130", mudo="#646a7e", destaque="#6d5dfc", destaque_texto="#ffffff", destaque_hover="#8577fd",
                  destaque_press="#5646e8", nav_ativo="#ece9ff", sec="#f59e0b", sec_texto="#231500",
                  ok="#15803d", aviso="#b45309", erro="#dc2626", campo="#ffffff", faixa=None),
}
# ordem em que aparecem no menu
ORDEM = ["teal", "brasil", "eua", "roxo", "sol", "claro"]


def rgba(c, a=255):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), a)


def mistura(a, b, t):
    ca, cb = rgba(a), rgba(b)
    return "#%02x%02x%02x" % tuple(int(round(ca[i] + (cb[i] - ca[i]) * t)) for i in range(3))


def peca(pasta, nome, w, h, raio, fundo=None, borda=None, larg_borda=1, cantos=(True, True, True, True)):
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
    img.save(os.path.join(pasta, nome + ".png"))


def salvar(img, pasta, nome, w, h):
    img.resize((w, h), Image.LANCZOS).save(os.path.join(pasta, nome + ".png"))


def check(pasta, nome, ligado, t):
    W = 18 * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if ligado:
        d.rounded_rectangle((0, 0, W - 1, W - 1), 5 * SS, fill=rgba(t["destaque"]))
        d.line([(4.5 * SS, 9.5 * SS), (7.5 * SS, 12.5 * SS), (13.5 * SS, 5.5 * SS)], fill=rgba(t["destaque_texto"]), width=int(2.2 * SS), joint="curve")
    else:
        d.rounded_rectangle((0, 0, W - 1, W - 1), 5 * SS, fill=rgba(t["borda_hover"]))
        d.rounded_rectangle((SS, SS, W - 1 - SS, W - 1 - SS), 4 * SS, fill=rgba(t["campo"]))
    salvar(img, pasta, nome, 18, 18)


def fechar(pasta, t):
    # botao "x" redondo do canto da janela (normal e com o mouse em cima, vermelho)
    for nome, fundo, cor in (("fechar", t["card"], t["mudo"]), ("fechar_hover", "#ef4444", "#ffffff")):
        W = 28 * SS
        img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.ellipse((0, 0, W - 1, W - 1), fill=rgba(fundo))
        a, b = 10 * SS, 18 * SS
        d.line([(a, a), (b, b)], fill=rgba(cor), width=int(2 * SS))
        d.line([(a, b), (b, a)], fill=rgba(cor), width=int(2 * SS))
        salvar(img, pasta, nome, 28, 28)


def faixa(pasta, cores):
    # faixa fina com as cores da bandeira, embaixo da logo
    W, H = 160 * SS, 4 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    n = len(cores)
    for i, c in enumerate(cores):
        d.rectangle((int(W * i / n), 0, int(W * (i + 1) / n), H), fill=rgba(c))
    mascara = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mascara).rounded_rectangle((0, 0, W - 1, H - 1), 2 * SS, fill=255)
    img.putalpha(mascara)
    salvar(img, pasta, "faixa", 160, 4)


def gerar_tema(id_tema, t, fontes):
    pasta = os.path.join(UI, id_tema)
    os.makedirs(pasta, exist_ok=True)
    claro = id_tema == "claro"
    sec_btn = mistura(t["card"], t["texto"], 0.05)
    sec_hover = mistura(t["card"], t["texto"], 0.11)
    peca(pasta, "janela", 48, 48, 14, t["fundo"], t["borda"])
    peca(pasta, "lateral", 48, 48, 14, t["lateral"], cantos=(True, False, False, True))
    peca(pasta, "card", 40, 40, 14, t["card"], t["borda"] if claro else None)
    peca(pasta, "nav_hover", 32, 32, 10, mistura(t["lateral"], t["texto"], 0.05))
    peca(pasta, "nav_ativo", 32, 32, 10, t["nav_ativo"])
    peca(pasta, "btn", 32, 32, 10, None, t["borda"])
    peca(pasta, "btn_hover", 32, 32, 10, t["card"], t["borda_hover"])
    peca(pasta, "btn_press", 32, 32, 10, t["lateral"], t["borda_hover"])
    peca(pasta, "pri", 32, 32, 10, t["destaque"])
    peca(pasta, "pri_hover", 32, 32, 10, t["destaque_hover"])
    peca(pasta, "pri_press", 32, 32, 10, t["destaque_press"])
    peca(pasta, "amb", 32, 32, 10, t["sec"])
    peca(pasta, "amb_hover", 32, 32, 10, mistura(t["sec"], "#ffffff", 0.18))
    peca(pasta, "amb_press", 32, 32, 10, mistura(t["sec"], "#000000", 0.15))
    peca(pasta, "perigo", 32, 32, 10, "#ef4444")
    peca(pasta, "perigo_hover", 32, 32, 10, "#f87171")
    peca(pasta, "perigo_press", 32, 32, 10, "#b91c1c")
    peca(pasta, "sec", 24, 24, 8, sec_btn, t["borda"] if claro else None)
    peca(pasta, "sec_hover", 24, 24, 8, sec_hover, t["borda_hover"] if claro else None)
    peca(pasta, "sec_press", 24, 24, 8, t["lateral"], t["borda_hover"] if claro else None)
    peca(pasta, "campo", 32, 32, 10, t["campo"], t["borda"])
    peca(pasta, "campo_hover", 32, 32, 10, t["campo"], t["borda_hover"])
    peca(pasta, "campo_foco", 32, 32, 10, t["campo"], t["destaque"])
    peca(pasta, "rolagem", 8, 24, 4, t["borda"])
    peca(pasta, "rolagem_hover", 8, 24, 4, t["borda_hover"])
    check(pasta, "check_off", False, t)
    check(pasta, "check_on", True, t)
    # trilho do interruptor (36 x 20); a bolinha (comum/bolinha.png) desliza por cima
    peca(pasta, "interruptor_off", 36, 20, 10, t["borda_hover"])
    peca(pasta, "interruptor_on", 36, 20, 10, t["destaque"])
    fechar(pasta, t)
    if t.get("faixa"):
        faixa(pasta, t["faixa"])
    if fontes:
        logo_e_nome(pasta, t, fontes)
    # cores para o codigo do menu (texto, etiquetas, avisos)
    with open(os.path.join(pasta, "tema.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("# gerado por ferramentas/gerar_ui.py\n")
        f.write("nome=" + t["nome"] + "\n")
        for k in ("fundo", "lateral", "card", "borda", "borda_hover", "texto", "mudo", "destaque", "destaque_texto",
                  "nav_ativo", "sec", "sec_texto", "ok", "aviso", "erro", "campo"):
            f.write(k + "=" + t[k] + "\n")
        f.write("claro=" + ("1" if claro else "0") + "\n")


def icone(nome, desenhar):
    """Icone branco 36 x 36 (desenhado em 18 px no menu); o codigo tinge com a cor do tema."""
    W = 36 * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    desenhar(d, SS * 2, rgba("#ffffff"))   # u = 1 px do icone em 18 px
    salvar(img, os.path.join(UI, "comum"), "ic_" + nome, 36, 36)


def gerar_comum(fontes):
    pasta = os.path.join(UI, "comum")
    os.makedirs(pasta, exist_ok=True)
    b = rgba("#ffffff")

    # sombra da janela: preto que some para fora (9-slice com borda 32)
    N = 96
    img = Image.new("RGBA", (N * SS, N * SS), (0, 0, 0, 0))
    for i in range(32):
        a = int(150 * (i / 32.0) ** 2)
        ImageDraw.Draw(img).rounded_rectangle((i * SS, i * SS, (N - i) * SS - 1, (N - i) * SS - 1), (46 - i) * SS, fill=(0, 0, 0, a))
    salvar(img, pasta, "sombra", N, N)

    peca(pasta, "branco", 12, 12, 4, "#ffffff")          # barra de progresso, linhas
    peca(pasta, "etiqueta", 24, 24, 12, "#ffffff")       # pilula tingida (categorias, ATIVA/PAUSADA)
    # bolinha do interruptor
    W = 16 * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse((0, 0, W - 1, W - 1), fill=b)
    salvar(img, pasta, "bolinha", 16, 16)
    # alca de redimensionar (pontinhos)
    W = 14 * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for (x, y) in [(11, 11), (7, 11), (11, 7), (3, 11), (7, 7), (11, 3)]:
        d.ellipse(((x - 1.2) * SS, (y - 1.2) * SS, (x + 1.2) * SS, (y + 1.2) * SS), fill=b)
    salvar(img, pasta, "alca", 14, 14)

    def L(u):
        return int(1.6 * u)   # espessura do traco

    def painel(d, u, c):
        for (x, y) in ((2, 2), (10, 2), (2, 10), (10, 10)):
            d.rounded_rectangle((x * u, y * u, (x + 6) * u, (y + 6) * u), int(1.5 * u), outline=c, width=L(u))

    def casa(d, u, c):
        d.line([(2 * u, 9 * u), (9 * u, 2.5 * u), (16 * u, 9 * u)], fill=c, width=L(u), joint="curve")
        d.line([(4 * u, 8 * u), (4 * u, 15.5 * u), (14 * u, 15.5 * u), (14 * u, 8 * u)], fill=c, width=L(u), joint="curve")
        d.rectangle((7.6 * u, 10.5 * u, 10.4 * u, 15.5 * u), fill=c)

    def chave(d, u, c):
        d.ellipse((2 * u, 2 * u, 9 * u, 9 * u), outline=c, width=L(u))
        d.line([(8 * u, 8 * u), (16 * u, 16 * u)], fill=c, width=L(u))
        d.line([(12.5 * u, 12.5 * u), (10.5 * u, 14.5 * u)], fill=c, width=L(u))
        d.line([(14.5 * u, 14.5 * u), (12.5 * u, 16.5 * u)], fill=c, width=L(u))

    def pincel(d, u, c):
        d.line([(4 * u, 14 * u), (14 * u, 4 * u)], fill=c, width=int(3 * u))
        d.polygon([(2.5 * u, 15.5 * u), (3.2 * u, 12 * u), (6 * u, 14.8 * u)], fill=c)
        d.line([(12 * u, 3 * u), (15 * u, 6 * u)], fill=c, width=L(u))

    def livro(d, u, c):
        d.line([(9 * u, 4 * u), (9 * u, 15 * u)], fill=c, width=L(u))
        d.polygon([(2 * u, 4 * u), (9 * u, 5 * u), (9 * u, 15 * u), (2 * u, 14 * u)], outline=c, width=L(u))
        d.polygon([(16 * u, 4 * u), (9 * u, 5 * u), (9 * u, 15 * u), (16 * u, 14 * u)], outline=c, width=L(u))

    def etiqueta(d, u, c):
        d.polygon([(2 * u, 2 * u), (9.5 * u, 2 * u), (16 * u, 8.5 * u), (8.5 * u, 16 * u), (2 * u, 9.5 * u)], outline=c, width=L(u))
        d.ellipse((5 * u, 5 * u, 7.6 * u, 7.6 * u), fill=c)

    def ajustes(d, u, c):
        for y, x in ((4, 11), (9, 6), (14, 12)):
            d.line([(2 * u, y * u), (16 * u, y * u)], fill=c, width=L(u))
            d.ellipse(((x - 2) * u, (y - 2) * u, (x + 2) * u, (y + 2) * u), fill=c)

    def terminal(d, u, c):
        d.rounded_rectangle((1.5 * u, 3 * u, 16.5 * u, 15 * u), int(2 * u), outline=c, width=L(u))
        d.line([(4.5 * u, 7 * u), (7 * u, 9 * u), (4.5 * u, 11 * u)], fill=c, width=L(u), joint="curve")
        d.line([(8.5 * u, 11.5 * u), (13 * u, 11.5 * u)], fill=c, width=L(u))

    def letras(d, u, c):
        if fontes:
            f = ImageFont.truetype(os.path.join(fontes, "SG-Bold.ttf"), int(13 * u))
            l, t2, r, bt = d.textbbox((0, 0), "Aa", font=f)
            d.text(((18 * u - (r - l)) / 2 - l, (18 * u - (bt - t2)) / 2 - t2), "Aa", font=f, fill=c)

    for nome, f in (("painel", painel), ("geral", casa), ("provedor", chave), ("estilo", pincel), ("glossario", livro),
                    ("categorias", etiqueta), ("avancado", ajustes), ("log", terminal), ("fontes", letras)):
        icone(nome, f)


def texto_centro(d, caixa, txt, fonte, cor):
    x0, y0, x1, y1 = caixa
    l, t, r, b = d.textbbox((0, 0), txt, font=fonte)
    d.text(((x0 + x1 - (r - l)) / 2 - l, (y0 + y1 - (b - t)) / 2 - t), txt, font=fonte, fill=cor)


def logo_e_nome(pasta, t, fontes):
    # logo escolhida (opcao 2: hexagono "PR"), na cor de destaque do tema
    N = 128 * SS
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = [(N / 2 + (N / 2 - 6 * SS) * math.cos(math.radians(60 * i - 90)), N / 2 + (N / 2 - 6 * SS) * math.sin(math.radians(60 * i - 90))) for i in range(6)]
    d.polygon(pts, fill=rgba(t["lateral"]))
    d.line(pts + [pts[0]], fill=rgba(t["destaque"]), width=7 * SS, joint="curve")
    texto_centro(d, (0, 0, N, N), "PR", ImageFont.truetype(os.path.join(fontes, "Orb-Bold.ttf"), 40 * SS), rgba(t["texto"]))
    img.resize((128, 128), Image.LANCZOS).save(os.path.join(pasta, "logo.png"))
    # nome do mod em Space Grotesk (a fonte da proposta B), em 2x para ficar nitido
    f1 = ImageFont.truetype(os.path.join(fontes, "SG-Bold.ttf"), 34)
    marca = Image.new("RGBA", (240, 84), (0, 0, 0, 0))
    dm = ImageDraw.Draw(marca)
    dm.text((0, -4), "Universal", font=f1, fill=rgba(t["destaque"]))
    dm.text((0, 36), "Translator", font=f1, fill=rgba(t["destaque"]))
    marca.crop(marca.getbbox()).save(os.path.join(pasta, "marca.png"))


def previa_temas(fontes):
    # folha com um mini menu de cada tema, para ver as cores juntas
    f = ImageFont.truetype(os.path.join(fontes, "SG-Bold.ttf"), 15)
    fp = ImageFont.truetype(os.path.join(fontes, "SG-Bold.ttf"), 12)
    larg, alt = 300, 190
    folha = Image.new("RGBA", (larg * 3 + 40, alt * 2 + 30), (20, 20, 24, 255))
    for i, id_tema in enumerate(ORDEM):
        t = TEMAS[id_tema]
        x0, y0 = 10 + (i % 3) * (larg + 10), 10 + (i // 3) * (alt + 10)
        d = ImageDraw.Draw(folha)
        d.rounded_rectangle((x0, y0, x0 + larg, y0 + alt), 14, fill=rgba(t["fundo"]), outline=rgba(t["borda"]))
        d.rounded_rectangle((x0, y0, x0 + 96, y0 + alt), 14, fill=rgba(t["lateral"]), corners=(True, False, False, True))
        logo = Image.open(os.path.join(UI, id_tema, "logo.png")).resize((28, 28), Image.LANCZOS)
        folha.paste(logo, (x0 + 10, y0 + 10), logo)
        if t.get("faixa"):
            fx = Image.open(os.path.join(UI, id_tema, "faixa.png")).resize((76, 3))
            folha.paste(fx, (x0 + 10, y0 + 44), fx)
        d.rounded_rectangle((x0 + 8, y0 + 56, x0 + 88, y0 + 78), 8, fill=rgba(t["sec"]))
        texto_centro(d, (x0 + 8, y0 + 56, x0 + 88, y0 + 78), "Pausar", fp, rgba(t["sec_texto"]))
        d.rounded_rectangle((x0 + 8, y0 + 84, x0 + 88, y0 + 104), 8, fill=rgba(t["nav_ativo"]))
        d.text((x0 + 16, y0 + 87), "Painel", font=fp, fill=rgba(t["destaque"]))
        for k, nome in enumerate(("Geral", "Glossario", "Avancado")):
            d.text((x0 + 16, y0 + 112 + k * 20), nome, font=fp, fill=rgba(t["mudo"]))
        d.text((x0 + 110, y0 + 12), t["nome"], font=f, fill=rgba(t["texto"]))
        d.rounded_rectangle((x0 + 108, y0 + 40, x0 + larg - 10, y0 + 120), 12, fill=rgba(t["card"]), outline=rgba(t["borda"]) if id_tema == "claro" else None)
        d.text((x0 + 120, y0 + 50), "Traduzidos hoje", font=fp, fill=rgba(t["mudo"]))
        d.text((x0 + 120, y0 + 68), "1.284", font=f, fill=rgba(t["texto"]))
        d.rounded_rectangle((x0 + 120, y0 + 94, x0 + 190, y0 + 112), 9, fill=rgba(mistura(t["card"], t["destaque"], 0.22)))
        texto_centro(d, (x0 + 120, y0 + 94, x0 + 190, y0 + 112), "Faccao", fp, rgba(t["destaque"]))
        d.rounded_rectangle((x0 + 108, y0 + 134, x0 + 200, y0 + 158), 10, fill=rgba(t["destaque"]))
        texto_centro(d, (x0 + 108, y0 + 134, x0 + 200, y0 + 158), "Salvar", fp, rgba(t["destaque_texto"]))
        d.rounded_rectangle((x0 + 210, y0 + 136, x0 + 246, y0 + 156), 10, fill=rgba(t["destaque"]))
        d.ellipse((x0 + 228, y0 + 138, x0 + 244, y0 + 154), fill=(255, 255, 255, 255))
    folha.save(os.path.join(LOGOS, "temas.png"))


if __name__ == "__main__":
    fontes = sys.argv[1] if len(sys.argv) > 1 else None
    gerar_comum(fontes)
    for id_tema in ORDEM:
        gerar_tema(id_tema, TEMAS[id_tema], fontes)
    with open(os.path.join(UI, "temas.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("# id=nome, na ordem do menu (gerado por ferramentas/gerar_ui.py)\n")
        for id_tema in ORDEM:
            f.write(id_tema + "=" + TEMAS[id_tema]["nome"] + "\n")
    if fontes:
        os.makedirs(LOGOS, exist_ok=True)
        previa_temas(fontes)
    print("ok")
