using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Traduz texto desenhado por IMGUI (OnGUI: GUI.Label, GUI.Button, GUILayout...), que nao fica em nenhum
// componente e por isso a varredura do LiveDriver nao enxerga. E o caso de janelas de outros mods
// (ex.: o editor de personagens). Usa o Harmony: o que ja estiver carregado por outro mod ou o 0Harmony.dll
// que vai junto na pasta Assemblies. Tudo por reflexao: sem Harmony o mod funciona igual, so sem esta parte.
internal static class LiveImgui
{
    // ligado durante o OnGUI do proprio mod (menu F8, painel da Workshop): esses textos nao se traduzem
    public static bool Pausado;

    private static readonly string[] NomesGUI = new string[] { "DoLabel", "Box", "DoButton", "DoRepeatButton", "DoToggle", "DoWindow", "DoModalWindow", "DoButtonGrid" };
    private static readonly string[] NomesLayout = new string[] { "DoLabel", "Box", "DoButton", "DoRepeatButton", "DoToggle", "DoWindow", "Toolbar", "SelectionGrid" };

    public static void Iniciar()
    {
        Assembly harmony = AcharHarmony();
        if (harmony == null)
        {
            LiveLog.Aviso("lg_imgui_sem");
            return;
        }
        Type tHarmony = harmony.GetType("HarmonyLib.Harmony");
        Type tMetodo = harmony.GetType("HarmonyLib.HarmonyMethod");
        if (tHarmony == null || tMetodo == null)
        {
            LiveLog.Aviso("lg_imgui_sem");
            return;
        }
        object h = Activator.CreateInstance(tHarmony, new object[] { "pauloraz3n.outerspace.live.imgui" });
        MethodInfo patch = null;
        foreach (MethodInfo m in tHarmony.GetMethods())
        {
            if (m.Name != "Patch") continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length >= 2 && ps[0].ParameterType == typeof(MethodBase) && ps[1].ParameterType == tMetodo) { patch = m; break; }
        }
        ConstructorInfo novoMetodo = tMetodo.GetConstructor(new Type[] { typeof(MethodInfo) });
        if (patch == null || novoMetodo == null)
        {
            LiveLog.Aviso("lg_imgui_sem");
            return;
        }

        hInst = h; hPatch = patch; hNovo = novoMetodo;
        int n = 0;
        n += Aplicar(typeof(GUI), NomesGUI, h, patch, novoMetodo);
        n += Aplicar(typeof(GUILayout), NomesLayout, h, patch, novoMetodo);
        LiveLog.Info("lg_imgui", n);
    }

    private static object hInst;
    private static MethodInfo hPatch;
    private static ConstructorInfo hNovo;

    // usado por outras partes do mod (ex.: EscalaUI): poe um prefixo do Harmony em "alvo". false = sem Harmony ou falhou
    public static bool Prefixar(MethodBase alvo, MethodInfo prefixo)
    {
        if (hInst == null || alvo == null || prefixo == null) return false;
        try
        {
            object pre = hNovo.Invoke(new object[] { prefixo });
            object[] args = new object[hPatch.GetParameters().Length];
            args[0] = alvo;
            args[1] = pre;
            hPatch.Invoke(hInst, args);
            return true;
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_parte", e, "Harmony " + alvo.Name);
            return false;
        }
    }

    private static int Aplicar(Type tipo, string[] nomes, object h, MethodInfo patch, ConstructorInfo novoMetodo)
    {
        const BindingFlags F = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        int n = 0;
        foreach (MethodInfo m in tipo.GetMethods(F))
        {
            if (Array.IndexOf(nomes, m.Name) < 0) continue;
            string prefixo = Prefixo(m);
            if (prefixo == null) continue;
            try
            {
                object pre = novoMetodo.Invoke(new object[] { typeof(LiveImgui).GetMethod(prefixo, BindingFlags.Static | BindingFlags.NonPublic) });
                object[] args = new object[patch.GetParameters().Length];
                args[0] = m;
                args[1] = pre;
                patch.Invoke(h, args);
                n++;
            }
            catch (Exception e)
            {
                LiveLog.Erro("le_parte", e, "IMGUI " + tipo.Name + "." + m.Name);
            }
        }
        return n;
    }

    // so funcoes que recebem o texto ja como GUIContent (as versoes com string chamam estas)
    private static string Prefixo(MethodInfo m)
    {
        foreach (ParameterInfo p in m.GetParameters())
        {
            if (p.ParameterType == typeof(GUIContent) && p.Name == "content") return "PreContent";
            if (p.ParameterType == typeof(GUIContent) && p.Name == "title") return "PreTitle";
            if (p.ParameterType == typeof(GUIContent[]) && p.Name == "contents") return "PreContents";
        }
        return null;
    }

    private static Assembly AcharHarmony()
    {
        Assembly melhor = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            AssemblyName n = a.GetName();
            if (n.Name != "0Harmony" || a.GetType("HarmonyLib.Harmony") == null) continue;
            if (melhor == null || n.Version > melhor.GetName().Version) melhor = a;
        }
        if (melhor != null) return melhor;

        List<string> pastas = new List<string>();
        try
        {
            string loc = typeof(LiveImgui).Assembly.Location;
            if (!string.IsNullOrEmpty(loc)) pastas.Add(Path.GetDirectoryName(loc));
        }
        catch { }
        if (!string.IsNullOrEmpty(LiveBoot.PastaMod))
        {
            pastas.Add(Path.Combine(Path.Combine(LiveBoot.PastaMod, "v_common"), "Assemblies"));
            pastas.Add(LiveBoot.PastaMod);
        }
        foreach (string p in pastas)
        {
            string arq = Path.Combine(p, "0Harmony.dll");
            if (!File.Exists(arq)) continue;
            try { return Assembly.LoadFrom(arq); }
            catch (Exception e) { LiveLog.Erro("le_parte", e, "load 0Harmony.dll"); }
        }
        return null;
    }

    private static void PreContent(ref GUIContent content)
    {
        content = Trocar(content);
    }

    private static void PreTitle(ref GUIContent title)
    {
        title = Trocar(title);
    }

    private static void PreContents(ref GUIContent[] contents)
    {
        if (Pausado || contents == null) return;
        for (int i = 0; i < contents.Length; i++) Trocar(contents[i]);
    }

    private static GUIContent Trocar(GUIContent c)
    {
        if (Pausado || c == null) return c;
        try
        {
            string s = c.text;
            if (string.IsNullOrEmpty(s) || !LiveConfig.Ativo) return c;
            string r = LiveMotor.Traduzir(s);
            if (r == null || r == s) return c;
            // troca so o texto no proprio GUIContent (a DLL cortada do jogo nao deixa ler a imagem para copiar).
            // Se o outro mod guardar esse objeto, no proximo quadro o texto ja e nosso e o motor deixa como esta.
            c.text = r;
            return c;
        }
        catch
        {
            return c;
        }
    }
}
