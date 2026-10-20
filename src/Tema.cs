using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Tema de cores do menu F8. Cada tema e uma pasta em v_common\UI (PNGs + tema.txt com as cores),
// gerada por ferramentas\gerar_ui.py. Sem a pasta, valem as cores do Teal (padrao) escritas aqui.
internal static class Tema
{
    public static Color Fundo, Lateral, Card, Borda, BordaHover, Texto, Mudo, Destaque, DestaqueTexto, NavAtivo, Sec, SecTexto, Ok, Aviso, Erro, Campo;
    public static bool Claro;
    // muda a cada troca de tema: o menu refaz os estilos
    public static int Versao;

    private static readonly List<string> ids = new List<string>();
    private static readonly List<string> nomes = new List<string>();
    private static string carregado;

    public static int Quantos { get { Listar(); return ids.Count; } }
    public static string Id(int i) { Listar(); return ids[i]; }
    public static string Nome(int i) { Listar(); return nomes[i]; }

    public static int Indice
    {
        get
        {
            Listar();
            int i = ids.IndexOf(LiveConfig.Tema);
            return i < 0 ? 0 : i;
        }
    }

    public static void Escolher(int i)
    {
        Listar();
        if (i < 0 || i >= ids.Count || ids[i] == LiveConfig.Tema) return;
        // as cores e os PNGs trocam no proximo quadro (Garantir), fora do desenho atual
        LiveConfig.Tema = ids[i];
        LiveConfig.Marcar();
    }

    // carrega as cores do tema do config (so quando muda)
    public static void Garantir()
    {
        string id = LiveConfig.Tema;
        if (carregado == id) return;
        carregado = id;
        Padrao();
        string arq = Png.Base != null ? Path.Combine(Path.Combine(Png.Base, id), "tema.txt") : null;
        try
        {
            if (arq != null && File.Exists(arq))
            {
                foreach (string linha in File.ReadAllLines(arq))
                {
                    int sep = linha.IndexOf('=');
                    if (sep <= 0 || linha.StartsWith("#")) continue;
                    Ler(linha.Substring(0, sep).Trim(), linha.Substring(sep + 1).Trim());
                }
            }
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_parte", e, "theme " + id);
        }
        Png.Limpar();
        Versao++;
    }

    private static void Listar()
    {
        if (ids.Count > 0) return;
        string arq = Png.Base != null ? Path.Combine(Png.Base, "temas.txt") : null;
        try
        {
            if (arq != null && File.Exists(arq))
            {
                foreach (string linha in File.ReadAllLines(arq))
                {
                    int sep = linha.IndexOf('=');
                    if (sep <= 0 || linha.StartsWith("#")) continue;
                    ids.Add(linha.Substring(0, sep).Trim());
                    nomes.Add(linha.Substring(sep + 1).Trim());
                }
            }
        }
        catch { }
        if (ids.Count == 0) { ids.Add("teal"); nomes.Add("Teal"); }
    }

    private static void Ler(string k, string v)
    {
        if (k == "claro") { Claro = v == "1"; return; }
        // "#rrggbb" (sem ColorUtility: a DLL cortada do jogo pode nao ter)
        string h = v.TrimStart('#');
        int rgb;
        if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out rgb)) return;
        Color c = Hex(rgb);
        switch (k)
        {
            case "fundo": Fundo = c; break;
            case "lateral": Lateral = c; break;
            case "card": Card = c; break;
            case "borda": Borda = c; break;
            case "borda_hover": BordaHover = c; break;
            case "texto": Texto = c; break;
            case "mudo": Mudo = c; break;
            case "destaque": Destaque = c; break;
            case "destaque_texto": DestaqueTexto = c; break;
            case "nav_ativo": NavAtivo = c; break;
            case "sec": Sec = c; break;
            case "sec_texto": SecTexto = c; break;
            case "ok": Ok = c; break;
            case "aviso": Aviso = c; break;
            case "erro": Erro = c; break;
            case "campo": Campo = c; break;
        }
    }

    private static void Padrao()
    {
        Fundo = Hex(0x101418); Lateral = Hex(0x0b0e11); Card = Hex(0x161b21); Borda = Hex(0x262e37); BordaHover = Hex(0x3a4652);
        Texto = Hex(0xe6edf3); Mudo = Hex(0x8b98a5); Destaque = Hex(0x2dd4bf); DestaqueTexto = Hex(0x04201c); NavAtivo = Hex(0x12302d);
        Sec = Hex(0xf59e0b); SecTexto = Hex(0x231500); Ok = Hex(0x86efac); Aviso = Hex(0xfcd34d); Erro = Hex(0xfca5a5); Campo = Hex(0x0b0e11);
        Claro = false;
    }

    public static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
