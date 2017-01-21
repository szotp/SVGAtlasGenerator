
namespace SVGAtlasGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = args.Length > 0 ? args[0] : ".";
            new DirectoryProcessor().Run(path, true);
        }
    }
}
