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

    private static float larguraUtil = 560f;   // largura do conteudo das abas (para os botoes de escolha)
    private static bool aberto;
    private static int aba = 8;             // 0 Geral, 1 Provedor, 2 Estilo, 3 Glossario, 4 Categorias, 5 Avancado, 6 Log, 7 Fontes, 8 Painel
    private static Vector2 rolagem;
    private static bool mostrarChave;
    private static string campoRpm;
    private static string campoLote;
    private static bool camposIniciados;
    private static Texture2D texFundo;
    private static Texture2D texLateral;
    // visual em PNG (modelo\v_common\UI, gerado por ferramentas\gerar_ui.py); sem o PNG fica a textura gerada
    private static GUIStyle sJanela, sLateral, sContorno;
    private static Texture2D texLogo, texNome, texAlca;
    // arrastar (barra de cima) e redimensionar (canto de baixo) a janela
    private static bool arrastando;
    private static bool redimensionando;
    private static Vector2 pegaMouse;
    private static Rect pegaJanela;
    // animacoes: abrir (fade), destaque da aba que desliza, interruptores
    private static float abertoEm;
    private static float navY = -1f, navX, navW;
    private static readonly float[] navYs = new float[9];
    private static readonly Dictionary<string, float> anims = new Dictionary<string, float>();
    // botao "Traducao" do canto: arrastar sem abrir o menu
    private static bool arrastandoBotao, moveuBotao;
    private static Vector2 pegaBotao, inicioBotao;
    // teclas: 0 = nada, 1 = esperando a tecla do menu, 2 = a de pausar
    private static int capturando;
    private static KeyCode teclaSegurada = KeyCode.None;
    private static string msgTecla = "";
    private static int versaoTema = -1;
    // estilos do tema (cartoes, numeros grandes, etiquetas, links)
    private static GUIStyle sCartao, sCartaoTit, sCartaoDest, sCartaoDestTit, sCartaoDestTxt, sBloco, sNum, sNumDest, sValor, sLink, sLinkDest;
    private static GUIStyle sFechar, sSombra, sVazio, sInterruptor, sRotulo, sOrig, sNavTxt, sNavTxtSel, sNavFundo;
    private static Texture2D texSombra, texBranco, texBolinha, texIntOff, texIntOn, texFaixa, texEtiqueta;
    // visual da proposta B: botoes planos arredondados, teal #2dd4bf no selecionado
    private static GUIStyle sBtn, sSel, sAmbar, sPerigo, sNav, sNavSel, sCampo, sMarcar, sBarra, sBarraPolegar;
    private static readonly Dictionary<Color, GUIStyle[]> chips = new Dictionary<Color, GUIStyle[]>();
    private static GUIStyle skBotao, skCampo, skMarcar, skBarra, skPolegar, skRotulo;
    private static Texture2D texMarcaDesl, texMarcaFoco, texMarcaLig;
    private static GUIStyle sCaixaDesl, sCaixaLig;
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
        // tecla segurada repete o KeyDown: so vale de novo depois de soltar
        if (ev != null && ev.type == EventType.KeyUp && ev.keyCode == teclaSegurada) teclaSegurada = KeyCode.None;
        if (ev != null && ev.type == EventType.KeyDown && ev.keyCode != KeyCode.None && ev.keyCode != teclaSegurada)
        {
            teclaSegurada = ev.keyCode;
            if (capturando != 0)
            {
                CapturarTecla(ev.keyCode);
                ev.Use();
            }
            else if (ev.keyCode == LiveConfig.TeclaMenu)
            {
                if (aberto) Fechar();
                else Abrir();
                ev.Use();
            }
            else if (LiveConfig.TeclaPausa != KeyCode.None && ev.keyCode == LiveConfig.TeclaPausa)
            {
                AlternarTraducao();
                ev.Use();
            }
        }

        float esc = Mathf.Clamp(Screen.height / 1080f, 0.85f, 2.5f);
        Matrix4x4 antigo = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(esc, esc, 1f));
        float sw = Screen.width / esc;
        float sh = Screen.height / esc;
        try
        {
            Tema.Garantir();
            if (versaoTema != Tema.Versao) { versaoTema = Tema.Versao; RefazerEstilos(); }
            PrepararEstilos();
            if (!aberto)
            {
                if (LiveConfig.BotaoVisivel) BotaoCanto(sw, sh);
            }
            else
            {
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

    // tempo desde o ultimo Repaint (as animacoes andam por tempo real, nao por quadro)
    // (Time.unscaledTime e o mesmo durante todo o quadro: so muda o passo quando o quadro muda)
    private static float ultimoQuadro = -1f;
    private static float passoAnim;

    private static float PassoAnim()
    {
        float agora = Time.unscaledTime;
        if (agora != ultimoQuadro)
        {
            passoAnim = ultimoQuadro < 0f ? 0f : Math.Min(0.1f, agora - ultimoQuadro);
            ultimoQuadro = agora;
        }
        return passoAnim;
    }

    private static void Abrir()
    {
        aberto = true;
        camposIniciados = false;
        abertoEm = Time.unscaledTime;
    }

    // Comecar / Pausar (botao do menu e a tecla configuravel)
    private static void AlternarTraducao()
    {
        LiveConfig.Ativo = !LiveConfig.Ativo;
        if (LiveConfig.Ativo) LiveMotor.VarrerAgora = true;
        LiveLog.Info(LiveConfig.Ativo ? "lg_comecou" : "lg_pausada");
    }

    // nome da tecla para mostrar (F8, Insert...)
    private static string NomeTecla(KeyCode k)
    {
        return k == KeyCode.None ? LiveTxt.T("tecla_nenhuma") : k.ToString();
    }

    private static void CapturarTecla(KeyCode k)
    {
        int qual = capturando;
        capturando = 0;
        msgTecla = "";
        if (k == KeyCode.Escape) return;
        KeyCode outra = qual == 1 ? LiveConfig.TeclaPausa : LiveConfig.TeclaMenu;
        if (k == outra) { msgTecla = LiveTxt.T("tecla_repetida"); return; }
        if (qual == 1) LiveConfig.TeclaMenu = k;
        else LiveConfig.TeclaPausa = k;
        LiveConfig.Marcar();
    }

    // Botao "Traducao" do canto: clique abre o menu; segurar e arrastar muda o lugar (salvo no config.txt).
    // Pisca de leve enquanto tem texto na fila.
    private static void BotaoCanto(float sw, float sh)
    {
        Vector2 tam = new Vector2(150f, 28f);
        Vector2 p = LiveConfig.PosBotao;
        if (p.x < 0f) p = new Vector2(sw - tam.x - 4f, 4f);
        p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, sw - tam.x));
        p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, sh - tam.y));
        Rect r = new Rect(p.x, p.y, tam.x, tam.y);

        Event ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition))
        {
            arrastandoBotao = true;
            moveuBotao = false;
            pegaBotao = ev.mousePosition - p;
            inicioBotao = ev.mousePosition;
            ev.Use();
        }
        else if (ev.type == EventType.MouseDrag && arrastandoBotao)
        {
            float dx = ev.mousePosition.x - inicioBotao.x, dy = ev.mousePosition.y - inicioBotao.y;
            if (dx * dx + dy * dy > 16f) moveuBotao = true;
            if (moveuBotao)
            {
                p = ev.mousePosition - pegaBotao;
                p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, sw - tam.x));
                p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, sh - tam.y));
                LiveConfig.PosBotao = p;
                r = new Rect(p.x, p.y, tam.x, tam.y);
            }
            ev.Use();
        }
        else if (ev.rawType == EventType.MouseUp && arrastandoBotao)
        {
            arrastandoBotao = false;
            if (moveuBotao) LiveConfig.Marcar();
            else Abrir();
            ev.Use();
        }

        if (ev.type != EventType.Repaint) return;
        Color antes = GUI.color;
        float a = 0.85f;
        if (LiveMotor.NaFila > 0) a = 0.75f + 0.25f * (float)Math.Sin(Time.unscaledTime * 4f);
        GUI.color = new Color(1f, 1f, 1f, a);
        string rot = LiveTxt.T("canto") + (LiveMotor.NaFila > 0 ? " (" + LiveMotor.NaFila + ")" : "");
        GUI.Box(r, new GUIContent(rot), LiveConfig.Ativo ? sSel : sBtn);
        // bolinha de estado: cor do texto do botao traduzindo, ambar pausado
        if (texBolinha != null)
        {
            Color c = LiveConfig.Ativo ? Tema.DestaqueTexto : Tema.Aviso;
            GUI.color = new Color(c.r, c.g, c.b, a);
            GUI.DrawTexture(new Rect(r.x + 10f, r.y + 10f, 8f, 8f), texBolinha);
        }
        GUI.color = antes;
    }

    private static void Fechar()
    {
        aberto = false;
        capturando = 0;
        arrastando = false;
        redimensionando = false;
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

        // primeira vez: 65% da largura e 85% da altura, centralizada (minimo 820 x 580); depois fica onde o jogador deixou
        Rect r = LiveConfig.Janela;
        if (r.width <= 0f)
        {
            float w0 = Mathf.Min(Mathf.Max(820f, sw * 0.65f), sw - 20f);
            float h0 = Mathf.Min(Mathf.Max(580f, sh * 0.85f), sh - 40f);
            r = new Rect((sw - w0) / 2f, 20f, w0, h0);
        }
        r.width = Mathf.Clamp(r.width, 820f, Mathf.Max(820f, sw - 10f));
        r.height = Mathf.Clamp(r.height, 580f, Mathf.Max(580f, sh - 10f));
        r.x = Mathf.Clamp(r.x, 60f - r.width, sw - 60f);
        r.y = Mathf.Clamp(r.y, 0f, sh - 40f);
        r = ArrastarJanela(r);
        float w = r.width;
        float h = r.height;
        porPagina = Mathf.Clamp((int)((h - 380f) / 38f), 8, 20);

        // transparencia escolhida + fade rapido ao abrir
        Color corAntes = GUI.color;
        float alfa = LiveConfig.Opacidade / 100f * Mathf.Clamp01((Time.unscaledTime - abertoEm) / 0.15f);
        GUI.color = new Color(1f, 1f, 1f, alfa);

        if (sSombra != null) GUI.Box(new Rect(r.x - 26f, r.y - 18f, r.width + 52f, r.height + 52f), GUIContent.none, sSombra);
        if (sJanela != null) GUI.Box(r, GUIContent.none, sJanela);
        else GUI.DrawTexture(r, texFundo);
        Rect lat = new Rect(r.x, r.y, 190f, h);
        if (sLateral != null) GUI.Box(lat, GUIContent.none, sLateral);
        else GUI.DrawTexture(lat, texLateral);
        Lateral(lat);

        larguraUtil = w - 214f - 34f;
        GUILayout.BeginArea(new Rect(r.x + 206f, r.y + 12f, w - 220f, h - 22f));

        // barra de titulo: nome da aba, etiqueta de estado, fila e o "x"
        GUILayout.BeginHorizontal();
        GUILayout.Label(TituloAba(), sTitulo);
        GUILayout.Space(6f);
        bool erro = LiveMotor.UltimoErro.Length > 0 && LiveMotor.Erros > 0 && LiveConfig.Ativo;
        GUILayout.Label(LiveTxt.T(erro ? "st_erro" : LiveConfig.Ativo ? "st_ativa" : "st_pausada"),
            Chip(erro ? Tema.Erro : LiveConfig.Ativo ? Tema.Ok : Tema.Aviso, false), GUILayout.Height(24f));
        GUILayout.FlexibleSpace();
        if (LiveMotor.NaFila > 0) GUILayout.Label(LiveTxt.F("fila_n", LiveMotor.NaFila), sPeq, GUILayout.Height(28f));
        if (GUILayout.Button("", sFechar, GUILayout.Width(28f), GUILayout.Height(28f))) Fechar();
        GUILayout.EndHorizontal();
        BarraFila();
        GUILayout.Space(8f);

        rolagem = GUILayout.BeginScrollView(rolagem, GUILayout.Height(h - 136f));
        switch (aba)
        {
            case 8: PaginaPainel(); break;
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

        // alca de redimensionar no canto de baixo
        if (texAlca != null)
        {
            GUI.color = new Color(Tema.Mudo.r, Tema.Mudo.g, Tema.Mudo.b, alfa);
            GUI.DrawTexture(new Rect(r.xMax - 18f, r.yMax - 18f, 14f, 14f), texAlca);
        }
        GUI.color = corAntes;
    }

    // linha embaixo do titulo; com texto na fila vira uma barra que corre (traduzindo)
    private static void BarraFila()
    {
        Rect lr = GUILayoutUtility.GetRect(1f, 3f, sVazio, GUILayout.ExpandWidth(true), GUILayout.Height(3f));
        if (Event.current.type != EventType.Repaint || texBranco == null) return;
        Color antes = GUI.color;
        GUI.color = new Color(Tema.Borda.r, Tema.Borda.g, Tema.Borda.b, antes.a);
        GUI.DrawTexture(new Rect(lr.x, lr.y + 1f, lr.width, 1f), texBranco);
        if (LiveMotor.NaFila > 0)
        {
            float t = (Time.unscaledTime * 0.6f) % 1f;
            float bw = lr.width * 0.25f;
            float x = lr.x - bw + t * (lr.width + bw);
            float x0 = Mathf.Max(lr.x, x), x1 = Mathf.Min(lr.xMax, x + bw);
            GUI.color = new Color(Tema.Destaque.r, Tema.Destaque.g, Tema.Destaque.b, antes.a);
            if (x1 > x0) GUI.DrawTexture(new Rect(x0, lr.y, x1 - x0, 3f), texBranco);
        }
        GUI.color = antes;
    }

    // Barra de cima (logo e titulo) arrasta; o canto de baixo (24 x 24) muda o tamanho. Salva no config.txt.
    private static Rect ArrastarJanela(Rect r)
    {
        Event ev = Event.current;
        if (ev == null) return r;
        Rect barra = new Rect(r.x + 190f, r.y, r.width - 330f, 44f);
        Rect barraLat = new Rect(r.x, r.y, 190f, 70f);
        Rect canto = new Rect(r.xMax - 24f, r.yMax - 24f, 24f, 24f);
        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            if (canto.Contains(ev.mousePosition)) redimensionando = true;
            else if (barra.Contains(ev.mousePosition) || barraLat.Contains(ev.mousePosition)) arrastando = true;
            if (arrastando || redimensionando)
            {
                pegaMouse = ev.mousePosition;
                pegaJanela = r;
                GUIUtility.keyboardControl = 0;
                ev.Use();
            }
        }
        else if (ev.type == EventType.MouseDrag && (arrastando || redimensionando))
        {
            Vector2 d = ev.mousePosition - pegaMouse;
            if (arrastando) { r.x = pegaJanela.x + d.x; r.y = pegaJanela.y + d.y; }
            else { r.width = Mathf.Max(820f, pegaJanela.width + d.x); r.height = Mathf.Max(580f, pegaJanela.height + d.y); }
            ev.Use();
        }
        else if (ev.rawType == EventType.MouseUp && (arrastando || redimensionando))
        {
            arrastando = false;
            redimensionando = false;
            LiveConfig.Marcar();
        }
        LiveConfig.Janela = r;
        return r;
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
            case 8: return LiveTxt.T("nav_painel");
            default: return LiveTxt.T("nav_geral");
        }
    }

    private static void Lateral(Rect lat)
    {
        GUILayout.BeginArea(new Rect(lat.x + 12f, lat.y + 14f, lat.width - 24f, lat.height - 28f));
        if (texLogo != null && texNome != null)
        {
            // logo PR + nome em Space Grotesk (PNGs do tema), o autor e a faixa da bandeira (temas Brasil e EUA)
            Rect topo = GUILayoutUtility.GetRect(lat.width - 24f, 62f, sPeq, GUILayout.Height(62f));
            GUI.DrawTexture(new Rect(topo.x + 2f, topo.y + 4f, 40f, 40f), texLogo);
            float nw = texNome.width / 2f, nh = texNome.height / 2f;
            GUI.DrawTexture(new Rect(topo.x + 50f, topo.y + 2f, nw, nh), texNome);
            GUI.Label(new Rect(topo.x + 50f, topo.y + 4f + nh, 120f, 18f), "By PauloRaz3n", sPeq);
            if (texFaixa != null) GUI.DrawTexture(new Rect(topo.x + 2f, topo.y + 56f, topo.width - 4f, 3f), texFaixa);
        }
        else
        {
            GUILayout.Label("Universal", sMarca);
            GUILayout.Label("Translator", sMarca);
            GUILayout.Label("By PauloRaz3n", sPeq);
        }
        GUILayout.Space(8f);
        BotaoAtivar(lat.width - 24f, 34f);
        GUILayout.Space(8f);

        // fundo da aba escolhida: desliza ate a aba nova
        if (Event.current.type == EventType.Repaint && navW > 0f)
        {
            float alvo = navYs[aba];
            if (navY < 0f) navY = alvo;
            navY += (alvo - navY) * (1f - (float)Math.Exp(-PassoAnim() * 16f));
            if (Math.Abs(navY - alvo) < 0.3f) navY = alvo;
            GUI.Box(new Rect(navX, navY, navW, 34f), GUIContent.none, sNavFundo);
        }
        Nav(8, "nav_painel", "ic_painel");
        Nav(0, "nav_geral", "ic_geral");
        Nav(1, "nav_prov", "ic_provedor");
        Nav(2, "nav_estilo", "ic_estilo");
        Nav(3, "nav_glos", "ic_glossario");
        Nav(4, "nav_cats", "ic_categorias");
        Nav(5, "nav_avanc", "ic_avancado");
        Nav(6, "nav_log", "ic_log");
        Nav(7, "nav_fontes", "ic_fontes");
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
        bool ativo = LiveConfig.Ativo;
        if (GUILayout.Button(LiveTxt.T(ativo ? "pausar" : "comecar"), ativo ? sAmbar : sSel, GUILayout.Width(largura), GUILayout.Height(altura)))
            AlternarTraducao();
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

    // Interruptor (trilho + bolinha que desliza) e o texto ao lado, os dois clicaveis.
    // Sem os PNGs do tema volta a ser a caixinha de 18 px.
    private static bool Marca(bool valor, string texto)
    {
        GUILayout.BeginHorizontal();
        bool clicou;
        if (texIntOff != null && texIntOn != null && texBolinha != null)
        {
            Rect rs = GUILayoutUtility.GetRect(36f, 20f, sInterruptor, GUILayout.Width(36f), GUILayout.Height(20f));
            clicou = GUI.Button(rs, GUIContent.none, sVazio);
            if (Event.current.type == EventType.Repaint)
            {
                float k;
                if (!anims.TryGetValue(texto, out k)) k = valor ? 1f : 0f;
                float passo = PassoAnim() * 8f, alvo = valor ? 1f : 0f;
                k = k < alvo ? Math.Min(alvo, k + passo) : Math.Max(alvo, k - passo);
                anims[texto] = k;
                Color antes = GUI.color;
                GUI.DrawTexture(rs, texIntOff);
                GUI.color = new Color(1f, 1f, 1f, antes.a * k);
                GUI.DrawTexture(rs, texIntOn);
                GUI.color = antes;
                GUI.DrawTexture(new Rect(rs.x + 2f + 16f * k, rs.y + 2f, 16f, 16f), texBolinha);
            }
        }
        else clicou = GUILayout.Button("", valor ? sCaixaLig : sCaixaDesl, GUILayout.Width(18f), GUILayout.Height(18f));
        if (GUILayout.Button(texto, sMarcar)) clicou = true;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return clicou ? !valor : valor;
    }

    // aba da lateral: icone (tingido) + nome; o fundo da escolhida e desenhado em Lateral (desliza)
    private static void Nav(int idx, string chave, string icone)
    {
        bool sel = aba == idx;
        string texto = LiveTxt.T(chave);
        Rect r = GUILayoutUtility.GetRect(new GUIContent(texto), sNavTxt, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            navYs[idx] = r.y;
            navX = r.x;
            navW = r.width;
        }
        if (GUI.Button(r, new GUIContent(texto), sel ? sNavTxtSel : sNavTxt))
        {
            aba = idx;
            rolagem = Vector2.zero;
            confLimpar = false;
            capturando = 0;
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
        Texture2D ic = Png.Tex(icone);
        if (ic != null && Event.current.type == EventType.Repaint)
        {
            Color antes = GUI.color;
            Color c = sel ? Tema.Destaque : Tema.Mudo;
            GUI.color = new Color(c.r, c.g, c.b, antes.a);
            GUI.DrawTexture(new Rect(r.x + 10f, r.y + 8f, 18f, 18f), ic);
            GUI.color = antes;
        }
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

        Cartao(LiveTxt.T("modo"));
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
        FimCartao();

        Cartao(LiveTxt.T("traduzir_para"));
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
        FimCartao();

        Cartao(LiveTxt.T("o_que"));
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
        FimCartao();
    }

    // ---------------- aba 2: Provedor / API ----------------

    private static void PaginaProvedor(MonoBehaviour drv)
    {
        bool gratis = LiveConfig.ProvedorId == LiveConfig.IdGoogle;
        Provedor p = LiveConfig.Atual;
        Cartao(gratis ? LiveTxt.T("modo_gratis") : LiveTxt.T("servico"));
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
        FimCartao();
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

        Cartao(LiveTxt.T("estilo_titulo"));
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
        FimCartao();

        Cartao(null);
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
        FimCartao();
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
        Cartao(LiveTxt.T("av_ritmo"));
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
        FimCartao();

        Cartao(LiveTxt.T("av_opcoes"));
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
        FimCartao();

        PainelAparencia();
        PainelTeclas();

        Cartao(LiveTxt.T("av_ferr"));
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
        FimCartao();

        Cartao(LiveTxt.T("av_cache"));
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
        FimCartao();
        GUILayout.Label(LiveTxt.T("aviso_teclas"), sPeq);
    }

    // tema, transparencia e botao do canto
    private static void PainelAparencia()
    {
        Cartao(LiveTxt.T("av_aparencia"));
        GUILayout.Label(LiveTxt.T("tema"), sPeq);
        int n = Tema.Quantos;
        string[] nomes = new string[n];
        for (int i = 0; i < n; i++) nomes[i] = Tema.Nome(i);
        int t = Escolha(Tema.Indice, nomes, 3, larguraUtil);
        if (t != Tema.Indice) Tema.Escolher(t);
        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("opacidade"), GUILayout.Width(250f));
        if (GUILayout.Button("-", GUILayout.Width(28f))) { LiveConfig.Opacidade = Mathf.Clamp(LiveConfig.Opacidade - 10, 40, 100); LiveConfig.Marcar(); }
        GUILayout.Label(LiveConfig.Opacidade + "%", GUILayout.Width(50f));
        if (GUILayout.Button("+", GUILayout.Width(28f))) { LiveConfig.Opacidade = Mathf.Clamp(LiveConfig.Opacidade + 10, 40, 100); LiveConfig.Marcar(); }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        bool botao = Marca(LiveConfig.BotaoVisivel, LiveTxt.F("botao_canto", NomeTecla(LiveConfig.TeclaMenu)));
        if (botao != LiveConfig.BotaoVisivel)
        {
            LiveConfig.BotaoVisivel = botao;
            LiveConfig.Marcar();
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("botao_dica"), sPeq);
        if (GUILayout.Button(LiveTxt.T("botao_resetar"), GUILayout.Width(240f)))
        {
            LiveConfig.PosBotao = new Vector2(-1f, -1f);
            LiveConfig.Marcar();
        }
        GUILayout.EndHorizontal();
        FimCartao();
    }

    // teclas de abrir o menu e de pausar/continuar: "Trocar" espera a proxima tecla
    private static void PainelTeclas()
    {
        Cartao(LiveTxt.T("av_teclas"));
        LinhaTecla(1, LiveTxt.T("tecla_menu"), LiveConfig.TeclaMenu, false);
        LinhaTecla(2, LiveTxt.T("tecla_pausa"), LiveConfig.TeclaPausa, true);
        if (capturando != 0) GUILayout.Label(LiveTxt.T("tecla_aperte"), sAviso);
        else if (msgTecla.Length > 0) GUILayout.Label(msgTecla, sErro);
        FimCartao();
    }

    private static void LinhaTecla(int qual, string rotulo, KeyCode tecla, bool podeTirar)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(rotulo, GUILayout.Width(250f));
        GUILayout.Label(capturando == qual ? "..." : NomeTecla(tecla), Chip(Tema.Destaque, capturando == qual), GUILayout.Width(110f), GUILayout.Height(26f));
        if (GUILayout.Button(LiveTxt.T("tecla_trocar"), GUILayout.Width(100f)))
        {
            capturando = capturando == qual ? 0 : qual;
            msgTecla = "";
        }
        if (podeTirar && tecla != KeyCode.None && GUILayout.Button(LiveTxt.T("tecla_sem"), GUILayout.Width(100f)))
        {
            LiveConfig.TeclaPausa = KeyCode.None;
            LiveConfig.Marcar();
            capturando = 0;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    // ---------------- aba Painel ----------------

    // cartao com titulo pequeno em maiusculas; larguraUtil encolhe o recuo do cartao ate o FimCartao
    private static void Cartao(string titulo)
    {
        GUILayout.BeginVertical(sCartao);
        if (titulo != null) GUILayout.Label(titulo.ToUpper(), sCartaoTit);
        larguraUtil -= 30f;
    }

    private static void FimCartao()
    {
        larguraUtil += 30f;
        GUILayout.EndVertical();
        GUILayout.Space(8f);
    }

    // 1.284 (ponto de milhar; virgula no ingles)
    private static string Num(int n)
    {
        string s = n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        return LiveTxt.Atual == 1 ? s : s.Replace(',', '.');
    }

    private static void PaginaPainel()
    {
        bool gratis = LiveConfig.ProvedorId == LiveConfig.IdGoogle;

        // faixa de estado
        GUILayout.BeginVertical(sCartao);
        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T(LiveConfig.Ativo ? "st_ativa" : "st_pausada"), Chip(LiveConfig.Ativo ? Tema.Ok : Tema.Aviso, true), GUILayout.Width(96f), GUILayout.Height(28f));
        GUILayout.Label(LiveTxt.T(LiveConfig.Ativo ? "st_sub_ativa" : "st_sub_pausada"), sPeq, GUILayout.Height(28f));
        GUILayout.FlexibleSpace();
        BotaoAtivar(180f, 32f);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(8f);

        // 6 cartoes (3 por linha); o "Abrir" leva para a aba
        float cw = Mathf.Floor((larguraUtil - 30f) / 3f);   // 3 cartoes + margens de 4 px
        GUILayout.BeginHorizontal();
        CartaoResumo(cw, LiveTxt.T("pn_modo"), gratis ? LiveTxt.T("modo_gratis") : LiveConfig.Atual.Nome,
            gratis ? LiveTxt.T("pn_gratis_sub") : LiveTxt.T("pn_api_sub"), gratis ? 0 : 1, false);
        CartaoResumo(cw, LiveTxt.T("pn_idioma"), NomeIdiomaAtual(), LiveTxt.T("pn_idioma_sub"), 0, false);
        CartaoResumo(cw, LiveTxt.T("pn_estilo"), gratis ? "-" : LiveTxt.T("estilo" + LiveConfig.Estilo),
            gratis ? LiveTxt.T("pn_estilo_gratis") : LiveTxt.F("pn_estilo_sub", (LiveConfig.PromptExtra ?? "").Length, MaxExtras), 2, false);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        int travados = 0;
        List<TermoGlossario> lista = Glossario.Lista;
        for (int i = 0; i < lista.Count; i++) if (lista[i].Travado) travados++;
        CartaoResumo(cw, LiveTxt.T("pn_glos"), LiveTxt.F("pn_termos", Num(Glossario.Total)),
            LiveTxt.F("pn_termos_sub", Categorias.Ordem().Length, Num(travados)), 3, true);
        CartaoResumo(cw, LiveTxt.T("pn_cache_card"), gratis ? LiveTxt.T("pn_ritmo_auto") : LiveTxt.F("pn_rpm_val", LiveConfig.Rpm),
            LiveTxt.F("pn_cache_sub", Num(TradCache.Total)), 5, false);
        CartaoResumo(cw, LiveTxt.T("pn_fontes"), FonteEscolhida.Indice <= 0 ? LiveTxt.T("fonte_original") : FonteEscolhida.Nome(FonteEscolhida.Indice),
            LiveTxt.T("pn_fontes_sub"), 7, false);
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);

        // estado: numeros grandes, limite de pedidos e cache
        Cartao(LiveTxt.T("pn_estado"));
        float bw = Mathf.Floor((larguraUtil - 12f) / 2f);
        GUILayout.BeginHorizontal();
        Bloco(bw, LiveTxt.T("pn_hoje"), Num(LiveConfig.TraduzidosHoje), null);
        Bloco(bw, LiveTxt.T("pn_cache"), Num(TradCache.Total), null);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Bloco(bw, LiveTxt.T("pn_pedmin"), Num(LiveMotor.PedidosMinuto), gratis ? null : LiveTxt.F("pn_de", LiveConfig.Rpm));
        Bloco(bw, LiveTxt.T("pn_glos"), Num(Glossario.Total), null);
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
        GUILayout.Label(LiveTxt.T("pn_limite").ToUpper(), sCartaoTit);
        if (gratis) GUILayout.Label(LiveTxt.T("pn_limite_gratis"), sPeq);
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(44f), GUILayout.Height(34f))) MudarRpm(-5, 600);
            string nrpm = GUILayout.TextField(campoRpm ?? LiveConfig.Rpm.ToString(), GUILayout.Height(34f));
            if (nrpm != campoRpm)
            {
                campoRpm = nrpm;
                int v;
                if (int.TryParse(nrpm, out v)) { LiveConfig.Rpm = Mathf.Clamp(v, 1, 600); LiveConfig.Marcar(); }
            }
            if (GUILayout.Button("+", GUILayout.Width(44f), GUILayout.Height(34f))) MudarRpm(5, 600);
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6f);
        GUILayout.Label(LiveTxt.T("pn_cache_disco").ToUpper(), sCartaoTit);
        GUILayout.BeginHorizontal();
        GUILayout.Label(LiveTxt.T("pn_ativado"), Chip(Tema.Ok, false), GUILayout.Width(bw - 8f), GUILayout.Height(32f));
        if (GUILayout.Button(LiveTxt.T("limpar_cache"), GUILayout.Width(bw - 8f), GUILayout.Height(32f)))
        {
            aba = 5;
            rolagem = Vector2.zero;
            confLimpar = true;   // na aba Avancado, o proximo clique apaga
        }
        GUILayout.EndHorizontal();
        FimCartao();

        // ultimas traducoes (a mais nova em cima)
        Cartao(LiveTxt.T("pn_ultimas"));
        List<string[]> ult = LiveMotor.Ultimas;
        if (ult.Count == 0) GUILayout.Label(LiveTxt.T("pn_ultimas_vazio"), sPeq);
        float wo = Mathf.Floor(larguraUtil * 0.28f), wt = Mathf.Floor(larguraUtil * 0.3f);
        for (int i = ult.Count - 1; i >= 0 && i >= ult.Count - 8; i--)
        {
            string orig = ult[i][0], trad = ult[i][1];
            GUILayout.BeginHorizontal();
            GUILayout.Label(Cortar(orig.Replace("\n", " "), (int)(wo / 8f)), sOrig, GUILayout.Width(wo), GUILayout.Height(26f));
            GUILayout.Label("→", sPeq, GUILayout.Width(18f), GUILayout.Height(26f));
            GUILayout.Label(Cortar(trad.Replace("\n", " "), (int)(wt / 8f)), sValor, GUILayout.Width(wt), GUILayout.Height(26f));
            GUILayout.FlexibleSpace();
            TermoGlossario tg = Glossario.Obter(orig);
            if (tg != null) GUILayout.Label(Cortar(Categorias.Nome(tg.Cat), 12), Chip(Categorias.Cor(tg.Cat), false), GUILayout.Height(24f));
            if (GUILayout.Button(LiveTxt.T("editar"), GUILayout.Width(80f), GUILayout.Height(26f)))
            {
                // abre o glossario ja buscando esse texto
                aba = 3;
                rolagem = Vector2.zero;
                busca = orig;
                pagina = 0;
                termoEdit = null;
                if (tg == null) { novoAberto = true; novoOrig = orig; novoTrad = trad; }
            }
            GUILayout.EndHorizontal();
        }
        FimCartao();
    }

    private static string NomeIdiomaAtual()
    {
        for (int i = 0; i < LiveConfig.Idiomas.Length; i++)
            if (LiveConfig.Idiomas[i][0] == LiveConfig.Idioma) return LiveConfig.Idiomas[i][1];
        return LiveConfig.Idioma;
    }

    // cartao do resumo: titulo, valor, linha de baixo e "Abrir"; destacado = cor do tema no fundo
    private static void CartaoResumo(float largura, string titulo, string valor, string sub, int abaDestino, bool destacado)
    {
        GUILayout.BeginVertical(destacado ? sCartaoDest : sCartao, GUILayout.Width(largura), GUILayout.Height(118f));
        GUILayout.Label(titulo.ToUpper(), destacado ? sCartaoDestTit : sCartaoTit);
        GUILayout.Label(Cortar(valor, (int)(largura / 10f)), destacado ? sNumDest : sValor);
        GUILayout.Label(Cortar(sub, (int)(largura / 6.5f)), destacado ? sCartaoDestTxt : sPeq);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(LiveTxt.T("pn_abrir"), destacado ? sLinkDest : sLink))
        {
            aba = abaDestino;
            rolagem = Vector2.zero;
            pagina = 0;
        }
        GUILayout.EndVertical();
    }

    // bloco do quadro Estado: rotulo pequeno e numero grande (com "de 30" opcional)
    private static void Bloco(float largura, string rotulo, string valor, string extra)
    {
        GUILayout.BeginVertical(sBloco, GUILayout.Width(largura));
        GUILayout.Label(rotulo, sPeq);
        GUILayout.BeginHorizontal();
        GUILayout.Label(valor, sNum);
        if (extra != null) GUILayout.Label(extra, sPeq, GUILayout.Height(30f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
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
                GUIStyle est = EstiloPrevia(j, j == sel);
                if (GUILayout.Button(est != null ? "" : nome, est != null ? est : (j == sel ? sSel : sBtn), GUILayout.Width(bw), GUILayout.Height(38f)))
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

    // Estilo cujo fundo e a imagem do botao com o nome na propria fonte. So a coluna lisa antes da
    // borda direita estica (9-slice), entao o texto nao deforma. null = fonte sem imagem.
    private static readonly Dictionary<string, GUIStyle> estilosPrevia = new Dictionary<string, GUIStyle>();
    private static int versaoPrevias = -1;

    private static GUIStyle EstiloPrevia(int j, bool selecionada)
    {
        if (versaoPrevias != FonteEscolhida.Versao) { estilosPrevia.Clear(); versaoPrevias = FonteEscolhida.Versao; }
        string chave = FonteEscolhida.Nome(j) + (selecionada ? "|s" : "|n");
        GUIStyle e;
        if (estilosPrevia.TryGetValue(chave, out e)) return e;
        e = null;
        Texture2D normal = FonteEscolhida.Previa(j, selecionada ? "_sel" : "");
        Texture2D foco = selecionada ? normal : FonteEscolhida.Previa(j, "_hover");
        if (normal != null)
        {
            if (foco == null) foco = normal;
            e = new GUIStyle(GUI.skin.label);
            e.normal.background = normal;
            e.hover.background = foco; e.active.background = foco; e.focused.background = normal;
            e.border = new RectOffset(Mathf.Max(0, normal.width - 9), 8, 0, 0);
            e.margin = new RectOffset(2, 2, 2, 2);
            e.padding = new RectOffset(0, 0, 0, 0);
        }
        estilosPrevia[chave] = e;
        return e;
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

    // tema trocado: joga fora os estilos e as texturas geradas; o PrepararEstilos refaz com as cores novas
    private static void RefazerEstilos()
    {
        texFundo = null;
        chips.Clear();
        anims.Clear();
        navY = -1f;
    }

    private static void PrepararEstilos()
    {
        if (texFundo != null) return;
        texFundo = NovaTextura(Tema.Fundo);
        texLateral = NovaTextura(Tema.Lateral);
        Color texto = Tema.Texto, cinza = Tema.Mudo;

        sRotulo = new GUIStyle(GUI.skin.label);
        sRotulo.normal.textColor = sRotulo.hover.textColor = sRotulo.active.textColor = texto;
        sRotulo.fontSize = 13;

        sTitulo = new GUIStyle(sRotulo);
        sTitulo.fontSize = 22;
        sTitulo.fontStyle = FontStyle.Bold;
        sTitulo.wordWrap = false;
        sTitulo.clipping = TextClipping.Overflow;

        sMarca = new GUIStyle(sRotulo);
        sMarca.fontSize = 20;
        sMarca.fontStyle = FontStyle.Bold;
        sMarca.normal.textColor = Tema.Destaque;

        sPeq = new GUIStyle(sRotulo);
        sPeq.fontSize = 12;
        sPeq.wordWrap = true;
        sPeq.normal.textColor = cinza;

        sCab = new GUIStyle(sPeq);
        sCab.fontStyle = FontStyle.Bold;
        sCab.wordWrap = false;

        sCel = new GUIStyle(sRotulo);
        sCel.wordWrap = false;

        sOrig = new GUIStyle(sPeq);
        sOrig.wordWrap = false;
        sOrig.alignment = TextAnchor.MiddleLeft;

        sValor = new GUIStyle(sRotulo);
        sValor.fontSize = 15;
        sValor.fontStyle = FontStyle.Bold;
        sValor.wordWrap = false;
        sValor.alignment = TextAnchor.MiddleLeft;

        sNum = new GUIStyle(sRotulo);
        sNum.fontSize = 24;
        sNum.fontStyle = FontStyle.Bold;
        sNum.wordWrap = false;

        sErro = new GUIStyle(sPeq);
        sErro.normal.textColor = Tema.Erro;
        sAviso = new GUIStyle(sPeq);
        sAviso.normal.textColor = Tema.Aviso;
        sOk = new GUIStyle(sPeq);
        sOk.normal.textColor = Tema.Ok;

        sBtn = Botao(Color.Lerp(Tema.Card, texto, 0.05f), Color.Lerp(Tema.Card, texto, 0.11f), Tema.Lateral, texto, false);
        sSel = Botao(Tema.Destaque, Color.Lerp(Tema.Destaque, Color.white, 0.25f), Color.Lerp(Tema.Destaque, Color.black, 0.15f), Tema.DestaqueTexto, true);
        sAmbar = Botao(Tema.Sec, Color.Lerp(Tema.Sec, Color.white, 0.18f), Color.Lerp(Tema.Sec, Color.black, 0.15f), Tema.SecTexto, true);
        sPerigo = Botao(Tema.Hex(0xef4444), Tema.Hex(0xf87171), Tema.Hex(0xb91c1c), Color.white, true);

        // abas da lateral: so o texto (o fundo da escolhida desliza, desenhado a parte); espaco a esquerda para o icone
        sNav = Botao(new Color(0f, 0f, 0f, 0f), Color.Lerp(Tema.Lateral, texto, 0.05f), Tema.NavAtivo, cinza, false);
        sNav.hover.textColor = texto;
        sNav.alignment = TextAnchor.MiddleLeft;
        sNav.padding = new RectOffset(14, 10, 6, 6);
        sNav.fontSize = 14;
        sNavSel = Botao(Tema.NavAtivo, Tema.NavAtivo, Tema.NavAtivo, Tema.Destaque, true);
        sNavSel.alignment = TextAnchor.MiddleLeft;
        sNavSel.padding = new RectOffset(14, 10, 6, 6);
        sNavSel.fontSize = 14;
        sNavTxt = new GUIStyle(sNav);
        sNavTxt.padding = new RectOffset(38, 10, 6, 6);
        sNavTxt.margin = new RectOffset(0, 0, 0, 0);
        sNavTxtSel = new GUIStyle(sNavTxt);
        sNavTxtSel.normal.background = null; sNavTxtSel.onNormal.background = null; sNavTxtSel.focused.background = null;
        sNavTxtSel.hover.background = null; sNavTxtSel.onHover.background = null;
        sNavTxtSel.normal.textColor = sNavTxtSel.hover.textColor = sNavTxtSel.active.textColor = sNavTxtSel.focused.textColor = Tema.Destaque;
        sNavTxtSel.fontStyle = FontStyle.Bold;
        sNavFundo = new GUIStyle();
        sNavFundo.normal.background = Redondo(Tema.NavAtivo, Tema.NavAtivo);
        sNavFundo.border = new RectOffset(6, 6, 6, 6);

        sCampo = new GUIStyle(GUI.skin.textField);
        sCampo.normal.background = Redondo(Tema.Campo, Tema.Borda);
        sCampo.hover.background = Redondo(Tema.Campo, Tema.BordaHover);
        sCampo.focused.background = Redondo(Tema.Campo, Tema.Destaque);
        sCampo.active.background = sCampo.focused.background;
        sCampo.onNormal.background = sCampo.normal.background;
        sCampo.onFocused.background = sCampo.focused.background;
        sCampo.normal.textColor = sCampo.hover.textColor = sCampo.focused.textColor = sCampo.active.textColor = texto;
        sCampo.border = new RectOffset(6, 6, 6, 6);
        sCampo.padding = new RectOffset(8, 8, 5, 5);
        sCampo.fontSize = 13;

        // caixa de marcar (reserva do interruptor): quadradinho de 18 px a parte e o texto num botao sem fundo
        texMarcaDesl = Caixinha(false, Tema.Campo, Tema.BordaHover);
        texMarcaFoco = Caixinha(false, Tema.Campo, cinza);
        texMarcaLig = Caixinha(true, Tema.Destaque, Tema.Destaque);
        sCaixaDesl = new GUIStyle(GUI.skin.label);
        sCaixaDesl.border = new RectOffset(0, 0, 0, 0);
        sCaixaDesl.padding = new RectOffset(0, 0, 0, 0);
        sCaixaDesl.overflow = new RectOffset(0, 0, 0, 0);
        sCaixaDesl.normal.background = texMarcaDesl; sCaixaDesl.focused.background = texMarcaDesl;
        sCaixaDesl.hover.background = texMarcaFoco; sCaixaDesl.active.background = texMarcaFoco;
        sCaixaDesl.margin = new RectOffset(4, 6, 5, 3);
        sCaixaLig = new GUIStyle(sCaixaDesl);
        sCaixaLig.normal.background = texMarcaLig; sCaixaLig.focused.background = texMarcaLig;
        sCaixaLig.hover.background = texMarcaLig; sCaixaLig.active.background = texMarcaLig;
        sMarcar = new GUIStyle(GUI.skin.label);
        sMarcar.border = new RectOffset(0, 0, 0, 0);
        sMarcar.overflow = new RectOffset(0, 0, 0, 0);
        sMarcar.padding = new RectOffset(0, 0, 3, 3);
        sMarcar.margin = new RectOffset(0, 4, 3, 3);
        sMarcar.alignment = TextAnchor.MiddleLeft;
        sMarcar.wordWrap = true;
        sMarcar.stretchWidth = false;
        sMarcar.fontSize = 13;
        sMarcar.normal.textColor = sMarcar.hover.textColor = sMarcar.active.textColor = texto;
        sMarcar.onNormal.textColor = sMarcar.onHover.textColor = sMarcar.onActive.textColor = texto;
        sInterruptor = new GUIStyle();
        sInterruptor.margin = new RectOffset(4, 8, 4, 2);
        sVazio = new GUIStyle();

        sBarra = new GUIStyle(GUI.skin.verticalScrollbar);
        sBarra.normal.background = Redondo(Tema.Lateral, Tema.Lateral);
        sBarra.border = new RectOffset(6, 6, 6, 6);
        sBarra.fixedWidth = 10f;
        sBarraPolegar = new GUIStyle(GUI.skin.verticalScrollbarThumb);
        sBarraPolegar.normal.background = Redondo(Tema.BordaHover, Tema.BordaHover);
        sBarraPolegar.border = new RectOffset(6, 6, 6, 6);
        sBarraPolegar.fixedWidth = 10f;

        // cartoes: comum (fundo do cartao) e destacado (cor do tema, texto escuro); blocos do quadro Estado
        sCartao = new GUIStyle();
        sCartao.normal.background = Redondo(Tema.Card, Tema.Claro ? Tema.Borda : Tema.Card);
        sCartao.border = new RectOffset(6, 6, 6, 6);
        sCartao.padding = new RectOffset(14, 14, 10, 12);
        sCartao.margin = new RectOffset(4, 4, 4, 4);
        sCartaoTit = new GUIStyle(sPeq);
        sCartaoTit.fontStyle = FontStyle.Bold;
        sCartaoTit.fontSize = 11;
        sCartaoTit.wordWrap = false;
        sCartaoTit.margin = new RectOffset(4, 4, 2, 6);
        sCartaoDest = new GUIStyle(sCartao);
        sCartaoDest.normal.background = Redondo(Tema.Destaque, Tema.Destaque);
        sCartaoDestTit = new GUIStyle(sCartaoTit);
        sCartaoDestTit.normal.textColor = Tema.DestaqueTexto;
        sCartaoDestTxt = new GUIStyle(sPeq);
        sCartaoDestTxt.normal.textColor = Tema.DestaqueTexto;
        sNumDest = new GUIStyle(sValor);
        sNumDest.normal.textColor = Tema.DestaqueTexto;
        sBloco = new GUIStyle(sCartao);
        sBloco.normal.background = Redondo(Color.Lerp(Tema.Card, texto, 0.05f), Color.Lerp(Tema.Card, texto, 0.05f));
        sBloco.padding = new RectOffset(12, 12, 8, 8);
        sBloco.margin = new RectOffset(3, 3, 3, 3);
        sLink = new GUIStyle(sRotulo);
        sLink.normal.textColor = Tema.Destaque;
        sLink.hover.textColor = Color.Lerp(Tema.Destaque, texto, 0.4f);
        sLink.fontStyle = FontStyle.Bold;
        sLink.wordWrap = false;
        sLink.stretchWidth = false;
        sLinkDest = new GUIStyle(sLink);
        sLinkDest.normal.textColor = Tema.DestaqueTexto;
        sLinkDest.hover.textColor = Tema.DestaqueTexto;

        // "x" de fechar (reserva sem PNG: botao vermelho pequeno)
        sFechar = new GUIStyle(sBtn);
        sFechar.hover.background = Redondo(Tema.Hex(0xef4444), Tema.Hex(0xef4444));
        sFechar.padding = new RectOffset(0, 0, 0, 0);

        UsarPngs();
    }

    // Troca as texturas geradas pelos PNGs do tema (mesmo visual, cantos mais suaves, com hover e clique).
    private static void UsarPngs()
    {
        sContorno = sBtn;
        sJanela = sLateral = sSombra = null;
        texLogo = texNome = texAlca = texSombra = texBranco = texBolinha = texIntOff = texIntOn = texFaixa = texEtiqueta = null;
        if (!Png.Existe) { LiveLog.Aviso("lg_ui_sem"); return; }
        Png.Fundos(sBtn, "sec", "sec_hover", "sec_press", 8);
        Png.Fundos(sSel, "pri", "pri_hover", "pri_press", 10);
        Png.Fundos(sAmbar, "amb", "amb_hover", "amb_press", 10);
        Png.Fundos(sPerigo, "perigo", "perigo_hover", "perigo_press", 10);
        Png.Fundos(sNav, null, "nav_hover", "nav_ativo", 10);
        Png.Fundos(sNavSel, "nav_ativo", "nav_ativo", "nav_ativo", 10);
        Png.Fundos(sNavTxt, null, "nav_hover", "nav_ativo", 10);
        Png.Fundos(sCampo, "campo", "campo_hover", "campo_foco", 10);
        sCampo.focused.background = sCampo.active.background;
        sCampo.onFocused.background = sCampo.active.background;
        sCampo.padding = new RectOffset(10, 10, 6, 6);
        sContorno = new GUIStyle(sBtn);
        Png.Fundos(sContorno, "btn", "btn_hover", "btn_press", 10);

        Texture2D t;
        if ((t = Png.Tex("nav_ativo")) != null) { sNavFundo.normal.background = t; sNavFundo.border = new RectOffset(10, 10, 10, 10); }
        if ((t = Png.Tex("card")) != null)
        {
            sCartao.normal.background = t; sCartao.border = new RectOffset(14, 14, 14, 14);
            sCartaoDest.border = sCartao.border;
            Texture2D d = Png.Tex("pri");
            if (d != null) { sCartaoDest.normal.background = d; sCartaoDest.border = new RectOffset(10, 10, 10, 10); }
        }
        if ((t = Png.Tex("sec")) != null) { sBloco.normal.background = t; sBloco.border = new RectOffset(8, 8, 8, 8); }
        if ((t = Png.Tex("check_off")) != null) { sCaixaDesl.normal.background = t; sCaixaDesl.focused.background = t; }
        if ((t = Png.Tex("check_on")) != null)
        {
            sCaixaLig.normal.background = t; sCaixaLig.focused.background = t;
            sCaixaLig.hover.background = t; sCaixaLig.active.background = t;
        }
        if ((t = Png.Tex("rolagem")) != null)
        {
            sBarra.normal.background = null;
            sBarra.fixedWidth = 8f;
            sBarraPolegar.normal.background = t;
            sBarraPolegar.hover.background = Png.Tex("rolagem_hover") ?? t;
            sBarraPolegar.active.background = sBarraPolegar.hover.background;
            sBarraPolegar.border = new RectOffset(4, 4, 4, 4);
            sBarraPolegar.fixedWidth = 8f;
        }
        Texture2D f = Png.Tex("fechar"), fh = Png.Tex("fechar_hover");
        if (f != null && fh != null)
        {
            sFechar = new GUIStyle();
            sFechar.normal.background = f;
            sFechar.hover.background = fh;
            sFechar.active.background = fh;
        }

        sJanela = Png.Estilo("janela", 16);
        sLateral = Png.Estilo("lateral", 16);
        sSombra = Png.Estilo("sombra", 32);
        texLogo = Png.Tex("logo");
        texNome = Png.Tex("marca");
        texAlca = Png.Tex("alca");
        texBranco = Png.Tex("branco");
        texBolinha = Png.Tex("bolinha");
        texIntOff = Png.Tex("interruptor_off");
        texIntOn = Png.Tex("interruptor_on");
        texFaixa = Png.Tex("faixa");
        texEtiqueta = Png.Tex("etiqueta");
    }

    // Durante o desenho da janela, os estilos padrao (usados pelos GUILayout.Button/TextField sem estilo)
    // viram os da proposta B; depois voltam, para nao mudar outros paineis.
    private static void TrocarSkin()
    {
        GUISkin k = GUI.skin;
        skBotao = k.button; skCampo = k.textField; skMarcar = k.toggle; skBarra = k.verticalScrollbar; skPolegar = k.verticalScrollbarThumb; skRotulo = k.label;
        k.button = sBtn; k.textField = sCampo; k.verticalScrollbar = sBarra; k.verticalScrollbarThumb = sBarraPolegar; k.label = sRotulo;
    }

    private static void VoltarSkin()
    {
        GUISkin k = GUI.skin;
        if (skBotao != null) { k.button = skBotao; k.textField = skCampo; k.toggle = skMarcar; k.verticalScrollbar = skBarra; k.verticalScrollbarThumb = skPolegar; k.label = skRotulo; }
    }

    // etiqueta de categoria: fundo escuro puxado para a cor e texto na cor (selecionada: fundo na cor, texto escuro)
    private static GUIStyle Chip(Color cor, bool selecionado)
    {
        GUIStyle[] par;
        if (!chips.TryGetValue(cor, out par))
        {
            // no tema claro: fundo clarinho e texto escurecido (as cores das categorias sao claras)
            Color fundo = Color.Lerp(Tema.Card, cor, Tema.Claro ? 0.28f : 0.22f);
            Color letra = Tema.Claro ? Color.Lerp(cor, Color.black, 0.55f) : cor;
            par = new GUIStyle[2];
            par[0] = Botao(fundo, Color.Lerp(fundo, cor, 0.15f), fundo, letra, false);
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
        Color marca = Tema.DestaqueTexto;
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
