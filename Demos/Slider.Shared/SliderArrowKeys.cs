namespace Gondwana.Demos.Slider;

/// <summary>
/// Platform keyboard identifiers. Keep the numeric mapping in the host rather
/// than referencing WinForms or Blazor key enums from the shared runtime.
/// </summary>
internal readonly record struct SliderArrowKeys(
    int Left,
    int Right,
    int Up,
    int Down)
{
    /// <summary>Offsets point to the piece beside the empty square.</summary>
    internal bool TryGetOffset(string? key, out int columnOffset, out int rowOffset)
    {
        columnOffset = 0;
        rowOffset = 0;

        // Named identifiers also support hosts that register descriptive key names.
        switch (key)
        {
            case "Left":
            case "ArrowLeft":
                columnOffset = 1;
                return true;
            case "Right":
            case "ArrowRight":
                columnOffset = -1;
                return true;
            case "Up":
            case "ArrowUp":
                rowOffset = 1;
                return true;
            case "Down":
            case "ArrowDown":
                rowOffset = -1;
                return true;
        }

        // KeyboardEventPoller defaults to decimal key-code strings. WinForms and
        // browser key enums are not guaranteed to assign the same numbers.
        if (!int.TryParse(key, out int code))
            return false;

        if (code == Left)
            columnOffset = 1;
        else if (code == Right)
            columnOffset = -1;
        else if (code == Up)
            rowOffset = 1;
        else if (code == Down)
            rowOffset = -1;
        else
            return false;

        return true;
    }
}
