using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;
using TempAnalysis.OpenSeesTranslator;

namespace TempAnalysis
{

    [DataContract(IsReference = true)]
    public class LoadCombinationFactors
    {
        [DataMember]
        public double D = 1;
        [DataMember]
        public double L = 1;
        [DataMember]
        public double E = 1;

        public LoadCombinationFactors()
        {

        }
        public LoadCombinationFactors(double d, double l, double e)
        {
            D = d;
            L = l;
            E = e;
        }
        public override string ToString()
        {
            return $"( {D} D, {L} L, {E} E)";
        }
    }

    [DataContract(IsReference = true)]
    [KnownType(typeof(GravityLoadCase))]
    [KnownType(typeof(ModalAnalysisLoadCase))]
    public abstract class LoadCase
    {
        [DataMember]
        public long ID;
        [DataMember]
        public bool Elastic;
        [DataMember]
        public List<LoadCombinationFactors> LoadCombinationsFactors = new List<LoadCombinationFactors>();

        public LoadCase()
        {

        }
        public LoadCase(IDsManager IDM,bool elastic)
        {
            ID = ++IDM.LastLoadCaseId;
            Elastic = elastic;
        }
        public void WriteFileCommand(StreamWriter file, string FileName)
        {
            file.WriteLine($"source {FileName};");
        }
        public void WriteMainFiles(StreamWriter file, OpenSeesTranslator.OpenSeesTranslator Translator)
        {
            WriteFileCommand(file, Translator.NodesFile);
            WriteFileCommand(file, Translator.MaterialsFile);
            WriteFileCommand(file, Translator.FrameElementsFile);
            WriteFileCommand(file, Translator.WallShellsFile);
            file.WriteLine("puts \"Model Defined\" ");
        }
        public abstract string GetName();
        public abstract void ReadResults(IModalAnalysisModel model, LoadCombinationFactors factors, OpenSeesTranslator.OpenSeesTranslator translator);
        public abstract void WriteAnalysisCommand(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator translator, LoadCombinationFactors factors);
        protected static bool ConvertLineToDoubleList(string line, int CheckCount, out List<double> doubleList)
        {
            doubleList = new List<double>();
            char[] whitespace = new char[] { ' ', '\t' };
            var strings = line.Split(whitespace).Where(x => !string.IsNullOrEmpty(x)).Select(s => double.Parse(s)).ToList();
            if (strings.Count != CheckCount)
                return false;

            foreach (var stringValue in strings)
            {
                doubleList.Add(stringValue);
            }
            return doubleList.Count == CheckCount;
        }

    }

    [DataContract(IsReference = true)]
    public class DriftAnalysis : LoadCase
    {
        public DriftAnalysis()
        {

        }
        public DriftAnalysis(IDsManager IDM) : base(IDM,true)
        {
            LoadCombinationsFactors = new List<LoadCombinationFactors>
            {
                new LoadCombinationFactors(1,0.5,1),
            };
        }
        public override string GetName() => "Drift Loads";

