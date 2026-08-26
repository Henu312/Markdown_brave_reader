using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var extensionDir = Path.Combine(root, "brave-markdown-reader", "icons");
Directory.CreateDirectory(extensionDir);
using var icon = Draw(256);
foreach (var size in new[] { 16, 32, 48, 128, 256 })
{
    using var bitmap = Draw(size);
    bitmap.Save(Path.Combine(extensionDir, $"icon{size}.png"), ImageFormat.Png);
}
using var handle = Draw(256).GetHicon() is var h ? Icon.FromHandle(h) : throw new InvalidOperationException();
using var stream = File.Create(Path.Combine(root, "md-launcher", "app.ico"));
handle.Save(stream);

static Bitmap Draw(int size)
{
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.Clear(Color.Transparent);
    var pad = size * 0.10f;
    var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);
    using var shadow = new SolidBrush(Color.FromArgb(35, 0, 30, 80));
    graphics.FillRoundedRectangle(shadow, new RectangleF(rect.X + size * .025f, rect.Y + size * .035f, rect.Width, rect.Height), size * .12f);
    using var blue = new SolidBrush(Color.FromArgb(36, 103, 218));
    graphics.FillRoundedRectangle(blue, rect, size * .12f);
    var fold = size * .25f;
    using var foldBrush = new SolidBrush(Color.FromArgb(111, 170, 255));
    graphics.FillPolygon(foldBrush, new[] { new PointF(rect.Right - fold, rect.Top), new PointF(rect.Right, rect.Top + fold), new PointF(rect.Right - fold, rect.Top + fold) });
    using var white = new SolidBrush(Color.White);
    using var font = new Font("Segoe UI", size * .40f, FontStyle.Bold, GraphicsUnit.Pixel);
    var text = "M";
    var measured = graphics.MeasureString(text, font);
    graphics.DrawString(text, font, white, rect.X + (rect.Width - measured.Width) / 2, rect.Y + rect.Height * .33f);
    return bitmap;
}

static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF rectangle, float radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
