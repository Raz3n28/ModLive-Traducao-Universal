using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Fonte da traducao" (aba Avancado): troca a fonte so dos textos que o mod traduziu.
// Opcoes: original do jogo (padrao), as fontes que vem com o mod (v_common/Fontes), as que o jogador por na
// pasta "fontes" dos dados do mod e algumas do Windows. So aparecem as que tem as letras do idioma escolhido:
// isso e conferido lendo a tabela de letras (cmap) do arquivo, sem carregar a fonte no jogo.
internal static class FonteEscolhida
{
    private sealed class Opcao
    {
        public string Nome;
        public string Caminho;   // "" = original do jogo
    }

    // fontes do Windows que combinam com interface (nome do arquivo, nome no menu)
    private static readonly string[][] DoWindows = new string[][]
    {
        new string[] { "segoeui.ttf", "Segoe UI" },
        new string[] { "bahnschrift.ttf", "Bahnschrift" },
        new string[] { "arial.ttf", "Arial" },
        new string[] { "verdana.ttf", "Verdana" },
        new string[] { "tahoma.ttf", "Tahoma" },
        new string[] { "trebuc.ttf", "Trebuchet MS" },
        new string[] { "calibri.ttf", "Calibri" },
        new string[] { "consola.ttf", "Consolas" }
    };

    private static List<Opcao> todas;
    private static readonly List<Opcao> filtradas = new List<Opcao>();
    private static string idiomaFiltro;
    private static readonly Dictionary<string, bool> cobertura = new Dictionary<string, bool>();

    private static string carregada;        // caminho da fonte carregada agora
    private static TMP_FontAsset asset;
    private static Font fonte;
    public static int Versao;               // muda quando a escolha muda: o LiveDriver reaplica nos textos

    // fonte e material originais de cada texto trocado, para devolver
    private static readonly Dictionary<TMP_Text, KeyValuePair<TMP_FontAsset, Material>> origTmp = new Dictionary<TMP_Text, KeyValuePair<TMP_FontAsset, Material>>();
    private static readonly Dictionary<Text, Font> origUi = new Dictionary<Text, Font>();

    public static string PastaUsuario
    {
        get
        {
            string p = Path.Combine(LiveConfig.Pasta, "fontes");
            try { if (!Directory.Exists(p)) Directory.CreateDirectory(p); } catch { }
            return p;
        }
    }

    public static bool Ligada { get { return !string.IsNullOrEmpty(LiveConfig.FonteTrad); } }

    // ---------------- lista para o menu ----------------

