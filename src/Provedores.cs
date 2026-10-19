using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

// Monta o pedido HTTP de cada tipo de provedor e le a resposta.
internal static class Provedores
{
    public static bool Configurado(Provedor p, out string motivo)
    {
        motivo = null;
        if (p.Tipo == 0) return true;
        if (string.IsNullOrEmpty(LiveConfig.Url(p.Id))) { motivo = LiveTxt.T("m_url"); return false; }
        if (string.IsNullOrEmpty(LiveConfig.Modelo(p.Id))) { motivo = LiveTxt.T("m_modelo"); return false; }
        if (p.PrecisaChave && string.IsNullOrEmpty(LiveConfig.Chave(p.Id))) { motivo = LiveTxt.T("m_chave"); return false; }
        return true;
    }

    // ep = qual endereco do Google usar (so vale no modo gratis; ver GoogleEndpoints)
    public static UnityWebRequest Montar(Provedor p, List<string> textos, int ep)
    {
        if (p.Tipo == 0) return MontarGoogle(textos, ep);
        if (p.Tipo == 2) return MontarClaude(p, textos);
        return MontarOpenAI(p, textos);
    }

    // devolve null se a resposta nao for utilizavel
    public static List<string> Interpretar(Provedor p, List<string> textos, string corpo, int ep)
    {
        int esperado = textos.Count;
        try
        {
            if (string.IsNullOrEmpty(corpo)) return null;
            if (p.Tipo == 0) return InterpretarGoogle(corpo, textos, ep);
            object raiz = Json.Parse(corpo);
            string conteudo;
            if (p.Tipo == 2) conteudo = Json.Obter(raiz, "content", 0, "text") as string;
            else conteudo = Json.Obter(raiz, "choices", 0, "message", "content") as string;
            List<string> lista = ListaDeTextos(conteudo);
            if (lista == null || lista.Count != esperado) return null;
            return lista;
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_parte", e, "read response");
            return null;
        }
    }

    // ---------------- Google Tradutor (gratis, sem chave) ----------------

    // Tres enderecos do Google, cada um com limite separado. O motor troca de um para outro quando um da 429.
    //   0 = gtx (translate.googleapis.com)      - textos um por linha, GET
    //   1 = dict-chrome-ex (clients5.google.com) - um "q=" por texto, GET (o da extensao do Chrome)
    //   2 = site (translate.google.com batchexecute) - igual ao XUnity "Google v2", POST
    public const int GoogleEndpoints = 3;
    private static readonly string[] nomesGoogle = new string[] { "gtx", "dict-chrome-ex", "site (batchexecute)" };
    private const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36";
    private static readonly System.Random sorteio = new System.Random();
    private static long fsid;
    private static long reqId;

    public static string NomeGoogle(int ep)
    {
        return ep >= 0 && ep < nomesGoogle.Length ? nomesGoogle[ep] : "?";
    }

    private static UnityWebRequest MontarGoogle(List<string> textos, int ep)
    {
        if (ep == 1) return MontarDict(textos);
        if (ep == 2) return MontarSite(textos);
        return MontarGtx(textos);
    }

    private static List<string> InterpretarGoogle(string corpo, List<string> textos, int ep)
    {
        if (ep == 1) return InterpretarDict(corpo, textos);
        if (ep == 2) return SepararLinhas(TextoSite(corpo), textos);
        object raiz = Json.Parse(corpo);
        List<object> segs = Json.Obter(raiz, 0) as List<object>;
        return SepararLinhas(JuntarSegmentos(segs), textos);
    }

    // textos um por linha (cada texto pode ter mais de uma linha)
    private static string JuntarLinhas(List<string> textos)
    {
        StringBuilder junto = new StringBuilder();
        for (int i = 0; i < textos.Count; i++)
        {
            if (i > 0) junto.Append('\n');
            junto.Append(SemCR(textos[i]));
        }
        return junto.ToString();
    }

    private static void Cab(UnityWebRequest r, string nome, string valor)
    {
        try { r.SetRequestHeader(nome, valor); } catch { }
    }

    // ---- 0: gtx ----
    private static UnityWebRequest MontarGtx(List<string> textos)
    {
        string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&dt=t&tl="
            + Json.UrlEsc(LiveConfig.Idioma) + "&q=" + Json.UrlEsc(JuntarLinhas(textos));
        UnityWebRequest r = UnityWebRequest.Get(url);
        Cab(r, "User-Agent", UA);
        r.timeout = 30;
        return r;
    }

