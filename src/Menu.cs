using System;
using System.Collections.Generic;
using UnityEngine;

// Menu dentro do jogo (IMGUI). Abre pelo botao no canto da tela ou pela tecla F8.
// Visual "painel lateral": abas Geral, Provedor/API, Estilo, Glossario, Categorias, Avancado, Log e Fontes.
// Textos em 6 idiomas (LiveTxt). So usa funcoes de IMGUI que existem nas DLLs cortadas do jogo.
internal static class LiveMenu
{
    private const int MaxExtras = 300;
    private static int porPagina = 8;      // termos por pagina no Glossario (cresce com a janela)

    private static readonly Color Teal = new Color(0.18f, 0.83f, 0.75f, 1f);

    private static float larguraUtil = 560f;   // largura do conteudo das abas (para os botoes de escolha)
    private static bool aberto;
    private static int aba;                 // 0 Geral, 1 Provedor, 2 Estilo, 3 Glossario, 4 Categorias, 5 Avancado
    private static Vector2 rolagem;
    private static bool mostrarChave;
    private static string campoRpm;
    private static string campoLote;
    private static bool camposIniciados;
    private static Texture2D texFundo;
    private static Texture2D texLateral;
    // visual da proposta B: botoes planos arredondados, teal #2dd4bf no selecionado
    private static GUIStyle sBtn, sSel, sAmbar, sPerigo, sNav, sNavSel, sCampo, sMarcar, sBarra, sBarraPolegar;
    private static readonly Dictionary<Color, GUIStyle[]> chips = new Dictionary<Color, GUIStyle[]>();
    private static GUIStyle skBotao, skCampo, skMarcar, skBarra, skPolegar;
    private static GUIStyle sTitulo;
    private static GUIStyle sMarca;
    private static GUIStyle sPeq;
    private static GUIStyle sCab;
    private static GUIStyle sCel;
    private static GUIStyle sErro;
    private static GUIStyle sAviso;
    private static GUIStyle sOk;

    // glossario
    private static string busca = "";
    private static int filtroCat;           // 0 = todos; senao categoria + 1
    private static int pagina;
    private static TermoGlossario termoEdit;
    private static string campoEdit = "";
    private static TermoGlossario termoCat;     // termo com a lista de categorias aberta
    private static bool novoAberto;
    private static string novoOrig = "";
    private static string novoTrad = "";
    private static readonly List<TermoGlossario> filtrado = new List<TermoGlossario>();
    private static readonly int[] contagens = new int[Categorias.LimiteId + 1];

    // painel de categorias (aba Glossario)
    private static bool catNova;            // true = criando; false = editando a categoria catSel
    private static int catSel;
    private static string cNome = "";
    private static string cPal = "";
    private static string cInstr = "";
    private static string catMsg = "";
    private static bool catMsgErro;
    private static bool catConfApagar;
    private static int versaoFiltro = -1;
    private static string buscaFiltro;
    private static int catFiltro = -1;

    // aba Avancado
    private static bool confLimpar;         // true = o proximo clique em "Limpar cache" apaga de verdade
    private static string msgAvanc = "";
    private static bool msgAvancErro;

