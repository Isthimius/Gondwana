using System.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers;

public sealed class AsepriteImporter : ExternalAssetImporter
{
    public override string Id => "aseprite";
    public override string DisplayName => "Aseprite (.ase/.aseprite)";
    public override IReadOnlyList<string> SupportedExtensions => [".ase", ".aseprite"];

    protected override void BuildPlan(ImportPlan plan, CancellationToken token)
    {
        using var source = File.OpenRead(plan.Request.SourcePath);
        var sprite = AsepriteFile.Read(source, plan.Report, token);
        string name = ImportNaming.Sanitize(Path.GetFileNameWithoutExtension(plan.Request.SourcePath));
        int columns = Math.Min(sprite.Frames.Count, Math.Max(1, 4096 / sprite.Width));
        int rows = (sprite.Frames.Count + columns - 1) / columns;
        if ((long)rows * sprite.Height > 16384 || sprite.Width > 16384) throw new InvalidDataException("Generated atlas exceeds the 16384-pixel dimension limit.");
        using var atlas = new SKBitmap(columns * sprite.Width, rows * sprite.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        atlas.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(atlas))
            for (int i = 0; i < sprite.Frames.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                using var frame = RenderFrame(sprite, i, token);
                canvas.DrawBitmap(frame, (i % columns) * sprite.Width, (i / columns) * sprite.Height);
            }
        using var png = atlas.Encode(SKEncodedImageFormat.Png, 100);
        plan.Add(name + ".atlas.png", "PNG", null, png.ToArray());
        var gts = new TilesheetDefinition { Name = name, Image = new() { FilePath = name + ".atlas.png" } };
        for (int row = 0; row < rows; row++)
            gts.Regions.Add(new() { Name = $"row-{row}", Area = new Rectangle(0, row * sprite.Height, Math.Min(columns, sprite.Frames.Count - row * columns) * sprite.Width, sprite.Height), TileSize = new Size(sprite.Width, sprite.Height) });
        plan.Add(name + ".gts", gts);
        IEnumerable<AsepriteTag> tags = sprite.Tags.Count == 0 ? [new("all", 0, sprite.Frames.Count - 1, 0, 0)] : sprite.Tags;
        foreach (var tag in tags)
        {
            string key = ImportNaming.Sanitize(tag.Name);
            var sequence = Enumerable.Range(tag.From, tag.To - tag.From + 1).ToList();
            if (tag.Direction is 1 or 3) sequence.Reverse();
            var type = tag.Direction >= 2 ? CycleType.PingPong : CycleType.Repeating;
            if (tag.Repeat > 0)
            {
                if ((long)sequence.Count * tag.Repeat > 100000) throw new InvalidDataException("Finite tag expansion exceeds 100000 frames.");
                var expanded = new List<int>();
                for (int repetition = 0; repetition < tag.Repeat; repetition++)
                {
                    var pass = tag.Direction >= 2 && repetition % 2 != 0 ? sequence.AsEnumerable().Reverse() : sequence;
                    expanded.AddRange(tag.Direction >= 2 && repetition > 0 && sequence.Count > 1 ? pass.Skip(1) : pass);
                }
                sequence = expanded; type = CycleType.Simple;
            }
            var gani = new AnimationDefinition { Key = name + "." + key, CycleType = type,
                TilesheetSources = [AnimationTilesheetSourceDefinition.Loose(name, name + ".gts")] };
            foreach (int index in sequence)
                gani.Frames.Add(new() { Tilesheet = name, RegionName = $"row-{index / columns}", XTile = index % columns, YTile = 0, DurationSeconds = sprite.Frames[index].DurationMilliseconds / 1000.0 });
            plan.Add(name + "-" + key + ".gani", gani);
        }
    }

    public static SKBitmap RenderFrame(AsepriteFile sprite, int frameIndex, CancellationToken token = default)
    {
        var parents = new int[sprite.Layers.Count];
        var stack = new List<int>();
        for (int i = 0; i < sprite.Layers.Count; i++)
        {
            var layer = sprite.Layers[i];
            while (stack.Count > layer.Level) stack.RemoveAt(stack.Count - 1);
            if (stack.Count != layer.Level) throw new InvalidDataException("Invalid Aseprite group hierarchy.");
            parents[i] = stack.Count == 0 ? -1 : stack[^1];
            if (layer.Type == 1) stack.Add(i);
        }
        var cels = sprite.Frames[frameIndex].Cels.ToDictionary(c => c.Layer);
        if (cels.Keys.Any(i => i < 0 || i >= sprite.Layers.Count)) throw new InvalidDataException("Cel references an unknown layer.");
        AsepriteCel Resolve(AsepriteCel cel)
        {
            int current = frameIndex;
            var seen = new HashSet<int>();
            while (cel.LinkedFrame is { } link)
            {
                if (link < 0 || link >= sprite.Frames.Count || !seen.Add(link)) throw new InvalidDataException("Invalid or cyclic linked cel.");
                current = link;
                cel = sprite.Frames[current].Cels.SingleOrDefault(c => c.Layer == cel.Layer)
                    ?? throw new InvalidDataException("Linked cel target is missing.");
            }
            return cel;
        }
        SKBitmap RenderGroup(int parent)
        {
            var bitmap = new SKBitmap(sprite.Width, sprite.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            try
            {
                bitmap.Erase(SKColors.Transparent);
                using var canvas = new SKCanvas(bitmap);
                var children = Enumerable.Range(0, parents.Length).Where(i => parents[i] == parent)
                    .OrderBy(i => i + (cels.TryGetValue(i, out var c) ? c.Z : 0))
                    .ThenBy(i => cels.TryGetValue(i, out var c) ? c.Z : 0);
                foreach (int i in children)
                {
                    token.ThrowIfCancellationRequested();
                    var layer = sprite.Layers[i];
                    if ((layer.Flags & 1) == 0) continue;
                    if (layer.Type == 1)
                    {
                        using var group = RenderGroup(i);
                        using var paint = new SKPaint { Color = SKColors.White.WithAlpha(layer.Opacity) };
                        canvas.DrawBitmap(group, 0, 0, paint);
                        continue;
                    }
                    if (!cels.TryGetValue(i, out var cel)) continue;
                    var image = Resolve(cel);
                    if (image.Width == 0 || image.Height == 0) continue;
                    using var pixels = new SKBitmap(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                    int bpp = sprite.Depth / 8;
                    for (int y = 0; y < image.Height; y++)
                    {
                        token.ThrowIfCancellationRequested();
                        for (int x = 0; x < image.Width; x++)
                        {
                            int p = (y * image.Width + x) * bpp;
                            var bytes = image.Pixels;
                            SKColor color = sprite.Depth switch
                            {
                                32 => new(bytes[p], bytes[p + 1], bytes[p + 2], bytes[p + 3]),
                                16 => new(bytes[p], bytes[p], bytes[p], bytes[p + 1]),
                                _ => bytes[p] == sprite.TransparentIndex && (layer.Flags & 8) == 0
                                    ? SKColors.Transparent : sprite.Frames[frameIndex].Palette[bytes[p]]
                            };
                            pixels.SetPixel(x, y, color);
                        }
                    }
                    using var celPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)((cel.Opacity * layer.Opacity + 127) / 255)) };
                    canvas.DrawBitmap(pixels, cel.X, cel.Y, celPaint);
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
        return RenderGroup(-1);
    }
}