    // ---- 1: dict-chrome-ex ----
    private static UnityWebRequest MontarDict(List<string> textos)
    {
        StringBuilder url = new StringBuilder("https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=");
        url.Append(Json.UrlEsc(LiveConfig.Idioma));
        for (int i = 0; i < textos.Count; i++) url.Append("&q=").Append(Json.UrlEsc(SemCR(textos[i])));
        UnityWebRequest r = UnityWebRequest.Get(url.ToString());
        Cab(r, "User-Agent", UA);
        r.timeout = 30;
        return r;
    }

    // Resposta: uma lista com um item por texto. Cada item e "traducao" ou ["traducao","idioma de origem"].
    // Com um texto so, as vezes vem ["traducao","idioma"] direto.
    private static List<string> InterpretarDict(string corpo, List<string> textos)
    {
        List<object> l = Json.Parse(corpo) as List<object>;
        if (l == null || l.Count == 0) return null;
        List<string> r = new List<string>();
        if (textos.Count == 1)
        {
            string u = ItemDict(l[0]);
            if (u == null || u.Trim().Length == 0) return null;
            r.Add(u.Trim());
            return r;
        }
        if (l.Count != textos.Count) return null;
        for (int i = 0; i < l.Count; i++)
        {
            string t = ItemDict(l[i]);
            if (t == null || t.Trim().Length == 0) return null;
            r.Add(t.Trim());
        }
        return r;
    }

    private static string ItemDict(object item)
    {
        string s = item as string;
        if (s != null) return s;
        return Json.Obter(item, 0) as string;
    }

