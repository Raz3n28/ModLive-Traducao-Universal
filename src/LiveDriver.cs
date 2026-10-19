using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fica de olho em todo texto que aparece na tela, troca pela traducao (se ja existe) ou pede ao motor.
// Roda por ultimo no quadro (ordem 32000): pega o texto que o jogo acabou de escrever antes de desenhar.
[DefaultExecutionOrder(32000)]
internal class LiveDriver : MonoBehaviour
{
    private readonly List<TMP_Text> fila = new List<TMP_Text>();
    // textos ja traduzidos: se o jogo reescrever o original, troca no mesmo quadro (sem piscar)
    private readonly List<TMP_Text> vigiados = new List<TMP_Text>();
    private readonly HashSet<int> idsVigiados = new HashSet<int>();
    // o mesmo para os textos do Unity antigo (UI.Text), que nao avisam quando mudam
    private readonly List<Text> vigiadosUi = new List<Text>();
    private readonly HashSet<int> idsVigiadosUi = new HashSet<int>();
    private const int MaxVigiados = 400;
    private readonly HashSet<int> naFila = new HashSet<int>();
    private Action<UnityEngine.Object> aoMudar;
    private float proximaVarredura;
    private float ultimaVarreduraRapida;
    private float proximoSalvar;
    private float proximaFonte;
    private bool cenaNova;
    private int versaoFonte;

    // para quem precisa rodar uma corrotina fora do motor (ex.: ClassificadorIA, chamado pelo menu)
    public static LiveDriver Instancia;

