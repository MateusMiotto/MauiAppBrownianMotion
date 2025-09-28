using System.Globalization;

namespace MauiAppBrownianMotion.Utilities;

/// <summary>
/// Utilitários de formatação numérica/monetária compacta (K, M, B, T) com fallback de precisão para valores pequenos.
/// Centraliza lógica reutilizada em tooltips, médias, e eixos.
/// </summary>
public static class NumberFormatUtils
{
    /// <summary>
    /// Formata valor monetário em reais com sufixos K/M/B/T e precisão adaptativa.
    /// Mantém notação científica apenas para valores extremamente pequenos (0.001).
    /// </summary>
    public static string FormatPriceCompact(double value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        double abs = Math.Abs(value);
        if (abs >= 1_000_000_000_000d) return $"R$ {value / 1_000_000_000_000d:0.##}T";
        if (abs >= 1_000_000_000d)    return $"R$ {value / 1_000_000_000d:0.##}B";
        if (abs >= 1_000_000d)        return $"R$ {value / 1_000_000d:0.##}M";
        if (abs >= 1_000d)            return $"R$ {value / 1_000d:0.##}K";
        if (abs >= 1d)                return $"R$ {value.ToString("N3", culture)}"; // milhares separador local
        if (abs >= 0.001d)            return $"R$ {value.ToString("0.####", culture)}"; // até 4 casas para pequenos
        return $"R$ {value:E2}"; // notação científica para valores muito pequenos
    }

    /// <summary>
    /// Abrevia número genérico com K/M/B/T.
    /// </summary>
    public static string Abbreviate(double value)
    {
        double abs = Math.Abs(value);
        if (abs >= 1_000_000_000_000d) return (value / 1_000_000_000_000d).ToString("0.##") + "T";
        if (abs >= 1_000_000_000d)    return (value / 1_000_000_000d).ToString("0.##") + "B";
        if (abs >= 1_000_000d)        return (value / 1_000_000d).ToString("0.##") + "M";
        if (abs >= 1_000d)            return (value / 1_000d).ToString("0.##") + "K";
        return value.ToString("0.##");
    }

    /// <summary>
    /// Formatação adaptativa de eixo Y baseada em range e magnitude máxima.
    /// Replica lógica anterior de GbmDrawable.FormatY para reutilização.
    /// </summary>
    public static string FormatAxisY(double value, double min, double max)
    {
        double range = Math.Abs(max - min);
        double absMax = Math.Max(Math.Abs(min), Math.Abs(max));
        if (absMax >= 1_000) return Abbreviate(value);
        if (range < 1e-6) return value.ToString("0.####");
        if (range < 0.01) return value.ToString("0.####");
        if (range < 0.1) return value.ToString("0.###");
        if (range < 1) return value.ToString("0.##");
        if (range < 10) return value.ToString("0.##");
        if (range < 100) return value.ToString("0.#");
        return value.ToString("0");
    }
}
