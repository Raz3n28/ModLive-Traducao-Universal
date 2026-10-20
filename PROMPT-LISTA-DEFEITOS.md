Continuação do meu mod "Universal Translator" (tradução ao vivo) do jogo Outer Space (Unity, Steam app 3080070).

REPOSITÓRIO E BRANCH
- Repo: https://github.com/Raz3n28/ModLive-Traducao-Universal
- Trabalhe SEMPRE na branch `claude/laughing-faraday-lmgyz3`. Ela fica em cima da `melhorias-revisao`, que é a minha versão mais nova. A `main` é antiga, não use.
- PR aberto (rascunho): https://github.com/Raz3n28/ModLive-Traducao-Universal/pull/2 (base: melhorias-revisao)
- ZIP para eu testar: https://github.com/Raz3n28/ModLive-Traducao-Universal/archive/refs/heads/claude/laughing-faraday-lmgyz3.zip

MODO LISTA DE DEFEITOS (o mais importante deste chat)
Vou mandar defeitos um por um (texto, prints, erros do compilar.bat ou do log do jogo). Enquanto a lista estiver aberta:
- NÃO mexa no código, NÃO faça commit de código e NÃO comece a consertar nada.
- Para cada defeito: anote no PROXIMAS-MELHORIAS.txt como item [ ] (com o que vi, onde acontece e, se já souber, a provável causa e o arquivo), faça commit e push só desse arquivo.
- Responda curto: o número do defeito, um resumo de uma linha do que entendeu e, se faltar algo para entender, UMA pergunta. Pode olhar o código para descobrir a causa, mas sem alterar.
- Se dois defeitos forem o mesmo problema, junte num item só e me avise.
A lista só fecha quando eu disser "fim da lista", "pode arrumar" ou "já arruma". Aí:
1. Me mostre a lista numerada (um resumo por linha) e a ordem em que vai consertar.
2. Conserte todos os itens [ ], marcando [x] no PROXIMAS-MELHORIAS.txt o que ficou pronto.
3. Compile (checagem), faça commit e push e me mande o link do ZIP com o que mudou em cada item e o que eu devo testar no jogo.
Se eu disser "já arruma o número N", conserte só aquele e a lista continua aberta para o resto.

COMO EU TESTO
- Eu compilo no meu PC com `compilar.bat` (csc do Windows, C# 5: sem $"", ?., =>) e copio para o jogo com `Testar no jogo.bat`.
- A DLL de IMGUI do jogo é "cortada": faltam funções como GUI.TextField, GUI.Button(Rect,string,GUIStyle), GUI.Toggle(Rect,bool,string,GUIStyle), o get de GUIContent.image e talvez GUILayoutUtility.GetLastRect. Use só o que o mod já usa (GUI.Button(Rect,GUIContent,GUIStyle), GUILayout.TextField(string, opções), GUILayoutUtility.GetRect, GUI.Box(Rect,GUIContent,GUIStyle), GUI.DrawTexture). Evite Mathf.Exp/MoveTowards/Sin, Time.unscaledDeltaTime, Queue e ColorUtility.
- Para checar a sintaxe na nuvem: mcs -langversion:5 (apt install mono-mcs) com as DLLs do NuGet "UnityEngine.Modules" 2021.3.33 (baixar de api.nuget.org) e stubs (versões de mentira) para TMPro, UnityEngine.UI, Steamworks e as classes do jogo (BaseModLauncher, ModXmlManifest, ChannelSteam, UGCManager).
- IMGUI: nunca chame um controle (GUI.Box, GUI.Button…) só no Repaint. Isso muda o número dos controles seguintes, e o cursor de texto aparece no campo errado (já aconteceu).

O QUE JÁ FOI FEITO NESSA BRANCH
- src/Imgui.cs: traduz janelas IMGUI de outros mods com o Harmony por reflexão (0Harmony.dll em modelo/v_common/Assemblies). Tem LiveImgui.Prefixar para outras partes usarem o Harmony.
- Menu F8 (src/Menu.cs) em PNG com 6 temas (modelo/v_common/UI, gerados por ferramentas/gerar_ui.py); src/Tema.cs e src/Png.cs.
- Aba Painel, teclas configuráveis (F8 menu, F7 pausar), botão do canto arrastável, QR do Ko-fi na lateral.
- Aba Interface (src/EscalaUI.cs): escala da interface (CanvasScaler) e tamanho das fontes de 50% a 200%. O "pisca" foi tratado com Harmony nos set do CanvasScaler + conferência em Canvas.willRenderCanvases (ainda confirmar no jogo).
- Imagem da Workshop (modelo/Manifest/preview.png) com cartões UI SCALE e FONT SIZE (ferramentas/gerar_preview.py).
- Publicar.bat: na atualização manda os arquivos e a imagem de capa; título e descrição NÃO são enviados. NÃO mexer na descrição da Workshop.

COMO EU GOSTO DE TRABALHAR
- Respostas em português, simples (estou aprendendo).
- Cache, glossário e traduções do usuário nunca podem ser resetados.
- Nada vai para a Steam sem eu mandar: eu mesmo publico.

AGORA
Leia PROXIMAS-MELHORIAS.txt e LEIA-ME.txt da branch, diga "Lista aberta" e aguarde o primeiro defeito.
