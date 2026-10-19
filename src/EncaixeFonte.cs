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
    // tamanho minimo = 65% do original (e nunca menor que 10)
    private const float Fator = 0.65f;
    private const float MinimoAbsoluto = 10f;

    private static readonly Dictionary<int, string> verificadosTmp = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> verificadosUi = new Dictionary<int, string>();
    public static int Ajustados;

    // Chamar so quando o texto NAO acabou de ser trocado (as medidas do TextMeshPro precisam estar atualizadas).
    public static void Verificar(TMP_Text t)
    {
        try
        {
            if (!t || !t.isActiveAndEnabled || t.enableAutoSizing) return;
            string s = t.text;
            if (string.IsNullOrEmpty(s)) return;
            int id = t.GetInstanceID();
            string antes;
            if (verificadosTmp.TryGetValue(id, out antes) && antes == s) return;
            if (verificadosTmp.Count > 20000) verificadosTmp.Clear();
            verificadosTmp[id] = s;

            Rect r = t.rectTransform.rect;
            if (r.width < 4f || r.height < 4f) return;
            if (t.GetComponent<ContentSizeFitter>() != null) return; // a caixa cresce sozinha com o texto

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
            if (!t || !t.isActiveAndEnabled || t.resizeTextForBestFit) return;
            string s = t.text;
            if (string.IsNullOrEmpty(s)) return;
            int id = t.GetInstanceID();
            string antes;
            if (verificadosUi.TryGetValue(id, out antes) && antes == s) return;
            if (verificadosUi.Count > 20000) verificadosUi.Clear();
            verificadosUi[id] = s;

            Rect r = t.rectTransform.rect;
            if (r.width < 4f || r.height < 4f) return;
            if (t.GetComponent<ContentSizeFitter>() != null) return;

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
