using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Script.PublicChannel.Scripts.Steam;
using Steamworks;
using UnityEngine;

// Envia o mod para a Workshop usando o proprio sistema Steam do jogo.
// So roda quando o jogo e aberto com o parametro -livepublicar (o Publicar.bat faz isso)
// e o mod esta na pasta local "mods" (nunca a partir de uma copia baixada da Workshop).
// Enquanto publica, mostra um painel na tela com o status e o progresso do envio.
internal static class PublicadorWorkshop
{
    private const string Flag = "-livepublicar";
    private const string Titulo = "Universal Translator";

    private static bool feito;
    private static bool rodando;
    private static int armado = -1;
    private static bool avisouEspera;

    // ---- painel na tela ----
    private static readonly List<string> linhas = new List<string>();
    private static string estado = "Iniciando...";
    private static int resultado; // 0 = andando, 1 = sucesso, 2 = falha
    private static float inicio = -1f;
    private static float inicioEnvio = -1f;
    private static float ultimaMudanca = -1f;
    private static ulong bytesFeitos, bytesTotal;
    private static string fase = "";
    private static string faseAnterior = "";
    private static object ugcAtual;
    private static FieldInfo campoHandle;
    private static bool procurouHandle;
    private static float proximoStatusArq;
    private static GUIStyle estiloTexto, estiloTitulo;
    private static Texture2D texFundo, texBarra, texBarraFundo;

    private static bool RodandoDaWorkshop
    {
        get
        {
            string p = LiveBoot.PastaMod;
            if (string.IsNullOrEmpty(p)) return false;
            return p.Replace('\\', '/').ToLowerInvariant().IndexOf("workshop/content") >= 0;
        }
    }

