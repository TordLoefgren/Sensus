using System.Windows;
using System.Windows.Controls;

namespace Sensus.Views.Controls;

public partial class PropertyRow : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(PropertyRow),
        new(string.Empty)
    );

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(PropertyRow),
        new(string.Empty)
    );

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public PropertyRow()
    {
        InitializeComponent();
    }
}
