using System;
using System.IO;

namespace SVGAtlasGenerator
{
    /// <summary>
    /// Checks if atlas should be updated/created.
    /// </summary>
    class FileProcessor
    {
        bool ShouldUpdate(string data, string sheet, string svg)
        {
            if (File.Exists(data) && File.Exists(sheet))
            {
                var inputInfo = new FileInfo(svg);
                var outputInfo = new FileInfo(data);

                if (outputInfo.LastWriteTime > inputInfo.LastWriteTime)
                {
                    return false;
                }
            }

            return true;
        }

        public void ProcessFile(string path)
        {
            Console.WriteLine($"Checking {path}");

            var svg = Path.GetFullPath(path);
            var data = Path.ChangeExtension(svg, ".json");
            var sheet = Path.ChangeExtension(svg, ".png");

            if (!ShouldUpdate(data, sheet, svg))
            {
                return;
            }

            Console.WriteLine($"Updating...");

            var renderer = new BitmapRenderer();
            renderer.CreateAtlas(svg, "s_");

            Console.WriteLine($"Done.");
        }
    }
}
