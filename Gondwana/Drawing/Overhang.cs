namespace Gondwana.Drawing;

public record struct Overhang(int Left, int Top, int Right, int Bottom)
{
    public static readonly Overhang None = new(0, 0, 0, 0);
    public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}
