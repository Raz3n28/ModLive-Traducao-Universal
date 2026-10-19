using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Quando um texto (geralmente ja traduzido) nao cabe no espaco dele, liga o ajuste automatico
// de tamanho da fonte: diminui so o necessario para caber, nunca passa do tamanho original.
// Resolve palavras quebradas ("Gover nante") e textos cortados ("···").
internal static class EncaixeFonte
{
    // tamanho minimo = LiveConfig.FonteMin % do original (padrao 65%, ajustavel na aba Avancado; nunca menor que 10)
    private static float Fator { get { return Mathf.Clamp(LiveConfig.FonteMin, 50, 90) / 100f; } }
    private const float MinimoAbsoluto = 10f;

    private static readonly Dictionary<int, string> verificadosTmp = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> verificadosUi = new Dictionary<int, string>();
    public static int Ajustados;

    // Chamar so quando o texto NAO acabou de ser trocado (as medidas do TextMeshPro precisam estar atualizadas).
    public static void Verificar(TMP_Text t)
    {
        try
        {
            if (!t || !t.isActiveAndEnabled) return;
            string s = t.text;
            if (string.IsNullOrEmpty(s)) return;
            int id = t.GetInstanceID();
            string antes;
            if (verificadosTmp.TryGetValue(id, out antes) && antes == s) return;

            // caixa ainda sem tamanho (tela abrindo): NAO marca como visto, para medir de novo na proxima vez
            Rect r = t.rectTransform.rect;
            if (r.width < 4f || r.height < 4f) return;
            if (verificadosTmp.Count > 20000) verificadosTmp.Clear();
            verificadosTmp[id] = s;

            // caixa que cresce junto com o texto: o texto "sempre cabe" nela, mas a caixa pode crescer por cima
            // do vizinho. Ai a fonte diminui ate a caixa caber no espaco livre.
            ContentSizeFitter csf = t.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                if (csf.horizontalFit != ContentSizeFitter.FitMode.Unconstrained && LiveMotor.Nossa(s)) EncaixarCaixa(t, r);
                return;
            }

            // o jogo ja usa ajuste automatico, mas com um minimo alto: o texto continua cortado com "..."
            if (t.enableAutoSizing)
            {
                float minimo = Mathf.Max(MinimoAbsoluto, t.fontSizeMax * Fator);
                if (t.isTextOverflowing && t.fontSizeMin > minimo + 0.5f)
                {
                    t.fontSizeMin = minimo;
                    Ajustados++;
                }
                return;
            }

            bool precisa = t.isTextOverflowing || PalavraQuebrada(t);
            if (!precisa)
            {
                if (t.enableWordWrapping) precisa = t.preferredHeight > r.height * 1.1f + 2f;
                else precisa = t.preferredWidth > r.width + 2f;
            }
            if (!precisa) return;

            float tam = t.fontSize;
            if (tam <= MinimoAbsoluto) return;
            t.fontSizeMax = tam;
            t.fontSizeMin = Mathf.Max(MinimoAbsoluto, tam * Fator);
            t.enableAutoSizing = true;
            Ajustados++;
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_fonte_ajuste", e, "TMP");
        }
    }

    // tamanho original e largura livre de cada texto de caixa que cresce (para recalcular quando o texto muda
    // e para devolver o tamanho quando a tela volta ao original)
    private static readonly Dictionary<TMP_Text, float> tamOrig = new Dictionary<TMP_Text, float>();
    private static readonly Dictionary<TMP_Text, float> largLivre = new Dictionary<TMP_Text, float>();

    private static void EncaixarCaixa(TMP_Text t, Rect r)
    {
        float cur = t.fontSize;
        if (cur <= 0f) return;
        float orig;
        if (!tamOrig.TryGetValue(t, out orig)) orig = cur;
        float sob = Sobreposicao(t.rectTransform);
        float livre;
        if (!largLivre.TryGetValue(t, out livre))
        {
            if (sob <= 1f) return;                      // nao encosta em nada: deixa como esta
            if (tamOrig.Count > 3000) LimparMortos();
            livre = r.width - sob;
            largLivre[t] = livre;
            tamOrig[t] = orig;
        }
        else if (sob > 1f)
        {
            livre = Mathf.Min(livre, r.width - sob);
            largLivre[t] = livre;
        }
        if (livre < 4f) return;

        // largura que o texto teria no tamanho original (a largura cresce junto com o tamanho da fonte)
        float larguraOrig = t.GetPreferredValues(t.text).x * orig / cur;
        float novo = orig * Mathf.Min(1f, livre / Mathf.Max(1f, larguraOrig));
        novo = Mathf.Max(novo, Mathf.Max(MinimoAbsoluto, orig * Fator));
        if (Mathf.Abs(novo - cur) > 0.25f)
        {
            t.fontSize = novo;
            Ajustados++;
        }
    }

    private static readonly Vector3[] cantos = new Vector3[4];

    // Quanto a caixa do texto passa por cima de um vizinho (outro texto, icone, botao do mesmo pai) ou
    // sai do pai, em unidades da propria caixa. Fundos (do tamanho do pai ou que envolvem o texto) nao contam.
    private static float Sobreposicao(RectTransform rt)
    {
        RectTransform pai = rt.parent as RectTransform;
        if (pai == null) return 0f;
        rt.GetWorldCorners(cantos);
        float x0 = cantos[0].x, x1 = cantos[2].x, y0 = cantos[0].y, y1 = cantos[2].y;
        float w = x1 - x0;
        if (w <= 0f) return 0f;
        pai.GetWorldCorners(cantos);
        float px0 = cantos[0].x, px1 = cantos[2].x;
        float pw = px1 - px0;

        float sob = 0f;
        // pai de tamanho fixo: o texto nao pode sair dele
        if (pai.GetComponent<ContentSizeFitter>() == null && pai.GetComponent<LayoutGroup>() == null)
            sob = Mathf.Max(0f, px0 - x0) + Mathf.Max(0f, x1 - px1);

        for (int i = 0; i < pai.childCount; i++)
        {
            RectTransform o = pai.GetChild(i) as RectTransform;
            if (o == null || o == rt || !o.gameObject.activeInHierarchy) continue;
            Graphic g = o.GetComponent<Graphic>();
            if (g == null || !g.enabled) continue;
            TMP_Text ot = g as TMP_Text;
            if (ot != null && string.IsNullOrEmpty(ot.text)) continue;
            o.GetWorldCorners(cantos);
            float ox0 = cantos[0].x, ox1 = cantos[2].x, oy0 = cantos[0].y, oy1 = cantos[2].y;
            float ow = ox1 - ox0;
            if (ow <= 0f || ow >= pw * 0.9f) continue;          // fundo do tamanho do pai
            if (ox0 <= x0 && ox1 >= x1) continue;               // envolve o texto: fundo
            float vy = Mathf.Min(y1, oy1) - Mathf.Max(y0, oy0);
            if (vy < (y1 - y0) * 0.3f) continue;                // outra linha
            float hx = Mathf.Min(x1, ox1) - Mathf.Max(x0, ox0);
            if (hx > sob) sob = hx;
        }
        return sob * (rt.rect.width / w);
    }

    // a tela voltou ao original: devolve o tamanho de fonte que o mod tinha mudado nas caixas que crescem
    public static void DevolverTodos()
    {
        foreach (KeyValuePair<TMP_Text, float> kv in tamOrig)
        {
            try { if (kv.Key) kv.Key.fontSize = kv.Value; } catch { }
        }
        tamOrig.Clear();
        largLivre.Clear();
        verificadosTmp.Clear();
        verificadosUi.Clear();
    }

    // a fonte dos textos mudou (menu): mede tudo de novo
    public static void Reverificar()
    {
        verificadosTmp.Clear();
        verificadosUi.Clear();
    }

    private static void LimparMortos()
    {
        List<TMP_Text> mortos = new List<TMP_Text>();
        foreach (TMP_Text t in tamOrig.Keys) if (!t) mortos.Add(t);
        for (int i = 0; i < mortos.Count; i++) { tamOrig.Remove(mortos[i]); largLivre.Remove(mortos[i]); }
    }

    // Uma palavra latina partida no meio entre duas linhas.
    private static bool PalavraQuebrada(TMP_Text t)
    {
        TMP_TextInfo ti = t.textInfo;
        if (ti == null || ti.lineCount < 2 || ti.characterInfo == null) return false;
        int n = Math.Min(ti.characterCount, ti.characterInfo.Length);
        for (int i = 1; i < ti.lineCount && i < ti.lineInfo.Length; i++)
        {
            int a = ti.lineInfo[i - 1].lastCharacterIndex;
            int b = ti.lineInfo[i].firstCharacterIndex;
            if (a < 0 || b != a + 1 || b >= n) continue;
            char ca = ti.characterInfo[a].character;
            char cb = ti.characterInfo[b].character;
            if (ca < '　' && cb < '　' && char.IsLetter(ca) && char.IsLetter(cb)) return true;
        }
        return false;
    }

    public static void Verificar(Text t)
    {
        try
        {
            if (!t || !t.isActiveAndEnabled) return;
            string s = t.text;
            if (string.IsNullOrEmpty(s)) return;
            int id = t.GetInstanceID();
            string antes;
            if (verificadosUi.TryGetValue(id, out antes) && antes == s) return;

            Rect r = t.rectTransform.rect;
            if (r.width < 4f || r.height < 4f) return;
            if (verificadosUi.Count > 20000) verificadosUi.Clear();
            verificadosUi[id] = s;
            if (t.GetComponent<ContentSizeFitter>() != null) return;
            if (t.resizeTextForBestFit)
            {
                // o jogo ja ajusta sozinho, mas o minimo dele pode ser alto demais
                int minimo = Mathf.Max((int)MinimoAbsoluto, Mathf.RoundToInt(t.resizeTextMaxSize * Fator));
                if (t.resizeTextMinSize > minimo && t.preferredWidth > r.width + 2f && t.horizontalOverflow != HorizontalWrapMode.Wrap)
                {
                    t.resizeTextMinSize = minimo;
                    Ajustados++;
                }
                return;
            }

            bool precisa;
            if (t.horizontalOverflow == HorizontalWrapMode.Wrap) precisa = t.preferredHeight > r.height * 1.1f + 2f;
            else precisa = t.preferredWidth > r.width + 2f;
            if (!precisa) return;

            int tam = t.fontSize;
            if (tam <= (int)MinimoAbsoluto) return;
            t.resizeTextMaxSize = tam;
            t.resizeTextMinSize = Mathf.Max((int)MinimoAbsoluto, Mathf.RoundToInt(tam * Fator));
            t.resizeTextForBestFit = true;
            Ajustados++;
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_fonte_ajuste", e, "UI.Text");
        }
    }
}
