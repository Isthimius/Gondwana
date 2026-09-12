using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogue;
using Gondwana.Widgets.Hud;

namespace Gondwana.Tests.Widgets;

[Collection("SpriteManager")]
public sealed class WidgetWishlistTests : IDisposable
{
    private readonly List<Sprite> _sprites = [];

    [Fact]
    public void CheckBoxWidget_ToggleChangesStateAndRaisesEvent()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var checkBox = new CheckBoxWidget(
            host,
            view,
            new Rectangle(10, 20, 180, 28),
            "Music");

        bool? changedTo = null;
        checkBox.CheckedChanged += value => changedTo = value;

        checkBox.Toggle();

        Assert.True(checkBox.IsChecked);
        Assert.True(checkBox.Mark.Visible);
        Assert.Equal(true, changedTo);
    }

    [Fact]
    public void RadioButtonWidget_GroupMaintainsSingleSelection()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        var group = new RadioButtonGroup();

        using var first = new RadioButtonWidget(
            host,
            view,
            new Rectangle(10, 20, 180, 28),
            "Windowed",
            group,
            isSelected: true);

        using var second = new RadioButtonWidget(
            host,
            view,
            new Rectangle(10, 52, 180, 28),
            "Fullscreen",
            group);

        second.Select();

        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.Same(second, group.SelectedButton);
    }

    [Fact]
    public void ListBoxWidget_SelectionScrollsIntoViewAndHonorsItemHeight()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var listBox = new ListBoxWidget(
            host,
            view,
            new Rectangle(10, 20, 180, 64),
            ["One", "Two", "Three", "Four", "Five", "Six"]);

        listBox.ItemHeight = 20;
        listBox.SelectedIndex = 5;

        Assert.Equal(3, listBox.VisibleItemCount);
        Assert.Equal(3, listBox.TopIndex);
        Assert.Equal("Six", listBox.SelectedItem);
        Assert.Equal(20, listBox.SelectionHighlight.ScreenBounds.Height);
    }

    [Fact]
    public void ComboBoxWidget_UsesListSelectionAndClosesDropDown()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var comboBox = new ComboBoxWidget(
            host,
            view,
            new Rectangle(10, 20, 200, 32),
            ["Easy", "Normal", "Hard"]);

        comboBox.OpenDropDown();
        Assert.True(comboBox.IsDropDownOpen);

        comboBox.SelectedIndex = 1;

        Assert.Equal("Normal", comboBox.SelectedItem);
        Assert.False(comboBox.IsDropDownOpen);
    }

    [Fact]
    public void TextBoxWidget_SupportsCaretInsertionDeletionAndMaxLength()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var textBox = new TextBoxWidget(
            host,
            view,
            new Rectangle(10, 20, 220, 32),
            text: "ac");

        textBox.CaretIndex = 1;
        textBox.InsertText("b");

        Assert.Equal("abc", textBox.Text);
        Assert.Equal(2, textBox.CaretIndex);

        textBox.Backspace();
        Assert.Equal("ac", textBox.Text);
        Assert.Equal(1, textBox.CaretIndex);

        textBox.Delete();
        Assert.Equal("a", textBox.Text);

        textBox.MaxLength = 3;
        textBox.InsertText("bcdef");
        Assert.Equal("abc", textBox.Text);
    }

    [Fact]
    public void ConversationBox_UpdatesContentAndRaisesAdvanceRequest()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var conversation = new ConversationBox(
            host,
            view,
            new Rectangle(20, 300, 500, 140),
            "Guard",
            "Halt!");

        int advances = 0;
        conversation.AdvanceRequested += () => advances++;

        conversation.SetConversation("Guard", "You may pass.");
        conversation.Advance();

        Assert.Equal("Guard", conversation.Speaker);
        Assert.Equal("You may pass.", conversation.Text);
        Assert.Equal(1, advances);
    }

    [Fact]
    public void NameTagWidget_FollowsSpriteAndUpdatesText()
    {
        using var host = new TestRenderSurfaceHost();
        SceneLayer layer = AddLayer(host);
        Sprite sprite = CreateSprite(layer, new Vector2(2, 3));

        using var nameTag = new NameTagWidget(
            host,
            sprite,
            "Merchant",
            size: new Size(100, 24));

        Rectangle before = nameTag.BoundsWorld;
        nameTag.SetText("Shopkeeper");
        sprite.SetPosition(new Vector2(5, 6));

        Assert.Equal("Shopkeeper", nameTag.Text);
        Assert.NotEqual(before.Location, nameTag.BoundsWorld.Location);
        Assert.Equal(
            sprite.DrawLocationWorld.Left + (sprite.DrawLocationWorld.Width - 100) / 2,
            nameTag.BoundsWorld.Left);
    }

    public void Dispose()
    {
        foreach (Sprite sprite in _sprites)
        {
            if (SpriteManager.Instance._spriteList.Remove(sprite))
                sprite.DisposeImmediate();
        }
    }

    private static View AddView(TestRenderSurfaceHost host)
    {
        var bounds = new Rectangle(0, 0, 640, 480);
        int zOrder = host.ViewManager.Views.Count;

        host.ViewManager.AddView(bounds, zOrder: zOrder);

        return host.ViewManager.Views.Single(view =>
            view.ZOrder == zOrder &&
            view.Viewport.TargetRectPx == bounds);
    }

    private static SceneLayer AddLayer(TestRenderSurfaceHost host)
    {
        return host.Scene.AddLayer(
            columnCount: 10,
            rowCount: 10,
            width: 64,
            height: 64,
            zOrder: 0,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);
    }

    private Sprite CreateSprite(SceneLayer layer, Vector2 position)
    {
        Sprite sprite = SpriteManager.Instance.CreateSprite(layer, default);
        sprite.RenderSize = new Size(64, 64);
        sprite.SetPosition(position);
        _sprites.Add(sprite);
        return sprite;
    }
}
