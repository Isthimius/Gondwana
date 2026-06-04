using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gondwana.Drawing;

public struct Padding
{
    public Padding() { }

    public int Top { get; set; } = 0;
    public int Bottom { get; set; } = 0;
    public int Left { get; set; } = 0;
    public int Right { get; set; } = 0;
}
