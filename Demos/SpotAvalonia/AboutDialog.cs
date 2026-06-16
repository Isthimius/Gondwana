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
    private const string REPO_URL = "https://github.com/isthimius/gondwana";
    private static readonly FontFamily ArchitectsDaughterFontFamily = TryCreateArchitectsDaughterFontFamily();

    public AboutDialog()
    {
        Title = "About Spot!";
        Width = 420;
        Height = 570;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.Black;

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Logo 1 - Spot logo
        var logoPath1 = Path.Combine(AppContext.BaseDirectory, "assets", "spot.png");
        if (File.Exists(logoPath1))
        {
            var logo1 = new Image
            {
                Source = new Bitmap(logoPath1),
                Width = 360,
                Height = 240,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(30, 0, 30, 0)
            };
            mainPanel.Children.Add(logo1);
        }

        // Logo 2 - Gondwana logo
        var logoPath2 = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        if (File.Exists(logoPath2))
        {
            var logo2 = new Image
            {
                Source = new Bitmap(logoPath2),
                Width = 200,
                Height = 200,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, -45, 0, 0)
            };
            mainPanel.Children.Add(logo2);
        }

        // Description text
        var description = new TextBlock
        {
            Text = "Built with Gondwana Game Engine",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            FontFamily = ArchitectsDaughterFontFamily,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 10, 0, 15)
        };
        mainPanel.Children.Add(description);

        // GitHub link
        var repoLink = new TextBlock
        {
            Text = "View Gondwana on GitHub",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#87CEFA")), // LightSkyBlue
            TextAlignment = TextAlignment.Center,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(0, 0, 0, 25)
        };

        repoLink.PointerPressed += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = REPO_URL,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently ignore if unable to open browser
            }
        };

        mainPanel.Children.Add(repoLink);

        // OK button
        var okButton = new Button
        {
            Content = "OK",
            Width = 100,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };

        okButton.Click += (_, _) => Close();

        mainPanel.Children.Add(okButton);

        Content = mainPanel;
    }

    private static FontFamily TryCreateArchitectsDaughterFontFamily()
    {
        try
        {
            return new FontFamily("avares://SpotAvalonia/assets/ArchitectsDaughter-Regular.ttf#Architects Daughter");
        }
        catch
        {
            return FontFamily.Default;
        }
    }
}