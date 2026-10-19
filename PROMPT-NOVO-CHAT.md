Continuação do meu mod "Universal Translator" (tradução ao vivo) do jogo Outer Space (Unity, Steam app 3080070).

REPOSITÓRIO E BRANCH
- Repo: https://github.com/Raz3n28/ModLive-Traducao-Universal
- Trabalhe SEMPRE na branch `claude/laughing-faraday-lmgyz3`. Ela fica em cima da `melhorias-revisao`, que é a minha versão mais nova. A `main` é antiga, não use.
- PR aberto (rascunho): https://github.com/Raz3n28/ModLive-Traducao-Universal/pull/2 (base: melhorias-revisao)
- ZIP para eu testar: https://github.com/Raz3n28/ModLive-Traducao-Universal/archive/refs/heads/claude/laughing-faraday-lmgyz3.zip

COMO EU TESTO
- Eu compilo no meu PC com `compilar.bat` (csc do Windows, C# 5: sem $"", ?., =>) e copio para o jogo com `Testar no jogo.bat`.
- A DLL de IMGUI do jogo é "cortada": faltam funções como GUI.TextField, GUI.Button(Rect,string,GUIStyle), GUI.Toggle(Rect,bool,string,GUIStyle), o get de GUIContent.image e talvez GUILayoutUtility.GetLastRect. Use só o que o mod já usa (GUI.Button(Rect,GUIContent,GUIStyle), GUILayout.TextField(string, opções), GUILayoutUtility.GetRect, GUI.Box(Rect,GUIContent,GUIStyle), GUI.DrawTexture). Evite Mathf.Exp/MoveTowards/Sin, Time.unscaledDeltaTime, Queue e ColorUtility.
- Para checar a sintaxe na nuvem: mcs -langversion:5 com as DLLs do NuGet "UnityEngine.Modules" 2021.3.33 e stubs (versões de mentira) para TMPro, UnityEngine.UI, Steamworks e as classes do jogo (BaseModLauncher, ModXmlManifest).
- IMGUI: nunca chame um controle (GUI.Box, GUI.Button…) só no Repaint. Isso muda o número dos controles seguintes, e o cursor de texto aparece no campo errado (já aconteceu).

O QUE JÁ FOI FEITO NESSA BRANCH
- src/Imgui.cs: traduz janelas IMGUI de outros mods (ex.: editor de personagens, Workshop 3822868988) com o Harmony por reflexão. O 0Harmony.dll vai em modelo/v_common/Assemblies.
- Menu F8 (src/Menu.cs) em PNG com 6 temas (Teal, Brasil, EUA, Roxo Neon, Pôr do Sol, Claro): modelo/v_common/UI/<tema>/*.png + tema.txt, com ícones, sombra e QR do Ko-fi em UI/comum. Tudo é gerado por ferramentas/gerar_ui.py (precisa das fontes Space Grotesk e Orbitron do Google Fonts). src/Tema.cs lê as cores e src/Png.cs carrega as imagens.
- Logo PR (hexágono) e "By PauloRaz3n". Janela arrastável e redimensionável. Botão "Tradução" do canto arrastável.
- Aba Painel (a primeira): estado, 6 cartões, traduzidos hoje (contador diário), pedidos por minuto, últimas traduções.
- Teclas configuráveis: menu F8, pausar/continuar F7, voltar a interface ao padrão (sem tecla por padrão).
- Aba Fontes segue o tema: as prévias são só o nome em branco transparente (ferramentas/gerar_previas_fontes.py).
- Aba Interface (src/EscalaUI.cs): escala da interface do jogo (CanvasScaler) e tamanho das fontes (TMP_Text/UI.Text) de 50% a 200%, geral e por Canvas.
- QR code do Ko-fi na lateral do menu (só a imagem, sem link escrito). NÃO mexer na descrição da Workshop.
- Lista de ideias e consertos: PROXIMAS-MELHORIAS.txt (tudo marcado [x] por enquanto). Quando eu disser "inclui na lista", anote lá sem mexer no código. Quando eu disser "pode fazer a lista", faça os itens [ ].

COMO EU GOSTO DE TRABALHAR
- Respostas em português, simples (estou aprendendo).
- Cache, glossário e traduções do usuário nunca podem ser resetados.
- Depois de cada mudança: compilar (checagem), commit, push na branch e me mandar o link do ZIP.

AGORA
Estou testando o mod no jogo. Vou te mandar prints e erros do compilar.bat ou do jogo. Comece lendo PROXIMAS-MELHORIAS.txt e o LEIA-ME.txt da branch e aguarde o que eu mandar.
