using System.Text;

namespace MauiAppBrownianMotion.Behaviors;

/// <summary>
/// Behavior para restringir o Entry a aceitar apenas dígitos e um único separador decimal da cultura atual.
/// Não permite sinal negativo (requisito: apenas números).
/// </summary>
public class NumericEntryBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry entry)
    {
        entry.TextChanged += OnTextChanged;
        base.OnAttachedTo(entry);
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.TextChanged -= OnTextChanged;
        base.OnDetachingFrom(entry);
    }

    void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry) return;
        if (string.IsNullOrEmpty(e.NewTextValue)) return; // permite limpar

        var decSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var sb = new StringBuilder(e.NewTextValue.Length);
        bool hasSep = false;

        foreach (var ch in e.NewTextValue)
        {
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
            }
            else if ((ch == '.' || ch == ',') && !hasSep)
            {
                sb.Append(decSep);
                hasSep = true;
            }
            // ignora qualquer outro caractere
        }

        var sanitized = sb.ToString();
        if (sanitized != e.NewTextValue)
        {
            // reposiciona texto somente se mudou para evitar loops
            entry.Text = sanitized;
        }
    }
}
