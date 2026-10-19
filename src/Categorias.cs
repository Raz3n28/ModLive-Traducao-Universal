using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

internal sealed class CategoriaInfo
{
    public int Id;
    public string Nome = "";        // so nas categorias criadas; as padrao usam o nome do LiveTxt
    public int Cor;                 // indice da paleta (so nas criadas)
    public string Palavras = "";    // palavras-chave (categoria automatica, vale em qualquer modo)
    public string Instrucao = "";   // instrucao para a IA (so no modo com chave)

    // palavras-chave ja separadas (cache)
    public string[] Chaves;
    public string ChavesDe;

    public bool Padrao { get { return Id < Categorias.NumBase; } }
}

// Categorias do glossario: as 9 padrao (ids 0-8) + ate 8 criadas pelo jogador (ids 9 em diante, nunca reaproveitados).
// Cada uma tem palavras-chave (categoria automatica, sem gastar token) e uma instrucao para a IA.
// Arquivo unico, vale para todos os idiomas: categorias.txt
//   id<TAB>nome<TAB>cor<TAB>palavras<TAB>instrucao        e uma linha  #proximo<TAB>N
internal static class Categorias
{
    public const int NumBase = 9;          // 0 Outros, 1 Faccao, 2 Item, 3 Arma, 4 Habilidade, 5 Estacao, 6 Nome, 7 Botao, 8 Titulo
    public const int LimiteId = 64;        // ids vao de 0 a 63
    public const int MaxNovas = 8;
    public const int MaxNome = 20;
    public const int MaxPalavras = 300;
    public const int MaxInstrucao = 150;

