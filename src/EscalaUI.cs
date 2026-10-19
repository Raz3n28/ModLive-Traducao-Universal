using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aba "Interface" do menu: escala da interface do PROPRIO jogo (Canvas do Unity) e tamanho das fontes.
// - escala geral (50% a 200%) x escala de cada Canvas (pelo nome do objeto): mexe no CanvasScaler.
// - fonte (50% a 200%): multiplica o tamanho dos textos TMP_Text e UI.Text.
// Guarda o valor original de cada um; se o jogo trocar o valor depois, o novo vira o original.
// Tudo em 100% = devolve os originais e para de mexer. Janelas IMGUI (menus de outros mods) nao mudam.
internal static class EscalaUI
{
    public const int Min = 50, Max = 200;

    private sealed class Orig
    {
        public float Valor, Valor2;     // tamanho (ou max/min do auto-ajuste); no Canvas: scaleFactor / referencia
        public float Posto, Posto2;     // o que o mod colocou da ultima vez (para saber se o jogo mudou)
        public bool Auto;               // texto com auto-ajuste de tamanho (o EncaixeFonte pode ligar depois)
        public float F = 1f;            // fator aplicado da ultima vez
    }

    private static readonly Dictionary<int, Orig> canvas = new Dictionary<int, Orig>();
    private static readonly Dictionary<int, Orig> textos = new Dictionary<int, Orig>();
    private static readonly List<string> nomes = new List<string>();
    private static float proxima;
    private static bool mexendo;            // ja mexeu em algo: precisa devolver ao voltar a 100%
    public static bool Mudou;               // o menu mudou um valor: aplica ja

    // nomes dos Canvas com CanvasScaler vistos na ultima passada (para a lista da aba Interface)
    public static List<string> Nomes { get { return nomes; } }

