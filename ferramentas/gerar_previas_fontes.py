# Gera as previas da aba Fontes: o nome de cada fonte escrito nela mesma, em branco e com fundo transparente.
# O menu desenha o botao na cor do tema e pinta o nome por cima com a cor do texto do tema (IMGUI nao
# desenha fonte carregada de arquivo, por isso o nome vai como imagem).
# Uso: python ferramentas/gerar_previas_fontes.py
# Le modelo/v_common/Fontes/*.ttf|*.otf e grava modelo/v_common/Fontes/previas/<nome>.png (2x: o menu desenha pela metade).
import glob
import os
from PIL import Image, ImageDraw, ImageFont

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTES = os.path.join(RAIZ, "modelo", "v_common", "Fontes")
SAIDA = os.path.join(FONTES, "previas")
ALTURA = 76      # 38 px no menu
TAMANHO = 38     # ~19 px no menu


def gerar(arq):
    nome = os.path.splitext(os.path.basename(arq))[0]
    fonte = ImageFont.truetype(arq, TAMANHO)
    d = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    l, t, r, b = d.textbbox((0, 0), nome, font=fonte)
    larg = (r - l) + 8
    img = Image.new("RGBA", (larg, ALTURA), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    # centraliza na altura pela caixa das letras
    d.text((4 - l, (ALTURA - (b - t)) / 2 - t), nome, font=fonte, fill=(255, 255, 255, 255))
    img.save(os.path.join(SAIDA, nome + ".png"))


if __name__ == "__main__":
    os.makedirs(SAIDA, exist_ok=True)
    # as previas antigas (botao inteiro pintado, com _hover e _sel) saem
    for velho in glob.glob(os.path.join(SAIDA, "*.png")):
        os.remove(velho)
    arqs = sorted(glob.glob(os.path.join(FONTES, "*.ttf")) + glob.glob(os.path.join(FONTES, "*.otf")))
    for a in arqs:
        gerar(a)
    print(len(arqs), "previas")
