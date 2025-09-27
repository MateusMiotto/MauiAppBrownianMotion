namespace MauiAppBrownianMotion.Behaviors;

/// <summary>
/// Restringe o Entry a conter apenas dígitos inteiros e aplica um limite máximo (padrão 9999).
/// Pode ser usado em conjunto com outros behaviors (como NumericEntryBehavior).
/// </summary>
public class IntegerMaxBehavior : Behavior<Entry>
{
    public static readonly BindableProperty MaxValueProperty = BindableProperty.Create(
        nameof(MaxValue), typeof(int), typeof(IntegerMaxBehavior), 9999);

    public int MaxValue
    {
        get => (int)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
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
        var txt = e.NewTextValue;
        if (string.IsNullOrEmpty(txt)) return; // permite limpar

        // Mantém apenas dígitos
        var digits = new string(txt.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) { entry.Text = string.Empty; return; }

        if (int.TryParse(digits, out int value))
        {
            if (value > MaxValue) value = MaxValue;
            var finalTxt = value.ToString();
            if (entry.Text != finalTxt)
                entry.Text = finalTxt; // evita loop se igual
        }
        else
        {
            entry.Text = MaxValue.ToString();
        }
    }
}
