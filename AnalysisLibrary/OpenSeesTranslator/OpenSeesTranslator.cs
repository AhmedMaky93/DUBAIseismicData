using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;

namespace TempAnalysis.OpenSeesTranslator
{
    [DataContract(IsReference =true)]
    public class OpenSeesTranslator
    {
        [DataMember]
        public string InputFile = "Input.tcl";
        [DataMember]
        public string NodesFile = "Nodes.tcl";
        [DataMember]
        public string MaterialsFile = "Materials.tcl";
        [DataMember]
        public string FrameElementsFile = "FrameElements.tcl";
        [DataMember]
        public string WallShellsFile = "WallShells.tcl";
        [DataMember]
        public string GravityLoadsFile = "GravityLoads.tcl";
        [DataMember]
        public string ModalReport = "Report.txt";
        [DataMember]
        public string AnalysisFile = "Analysis.tcl";
        [DataMember]
        public string FloorDisplacement = "FloorDisplacement.out";
        [DataMember]
        public string BaseReaction = "BaseReaction.out";
        [DataMember]
        public string FolderPath;

        public OpenSeesTranslator()
        {

        }
        public void ClearAnalysisFolder(params string[] filesNames)
        {
            foreach (string fileName in filesNames)
            {
                string filePath = Directory.GetFiles(FolderPath).FirstOrDefault(x => x.Contains(fileName));
                if (File.Exists(filePath))
                { 
                    File.Delete(filePath);
                }
            }
        }
        public void ClearAnalysisFolder()
        {
            foreach(string fileName in Directory.GetFiles(FolderPath).Where(x => x.EndsWith(".tcl") || x.EndsWith(".out") || x.EndsWith(".txt")))
            {
                File.Delete(fileName);
            }
        }

        public void WriteModel(IModalAnalysisModel model, bool Elastic)
        {
            string filePath = Path.Combine(FolderPath, NodesFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                model.WriteNodes(file);
                file.Close();
            }
            filePath = Path.Combine(FolderPath, MaterialsFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                model.WriteMateials(file);
                file.Close();
            }
            filePath = Path.Combine(FolderPath, FrameElementsFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                model.WriteFrameElements(file, Elastic);
                file.Close();
            }
            filePath = Path.Combine(FolderPath, WallShellsFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                model.WriteWallsElements(file, Elastic);
                file.Close();
            }
        }
        
        public bool Run(string FinalFile)
        {
            try
            {
                string exepath = Assembly.GetExecutingAssembly().Location;
                string mainDirectory = Directory.GetDirectoryRoot(exepath);
                string folderPath = Path.Combine(Path.Combine(Path.GetDirectoryName(exepath), "OpenSees"), "bin");
                List<string> commands = new List<string>();
                commands.Add(exepath);
                commands.Add(string.Format("cd {0}", FolderPath));
                commands.Add(string.Format("OpenSees.exe {0}", FinalFile));
                commands.Add("C:");

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "cmd.exe";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = false;
                info.RedirectStandardError = false;

                using (Process p = Process.Start(info))
                {
                    commands.ForEach(x =>
                    {
                        p.StandardInput.WriteLine(x);
                        p.StandardInput.Flush();
                    });
                    p.StandardInput.Close();
                    p.WaitForExit();
                    p.Close();
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        public void WriteSurfacesLoads(IModalAnalysisModel model, LoadCombinationFactors factors)
        {
            string filePath = Path.Combine(FolderPath, GravityLoadsFile);
            using (StreamWriter file = new StreamWriter(filePath,false))
            {
                model.WriteLoadSurfacesCommands(file, factors);
            }
        }
        public void RunModel(IModalAnalysisModel model, LoadCase loadsCase)
        {
            ClearAnalysisFolder();
            WriteModel(model, loadsCase.Elastic);
            loadsCase.LoadCombinationsFactors.ForEach(factors => {
                loadsCase.WriteAnalysisCommand(model, this, factors);
                Project.LogAction($"{model._Name}: {loadsCase.GetName()} {factors} - Start OpenSees.");
                if (Run(AnalysisFile))
                {
                    Project.LogAction($"{model._Name}: {loadsCase.GetName()} {factors} - OpenSees closed.");
                    loadsCase.ReadResults(model, factors, this);
                }
            });
        }
        
    }
}
