using System;
using System.Diagnostics;
using TempAnalysis.Models;

namespace TempAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello Thesis!");
            try
            {
                bool RunFromStart = false;
                bool TestSet = false;
                Project pro = Project.GetIntance(RunFromStart);

                var timer = new Stopwatch();
                timer.Start();
                pro.Run(RunFromStart, TestSet);
                timer.Stop();
                Console.WriteLine("Project Saved.");
                Console.WriteLine($"Accomplished in :{ timer.Elapsed.ToString()}");
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Environment.Exit(0);
            }

        }
    }
}