    public static void Desenhar(MonoBehaviour drv)
    {
        Event ev = Event.current;
        if (ev != null && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.F8)
        {
            aberto = !aberto;
            if (!aberto) Fechar();
            else camposIniciados = false;
            ev.Use();
        }

        float esc = Mathf.Clamp(Screen.height / 1080f, 0.85f, 2.5f);
        Matrix4x4 antigo = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(esc, esc, 1f));
        float sw = Screen.width / esc;
        float sh = Screen.height / esc;
        try
        {
            if (!aberto)
            {
                if (LiveConfig.BotaoVisivel) BotaoCanto(sw);
            }
            else
            {
                PrepararEstilos();
                TrocarSkin();
                try { Janela(drv, sw, sh); }
                finally { VoltarSkin(); }
            }
        }
        finally
        {
            GUI.matrix = antigo;
        }
    }

    private static void BotaoCanto(float sw)
    {
        Color antes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.55f);
        string rot = LiveTxt.T("canto") + (LiveMotor.NaFila > 0 ? " (" + LiveMotor.NaFila + ")" : "");
        if (GUI.Button(new Rect(sw - 150f, 4f, 146f, 24f), rot))
        {
            aberto = true;
            camposIniciados = false;
        }
        GUI.color = antes;
    }

    private static void Fechar()
    {
        aberto = false;
        confLimpar = false;
        termoEdit = null;
        termoCat = null;
        LiveConfig.SalvarSeSujo();
        Glossario.SalvarSeSujo();
        TradCache.Flush();
    }

    private static void Mudou()
    {
        LiveConfig.Marcar();
        LiveMotor.Reiniciar();
        LiveMotor.VarrerAgora = true;
    }

    // ---------------- janela ----------------

    private static void Janela(MonoBehaviour drv, float sw, float sh)
    {
        if (!camposIniciados)
        {
            camposIniciados = true;
            campoRpm = LiveConfig.Rpm.ToString();
            campoLote = LiveConfig.Lote.ToString();
        }

        // janela proporcional a tela: 65% da largura e 85% da altura (minimo 820 x 580)
        float w = Mathf.Min(Mathf.Max(820f, sw * 0.65f), sw - 20f);
        float h = Mathf.Min(Mathf.Max(580f, sh * 0.85f), sh - 40f);
        porPagina = Mathf.Clamp((int)((h - 380f) / 38f), 8, 20);
        Rect r = new Rect((sw - w) / 2f, 20f, w, h);
        GUI.DrawTexture(r, texFundo);
        Rect lat = new Rect(r.x, r.y, 190f, h);
        GUI.DrawTexture(lat, texLateral);
        Lateral(lat);

        larguraUtil = w - 214f - 34f;
        GUILayout.BeginArea(new Rect(r.x + 202f, r.y + 10f, w - 214f, h - 20f));

        GUILayout.BeginHorizontal();
        // ExpandWidth: o titulo ocupa o espaco todo e nao e mais cortado
        GUILayout.Label(TituloAba(), sTitulo, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(LiveTxt.T("fechar"), GUILayout.Width(110f))) Fechar();
        GUILayout.EndHorizontal();

        rolagem = GUILayout.BeginScrollView(rolagem, GUILayout.Height(h - 130f));
        switch (aba)
        {
            case 1: PaginaProvedor(drv); break;
            case 2: PaginaEstilo(); break;
            case 3: PaginaGlossario(); break;
            case 4: PaginaCategorias(); break;
            case 5: PaginaAvancado(); break;
            case 6: PaginaLog(); break;
            case 7: PaginaFontes(); break;
            default: PaginaGeral(); break;
        }
        GUILayout.EndScrollView();

        Rodape();
        GUILayout.EndArea();
    }

    private static string TituloAba()
    {
        switch (aba)
        {
            case 1: return LiveTxt.T("nav_prov");
            case 2: return LiveTxt.T("nav_estilo");
            case 3: return LiveTxt.T("nav_glos");
            case 4: return LiveTxt.T("nav_cats");
            case 5: return LiveTxt.T("nav_avanc");
            case 6: return LiveTxt.T("nav_log");
            case 7: return LiveTxt.T("nav_fontes");
            default: return LiveTxt.T("nav_geral");
        }
    }

    private static void Lateral(Rect lat)
    {
        GUILayout.BeginArea(new Rect(lat.x + 12f, lat.y + 14f, lat.width - 24f, lat.height - 28f));
        GUILayout.Label("Universal", sMarca);
        GUILayout.Label("Translator", sMarca);
        GUILayout.Label("By Raz3n", sPeq);
        GUILayout.Space(10f);
        BotaoAtivar(lat.width - 24f, 34f);
        GUILayout.Space(10f);
        Nav(0, "nav_geral");
        Nav(1, "nav_prov");
        Nav(2, "nav_estilo");
        Nav(3, "nav_glos");
        Nav(4, "nav_cats");
        Nav(5, "nav_avanc");
        Nav(6, "nav_log");
        Nav(7, "nav_fontes");
        GUILayout.FlexibleSpace();
        GUILayout.Label(LiveTxt.T("menu_idioma"), sPeq);
        int atual = LiveTxt.Atual;
        int novo = Escolha(atual, LiveTxt.Codigos, 3, lat.width - 24f);
        if (novo != atual)
        {
            LiveTxt.Atual = novo;
            LiveConfig.Marcar();
        }
        GUILayout.EndArea();
    }

    // Comecar / Pausar a traducao. O mod sempre abre pausado; o jogador comeca quando quiser.
    private static void BotaoAtivar(float largura, float altura)
    {
        Color antes = GUI.backgroundColor;
        bool ativo = LiveConfig.Ativo;
        if (GUILayout.Button(LiveTxt.T(ativo ? "pausar" : "comecar"), ativo ? sAmbar : sSel, GUILayout.Width(largura), GUILayout.Height(altura)))
        {
            LiveConfig.Ativo = !ativo;
            if (LiveConfig.Ativo) LiveMotor.VarrerAgora = true;
            LiveLog.Info(LiveConfig.Ativo ? "lg_comecou" : "lg_pausada");
        }
        GUI.backgroundColor = antes;
    }

    // Botoes de escolha (substitui Toolbar/SelectionGrid): o selecionado fica em teal (estilo sSel).
    private static int Escolha(int sel, string[] rot, int cols, float largura)
    {
        int res = sel;
        float bw = Mathf.Floor((largura - 4f * (cols - 1)) / cols);
        Color antes = GUI.backgroundColor;
        for (int i = 0; i < rot.Length; i += cols)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + cols && j < rot.Length; j++)
            {
                if (GUILayout.Button(rot[j], j == sel ? sSel : sBtn, GUILayout.Width(bw), GUILayout.Height(28f))) res = j;
            }
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = antes;
        return res;
    }

    // Varias opcoes ao mesmo tempo: cada botao liga/desliga um bit (teal = ligado)
    private static int EscolhaMulti(int mascara, string[] rot, int cols, float largura)
    {
        int res = mascara;
        float bw = Mathf.Floor((largura - 4f * (cols - 1)) / cols);
        Color antes = GUI.backgroundColor;
        for (int i = 0; i < rot.Length; i += cols)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + cols && j < rot.Length; j++)
            {
                int bit = 1 << j;
                if (GUILayout.Button(rot[j], (mascara & bit) != 0 ? sSel : sBtn, GUILayout.Width(bw), GUILayout.Height(28f))) res = mascara ^ bit;
            }
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = antes;
        return res;
    }

    // Caixa de marcar: fica em teal quando ligada
    private static bool Marca(bool valor, string texto)
    {
        return GUILayout.Toggle(valor, texto, sMarcar);
    }

    private static void Nav(int idx, string chave)
    {
        Color antes = GUI.backgroundColor;
        if (GUILayout.Button((idx + 1).ToString("00") + "    " + LiveTxt.T(chave), aba == idx ? sNavSel : sNav, GUILayout.Height(34f)))
        {
            aba = idx;
            rolagem = Vector2.zero;
            confLimpar = false;
            msgAvanc = "";
            pagina = 0;
            termoEdit = null;
            termoCat = null;
            if (idx == 4)
            {
                catNova = false;
                catMsg = "";
                catConfApagar = false;
                if (!Categorias.Existe(catSel)) catSel = 0;
            }
        }
        GUI.backgroundColor = antes;
        GUILayout.Space(2f);
    }

    // status e erros: sempre visiveis embaixo
    private static void Rodape()
    {
        GUILayout.Space(4f);
        GUILayout.Label(LiveTxt.F("status", LiveMotor.NaFila, LiveMotor.Traduzidos, TradCache.Total, LiveMotor.Pedidos, LiveMotor.Erros), sPeq);
        string motivo;
        if (!Provedores.Configurado(LiveConfig.Atual, out motivo)) GUILayout.Label(LiveTxt.T("falta") + motivo, sAviso);
        if (LiveMotor.UltimoErro.Length > 0) GUILayout.Label(LiveTxt.T("ultimo_erro") + LiveMotor.UltimoErro, sErro);
    }

    // ---------------- aba 1: Geral ----------------

    private static void PaginaGeral()
    {
        BotaoAtivar(260f, 34f);
        GUILayout.Label(LiveTxt.T(LiveConfig.Ativo ? "trad_rodando" : "trad_pausada"), LiveConfig.Ativo ? sOk : sAviso);
        GUILayout.Space(8f);

        GUILayout.Label(LiveTxt.T("modo"));
        bool gratis = LiveConfig.ProvedorId == LiveConfig.IdGoogle;
        int modo = Escolha(gratis ? 0 : 1, new string[] { LiveTxt.T("modo_gratis"), LiveTxt.T("modo_api") }, 2, larguraUtil);
        if (modo == 0 && !gratis)
        {
            LiveConfig.ProvedorId = LiveConfig.IdGoogle;
            Mudou();
            gratis = true;
        }
        else if (modo == 1 && gratis)
        {
            LiveConfig.ProvedorId = LiveConfig.UltimoProvedorComChave;
            if (LiveConfig.ProvedorId == LiveConfig.IdGoogle) LiveConfig.ProvedorId = "gemini";
            Mudou();
            gratis = false;
        }
        GUILayout.Label(Dica(LiveConfig.Atual), sPeq);
        GUILayout.Space(10f);

        GUILayout.Label(LiveTxt.T("traduzir_para"));
        string[] nomesIdioma = new string[LiveConfig.Idiomas.Length];
        int idxIdioma = 0;
        for (int i = 0; i < LiveConfig.Idiomas.Length; i++)
        {
            nomesIdioma[i] = LiveConfig.Idiomas[i][1];
            if (LiveConfig.Idiomas[i][0] == LiveConfig.Idioma) idxIdioma = i;
        }
        int novoIdioma = Escolha(idxIdioma, nomesIdioma, 4, larguraUtil);
        if (novoIdioma != idxIdioma)
        {
            LiveConfig.Idioma = LiveConfig.Idiomas[novoIdioma][0];
            TradCache.TrocouIdioma();
            Glossario.TrocouIdioma();
            FonteEscolhida.ConferirIdioma();   // fonte sem as letras do idioma novo volta para a do jogo
            termoEdit = null;
            Mudou();
        }
        GUILayout.Label(LiveTxt.T("aviso_cjk"), sPeq);
        GUILayout.Space(10f);

        GUILayout.Label(LiveTxt.T("o_que"));
        string[] tipos = new string[]
        {
            LiveTxt.T("fonte_zh"), LiveTxt.T("fonte_ja"), LiveTxt.T("fonte_ko"),
            LiveTxt.T("fonte_cir"), LiveTxt.T("fonte_lat"), LiveTxt.T("fonte_out")
        };
        int fontes = EscolhaMulti(LiveConfig.Fontes, tipos, 3, larguraUtil);
        if (fontes != LiveConfig.Fontes)
        {
            LiveConfig.Fontes = fontes;
            Mudou();
        }
        GUILayout.Label(LiveTxt.T("fonte_dica"), sPeq);
        if (LiveConfig.Fontes == 0) GUILayout.Label(LiveTxt.T("fonte_nenhum"), sAviso);
    }

    // ---------------- aba 2: Provedor / API ----------------

    private static void PaginaProvedor(MonoBehaviour drv)
    {
        bool gratis = LiveConfig.ProvedorId == LiveConfig.IdGoogle;
        Provedor p = LiveConfig.Atual;
        if (gratis)
        {
            GUILayout.Label(LiveTxt.T("gratis_info"), sAviso);
            GUILayout.Space(4f);
            GUILayout.Label(Dica(p), sPeq);
        }
        else
        {
            DesenharProvedor(p);
        }
        GUILayout.Space(10f);
        if (GUILayout.Button(LiveTxt.T("testar"), GUILayout.Height(28f))) LiveMotor.Testar(drv);
        if (LiveMotor.Teste.Length > 0)
            GUILayout.Label(LiveMotor.Teste, LiveMotor.Teste.StartsWith("OK") ? sOk : sAviso);
    }

    private static void DesenharProvedor(Provedor atual)
    {
        List<Provedor> lista = new List<Provedor>();
        for (int i = 0; i < LiveConfig.Provedores.Length; i++)
            if (LiveConfig.Provedores[i].Id != LiveConfig.IdGoogle) lista.Add(LiveConfig.Provedores[i]);

        string[] nomes = new string[lista.Count];
        int sel = 0;
        for (int i = 0; i < lista.Count; i++)
        {
            nomes[i] = lista[i].Nome;
            if (lista[i].Id == atual.Id) sel = i;
        }
        GUILayout.Label(LiveTxt.T("servico"));
        int novo = Escolha(sel, nomes, 3, larguraUtil);
        if (novo != sel)
        {
            LiveConfig.ProvedorId = lista[novo].Id;
            LiveConfig.UltimoProvedorComChave = lista[novo].Id;
            Mudou();
            atual = lista[novo];
        }
        GUILayout.Label(Dica(atual), sPeq);

        string id = atual.Id;
        GUILayout.Label(LiveTxt.T("url"));
        string u = GUILayout.TextField(LiveConfig.Url(id));
        if (u != LiveConfig.Url(id)) { LiveConfig.Urls[id] = u; LiveConfig.Marcar(); LiveMotor.Destravar(); }

        GUILayout.Label(atual.PrecisaChave ? LiveTxt.T("chave_obrig") : LiveTxt.T("chave_opc"));
        string k = LiveConfig.Chave(id);
        string nk = k;
        if (mostrarChave || k.Length == 0)
        {
            nk = GUILayout.TextField(k);
        }
        else
        {
            string fim = k.Length > 4 ? k.Substring(k.Length - 4) : "";
            GUILayout.Label("************ " + fim + "   (" + k.Length + " " + LiveTxt.T("chars") + ")");
        }
        GUILayout.BeginHorizontal();
        mostrarChave = Marca(mostrarChave, LiveTxt.T("mostrar"));
        if (k.Length > 0 && GUILayout.Button(LiveTxt.T("apagar_chave"), GUILayout.Width(130f))) nk = "";
        GUILayout.EndHorizontal();
        if (nk != k) { LiveConfig.Chaves[id] = nk.Trim(); LiveConfig.Marcar(); LiveMotor.Destravar(); }

        GUILayout.Label(LiveTxt.T("modelo"));
        string m = GUILayout.TextField(LiveConfig.Modelo(id));
        if (m != LiveConfig.Modelo(id)) { LiveConfig.Modelos[id] = m; LiveConfig.Marcar(); LiveMotor.Destravar(); }
    }

    private static string Dica(Provedor p)
    {
        string k = "dica_" + p.Id;
        return LiveTxt.Tem(k) ? LiveTxt.T(k) : p.Dica;
    }

    // ---------------- aba 3: Estilo e instrucoes extras ----------------

    private static void PaginaEstilo()
    {
        if (LiveConfig.ProvedorId == LiveConfig.IdGoogle)
        {
            GUILayout.Label(LiveTxt.T("estilo_gratis"), sAviso);
            return;
        }

        GUILayout.Label(LiveTxt.T("estilo_titulo"));
        string[] nomes = new string[LiveConfig.Estilos.Length];
        for (int i = 0; i < nomes.Length; i++) nomes[i] = LiveTxt.T("estilo" + i);
        int e = Escolha(LiveConfig.Estilo, nomes, 2, larguraUtil);
        if (e != LiveConfig.Estilo)
        {
            LiveConfig.Estilo = e;
            LiveConfig.Marcar();
        }
        // resumo do estilo escolhido
        GUILayout.Label(LiveTxt.T("resumo" + LiveConfig.Estilo), sOk);
        GUILayout.Space(10f);

        string extra = LiveConfig.PromptExtra ?? "";
        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("extras_titulo"), sPeq);
        GUILayout.FlexibleSpace();
        GUILayout.Label(extra.Length + " / " + MaxExtras, extra.Length >= MaxExtras ? sErro : sPeq, GUILayout.Width(80f));
        GUILayout.EndHorizontal();
        string ne = Caixa(extra, 4);
        if (ne != extra)
        {
            if (ne.Length > MaxExtras) ne = ne.Substring(0, MaxExtras);
            LiveConfig.PromptExtra = ne;
            LiveConfig.Marcar();
        }

        GUILayout.Space(6f);
        GUILayout.Label(LiveTxt.T("exemplos"), sPeq);
        string[] exemplos = new string[]
        {
            LiveTxt.T("ex1"), LiveTxt.T("ex2"), LiveTxt.T("ex3"), LiveTxt.T("ex4"), LiveTxt.T("ex5")
        };
        int ex = GUILayout.SelectionGrid(-1, exemplos, 2);
        if (ex >= 0) AdicionarExtra(exemplos[ex]);

        GUILayout.Space(4f);
        if (GUILayout.Button(LiveTxt.T("limpar_extras"), GUILayout.Width(110f)))
        {
            LiveConfig.PromptExtra = "";
            LiveConfig.Marcar();
        }
        GUILayout.Space(6f);
        GUILayout.Label(LiveTxt.T("estilo_nota"), sPeq);
    }

    private static void AdicionarExtra(string texto)
    {
        string atual = LiveConfig.PromptExtra ?? "";
        if (atual.IndexOf(texto, StringComparison.Ordinal) >= 0) return;
        string novo = atual.Length == 0 ? texto : atual.TrimEnd() + " " + texto;
        if (novo.Length > MaxExtras) return;
        LiveConfig.PromptExtra = novo;
        LiveConfig.Marcar();
    }

    // ---------------- aba 4: Glossario ----------------

    private static void PaginaGlossario()
    {
        Event ev = Event.current;
        if (ev == null || ev.type == EventType.Layout) AtualizarFiltro();

        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("glos_buscar"), GUILayout.Width(60f));
        string nb = GUILayout.TextField(busca);
        if (nb != busca)
        {
            busca = nb;
            pagina = 0;
        }
        if (GUILayout.Button(LiveTxt.T("glos_novo"), GUILayout.Width(140f))) novoAberto = !novoAberto;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LiveTxt.T("cat_novo"), GUILayout.Width(140f)))
        {
            // abre a aba Categorias ja no modo "nova categoria"
            aba = 4;
            rolagem = Vector2.zero;
            termoEdit = null;
            termoCat = null;
            catNova = true;
            cNome = "";
            cPal = "";
            cInstr = "";
            catMsg = "";
            catConfApagar = false;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (novoAberto) NovoTermo();

        // filtro por categoria (Todos + 9 categorias)
        int[] ordem = Categorias.Ordem();
        string[] rotulos = new string[ordem.Length + 1];
        rotulos[0] = LiveTxt.T("todos") + " " + contagens[0];
        for (int i = 0; i < ordem.Length; i++)
            rotulos[i + 1] = Cortar(Categorias.Nome(ordem[i]), 14) + " " + contagens[ordem[i] + 1];
        int selPos = 0;
        if (filtroCat > 0) selPos = PosOrdem(ordem, filtroCat - 1) + 1;
        int novaPos = Escolha(selPos, rotulos, 5, larguraUtil);
        if (novaPos != selPos)
        {
            filtroCat = novaPos == 0 ? 0 : ordem[novaPos - 1] + 1;
            pagina = 0;
            termoEdit = null;
        }
        GUILayout.Space(4f);

        // cabecalho da tabela
        MedirColunas();
        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("col_orig"), sCab, GUILayout.Width(wOrig));
        GUILayout.Space(ColEsp);
        GUILayout.Label(LiveTxt.T("col_trad"), sCab, GUILayout.Width(wTrad));
        GUILayout.Space(ColEsp);
        GUILayout.Label(LiveTxt.T("col_cat"), sCab, GUILayout.Width(ColCat));
        GUILayout.Space(ColEsp);
        GUILayout.Label(LiveTxt.T("col_trava"), sCab, GUILayout.Width(ColTrava));
        GUILayout.EndHorizontal();

        int total = filtrado.Count;
        int paginas = Mathf.Max(1, (total + porPagina - 1) / porPagina);
        if (pagina >= paginas) pagina = paginas - 1;
        if (pagina < 0) pagina = 0;
        int ini = pagina * porPagina;
        int fim = Mathf.Min(total, ini + porPagina);

        if (Glossario.Total == 0) GUILayout.Label(LiveTxt.T("glos_vazio"), sPeq);
        // se o termo com a lista aberta saiu da tela (outra pagina/filtro/busca), fecha a lista
        if (termoCat != null)
        {
            bool visivel = false;
            for (int i = ini; i < fim; i++) if (filtrado[i] == termoCat) visivel = true;
            if (!visivel) termoCat = null;
        }
        for (int i = ini; i < fim; i++) Linha(filtrado[i]);

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(contagens[0] + " " + LiveTxt.T("glos_termos"), sPeq);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("<", GUILayout.Width(30f)) && pagina > 0) pagina--;
        GUILayout.Label(LiveTxt.T("pag") + " " + (pagina + 1) + " / " + paginas, GUILayout.Width(110f));
        if (GUILayout.Button(">", GUILayout.Width(30f)) && pagina < paginas - 1) pagina++;
        GUILayout.EndHorizontal();
        GUILayout.Label(LiveTxt.T("glos_nota"), sPeq);
    }

    // Colunas da tabela do glossario: botoes com largura fixa, Original e Traducao dividem o resto da janela.
    private const float ColEsp = 24f;     // espaco entre colunas
    private const float ColCat = 120f;
    private const float ColTrava = 100f;
    private const float ColEdit = 90f;
    private static float wOrig = 150f;
    private static float wTrad = 170f;

    private static void MedirColunas()
    {
        float resto = larguraUtil - ColCat - ColTrava - ColEdit - ColEsp * 4f;
        wOrig = Mathf.Max(150f, Mathf.Floor(resto * 0.45f));
        wTrad = Mathf.Max(170f, Mathf.Floor(resto * 0.55f));
    }

    // recuo para o que fica embaixo da linha (OK/x/Apagar e a lista de categorias): alinha com a coluna Traducao
    private static float Recuo { get { return wOrig + ColEsp + 4f; } }

    private static void Linha(TermoGlossario t)
    {
        bool edit = termoEdit == t;
        GUILayout.BeginHorizontal();
        // ~9 px por letra no Original (tem chines, mais largo) e ~8 px na Traducao
        GUILayout.Label(Cortar(t.Original, Mathf.Max(10, (int)(wOrig / 9f))), sCel, GUILayout.Width(wOrig));
        GUILayout.Space(ColEsp);
        if (edit) campoEdit = GUILayout.TextField(campoEdit, GUILayout.Width(wTrad));
        else GUILayout.Label(Cortar(t.Traducao, Mathf.Max(12, (int)(wTrad / 8f))), sCel, GUILayout.Width(wTrad));
        GUILayout.Space(ColEsp);

        Color antes = GUI.backgroundColor;
        if (GUILayout.Button(Cortar(Categorias.Nome(t.Cat), 14), Chip(Categorias.Cor(t.Cat), false), GUILayout.Width(ColCat)))
        {
            // abre/fecha a lista de categorias (em vez de trocar uma por uma)
            termoCat = termoCat == t ? null : t;
        }
        GUI.backgroundColor = antes;
        GUILayout.Space(ColEsp);

        if (GUILayout.Button(t.Travado ? LiveTxt.T("travado") : LiveTxt.T("trava_btn"), GUILayout.Width(ColTrava)))
        {
            t.Travado = !t.Travado;
            Glossario.Marcar();
        }
        GUILayout.Space(ColEsp);

        if (!edit)
        {
            if (GUILayout.Button(LiveTxt.T("editar"), GUILayout.Width(ColEdit)))
            {
                termoEdit = t;
                campoEdit = t.Traducao;
            }
        }
        GUILayout.EndHorizontal();

        if (edit)
        {
            // OK, x e Apagar ficam logo abaixo da caixa de texto
            GUILayout.BeginHorizontal();
            GUILayout.Space(Recuo);
            if (GUILayout.Button(LiveTxt.T("ok"), GUILayout.Width(60f))) ConfirmarEdicao(t);
            if (GUILayout.Button("x", GUILayout.Width(40f))) termoEdit = null;
            if (GUILayout.Button(LiveTxt.T("apagar_termo"), GUILayout.Width(100f)))
            {
                Glossario.Remover(t);
                termoEdit = null;
                LiveMotor.VarrerAgora = true;
            }
            GUILayout.EndHorizontal();
        }

        if (termoCat == t) ListaCategorias(t);
    }

    // Lista de categorias logo abaixo da linha: clica na que quiser. A atual fica em teal.
    // Escolher a categoria na mao trava o termo (assim "Reclassificar" nao desfaz a sua escolha).
    private static void ListaCategorias(TermoGlossario t)
    {
        Color antes = GUI.backgroundColor;
        int[] ordem = Categorias.Ordem();
        const int Colunas = 5;
        for (int i = 0; i < ordem.Length; i += Colunas)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Recuo);
            for (int j = i; j < i + Colunas && j < ordem.Length; j++)
            {
                int c = ordem[j];
                if (GUILayout.Button(Cortar(Categorias.Nome(c), 10), Chip(Categorias.Cor(c), c == t.Cat), GUILayout.Width(80f)))
                {
                    if (c != t.Cat)
                    {
                        t.Cat = c;
                        t.Travado = true;
                        Glossario.Marcar();
                    }
                    termoCat = null;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = antes;
        GUILayout.Space(4f);
    }

    // ---------------- painel de categorias ----------------

    // Caixa de texto com varias linhas e quebra de linha (o jogo so tem TextField de uma linha:
    // ligamos a quebra de linha so durante esta chamada e voltamos ao normal em seguida).
    private static string Caixa(string valor, int linhas)
    {
        GUIStyle st = GUI.skin.textField;
        bool antes = st.wordWrap;
        st.wordWrap = true;
        string r;
        try { r = GUILayout.TextField(valor, GUILayout.Height(linhas * 20f + 6f)); }
        finally { st.wordWrap = antes; }
        return r;
    }

    private static string Limitar(string s, int max)
    {
        if (s == null) return "";
        return s.Length > max ? s.Substring(0, max) : s;
    }

    // aba "Categorias": editar as existentes ou criar uma nova
    private static void PaginaCategorias()
    {
        if (catNova)
        {
            GUILayout.Label(LiveTxt.T("cat_nova_tit"), sCab);
            PainelNova();
        }
        else
        {
            PainelEditar();
        }
    }

    private static void PainelNova()
    {
        GUILayout.Label(LiveTxt.T("cat_nome"), sPeq);
        cNome = Limitar(GUILayout.TextField(cNome), Categorias.MaxNome);

        GUILayout.Label(LiveTxt.T("cat_palavras"), sPeq);
        cPal = Limitar(Caixa(cPal, 4), Categorias.MaxPalavras);
        GUILayout.Label(LiveTxt.T("cat_ex_pal"), sPeq);

        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("cat_instr"), sPeq);
        GUILayout.FlexibleSpace();
        GUILayout.Label(cInstr.Length + " / " + Categorias.MaxInstrucao, sPeq, GUILayout.Width(70f));
        GUILayout.EndHorizontal();
        cInstr = Limitar(Caixa(cInstr, 3), Categorias.MaxInstrucao);
        GUILayout.Label(LiveTxt.T("cat_ex"), sPeq);
        if (LiveConfig.ProvedorId == LiveConfig.IdGoogle) GUILayout.Label(LiveTxt.T("cat_gratis"), sAviso);

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LiveTxt.T("cat_criar"), GUILayout.Width(110f)))
        {
            string erro;
            int id = Categorias.Criar(cNome, cPal, cInstr, out erro);
            if (id >= 0)
            {
                catSel = id;
                catNova = false;
                catMsg = "";
                cNome = "";
                cPal = "";
                cInstr = "";
            }
            else
            {
                catMsg = LiveTxt.F(erro, Categorias.MaxNovas);
                catMsgErro = true;
            }
        }
        if (GUILayout.Button(LiveTxt.T("cancelar"), GUILayout.Width(110f)))
        {
            catNova = false;
            catMsg = "";
            if (!Categorias.Existe(catSel)) catSel = 0;
        }
        GUILayout.EndHorizontal();
        if (catMsg.Length > 0) GUILayout.Label(catMsg, catMsgErro ? sErro : sOk);
    }

    private static void PainelEditar()
    {
        int[] ordem = Categorias.Ordem();
        string[] nomes = new string[ordem.Length];
        int pos = 0;
        for (int i = 0; i < ordem.Length; i++)
        {
            nomes[i] = Cortar(Categorias.Nome(ordem[i]), 14);
            if (ordem[i] == catSel) pos = i;
        }
        int np = Escolha(pos, nomes, 5, larguraUtil);
        if (np != pos)
        {
            catSel = ordem[np];
            catConfApagar = false;
            catMsg = "";
        }
        CategoriaInfo ci = Categorias.Obter(catSel);
        if (ci == null)
        {
            catSel = 0;
            ci = Categorias.Obter(0);
            if (ci == null) return;
        }

        GUILayout.Space(4f);
        if (GUILayout.Button(LiveTxt.T("cat_novo"), GUILayout.Width(140f)))
        {
            catNova = true;
            cNome = "";
            cPal = "";
            cInstr = "";
            catMsg = "";
            catConfApagar = false;
            return;
        }
        GUILayout.Space(4f);
        if (ci.Padrao)
        {
            GUILayout.Label(LiveTxt.T("cat_fixa"), sPeq);
        }
        else
        {
            GUILayout.Label(LiveTxt.T("cat_nome"), sPeq);
            string nn = Limitar(GUILayout.TextField(ci.Nome), Categorias.MaxNome);
            if (nn != ci.Nome)
            {
                ci.Nome = nn;
                Categorias.Marcar();
            }
        }

        GUILayout.Label(LiveTxt.T("cat_palavras"), sPeq);
        string np2 = Limitar(Caixa(ci.Palavras, 4), Categorias.MaxPalavras);
        if (np2 != ci.Palavras)
        {
            ci.Palavras = np2;
            Categorias.Marcar();
        }
        GUILayout.Label(LiveTxt.T("cat_ex_pal"), sPeq);

        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("cat_instr"), sPeq);
        GUILayout.FlexibleSpace();
        GUILayout.Label(ci.Instrucao.Length + " / " + Categorias.MaxInstrucao, sPeq, GUILayout.Width(70f));
        GUILayout.EndHorizontal();
        string ni = Limitar(Caixa(ci.Instrucao, 3), Categorias.MaxInstrucao);
        if (ni != ci.Instrucao)
        {
            ci.Instrucao = ni;
            Categorias.Marcar();
        }
        GUILayout.Label(LiveTxt.T("cat_ex"), sPeq);
        if (LiveConfig.ProvedorId == LiveConfig.IdGoogle) GUILayout.Label(LiveTxt.T("cat_gratis"), sAviso);

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LiveTxt.T("cat_restaurar"), GUILayout.Width(150f)))
        {
            Categorias.Restaurar(catSel);
            catConfApagar = false;
            catMsg = "";
        }
        if (!ci.Padrao)
        {
            if (GUILayout.Button(LiveTxt.T(catConfApagar ? "cat_apagar_conf" : "cat_apagar"), GUILayout.Width(190f)))
            {
                if (catConfApagar)
                {
                    int apagar = catSel;
                    Categorias.Apagar(apagar);
                    if (filtroCat == apagar + 1) filtroCat = 0;
                    catSel = 0;
                    catConfApagar = false;
                    catMsg = "";
                    termoCat = null;
                }
                else catConfApagar = true;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LiveTxt.T("cat_reclass"), GUILayout.Width(220f)))
        {
            int n = Glossario.Reclassificar();
            catMsg = LiveTxt.F("cat_reclass_n", n);
            catMsgErro = false;
        }
        // modo com chave: a IA decide a categoria dos termos nao travados (opcional, gasta tokens)
        if (LiveConfig.ProvedorId != LiveConfig.IdGoogle)
        {
            if (!ClassificadorIA.Rodando)
            {
                if (GUILayout.Button(LiveTxt.T("cat_reclass_ia"), GUILayout.Width(220f)))
                {
                    catMsg = "";
                    ClassificadorIA.Iniciar(LiveDriver.Instancia);
                }
            }
            else if (GUILayout.Button(LiveTxt.T("cia_parar"), GUILayout.Width(220f))) ClassificadorIA.Parar();
        }
        GUILayout.EndHorizontal();
        if (LiveConfig.ProvedorId != LiveConfig.IdGoogle)
        {
            GUILayout.Label(LiveTxt.T("cat_reclass_ia_dica"), sPeq);
            if (ClassificadorIA.Status.Length > 0) GUILayout.Label(ClassificadorIA.Status, ClassificadorIA.StatusErro ? sErro : sOk);
        }
        if (catMsg.Length > 0) GUILayout.Label(catMsg, catMsgErro ? sErro : sOk);
    }

    private static void ConfirmarEdicao(TermoGlossario t)
    {
        string nova = campoEdit == null ? "" : campoEdit.Trim();
        if (nova.Length > 0 && nova != t.Traducao)
        {
            string antiga = t.Traducao;
            t.Traducao = nova;
            t.Travado = true;           // o que o jogador editou fica dele
            Glossario.Marcar();
            TradCache.Add(t.Original, nova);
            LiveMotor.Substituir(antiga, nova);
        }
        termoEdit = null;
    }

    private static void NovoTermo()
    {
        GUILayout.Label(LiveTxt.T("novo_orig"), sPeq);
        novoOrig = GUILayout.TextField(novoOrig);
        GUILayout.Label(LiveTxt.T("lbl_trad"), sPeq);
        novoTrad = GUILayout.TextField(novoTrad);
        if (GUILayout.Button(LiveTxt.T("add"), GUILayout.Width(110f)))
        {
            string o = novoOrig.Trim();
            string tr = novoTrad.Trim();
            if (o.Length > 0 && tr.Length > 0)
            {
                int cat = filtroCat > 0 ? filtroCat - 1 : Glossario.Categorizar(o, "");
                Glossario.Definir(o, tr, cat, true);
                TradCache.Add(o, tr);
                novoOrig = "";
                novoTrad = "";
                novoAberto = false;
                LiveMotor.Reiniciar();
                LiveMotor.VarrerAgora = true;
            }
        }
        GUILayout.Space(4f);
    }

    private static void AtualizarFiltro()
    {
        if (versaoFiltro == Glossario.Versao && buscaFiltro == busca && catFiltro == filtroCat) return;
        versaoFiltro = Glossario.Versao;
        buscaFiltro = busca;
        catFiltro = filtroCat;

        filtrado.Clear();
        for (int i = 0; i < contagens.Length; i++) contagens[i] = 0;
        string b = busca == null ? "" : busca.Trim().ToLowerInvariant();
        List<TermoGlossario> todos = Glossario.Lista;
        for (int i = 0; i < todos.Count; i++)
        {
            TermoGlossario t = todos[i];
            if (b.Length > 0 &&
                t.Original.ToLowerInvariant().IndexOf(b, StringComparison.Ordinal) < 0 &&
                t.Traducao.ToLowerInvariant().IndexOf(b, StringComparison.Ordinal) < 0) continue;
            contagens[0]++;
            contagens[t.Cat + 1]++;
            if (filtroCat != 0 && t.Cat != filtroCat - 1) continue;
            filtrado.Add(t);
        }
        filtrado.Sort(delegate (TermoGlossario x, TermoGlossario y)
        {
            return string.Compare(x.Original, y.Original, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static int PosOrdem(int[] ordem, int cat)
    {
        for (int i = 0; i < ordem.Length; i++)
            if (ordem[i] == cat) return i;
        return 0;
    }

    private static string Cortar(string s, int max)
    {
        if (s == null) return "";
        if (s.Length <= max) return s;
        return s.Substring(0, max - 1) + "…";
    }

    // ---------------- aba 5: Avancado ----------------

    private static void PaginaAvancado()
    {
        bool gratis = LiveConfig.ProvedorId == LiveConfig.IdGoogle;
        int maxRpm = 600;

        // modo gratis: o ritmo e automatico (um pedido por vez); pedidos por minuto e lote so valem para IA
        if (gratis) GUILayout.Label(LiveTxt.T("rpm_gratis"), sPeq);
        else
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(LiveTxt.T("rpm"), GUILayout.Width(170f));
            if (GUILayout.Button("-", GUILayout.Width(28f))) MudarRpm(-5, maxRpm);
            string nrpm = GUILayout.TextField(campoRpm, GUILayout.Width(60f));
            if (nrpm != campoRpm)
            {
                campoRpm = nrpm;
                int v;
                if (int.TryParse(nrpm, out v)) { LiveConfig.Rpm = Mathf.Clamp(v, 1, maxRpm); LiveConfig.Marcar(); }
            }
            if (GUILayout.Button("+", GUILayout.Width(28f))) MudarRpm(5, maxRpm);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(LiveTxt.T("lote"), GUILayout.Width(170f));
            string nlote = GUILayout.TextField(campoLote, GUILayout.Width(60f));
            if (nlote != campoLote)
            {
                campoLote = nlote;
                int v2;
                if (int.TryParse(nlote, out v2)) { LiveConfig.Lote = Mathf.Clamp(v2, 1, 30); LiveConfig.Marcar(); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(LiveTxt.T("rpm_aviso"), sPeq);
        }
        GUILayout.Space(8f);

        bool botao = Marca(LiveConfig.BotaoVisivel, LiveTxt.T("botao_canto"));
        if (botao != LiveConfig.BotaoVisivel)
        {
            LiveConfig.BotaoVisivel = botao;
            LiveConfig.Marcar();
        }

        // contexto para a IA (so vale no modo com chave)
        bool ctx = Marca(LiveConfig.Contexto, LiveTxt.T("contexto"));
        if (ctx != LiveConfig.Contexto)
        {
            LiveConfig.Contexto = ctx;
            LiveConfig.Marcar();
        }
        GUILayout.Label(LiveTxt.T("contexto_dica"), sPeq);

        // deixar a IA pensar (melhor, mais lento) ou pedir o minimo (mais rapido)
        bool rac = Marca(LiveConfig.Raciocinio, LiveTxt.T("raciocinio"));
        if (rac != LiveConfig.Raciocinio)
        {
            LiveConfig.Raciocinio = rac;
            LiveConfig.Marcar();
            Provedores.LimparRaciocinio();
        }
        GUILayout.Label(LiveTxt.T("raciocinio_dica"), sPeq);
        GUILayout.Space(8f);

        // tamanho minimo da fonte quando o texto nao cabe
        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("fonte_min"), GUILayout.Width(250f));
        if (GUILayout.Button("-", GUILayout.Width(28f))) MudarFonteMin(-5);
        GUILayout.Label(LiveConfig.FonteMin + "%", GUILayout.Width(50f));
        if (GUILayout.Button("+", GUILayout.Width(28f))) MudarFonteMin(5);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(LiveTxt.T("fonte_min_dica"), sPeq);
        GUILayout.Space(8f);

        GUILayout.Space(10f);

        // pre-traduzir o que ja esta carregado (menus e janelas escondidos)
        if (GUILayout.Button(LiveTxt.T("pre_trad"), GUILayout.Height(28f)))
        {
            confLimpar = false;
            if (!LiveConfig.Ativo)
            {
                msgAvanc = LiveTxt.T("pre_pausado");
                msgAvancErro = true;
            }
            else
            {
                msgAvanc = LiveTxt.F("pre_ok", LiveDriver.PreTraduzir());
                msgAvancErro = false;
            }
        }
        GUILayout.Label(LiveTxt.T(gratis ? "pre_dica_gratis" : "pre_dica"), sPeq);
        GUILayout.Space(10f);

        // exportar / importar glossario e cache
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LiveTxt.T("comp_exportar"), GUILayout.Height(28f)))
        {
            confLimpar = false;
            msgAvanc = Compartilhar.Exportar();
            msgAvancErro = false;
        }
        if (GUILayout.Button(LiveTxt.T("comp_importar"), GUILayout.Height(28f)))
        {
            confLimpar = false;
            msgAvanc = Compartilhar.Importar();
            msgAvancErro = false;
        }
        if (GUILayout.Button(LiveTxt.T("log_pasta"), GUILayout.Height(28f), GUILayout.Width(130f)))
        {
            try { Application.OpenURL("file:///" + Compartilhar.Pasta.Replace('\\', '/')); }
            catch (Exception e) { LiveLog.Erro("le_parte", e, "open folder"); }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(LiveTxt.F("comp_dica", LiveConfig.Idioma), sPeq);
        if (msgAvanc.Length > 0) GUILayout.Label(msgAvanc, msgAvancErro ? sErro : sOk);
        GUILayout.Space(10f);

        // limpar cache: pede um segundo clique (apaga tudo do idioma e nao tem volta)
        Color corAntes = GUI.backgroundColor;
        if (GUILayout.Button(LiveTxt.T(confLimpar ? "limpar_conf" : "limpar_cache"), confLimpar ? sPerigo : sBtn, GUILayout.Height(28f)))
        {
            if (!confLimpar)
            {
                confLimpar = true;
            }
            else
            {
                confLimpar = false;
                TradCache.Limpar();
                Glossario.LimparNaoTravados();
                termoEdit = null;
                LiveMotor.ZerarContadores();
                Mudou();   // Reiniciar: a tela volta ao original e e traduzida de novo
                LiveLog.Info("lg_cache_limpo", LiveConfig.Idioma);
                msgAvanc = LiveTxt.T("limpar_feito");
                msgAvancErro = false;
            }
        }
        GUI.backgroundColor = corAntes;
        if (confLimpar) GUILayout.Label(LiveTxt.F("limpar_aviso", TradCache.Total), sErro);
        GUILayout.Space(10f);
        GUILayout.Label(LiveTxt.T("aviso_teclas"), sPeq);
    }

    // ---------------- aba 8: Fontes ----------------

    private static void PaginaFontes()
    {
        GUILayout.Label(LiveTxt.F("fonte_trad_dica", FonteEscolhida.Quantas - 1), sPeq);
        GUILayout.Space(6f);

        int sel = FonteEscolhida.Indice;
        int n = FonteEscolhida.Quantas;
        const int cols = 3;
        float bw = Mathf.Floor((larguraUtil - 4f * (cols - 1)) / cols);
        Color antes = GUI.backgroundColor;
        for (int i = 0; i < n; i += cols)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + cols && j < n; j++)
            {
                string nome = j == 0 ? LiveTxt.T("fonte_original") : FonteEscolhida.Nome(j);
                if (GUILayout.Button(nome, j == sel ? sSel : sBtn, GUILayout.Width(bw), GUILayout.Height(34f)))
                    FonteEscolhida.Escolher(j);
            }
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = antes;

        GUILayout.Space(10f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LiveTxt.T("fonte_pasta"), GUILayout.Height(28f), GUILayout.Width(180f)))
        {
            try { Application.OpenURL("file:///" + FonteEscolhida.PastaUsuario.Replace('\\', '/')); }
            catch (Exception e) { LiveLog.Erro("le_parte", e, "open folder"); }
        }
        if (GUILayout.Button(LiveTxt.T("fonte_atualizar"), GUILayout.Height(28f), GUILayout.Width(180f))) FonteEscolhida.Recarregar();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(LiveTxt.T("fonte_pasta_dica"), sPeq);
    }

    // ---------------- aba 7: Log ----------------

    private static void PaginaLog()
    {
        // estado do Google gratis: qual endereco esta em uso e quais estao parados
        if (LiveConfig.Atual.Tipo == 0)
        {
            GUILayout.Label(LiveTxt.T("log_google"), sCab);
            for (int i = 0; i < Provedores.GoogleEndpoints; i++)
            {
                int falta = LiveMotor.CastigoRestante(i);
                string est = falta > 0 ? LiveTxt.F("log_ep_parado", falta / 60, (falta % 60).ToString("00")) : LiveTxt.T("log_ep_livre");
                string marca = i == LiveMotor.GoogleEp ? ">> " : "    ";
                GUILayout.Label(marca + Provedores.NomeGoogle(i) + ": " + est, falta > 0 ? sAviso : sOk);
            }
            GUILayout.Label(LiveTxt.F("log_lotes", LiveMotor.LotesOk, LiveMotor.LotesFallback), sPeq);
            GUILayout.Label(LiveTxt.F("log_prov", TradCache.TotalProvisorios, LiveMotor.Retraduzidos), TradCache.TotalProvisorios > 0 ? sAviso : sPeq);
            GUILayout.Space(8f);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LiveTxt.T("log_limpar"), GUILayout.Height(26f), GUILayout.Width(160f))) LiveLog.Limpar();
        if (GUILayout.Button(LiveTxt.T("log_pasta"), GUILayout.Height(26f), GUILayout.Width(160f)))
        {
            try { Application.OpenURL("file:///" + LiveConfig.Pasta.Replace('\\', '/')); }
            catch (Exception e) { LiveLog.Erro("le_parte", e, "open folder"); }
        }
        GUILayout.EndHorizontal();
        if (LiveLog.Arquivo.Length > 0) GUILayout.Label(LiveLog.Arquivo, sPeq);
        GUILayout.Space(6f);

        // mais novas primeiro
        // (cada linha e traduzida na hora para o idioma do menu)
        int total = LiveLog.Count;
        if (total == 0) GUILayout.Label(LiveTxt.T("log_vazio"), sPeq);
        int ate = Mathf.Max(0, total - 150);
        for (int i = total - 1; i >= ate; i--)
            GUILayout.Label(LiveLog.Texto(i), LiveLog.Ruim(i) ? sErro : sPeq);
    }

    private static void MudarFonteMin(int delta)
    {
        LiveConfig.FonteMin = Mathf.Clamp(LiveConfig.FonteMin + delta, 50, 90);
        LiveConfig.Marcar();
    }

    private static void MudarRpm(int delta, int maxRpm)
    {
        int v = Mathf.Clamp(LiveConfig.Rpm + delta, 1, maxRpm);
        LiveConfig.Rpm = v;
        campoRpm = v.ToString();
        LiveConfig.Marcar();
    }

    // ---------------- estilos ----------------

    private static void PrepararEstilos()
    {
        if (texFundo != null) return;
        texFundo = NovaTextura(new Color(0.063f, 0.078f, 0.094f, 0.97f));
        texLateral = NovaTextura(new Color(0.043f, 0.055f, 0.067f, 0.99f));

        sTitulo = new GUIStyle(GUI.skin.label);
        sTitulo.fontSize = 20;
        sTitulo.fontStyle = FontStyle.Bold;
        sTitulo.normal.textColor = Color.white;
        sTitulo.wordWrap = false;
        sTitulo.clipping = TextClipping.Overflow;

        sMarca = new GUIStyle(GUI.skin.label);
        sMarca.fontSize = 20;
        sMarca.fontStyle = FontStyle.Bold;
        sMarca.normal.textColor = Teal;

        sPeq = new GUIStyle(GUI.skin.label);
        sPeq.fontSize = 12;
        sPeq.wordWrap = true;
        sPeq.normal.textColor = new Color(0.62f, 0.68f, 0.74f);

        sCab = new GUIStyle(sPeq);
        sCab.fontStyle = FontStyle.Bold;
        sCab.wordWrap = false;

        sCel = new GUIStyle(GUI.skin.label);
        sCel.fontSize = 13;
        sCel.wordWrap = false;

        sErro = new GUIStyle(sPeq);
        sErro.normal.textColor = new Color(1f, 0.45f, 0.4f);

        sAviso = new GUIStyle(sPeq);
        sAviso.normal.textColor = new Color(1f, 0.84f, 0.3f);

        sOk = new GUIStyle(sPeq);
        sOk.normal.textColor = new Color(0.45f, 1f, 0.7f);

        Color texto = Hex(0xe6edf3), cinza = Hex(0x8b98a5), escuro = Hex(0x04201c);
        sBtn = Botao(Hex(0x1b2128), Hex(0x252d36), Hex(0x12171c), texto, false);
        sSel = Botao(Hex(0x2dd4bf), Hex(0x5ee6d3), Hex(0x22a897), escuro, true);
        sAmbar = Botao(Hex(0xf59e0b), Hex(0xfbbf24), Hex(0xc27c06), Hex(0x231500), true);
        sPerigo = Botao(Hex(0xef4444), Hex(0xf87171), Hex(0xb91c1c), Color.white, true);

        sNav = Botao(new Color(0f, 0f, 0f, 0f), Hex(0x161b21), Hex(0x12302d), cinza, false);
        sNav.hover.textColor = texto;
        sNav.alignment = TextAnchor.MiddleLeft;
        sNav.padding = new RectOffset(14, 10, 6, 6);
        sNav.fontSize = 14;
        sNavSel = Botao(Hex(0x12302d), Hex(0x12302d), Hex(0x12302d), Hex(0x2dd4bf), true);
        sNavSel.alignment = TextAnchor.MiddleLeft;
        sNavSel.padding = new RectOffset(14, 10, 6, 6);
        sNavSel.fontSize = 14;

        sCampo = new GUIStyle(GUI.skin.textField);
        sCampo.normal.background = Redondo(Hex(0x0b0e11), Hex(0x262e37));
        sCampo.hover.background = Redondo(Hex(0x0b0e11), Hex(0x3a4651));
        sCampo.focused.background = Redondo(Hex(0x0b0e11), Hex(0x2dd4bf));
        sCampo.active.background = sCampo.focused.background;
        sCampo.onNormal.background = sCampo.normal.background;
        sCampo.onFocused.background = sCampo.focused.background;
        sCampo.normal.textColor = sCampo.hover.textColor = sCampo.focused.textColor = sCampo.active.textColor = texto;
        sCampo.border = new RectOffset(6, 6, 6, 6);
        sCampo.padding = new RectOffset(8, 8, 5, 5);
        sCampo.fontSize = 13;

        // caixa de marcar: quadrado de 18 px a esquerda (borda fixa, o resto da caixa nao desenha nada)
        sMarcar = new GUIStyle(GUI.skin.toggle);
        Texture2D desl = Caixinha(false, Hex(0x0b0e11), Hex(0x3a4651));
        Texture2D desl2 = Caixinha(false, Hex(0x0b0e11), Hex(0x8b98a5));
        Texture2D lig = Caixinha(true, Hex(0x2dd4bf), Hex(0x2dd4bf));
        sMarcar.normal.background = desl; sMarcar.hover.background = desl2; sMarcar.active.background = desl2; sMarcar.focused.background = desl;
        sMarcar.onNormal.background = lig; sMarcar.onHover.background = lig; sMarcar.onActive.background = lig; sMarcar.onFocused.background = lig;
        sMarcar.border = new RectOffset(18, 0, 18, 0);
        sMarcar.overflow = new RectOffset(0, 0, -3, 0);
        sMarcar.padding = new RectOffset(26, 0, 2, 2);
        sMarcar.fontSize = 13;
        sMarcar.normal.textColor = sMarcar.hover.textColor = sMarcar.active.textColor = texto;
        sMarcar.onNormal.textColor = sMarcar.onHover.textColor = sMarcar.onActive.textColor = texto;

        sBarra = new GUIStyle(GUI.skin.verticalScrollbar);
        sBarra.normal.background = Redondo(Hex(0x0b0e11), Hex(0x0b0e11));
        sBarra.border = new RectOffset(6, 6, 6, 6);
        sBarra.fixedWidth = 10f;
        sBarraPolegar = new GUIStyle(GUI.skin.verticalScrollbarThumb);
        sBarraPolegar.normal.background = Redondo(Hex(0x2a333d), Hex(0x2a333d));
        sBarraPolegar.border = new RectOffset(6, 6, 6, 6);
        sBarraPolegar.fixedWidth = 10f;
    }

    // Durante o desenho da janela, os estilos padrao (usados pelos GUILayout.Button/TextField sem estilo)
    // viram os da proposta B; depois voltam, para nao mudar outros paineis.
    private static void TrocarSkin()
    {
        GUISkin k = GUI.skin;
        skBotao = k.button; skCampo = k.textField; skMarcar = k.toggle; skBarra = k.verticalScrollbar; skPolegar = k.verticalScrollbarThumb;
        k.button = sBtn; k.textField = sCampo; k.toggle = sMarcar; k.verticalScrollbar = sBarra; k.verticalScrollbarThumb = sBarraPolegar;
    }

    private static void VoltarSkin()
    {
        GUISkin k = GUI.skin;
        if (skBotao != null) { k.button = skBotao; k.textField = skCampo; k.toggle = skMarcar; k.verticalScrollbar = skBarra; k.verticalScrollbarThumb = skPolegar; }
    }

    // etiqueta de categoria: fundo escuro puxado para a cor e texto na cor (selecionada: fundo na cor, texto escuro)
    private static GUIStyle Chip(Color cor, bool selecionado)
    {
        GUIStyle[] par;
        if (!chips.TryGetValue(cor, out par))
        {
            Color fundo = Color.Lerp(Hex(0x161b21), cor, 0.22f);
            par = new GUIStyle[2];
            par[0] = Botao(fundo, Color.Lerp(fundo, cor, 0.15f), fundo, cor, false);
            par[1] = Botao(cor, cor, cor, Hex(0x0b0e11), true);
            par[0].fontSize = par[1].fontSize = 12;
            chips[cor] = par;
        }
        return par[selecionado ? 1 : 0];
    }

    private static GUIStyle Botao(Color normal, Color hover, Color ativo, Color texto, bool negrito)
    {
        GUIStyle b = new GUIStyle(GUI.skin.button);
        Texture2D n = Redondo(normal, normal), h = Redondo(hover, hover), a = Redondo(ativo, ativo);
        b.normal.background = n; b.hover.background = h; b.active.background = a; b.focused.background = n;
        b.onNormal.background = n; b.onHover.background = h; b.onActive.background = a; b.onFocused.background = n;
        b.normal.textColor = b.hover.textColor = b.active.textColor = b.focused.textColor = texto;
        b.onNormal.textColor = b.onHover.textColor = b.onActive.textColor = b.onFocused.textColor = texto;
        b.border = new RectOffset(6, 6, 6, 6);
        b.padding = new RectOffset(10, 10, 5, 5);
        b.fontSize = 13;
        b.fontStyle = negrito ? FontStyle.Bold : FontStyle.Normal;
        b.alignment = TextAnchor.MiddleCenter;
        return b;
    }

    private static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }

    // retangulo arredondado 16x16 (raio 6) com borda de 1 px, para esticar com border 6
    private static Texture2D Redondo(Color fundo, Color borda)
    {
        const int N = 16;
        const float R = 6f;
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // distancia ate a borda do retangulo arredondado (negativa = dentro)
                float cx = Mathf.Clamp(x + 0.5f, R, N - R), cy = Mathf.Clamp(y + 0.5f, R, N - R);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - R;
                float alfa = Mathf.Clamp01(0.5f - d);
                Color c = d > -1.5f ? borda : fundo;
                c.a *= alfa;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    // caixinha de marcar 18x18; ligada leva um "v" escuro
    private static Texture2D Caixinha(bool ligada, Color fundo, Color borda)
    {
        const int N = 18;
        const float R = 4f;
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color marca = Hex(0x04201c);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, R, N - R), cy = Mathf.Clamp(y + 0.5f, R, N - R);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - R;
                Color c = d > -1.5f ? borda : fundo;
                if (ligada)
                {
                    // "v": perna curta de (4,9) a (7,6) e longa de (7,6) a (14,13) (y cresce para cima na textura)
                    float px = x + 0.5f, py = y + 0.5f;
                    if (DistSeg(px, py, 4.5f, 9.5f, 7.5f, 6f) < 1.3f || DistSeg(px, py, 7.5f, 6f, 13.5f, 12.5f) < 1.3f) c = marca;
                }
                c.a *= Mathf.Clamp01(0.5f - d);
                t.SetPixel(x, y, c);
            }
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    private static float DistSeg(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay;
        float u = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
        float dx = ax + u * vx - px, dy = ay + u * vy - py;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static Texture2D NovaTextura(Color cor)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, cor);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }
}
