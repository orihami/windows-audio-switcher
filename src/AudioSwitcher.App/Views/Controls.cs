using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AudioSwitcher.App.Views;

/// <summary>設定画面の部品(Windows 11 の設定アプリのような行・トグル・ボタン)。</summary>
public static class Ui
{
    public static TextBlock Text(string s, double size, Brush fg, FontWeight? weight = null, Thickness? margin = null) => new()
    {
        Text = s, FontSize = size, Foreground = fg, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? new Thickness(0), VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Icon(string glyph, double size, Brush fg) => new()
    {
        Text = glyph, FontFamily = Palette.Icons, FontSize = size, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
    };

    /// <summary>見出しと説明と、右側の操作部品が1つ入った行。</summary>
    public static Border Row(Palette p, string glyph, string title, string? desc, UIElement? control)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = Icon(glyph, 18, p.Text);
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        t.Children.Add(Text(title, 14, p.Text));
        if (!string.IsNullOrEmpty(desc)) t.Children.Add(Text(desc, 12, p.SubText, margin: new Thickness(0, 2, 0, 0)));
        Grid.SetColumn(t, 1);
        g.Children.Add(icon);
        g.Children.Add(t);
        if (control != null)
        {
            Grid.SetColumn(control, 2);
            g.Children.Add(control);
        }
        return Card(p, g);
    }

    public static Border Card(Palette p, UIElement child, Thickness? padding = null) => new()
    {
        Child = child, Background = p.Card, BorderBrush = p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
        Padding = padding ?? new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 4), MinHeight = 64,
    };

    public static TextBlock Header(Palette p, string s) => Text(s, 14, p.Text, FontWeights.SemiBold, new Thickness(2, 18, 0, 8));

    /// <summary>オン/オフのスイッチ。</summary>
    public static ToggleButton Toggle(Palette p, bool on, Action<bool> changed)
    {
        var b = new ToggleButton { IsChecked = on, Width = 64, Height = 28, Cursor = Cursors.Hand, Focusable = true };
        b.Template = ToggleTemplate(p);
        b.Checked += (_, _) => changed(true);
        b.Unchecked += (_, _) => changed(false);
        return b;
    }

    static ControlTemplate ToggleTemplate(Palette p)
    {
        // 「オン/オフ」の文字 + 丸いスイッチ
        var t = new ControlTemplate(typeof(ToggleButton));
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.Name = "label";
        label.SetValue(TextBlock.TextProperty, "オフ");
        label.SetValue(TextBlock.ForegroundProperty, p.Text);
        label.SetValue(TextBlock.FontSizeProperty, 13.0);
        label.SetValue(FrameworkElement.WidthProperty, 26.0);
        label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var track = new FrameworkElementFactory(typeof(Border));
        track.Name = "track";
        track.SetValue(FrameworkElement.WidthProperty, 38.0);
        track.SetValue(FrameworkElement.HeightProperty, 20.0);
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        track.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        track.SetValue(Border.BorderBrushProperty, p.SubText);
        track.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var knob = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        knob.Name = "knob";
        knob.SetValue(FrameworkElement.WidthProperty, 12.0);
        knob.SetValue(FrameworkElement.HeightProperty, 12.0);
        knob.SetValue(System.Windows.Shapes.Shape.FillProperty, p.SubText);
        knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        knob.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
        track.AppendChild(knob);
        root.AppendChild(label);
        root.AppendChild(track);
        t.VisualTree = root;
        var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(TextBlock.TextProperty, "オン", "label"));
        on.Setters.Add(new Setter(Border.BackgroundProperty, p.AccentBrush, "track"));
        on.Setters.Add(new Setter(Border.BorderBrushProperty, p.AccentBrush, "track"));
        on.Setters.Add(new Setter(System.Windows.Shapes.Shape.FillProperty, p.OnAccent, "knob"));
        on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "knob"));
        on.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0), "knob"));
        t.Triggers.Add(on);
        return t;
    }

    /// <summary>ふつうのボタン(accent=true で色つき)。</summary>
    public static Button Button(Palette p, string text, Action click, string? glyph = null, bool accent = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph != null) content.Children.Add(new TextBlock { Text = glyph, FontFamily = Palette.Icons, FontSize = 13, Margin = new Thickness(0, 0, text == "" ? 0 : 6, 0), VerticalAlignment = VerticalAlignment.Center });
        if (text != "") content.Children.Add(new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var b = new Button { Content = content, Cursor = Cursors.Hand, Padding = new Thickness(12, 5, 12, 6), MinHeight = 32, MinWidth = 32 };
        var bg = accent ? p.AccentBrush : p.CardHover;
        var fg = accent ? p.OnAccent : p.Text;
        var t = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "bd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BackgroundProperty, bg);
        border.SetValue(Border.BorderBrushProperty, p.Border);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);
        t.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85, "bd"));
        t.Triggers.Add(hover);
        var dis = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        dis.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "bd"));
        t.Triggers.Add(dis);
        b.Template = t;
        b.Foreground = fg;
        b.Click += (_, _) => click();
        return b;
    }

    public static ComboBox Combo(Palette p, IEnumerable<string> items, int selected, Action<int> changed, double width = 260)
    {
        var c = new ComboBox { Width = width, FontSize = 13, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var s in items) c.Items.Add(s);
        c.SelectedIndex = selected;
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) changed(c.SelectedIndex); };
        return c;
    }
}
