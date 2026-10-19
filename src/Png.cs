using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// PNGs do visual do menu (modelo\v_common\UI, gerados por ferramentas\gerar_ui.py).
// Cada um e lido uma vez; o que faltar devolve null e o menu fica com a textura gerada no codigo.
internal static class Png
{
    private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    private static string pasta;
    private static bool procurou;

    public static bool Existe { get { return Pasta != null; } }

    private static string Pasta
    {
        get
        {
            if (procurou) return pasta;
            procurou = true;
            List<string> c = new List<string>();
            if (!string.IsNullOrEmpty(LiveBoot.PastaMod)) c.Add(Path.Combine(Path.Combine(LiveBoot.PastaMod, "v_common"), "UI"));
            try
            {
                // ...\v_common\Assemblies\TraducaoLive.dll -> ...\v_common\UI
                string loc = typeof(Png).Assembly.Location;
                if (!string.IsNullOrEmpty(loc)) c.Add(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(loc)), "UI"));
            }
            catch { }
            for (int i = 0; i < c.Count; i++)
            {
                try { if (File.Exists(Path.Combine(c[i], "janela.png"))) { pasta = c[i]; break; } }
                catch { }
            }
            LiveLog.Info("lg_ui", pasta ?? "-");
            return pasta;
        }
    }

    public static Texture2D Tex(string nome)
    {
        Texture2D t;
        if (cache.TryGetValue(nome, out t)) return t;
        t = null;
        if (Pasta != null)
        {
            string arq = Path.Combine(pasta, nome + ".png");
            try
            {
                if (File.Exists(arq))
                {
                    t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(t, File.ReadAllBytes(arq)))
                    {
                        t.wrapMode = TextureWrapMode.Clamp;
                        t.filterMode = FilterMode.Bilinear;
                        t.hideFlags = HideFlags.HideAndDontSave;
                    }
                    else t = null;
                }
            }
            catch (Exception e)
            {
                LiveLog.Erro("le_parte", e, "PNG " + nome);
                t = null;
            }
        }
        cache[nome] = t;
        return t;
    }

    // fundos normal / passar o mouse / clique de um botao (9-slice com a borda dada). null em normal = deixa como esta.
    public static void Fundos(GUIStyle s, string normal, string hover, string clique, int borda)
    {
        Texture2D n = normal != null ? Tex(normal) : null;
        Texture2D h = Tex(hover);
        Texture2D a = Tex(clique);
        if (h == null || a == null || (normal != null && n == null)) return;
        if (n != null) { s.normal.background = n; s.focused.background = n; s.onNormal.background = n; s.onFocused.background = n; }
        s.hover.background = h; s.onHover.background = h;
        s.active.background = a; s.onActive.background = a;
        s.border = new RectOffset(borda, borda, borda, borda);
    }

    // estilo so com fundo (janela, painel lateral); null se o PNG nao existir
    public static GUIStyle Estilo(string nome, int borda)
    {
        Texture2D t = Tex(nome);
        if (t == null) return null;
        GUIStyle s = new GUIStyle();
        s.normal.background = t;
        s.border = new RectOffset(borda, borda, borda, borda);
        return s;
    }
}
