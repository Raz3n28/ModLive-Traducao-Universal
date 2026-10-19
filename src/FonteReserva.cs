using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

// A fonte do jogo e chinesa: nao tem acentos (a, c, e...) nem outros alfabetos. Aqui criamos fontes reserva
// dinamicas (fontes do Windows, so em memoria) e colocamos em PRIMEIRO lugar na lista de reservas de cada fonte do jogo.
// Assim qualquer letra que faltar e desenhada pela fonte do Windows com o mesmo tamanho. Nada e gravado no disco.
internal static class FonteReserva
{
    private const string Acentos = "ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿŁłŃńŚśŹźŻżĄąĘęĆćŠšŽžČčĚěŘřŮůŇňŤťĎďĞğİıŞşŐőŰű“”‘’—–…º ª°";
    // latino/cirilico/grego -> segoeui ; coreano -> malgun ; japones -> meiryo ; chines -> msyh
    private static readonly string[] Preferidas = { "segoeui.ttf", "malgun.ttf", "meiryo.ttc", "msyh.ttc", "arial.ttf" };

    private static readonly HashSet<int> vistas = new HashSet<int>();
    private static readonly List<TMP_FontAsset> reservas = new List<TMP_FontAsset>();
    private static bool reservaTentada;
    public static int Corrigidas;

    public static void Verificar()
    {
        try
        {
            UnityEngine.Object[] fontes = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
            for (int i = 0; i < fontes.Length; i++)
            {
                TMP_FontAsset f = fontes[i] as TMP_FontAsset;
                if (!f || reservas.Contains(f)) continue;
                if (!vistas.Add(f.GetInstanceID())) continue;
                Corrigir(f);
            }
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_fontes_reserva", e);
        }
    }

    // Chamado pela varredura com a fonte de cada texto: fonte que ainda nao foi vista e ligada na hora,
    // sem esperar a procura completa (que agora roda so ao carregar cena e a cada 30 s).
    public static void VerificarFonte(TMP_FontAsset f)
    {
        try
        {
            if (!f || reservas.Contains(f)) return;
            if (!vistas.Add(f.GetInstanceID())) return;
            Corrigir(f);
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_fontes_reserva", e);
        }
    }

    private static void Corrigir(TMP_FontAsset f)
    {
        try
        {
            if (!reservaTentada) CriarReservas();
            if (reservas.Count == 0) return;
            if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new List<TMP_FontAsset>();
            for (int i = reservas.Count - 1; i >= 0; i--)
            {
                if (!f.fallbackFontAssetTable.Contains(reservas[i]))
                    f.fallbackFontAssetTable.Insert(0, reservas[i]);
            }
            Corrigidas++;
            LiveLog.Info("lg_fonte_ligada", f.name);
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_corrigir_fonte", e, f ? f.name : "?");
        }
    }

    private static void CriarReservas()
    {
        reservaTentada = true;
        try
        {
            string[] caminhos = Font.GetPathsToOSFonts();
            for (int p = 0; p < Preferidas.Length; p++)
            {
                string escolhido = null;
                for (int i = 0; i < caminhos.Length; i++)
                {
                    if (string.Equals(Path.GetFileName(caminhos[i]), Preferidas[p], StringComparison.OrdinalIgnoreCase))
                    {
                        escolhido = caminhos[i];
                        break;
                    }
                }
                if (escolhido == null) continue;
                try
                {
                    Font fonte = new Font(escolhido);
                    TMP_FontAsset a = TMP_FontAsset.CreateFontAsset(fonte);
                    if (a == null) continue;
                    a.name = "LIVE_Reserva_" + Preferidas[p];
                    if (p == 0) a.TryAddCharacters(Acentos);
                    reservas.Add(a);
                    LiveLog.Info("lg_fonte_criada", escolhido);
                }
                catch (Exception e)
                {
                    LiveLog.Erro("le_fonte", e, escolhido);
                }
            }
            if (reservas.Count == 0) LiveLog.Info("lg_fonte_nenhuma");
        }
        catch (Exception e)
        {
            LiveLog.Erro("le_criar_fontes", e);
        }
    }
}
