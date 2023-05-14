using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GISChapter
{
    class Program
    {
        static void Main(string[] args)
        {
            bool RunFromStart = false;
            GISProject pro = GISProject.GetIntance(RunFromStart);
            Stopwatch timer = new Stopwatch();
            timer.Start();
            pro.Run(RunFromStart);
            timer.Stop();
            Console.WriteLine("Project Saved.");
            Console.WriteLine($"Accomplished in :{ timer.Elapsed}");
        }
    }
}
