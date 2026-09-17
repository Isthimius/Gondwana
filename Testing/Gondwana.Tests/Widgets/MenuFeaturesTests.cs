using System.Drawing;
using System.Numerics;
using System.Reflection;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Views;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Menus;
using SkiaSharp;

namespace Gondwana.Tests.Widgets;

public sealed class MenuFeaturesTests
{
    [Fact]
    public void Keys_AreStableRecursiveAndOptional()
    {
        using var context = new MenuContext();
        context.Bar.AddMenu("File", menu => menu.AddItem("Same").AddItem("Same")
            .AddSubMenu("Export", export => export.AddSubMenu("Image", image => image
                .AddCheckItem("PNG", key: "file.png"))));
        var item = context.Bar["file.png"];
        item.SetChecked(true).SetEnabled(false);
        Assert.Same(item, context.Bar.GetItem("file.png"));
        Assert.True(context.Bar.TryGetItem("file.png", out var found));
        Assert.Same(item, found);
        Assert.False(context.Bar.TryGetItem("missing", out found));
        Assert.Null(found);
        Assert.Throws<KeyNotFoundException>(() => context.Bar.GetItem("missing"));
        Assert.Null(typeof(MenuItemWidget).GetProperty(nameof(MenuItemWidget.Key))!.SetMethod);
    }

    [Fact]
    public void FailedConfiguration_RollsBackKeysShortcutsAndWidgets()
    {
        using var context = new MenuContext();
        context.Bar.AddMenu("File", menu => menu.AddItem("Save", key: "save", shortcut: KeyGesture.Ctrl('S')));
        Assert.Throws<ArgumentException>(() => context.Bar.AddMenu("Bad", menu => menu
            .AddItem("Temporary", key: "temp", shortcut: KeyGesture.Ctrl('T'))
            .AddSubMenu("Nested", nested => nested.AddItem("Duplicate", key: "save"))));
        Assert.Single(context.Bar.Menus);
        Assert.False(context.Bar.TryGetItem("temp", out _));
        context.Bar.AddMenu("Good", menu => menu.AddItem("Temporary", key: "temp", shortcut: KeyGesture.Ctrl('T')));
        Assert.Throws<ArgumentException>(() => context.Bar.Menus[0].DropDown.AddItem("Conflict", shortcut: KeyGesture.Ctrl('S')));
        Assert.Throws<ArgumentException>(() => context.Bar.Menus[0].DropDown.AddItem("Empty", key: " "));
    }

