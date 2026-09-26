using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal static class SpotAboutBoxFactory
{
    internal static AboutBox Create(
        RenderSurfaceHostBase host,
        View view,
        SKImage spotLogo,
        SKImage gondwanaLogo,
        SKTypeface typeface,
        IExternalUriLauncher uriLauncher)
    {
        Rectangle viewport = view.Viewport.TargetRectPx;

        const int dialogWidth = 420;
        const int dialogHeight = 606;
        var bounds = new Rectangle(
            viewport.Left + (viewport.Width - dialogWidth) / 2,
            viewport.Top + (viewport.Height - dialogHeight) / 2,
            dialogWidth,
            dialogHeight);

        var about = new AboutBox(
            host,
            view,
            applicationName: "Spot!",
            version: string.Empty,
            description: "Built with Gondwana Game Engine",
            logo: spotLogo,
            bounds: bounds,
            nickname: "spot.about",
            uriLauncher: uriLauncher,
            hyperlinkUri: new Uri("https://github.com/isthimius/gondwana"),
            hyperlinkText: "View Gondwana on GitHub");

        about.Panel.SetColor(Color.Black)
                   .SetBorderColor(Color.FromArgb(255, 90, 90, 90))
                   .SetCornerRadius(0f);
        about.TitleBar.SetColor(Color.FromArgb(255, 32, 32, 32))
                      .SetCornerRadius(0f);
        about.TitleText.SetText("About Spot!")
                       .SetColors(SKColors.White, SKColors.Transparent);

        if (about.Logo is not null)
        {
            about.Logo.ScreenBounds = new Rectangle(bounds.Left + 30, bounds.Top + 36, 360, 240);
            about.SetLocalOffset(about.Logo, new Vector2(30, 36));
            about.Logo.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        var gondwanaImage = new DirectImage(
            gondwanaLogo,
            host,
            view,
            new Rectangle(bounds.Left + 110, bounds.Top + 231, 200, 200),
            "spot.about.gondwanaLogo")
            .SetScaleMode(DirectImage.ScaleMode.Fit);
        gondwanaImage.ZOrder = 10_004;
        about.Add(gondwanaImage);

        about.HeaderText.ScreenBounds = new Rectangle(bounds.Left, bounds.Top + 441, 420, 30);
        about.SetLocalOffset(about.HeaderText, new Vector2(0, 441));
        about.HeaderText.SetText("Built with Gondwana Game Engine")
                        .SetFont(typeface, 21f, minSize: 16f)
                        .SetColors(SKColors.White, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .EnableWrapping(false);

        about.VersionText.SetText(string.Empty);
        about.DetailsText.SetText(string.Empty);

        if (about.Hyperlink is not null)
        {
            about.Hyperlink.Label.ScreenBounds = new Rectangle(bounds.Left, bounds.Top + 486, 420, 25);
            about.SetLocalOffset(about.Hyperlink, new Vector2(0, 486));
            about.Hyperlink.Label.SetFont(SKTypeface.Default, 19f, minSize: 14f)
                                 .SetColors(new SKColor(135, 206, 250), SKColors.Transparent)
                                 .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                                 .EnableWrapping(false);
        }

        about.OkButton.Background.ScreenBounds = new Rectangle(bounds.Left + 160, bounds.Top + 541, 100, 32);
        about.OkButton.Label.ScreenBounds = new Rectangle(bounds.Left + 160, bounds.Top + 541, 100, 32);
        about.SetLocalOffset(about.OkButton, new Vector2(160, 541));
        about.OkButton.SetBackgroundColors(
                         Color.FromArgb(255, 52, 52, 52),
                         Color.FromArgb(255, 70, 70, 70),
                         Color.FromArgb(255, 38, 38, 38))
                      .SetTextColor(Color.White);

        return about;
    }
}