    // ---- 2: site (batchexecute), copiado do XUnity GoogleTranslateEndpointV2 ----
    private static UnityWebRequest MontarSite(List<string> textos)
    {
        if (fsid == 0)
        {
            fsid = ((long)sorteio.Next(100000000, 999999999) * 1000000000L) + sorteio.Next(0, 999999999);
            reqId = sorteio.Next(1000, 99999);
        }
        string interno = "[[" + Json.Esc(JuntarLinhas(textos)) + ",\"auto\"," + Json.Esc(LiveConfig.Idioma) + ",true],[null]]";
        string freq = "[[[\"MkEWBc\"," + Json.Esc(interno) + ",null,\"generic\"]]]";
        string url = "https://translate.google.com/_/TranslateWebserverUi/data/batchexecute?rpcids=MkEWBc"
            + "&f.sid=" + fsid
            + "&bl=" + Json.UrlEsc("boq_translate-webserver_20210323.10_p0")
            + "&hl=en-US&soc-app=1&soc-platform=1&soc-device=1"
            + "&_reqid=" + reqId
            + "&rt=c";
        reqId += 100000;
        UnityWebRequest r = new UnityWebRequest(url, "POST");
        r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("f.req=" + Json.UrlEsc(freq) + "&"));
        r.downloadHandler = new DownloadHandlerBuffer();
        Cab(r, "Content-Type", "application/x-www-form-urlencoded;charset=UTF-8");
        Cab(r, "User-Agent", UA);
        Cab(r, "Accept", "*/*");
        Cab(r, "X-Same-Domain", "1");
        Cab(r, "Referer", "https://translate.google.com/");
        Cab(r, "Origin", "https://translate.google.com");
        r.timeout = 30;
        return r;
    }

    // A resposta comeca com )]}' e um numero; o primeiro bloco [["wrb.fr","MkEWBc","<json em texto>",...]] tem a traducao.
    private static string TextoSite(string corpo)
    {
        int ini = corpo.IndexOf("[[\"wrb.fr\"", StringComparison.Ordinal);
        if (ini < 0) return null;
        object bloco = Json.Parse(corpo.Substring(ini)); // o leitor para no fim do primeiro bloco
        string interno = Json.Obter(bloco, 0, 2) as string;
        if (string.IsNullOrEmpty(interno)) return null;
        object arr = Json.Parse(interno);
        List<object> segs = Json.Obter(arr, 1, 0, 0, 5) as List<object>;
        if (segs == null) return Json.Obter(arr, 1, 0, 0, 0) as string;
        return JuntarSegmentos(segs);
    }

    // ---- comum aos enderecos que devolvem pedacos (frases) ----

    // Junta os pedacos. Se nenhum dos dois lados tiver espaco/quebra na emenda, poe uma quebra de linha
    // (como o XUnity). O endereco "site" manda a quebra no COMECO do pedaco seguinte ("\nNo dependencies"),
    // entao olhar so o fim do anterior criava uma linha em branco a mais e o lote sempre falhava.
    private static string JuntarSegmentos(List<object> segs)
    {
        if (segs == null) return null;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < segs.Count; i++)
        {
            string parte = Json.Obter(segs[i], 0) as string;
            if (string.IsNullOrEmpty(parte)) continue;
            if (sb.Length > 0 && !char.IsWhiteSpace(sb[sb.Length - 1]) && !char.IsWhiteSpace(parte[0])) sb.Append('\n');
            sb.Append(parte);
        }
        return sb.ToString();
    }

    private static string SemCR(string s)
    {
        return s.IndexOf('\r') < 0 ? s : s.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    // Separa por linha: cada texto original leva tantas linhas quantas tinha.
    // Se a conta nao bater, devolve null (o motor entao tenta de novo um texto por vez).
    private static List<string> SepararLinhas(string tudo, List<string> textos)
    {
        if (string.IsNullOrEmpty(tudo)) return null;
        List<string> r = new List<string>();
        if (textos.Count <= 1)
        {
            r.Add(tudo.Trim());
            return r;
        }
        string[] linhas = SemCR(tudo).TrimEnd('\n').Split('\n');
        int atual = 0;
        for (int i = 0; i < textos.Count; i++)
        {
            int n = SemCR(textos[i]).Split('\n').Length;
            if (atual + n > linhas.Length) return null;
            StringBuilder t = new StringBuilder();
            for (int k = 0; k < n; k++)
            {
                if (k > 0) t.Append('\n');
                t.Append(linhas[atual + k].Trim());
            }
            atual += n;
            string s = t.ToString();
            if (s.Trim().Length == 0) return null;
            r.Add(s);
        }
        if (atual != linhas.Length) return null;
        return r;
    }

    // ---------------- IA no formato OpenAI (DeepSeek, Gemini, Groq, OpenRouter, Ollama...) ----------------

    private static UnityWebRequest MontarOpenAI(Provedor p, List<string> textos)
    {
        string url = LiveConfig.Url(p.Id).Trim().TrimEnd('/');
        if (!url.EndsWith("/chat/completions")) url += "/chat/completions";

        string modelo = LiveConfig.Modelo(p.Id).Trim();
        string json = "{\"model\":" + Json.Esc(modelo) + (AceitaTemperatura(modelo) ? ",\"temperature\":0.2" : "") + ",\"messages\":["
            + "{\"role\":\"system\",\"content\":" + Json.Esc(PromptSistema(textos)) + "},"
            + "{\"role\":\"user\",\"content\":" + Json.Esc(Json.ListaTextos(textos)) + "}]}";
        UnityWebRequest r = Post(url, json);
        string chave = LiveConfig.Chave(p.Id).Trim();
        if (chave.Length > 0) r.SetRequestHeader("Authorization", "Bearer " + chave);
        return r;
    }

    // Modelos "que pensam" da OpenAI (gpt-5..., o1, o3, o4...) so aceitam a temperatura padrao:
    // mandar 0.2 da erro 400. Para eles o campo nao vai no pedido.
    private static bool AceitaTemperatura(string modelo)
    {
        string m = (modelo ?? "").ToLowerInvariant();
        int barra = m.LastIndexOf('/');
        if (barra >= 0) m = m.Substring(barra + 1);   // ex.: "openai/gpt-5-mini" no OpenRouter
        if (m.StartsWith("gpt-5", StringComparison.Ordinal)) return false;
        if (m.Length >= 2 && m[0] == 'o' && char.IsDigit(m[1])) return false;
        return true;
    }

    // ---------------- Claude (Anthropic) ----------------

    private static UnityWebRequest MontarClaude(Provedor p, List<string> textos)
    {
        string url = LiveConfig.Url(p.Id).Trim();
        string json = "{\"model\":" + Json.Esc(LiveConfig.Modelo(p.Id).Trim()) + ",\"max_tokens\":" + MaxTokens(textos) + ",\"temperature\":0.2,"
            + "\"system\":" + Json.Esc(PromptSistema(textos)) + ",\"messages\":["
            + "{\"role\":\"user\",\"content\":" + Json.Esc(Json.ListaTextos(textos)) + "}]}";
        UnityWebRequest r = Post(url, json);
        r.SetRequestHeader("x-api-key", LiveConfig.Chave(p.Id).Trim());
        r.SetRequestHeader("anthropic-version", "2023-06-01");
        return r;
    }

    // Limite de resposta pelo tamanho do lote: folga para idiomas que gastam mais tokens,
    // sem pedir demais num lote pequeno. Resposta cortada no meio faz o lote inteiro falhar.
    private static int MaxTokens(List<string> textos)
    {
        int chars = 0;
        for (int i = 0; i < textos.Count; i++) chars += textos[i].Length;
        return Mathf.Clamp(1024 + chars * 3, 1024, 8192);
    }

    // ---------------- comum ----------------

    private static UnityWebRequest Post(string url, string json)
    {
        UnityWebRequest r = new UnityWebRequest(url, "POST");
        r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        r.downloadHandler = new DownloadHandlerBuffer();
        r.SetRequestHeader("Content-Type", "application/json");
        r.timeout = 60;
        return r;
    }

    // Regras fixas (garantem o formato da resposta) + estilo escolhido pelo jogador
    // + termos do glossario que aparecem nos textos + textos recentes como contexto.
    public static string PromptSistema(List<string> textos)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("You are a professional game localizer. You translate UI text, item names, quests and dialogue of a sci-fi space station and fleet management game called 'Outer Space'. ");
        sb.Append("The user sends a JSON array of strings. Translate EVERY string into ").Append(LiveConfig.NomeIdiomaIngles()).Append(". ");
        sb.Append("The source text may be Chinese, English or a mix of both. ");
        sb.Append("Hard rules: keep placeholders such as {0}, {1}, {2} and all markup tags EXACTLY unchanged and in the text; ");
        sb.Append("never add notes or explanations; if a string is already in the target language, return it unchanged. ");
        sb.Append("Reply with ONLY a JSON array of strings, with exactly the same number of items and the same order as the input.");

        int e = LiveConfig.Estilo;
        if (e < 0 || e >= LiveConfig.Estilos.Length) e = 0;
        string estilo = LiveConfig.Estilos[e][1];
        if (estilo.Length > 0) sb.Append("\nStyle: ").Append(estilo);
        string extra = LiveConfig.PromptExtra;
        if (extra != null && extra.Trim().Length > 0)
            sb.Append("\nExtra instructions from the player (follow them, but the hard rules above always win): ").Append(extra.Trim());
        string cats = Categorias.TextoPrompt();
        if (cats.Length > 0) sb.Append("\n").Append(cats);

        // glossario: o mesmo nome traduzido do mesmo jeito tambem dentro de frases
        List<TermoGlossario> termos = Glossario.TermosEm(textos, 40, 1500);
        if (termos.Count > 0)
        {
            sb.Append("\nGlossary: when one of these terms appears inside a string, translate it exactly like this:");
            for (int i = 0; i < termos.Count; i++)
                sb.Append("\n- ").Append(UmaLinha(termos[i].Original)).Append(" => ").Append(UmaLinha(termos[i].Traducao));
        }

        // contexto: o que apareceu na tela pouco antes (ajuda em dialogos: genero, tratamento, assunto)
        List<string[]> recentes = LiveMotor.Recentes(textos);
        if (recentes.Count > 0)
        {
            sb.Append("\nRecent text shown in the game just before, with its translation. Context only: do NOT translate it again and do NOT include it in your reply:");
            for (int i = 0; i < recentes.Count; i++)
                sb.Append("\n- ").Append(UmaLinha(recentes[i][0])).Append(" => ").Append(UmaLinha(recentes[i][1]));
        }
        return sb.ToString();
    }

    private static string UmaLinha(string s)
    {
        return (s ?? "").Replace("\r", " ").Replace("\n", " ");
    }

    // Tira cercas de codigo (```json) e le o primeiro array JSON de strings.
    private static List<string> ListaDeTextos(string conteudo)
    {
        if (conteudo == null) return null;
        int ini = conteudo.IndexOf('[');
        int fim = conteudo.LastIndexOf(']');
        if (ini < 0 || fim <= ini) return null;
        List<object> a = Json.Parse(conteudo.Substring(ini, fim - ini + 1)) as List<object>;
        if (a == null) return null;
        List<string> r = new List<string>();
        for (int i = 0; i < a.Count; i++)
        {
            string t = a[i] as string;
            if (t == null) return null;
            r.Add(t);
        }
        return r;
    }
}
