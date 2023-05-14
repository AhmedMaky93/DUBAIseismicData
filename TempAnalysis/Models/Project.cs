using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using TempAnalysis.Models.ModelMaterial;
using TempAnalysis.Utilities;

namespace TempAnalysis.Models
{
    public enum ModelState
    {
        DesignGravityLoads,
        ShearWallLengthCalculate,
        Estimate,
        WriteSections,
        StartSimpling,
        PreSimpling,
        ExportFinalTclFinalForR2D
    }

    [DataContract(IsReference = true)]
    public class Project
    {
        [DataMember]
        public ModelState State;
        [DataMember]
        private List<ConCreteMaterial> _concreteMaterials = new List<ConCreteMaterial>();
        [DataMember]
        private List<SteelMaterial> _steelMaterials = new List<SteelMaterial>();
        [DataMember]
        private List<DetailedModel> _models = new List<DetailedModel>();
        [DataMember]
        private List<CalibrationUnit> _calibrationUnits = new List<CalibrationUnit>();
        [DataMember]
        public List<Point2D> Alhpa_TRatio = new List<Point2D>();
        [DataMember]
        public List<Point2D> Alhpa_Sigma1 = new List<Point2D>();
        public List<DetailedModel> Models
        {
            get { return _models; }
            set { _models = value; }
        }
        public Project()
        {

        }
        public static Project GetIntance(bool FromStart)
        {
            if (FromStart)
                return new Project();
            try
            {
                var savingPath = GetPath();
                if (File.Exists(savingPath))
                {
                    FileStream fs = new FileStream(savingPath, FileMode.OpenOrCreate);
                    XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, new XmlDictionaryReaderQuotas());
                    DataContractSerializer ser = new DataContractSerializer(typeof(Project));
                    Project project = (ser.ReadObject(reader) as Project);
                    reader.Close();
                    return project;
                }
                return new Project();
            }
            catch (Exception ex)
            {
                return new Project();
            }
            
        }
        internal void Serialize()
        {
            var path = GetPath();
            FileStream fs = new FileStream(path, FileMode.Create);
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(fs);
            DataContractSerializer ser =
                new DataContractSerializer(typeof(Project));
            ser.WriteObject(writer, this);
            writer.Close();
            fs.Close();
        }
        public static string GetDirectoryPath()
        {
            return Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location)?.FullName;
        }
        public static string GetFolderPath()
        {
            return Path.Combine(GetDirectoryPath(), "Models");
        }
        public static string GetPath()
        {
            return Path.Combine(GetDirectoryPath(), "Project.AMM");
        }
        internal void DesignColumnsForGravityLoads(string folderpath, bool TestSet)
        {
            Console.WriteLine("Start Creating Models");

            DirectoryInfo dir = new DirectoryInfo(folderpath);
            if (dir.Exists)
                dir.Delete(true);
            dir.Create();

            DefineMaterials(TestSet);
            DefineModels(TestSet);
            Task[] modelsTasks = new Task[_models.Count];
            for (int i = 0; i < _models.Count; i++)
            {
                modelsTasks[i] = _models[i].DesignColumnsForGravityLoads(folderpath, TestSet);
            }
            Task.WaitAll(modelsTasks);
            this.State = ModelState.ShearWallLengthCalculate;
        }
        private void CalculateShearWallLengthes(string folderpath,bool TestSet)
        {
            Task[] modelsTasks = new Task[_models.Count];
            for (int i = 0; i < _models.Count; i++)
            {
                modelsTasks[i] = _models[i].CalculateShearWallLengthes(folderpath, TestSet);
            }
            Task.WaitAll(modelsTasks);
            SetState(ModelState.Estimate);
        }
        protected void SetState(ModelState modelState)
        {
            State = modelState;
            Serialize();
        }
        internal async void Run(bool FromStart, bool TestSet)
        {
            if (FromStart)
                SetState(ModelState.DesignGravityLoads);
            string folderpath = GetFolderPath();
            if (State == ModelState.DesignGravityLoads)
                DesignColumnsForGravityLoads(folderpath,TestSet);
            if (State == ModelState.ShearWallLengthCalculate)
                CalculateShearWallLengthes(folderpath, TestSet);
            if (State == ModelState.Estimate)
                Estimate();
            if (State == ModelState.WriteSections)
                WriteSections(folderpath);
            if (State == ModelState.PreSimpling)
                PreSimplingModels(folderpath,TestSet);
            if (TestSet)
                return;
            if (State == ModelState.StartSimpling || State == ModelState.ExportFinalTclFinalForR2D)
                SimplfyingModels(folderpath);
            if (State == ModelState.ExportFinalTclFinalForR2D)
                ExportModelsToTcl(folderpath);
        }
        private void ExportModelsToTcl(string folderpath)
        {
        }
        private void PreSimplingModels(string folderpath, bool TestSet)
        {
            Task[] modelsTasks = new Task[_models.Count];
            for (int i = 0; i < _models.Count; i++)
            {
                modelsTasks[i] = preSimplifyModel(_models[i]);
            }
            Task.WaitAll(modelsTasks);
            WritePushoverCurves(folderpath, TestSet);
            SetState(ModelState.StartSimpling);
        }

        private async Task preSimplifyModel(DetailedModel detailedModel)
        {
            if (detailedModel.LayoutUtility.InnerWallsLocations.Count > 0)
                detailedModel.ModeShapesMultiplier = new List<double>() { 1, 0, 0 };
            await detailedModel.RunModalAnalysis(false);
            await detailedModel.RunPushoverResults();
            
        }

        private void SimplfyingModels(string folderpath)
        {
            if (Alhpa_TRatio == null || Alhpa_TRatio.Count == 0 || Alhpa_Sigma1 == null || Alhpa_Sigma1.Count == 0)
                ReadRelationShips();
            _calibrationUnits = new List<CalibrationUnit>();
            Task[] modelsTasks = new Task[_models.Count];
            for (int i = 0; i < _models.Count; i++)
            {
                modelsTasks[i] = CalibrateModel(_models[i], Alhpa_Sigma1,folderpath);
            }
            Task.WaitAll(modelsTasks);
            WritePushoverResults(folderpath);
            SetState(ModelState.ExportFinalTclFinalForR2D);
        }

        private void WritePushoverResults(string folderpath)
        {
            ExcelWriterUtiliy writerUtiliy = new ExcelWriterUtiliy() { MainFolderPath = folderpath };
            writerUtiliy.WriteResults(_calibrationUnits);
        }

        public List<double> GetDoublelistFromLine(string Line)
        {
            List<double> values = new List<double>();
            foreach (var value in Line.Split(','))
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                double number;
                if (double.TryParse(value,out number))
                    values.Add(number);
            }
            return values;
            
        }
        private void ReadRelationShips()
        {
            Alhpa_TRatio = new List<Point2D>();
            Alhpa_Sigma1 = new List<Point2D>();

            string workingDirectory = Environment.CurrentDirectory;
            string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
            string filePath = Path.Combine(projectDirectory, "AlphaTValues.csv");
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs, System.Text.Encoding.Default))
                {
                    sr.ReadLine();
                    string Line = sr.ReadLine();
                    while (!string.IsNullOrEmpty(Line))
                    {
                        List<double> values = GetDoublelistFromLine(Line);
                        if (values.Count == 4)
                        {
                            Alhpa_TRatio.Add(new Point2D(values[0], values[1]));
                            Alhpa_Sigma1.Add(new Point2D(values[2], values[3]));
                        }
                        else if (values.Count == 2)
                        { 
                            Alhpa_TRatio.Add(new Point2D(values[0], values[1]));
                        }
                        Line = sr.ReadLine();
                    }
                    sr.Close();
                }
                fs.Close();
            }
            
        }

        private void Estimate()
        {
            Task[] modelsTasks = new Task[_models.Count];
            for (int i = 0; i < _models.Count; i++)
            {
                modelsTasks[i] = _models[i].CalcCost();
            }
            Task.WaitAll(modelsTasks);
            SetState(ModelState.WriteSections);
        }
        private void WritePushoverCurves(string folderpath, bool TestSet)
        {
            ExcelWriterUtiliy writerUtiliy = new ExcelWriterUtiliy() { MainFolderPath = folderpath };
            writerUtiliy.WritePushOverCurves(_models, TestSet);
        }
        private void WriteSections(string folderpath)
        {
            ExcelWriterUtiliy writerUtiliy = new ExcelWriterUtiliy() { MainFolderPath = folderpath };
            writerUtiliy.WriteResults(_models);
            SetState(ModelState.PreSimpling);
        }
        private async Task CalibrateModel(DetailedModel detailed, List<Point2D> Alpha_Sigma1, string folderpath)
        {
            CalibrationUnit calibrationUnit = new CalibrationUnit(detailed);
            _calibrationUnits.Add(calibrationUnit);
            await calibrationUnit.StartCalibration(Alpha_Sigma1,folderpath);
        }
        private void DefineModels(bool TestSet)
        {
            _models = new List<DetailedModel>();
            LayoutUtility utility = new LayoutUtility();
            if (TestSet)
            {
                //utility.CoreColumnsLocations.Clear();
                utility.InnerWallsLocations.Clear();
                utility.NumberOfFloorPerGroup = 3;
                utility.CoreOpeningLength = utility.MajorSpacing;
                //
                utility.PL = 0.72 * 1000; //0.72 * 1000;
                utility.DL = (3.6 + 0.5) * 1000; //(3.6 + 0.5) * 1000;
                utility.LL = 2.4 * 1000; //2.4 * 1000;
                utility.RLL = 1.00* 1000;// 1000;

                int floors = 6;
                while (floors < 13)
                {
                    _models.Add(new DetailedModel($"M{_models.Count + 1}_{floors}F_{_concreteMaterials[1].GetYieldStrength_MPA()}RC", floors
                        , new ModelMaterialsInfo(_concreteMaterials[0], _concreteMaterials[1], _steelMaterials[0], _steelMaterials[1], _concreteMaterials[0])
                        , utility));
                    floors += utility.NumberOfFloorPerGroup;
                }
            }
            else
            {
                utility.CoreColumnsLocations.Clear();
                
                int floors = 10;
                ConCreteMaterial conCreteMaterial = _concreteMaterials[0];
                SteelMaterial steelMaterial = _steelMaterials[2];
                while (floors < 51)  
                {
                    _models.Add(new DetailedModel($"M{_models.Count + 1}_{floors}F_{conCreteMaterial.GetYieldStrength_MPA()}RC",floors
                        , new ModelMaterialsInfo(conCreteMaterial, _concreteMaterials[1], _steelMaterials[1], steelMaterial, conCreteMaterial)
                        ,utility));
                    floors += utility.NumberOfFloorPerGroup;
                    if (floors > 20)
                    { 
                        conCreteMaterial = _concreteMaterials[1];
                        steelMaterial = _steelMaterials[2];

                    }
                    if (floors > 35)
                    { 
                        conCreteMaterial = _concreteMaterials[2];
                        steelMaterial = _steelMaterials[3];
                    }
                }
            }
        }
        private void DefineMaterials(bool TestSet)
        {
            if (TestSet)
            {
                // N , m , AED
                _concreteMaterials = new List<ConCreteMaterial>()
                {
                    new ConCreteMaterial(28E+6,220),
                    new ConCreteMaterial(38E+6,220),
                };
                _steelMaterials = new List<SteelMaterial>()
                {
                    new SteelMaterial("G40",280E+6,2200),
                    new SteelMaterial("G60",420E+6,2200),
                };
            }
            else
            { 
                // N , m , AED
                _concreteMaterials = new List<ConCreteMaterial>()
                {
                    new ConCreteMaterial(50E+6,220),
                    new ConCreteMaterial(60E+6,235),
                    new ConCreteMaterial(70E+6,250),
                    //new ConCreteMaterial(150E+6,300),
                    //new ConCreteMaterial(185E+6,350),
                    //new ConCreteMaterial(220E+6,400)
                };

                _steelMaterials = new List<SteelMaterial>()
                {
                    new SteelMaterial("G40",280E+6,2200),
                    new SteelMaterial("G60",420E+6,2200),
                    new SteelMaterial("G75",500E+6,2200),
                    new SteelMaterial("G80",550E+6,2200),
                    //new SteelMaterial("G100",690E+6,2200)
                };
            }
        }
    }
}
