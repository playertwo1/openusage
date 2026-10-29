using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Path = System.Windows.Shapes.Path;

namespace OpenUsage.Windows;

public static partial class PresentationTheme
{
    public static FrameworkElement Icon(string name, double size = 18)
    {
        var (data, filled) = name.ToLowerInvariant() switch
        {
            "add" => ("M12,5 L12,19 M5,12 L19,12", false),
            "refresh" => ("M20,10 A8,8 0 1 0 19,17 M20,4 L20,10 L14,10", false),
            "settings" => ("M4,7 L20,7 M4,17 L20,17 M8,4 L8,10 M16,14 L16,20", false),
            "accounts" => ("M16,7 A4,4 0 1 1 8,7 A4,4 0 1 1 16,7 M4,21 L4,19 C4,13 20,13 20,19 L20,21", false),
            "dashboard" or "cards" => ("M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z", false),
            "list" => ("M4,5 L20,5 M4,12 L20,12 M4,19 L20,19", false),
            "star" => ("M12,3 L14.7,8.5 L21,9.4 L16.5,13.8 L17.6,20 L12,17.1 L6.4,20 L7.5,13.8 L3,9.4 L9.3,8.5 Z", false),
            "starfilled" => ("M12,3 L14.7,8.5 L21,9.4 L16.5,13.8 L17.6,20 L12,17.1 L6.4,20 L7.5,13.8 L3,9.4 L9.3,8.5 Z", true),
            "check" => ("M5,12 L10,17 L20,7", false),
            "chevron" or "down" => ("M6,9 L12,15 L18,9", false),
            "up" => ("M6,15 L12,9 L18,15", false),
            "close" => ("M6,6 L18,18 M18,6 L6,18", false),
            "minus" => ("M5,12 L19,12", false),
            "arrowleft" => ("M14,5 L7,12 L14,19 M7,12 L21,12", false),
            "eye" => ("M2,12 C7,3 17,3 22,12 C17,21 7,21 2,12 Z M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12", false),
            "more" => ("M5,12 L5.01,12 M12,12 L12.01,12 M19,12 L19.01,12", false),
            "moon" => ("M20,15 A9,9 0 1 1 9,4 A7,7 0 0 0 20,15 Z", false),
            "sun" => ("M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12 M12,1 L12,3 M12,21 L12,23 M1,12 L3,12 M21,12 L23,12 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4", false),
            "clock" => ("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M12,7 L12,12 L15,14", false),
            "lock" => ("M7,10 L7,7 A5,5 0 0 1 17,7 L17,10 M5,10 L19,10 L19,21 L5,21 Z M12,14 L12,17", false),
            "exit" => ("M10,4 L4,4 L4,20 L10,20 M9,12 L21,12 M16,7 L21,12 L16,17", false),
            _ => throw new ArgumentException($"Unknown Icon: {name}", nameof(name))
        };
        var path = new Path { Data = Geometry.Parse(data), StrokeThickness = 1.65, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        BindForeground(path, filled ? Path.FillProperty : Path.StrokeProperty);
        return new Viewbox { Width = size, Height = size, Child = new Canvas { Width = 24, Height = 24, Children = { path } }, IsHitTestVisible = false };
    }

    public static FrameworkElement ProviderIcon(ProviderKind kind, double size = 22)
        => SvgIcon(kind switch { ProviderKind.Codex => "codex", ProviderKind.OpenCode => "opencode", ProviderKind.OpenRouter => "openrouter", _ => kind.ToString().ToLowerInvariant() }, size);

    public static FrameworkElement AppIcon(double size = 22)
    {
        var path = new Path { Data = Geometry.Parse("M5,4 L5,13 A7,7 0 0 0 19,13 L19,4"), StrokeThickness = 3.4,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        BindForeground(path, Path.StrokeProperty);
        return new Viewbox { Width = size, Height = size, Child = new Canvas { Width = 24, Height = 24, Children = { path } }, IsHitTestVisible = false };
    }

    private static FrameworkElement SvgIcon(string name, double size)
    {
        using var stream = typeof(PresentationTheme).Assembly.GetManifestResourceStream($"ProviderIcons.{name}.svg")
            ?? throw new InvalidOperationException($"Missing Provider Icon: {name}");
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var root = XDocument.Load(reader).Root ?? throw new InvalidOperationException("Invalid Provider Icon.");
        var box = ((string?)root.Attribute("viewBox") ?? "0 0 100 100").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (box.Length != 4 || box[2] <= 0 || box[3] <= 0) throw new InvalidOperationException("Invalid Icon View Box.");
        var canvas = new Canvas { Width = box[2], Height = box[3] };
        foreach (var element in root.Descendants().Where(e => e.Name.LocalName == "path"))
        {
            var geometry = Geometry.Parse((string?)element.Attribute("d") ?? "");
            if ((string?)element.Attribute("fill-rule") == "evenodd")
            {
                var pathGeometry = PathGeometry.CreateFromGeometry(geometry);
                pathGeometry.FillRule = FillRule.EvenOdd;
                geometry = pathGeometry;
            }
            var path = new Path { Data = geometry, Stretch = Stretch.None };
            var fill = (string?)element.Attribute("fill") ?? (string?)root.Attribute("fill") ?? "currentColor";
            if (fill == "currentColor") BindForeground(path, Path.FillProperty);
            else if (fill != "none") path.Fill = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(fill));
            Canvas.SetLeft(path, -box[0]); Canvas.SetTop(path, -box[1]);
            canvas.Children.Add(path);
        }
        return new Viewbox { Width = size, Height = size, Child = canvas, IsHitTestVisible = false };
    }
    private static void BindForeground(Path path, DependencyProperty property) => path.SetBinding(property,
        new System.Windows.Data.Binding { RelativeSource = RelativeSource.Self, Path = new PropertyPath("(0)", TextElement.ForegroundProperty) });
}