    public static void Tick()
    {
        float agora = Time.unscaledTime;
        if (!Mudou && agora < proxima) return;
        Mudou = false;
        proxima = agora + 2f;
        bool padrao = LiveConfig.EscalaUI == 100 && LiveConfig.EscalaFonte == 100 && !LiveConfig.TemEscalaCanvas();
        if (padrao && !mexendo) { ListarNomes(); return; }
        try { AplicarCanvas(); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "UI scale"); }
        try { AplicarTextos(); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "font scale"); }
        mexendo = !padrao;
        if (padrao) { canvas.Clear(); textos.Clear(); }
    }

    private static void ListarNomes()
    {
        nomes.Clear();
        UnityEngine.Object[] cs = UnityEngine.Object.FindObjectsOfType(typeof(CanvasScaler));
        for (int i = 0; i < cs.Length && nomes.Count < 30; i++)
        {
            CanvasScaler c = cs[i] as CanvasScaler;
            if (c && !nomes.Contains(c.gameObject.name)) nomes.Add(c.gameObject.name);
        }
        nomes.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private static void AplicarCanvas()
    {
        nomes.Clear();
        UnityEngine.Object[] cs = UnityEngine.Object.FindObjectsOfType(typeof(CanvasScaler));
        for (int i = 0; i < cs.Length; i++)
        {
            CanvasScaler c = cs[i] as CanvasScaler;
            if (!c) continue;
            string nome = c.gameObject.name;
            if (nomes.Count < 30 && !nomes.Contains(nome)) nomes.Add(nome);
            float f = LiveConfig.EscalaUI / 100f * LiveConfig.EscalaCanvas(nome) / 100f;
            int id = c.GetInstanceID();
            Orig o;
            if (c.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                // tela de referencia menor = interface maior
                Vector2 rr = c.referenceResolution;
                if (!canvas.TryGetValue(id, out o)) { o = new Orig(); o.Valor = rr.x; o.Valor2 = rr.y; o.Posto = -1f; canvas[id] = o; }
                else if (Math.Abs(rr.x - o.Posto) > 0.5f || Math.Abs(rr.y - o.Posto2) > 0.5f) { o.Valor = rr.x; o.Valor2 = rr.y; }
                Vector2 novo = new Vector2(o.Valor / f, o.Valor2 / f);
                if (Math.Abs(novo.x - rr.x) > 0.5f || Math.Abs(novo.y - rr.y) > 0.5f) c.referenceResolution = novo;
                o.Posto = novo.x; o.Posto2 = novo.y;
            }
            else if (c.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                float sf = c.scaleFactor;
                if (!canvas.TryGetValue(id, out o)) { o = new Orig(); o.Valor = sf; o.Posto = -1f; canvas[id] = o; }
                else if (Math.Abs(sf - o.Posto) > 0.001f) o.Valor = sf;
                float novo = o.Valor * f;
                if (Math.Abs(novo - sf) > 0.001f) c.scaleFactor = novo;
                o.Posto = novo;
            }
        }
        nomes.Sort(StringComparer.OrdinalIgnoreCase);
        if (canvas.Count > 2000) canvas.Clear();
    }

    private static void AplicarTextos()
    {
        float f = LiveConfig.EscalaFonte / 100f;
        UnityEngine.Object[] tmps = UnityEngine.Object.FindObjectsOfType(typeof(TMP_Text));
        for (int i = 0; i < tmps.Length; i++)
        {
            TMP_Text t = tmps[i] as TMP_Text;
            if (!t) continue;
            int id = t.GetInstanceID();
            Orig o = null;
            bool auto = t.enableAutoSizing;
            if (textos.TryGetValue(id, out o) && o.Auto != auto)
            {
                // ligou/desligou o auto-ajuste (ex.: o EncaixeFonte, com o tamanho ja escalado): recomeca sem a escala
                o.Auto = auto;
                o.Valor = (auto ? t.fontSizeMax : t.fontSize) / o.F;
                o.Valor2 = auto ? t.fontSizeMin / o.F : 0f;
                o.Posto = auto ? t.fontSizeMax : t.fontSize;
                o.Posto2 = auto ? t.fontSizeMin : 0f;
            }
            if (auto)
            {
                // auto-ajuste: muda o maximo e o minimo (o TextMeshPro escolhe o tamanho entre eles)
                if (o == null) { o = new Orig(); o.Auto = true; o.Valor = t.fontSizeMax; o.Valor2 = t.fontSizeMin; o.Posto = -1f; textos[id] = o; }
                else if (Math.Abs(t.fontSizeMax - o.Posto) > 0.05f || Math.Abs(t.fontSizeMin - o.Posto2) > 0.05f) { o.Valor = t.fontSizeMax; o.Valor2 = t.fontSizeMin; }
                float mx = o.Valor * f, mn = o.Valor2 * f;
                if (Math.Abs(mx - t.fontSizeMax) > 0.05f) t.fontSizeMax = mx;
                if (Math.Abs(mn - t.fontSizeMin) > 0.05f) t.fontSizeMin = mn;
                o.Posto = mx; o.Posto2 = mn;
            }
            else
            {
                float tam = t.fontSize;
                if (o == null) { o = new Orig(); o.Valor = tam; o.Posto = -1f; textos[id] = o; }
                else if (Math.Abs(tam - o.Posto) > 0.05f) o.Valor = tam;
                float novo = o.Valor * f;
                if (Math.Abs(novo - tam) > 0.05f) t.fontSize = novo;
                o.Posto = novo;
            }
            o.F = f;
        }
        UnityEngine.Object[] txts = UnityEngine.Object.FindObjectsOfType(typeof(Text));
        for (int i = 0; i < txts.Length; i++)
        {
            Text t = txts[i] as Text;
            if (!t) continue;
            int id = t.GetInstanceID();
            Orig o;
            int tam = t.fontSize;
            if (!textos.TryGetValue(id, out o)) { o = new Orig(); o.Valor = tam; o.Valor2 = t.resizeTextMaxSize; o.Posto = -1f; textos[id] = o; }
            else if (Math.Abs(tam - o.Posto) > 0.5f) { o.Valor = tam; o.Valor2 = t.resizeTextMaxSize; }
            int novo = Math.Max(1, (int)Math.Round(o.Valor * f));
            if (novo != tam) t.fontSize = novo;
            if (t.resizeTextForBestFit)
            {
                int mx = Math.Max(1, (int)Math.Round(o.Valor2 * f));
                if (mx != t.resizeTextMaxSize) t.resizeTextMaxSize = mx;
            }
            o.Posto = novo;
        }
        if (textos.Count > 30000) textos.Clear();
    }
}
