using System.Text;

namespace MauiAppBrownianMotion.Behaviors;

/// <summary>
/// Behavior para restringir o Entry a aceitar apenas dígitos e um único separador decimal da cultura atual.
/// Permite opcionalmente sinal negativo (configurável por AllowNegative).
/// </summary>
public class NumericEntryBehavior : Behavior<Entry>
{
    public static readonly BindableProperty AllowNegativeProperty = BindableProperty.Create(
        nameof(AllowNegative), typeof(bool), typeof(NumericEntryBehavior), false);

    /// <summary>
    /// Quando true permite um único sinal '-' apenas na primeira posição.
    /// </summary>
    public bool AllowNegative
    {
        get => (bool)GetValue(AllowNegativeProperty);
        set => SetValue(AllowNegativeProperty, value);
    }

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
        bool hasSign = false;
        int pos = 0;

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
            else if (AllowNegative && ch == '-' && pos == 0 && !hasSign)
            {
                sb.Append('-');
                hasSign = true;
            }
            // ignora qualquer outro caractere
            pos++;
        }

        var sanitized = sb.ToString();
        if (sanitized != e.NewTextValue)
        {
            entry.Text = sanitized; // atualiza somente se houve alteração
        }
    }
}
