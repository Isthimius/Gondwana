namespace Gondwana.Widgets.Menus;

/// <summary>
/// Associates one top-level menu header with its dropdown.
/// </summary>
public sealed class MenuBarMenu
{
    internal MenuBarMenu(MenuHeaderWidget header,
                         MenuDropDownWidget dropDown)
    {
        Header = header;
        DropDown = dropDown;
    }

    /// <summary>Gets the top-level menu header.</summary>
    public MenuHeaderWidget Header { get; }

    /// <summary>Gets the dropdown owned by the header.</summary>
    public MenuDropDownWidget DropDown { get; }
}
