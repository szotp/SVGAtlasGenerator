using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Svg;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace SVGAtlasGenerator
{
    class BitmapRenderer
    {
        /// <summary>
        /// Downloaded from http://stackoverflow.com/questions/4820212/automatically-trim-a-bitmap-to-minimum-size
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        Bitmap TrimBitmap(Bitmap source)
        {
            Rectangle srcRect = default(Rectangle);
            BitmapData data = null;
            try
            {
                data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] buffer = new byte[data.Height * data.Stride];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                int xMin = int.MaxValue,
                    xMax = int.MinValue,
                    yMin = int.MaxValue,
                    yMax = int.MinValue;

                bool foundPixel = false;

                // Find xMin
                for (int x = 0; x < data.Width; x++)
                {
                    bool stop = false;
                    for (int y = 0; y < data.Height; y++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            xMin = x;
                            stop = true;
                            foundPixel = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Image is empty...
                if (!foundPixel)
                    return null;

                // Find yMin
                for (int y = 0; y < data.Height; y++)
                {
                    bool stop = false;
                    for (int x = xMin; x < data.Width; x++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            yMin = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find xMax
                for (int x = data.Width - 1; x >= xMin; x--)
                {
                    bool stop = false;
                    for (int y = yMin; y < data.Height; y++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            xMax = x;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find yMax
                for (int y = data.Height - 1; y >= yMin; y--)
                {
                    bool stop = false;
                    for (int x = xMin; x <= xMax; x++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            yMax = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                xMax += 1;
                yMax += 1;
                srcRect = Rectangle.FromLTRB(xMin, yMin, xMax, yMax);
            }
            finally
            {
                if (data != null)
                    source.UnlockBits(data);
            }

            Bitmap dest = new Bitmap(srcRect.Width, srcRect.Height);
            Rectangle destRect = new Rectangle(0, 0, srcRect.Width, srcRect.Height);
            using (Graphics graphics = Graphics.FromImage(dest))
            {
                graphics.DrawImage(source, destRect, srcRect, GraphicsUnit.Pixel);
            }
            return dest;
        }

        /// <summary>
        /// Generate atlas .json and .png for all objects in the .svg where their identifier starts with the provided prefix.
        /// </summary>
        /// <param name="svg"></param>
        /// <param name="prefix"></param>
        public void CreateAtlas(string svg, string prefix)
        {
            SvgDocument document = null;
            do
            {
                try
                {
                    document = SvgDocument.Open(svg);
                }
                catch
                {
                    
                    Console.WriteLine("Crash!");
                    Thread.Sleep(1000);
                }
            } while (document == null);

            var children = document.Children.SelectMany(x => x.Children).Where(x => x.ID != null && x.ID.StartsWith(prefix)).OfType<SvgVisualElement>();

            var size = document.GetDimensions();
            var bitmap = new Bitmap((int)size.Width, (int)size.Height);
            var graphics = Graphics.FromImage(bitmap);
            var renderer = SvgRenderer.FromGraphics(graphics);

            var sheetBitmap = new Bitmap(1024, 1024);
            var sheetGraphics = Graphics.FromImage(sheetBitmap);


            var rects = new MaxRectsBinPack(1024, 1024, false);
            var atlas = new AtlasDefinition();

            var margin = 1;

            foreach (var item in children)
            {
                graphics.Clear(Color.Transparent);
                item.RenderElement(renderer);

                var trimmed = TrimBitmap(bitmap);
                var rectangle = rects.Insert(trimmed.Width+ margin, trimmed.Height+ margin, MaxRectsBinPack.FreeRectChoiceHeuristic.RectBestAreaFit);
                rectangle.width -= margin;
                rectangle.height -= margin;

                sheetGraphics.DrawImage(trimmed, new Point((int)rectangle.x, (int)rectangle.y));

                var rect = new AtlasDefinition.Rect()
                {
                    x = (int)rectangle.x,
                    y = (int)rectangle.y,
                    w = (int)rectangle.width,
                    h = (int)rectangle.height
                };
                atlas.Add(item.ID + ".png", rect);
            }

            var pngPath = Path.ChangeExtension(svg, "png");

            var finalSheet = TrimBitmap(sheetBitmap);
            finalSheet.Save(Path.ChangeExtension(svg, "png"));

            atlas.SetMeta(Path.GetFileName(pngPath), finalSheet.Width, finalSheet.Height);
            atlas.Save(svg);
        }
    }
}


public class AtlasDefinition
{
    public struct Rect
    {
        public int x, y, w, h;
    }

    public struct Size
    {
        public int w, h;
    }

    public class FrameDefinition
    {
        public bool Rotated;
        public bool Timmed;
        public Rect Frame;
        public Rect SpriteSourceSize;
        public Size SourceSize;
    }

    public class MetaDefinition
    {
        public string App = "SVGAtlasGenerator";
        public string Version = "0.1";
        public string Image;
        public string Format = "RGBA8888";
        public Size Size = new Size();
        public float Scale = 1;
    }

    public Dictionary<string, FrameDefinition> Frames = new Dictionary<string, FrameDefinition>();
    public MetaDefinition Meta = new MetaDefinition();

    public void Add(string name, Rect rect)
    {
        var frame = new FrameDefinition();
        frame.Frame = rect;
        frame.SpriteSourceSize = new Rect() { w = rect.w, h = rect.h };
        frame.SourceSize = new Size() { w = rect.w, h = rect.h };
        Frames[name] = frame;
    }

    public void SetMeta(string name, int width, int height)
    {
        Meta.Image = name;
        Meta.Size = new Size()
        {
            w = width,
            h = height
        };
    }

    public void Save(string path)
    {
        var settings = new JsonSerializerSettings();
        settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        var jsonData = JsonConvert.SerializeObject(this, Formatting.Indented, settings);
        var jsonPath = Path.ChangeExtension(path, "json");
        File.WriteAllText(jsonPath, jsonData);
    }
}