    private static bool Armado()
    {
        if (armado < 0)
        {
            armado = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                    if (args[i] != null && args[i].ToLowerInvariant() == Flag) armado = 1;
            }
            catch { }
        }
        return armado == 1;
    }

    public static void Tick()
    {
        if (Armado() && inicio < 0f)
        {
            inicio = Time.realtimeSinceStartup;
            Passo("Modo de publicacao ligado (" + Titulo + ")");
        }
        if (rodando) AtualizarProgresso();
        if (Armado() && Time.realtimeSinceStartup >= proximoStatusArq)
        {
            proximoStatusArq = Time.realtimeSinceStartup + 1f;
            SalvarStatus();
        }

        if (feito || rodando) return;
        if (!Armado()) { feito = true; return; }

        if (RodandoDaWorkshop || string.IsNullOrEmpty(LiveBoot.PastaMod))
        {
            feito = true;
            Relatar("Publicacao ignorada: o mod precisa estar na pasta local 'mods' do jogo.", false);
            return;
        }

        ChannelSteam steam = ChannelSteam.Instance;
        if ((object)steam == null || !steam || !steam.workshopInitialized)
        {
            if (!avisouEspera) { avisouEspera = true; Passo("Esperando a Steam iniciar..."); }
            estado = "Esperando a Steam iniciar... (" + Segundos(inicio) + ")";
            return;
        }

        UGCManager ugc = PegarUgc(steam);
        if (ugc == null) { feito = true; return; }

        rodando = true;
        try
        {
            ulong id = LerId();
            if (id != 0)
            {
                Passo("Steam pronta. Atualizando o item " + id);
                Enviar(ugc, id, false);
            }
            else
            {
                Passo("Steam pronta. Criando item novo na Workshop...");
                estado = "Criando o item na Workshop...";
                ugc.createItem(new Action<UGCManager.OperationResult>(AoCriar));
            }
        }
        catch (Exception e)
        {
            Relatar("FALHOU ao iniciar a publicacao: " + e.Message, false);
            feito = true;
            rodando = false;
        }
    }

    private static UGCManager PegarUgc(ChannelSteam steam)
    {
        try
        {
            FieldInfo f = typeof(ChannelSteam).GetField("ugc", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            object o = f != null ? f.GetValue(steam) : null;
            UGCManager u = o as UGCManager;
            if (u == null) Relatar("FALHOU: nao achei o gerenciador da Workshop do jogo.", false);
            return u;
        }
        catch (Exception e)
        {
            Relatar("FALHOU ao acessar a Workshop: " + e.Message, false);
            return null;
        }
    }

    private static void AoCriar(UGCManager.OperationResult r)
    {
        if (r == null) { Relatar("FALHOU: a Steam nao respondeu ao criar o item.", false); Fim(); return; }
        if (r.needsLegalAgreement)
        {
            Relatar("A Steam pede que voce aceite o Acordo Legal da Workshop. Abra a pagina da Workshop do Outer Space na Steam, aceite o acordo e rode o Publicar.bat de novo.", false);
            Fim();
            return;
        }
        ulong id = r.itemId.m_PublishedFileId;
        if (!r.success || id == 0)
        {
            Relatar("FALHOU ao criar o item (" + r.result + " " + (r.error ?? "") + ")", false);
            Fim();
            return;
        }
        SalvarId(id);
        Passo("Item criado: " + id);
        try
        {
            ChannelSteam steam = ChannelSteam.Instance;
            UGCManager ugc = PegarUgc(steam);
            if (ugc == null) { Fim(); return; }
            Enviar(ugc, id, true);
        }
        catch (Exception e)
        {
            Relatar("FALHOU ao enviar: " + e.Message, false);
            Fim();
        }
    }

    private static void Enviar(UGCManager ugc, ulong id, bool novo)
    {
        string pasta = LiveBoot.PastaMod;
        if (!novo)
        {
            EnviarSoArquivos(id, pasta);
            return;
        }
        // item novo: usa o sistema do jogo (titulo, descricao, capa e dados que o jogo precisa)
        UGCManager.ItemUpdate up = new UGCManager.ItemUpdate();
        up.title = Titulo;
        up.description = LerDescricao();
        up.contentFolder = pasta;
        up.previewFile = Path.Combine(Path.Combine(pasta, "Manifest"), "preview.png");
        up.tags = new List<string>();
        up.visibility = new ERemoteStoragePublishedFileVisibility?((ERemoteStoragePublishedFileVisibility)2); // privado na primeira vez
        Passo("Enviando arquivos de " + pasta + " (" + TamanhoPasta(pasta) + ")");
        estado = "Enviando para a Steam...";
        ugcAtual = ugc;
        inicioEnvio = Time.realtimeSinceStartup;
        ultimaMudanca = inicioEnvio;
        ugc.updateItem(new PublishedFileId_t(id), up, new Action<UGCManager.OperationResult>(AoAtualizar), "Primeira versao");
    }

    // Atualizacao: manda os arquivos do mod e a imagem de capa (Manifest\preview.png). Titulo e descricao
    // nao sao tocados (edite direto na pagina da Workshop), para a Steam nao colocar o item em analise de novo.
    private static CallResult<SubmitItemUpdateResult_t> retornoEnvio;
    private static UGCUpdateHandle_t handleDireto = UGCUpdateHandle_t.Invalid;
    private static ulong idEnvio;

    private static void EnviarSoArquivos(ulong id, string pasta)
    {
        idEnvio = id;
        Passo("Enviando os arquivos e a imagem de capa (titulo e descricao ficam como estao na Steam)");
        Passo("Pasta: " + pasta + " (" + TamanhoPasta(pasta) + ")");
        UGCUpdateHandle_t h = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), new PublishedFileId_t(id));
        if (h == UGCUpdateHandle_t.Invalid)
        {
            Relatar("FALHOU: a Steam nao aceitou iniciar a atualizacao do item " + id + ".", false);
            Fim();
            return;
        }
        if (!SteamUGC.SetItemContent(h, pasta))
        {
            Relatar("FALHOU: a Steam nao aceitou a pasta do mod: " + pasta, false);
            Fim();
            return;
        }
        string capa = Path.Combine(Path.Combine(pasta, "Manifest"), "preview.png");
        if (File.Exists(capa))
        {
            if (SteamUGC.SetItemPreview(h, capa)) Passo("Imagem de capa: " + capa);
            else Passo("AVISO: a Steam nao aceitou a imagem de capa (os arquivos vao mesmo assim).");
        }
        handleDireto = h;
        estado = "Enviando para a Steam...";
        inicioEnvio = Time.realtimeSinceStartup;
        ultimaMudanca = inicioEnvio;
        SteamAPICall_t call = SteamUGC.SubmitItemUpdate(h, "Atualizacao do mod");
        retornoEnvio = CallResult<SubmitItemUpdateResult_t>.Create(new CallResult<SubmitItemUpdateResult_t>.APIDispatchDelegate(AoEnviarDireto));
        retornoEnvio.Set(call, null);
    }

    private static void AoEnviarDireto(SubmitItemUpdateResult_t r, bool falhaIO)
    {
        if (!falhaIO && r.m_eResult == EResult.k_EResultOK)
        {
            if (r.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                Relatar("Enviado, mas a Steam pede que voce aceite o Acordo Legal da Workshop para o item ficar visivel.", true);
            else
                Relatar("SUCESSO! Mod enviado para a Workshop. Item: " + idEnvio + "  https://steamcommunity.com/sharedfiles/filedetails/?id=" + idEnvio, true);
        }
        else
        {
            string res = falhaIO ? "falha de conexao" : r.m_eResult.ToString();
            string dica = "";
            if (res.IndexOf("FileNotFound") >= 0) dica = " O item foi apagado na Steam? Apague o arquivo " + IdPath() + " e publique de novo.";
            else if (res.IndexOf("Busy") >= 0) dica = " A Steam esta ocupada; tente de novo em alguns minutos.";
            else if (res.IndexOf("Timeout") >= 0 || falhaIO) dica = " A conexao com a Steam caiu no meio; tente de novo.";
            Relatar("FALHOU ao enviar (" + res + ")." + dica, false);
        }
        Fim();
    }

    private static void AoAtualizar(UGCManager.OperationResult r)
    {
        if (r != null && r.success)
        {
            ulong id = r.itemId.m_PublishedFileId;
            Relatar("SUCESSO! Mod enviado para a Workshop. Item: " + id + "  https://steamcommunity.com/sharedfiles/filedetails/?id=" + id, true);
        }
        else if (r != null && r.needsLegalAgreement)
        {
            Relatar("Enviado, mas a Steam pede que voce aceite o Acordo Legal da Workshop para o item ficar visivel.", true);
        }
        else
        {
            string res = r != null ? r.result.ToString() : "sem resposta";
            string dica = "";
            if (res.IndexOf("FileNotFound") >= 0) dica = " O item foi apagado na Steam? Apague o arquivo " + IdPath() + " e publique de novo.";
            else if (res.IndexOf("Busy") >= 0) dica = " A Steam esta ocupada; tente de novo em alguns minutos.";
            else if (res.IndexOf("Timeout") >= 0) dica = " A conexao com a Steam caiu no meio; tente de novo.";
            Relatar("FALHOU ao enviar (" + res + " " + (r != null ? (r.error ?? "") : "") + ")." + dica, false);
        }
        Fim();
    }

    private static void Fim()
    {
        rodando = false;
        feito = true;
        SalvarStatus();
    }

    // ---------------- progresso do envio ----------------

    // O jogo guarda o "handle" do envio dentro do UGCManager; procuramos o campo por tipo.
    private static void AtualizarProgresso()
    {
        if (inicioEnvio < 0f) return;
        try
        {
            if (handleDireto != UGCUpdateHandle_t.Invalid) { LerProgresso(handleDireto); return; }
            if (ugcAtual == null) return;
            if (!procurouHandle)
            {
                procurouHandle = true;
                FieldInfo[] fs = ugcAtual.GetType().GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fs.Length; i++)
                    if (fs[i].FieldType == typeof(UGCUpdateHandle_t)) { campoHandle = fs[i]; break; }
                if (campoHandle == null) Passo("(a Steam nao informa a porcentagem neste jogo; mostrando so o tempo)");
            }
            if (campoHandle == null) return;
            UGCUpdateHandle_t h = (UGCUpdateHandle_t)campoHandle.GetValue(campoHandle.IsStatic ? null : ugcAtual);
            if (h.m_UGCUpdateHandle == 0 || h == UGCUpdateHandle_t.Invalid) return;
            LerProgresso(h);
        }
        catch { campoHandle = null; }
    }

    private static void LerProgresso(UGCUpdateHandle_t h)
    {
        try
        {
            ulong feitos, total;
            EItemUpdateStatus st = SteamUGC.GetItemUpdateProgress(h, out feitos, out total);
            string nova = NomeFase(st);
            if (nova != faseAnterior && nova.Length > 0)
            {
                faseAnterior = nova;
                Passo(nova);
            }
            fase = nova;
            if (feitos != bytesFeitos || total != bytesTotal) ultimaMudanca = Time.realtimeSinceStartup;
            bytesFeitos = feitos;
            bytesTotal = total;
        }
        catch { }
    }

    private static string NomeFase(EItemUpdateStatus st)
    {
        switch ((int)st)
        {
            case 1: return "Preparando as informacoes do item";
            case 2: return "Preparando os arquivos (a Steam esta compactando)";
            case 3: return "Enviando os arquivos";
            case 4: return "Enviando a imagem de capa";
            case 5: return "Finalizando (a Steam esta gravando as mudancas)";
            default: return "";
        }
    }

    // ---------------- painel na tela (chamado pelo OnGUI do LiveDriver) ----------------

    public static void Desenhar()
    {
        if (!Armado() || inicio < 0f) return;
        PrepararEstilos();

        float w = Mathf.Min(720f, Screen.width - 40f);
        int mostrar = Mathf.Min(linhas.Count, 10);
        float h = 150f + mostrar * 24f;
        Rect caixa = new Rect(20f, 20f, w, h);
        GUI.DrawTexture(caixa, texFundo);

        float y = caixa.y + 12f;
        float x = caixa.x + 16f;
        float lw = w - 32f;

        Color cor = resultado == 1 ? new Color(0.45f, 1f, 0.45f) : resultado == 2 ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.84f, 0.3f);
        estiloTitulo.normal.textColor = cor;
        GUI.Label(new Rect(x, y, lw, 30f), "PUBLICANDO NA WORKSHOP - " + Titulo, estiloTitulo);
        y += 32f;

        string tempo = inicioEnvio > 0f && resultado == 0 ? "  |  envio: " + Segundos(inicioEnvio) : "";
        GUI.Label(new Rect(x, y, lw, 26f), estado + tempo, estiloTexto);
        y += 28f;

        // barra de progresso
        Rect barra = new Rect(x, y, lw, 22f);
        GUI.DrawTexture(barra, texBarraFundo);
        float p = resultado == 1 ? 1f : (bytesTotal > 0 ? (float)((double)bytesFeitos / bytesTotal) : 0f);
        if (p > 0f) GUI.DrawTexture(new Rect(barra.x, barra.y, barra.width * Mathf.Clamp01(p), barra.height), texBarra);
        string txtBarra = resultado == 1 ? "100%" :
            bytesTotal > 0 ? (p * 100f).ToString("0") + "%  (" + Mb(bytesFeitos) + " de " + Mb(bytesTotal) + ")" :
            (fase.Length > 0 ? fase : (resultado == 0 ? "aguardando..." : ""));
        GUI.Label(new Rect(barra.x + 8f, barra.y, barra.width - 16f, barra.height), txtBarra, estiloTexto);
        y += 30f;

        if (resultado == 0 && inicioEnvio > 0f && ultimaMudanca > 0f && Time.realtimeSinceStartup - ultimaMudanca > 180f)
        {
            GUI.Label(new Rect(x, y, lw, 24f), "Sem progresso ha " + Segundos(ultimaMudanca) + ". Confira a internet e o download/upload da Steam.", estiloTexto);
        }
        y += 26f;

        for (int i = linhas.Count - mostrar; i < linhas.Count; i++)
        {
            GUI.Label(new Rect(x, y, lw, 24f), linhas[i], estiloTexto);
            y += 24f;
        }
        if (resultado != 0)
            GUI.Label(new Rect(x, caixa.yMax - 26f, lw, 24f), "Pode fechar o jogo.", estiloTexto);
    }

    private static void PrepararEstilos()
    {
        if (estiloTexto != null) return;
        texFundo = Cor(new Color(0f, 0f, 0f, 0.82f));
        texBarraFundo = Cor(new Color(1f, 1f, 1f, 0.15f));
        texBarra = Cor(new Color(0.25f, 0.65f, 1f, 0.9f));
        estiloTexto = new GUIStyle(GUI.skin.label);
        estiloTexto.fontSize = 16;
        estiloTexto.wordWrap = false;
        estiloTexto.clipping = TextClipping.Clip;
        estiloTexto.normal.textColor = Color.white;
        estiloTitulo = new GUIStyle(estiloTexto);
        estiloTitulo.fontSize = 20;
        estiloTitulo.fontStyle = FontStyle.Bold;
    }

    private static Texture2D Cor(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    private static void Passo(string msg)
    {
        string linha = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
        linhas.Add(linha);
        if (linhas.Count > 50) linhas.RemoveAt(0);
        LiveLog.Info("lg_publicacao", msg);
        SalvarStatus();
    }

    // live_status_publicacao.txt: o Publicar.bat mostra este arquivo enquanto espera
    private static void SalvarStatus()
    {
        try
        {
            string t = estado;
            if (bytesTotal > 0 && resultado == 0) t += "  " + ((double)bytesFeitos * 100.0 / bytesTotal).ToString("0") + "% (" + Mb(bytesFeitos) + " de " + Mb(bytesTotal) + ")";
            else if (fase.Length > 0 && resultado == 0) t += "  [" + fase + "]";
            if (inicioEnvio > 0f && resultado == 0) t += "  tempo: " + Segundos(inicioEnvio);
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "live_status_publicacao.txt"), t);
        }
        catch { }
    }

    private static string Segundos(float desde)
    {
        int s = (int)(Time.realtimeSinceStartup - desde);
        if (s < 0) s = 0;
        return (s / 60) + "m" + (s % 60).ToString("00") + "s";
    }

    private static string Mb(ulong b)
    {
        return ((double)b / (1024.0 * 1024.0)).ToString("0.0") + " MB";
    }

    private static string TamanhoPasta(string pasta)
    {
        try
        {
            long t = 0;
            string[] arqs = Directory.GetFiles(pasta, "*", SearchOption.AllDirectories);
            for (int i = 0; i < arqs.Length; i++) t += new FileInfo(arqs[i]).Length;
            return arqs.Length + " arquivos, " + Mb((ulong)t);
        }
        catch { return "?"; }
    }

    // ---------------- arquivos ----------------

    private static string LerDescricao()
    {
        try
        {
            string arq = Path.Combine(Path.Combine(LiveBoot.PastaMod, "v_common"), "workshop_description.txt");
            if (File.Exists(arq)) return File.ReadAllText(arq);
        }
        catch { }
        return "Universal Translator: translates the game in real time into 18 languages. Free mode or your own AI key.";
    }

    private static string IdPath()
    {
        return Path.Combine(Application.persistentDataPath, "live_workshop_item_id.txt");
    }

    private static ulong LerId()
    {
        try
        {
            string p = IdPath();
            if (!File.Exists(p)) return 0;
            ulong v;
            if (ulong.TryParse(File.ReadAllText(p).Trim(), out v)) return v;
        }
        catch { }
        return 0;
    }

    private static void SalvarId(ulong id)
    {
        try { File.WriteAllText(IdPath(), id.ToString()); } catch { }
    }

    private static void Relatar(string msg, bool ok)
    {
        resultado = ok ? 1 : 2;
        estado = ok ? "CONCLUIDO" : "FALHOU";
        Passo(msg);
        try
        {
            string linha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n";
            // fica em AppData\LocalLow\CatStudio\Outer Space (o Publicar.bat le dali)
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "live_resultado_publicacao.txt"), linha);
        }
        catch { }
    }
}
