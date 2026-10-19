@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
rem Copia o mod para a pasta "mods" do jogo (e a unica forma de o jogo achar um mod local).
rem Nao altera nenhum arquivo do jogo: so cria/atualiza mods\TraducaoLive. Para tirar, apague essa pasta.
set "JOGO=C:\Program Files (x86)\Steam\steamapps\common\Outer Space"
if not exist "saida\TraducaoLive\v_common\Assemblies\TraducaoLive.dll" (echo Rode o compilar.bat primeiro. & pause & exit /b 1)
if exist "%JOGO%\mods\TraducaoLive" rmdir /s /q "%JOGO%\mods\TraducaoLive"
xcopy /e /i /y /q "saida\TraducaoLive" "%JOGO%\mods\TraducaoLive\" >nul
if errorlevel 1 (echo ERRO ao copiar. Tente rodar como administrador. & pause & exit /b 1)
echo Copiado para %JOGO%\mods\TraducaoLive
echo Abra o jogo, va em Mods, ative "Universal Translator" (e DESATIVE os outros mods de traducao) e reinicie o jogo.
pause