        public override void ReadResults(IModalAnalysisModel model, LoadCombinationFactors factors, OpenSeesTranslator.OpenSeesTranslator translator)
        {
            DetailedModel detailedModel = model as DetailedModel;
            if (detailedModel == null)
                return;
            List<List<double>> Displacements = ReadFloorsDisplacements(model, translator);
            int Steps =  Displacements.Count;
            double FloorHeight = detailedModel.LayoutUtility.FloorHeight;
            List<double> LastDisplacments = Displacements[Steps - 1];
            List<double> InterSotryDrifts = new List<double>();
            InterSotryDrifts.Add(LastDisplacments[0]);
            for (int i = 1; i < LastDisplacments.Count; i++)
            {
                InterSotryDrifts.Add(detailedModel.FloorForceAmplificationFactor *  detailedModel.ResponseParameters.Cd *(LastDisplacments[i] - LastDisplacments[i - 1]) / FloorHeight);
            }
            detailedModel.FloorDrifts = InterSotryDrifts;
        }
        private List<List<double>> ReadFloorsDisplacements(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator translator)
        {
            List<List<double>> Results = new List<List<double>>();

            string file = Path.Combine(translator.FolderPath, translator.FloorDisplacement);
            if (!File.Exists(file))
                return Results;

            int Count = model._NumOfFloors;
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs, Encoding.Default))
                {
                    List<double> doubleList = new List<double>();
                    string line = sr.ReadLine();
                    while (!string.IsNullOrEmpty(line) && ConvertLineToDoubleList(line, Count, out doubleList))
                    {
                        Results.Add(doubleList);
                        line = sr.ReadLine();
                        doubleList = new List<double>();
                    }
                    fs.Close();
                }
                fs.Close();
            }
            return Results;
        }
        public override void WriteAnalysisCommand(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator Translator, LoadCombinationFactors factors)
        {
            DetailedModel Model = model as DetailedModel;
            if (Model == null)
                return;
            Translator.WriteSurfacesLoads(Model, factors);
            string filePath = Path.Combine(Translator.FolderPath, Translator.AnalysisFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                WriteMainFiles(file, Translator);
                WriteFileCommand(file, Translator.GravityLoadsFile);
                file.WriteLine($"recorder Node -file {Translator.FloorDisplacement} -region {model._MasterNodesRegion.ID} -dof 1;");
                WriteGravityAnalysisCommands(file);
            }
        }
        private void WriteGravityAnalysisCommands(StreamWriter file)
        {
            file.WriteLine($"constraints Transformation;");
            file.WriteLine($"numberer RCM;");
            file.WriteLine($"system SparseSYM;");
            file.WriteLine($"test NormDispIncr 1.0e-4 2000 2;");
            file.WriteLine($"algorithm NewtonLineSearch 0.75;");
            file.WriteLine($"integrator LoadControl 0.2;");
            file.WriteLine($"analysis Static;");
            file.WriteLine($"analyze 5;");
        }
    }
    [DataContract(IsReference = true)]
    public class GravityLoadCase : LoadCase
    {
        public GravityLoadCase()
        {

        }
        public GravityLoadCase(IDsManager IDM) : base(IDM,true)
        {
            LoadCombinationsFactors = new List<LoadCombinationFactors>
            {
                new LoadCombinationFactors(1.4,0,0),
                new LoadCombinationFactors(1.2,1.6,0),
            };
        }
        public override string GetName() => "Gravity Loads";
        public override void ReadResults(IModalAnalysisModel model,LoadCombinationFactors factors, OpenSeesTranslator.OpenSeesTranslator translator)
        {
            DetailedModel Model = model as DetailedModel;
            if (Model == null)
                return;
            Model.FloorsGroups.ForEach(fg=> {
                fg.ReadGravityResults(translator.FolderPath, factors);
            });
        }
        public override void WriteAnalysisCommand(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator Translator, LoadCombinationFactors factors)
        {
            DetailedModel Model = model as DetailedModel;
            if (Model == null)
                return;
            Translator.WriteSurfacesLoads(Model, factors);
            string filePath = Path.Combine(Translator.FolderPath, Translator.AnalysisFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                WriteMainFiles(file,Translator);
                WriteFileCommand(file, Translator.GravityLoadsFile);
                Model.WriteGravityRecorders(file);
                WriteGravityAnalysisCommands(file);
            }
        }
        private void WriteGravityAnalysisCommands(StreamWriter file)
        {
            file.WriteLine($"constraints Transformation;");
            file.WriteLine($"numberer RCM;");
            file.WriteLine($"system SparseSYM;");
            file.WriteLine($"test NormDispIncr 1.0e-4 2000 2;");
            file.WriteLine($"algorithm NewtonLineSearch 0.75;");
            file.WriteLine($"integrator LoadControl 0.2;");
            file.WriteLine($"analysis Static;");
            file.WriteLine($"analyze 5;");
        }
    }

    [DataContract(IsReference = true)]
    public class ModalAnalysisLoadCase : LoadCase
    {
        public ModalAnalysisLoadCase()
        {

        }
        public ModalAnalysisLoadCase(IDsManager IDM, bool Elastic = true) : base(IDM, Elastic)
        {
            LoadCombinationsFactors = new List<LoadCombinationFactors>
            {
                new LoadCombinationFactors(1,1,0)
            };
        }
        public override string GetName() => "Modal Analysis";
        public override void ReadResults(IModalAnalysisModel model, LoadCombinationFactors factors, OpenSeesTranslator.OpenSeesTranslator translator)
        {
            ReadModalAnalysisReport(model, translator);
            ReadModesResults(model, translator);
            model._ModeShapes.ForEach(mode => mode.Normalize());
            model.PostCalculateFloorForces();
        }       
        private void ReadModesResults(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator Translator)
        {
            int incr =  3;
            int lineRecords = model._NumOfFloors * incr;
            foreach (var mode in model._ModeShapes)
            {
                string file = Path.Combine(Translator.FolderPath, string.Format("mode{0}.out", mode.Index));
                if (!File.Exists(file))
                    continue;
                List<double> Results = new List<double>();
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var sr = new StreamReader(fs, Encoding.Default))
                    {
                        string line = sr.ReadLine();
                        List<double> doubleList = new List<double>();
                        while (!string.IsNullOrEmpty(line) && ConvertLineToDoubleList(line, lineRecords, out doubleList))
                        {
                            Results.AddRange(doubleList);
                            doubleList.Clear();
                            line = sr.ReadLine();
                        }
                        sr.Close();
                    }
                    fs.Close();
                }
                for (int i = 0; i < Results.Count; i += incr)
                {
                    mode.X_values.Add(Results[i]);
                    mode.Y_values.Add(Results[i + 2]);
                }
                //mode.Normalize();
            }
        }
        private void ReadModalAnalysisReport(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator Translator)
        {
            int modeShapes = model.GetNumOfModeShapes();
            List<ModeShapeData> ModeShapes = new List<ModeShapeData>();
            for (int i = 0; i < modeShapes; i++)
            {
                ModeShapes.Add(new ModeShapeData(i + 1));
            }

            string filePath = Path.Combine(Translator.FolderPath, Translator.ModalReport);
            List<double> doubleList;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs, Encoding.Default))
                {
                    string line = "";
                    // read eigen analysis
                    line = sr.ReadLine();
                    line = sr.ReadLine();
                    line = sr.ReadLine();
                    int lineRecords = 5;
                    for (int i = 0; i < modeShapes; i++)
                    {
                        line = sr.ReadLine();
                        if (!ConvertLineToDoubleList(line, lineRecords, out doubleList))
                            return;

                        ModeShapes[i].Lambda = doubleList[1];
                        ModeShapes[i].Omega = doubleList[2];
                        ModeShapes[i].Frequency = doubleList[3];
                        ModeShapes[i].Period = doubleList[4];
                    }

                    // read totalmass
                    line = sr.ReadLine();
                    line = sr.ReadLine();

                    line = sr.ReadLine();
                    lineRecords = 3;
                    if (!ConvertLineToDoubleList(line, lineRecords, out doubleList))
                        return;

                    //modalData.SumMx = doubleList[0];
                    //modalData.SumMy = doubleList[1];
                    //modalData.SumMrz = doubleList[2];

                    // read mode mass
                    line = sr.ReadLine();
                    line = sr.ReadLine();
                    lineRecords = 4;
                    for (int i = 0; i < modeShapes; i++)
                    {
                        line = sr.ReadLine();
                        if (!ConvertLineToDoubleList(line, lineRecords, out doubleList))
                            return;
                        ModeShapes[i].Mx = doubleList[1];
                        ModeShapes[i].My = doubleList[3];
                    }
                    // read mode aculumlative mass 
                    line = sr.ReadLine();
                    line = sr.ReadLine();
                    for (int i = 0; i < modeShapes; i++)
                    {
                        line = sr.ReadLine();
                        if (!ConvertLineToDoubleList(line, lineRecords, out doubleList))
                            return;
                        ModeShapes[i].SumMx = doubleList[1];
                        ModeShapes[i].SumMy = doubleList[3];
                    }
                    sr.Close();
                }
                fs.Close();
            }

            model._ModeShapes = new List<ModeShapeData>();
            for (int i = 0; i < ModeShapes.Count; i++)
            {
                ModeShapeData mode = ModeShapes[i];
                if (Math.Abs(mode.Lambda) < 1e-9)
                    continue;
                if (!IsCountedModeShape(model, mode))
                    continue;
                model._ModeShapes.Add(mode);
                mode.Sa = model._ResponseParameters.GetSa(mode.Period);
                if (IsEnoughModeShapes(model))
                    break;
            }

        }
        public bool IsCountedModeShape(IModalAnalysisModel model, ModeShapeData mode)
        {
            double Tolerance = 1e-9;
            return (Math.Abs(mode.Mx) > Tolerance || Math.Abs(mode.My) > Tolerance);
        }
        public bool IsEnoughModeShapes(IModalAnalysisModel model)
        {
            double Tolerance = 1e-9;
            double Max = 90;
            if (model._ModeShapes.Count == 0)
                return false;
            ModeShapeData LastMode = model._ModeShapes.Last();

            if (Math.Abs(LastMode.SumMx - LastMode.SumMy) > Tolerance)
                return false;
            if (LastMode.SumMx < Max)
                return false;
            return true;
        }
        public override void WriteAnalysisCommand(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator Translator, LoadCombinationFactors factors)
        {
            string filePath = Path.Combine(Translator.FolderPath, Translator.AnalysisFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                WriteMainFiles(file, Translator);
                WriteModalAnalysisCommands(file,model, Translator);
            }
        }
        private void WriteModalAnalysisCommands(StreamWriter file, IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator Translator)
        {
            int modeShapes = model.GetNumOfModeShapes();
            file.WriteLine("constraints Transformation;");
            file.WriteLine("numberer RCM;");
            file.WriteLine("system UmfPack;");
            //file.WriteLine("system CuSP -rTol 1e-6 -mInt 100000;");
            file.WriteLine("test NormDispIncr 1e-5 100;");
            file.WriteLine("set algorithmType Newton;");
            file.WriteLine("for { set k 1 } { $k <= " + modeShapes + " } { incr k } {");
            file.WriteLine(string.Format("  recorder Node -file [format \"mode%i.out\" $k] -region {0} -dof {1} \"eigen $k\"", model._MasterNodesRegion.ID, "1 5 3"));
            file.WriteLine("}");
            file.WriteLine(string.Format("set num_modes {0};", modeShapes));
            file.WriteLine(string.Format("set filename {0};", Translator.ModalReport));
            file.WriteLine(AnalysisLibrary.Properties.Resources.ModalAnalysisScript3D);
        }
    }

    [DataContract(IsReference = true)]
    public class PushOverLoadCase : LoadCase
    {
        [DataMember]
        public double Power;
        public PushOverLoadCase()
        {

        }
        public PushOverLoadCase(IDsManager IDM, double Power) : base(IDM, false)
        {
            this.Power = Power;
            LoadCombinationsFactors = new List<LoadCombinationFactors>
            {
                new LoadCombinationFactors(1,1,0)
            };
        }
        public override string GetName() => "PushOver Analysis";
        public override void ReadResults(IModalAnalysisModel model, LoadCombinationFactors factors, OpenSeesTranslator.OpenSeesTranslator translator)
        {
            model._PushOverResults = new PushOverResults();
            model._PushOverResults.K = Power;
            List <List<double>> Displacements = ReadFloorsDisplacements(model,translator);
            List<double> Reactions = ReadBaseReaction(model, translator);
            model._PushOverResults.CurveList = new List<Utilities.Point2D>();
            model._PushOverResults.Displacements = new List<List<double>>();
            model._PushOverResults.InterSotryDrifts = new List<double>();
            int Steps = Math.Min(Reactions.Count, Displacements.Count);
            if (Steps == 0)
            { 
                return;
            }
            for (int i = 0; i < Steps; i++)
            {
                model._PushOverResults.CurveList.Add(new Utilities.Point2D(Displacements[i].Last(), Reactions[i]));
            }
            model._PushOverResults.Displacements = Displacements;
            model._PushOverResults.SetInterStoryDrifts(model.GetH()/model._NumOfFloors);
            model._PushOverResults.SetProperties();
        }
        private List<double> ReadBaseReaction(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator translator)
        {
            List<double> BaseReactions = new List<double>();

            string file = Path.Combine(translator.FolderPath, translator.BaseReaction);
            if (!File.Exists(file))
                return BaseReactions;
            int Count = (int)(model._BaseNodesRegion.EndIndex - model._BaseNodesRegion.StartIndex +1);

            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs, Encoding.Default))
                {
                    List<double> doubleList = new List<double>();
                    string line = sr.ReadLine();
                    while (!string.IsNullOrEmpty(line) && ConvertLineToDoubleList(line, Count, out doubleList))
                    {
                        BaseReactions.Add(Math.Abs(doubleList.Sum()));
                        line = sr.ReadLine();
                        doubleList = new List<double>();
                    }
                    fs.Close();
                }
                fs.Close();
            }
            return BaseReactions;
        }
        private List<List<double>> ReadFloorsDisplacements(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator translator)
        {
            List<List<double>> Results = new List<List<double>>();

            string file = Path.Combine(translator.FolderPath, translator.FloorDisplacement);
            if (!File.Exists(file))
                return Results;

            int Count = model._NumOfFloors;
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs, Encoding.Default))
                {
                    List<double> doubleList = new List<double>();
                    string line = sr.ReadLine();
                    while (!string.IsNullOrEmpty(line) && ConvertLineToDoubleList(line, Count, out doubleList))
                    {
                        Results.Add(doubleList);
                        line = sr.ReadLine();
                        doubleList = new List<double>();
                    }
                    fs.Close();
                }
                fs.Close();
            } 
            return Results;
        }
        public override void WriteAnalysisCommand(IModalAnalysisModel model, OpenSeesTranslator.OpenSeesTranslator Translator, LoadCombinationFactors factors)
        {
            string filePath = Path.Combine(Translator.FolderPath, Translator.AnalysisFile);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                WriteMainFiles(file, Translator);
                WritePushOverRecords(model,file,Translator);
                WritepushOverAnalysisCommand(model,file);
                file.Close();
            }
        }
        private void WritepushOverAnalysisCommand(IModalAnalysisModel model,StreamWriter file)
        {
            long Top = model._MasterNodesRegion.EndIndex;
            long bottom = model._MasterNodesRegion.StartIndex;
            long count = (Top - bottom) + 1;
            double Height = model.GetH();
            double FloorHeight = Height / count;
            string Loads = "0.0 0.0 0.0 0.0 0.0";
            file.WriteLine("# PUSH OVER analysis ------------------------------");
            file.WriteLine("pattern Plain 2 Linear {");
            if (Power >= 0)
            {
                for (long i = bottom; i <= Top; i++)
                { 
                    double H = ((i - bottom) + 1) * FloorHeight;
                    file.WriteLine($"load {i} {1.0 * Math.Pow(H / Height, Power)} {Loads};");
                }
            }
            else
            {
                ModeShapeData mode = model._ModeShapes.OrderByDescending(x=> x.Mx).FirstOrDefault();
                List<double> MFs = model.GetModalForces(new List<ModeShapeData> { mode });
                for (long i = bottom; i <= Top; i++)
                {
                    int index = (int)(i -bottom);
                    file.WriteLine($"load {i} {0.1 * MFs[index]} {Loads};");
                }
            }
            
            file.WriteLine("}");
            file.WriteLine("# pushover: diplacement controlled static analysis");
            file.WriteLine("constraints Transformation;");
            file.WriteLine("numberer RCM;");
            file.WriteLine("system UmfPack;");
            //file.WriteLine("system CuSP -rTol 1e-6 -mInt 100000;");
            file.WriteLine("test NormDispIncr 1e-3 100;");
            file.WriteLine("set algorithmType Newton;");
            file.WriteLine("algorithm $algorithmType;");
            file.WriteLine("set IDctrlDOF 1;");
            //double DriftRatio = Math.Min(0.04 + (model._NumOfFloors-10) * 0.001, 0.1);
            double DriftRatio = (3.0 + (model._NumOfFloors)/10)/100.0;
            file.WriteLine(string.Format("set Dmax double({0});", Height * DriftRatio));
            file.WriteLine(string.Format("set Dincr [ expr $Dmax/{0} ]", (600 + DriftRatio * 1.0E+4) ) );
            file.WriteLine(string.Format("set IDctrlNode {0};", model.GetRoofNodeID()));
            file.WriteLine("integrator DisplacementControl $IDctrlNode $IDctrlDOF $Dincr;");
            file.WriteLine("analysis Static;");
            file.WriteLine("set Nsteps [expr int($Dmax /$Dincr)];");
            file.WriteLine("set ok [analyze $Nsteps];");
            file.WriteLine(AnalysisLibrary.Properties.Resources.RunMethods);
        }
        private void WritePushOverRecords(IModalAnalysisModel model, StreamWriter file, OpenSeesTranslator.OpenSeesTranslator Translator)
        {
            file.WriteLine($"recorder Node -file {Translator.FloorDisplacement} -region {model._MasterNodesRegion.ID} -dof 1 disp;");
            file.WriteLine($"recorder Node -file {Translator.BaseReaction} -region {model._BaseNodesRegion.ID} -dof 1 reaction;");
        }
    }
}
