using System;
using System.IO;

class Program
{
    static void Main()
    {
        string[] dirs = Directory.GetDirectories(@"c:\Scripting\ACECustom", "visualizer_cache", SearchOption.AllDirectories);
        foreach(var d in dirs)
        {
            try {
                Directory.Delete(d, true);
                Console.WriteLine("Deleted " + d);
            } catch (Exception ex) {
                Console.WriteLine("Failed to delete " + d + ": " + ex.Message);
            }
        }
    }
}
