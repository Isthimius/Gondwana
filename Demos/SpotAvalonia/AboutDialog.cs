using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// About dialog for SpotAvalonia showing logos, attribution, and a link to the Gondwana repository.
/// </summary>
internal sealed class AboutDialog : Window
{
    private const string RepoUrl = "https://github.com/isthimius/gondwana";

    internal AboutDialog()
    {
        Title       = "About Spot (Avalonia)";
        Width       = 420;
        Height      = 580;
        CanResize   = false;
        Background  = new SolidColorBrush(Colors.Black);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing             = 0,
        };

        // Spot logo
        var spotLogoPath = Path.Combine(AppContext.BaseDirectory, "assets", "spot.png");
        if (File.Exists(spotLogoPath))
        {
            var spotBitmap = new Bitmap(spotLogoPath);
            Closed += (_, _) => spotBitmap.Dispose();

            var spotLogo = new Image
            {
                Source              = spotBitmap,
                Width               = 360,
                Height              = 240,
                Stretch             = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 0),
            };
            root.Children.Add(spotLogo);
        }

        // Gondwana logo
        var gondwanaLogoPath = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        if (File.Exists(gondwanaLogoPath))
        {
            var gondwanaLogo = new Image
            {
                Source              = new Bitmap(gondwanaLogoPath),
                Width               = 200,
                Height              = 200,
                Stretch             = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, -40, 0, 0),
            };
            root.Children.Add(gondwanaLogo);
        }

        // Attribution label
        var label = new TextBlock
        {
            Text                = "Built with Gondwana Game Engine",
            Foreground          = Brushes.White,
            FontSize            = 16,
            FontWeight          = FontWeight.Bold,
            TextAlignment       = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 8, 0, 8),
        };
        root.Children.Add(label);

        // Repository link
        var link = new TextBlock
        {
            Text                = RepoUrl,
            Foreground          = new SolidColorBrush(Colors.LightSkyBlue),
            TextDecorations     = TextDecorations.Underline,
            FontSize            = 14,
            TextAlignment       = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Cursor              = new Cursor(StandardCursorType.Hand),
            Margin              = new Thickness(0, 0, 0, 16),
        };
        link.PointerPressed += (_, _) =>
        {
#if !BROWSER
            Process.Start(new ProcessStartInfo { FileName = RepoUrl, UseShellExecute = true });
#endif
        };
        root.Children.Add(link);

        // OK button
        var okButton = new Button
        {
            Content             = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth            = 100,
            MinHeight           = 32,
        };
        okButton.Click += (_, _) => Close();
        root.Children.Add(okButton);

        Content = root;
    }
}
