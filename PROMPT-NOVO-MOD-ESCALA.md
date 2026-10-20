Quero criar um mod NOVO para o jogo Outer Space (Unity, Steam app 3080070), separado do meu mod de tradução, usando a mesma interface (menu em PNG com temas) que já fiz no "Universal Translator".

O NOVO MOD
- Nome sugerido: "UI Scale" (pode sugerir outro). Nome da pasta/DLL: EscalaUI (pasta saida\EscalaUI, DLL EscalaUI.dll).
- Só duas funções:
  1. Escala da interface do jogo (CanvasScaler), de 50% a 200%, geral e por parte da tela (por Canvas).
  2. Tamanho das fontes do jogo (TMP_Text e UI.Text), de 50% a 200%.
- Tudo muda na hora e fica salvo no config.txt do mod. Botão "Voltar ao padrão" e uma tecla para voltar ao padrão (sem tecla por padrão).
- Menu com tecla configurável (sugestão: F9, para não brigar com o F8 do tradutor) e botão no canto da tela arrastável.
- Menu em 6 idiomas (Português, Inglês, Espanhol, Russo, Francês, Alemão), igual ao tradutor.

DE ONDE COPIAR (repo do tradutor)
- Repo: https://github.com/Raz3n28/ModLive-Traducao-Universal, branch `claude/laughing-faraday-lmgyz3` (só para LER e copiar; não mexa nele).
- src/EscalaUI.cs: toda a lógica de escala e fonte. Já tem o conserto do "pisca": Harmony nos set do CanvasScaler (scaleFactor e referenceResolution) e a conferência a cada quadro em Canvas.willRenderCanvases.
- src/Imgui.cs: só a parte de achar e carregar o Harmony (AcharHarmony e Prefixar). A tradução de IMGUI NÃO vai para o mod novo.
- src/Menu.cs: copiar o visual (janela arrastável e redimensionável, sombra, barra de título, lateral com logo PR e "By PauloRaz3n", abas com ícone, cartões, botões, interruptor, QR do Ko-fi) e a aba Interface. Tirar todas as abas do tradutor.
- src/Tema.cs e src/Png.cs: inteiros (temas e imagens).
- src/Config.cs, src/LiveTxt.cs, src/Launcher.cs, src/LiveDriver.cs: só o que o mod novo precisa (config, textos do menu, log, iniciar, Update).
- modelo/v_common/UI (os 6 temas + comum) e ferramentas/gerar_ui.py (os PNGs; precisa das fontes Space Grotesk e Orbitron do Google Fonts). No gerar_ui.py, trocar o nome do mod no logo e deixar só os ícones que o mod novo usa.
- modelo/v_common/Assemblies/0Harmony.dll (licença MIT).
- compilar.bat, Testar no jogo.bat e Publicar.bat: adaptar para o nome novo (pasta mods\EscalaUI).
- modelo/Manifest: novo nome, nova descrição, sem o ID da Workshop do tradutor (vai ser um item novo).

CUIDADOS
- Renomear as classes internas (não usar os prefixos Live*) e usar outro ID no Harmony (ex.: "pauloraz3n.outerspace.escalaui").
- Pasta de dados do usuário própria: %USERPROFILE%\AppData\LocalLow\CatStudio\Outer Space\EscalaUI.
- Se o "Universal Translator" estiver ligado junto, os dois vão mexer na escala ao mesmo tempo (escala dobrada). O mod novo deve detectar isso (procurar a DLL TraducaoLive entre os assemblies carregados) e mostrar um aviso no menu e no log. Depois eu vejo se tiro a aba Interface do tradutor.
- O jogo procura a classe ModPluginLauncher (sem namespace, herdando BaseModLauncher) na DLL principal do mod.

COMO EU TESTO
- Eu compilo no meu PC com `compilar.bat` (csc do Windows, C# 5: sem $"", ?., =>) e copio para o jogo com `Testar no jogo.bat`.
- A DLL de IMGUI do jogo é "cortada": faltam funções como GUI.TextField, GUI.Button(Rect,string,GUIStyle), GUI.Toggle(Rect,bool,string,GUIStyle), o get de GUIContent.image e talvez GUILayoutUtility.GetLastRect. Use só o que o tradutor já usa (GUI.Button(Rect,GUIContent,GUIStyle), GUILayout.TextField(string, opções), GUILayoutUtility.GetRect, GUI.Box(Rect,GUIContent,GUIStyle), GUI.DrawTexture). Evite Mathf.Exp/MoveTowards/Sin, Time.unscaledDeltaTime, Queue e ColorUtility.
- Para checar a sintaxe na nuvem: mcs -langversion:5 (apt install mono-mcs) com as DLLs do NuGet "UnityEngine.Modules" 2021.3.33 (baixar de api.nuget.org) e stubs (versões de mentira) para TMPro, UnityEngine.UI e as classes do jogo (BaseModLauncher, ModXmlManifest).
- IMGUI: nunca chame um controle (GUI.Box, GUI.Button…) só no Repaint. Isso muda o número dos controles seguintes, e o cursor de texto aparece no campo errado (já aconteceu).

REPOSITÓRIO DO MOD NOVO
- Repo: https://github.com/Raz3n28/<NOME-DO-REPO-NOVO> (eu crio vazio antes, com um README).
- Trabalhe numa branch nova e abra um PR em rascunho. Mande o link do ZIP da branch para eu testar.

COMO EU GOSTO DE TRABALHAR
- Respostas em português, simples (estou aprendendo).
- Depois de cada mudança: compilar (checagem), commit, push na branch e me mandar o link do ZIP.
- Crie um LEIA-ME.txt e um PROXIMAS-MELHORIAS.txt iguais aos do tradutor. Quando eu disser "inclui na lista", anote lá sem mexer no código. Quando eu disser "pode fazer a lista", faça os itens [ ].

AGORA
Leia os arquivos do tradutor que eu citei, me diga em poucas linhas o plano (quais arquivos o mod novo vai ter) e já monte a primeira versão.
