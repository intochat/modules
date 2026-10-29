namespace IntoChat.OS.Modules.Flutter.ImageCanvas;

public sealed record ImagePoint(double X, double Y);
public sealed record CropRect(int X, int Y, int Width, int Height);
public sealed record PenStroke(uint ColorArgb, double Width, ImagePoint[] Points);
public sealed record ImageRecipe
{
    public CropRect? Crop { get; init; }
    public PenStroke[] Strokes { get; init; } = [];
    public void Validate(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 40_000_000) { throw new ArgumentException("Image dimensions exceed the supported limit."); }
        if (Crop is { } c && (c.X < 0 || c.Y < 0 || c.Width <= 0 || c.Height <= 0 || (long)c.X + c.Width > width || (long)c.Y + c.Height > height)) { throw new ArgumentException("Crop must be inside the image."); }
        if (Strokes.Length > 10000 || Strokes.Sum(s => (long)s.Points.Length) > 500000) { throw new ArgumentException("Drawing limit reached."); }
        foreach (var s in Strokes)
        {
            if (!double.IsFinite(s.Width) || s.Width <= 0 || s.Width > 500 || s.Points.Length == 0) { throw new ArgumentException("Invalid pen stroke."); }
            if (s.Points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X < 0 || p.Y < 0 || p.X > width || p.Y > height)) { throw new ArgumentException("Pen points must be inside the image."); }
        }
    }
}
