@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

rem Pasta do jogo (so LEITURA: as DLLs sao usadas como referencia para compilar)
set "JOGO=C:\Program Files (x86)\Steam\steamapps\common\Outer Space"
set "MANAGED=%JOGO%\Outer Space_Data\Managed"
set "SAIDA=saida\TraducaoLive"

echo ==============================================
echo   Universal Translator - compilar e montar o mod
echo ==============================================
echo.

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo ERRO: csc.exe do Windows nao encontrado. & pause & exit /b 1)
if not exist "%MANAGED%\Assembly-CSharp.dll" (echo ERRO: jogo nao encontrado em "%JOGO%". Corrija a linha JOGO= no compilar.bat. & pause & exit /b 1)

if exist "%SAIDA%" rmdir /s /q "%SAIDA%"
mkdir "%SAIDA%\v_common\Assemblies"

set "RSP=saida\compilar.rsp"
> "%RSP%" echo /nologo /nostdlib+ /target:library /optimize+ /codepage:65001 /nowarn:1701,1702
>> "%RSP%" echo "/out:%SAIDA%\v_common\Assemblies\TraducaoLive.dll"
for %%f in ("%MANAGED%\*.dll") do >> "%RSP%" echo "/reference:%%~ff"
>> "%RSP%" echo "src\*.cs"

echo [1/2] Compilando a DLL do mod...
"%CSC%" /noconfig @"%RSP%"
if not exist "%SAIDA%\v_common\Assemblies\TraducaoLive.dll" (
  echo.
  echo ERRO na compilacao. Copie as mensagens acima e mande pro Claude.
  pause & exit /b 1
)

echo [2/2] Montando a pasta do mod...
xcopy /e /i /y /q "modelo\*" "%SAIDA%\" >nul

echo.
echo Mod pronto em: %CD%\%SAIDA%
echo Para testar: rode "Testar no jogo.bat".
pause