    private void Start()
    {
        Instancia = this;
        try
        {
            aoMudar = AoMudarTexto;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(aoMudar);
            LiveLog.Info("lg_ativo");
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_tmp_evento", e);
        }
        proximaVarredura = Time.unscaledTime + 1f;
        try { SceneManager.sceneLoaded += AoCarregarCena; }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "scene event"); }
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        cenaNova = true;
    }

    private void OnDestroy()
    {
        try { if (aoMudar != null) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(aoMudar); } catch { }
        try { SceneManager.sceneLoaded -= AoCarregarCena; } catch { }
        try { TradCache.Flush(); LiveConfig.SalvarSeSujo(); Glossario.SalvarSeSujo(); } catch { }
    }

    private void OnApplicationQuit()
    {
        try { TradCache.Flush(); LiveConfig.SalvarSeSujo(); Glossario.SalvarSeSujo(); } catch { }
    }

    private void AoMudarTexto(UnityEngine.Object obj)
    {
        TMP_Text t = obj as TMP_Text;
        if ((object)t == null) return;
        if (naFila.Add(t.GetInstanceID())) fila.Add(t);
    }

    private void OnGUI()
    {
        try { LiveMenu.Desenhar(this); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "menu"); }
        try { PublicadorWorkshop.Desenhar(); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "publish panel"); }
    }

    private void LateUpdate()
    {
        float agora = Time.unscaledTime;

        try { PublicadorWorkshop.Tick(); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "publishing"); }

        try { LiveMotor.Tick(this); }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "engine"); }

        if (agora >= proximoSalvar)
        {
            proximoSalvar = agora + 10f;
            LiveConfig.SalvarSeSujo();
            Glossario.SalvarSeSujo();
        }

        // memorias esvaziadas (limpar cache, trocar idioma...): devolve o texto original antes de tudo,
        // senao o mod trataria a traducao antiga como texto novo
        if (LiveMotor.RestaurarPendente) RestaurarOriginais();
        // traducoes provisorias que acabaram de ser retraduzidas: troca na tela pela nova
        if (LiveMotor.AtualizarPendente) AtualizarProvisorios();

        if (versaoFonte != FonteEscolhida.Versao) ReaplicarFonte();

        if (!LiveConfig.Ativo) { fila.Clear(); naFila.Clear(); return; }

        Vigiar();
        if (fila.Count > 0)
        {
            for (int i = 0; i < fila.Count; i++)
            {
                TMP_Text t = fila[i];
                if (!t) continue;
                FonteReserva.VerificarFonte(t.font);
                if (!Aplicar(t)) EncaixeFonte.Verificar(t);
            }
            fila.Clear();
            naFila.Clear();
        }

        bool rapida = LiveMotor.VarrerAgora && agora - ultimaVarreduraRapida > 0.5f;
        if (agora >= proximaVarredura || rapida)
        {
            proximaVarredura = agora + 3f;
            if (rapida) { LiveMotor.VarrerAgora = false; ultimaVarreduraRapida = agora; }
            Varrer();
        }

        // Procurar fontes em TODOS os objetos carregados e caro: so quando uma cena carrega e, por garantia,
        // a cada 30 s. Fonte nova que aparece num texto e ligada na hora pela varredura (FonteReserva.VerificarFonte).
        if (cenaNova || agora >= proximaFonte)
        {
            cenaNova = false;
            proximaFonte = agora + 30f;
            FonteReserva.Verificar();
        }
    }

    // Lista de vigiados cheia: tira so o mais antigo (antes apagava tudo e os textos piscavam o original).
    private void VigiarTmp(TMP_Text t)
    {
        if (!idsVigiados.Add(t.GetInstanceID())) return;
        if (vigiados.Count >= MaxVigiados)
        {
            idsVigiados.Remove(vigiados[0].GetInstanceID());
            vigiados.RemoveAt(0);
        }
        vigiados.Add(t);
    }

    private void VigiarUi(Text t)
    {
        if (!idsVigiadosUi.Add(t.GetInstanceID())) return;
        if (vigiadosUi.Count >= MaxVigiados)
        {
            idsVigiadosUi.Remove(vigiadosUi[0].GetInstanceID());
            vigiadosUi.RemoveAt(0);
        }
        vigiadosUi.Add(t);
    }

    // A escolha de fonte mudou no menu: devolve a original de todo texto trocado e aplica a nova nos traduzidos.
    private void ReaplicarFonte()
    {
        versaoFonte = FonteEscolhida.Versao;
        FonteEscolhida.DevolverTodas();
        EncaixeFonte.Reverificar();
        if (!FonteEscolhida.Ligada) return;
        for (int i = 0; i < vigiados.Count; i++) if (vigiados[i]) FonteEscolhida.Trocar(vigiados[i]);
        for (int i = 0; i < vigiadosUi.Count; i++) if (vigiadosUi[i]) FonteEscolhida.Trocar(vigiadosUi[i]);
    }

    // Botao "Pre-traduzir" (aba Avancado): poe na fila o texto de tudo que ja esta carregado na memoria,
    // inclusive janelas e menus escondidos. Nao troca nada na tela: quando o texto aparecer, ja vem do cache.
    public static int PreTraduzir()
    {
        int n = 0;
        int antes = LiveMotor.NaFila;
        try
        {
            UnityEngine.Object[] tmps = Resources.FindObjectsOfTypeAll(typeof(TMP_Text));
            for (int i = 0; i < tmps.Length; i++)
            {
                TMP_Text t = tmps[i] as TMP_Text;
                if (!t) continue;
                string s = t.text;
                if (!string.IsNullOrEmpty(s)) { LiveMotor.Traduzir(s, t); n++; }
            }
            UnityEngine.Object[] txts = Resources.FindObjectsOfTypeAll(typeof(Text));
            for (int i = 0; i < txts.Length; i++)
            {
                Text t = txts[i] as Text;
                if (!t) continue;
                string s = t.text;
                if (!string.IsNullOrEmpty(s)) { LiveMotor.Traduzir(s, t); n++; }
            }
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "pre-translate"); }
        int novos = Mathf.Max(0, LiveMotor.NaFila - antes);
        LiveLog.Info("lg_pretrad", n, novos);
        return novos;
    }

    // Retorna true se trocou o texto (ai o ajuste de fonte espera o proximo aviso de mudanca).
    private bool Aplicar(TMP_Text t)
    {
        try
        {
            string s = t.text;
            string r = LiveMotor.Traduzir(s, t);
            if (r != null && r != s)
            {
                t.text = r;
                FonteEscolhida.Trocar(t);
                VigiarTmp(t);
                return true;
            }
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_parte", e, "translate TMP text");
        }
        return false;
    }

    private void Vigiar()
    {
        for (int i = vigiados.Count - 1; i >= 0; i--)
        {
            TMP_Text t = vigiados[i];
            if (!t)
            {
                idsVigiados.Remove(t.GetInstanceID());
                vigiados.RemoveAt(i);
                continue;
            }
            if (!t.isActiveAndEnabled) continue;
            try
            {
                string s = t.text;
                if (LiveMotor.Nossa(s)) continue;
                string r = LiveMotor.Traduzir(s, t);
                if (r != null && r != s) { t.text = r; FonteEscolhida.Trocar(t); }
            }
            catch { }
        }
        for (int i = vigiadosUi.Count - 1; i >= 0; i--)
        {
            Text t = vigiadosUi[i];
            if (!t)
            {
                idsVigiadosUi.Remove(t.GetInstanceID());
                vigiadosUi.RemoveAt(i);
                continue;
            }
            if (!t.isActiveAndEnabled) continue;
            try
            {
                string s = t.text;
                if (LiveMotor.Nossa(s)) continue;
                string r = LiveMotor.Traduzir(s, t);
                if (r != null && r != s) { t.text = r; FonteEscolhida.Trocar(t); }
            }
            catch { }
        }
    }

    // Troca de volta pelo original todo texto que o mod tinha traduzido: os vigiados (mesmo escondidos)
    // e os que estao ativos na tela agora. Depois a varredura normal traduz tudo de novo.
    private void RestaurarOriginais()
    {
        int n = 0;
        for (int i = vigiados.Count - 1; i >= 0; i--)
        {
            TMP_Text t = vigiados[i];
            if (!t) continue;
            try
            {
                string o = LiveMotor.Original(t.text);
                if (o != null) { t.text = o; n++; }
            }
            catch { }
        }
        for (int i = vigiadosUi.Count - 1; i >= 0; i--)
        {
            Text t = vigiadosUi[i];
            if (!t) continue;
            try
            {
                string o = LiveMotor.Original(t.text);
                if (o != null) { t.text = o; n++; }
            }
            catch { }
        }
        try
        {
            UnityEngine.Object[] tmps = UnityEngine.Object.FindObjectsOfType(typeof(TMP_Text));
            for (int i = 0; i < tmps.Length; i++)
            {
                TMP_Text t = tmps[i] as TMP_Text;
                if (!t) continue;
                string o = LiveMotor.Original(t.text);
                if (o != null) { t.text = o; n++; }
            }
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "restore TMP"); }
        try
        {
            UnityEngine.Object[] txts = UnityEngine.Object.FindObjectsOfType(typeof(Text));
            for (int i = 0; i < txts.Length; i++)
            {
                Text t = txts[i] as Text;
                if (!t) continue;
                string o = LiveMotor.Original(t.text);
                if (o != null) { t.text = o; n++; }
            }
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "restore UI.Text"); }

        FonteEscolhida.DevolverTodas();
        EncaixeFonte.DevolverTodos();
        vigiados.Clear();
        idsVigiados.Clear();
        vigiadosUi.Clear();
        idsVigiadosUi.Clear();
        fila.Clear();
        naFila.Clear();
        LiveMotor.FimRestaurar();
        LiveMotor.VarrerAgora = true;
        LiveLog.Info("lg_restaurado", n);
    }

    private void AtualizarProvisorios()
    {
        int n = 0;
        for (int i = vigiados.Count - 1; i >= 0; i--)
        {
            TMP_Text t = vigiados[i];
            if (!t) continue;
            try
            {
                string s = t.text;
                string r = LiveMotor.Retraduzir(s);
                if (r != null && r != s) { t.text = r; n++; }
            }
            catch { }
        }
        for (int i = vigiadosUi.Count - 1; i >= 0; i--)
        {
            Text t = vigiadosUi[i];
            if (!t) continue;
            try
            {
                string s = t.text;
                string r = LiveMotor.Retraduzir(s);
                if (r != null && r != s) { t.text = r; n++; }
            }
            catch { }
        }
        try
        {
            UnityEngine.Object[] tmps = UnityEngine.Object.FindObjectsOfType(typeof(TMP_Text));
            for (int i = 0; i < tmps.Length; i++)
            {
                TMP_Text t = tmps[i] as TMP_Text;
                if (!t) continue;
                string s = t.text;
                string r = LiveMotor.Retraduzir(s);
                if (r != null && r != s) { t.text = r; n++; }
            }
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "update TMP"); }
        try
        {
            UnityEngine.Object[] txts = UnityEngine.Object.FindObjectsOfType(typeof(Text));
            for (int i = 0; i < txts.Length; i++)
            {
                Text t = txts[i] as Text;
                if (!t) continue;
                string s = t.text;
                string r = LiveMotor.Retraduzir(s);
                if (r != null && r != s) { t.text = r; n++; }
            }
        }
        catch (Exception e) { LiveLog.Erro("le_parte", e, "update UI.Text"); }
        LiveMotor.FimAtualizar();
        if (n > 0) LiveLog.Info("lg_melhorada", n);
    }

    private void Varrer()
    {
        try
        {
            UnityEngine.Object[] tmps = UnityEngine.Object.FindObjectsOfType(typeof(TMP_Text));
            for (int i = 0; i < tmps.Length; i++)
            {
                TMP_Text t = tmps[i] as TMP_Text;
                if (!t) continue;
                FonteReserva.VerificarFonte(t.font);
                if (!Aplicar(t)) EncaixeFonte.Verificar(t);
            }
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_parte", e, "scan TMP");
        }

        try
        {
            UnityEngine.Object[] txts = UnityEngine.Object.FindObjectsOfType(typeof(Text));
            for (int i = 0; i < txts.Length; i++)
            {
                Text t = txts[i] as Text;
                if (!t) continue;
                string s = t.text;
                string r = LiveMotor.Traduzir(s, t);
                if (r != null && r != s)
                {
                    t.text = r;
                    FonteEscolhida.Trocar(t);
                    VigiarUi(t);
                }
                else EncaixeFonte.Verificar(t);
            }
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_parte", e, "scan UI.Text");
        }
    }
}