    private static void Montar()
    {
        todas = new List<Opcao>();
        try
        {
            if (!string.IsNullOrEmpty(LiveBoot.PastaMod))
                AddPasta(Path.Combine(Path.Combine(LiveBoot.PastaMod, "v_common"), "Fontes"));
        }
        catch { }
        AddPasta(PastaUsuario);
        try
        {
            string[] caminhos = Font.GetPathsToOSFonts();
            for (int w = 0; w < DoWindows.Length; w++)
            {
                for (int i = 0; i < caminhos.Length; i++)
                {
                    if (!string.Equals(Path.GetFileName(caminhos[i]), DoWindows[w][0], StringComparison.OrdinalIgnoreCase)) continue;
                    Add(DoWindows[w][1] + " (Windows)", caminhos[i]);
                    break;
                }
            }
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "font list"); }
    }

    private static void AddPasta(string pasta)
    {
        try
        {
            if (!Directory.Exists(pasta)) return;
            List<string> arqs = new List<string>(Directory.GetFiles(pasta));
            arqs.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arqs.Count; i++)
            {
                string ext = Path.GetExtension(arqs[i]).ToLowerInvariant();
                if (ext == ".ttf" || ext == ".otf") Add(Path.GetFileNameWithoutExtension(arqs[i]), arqs[i]);
            }
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "font folder"); }
    }

    private static void Add(string nome, string caminho)
    {
        for (int i = 0; i < todas.Count; i++)
            if (string.Equals(todas[i].Nome, nome, StringComparison.OrdinalIgnoreCase)) return;
        Opcao o = new Opcao();
        o.Nome = nome;
        o.Caminho = caminho;
        todas.Add(o);
    }

    // Botao "Atualizar lista" do menu (o jogador pos uma fonte nova na pasta)
    public static void Recarregar()
    {
        todas = null;
        idiomaFiltro = null;
    }

    private static void Filtrar()
    {
        if (todas == null) Montar();
        if (idiomaFiltro == LiveConfig.Idioma) return;
        idiomaFiltro = LiveConfig.Idioma;
        filtradas.Clear();
        Opcao orig = new Opcao();
        orig.Nome = "";
        orig.Caminho = "";
        filtradas.Add(orig);
        string letras = Letras(LiveConfig.Idioma);
        for (int i = 0; i < todas.Count; i++)
            if (Cobre(todas[i].Caminho, letras)) filtradas.Add(todas[i]);
    }

    public static int Quantas
    {
        get { Filtrar(); return filtradas.Count; }
    }

    // posicao da escolha atual na lista filtrada (0 = original); -1 se a escolhida nao serve para o idioma
    public static int Indice
    {
        get
        {
            Filtrar();
            if (!Ligada) return 0;
            for (int i = 1; i < filtradas.Count; i++)
                if (filtradas[i].Caminho == LiveConfig.FonteTrad) return i;
            return -1;
        }
    }

    // aba Fontes do menu: nome e caminho de cada opcao da lista filtrada (0 = original do jogo, nome "")
    public static string Nome(int i)
    {
        Filtrar();
        return i > 0 && i < filtradas.Count ? filtradas[i].Nome : "";
    }

    public static void Escolher(int i)
    {
        Filtrar();
        if (i >= 0 && i < filtradas.Count) Escolher(filtradas[i].Caminho);
    }

    public static void Escolher(string caminho)
    {
        if ((caminho ?? "") == (LiveConfig.FonteTrad ?? "")) return;
        LiveConfig.FonteTrad = caminho ?? "";
        LiveConfig.Marcar();
        Versao++;
    }

    // Idioma trocado para um que a fonte escolhida nao cobre: volta para a original
    public static void ConferirIdioma()
    {
        if (Ligada && Indice < 0) Escolher("");
    }

    // ---------------- letras de cada idioma ----------------

    private const string Cirilico = "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгдежзийклмнопрстуфхцчшщьюя";

    private static string Letras(string idioma)
    {
        string basico = "AZaz09";
        switch (idioma)
        {
            case "pt": return basico + "ãõáéíóúâêôàçÃÕÁÉÍÓÚÂÊÔÀÇ";
            case "es": return basico + "ñÑáéíóúüÁÉÍÓÚ¿¡";
            case "fr": return basico + "àâæçéèêëîïôœùûüÿÀÂÇÉÈÊ";
            case "de": return basico + "äöüßÄÖÜ";
            case "it": return basico + "àèéìòùÀÈÉ";
            case "ru": return Cirilico + "ЁЪЫЭёъыэ";
            case "uk": return Cirilico + "ҐЄІЇґєії";
            case "pl": return basico + "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ";
            case "tr": return basico + "çğıöşüÇĞİÖŞÜ";
            case "nl": return basico + "ëïéÉ";
            case "vi": return basico + "ăâđêôơưĂÂĐÊÔƠƯạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ";
            case "cs": return basico + "áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ";
            case "ro": return basico + "ăâîĂÂÎ";
            case "ja": return "日本語のをはがでしたアイウ";
            case "ko": return "한국어의가는을에서";
            case "zh-CN": return "中文的是在了不和有";
            default: return basico;
        }
    }

    private static bool Cobre(string caminho, string letras)
    {
        string k = caminho + "|" + letras;
        bool r;
        if (cobertura.TryGetValue(k, out r)) return r;
        r = false;
        try
        {
            List<uint[]> faixas = LerCmap(File.ReadAllBytes(caminho));
            if (faixas != null)
            {
                r = true;
                for (int i = 0; i < letras.Length && r; i++)
                    r = Tem(faixas, letras[i]);
            }
        }
        catch { r = false; }
        cobertura[k] = r;
        return r;
    }

    private static bool Tem(List<uint[]> faixas, uint c)
    {
        for (int i = 0; i < faixas.Count; i++)
            if (c >= faixas[i][0] && c <= faixas[i][1]) return true;
        return false;
    }

    // Le a tabela cmap (letras que a fonte tem) de um .ttf/.otf: faixas [inicio, fim] de codigos Unicode.
    // Usa o formato 12 (Unicode completo) se existir, senao o formato 4 (BMP). null se nao entender o arquivo.
    private static List<uint[]> LerCmap(byte[] d)
    {
        if (d.Length < 12) return null;
        int nTab = U16(d, 4);
        int cmap = -1;
        for (int i = 0; i < nTab; i++)
        {
            int r = 12 + i * 16;
            if (r + 16 > d.Length) return null;
            if (d[r] == 'c' && d[r + 1] == 'm' && d[r + 2] == 'a' && d[r + 3] == 'p') { cmap = (int)U32(d, r + 8); break; }
        }
        if (cmap < 0 || cmap + 4 > d.Length) return null;
        int nSub = U16(d, cmap + 2);
        int f4 = -1, f12 = -1;
        for (int i = 0; i < nSub; i++)
        {
            int r = cmap + 4 + i * 8;
            if (r + 8 > d.Length) break;
            int plat = U16(d, r), enc = U16(d, r + 2);
            int off = cmap + (int)U32(d, r + 4);
            if (off + 2 > d.Length) continue;
            int fmt = U16(d, off);
            bool unicode = plat == 0 || (plat == 3 && (enc == 1 || enc == 10));
            if (!unicode) continue;
            if (fmt == 12 && f12 < 0) f12 = off;
            else if (fmt == 4 && f4 < 0) f4 = off;
        }
        List<uint[]> faixas = new List<uint[]>();
        if (f12 >= 0)
        {
            uint n = U32(d, f12 + 12);
            for (uint g = 0; g < n; g++)
            {
                int r = f12 + 16 + (int)g * 12;
                if (r + 12 > d.Length) break;
                faixas.Add(new uint[] { U32(d, r), U32(d, r + 4) });
            }
            return faixas;
        }
        if (f4 >= 0)
        {
            int segs = U16(d, f4 + 6) / 2;
            int fins = f4 + 14, inicios = fins + segs * 2 + 2;
            int deltas = inicios + segs * 2, offs = deltas + segs * 2;
            for (int s = 0; s < segs; s++)
            {
                if (offs + s * 2 + 2 > d.Length) break;
                uint fim = (uint)U16(d, fins + s * 2), ini = (uint)U16(d, inicios + s * 2);
                if (ini == 0xFFFF) continue;
                int delta = (short)U16(d, deltas + s * 2);
                int ro = U16(d, offs + s * 2);
                if (ro == 0)
                {
                    // sem tabela de glifos: todo codigo da faixa tem glifo, menos o que cai no glifo 0
                    faixas.Add(new uint[] { ini, fim });
                    continue;
                }
                // com tabela: confere codigo por codigo (glifo 0 = a letra nao existe)
                uint a = 0; bool aberto = false;
                for (uint c = ini; c <= fim; c++)
                {
                    int pos = offs + s * 2 + ro + (int)(c - ini) * 2;
                    int gl = pos + 2 <= d.Length ? U16(d, pos) : 0;
                    if (gl != 0) gl = (gl + delta) & 0xFFFF;
                    if (gl != 0 && !aberto) { a = c; aberto = true; }
                    else if (gl == 0 && aberto) { faixas.Add(new uint[] { a, c - 1 }); aberto = false; }
                }
                if (aberto) faixas.Add(new uint[] { a, fim });
            }
            return faixas;
        }
        return null;
    }

    private static int U16(byte[] d, int o) { return (d[o] << 8) | d[o + 1]; }
    private static uint U32(byte[] d, int o) { return ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3]; }

    // ---------------- trocar a fonte nos textos ----------------

    // carrega a fonte escolhida (so na primeira vez que precisa); false se nao deu
    private static bool Carregar()
    {
        string cam = LiveConfig.FonteTrad;
        if (string.IsNullOrEmpty(cam)) return false;
        if (cam == carregada) return asset != null;
        carregada = cam;
        asset = null;
        fonte = null;
        try
        {
            if (!File.Exists(cam)) cam = Achar(Path.GetFileName(cam));
            if (cam == null) { LiveLog.Aviso("lg_fonte_trad_falta", carregada); return false; }
            fonte = new Font(cam);
            asset = TMP_FontAsset.CreateFontAsset(fonte);
            if (asset == null) { LiveLog.Aviso("lg_fonte_trad_falta", cam); return false; }
            asset.name = "LIVE_Trad_" + Path.GetFileNameWithoutExtension(cam);
            FonteReserva.VerificarFonte(asset);   // letras que faltarem (ex.: chines) vem da fonte reserva
            LiveLog.Info("lg_fonte_trad", Path.GetFileNameWithoutExtension(cam));
            return true;
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_fonte", e, cam);
            asset = null;
            return false;
        }
    }

    // a pasta do mod muda (ex.: copia local -> Workshop): procura o arquivo pelo nome nas pastas de fontes
    private static string Achar(string arquivo)
    {
        if (todas == null) Montar();
        for (int i = 0; i < todas.Count; i++)
            if (string.Equals(Path.GetFileName(todas[i].Caminho), arquivo, StringComparison.OrdinalIgnoreCase)) return todas[i].Caminho;
        return null;
    }

    // texto que acabou de receber a traducao
    public static void Trocar(TMP_Text t)
    {
        try
        {
            if (!t) return;
            if (!Ligada) { Devolver(t); return; }
            if (!Carregar() || t.font == asset) return;
            if (!origTmp.ContainsKey(t))
            {
                if (origTmp.Count > 3000) LimparMortos();
                origTmp[t] = new KeyValuePair<TMP_FontAsset, Material>(t.font, t.fontSharedMaterial);
            }
            t.font = asset;
            t.fontSharedMaterial = asset.material;
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "set font"); }
    }

    // Texto do Unity antigo (UI.Text) fica com a fonte do jogo: no teste, uma Font carregada de arquivo
    // nao desenhou nada no texto antigo (o menu IMGUI, que usa o mesmo desenho, ficou com os botoes vazios).
    public static void Trocar(Text t)
    {
        Devolver(t);
    }

    public static void Devolver(TMP_Text t)
    {
        KeyValuePair<TMP_FontAsset, Material> o;
        if (!t || !origTmp.TryGetValue(t, out o)) return;
        origTmp.Remove(t);
        try
        {
            if (o.Key) t.font = o.Key;
            if (o.Value) t.fontSharedMaterial = o.Value;
        }
        catch { }
    }

    public static void Devolver(Text t)
    {
        Font o;
        if (!t || !origUi.TryGetValue(t, out o)) return;
        origUi.Remove(t);
        try { if (o) t.font = o; } catch { }
    }

    // volta a fonte original em todo texto trocado (escolheu "original" ou a tela voltou ao original)
    public static void DevolverTodas()
    {
        List<TMP_Text> a = new List<TMP_Text>(origTmp.Keys);
        for (int i = 0; i < a.Count; i++) Devolver(a[i]);
        List<Text> b = new List<Text>(origUi.Keys);
        for (int i = 0; i < b.Count; i++) Devolver(b[i]);
        origTmp.Clear();
        origUi.Clear();
    }

    private static void LimparMortos()
    {
        List<TMP_Text> a = new List<TMP_Text>();
        foreach (TMP_Text t in origTmp.Keys) if (!t) a.Add(t);
        for (int i = 0; i < a.Count; i++) origTmp.Remove(a[i]);
        List<Text> b = new List<Text>();
        foreach (Text t in origUi.Keys) if (!t) b.Add(t);
        for (int i = 0; i < b.Count; i++) origUi.Remove(b[i]);
    }
}