    [Fact]
    public void DisposedItem_UnregistersKeyAndShortcut()
    {
        using var context = new MenuContext();
        int count = 0;
        context.Bar.AddMenu("File", menu => menu.AddItem("Save", () => count++, key: "save", shortcut: KeyGesture.Ctrl('S')));
        var item = context.Bar["save"];
        item.Dispose();
        Assert.False(context.Bar.TryGetItem("save", out _));
        context.Key('S', KeyboardModifierState.Ctrl);
        item.PerformClick();
        Assert.Equal(0, count);
        context.Bar.Menus[0].DropDown.AddItem("Replacement", key: "save", shortcut: KeyGesture.Ctrl('S'));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CheckInvocation_UsesOnePathAndClosesBeforeCallback(int path)
    {
        using var context = new MenuContext();
        var observations = new List<string>();
        context.Bar.AddMenu("View", menu => menu.AddCheckItem("Grid", value =>
        {
            Assert.True(value);
            Assert.True(context.Bar["grid"].IsChecked);
            Assert.Equal(-1, context.Bar.OpenMenuIndex);
            observations.Add("callback");
        }, key: "grid", shortcut: KeyGesture.Ctrl('G')));
        var item = context.Bar["grid"];
        context.Bar.ItemInvoked += _ => observations.Add("bar");
        item.Invoked += _ => observations.Add("item");
        context.Bar.OpenMenuAt(0);
        if (path == 0) item.PerformClick();
        if (path == 1) context.Key(13);
        if (path == 2) context.Key('G', KeyboardModifierState.Ctrl);
        if (path == 3) Pointer(item, context.View, "DispatchPointerClick");
        Assert.Equal(new[] { "bar", "item", "callback" }, observations);
        Assert.True(item.IsChecked);
    }

    [Fact]
    public void CheckSetter_IsSilentAndDisabledInvocationDoesNotToggle()
    {
        using var context = new MenuContext();
        int count = 0;
        context.Bar.AddMenu("View", menu => menu.AddCheckItem("Grid", _ => count++, isChecked: true, key: "grid"));
        var item = context.Bar["grid"];
        Assert.True(item.IsChecked);
        item.SetChecked(false);
        Assert.False(item.IsChecked);
        item.SetEnabled(false).PerformClick();
        Assert.False(item.IsChecked);
        Assert.Equal(0, count);
        item.SetEnabled(true).PerformClick();
        Assert.True(item.IsChecked);
        item.PerformClick();
        Assert.False(item.IsChecked);
        Assert.Equal(2, count);
    }

    [Fact]
    public void RadioGroups_AreLocalExclusiveAndProgrammaticallySettable()
    {
        using var context = new MenuContext();
        context.Bar.AddMenu("View", menu => menu
            .AddRadioItem("Pixels", null, "units", isChecked: true, key: "pixels")
            .AddRadioItem("Tiles", null, "units", key: "tiles")
            .AddRadioItem("Other", null, "other", isChecked: true, key: "other")
            .AddSubMenu("Child", child => child.AddRadioItem("Child", null, "units", isChecked: true, key: "child")));
        var pixels = context.Bar["pixels"];
        var tiles = context.Bar["tiles"];
        tiles.SetChecked(true);
        Assert.False(pixels.IsChecked);
        Assert.True(tiles.IsChecked);
        tiles.PerformClick();
        Assert.True(tiles.IsChecked);
        Assert.True(tiles.IsChecked);
        Assert.True(context.Bar["other"].IsChecked);
        Assert.True(context.Bar["child"].IsChecked);
        pixels.SetEnabled(false).PerformClick();
        Assert.False(pixels.IsChecked);
        pixels.SetChecked(true);
        Assert.False(tiles.IsChecked);
    }

    [Fact]
    public void DeepSubmenus_NavigateUnwindInvokeAndDisposeRecursively()
    {
        using var context = new MenuContext();
        int count = 0;
        BuildDeep(context, () => count++);
        var root = context.Bar.Menus[0].DropDown;
        var export = context.Bar["export"].SubMenu!;
        var image = context.Bar["image"].SubMenu!;
        Assert.Contains(export, root.ChildWidgets);
        Assert.Contains(image, export.ChildWidgets);
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        Assert.True(export.IsOpen);
        Assert.Equal(0, export.SelectedIndex);
        context.Key(13);
        Assert.True(image.IsOpen);
        Assert.True(image.Panel.ZOrder > export.Panel.ZOrder);
        context.Key(37);
        Assert.False(image.IsOpen);
        Assert.True(export.IsOpen);
        context.Key(39);
        context.Key(27);
        Assert.False(image.IsOpen);
        context.Key(27);
        Assert.False(export.IsOpen);
        Assert.True(root.IsOpen);
        context.Key(39);
        context.Key(39);
        context.Key(13);
        Assert.Equal(1, count);
        Assert.All(new[] { root, export, image }, menu => { Assert.False(menu.Visible); Assert.False(menu.IsOpen); });
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        context.Key(39);
        int disposed = 0;
        context.Bar["png"].Disposing += (_, _) => disposed++;
        context.Bar.Dispose();
        Assert.Equal(1, disposed);
        Assert.All(new[] { root, export, image }, menu => { Assert.False(menu.Visible); Assert.False(menu.IsOpen); });
        context.Key('P', KeyboardModifierState.Ctrl);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Hover_OpensSubmenuAndClosesSiblingAndParentSwitchClosesHierarchy()
    {
        using var context = new MenuContext();
        context.Bar.AddMenu("File", menu => menu
            .AddSubMenu("One", child => child.AddItem("A"), key: "one")
            .AddSubMenu("Two", child => child.AddItem("B"), key: "two"))
            .AddMenu("Help", menu => menu.AddItem("About"));
        context.Bar.OpenMenuAt(0);
        Pointer(context.Bar["one"], context.View, "DispatchPointerEnter");
        Assert.True(context.Bar["one"].SubMenu!.IsOpen);
        Pointer(context.Bar["two"], context.View, "DispatchPointerEnter");
        Assert.False(context.Bar["one"].SubMenu!.Visible);
        Assert.True(context.Bar["two"].SubMenu!.IsOpen);
        Pointer(context.Bar.Menus[1].Header, context.View, "DispatchPointerEnter");
        Assert.Equal(1, context.Bar.OpenMenuIndex);
        Assert.False(context.Bar["two"].SubMenu!.Visible);
    }

    [Fact]
    public void Placement_FlipsLeftAndClampsVerticallyAtEveryDepth()
    {
        using var context = new MenuContext(new Rectangle(520, 420, 120, 30));
        BuildDeep(context, () => { });
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        context.Key(39);
        var root = context.Bar.Menus[0].DropDown;
        var child = context.Bar["export"].SubMenu!;
        var deep = context.Bar["image"].SubMenu!;
        Assert.True(child.Panel.ScreenBounds.Left < root.Panel.ScreenBounds.Left);
        Assert.All(new[] { root, child, deep }, menu => Assert.True(context.View.Viewport.TargetRectPx.Contains(menu.Panel.ScreenBounds)));
    }

    [Theory]
    [InlineData(MenuDropDownAnimation.None)]
    [InlineData(MenuDropDownAnimation.Fade)]
    [InlineData(MenuDropDownAnimation.FadeAndReveal)]
    public void RapidCloseReopenHideDispose_LeavesNoVisibleDescendants(MenuDropDownAnimation animation)
    {
        using var context = new MenuContext();
        BuildDeep(context, () => { });
        context.Bar.DropDownAnimation = animation;
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        context.Key(39);
        var child = context.Bar["image"].SubMenu!;
        context.Bar.CloseMenu();
        Assert.False(child.Visible);
        Assert.False(context.Bar.HitTest(context.View, new Point(600, 450)));
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        context.Key(39);
        context.Bar.Hide();
        Assert.False(child.Visible);
        context.Bar.Show();
        Assert.False(child.Visible);
        Assert.Equal(-1, context.Bar.OpenMenuIndex);
        context.Bar.Dispose();
        Assert.Empty(child.Children);
    }

    [Fact]
    public void IconsAndMarkers_AlignInSeparateColumnsAndFollowVisibility()
    {
        using var bitmap = new SKBitmap(8, 8);
        using var icon = SKImage.FromBitmap(bitmap);
        using var context = new MenuContext();
        context.Bar.AddMenu("View", menu => menu.AddItem("Plain", key: "plain")
            .AddCheckItem("Grid", key: "grid", icon: icon, isChecked: true)
            .AddRadioItem("Pixels", null, "units", key: "pixels", isChecked: true));
        var plain = context.Bar["plain"];
        var grid = context.Bar["grid"];
        Assert.Null(plain.IconDrawing);
        Assert.False(grid.IconDrawing!.Visible);
        context.Bar.OpenMenuAt(0);
        Assert.True(grid.IconDrawing.Visible);
        Assert.Equal(plain.Label.ScreenBounds.Left, grid.Label.ScreenBounds.Left);
        Assert.True(grid.Marker.ScreenBounds.Right <= grid.IconDrawing.ScreenBounds.Left);
        Assert.True(grid.IconDrawing.ScreenBounds.Right <= grid.Label.ScreenBounds.Left);
        context.Bar.CloseMenu();
        Assert.False(grid.IconDrawing.Visible);
        context.Bar.Dispose();
        Assert.Equal(8, icon.Width); // The caller still owns the image.
    }

    [Fact]
    public void Shortcuts_WorkClosedAndDeepWithoutOpeningOrStealingFocus()
    {
        using var context = new MenuContext();
        int count = 0;
        BuildDeep(context, () => count++);
        using var textBox = new TextBoxWidget(context.Host, context.View, new Rectangle(20, 250, 300, 30));
        textBox.Show();
        context.Router.Focus(textBox);
        context.Key('P', KeyboardModifierState.Ctrl | KeyboardModifierState.Shift);
        Assert.Equal(0, count);
        context.Key('P', KeyboardModifierState.Ctrl);
        Assert.Equal(1, count);
        Assert.Same(textBox, context.Router.FocusedWidget);
        Assert.Equal(-1, context.Bar.OpenMenuIndex);
        Assert.Equal("Ctrl+P", context.Bar["png"].ShortcutText);
        context.Bar["export"].SetEnabled(false);
        context.Key('P', KeyboardModifierState.Ctrl);
        Assert.Equal(1, count);
        context.Bar["export"].SetEnabled(true);
        context.Bar["png"].SetEnabled(false);
        context.Key('P', KeyboardModifierState.Ctrl);
        Assert.Equal(1, count);
        context.Bar["png"].SetEnabled(true);
        context.Bar.Hide();
        context.Key('P', KeyboardModifierState.Ctrl);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusedWidget_GetsFirstOpportunityForShortcutAndMnemonic(bool mnemonic)
    {
        using var context = new MenuContext();
        var sequence = new List<string>();
        context.Bar.AddMenu("File", menu => menu.AddItem("Open", () => sequence.Add("command"), shortcut: KeyGesture.Ctrl('F')), mnemonic: 'f');
        using var textBox = new TextBoxWidget(context.Host, context.View, new Rectangle(10, 250, 200, 30));
        textBox.Show();
        bool consume = true;
        textBox.KeyboardInput += args => { sequence.Add("focused"); args.Handled = consume; };
        context.Router.Focus(textBox);
        context.Key('F', mnemonic ? KeyboardModifierState.Alt : KeyboardModifierState.Ctrl);
        Assert.Equal(new[] { "focused" }, sequence);
        Assert.Equal(-1, context.Bar.OpenMenuIndex);
        consume = false;
        context.Key('F', mnemonic ? KeyboardModifierState.Alt : KeyboardModifierState.Ctrl);
        if (mnemonic)
        {
            Assert.Equal(0, context.Bar.OpenMenuIndex);
            context.Key(27);
            Assert.Same(textBox, context.Router.FocusedWidget);
        }
        else Assert.Equal(new[] { "focused", "focused", "command" }, sequence);
    }

    [Fact]
    public void OneKey_InvokesOnlyMostRecentlyRegisteredEligibleBar()
    {
        using var context = new MenuContext();
        int first = 0, second = 0;
        context.Bar.AddMenu("First", menu => menu.AddItem("Go", () => first++, shortcut: KeyGesture.Ctrl('G')));
        using var other = new MenuBarWidget(context.Host, context.View, new Rectangle(0, 40, 640, 30))
            .AddMenu("Second", menu => menu.AddItem("Go", () => second++, shortcut: KeyGesture.Ctrl('G')));
        other.Show();
        context.Key('G', KeyboardModifierState.Ctrl);
        Assert.Equal(0, first);
        Assert.Equal(1, second);
        other.Hide();
        context.Key('G', KeyboardModifierState.Ctrl);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void OpeningOlderBar_PromotesUnhandledAcceleratorsWithoutBreakingPointerOrder()
    {
        using var context = new MenuContext();
        int first = 0, second = 0;
        context.Bar.AddMenu("First", menu => menu.AddItem("Go", () => first++, shortcut: KeyGesture.Ctrl('G')));
        using var other = new MenuBarWidget(context.Host, context.View, new Rectangle(0, 40, 640, 30))
            .AddMenu("Second", menu => menu.AddItem("Go", () => second++, shortcut: KeyGesture.Ctrl('G')));
        other.Show();
        context.Bar.OpenMenuAt(0);
        var item = context.Bar.Menus[0].DropDown.Items[0];
        var bounds = item.Background.ScreenBounds;
        Assert.Same(item, context.Hit(new Point(bounds.Left + 10, bounds.Top + bounds.Height / 2)));
        Assert.Same(context.Bar.ChildWidgets.First(), context.Hit(new Point(600, 450)));
        // Bypass focused-header bubbling to exercise the unhandled fallback priority.
        context.Router.ClearFocus();
        context.Key('G', KeyboardModifierState.Ctrl);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(-1, context.Bar.OpenMenuIndex);
    }

    [Fact]
    public void DisposingLongestRow_RecalculatesWidthHeightAndRemainingPositions()
    {
        using var context = new MenuContext();
        context.Bar.AddMenu("File", menu => menu
            .AddItem(new string('W', 50), key: "long")
            .AddItem("Keep", key: "keep"));
        context.Bar.OpenMenuAt(0);
        var dropdown = context.Bar.Menus[0].DropDown;
        int oldWidth = dropdown.Width;
        int oldHeight = dropdown.Height;
        int firstRowTop = context.Bar["long"].Background.ScreenBounds.Top;
        context.Bar["long"].Dispose();
        Assert.True(dropdown.Width < oldWidth);
        Assert.Equal(oldHeight - MenuBarTheme.Default.ItemHeight, dropdown.Height);
        Assert.Equal(dropdown.Width, dropdown.Panel.ScreenBounds.Width);
        Assert.Equal(dropdown.Height, dropdown.Panel.ScreenBounds.Height);
        Assert.Equal(firstRowTop, context.Bar["keep"].Background.ScreenBounds.Top);
        var staleArea = new Point(dropdown.Panel.ScreenBounds.Left + oldWidth - 2, firstRowTop + 2);
        Assert.False(dropdown.HitTest(context.View, staleArea));
        context.Bar["keep"].Dispose();
        Assert.Empty(dropdown.Items);
        Assert.Equal(2 * MenuBarTheme.Default.DropDownVerticalPadding, dropdown.Height);
        Assert.False(dropdown.HitTest(context.View, new Point(15, firstRowTop + 15)));
    }

    [Fact]
    public void Mnemonics_AreExplicitCaseInsensitiveAndConflictsRejected()
    {
        using var context = new MenuContext();
        int count = 0;
        context.Bar.AddMenu("File", menu => menu.AddItem("Disabled", () => count++, enabled: false, mnemonic: 'D')
            .AddSubMenu("Export", child => child.AddItem("PNG", () => count++, mnemonic: 'p'), mnemonic: 'e'), mnemonic: 'f');
        Assert.Equal("File", context.Bar.Menus[0].Header.Label.Text);
        Assert.Throws<ArgumentException>(() => context.Bar.AddMenu("Conflict", mnemonic: 'F'));
        Assert.Throws<ArgumentException>(() => context.Bar.Menus[0].DropDown.AddItem("Conflict", mnemonic: 'E'));
        context.Key('F', KeyboardModifierState.Alt);
        Assert.Equal(1, context.Bar.Menus[0].DropDown.SelectedIndex);
        context.Key('D');
        Assert.Equal(0, count);
        context.Key('E');
        context.Key('P');
        Assert.Equal(1, count);
        Assert.Equal(-1, context.Bar.OpenMenuIndex);
    }

    [Fact]
    public void Navigation_SkipsDisabledAndSeparatorsAndSwitchesTopLevel()
    {
        using var context = new MenuContext();
        int count = 0;
        context.Bar.AddMenu("File", menu => menu.AddItem("Disabled", enabled: false).AddSeparator()
            .AddCheckItem("Grid", _ => count++).AddItem("Last"))
            .AddMenu("Help", menu => menu.AddItem("About"));
        context.Bar.OpenMenuAt(0);
        Assert.Equal(1, context.Bar.Menus[0].DropDown.SelectedIndex);
        context.Key(38);
        Assert.Equal(2, context.Bar.Menus[0].DropDown.SelectedIndex);
        context.Key(40);
        Assert.Equal(1, context.Bar.Menus[0].DropDown.SelectedIndex);
        context.Key(32);
        Assert.Equal(1, count);
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        Assert.Equal(1, context.Bar.OpenMenuIndex);
        context.Key(37);
        Assert.Equal(0, context.Bar.OpenMenuIndex);
    }

    [Fact]
    public void ClickAway_ClosesDeepHierarchyAndReleasesHitArea()
    {
        using var context = new MenuContext();
        BuildDeep(context, () => { });
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        context.Key(39);
        var dismiss = context.Bar.ChildWidgets.First();
        Pointer(dismiss, context.View, "DispatchPointerClick");
        Assert.Equal(-1, context.Bar.OpenMenuIndex);
        Assert.False(context.Bar["image"].SubMenu!.Visible);
        Assert.False(context.Bar.HitTest(context.View, new Point(600, 400)));
    }

    [Fact]
    public void Callback_CanDisposeBarAfterHierarchyIsStable()
    {
        using var context = new MenuContext();
        context.Bar.AddMenu("File", menu => menu.AddItem("Exit", () =>
        {
            Assert.Equal(-1, context.Bar.OpenMenuIndex);
            context.Bar.Dispose();
        }));
        context.Bar.OpenMenuAt(0);
        context.Key(13);
        Assert.Empty(context.Bar.Children);
    }

    [Fact]
    public void Gestures_CompareExactlyAndGenerateDisplay()
    {
        Assert.Equal(KeyGesture.Ctrl('O'), new KeyGesture('O', KeyboardModifierState.Ctrl));
        Assert.NotEqual(KeyGesture.Ctrl('O'), KeyGesture.CtrlShift('O'));
        Assert.Equal("Ctrl+Shift+S", KeyGesture.CtrlShift('S').ToString());
        Assert.Equal("Alt+F4", new KeyGesture(115, KeyboardModifierState.Alt).ToString());
        Assert.Equal("F5", new KeyGesture(116).ToString());
    }

    [Fact]
    public void AnimationCompletion_DoesNotHideReopenedMenuOrLeaveRevealRunning()
    {
        using var context = new MenuContext();
        BuildDeep(context, () => { });
        context.Bar.DropDownAnimation = MenuDropDownAnimation.FadeAndReveal;
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        var child = context.Bar["export"].SubMenu!;
        context.Bar.CloseMenu();
        Assert.False(child.Visible);
        context.Bar.OpenMenuAt(0);
        var panel = context.Bar.Menus[0].DropDown.Panel;
        panel.Update(long.MaxValue / 2); // Deterministically complete all pending time.
        Assert.True(context.Bar.Menus[0].DropDown.IsOpen);
        Assert.True(panel.Visible);
        context.Bar.CloseMenu();
        panel.Update(long.MaxValue);
        Assert.False(context.Bar.Menus[0].DropDown.Visible);
        Assert.False(context.Bar.HitTest(context.View, new Point(600, 450)));
        var revealAnimating = typeof(DirectDrawingBase).GetField("_revealAnimating", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.False((bool)revealAnimating.GetValue(panel)!);
        Assert.False((bool)revealAnimating.GetValue(child.Panel)!);
    }

    [Fact]
    public void PointerHitOrder_PrefersDeepItemsAndDismissLayerOnlyWhileOpen()
    {
        using var context = new MenuContext();
        BuildDeep(context, () => { });
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        context.Key(39);
        var item = context.Bar["png"];
        var bounds = item.Background.ScreenBounds;
        Assert.Same(item, context.Hit(new Point(bounds.Left + 10, bounds.Top + bounds.Height / 2)));
        Assert.Same(context.Bar.ChildWidgets.First(), context.Hit(new Point(600, 450)));
        context.Bar.CloseMenu();
        Assert.Null(context.Hit(new Point(600, 450)));
    }

    [Fact]
    public void DisabledOwner_ClosesItsBranchAndPreventsDescendantActivation()
    {
        using var context = new MenuContext();
        int count = 0;
        BuildDeep(context, () => count++);
        context.Bar.OpenMenuAt(0);
        context.Key(39);
        context.Key(39);
        context.Bar["export"].SetEnabled(false);
        Assert.False(context.Bar["image"].SubMenu!.Visible);
        context.Bar["png"].PerformClick();
        Assert.Equal(0, count);
        context.Bar.CloseMenu();
        context.Bar["export"].SetEnabled(true);
        Assert.False(context.Bar["export"].IsInputEnabled);
    }

    [Fact]
    public void LegacyText_IsDisplayOnlyAndGestureDisplayWins()
    {
        using var context = new MenuContext();
        int count = 0;
        context.Bar.AddMenu("File", menu => menu.AddItem("Legacy", () => count++, shortcutText: "Ctrl+O")
            .AddItem("Typed", () => count++, shortcutText: "custom", shortcut: KeyGesture.Ctrl('S'), key: "typed"));
        context.Key('O', KeyboardModifierState.Ctrl);
        Assert.Equal(0, count);
        Assert.Equal("Ctrl+S", context.Bar["typed"].ShortcutText);
        context.Key('S', KeyboardModifierState.Ctrl);
        Assert.Equal(1, count);
    }

    [Fact]
    public void TypedInput_ConsumedByTextBoxDoesNotActivateUnmodifiedShortcut()
    {
        using var context = new MenuContext();
        int count = 0;
        context.Bar.AddMenu("File", menu => menu.AddItem("A", () => count++, shortcut: new KeyGesture('A')));
        using var textBox = new TextBoxWidget(context.Host, context.View, new Rectangle(10, 250, 200, 30));
        textBox.Show();
        context.Router.Focus(textBox);
        context.Key('A');
        Assert.Equal("a", textBox.Text);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Columns_DoNotOverlapForLongLabelsShortcutsAndSubmenuArrows()
    {
        using var context = new MenuContext();
        context.Bar.AddMenu("File", menu => menu
            .AddItem("A substantially longer command label", key: "label")
            .AddItem("Save", shortcut: KeyGesture.CtrlShift('S'), key: "shortcut")
            .AddSubMenu("Export", child => child.AddItem("PNG")));
        var dropdown = context.Bar.Menus[0].DropDown;
        dropdown.SetWidth(MenuBarTheme.Default.MinimumDropDownWidth);
        foreach (var item in dropdown.Items)
        {
            Assert.True(item.Label.ScreenBounds.Right < item.ShortcutLabel.ScreenBounds.Left);
            Assert.True(item.ShortcutLabel.ScreenBounds.Right <= item.Arrow.ScreenBounds.Left);
        }
    }

    [Fact]
    public void MenuVisuals_RenderChecksRadiosIconsAndText()
    {
        using var bitmap = new SKBitmap(18, 18);
        bitmap.Erase(SKColors.Gold);
        using var icon = SKImage.FromBitmap(bitmap);
        using var context = new MenuContext();
        context.Bar.AddMenu("View", menu => menu
            .AddCheckItem("Show Grid", isChecked: true, icon: icon, key: "grid", shortcut: KeyGesture.Ctrl('G'))
            .AddRadioItem("Pixels", null, "units", isChecked: true)
            .AddRadioItem("Tiles", null, "units")
            .AddSeparator()
            .AddSubMenu("Export", child => child.AddItem("PNG")));
        context.Bar.OpenMenuAt(0);
        using var backbuffer = new Gondwana.Rendering.Backbuffers.BitmapBackbuffer(640, 480);
        foreach (var drawing in Visuals(context.Bar).Where(drawing => drawing.Visible).OrderBy(drawing => drawing.ZOrder))
            drawing.Draw(backbuffer, drawing.ScreenBounds);
        using var snapshot = backbuffer.Snapshot();
        using var rendered = SKBitmap.FromImage(snapshot);
        var grid = context.Bar["grid"];
        var iconBounds = grid.IconDrawing!.ScreenBounds;
        Assert.Equal(SKColors.Gold, rendered.GetPixel(iconBounds.Left + 4, iconBounds.Top + 4));
        var marker = grid.Marker.ScreenBounds;
        bool hasMarkerInk = false;
        for (int y = marker.Top; y < marker.Bottom; y++)
            for (int x = marker.Left; x < marker.Right; x++)
                hasMarkerInk |= rendered.GetPixel(x, y).Red > 180;
        Assert.True(hasMarkerInk);

    }

    private static IEnumerable<DirectDrawingBase> Visuals(IDirectCompositeContainer container)
    {
        foreach (var child in container.Children)
        {
            if (child is DirectDrawingBase drawing) yield return drawing;
            if (child is IDirectCompositeContainer nested)
                foreach (var visual in Visuals(nested)) yield return visual;
        }
    }

    private static void BuildDeep(MenuContext context, Action callback) => context.Bar.AddMenu("File", file => file
        .AddSubMenu("Export", export => export.AddSubMenu("Image", image => image
            .AddItem("PNG", callback, key: "png", shortcut: KeyGesture.Ctrl('P')),
            key: "image"), key: "export"));

    private static void Pointer(WidgetBase widget, View view, string method) => typeof(WidgetBase)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(widget, new object[] { new WidgetPointerEventArgs(widget, view, PointF.Empty,
            WidgetPointerButtonEnum.Left, 1, Vector2.Zero, 0, WidgetInputRouter.MousePointerId) });

    private sealed class MenuContext : IDisposable
    {
        public TestRenderSurfaceHost Host { get; } = new();
        public View View { get; }
        public WidgetInputRouter Router { get; }
        public MenuBarWidget Bar { get; }
        public MenuContext(Rectangle? bounds = null)
        {
            Host.ViewManager.AddView(new Rectangle(0, 0, 640, 480), zOrder: 0);
            View = Host.ViewManager.Views.Single(v => v.ZOrder == 0 && v.Viewport.TargetRectPx.Width == 640);
            Router = new WidgetInputRouter(Host, null, null, null);
            Router.Start();
            Bar = new MenuBarWidget(Host, View, bounds ?? new Rectangle(0, 0, 640, 30))
            { DropDownAnimation = MenuDropDownAnimation.None };
            Bar.Show();
        }
        public void Key(int key, KeyboardModifierState modifiers = KeyboardModifierState.None) => typeof(WidgetInputRouter)
            .GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Router, new object[] { new KeyDownEventArgs(new KeyEventConfiguration(key.ToString()), modifiers, KeyAction.Pressed) });
        public WidgetBase? Hit(Point point)
        {
            var hit = typeof(WidgetInputRouter).GetMethod("HitTest", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Router, new object[] { point });
            return hit?.GetType().GetProperty("Widget")!.GetValue(hit) as WidgetBase;
        }
        public void Dispose()
        {
            Bar.Dispose();
            Router.Dispose();
            Host.Dispose();
        }
    }
}
