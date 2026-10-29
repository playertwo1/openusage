using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Control = System.Windows.Controls.Control;
using FontFamily = System.Windows.Media.FontFamily;
using SystemColors = System.Windows.SystemColors;

namespace OpenUsage.Windows;

public static partial class PresentationTheme
{
    public static bool IsDark(DisplayPreferences display)
    {
        if (SystemParameters.HighContrast) return SystemColors.WindowColor.R < 128;
        if (display.Theme != ColorTheme.System) return display.Theme == ColorTheme.Dark;
        using var settings = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return settings?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    public static void Apply(FrameworkElement root, DisplayPreferences display)
    {
        var dark = IsDark(display);
        var resources = (ResourceDictionary)XamlReader.Parse(Styles);
        var colors = dark ? new Dictionary<string, string>
        {
            ["Background"] = "#18191D", ["Surface"] = "#24262C", ["Raised"] = "#30333A",
            ["Foreground"] = "#F4F4F6", ["Muted"] = "#A9ABB5", ["Border"] = "#393C46",
            ["Accent"] = "#BF5AF2", ["OnAccent"] = "#1B0A26", ["AccentSoft"] = "#3A2A4D", ["Warning"] = "#FFD60A",
            ["Danger"] = "#FF453A", ["Success"] = "#30D158", ["Track"] = "#42454E",
            ["S5h"] = "#BF5AF2", ["Swk"] = "#64D2FF"
        } : new Dictionary<string, string>
        {
            ["Background"] = "#FCFCFD", ["Surface"] = "#F2F2F5", ["Raised"] = "#FFFFFF",
            ["Foreground"] = "#202127", ["Muted"] = "#64656D", ["Border"] = "#E0E1E6",
            ["Accent"] = "#9B30D0", ["OnAccent"] = "#FFFFFF", ["AccentSoft"] = "#F3E8FB", ["Warning"] = "#A96500",
            ["Danger"] = "#C73A33", ["Success"] = "#22824B", ["Track"] = "#DDE0E5",
            ["S5h"] = "#9B30D0", ["Swk"] = "#0A8FC2"
        };
        foreach (var (key, value) in colors)
        {
            var brush = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
            brush.Freeze();
            resources[key] = brush;
        }
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "Background", "Surface", "Raised", "AccentSoft" }) resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "Foreground", "Muted", "Border", "Track" }) resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "Accent", "Success", "Warning", "Danger", "S5h", "Swk" }) resources[key] = SystemColors.HighlightBrush;
            resources["OnAccent"] = SystemColors.HighlightTextBrush;
        }
        root.Resources = resources;
        root.SetValue(TextElement.FontFamilyProperty, new FontFamily("Segoe UI Variable, Segoe UI"));
        root.SetValue(TextElement.FontSizeProperty, 13d);
        root.SetResourceReference(TextElement.ForegroundProperty, "Foreground");
        root.UseLayoutRounding = true;
        root.SnapsToDevicePixels = true;
    }

    public static Brush Brush(FrameworkElement root, string key) => root.TryFindResource(key) as Brush ?? Brushes.Black;

    private const string Styles = """
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="TextBlock">
            <Setter Property="TextOptions.TextFormattingMode" Value="Display" />
          </Style>
          <Style x:Key="FocusRing" TargetType="Control">
            <Setter Property="Template"><Setter.Value><ControlTemplate>
              <Rectangle Margin="-3" RadiusX="8" RadiusY="8" Stroke="{DynamicResource Accent}" StrokeThickness="2" />
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style x:Key="QuietButton" TargetType="Button">
            <Setter Property="Foreground" Value="{DynamicResource Foreground}" />
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderBrush" Value="Transparent" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="11,7" />
            <Setter Property="MinHeight" Value="32" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="HorizontalContentAlignment" Value="Center" />
            <Setter Property="VerticalContentAlignment" Value="Center" />
            <Setter Property="FocusVisualStyle" Value="{StaticResource FocusRing}" />
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
              <Border x:Name="chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                  VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True" />
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="chrome" Property="Background" Value="{DynamicResource Surface}" /></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="chrome" Property="Background" Value="{DynamicResource Border}" /></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45" /></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style x:Key="PrimaryButton" TargetType="Button" BasedOn="{StaticResource QuietButton}">
            <Setter Property="Background" Value="{DynamicResource Accent}" />
            <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
              <Border x:Name="chrome" Background="{TemplateBinding Background}" CornerRadius="8" Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True" />
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="chrome" Property="Opacity" Value="0.88" /></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="chrome" Property="Opacity" Value="0.72" /></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45" /></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style x:Key="IconButton" TargetType="Button" BasedOn="{StaticResource QuietButton}">
            <Setter Property="Width" Value="32" /><Setter Property="Height" Value="32" /><Setter Property="Padding" Value="6" />
          </Style>
          <Style x:Key="SegmentButton" TargetType="Button" BasedOn="{StaticResource QuietButton}">
            <Setter Property="Padding" Value="10,3" /><Setter Property="MinHeight" Value="24" />
            <Style.Triggers><Trigger Property="Tag" Value="selected">
              <Setter Property="Background" Value="{DynamicResource Raised}" /><Setter Property="FontWeight" Value="SemiBold" />
            </Trigger></Style.Triggers>
          </Style>
          <Style x:Key="Choice" TargetType="ComboBox">
            <Setter Property="Foreground" Value="{DynamicResource Foreground}" />
            <Setter Property="Background" Value="{DynamicResource Raised}" />
            <Setter Property="BorderBrush" Value="{DynamicResource Border}" />
            <Setter Property="Padding" Value="9,2" /><Setter Property="MinHeight" Value="22" />
            <Setter Property="MinWidth" Value="76" /><Setter Property="FontSize" Value="12" />
            <Setter Property="HorizontalContentAlignment" Value="Left" />
            <Setter Property="FocusVisualStyle" Value="{StaticResource FocusRing}" />
            <Setter Property="ItemContainerStyle"><Setter.Value><Style TargetType="ComboBoxItem">
              <Setter Property="Foreground" Value="{DynamicResource Foreground}" />
              <Setter Property="Background" Value="{DynamicResource Raised}" />
              <Setter Property="Padding" Value="8,4" />
              <Style.Triggers>
                <Trigger Property="IsHighlighted" Value="True"><Setter Property="Background" Value="{DynamicResource AccentSoft}" /></Trigger>
                <Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="{DynamicResource AccentSoft}" /></Trigger>
              </Style.Triggers>
            </Style></Setter.Value></Setter>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox">
              <Grid>
                <ToggleButton x:Name="toggle" Focusable="False" ClickMode="Press"
                              IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
                  <ToggleButton.Template><ControlTemplate TargetType="ToggleButton">
                    <Border Background="{DynamicResource Raised}" BorderBrush="{DynamicResource Border}" BorderThickness="1" CornerRadius="6">
                      <StackPanel HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,7,0">
                        <Path Data="M0,3 L3,0 L6,3" Stroke="{DynamicResource Muted}" StrokeThickness="1.2" HorizontalAlignment="Center" Margin="0,0,0,2" />
                        <Path Data="M0,0 L3,3 L6,0" Stroke="{DynamicResource Muted}" StrokeThickness="1.2" HorizontalAlignment="Center" />
                      </StackPanel>
                    </Border>
                  </ControlTemplate></ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter Margin="9,2,22,2" VerticalAlignment="Center" HorizontalAlignment="Left" IsHitTestVisible="False"
                                  Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" />
                <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False" PopupAnimation="None">
                  <Border Background="{DynamicResource Raised}" BorderBrush="{DynamicResource Border}" BorderThickness="1" CornerRadius="8" Padding="3" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}">
                    <ScrollViewer MaxHeight="320" CanContentScroll="True"><ItemsPresenter /></ScrollViewer>
                  </Border>
                </Popup>
              </Grid>
              <ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45" /></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style x:Key="Switch" TargetType="CheckBox">
            <Setter Property="Foreground" Value="{DynamicResource Foreground}" />
            <Setter Property="MinHeight" Value="16" /><Setter Property="Cursor" Value="Hand" />
            <Setter Property="FocusVisualStyle" Value="{StaticResource FocusRing}" />
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
              <Grid Background="Transparent">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                <ContentPresenter VerticalAlignment="Center" Margin="0,0,14,0" RecognizesAccessKey="True" />
                <Border x:Name="track" Grid.Column="1" Width="28" Height="16" CornerRadius="8" Background="{DynamicResource Track}" VerticalAlignment="Center">
                  <Ellipse x:Name="thumb" Width="12" Height="12" Fill="White" HorizontalAlignment="Left" Margin="2,0,0,0" />
                </Border>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="track" Property="Background" Value="{DynamicResource Accent}" /><Setter TargetName="thumb" Property="HorizontalAlignment" Value="Right" /><Setter TargetName="thumb" Property="Margin" Value="0,0,2,0" /></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="track" Property="Opacity" Value="0.8" /></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45" /></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ScrollViewer"><Setter Property="VerticalScrollBarVisibility" Value="Auto" /><Setter Property="HorizontalScrollBarVisibility" Value="Disabled" /></Style>
          <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="8" /><Setter Property="Background" Value="Transparent" />
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
              <Grid Background="Transparent">
                <Track x:Name="PART_Track" IsDirectionReversed="True" Orientation="{TemplateBinding Orientation}"
                       Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" ViewportSize="{TemplateBinding ViewportSize}"
                       Value="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False" /></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb MinHeight="24" Margin="2,0" Background="{DynamicResource Muted}">
                    <Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="{TemplateBinding Background}" CornerRadius="3" Opacity="0.5" /></ControlTemplate></Thumb.Template>
                  </Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False" /></Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Background" Value="{DynamicResource Raised}" /><Setter Property="Foreground" Value="{DynamicResource Foreground}" />
            <Setter Property="CaretBrush" Value="{DynamicResource Accent}" /><Setter Property="SelectionBrush" Value="{DynamicResource AccentSoft}" />
            <Setter Property="BorderBrush" Value="{DynamicResource Border}" /><Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="10,9" /><Setter Property="FontSize" Value="13" />
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox">
              <Border x:Name="field" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}">
                <ScrollViewer x:Name="PART_ContentHost" />
              </Border>
              <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="field" Property="BorderBrush" Value="{DynamicResource Accent}" /></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="PasswordBox">
            <Setter Property="Background" Value="{DynamicResource Raised}" /><Setter Property="Foreground" Value="{DynamicResource Foreground}" />
            <Setter Property="CaretBrush" Value="{DynamicResource Accent}" /><Setter Property="SelectionBrush" Value="{DynamicResource AccentSoft}" />
            <Setter Property="BorderBrush" Value="{DynamicResource Border}" /><Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="10,9" /><Setter Property="FontSize" Value="13" />
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="PasswordBox">
              <Border x:Name="field" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}">
                <ScrollViewer x:Name="PART_ContentHost" />
              </Border>
              <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="field" Property="BorderBrush" Value="{DynamicResource Accent}" /></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
        </ResourceDictionary>
        """;
}
