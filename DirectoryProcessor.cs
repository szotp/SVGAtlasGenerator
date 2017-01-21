using System;
using System.IO;
using System.Windows.Forms;

namespace SVGAtlasGenerator
{
    /// <summary>
    /// Finds SVG files in directory and directs them to the FileProcessor.
    /// </summary>
    class DirectoryProcessor
    {
        public FileProcessor fileProcessor = new FileProcessor();

        void Observe(string directory)
        {
            directory = Path.GetFullPath(directory);
            var watcher = new FileSystemWatcher(directory, "*.svg");
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.LastWrite;

            while(true)
            {
                var result = watcher.WaitForChanged(WatcherChangeTypes.Changed);

                Console.WriteLine($"Received {result.Name}");
                fileProcessor.ProcessFile(result.Name);
                Console.WriteLine("Observing...");
            }

            
        }

        public void Run(string arg, bool observe)
        {

            if (Directory.Exists(arg))
            {
                var files = Directory.GetFiles(arg, "*.svg", SearchOption.AllDirectories);
                foreach (var item in files)
                {
                    fileProcessor.ProcessFile(item);
                }

                if(observe)
                {
                    Observe(arg);
                }
            }
            else if (File.Exists(arg))
            {
                fileProcessor.ProcessFile(arg);
            }
            else
            {
                Console.WriteLine("Error!");
            }
        }
    }
}
