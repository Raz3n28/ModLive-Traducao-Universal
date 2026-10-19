@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
set "JOGO=C:\Program Files (x86)\Steam\steamapps\common\Outer Space"
set "DADOS=%USERPROFILE%\AppData\LocalLow\CatStudio\Outer Space"
set "RES=%DADOS%\live_resultado_publicacao.txt"
set "STA=%DADOS%\live_status_publicacao.txt"
set "MODDAT=%DADOS%\mod.dat"

if not exist "%JOGO%\mods\TraducaoLive\v_common\Assemblies\TraducaoLive.dll" (
  echo Rode o compilar.bat e o "Testar no jogo.bat" primeiro.
  pause & exit /b 1
)
tasklist /FI "IMAGENAME eq Outer Space.exe" | find /I "Outer Space.exe" >nul
if not errorlevel 1 (echo Feche o jogo antes de publicar. & pause & exit /b 1)

rem O envio so acontece se o Universal Translator estiver ATIVADO no menu Mods do jogo
if exist "%MODDAT%" (
  findstr /C:"pauloraz3n.outerspace.live" "%MODDAT%" >nul
  if errorlevel 1 (
    echo O "Universal Translator" NAO esta ativado no menu Mods do jogo.
    echo Abra o jogo, va em Mods, ative o Universal Translator, feche o jogo e rode este arquivo de novo.
    pause & exit /b 1
  )
)

if exist "%RES%" del "%RES%"
if exist "%STA%" del "%STA%"
echo Abrindo o jogo pela Steam em modo de publicacao...
echo (o status tambem aparece num painel no canto da tela do jogo)
echo.
start "" "steam://run/3080070//-livepublicar/"

echo Aguardando o envio (ate 30 minutos)...
set "ULT="
set /a n=0
:espera
timeout /t 5 /nobreak >nul
if exist "%RES%" goto pronto
if exist "%STA%" (
  set "ATU="
  set /p ATU=<"%STA%"
  call :mostrar
)
set /a n+=1
if %n% lss 360 goto espera
echo.
echo Nao recebi resposta. O Universal Translator esta ATIVADO no menu Mods? Veja o Player.log procurando [LIVE].
pause & exit /b 1

:mostrar
if "%ATU%"=="%ULT%" exit /b 0
set "ULT=%ATU%"
echo   %time:~0,8%  %ATU%
exit /b 0

:pronto
echo.
type "%RES%"
echo.
echo Pode fechar o jogo.
pause
