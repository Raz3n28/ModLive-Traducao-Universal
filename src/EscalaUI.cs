using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aba "Interface" do menu: escala da interface do PROPRIO jogo (Canvas do Unity) e tamanho das fontes.
// - escala geral (50% a 200%) x escala de cada Canvas (pelo nome do objeto): mexe no CanvasScaler.
// - fonte (50% a 200%): multiplica o tamanho dos textos TMP_Text e UI.Text.
// Guarda o valor original de cada um; se o jogo trocar o valor depois, o novo vira o original.
// Tudo em 100% = devolve os originais e para de mexer. Janelas IMGUI (menus de outros mods) nao mudam.
// O jogo pode reescrever a escala do CanvasScaler a todo quadro (a escala do mod piscava): com o Harmony,
// o mod intercepta essa escrita e ja multiplica o valor do jogo pela escala escolhida.
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
    private static bool proprio;            // o proprio mod esta escrevendo no CanvasScaler (o prefixo deixa passar)
    private static readonly HashSet<int> avisados = new HashSet<int>();

    // poe os prefixos do Harmony nos "set" do CanvasScaler (scaleFactor e referenceResolution)
    public static void Iniciar()
    {
        const BindingFlags F = BindingFlags.Static | BindingFlags.NonPublic;
        PropertyInfo pf = typeof(CanvasScaler).GetProperty("scaleFactor");
        PropertyInfo pr = typeof(CanvasScaler).GetProperty("referenceResolution");
        bool a = pf != null && LiveImgui.Prefixar(pf.GetSetMethod(), typeof(EscalaUI).GetMethod("PreFator", F));
        bool b = pr != null && LiveImgui.Prefixar(pr.GetSetMethod(), typeof(EscalaUI).GetMethod("PreRef", F));
        LiveLog.Info("UI scale: CanvasScaler hooked = " + (a && b));
        Canvas.willRenderCanvases += AntesDeDesenhar;
    }

    // Canvas que estao com escala do mod (refeita a cada passada do Tick)
    private static readonly List<CanvasScaler> vigiados = new List<CanvasScaler>();
    private static readonly Dictionary<int, float> escritoCanvas = new Dictionary<int, float>();
    private static readonly HashSet<int> avisadosCanvas = new HashSet<int>();

    // Segunda protecao: se o jogo escreve direto no Canvas.scaleFactor (sem passar pelo CanvasScaler),
    // logo antes de desenhar a tela o mod multiplica de novo o valor do jogo pela escala escolhida.
    private static void AntesDeDesenhar()
    {
        if (vigiados.Count == 0) return;
        proprio = true;
        try
        {
            for (int i = 0; i < vigiados.Count; i++)
            {
                CanvasScaler c = vigiados[i];
                if (!c || !c.isActiveAndEnabled) continue;
                Canvas cv = c.GetComponent<Canvas>();
                if (!cv || !cv.isRootCanvas) continue;
                // o jogo pode ter reescrito o CanvasScaler sem passar pelo Harmony (funcao "inline"): refaz ja,
                // e ja passa para o Canvas neste quadro (o CanvasScaler so atualiza no proximo)
                float esperado = Esperado(c, cv);
                if (AplicarUm(c))
                {
                    float e2 = Esperado(c, cv);
                    if (e2 > 0f && Math.Abs(cv.scaleFactor - e2) > 0.001f) cv.scaleFactor = e2;
                    esperado = e2;
                }
                // no modo "tamanho fixo" o Canvas deve ficar igual ao scaleFactor do CanvasScaler; se nao ficou, foi o jogo
                if (c.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize) continue;
                float f = Fator(c);
                if (Math.Abs(f - 1f) < 0.001f) continue;
                float atual = cv.scaleFactor;
                if (esperado <= 0f || Math.Abs(atual - esperado) < 0.001f) continue;
                int id = cv.GetInstanceID();
                float ultimo;
                if (escritoCanvas.TryGetValue(id, out ultimo) && Math.Abs(atual - ultimo) < 0.001f) continue;
                // valor diferente do CanvasScaler e do que o mod escreveu: foi o jogo, e o valor dele e o original
                if (avisadosCanvas.Count < 200 && avisadosCanvas.Add(id))
                    LiveLog.Info("UI scale: the game sets Canvas.scaleFactor of '" + c.gameObject.name + "' directly");
                float novo = atual * f;
                cv.scaleFactor = novo;
                escritoCanvas[id] = novo;
            }
        }
        catch { }
        finally { proprio = false; }
    }

    // escala que o CanvasScaler (ja com a escala do mod) coloca no Canvas; 0 = nao sei calcular
    private static float Esperado(CanvasScaler c, Canvas cv)
    {
        if (c.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) return c.scaleFactor;
        if (c.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return 0f;
        Vector2 rr = c.referenceResolution;
        if (rr.x <= 0f || rr.y <= 0f) return 0f;
        float w = Screen.width, h = Screen.height;     // o CanvasScaler usa o tamanho da tela
        double s;
        if (c.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
        {
            double lw = Math.Log(w / rr.x, 2), lh = Math.Log(h / rr.y, 2);
            double m = c.matchWidthOrHeight;
            s = Math.Pow(2, lw + (lh - lw) * m);
        }
        else if (c.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand) s = Math.Min(w / rr.x, h / rr.y);
        else s = Math.Max(w / rr.x, h / rr.y);
        return (float)s;
    }

    private static float Fator(CanvasScaler c)
    {
        return LiveConfig.EscalaUI / 100f * LiveConfig.EscalaCanvas(c.gameObject.name) / 100f;
    }

    private static void Avisar(CanvasScaler c)
    {
        int id = c.GetInstanceID();
        if (avisados.Count > 200 || !avisados.Add(id)) return;
        LiveLog.Info("UI scale: the game sets the CanvasScaler of '" + c.gameObject.name + "' (" + c.uiScaleMode + ")");
    }

    // o jogo escreveu scaleFactor: guarda como original e ja grava multiplicado
    private static void PreFator(CanvasScaler __instance, ref float value)
    {
        if (proprio || !__instance) return;
        try
        {
            if (__instance.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize) return;
            Avisar(__instance);
            int id = __instance.GetInstanceID();
            Orig o;
            if (!canvas.TryGetValue(id, out o)) { o = new Orig(); canvas[id] = o; }
            o.Valor = value;
            value = value * Fator(__instance);
            o.Posto = value;
        }
        catch { }
    }

    // o jogo escreveu referenceResolution: guarda como original e ja grava dividido (referencia menor = maior)
    private static void PreRef(CanvasScaler __instance, ref Vector2 value)
    {
        if (proprio || !__instance) return;
        try
        {
            if (__instance.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
            Avisar(__instance);
            int id = __instance.GetInstanceID();
            Orig o;
            if (!canvas.TryGetValue(id, out o)) { o = new Orig(); canvas[id] = o; }
            o.Valor = value.x; o.Valor2 = value.y;
            float f = Fator(__instance);
            value = new Vector2(value.x / f, value.y / f);
            o.Posto = value.x; o.Posto2 = value.y;
        }
        catch { }
    }

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
        try { proprio = true; AplicarCanvas(); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "UI scale"); }
        finally { proprio = false; }
        try { AplicarTextos(); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "font scale"); }
        mexendo = !padrao;
        if (padrao) { canvas.Clear(); textos.Clear(); vigiados.Clear(); escritoCanvas.Clear(); }
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
        vigiados.Clear();
        UnityEngine.Object[] cs = UnityEngine.Object.FindObjectsOfType(typeof(CanvasScaler));
        for (int i = 0; i < cs.Length; i++)
        {
            CanvasScaler c = cs[i] as CanvasScaler;
            if (!c) continue;
            if (vigiados.Count < 100) vigiados.Add(c);
            string nome = c.gameObject.name;
            if (nomes.Count < 30 && !nomes.Contains(nome)) nomes.Add(nome);
            AplicarUm(c);
        }
        nomes.Sort(StringComparer.OrdinalIgnoreCase);
        if (canvas.Count > 2000) canvas.Clear();
    }

    // poe a escala do mod num CanvasScaler; se o jogo trocou o valor, o novo vira o original. true = escreveu algo
    private static bool AplicarUm(CanvasScaler c)
    {
        bool mudou = false;
        float f = Fator(c);
        int id = c.GetInstanceID();
        Orig o;
        if (c.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            // tela de referencia menor = interface maior
            Vector2 rr = c.referenceResolution;
            if (!canvas.TryGetValue(id, out o)) { o = new Orig(); o.Valor = rr.x; o.Valor2 = rr.y; o.Posto = -1f; canvas[id] = o; }
            else if (Math.Abs(rr.x - o.Posto) > 0.5f || Math.Abs(rr.y - o.Posto2) > 0.5f) { o.Valor = rr.x; o.Valor2 = rr.y; }
            Vector2 novo = new Vector2(o.Valor / f, o.Valor2 / f);
            if (Math.Abs(novo.x - rr.x) > 0.5f || Math.Abs(novo.y - rr.y) > 0.5f) { c.referenceResolution = novo; mudou = true; }
            o.Posto = novo.x; o.Posto2 = novo.y;
        }
        else if (c.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
        {
            float sf = c.scaleFactor;
            if (!canvas.TryGetValue(id, out o)) { o = new Orig(); o.Valor = sf; o.Posto = -1f; canvas[id] = o; }
            else if (Math.Abs(sf - o.Posto) > 0.001f) o.Valor = sf;
            float novo = o.Valor * f;
            if (Math.Abs(novo - sf) > 0.001f) { c.scaleFactor = novo; mudou = true; }
            o.Posto = novo;
        }
        return mudou;
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