    // ordem em que aparecem nos filtros (Outros por ultimo), depois as criadas
    private static readonly int[] OrdemPadrao = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 0 };
    // ordem em que as palavras-chave das padrao sao testadas (a mesma de antes: Item por ultimo)
    private static readonly int[] OrdemBusca = new int[] { 1, 3, 4, 5, 2, 6, 7, 8 };

    private static readonly Color[] Paleta = new Color[]
    {
        new Color(0.40f, 0.85f, 0.85f, 1f),
        new Color(0.95f, 0.55f, 0.75f, 1f),
        new Color(0.70f, 0.90f, 0.40f, 1f),
        new Color(0.95f, 0.65f, 0.30f, 1f),
        new Color(0.55f, 0.65f, 1.00f, 1f),
        new Color(0.85f, 0.80f, 0.55f, 1f),
        new Color(0.60f, 0.95f, 0.75f, 1f),
        new Color(0.85f, 0.60f, 0.95f, 1f)
    };

    private static readonly Dictionary<int, CategoriaInfo> mapa = new Dictionary<int, CategoriaInfo>();
    private static readonly List<CategoriaInfo> lista = new List<CategoriaInfo>();
    private static int proximoId = NumBase;
    private static bool sujo;
    private static int[] ordemCache;

    // sobe a cada mudanca
    public static int Versao;

    private static string Arquivo { get { return Path.Combine(LiveConfig.Pasta, "categorias.txt"); } }

    public static int NumNovas { get { return lista.Count - NumBase; } }

    // ---------------- carregar / salvar ----------------

    public static void Carregar()
    {
        mapa.Clear();
        lista.Clear();
        proximoId = NumBase;
        for (int i = 0; i < NumBase; i++)
        {
            CategoriaInfo c = new CategoriaInfo();
            c.Id = i;
            c.Palavras = PalavrasPadrao(i);
            c.Instrucao = InstrucaoPadrao(i);
            Adicionar(c);
        }

        string arq = Arquivo;
        if (File.Exists(arq))
        {
            try
            {
                string[] linhas = File.ReadAllLines(arq, Encoding.UTF8);
                // arquivo de antes das instrucoes padrao (sem a linha #instrucoes): instrucao vazia numa
                // categoria padrao vira a instrucao padrao. Depois disso, vazio e escolha do jogador.
                bool temMarca = false;
                for (int i = 0; i < linhas.Length; i++)
                    if (linhas[i].StartsWith("#instrucoes", StringComparison.Ordinal)) temMarca = true;
                for (int i = 0; i < linhas.Length; i++)
                {
                    string[] p = linhas[i].Split('\t');
                    if (p.Length >= 2 && p[0] == "#proximo")
                    {
                        int pr;
                        if (int.TryParse(p[1], out pr) && pr > proximoId && pr <= LimiteId) proximoId = pr;
                        continue;
                    }
                    if (p.Length < 5) continue;
                    int id;
                    if (!int.TryParse(p[0], out id) || id < 0 || id >= LimiteId) continue;
                    string nome = Desescapar(p[1]);
                    int cor;
                    int.TryParse(p[2], out cor);
                    string pal = Cortar(Desescapar(p[3]), MaxPalavras);
                    string ins = Cortar(Desescapar(p[4]), MaxInstrucao);
                    CategoriaInfo c;
                    if (id < NumBase)
                    {
                        c = mapa[id];
                    }
                    else
                    {
                        if (mapa.ContainsKey(id) || NumNovas >= MaxNovas || nome.Trim().Length == 0) continue;
                        c = new CategoriaInfo();
                        c.Id = id;
                        c.Nome = Cortar(nome.Trim(), MaxNome);
                        c.Cor = cor < 0 ? 0 : cor;
                        Adicionar(c);
                        if (id >= proximoId) proximoId = id + 1;
                    }
                    c.Palavras = pal;
                    c.Instrucao = (!temMarca && id < NumBase && ins.Trim().Length == 0) ? InstrucaoPadrao(id) : ins;
                }
            }
            catch (Exception e)
            {
                LiveLog.Erro("le_ler", e, LiveLog.T("it_cat"));
            }
        }
        Versao++;
        ordemCache = null;
        LiveLog.Info("lg_categorias", lista.Count);
    }

    private static void Adicionar(CategoriaInfo c)
    {
        mapa[c.Id] = c;
        lista.Add(c);
    }

    public static void Marcar()
    {
        sujo = true;
        Versao++;
        ordemCache = null;
    }

    public static void SalvarSeSujo()
    {
        if (sujo) Salvar();
    }

    public static void Salvar()
    {
        sujo = false;
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("#proximo\t").Append(proximoId).Append('\n');
            sb.Append("#instrucoes\t1\n");
            for (int i = 0; i < lista.Count; i++)
            {
                CategoriaInfo c = lista[i];
                sb.Append(c.Id).Append('\t')
                  .Append(Escapar(c.Padrao ? "" : c.Nome)).Append('\t')
                  .Append(c.Cor).Append('\t')
                  .Append(Escapar(c.Palavras ?? "")).Append('\t')
                  .Append(Escapar(c.Instrucao ?? "")).Append('\n');
            }
            File.WriteAllText(Arquivo, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_gravar", e, LiveLog.T("it_cat"));
        }
    }

    // ---------------- consultas ----------------

    public static CategoriaInfo Obter(int id)
    {
        CategoriaInfo c;
        return mapa.TryGetValue(id, out c) ? c : null;
    }

    public static bool Existe(int id)
    {
        return mapa.ContainsKey(id);
    }

    // ids na ordem dos filtros e das listas
    public static int[] Ordem()
    {
        if (ordemCache != null) return ordemCache;
        List<int> r = new List<int>();
        for (int i = 0; i < OrdemPadrao.Length; i++) r.Add(OrdemPadrao[i]);
        for (int i = NumBase; i < lista.Count; i++) r.Add(lista[i].Id);
        ordemCache = r.ToArray();
        return ordemCache;
    }

    public static string Nome(int id)
    {
        CategoriaInfo c = Obter(id);
        if (c == null) return LiveTxt.T("cat0");
        if (c.Padrao) return LiveTxt.T("cat" + id);
        return c.Nome.Length > 0 ? c.Nome : "?";
    }

    // para o prompt da IA (sempre em ingles nas padrao)
    public static string NomeIngles(int id)
    {
        CategoriaInfo c = Obter(id);
        if (c == null) return "Other";
        if (c.Padrao) return LiveTxt.TIdioma("cat" + id, 1);
        return c.Nome;
    }

    public static Color Cor(int id)
    {
        CategoriaInfo c = Obter(id);
        if (c != null && !c.Padrao) return Paleta[c.Cor % Paleta.Length];
        switch (id)
        {
            case 1: return new Color(0.49f, 0.83f, 0.99f, 1f);
            case 2: return new Color(0.99f, 0.73f, 0.45f, 1f);
            case 3: return new Color(0.99f, 0.62f, 0.62f, 1f);
            case 4: return new Color(0.77f, 0.71f, 0.99f, 1f);
            case 5: return new Color(0.99f, 0.83f, 0.30f, 1f);
            case 6: return new Color(0.65f, 0.71f, 0.99f, 1f);
            case 7: return new Color(0.53f, 0.94f, 0.67f, 1f);
            case 8: return new Color(0.94f, 0.67f, 0.99f, 1f);
            default: return new Color(0.75f, 0.75f, 0.78f, 1f);
        }
    }

    // ---------------- criar / apagar / restaurar ----------------

    // devolve o id da nova categoria, ou -1 (erro = chave do texto de erro no LiveTxt)
    public static int Criar(string nome, string palavras, string instrucao, out string erro)
    {
        erro = null;
        string n = (nome ?? "").Trim();
        if (n.Length > MaxNome) n = n.Substring(0, MaxNome);
        if (NumNovas >= MaxNovas || proximoId >= LimiteId)
        {
            erro = "cat_limite";
            return -1;
        }
        if (n.Length == 0)
        {
            erro = "cat_erro_nome";
            return -1;
        }
        for (int i = 0; i < lista.Count; i++)
        {
            if (string.Compare(Nome(lista[i].Id), n, StringComparison.OrdinalIgnoreCase) == 0)
            {
                erro = "cat_erro_nome";
                return -1;
            }
        }
        CategoriaInfo c = new CategoriaInfo();
        c.Id = proximoId++;
        c.Nome = n;
        c.Cor = c.Id - NumBase;
        c.Palavras = Cortar((palavras ?? "").Trim(), MaxPalavras);
        c.Instrucao = Cortar((instrucao ?? "").Trim(), MaxInstrucao);
        Adicionar(c);
        Marcar();
        return c.Id;
    }

    // so as criadas. Os termos dela voltam para "Outros".
    public static void Apagar(int id)
    {
        CategoriaInfo c = Obter(id);
        if (c == null || c.Padrao) return;
        mapa.Remove(id);
        lista.Remove(c);
        Glossario.MoverCategoria(id, 0);
        Marcar();
    }

    // padrao: volta as palavras-chave originais e limpa a instrucao. Criada: limpa os dois campos.
    public static void Restaurar(int id)
    {
        CategoriaInfo c = Obter(id);
        if (c == null) return;
        c.Palavras = c.Padrao ? PalavrasPadrao(id) : "";
        c.Instrucao = c.Padrao ? InstrucaoPadrao(id) : "";
        Marcar();
    }

    // ---------------- categoria automatica (palavras-chave) ----------------

    // modo 0 = todas (criadas primeiro), 1 = so as criadas, 2 = so as padrao. Devolve o id ou -1.
    public static int PorTexto(string texto, int modo)
    {
        if (string.IsNullOrEmpty(texto)) return -1;
        string baixo = texto.ToLowerInvariant();
        HashSet<string> palavras = PalavrasDe(baixo);
        if (modo != 2)
        {
            for (int i = NumBase; i < lista.Count; i++)
                if (Casa(lista[i], baixo, palavras)) return lista[i].Id;
        }
        if (modo != 1)
        {
            for (int i = 0; i < OrdemBusca.Length; i++)
            {
                CategoriaInfo c = Obter(OrdemBusca[i]);
                if (c != null && Casa(c, baixo, palavras)) return c.Id;
            }
        }
        return -1;
    }

    private static bool Casa(CategoriaInfo c, string baixo, HashSet<string> palavras)
    {
        string[] chaves = Chaves(c);
        for (int i = 0; i < chaves.Length; i++)
        {
            string k = chaves[i];
            if (SoLetrasAscii(k))
            {
                if (palavras.Contains(k)) return true;
            }
            else if (baixo.IndexOf(k, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static string[] Chaves(CategoriaInfo c)
    {
        string pal = c.Palavras ?? "";
        if (c.Chaves != null && c.ChavesDe == pal) return c.Chaves;
        List<string> r = new List<string>();
        string[] partes = pal.Split(new char[] { ',', ';', '\uFF0C', '\u3001', '\n' });
        for (int i = 0; i < partes.Length && r.Count < 80; i++)
        {
            string k = partes[i].Trim().ToLowerInvariant();
            if (k.Length > 0) r.Add(k);
        }
        c.Chaves = r.ToArray();
        c.ChavesDe = pal;
        return c.Chaves;
    }

    private static bool SoLetrasAscii(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch < 'a' || ch > 'z') return false;
        }
        return s.Length > 0;
    }

    // palavras do texto (e a forma sem "s" do plural)
    private static HashSet<string> PalavrasDe(string baixo)
    {
        HashSet<string> set = new HashSet<string>();
        int n = baixo.Length;
        int i = 0;
        while (i < n)
        {
            while (i < n && !char.IsLetter(baixo[i])) i++;
            int ini = i;
            while (i < n && char.IsLetter(baixo[i])) i++;
            if (i > ini)
            {
                string w = baixo.Substring(ini, i - ini);
                set.Add(w);
                if (w.Length > 3 && w[w.Length - 1] == 's') set.Add(w.Substring(0, w.Length - 1));
            }
        }
        return set;
    }

    // ---------------- prompt da IA ----------------

    public static string TextoPrompt()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < lista.Count; i++)
        {
            CategoriaInfo c = lista[i];
            string ins = (c.Instrucao ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
            if (ins.Length == 0) continue;
            if (sb.Length > 1500) break;
            if (sb.Length == 0)
                sb.Append("Term categories. When a short string (a name, item, button, title...) clearly belongs to one of these categories, follow that category's instruction (the hard rules above always win):");
            sb.Append("\n- ").Append(NomeIngles(c.Id)).Append(": ").Append(ins);
        }
        return sb.ToString();
    }

    // ---------------- padroes ----------------

    // instrucao de fabrica para a IA (so modo com chave). Em ingles porque vai no pedido junto com as regras fixas,
    // e vale para qualquer idioma de destino. Maximo MaxInstrucao (150) letras cada.
    public static string InstrucaoPadrao(int id)
    {
        switch (id)
        {
            case 1: return "Faction names: translate generic words (alliance, guild, army), keep invented proper names. Same faction = same translation.";
            case 2: return "Item names: short noun phrase, no articles. Keep model codes, numbers and invented brand names unchanged.";
            case 3: return "Weapon names: concise, use the usual military/sci-fi terms of the target language. Keep model codes (e.g. MK-II) unchanged.";
            case 4: return "Skill names: short and punchy, like a game ability name. No full sentences.";
            case 5: return "Station and place names: keep the proper name, translate only the generic part (station, port, base, dock).";
            case 6: return "Names of people and ships: do not translate the meaning. Keep Latin names as is; romanize others (e.g. Chinese to pinyin).";
            case 7: return "Button labels: as short as possible (1-2 words). Use the imperative/infinitive usual for game buttons in the target language.";
            case 8: return "Window and section titles: short, no final period.";
            default: return "";
        }
    }

    // palavras-chave de fabrica das categorias padrao (as mesmas que antes eram fixas no codigo)
    public static string PalavrasPadrao(int id)
    {
        switch (id)
        {
            case 1: return "faction, alliance, garrison, school, guild, corps, union, front, army, empire, republic, federation, clan, legion, \u9635\u8425, \u8054\u76DF, \u52BF\u529B, \u519B\u56E2, \u5B66\u9662, \u516C\u4F1A";
            case 2: return "kit, module, armor, shield, engine, reactor, ore, crystal, fuel, pack, cargo, container, equipment, item, tool, chip, battery, core, component, part, material, \u88C5\u5907, \u6A21\u5757, \u6750\u6599, \u9053\u5177, \u7269\u54C1, \u96F6\u4EF6, \u71C3\u6599, \u7535\u6C60";
            case 3: return "rifle, gun, cannon, pistol, blade, sword, missile, torpedo, laser, railgun, turret, shotgun, grenade, weapon, bomb, lance, \u67AA, \u70AE, \u5251, \u5BFC\u5F39, \u9C7C\u96F7, \u6FC0\u5149, \u6B66\u5668";
            case 4: return "skill, ability, boost, overclock, passive, talent, buff, aura, ultimate, \u6280\u80FD, \u5929\u8D4B, \u88AB\u52A8, \u589E\u76CA";
            case 5: return "station, port, dock, hangar, base, outpost, colony, terminal, harbor, shipyard, \u7A7A\u95F4\u7AD9, \u57FA\u5730, \u636E\u70B9, \u6B96\u6C11, \u7801\u5934, \u673A\u5E93";
            default: return "";
        }
    }

    // ---------------- util ----------------

    private static string Cortar(string s, int max)
    {
        if (s == null) return "";
        return s.Length <= max ? s : s.Substring(0, max);
    }

    private static string Escapar(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private static string Desescapar(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char p = s[++i];
                if (p == 't') sb.Append('\t');
                else if (p == 'n') sb.Append('\n');
                else if (p == 'r') sb.Append('\r');
                else sb.Append(p);
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
