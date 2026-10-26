using System.Drawing.Imaging;
using System.Reflection;
using Gondwana.Tooling.WinForms.Theming;
using WeifenLuo.WinFormsUI.Docking;

// Run once per theme in a fresh process: dotnet run --project Testing/Gondwana.Tooling.ThemeProbe -- Light <output-directory>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var theme = Enum.Parse<ToolingTheme>(args[0]);
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var settings = Path.Combine(output, "preferences-" + theme + "-" + Guid.NewGuid());
        var workspace = Path.Combine(output, "sample-workspace");
        Directory.CreateDirectory(workspace);
        Environment.CurrentDirectory = workspace;
        AppContext.SetData("Gondwana.Tooling.SettingsRoot", settings);
        new ThemePreferenceStore(settings).Write(theme);
        ApplicationConfiguration.Initialize();
        Run(theme, output, args.Contains("--inspect"));
    }

    // Separate method ensures the preference is written before the process-wide appearance initializes.
    private static void Run(ToolingTheme theme, string output, bool inspect)
    {
        if (ToolingAppearance.Current != theme) throw new InvalidOperationException("Preference not restored.");
        ToolingAppearance.Initialize();
        foreach (var app in new[] { "Studio", "Assets", "Tilesheets", "Animations", "Audio", "Scenes", "Sprites" })
        {
            var assembly = Assembly.Load(app == "Studio" ? "Gondwana.Studio.WinForms" : $"Gondwana.Tooling.{app}.WinForms");
            var type = assembly.GetType($"Gondwana.Tooling.{app}.WinForms.MainForm", throwOnError: true)!;
            using var form = (Form)Activator.CreateInstance(type, nonPublic: true)!;
            form.Show();
            Application.DoEvents();
            if (app == "Studio")
            {
                var create = type.GetMethod("NewDocument", BindingFlags.Instance | BindingFlags.NonPublic)!;
                foreach (var format in new[] { "gaf", "gts", "gani", "gsnd", "gscn", "gspr" })
                    create.Invoke(form, new object?[] { format, format == "gaf" ? Path.Combine(output, theme + ".gaf") : null, null, false });
            }
            else
            {
                if (app == "Assets")
                    type.GetMethod("OpenDocument", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                        .Invoke(form, new object?[] { Path.Combine(output, theme + ".gaf") });
                else
                {
                    var create = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                        .First(m => m.Name == "NewDocument" && m.GetParameters().All(p => p.IsOptional));
                    create.Invoke(form, create.GetParameters().Select(p => p.DefaultValue).ToArray());
                }
            }
            ToolingAppearance.Apply(form);
            Application.DoEvents();
            if (app == "Studio") type.GetMethod("RebuildViewMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            var view = form.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "&View");
            var themeMenu = view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Theme");
            if (!themeMenu.DropDownItems.Cast<ToolStripMenuItem>().Select(item => item.Text)
                .SequenceEqual(new[] { "Dark", "Dark Blue", "Light" }) ||
                themeMenu.DropDownItems.Cast<ToolStripMenuItem>().Single(item => item.Checked).Text != ToolingPalette.Name(theme))
                throw new InvalidOperationException($"{app}: incorrect theme menu");
            foreach (var dock in Descendants(form).OfType<DockPanel>().ToArray())
            {
                using var expectedTheme = ToolingAppearance.CreateDockTheme(theme);
                if (dock.Theme.GetType() != expectedTheme.GetType())
                    throw new InvalidOperationException($"{app}: mismatched nested docking theme");
                foreach (var content in dock.Contents.OfType<DockContent>().ToArray()) content.Activate();
            }
            foreach (var grid in Descendants(form).OfType<PropertyGrid>())
                if (grid.ViewBackColor != ToolingAppearance.Palette.Background ||
                    grid.SelectedItemWithFocusBackColor != ToolingAppearance.Palette.Selection)
                    throw new InvalidOperationException($"{app}: unstyled nested property grid");
            foreach (var tree in Descendants(form).OfType<TreeView>())
                if (tree.BackColor != ToolingAppearance.Palette.Background)
                    throw new InvalidOperationException($"{app}: unstyled nested tree");
            var outer = Descendants(form).OfType<DockPanel>().First();
            var floating = outer.Contents.OfType<DockContent>().First();
            var state = floating.DockState;
            floating.DockState = DockState.Float;
            Application.DoEvents();
            floating.DockState = state;
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            using (var graphics = Graphics.FromImage(bitmap)) RenderDock(outer, graphics, form.Location);
            bitmap.Save(Path.Combine(output, $"{theme}-{app}.png"), ImageFormat.Png);
            Console.WriteLine($"PASS {theme} {app}: startup, menus, nested controls/docks, activation, float/redock, capture, dispose");
            if (inspect && app == "Studio")
            {
                form.Text += " — " + ToolingPalette.Name(theme) + " validation";
                using var timer = new System.Windows.Forms.Timer { Interval = 180000 };
                timer.Tick += (_, _) => form.Dispose();
                timer.Start();
                Application.Run(form);
                return;
            }
        }
    }

    // DockPanel dummy surfaces obscure panes in DrawToBitmap. Render each visible pane and its active
    // content explicitly, then its nested workspace. These are control renderings, not desktop screenshots.
    private static void RenderDock(DockPanel dock, Graphics graphics, Point origin)
    {
        void Draw(Control control)
        {
            if (control.Width <= 0 || control.Height <= 0) return;
            using var image = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(image, new Rectangle(Point.Empty, control.Size));
            var point = control.PointToScreen(Point.Empty);
            graphics.DrawImageUnscaled(image, point.X - origin.X, point.Y - origin.Y);
        }
        foreach (var pane in dock.Panes.Where(pane => !pane.IsHidden))
        {
            Draw(pane);
            if (pane.ActiveContent is not Control content) continue;
            Draw(content);
            foreach (var inner in Descendants(content).OfType<DockPanel>()) RenderDock(inner, graphics, origin);
        }
    }
    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
