using System;
using System.Windows.Forms;

namespace Prompter
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args) // Capture command-line arguments here
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Pass the command-line arguments to the Form1 constructor
            Application.Run(new Form1(args));
        }
    }
}
