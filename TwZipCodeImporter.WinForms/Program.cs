using System.Text;

namespace TwZipCodeImporter.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
