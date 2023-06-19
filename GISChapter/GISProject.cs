using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Excel = Microsoft.Office.Interop.Excel;

namespace GISChapter
{
    public enum ProjectState
    {
        ReadData,
        InitModels,
        WriteInputFiles,
        ReadAnalysisResults, 
        WriteStatsFiles,
        WriteStatisticalFiles
    }
    [DataContract(IsReference = true)]
    public class GISProject
    {
        public static double minX = double.MaxValue;
        public static double minY = double.MaxValue;
        public static double maxX = double.MinValue;
        public static double maxY = double.MinValue;

        [DataMember]
        public ProjectState State;

        [DataMember]
        public List<Neighbour> Neighbours;
        [DataMember]
        public List<ArchTypesGroup> Groups;
        public static GISProject GetIntance(bool FromStart)
        {
            if (FromStart)
                return new GISProject();
            try
            {
                var savingPath = GetPath();
                if (File.Exists(savingPath))
                {
                    FileStream fs = new FileStream(savingPath, FileMode.OpenOrCreate);
                    XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, new XmlDictionaryReaderQuotas());
                    DataContractSerializer ser = new DataContractSerializer(typeof(GISProject));
                    GISProject project = (ser.ReadObject(reader) as GISProject);
                    reader.Close();
                    return project;
                }
                return new GISProject();
            }
            catch (Exception ex)
            {
                return new GISProject();
            }

        }
        public static string GetFilePath(string FolderName)
        {
            return Path.Combine(GetDirectoryPath(), FolderName);
        }
        public static string GetPath()
        {
            return Path.Combine(GetDirectoryPath(), "GISProject.AMM");
        }
        public static string GetDirectoryPath()
        {
            return Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location)?.FullName;
        }
        internal void Serialize()
        {
            var path = GetPath();
            FileStream fs = new FileStream(path, FileMode.Create);
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(fs);
            DataContractSerializer ser =
                new DataContractSerializer(typeof(GISProject));
            ser.WriteObject(writer, this);
            writer.Close();
            fs.Close();
        }
        protected void SetState(ProjectState modelState)
        {
            State = modelState;
            Serialize();
        }
        public async Task Run(bool FromStart)
        {
            // Reset the current state to the first stage
            if (FromStart)
                SetState(ProjectState.ReadData);
            // Reade .CSV data Neighbours (Name, polygon , Number of builsings, Classification)
            if (State == ProjectState.ReadData)
                ReadData();
            // Assign buildings of 
            if (State == ProjectState.InitModels)
                InitModes();
            // Create Input Spread Sheets for R2D
            if (State == ProjectState.WriteInputFiles)
                WriteInputFiles();
            // Read Results From R2D Log File (After moving them to Local Folder)
            if (State == ProjectState.ReadAnalysisResults)
                ReadResults();
            // Genrate ,CSV files for results staistics per archetype, archetype group, neighbour
            if (State == ProjectState.WriteStatsFiles)
                WriteOutputFiles();
            // Write GeJson File for maps visulaization
            if (State == ProjectState.WriteStatisticalFiles)
                WriteMapsFiles();
        }
        private void CheckFiles(List<string> filnames)
        {
            foreach (var Filename in filnames)
            {
                string filePath = GetFilePath(Filename);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
        private void WriteMapsFiles()
        {
            List<string> Filenames = new List<string> { "Map.json", "CollapseProps.csv", "RepairCost.csv", "RepairTime.csv", "Injuries.csv" };
            CheckFiles(Filenames);

            string spaceSmall = "\t";
            using (StreamWriter file = new StreamWriter(GetFilePath(Filenames[0]), false))
            {
                file.WriteLine("{");
                file.WriteLine(spaceSmall + $"\"type\": \"FeatureCollection\",");
                file.WriteLine(spaceSmall + "\"features\": [");
                for (int i = 0; i < Neighbours.Count - 1; i++)
                {
                    Neighbours[i].WriteJSON(file, false);
                }
                Neighbours[Neighbours.Count - 1].WriteJSON(file, true);
                file.WriteLine(spaceSmall + "]");
                file.WriteLine("}");
                file.Close();
            }
            using (StreamWriter file = new StreamWriter(GetFilePath(Filenames[1]), false))
            {
                file.WriteLine($"Ids,CollapseProps");
                for (int i = 0; i < Neighbours.Count; i++)
                {
                    file.WriteLine($"{Neighbours[i].ID},{Neighbours[i].BuildingResult.Collapse_Prob.Mean}");
                }
            }
            using (StreamWriter file = new StreamWriter(GetFilePath(Filenames[2]), false))
            {
                file.WriteLine($"Ids,RepairCost");
                for (int i = 0; i < Neighbours.Count; i++)
                {
                    file.WriteLine($"{Neighbours[i].ID},{Neighbours[i].BuildingResult.RepairCost.Mean}");
                }
            }
            using (StreamWriter file = new StreamWriter(GetFilePath(Filenames[3]), false))
            {
                file.WriteLine($"Ids,RepairTime");
                for (int i = 0; i < Neighbours.Count; i++)
                {
                    file.WriteLine($"{Neighbours[i].ID},{Neighbours[i].BuildingResult.RepairTime.Mean}");
                }
            }
            using (StreamWriter file = new StreamWriter(GetFilePath(Filenames[4]), false))
            {
                file.WriteLine($"Ids,Injuries");
                for (int i = 0; i < Neighbours.Count; i++)
                {
                    file.WriteLine($"{Neighbours[i].ID},{Neighbours[i].BuildingResult.Injuries.Mean}");
                }

            }
        }
        private void WriteDetailedOutput(Dictionary<string,BuildingResult> Results, string TitleName, string FileName)
        {
            string filePath = GetFilePath(FileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }    
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                file.WriteLine($"{TitleName},Number of Assets,Parameter, Mean, Std, V1, V2");
                foreach (string record in Results.Keys)
                {
                    BuildingResult Result = Results[record];
                    file.WriteLine($"{record},{Result.N},Collapse probability (%)," + Result.Collapse_Prob.GetString());
                    file.WriteLine($",,Total Repair Cost Ratio (%)," + Result.RepairCost.GetString());
                    file.WriteLine($",,(S) Repair Cost Ratio (%)," + Result.S_Repair.GetString());
                    file.WriteLine($",,(NSA) Repair Cost Ratio (%)," + Result.NSA_Repair.GetString());
                    file.WriteLine($",,(NSD) Repair Cost Ratio (%)," + Result.NSD_Repair.GetString());
                    file.WriteLine($",,Repair Time [Days]," + Result.RepairTime.GetString());
                    file.WriteLine($",,Injuries (SEV-1) (%)," + Result.Sev1_Injuries.GetString());
                    file.WriteLine($",,Injuries (SEV-2) (%)," + Result.Sev2_Injuries.GetString());
                    file.WriteLine($",,Injuries (SEV-3) (%)," + Result.Sev3_Injuries.GetString());
                    file.WriteLine($",,Injuries (SEV-4) (%)," + Result.Sev4_Injuries.GetString());
                    file.WriteLine($",,Total Injuries (%)," + Result.Injuries.GetString());
                }
                file.Close();
            }
        }
        private void WriteSummaryOutput()
        {
            string filePath = GetFilePath("Neigbours.csv");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                file.WriteLine($"Neighbour,Usage, Latitude , Longtitude ,Collapse probability (%),Repair Cost Ratio (%),Repair Time [Days], Injuries, Injuries Sev-1, Injuries Sev-2 ,Injuries Sev-3, Injuries Sev-4");
                foreach (Neighbour Neigh in Neighbours)
                {
                    BuildingResult result = Neigh.BuildingResult;
                    file.WriteLine($"{Neigh.Name},{Neigh.Usage},{Neigh.PointLocation.X},{Neigh.PointLocation.Y},{result.Collapse_Prob.Mean},{result.RepairCost.Mean},{result.RepairTime.Mean},{result.Injuries.Mean},{result.Sev1_Injuries.Mean},{result.Sev2_Injuries.Mean},{result.Sev3_Injuries.Mean},{result.Sev4_Injuries.Mean}");
                }
                file.Close();
            }
        }
        private void WriteOutputFiles()
        {
            Dictionary<string, BuildingResult> dict = new Dictionary<string, BuildingResult>();
            dict.Add("Total Buildings", GetResultOfListOfBuildings(GetAllBuildings()));
            WriteDetailedOutput(dict, "", "AllBuildings.csv");
            dict.Clear();

            foreach (ArchTypesGroup archTypesGroup in Groups)
            {
                dict.Add(archTypesGroup.Label, GetResultOfListOfBuildings(GetBuildingsByArchTypeGroup(archTypesGroup)));
            }
            WriteDetailedOutput(dict, "ArchType Group", "ArchTypeGroups.csv");
            dict.Clear();

            foreach (ArchType archType in Groups.SelectMany(a=>a.ArchTypes))
            {
                dict.Add(archType.Label, GetResultOfListOfBuildings(GetBuildingsByArchType(archType)));
            }
            WriteDetailedOutput(dict, "ArchType", "ArchTypes.csv");
            dict.Clear();

            foreach (var neighboursGoup in Neighbours.GroupBy(x=>x.Usage))
            {
                dict.Add(neighboursGoup.Key, GetResultOfListOfNeighbours(neighboursGoup.ToList()));
            }
            WriteDetailedOutput(dict, "NeighbourClass", "NeighboursClasses.csv");

            dict.Clear();
            WriteSummaryOutput();
            SetState(ProjectState.WriteStatisticalFiles);
        }
        private void ReadResults()
        {
            string Folderpath = GetDirectoryPath();
            DirectoryInfo d = new DirectoryInfo(Folderpath); //Assuming Test is your Folder
            foreach (FileInfo f in d.GetFiles("*.csv"))
            {
                if (!f.Name.StartsWith("DV"))
                    continue;
                using (StreamReader file = new StreamReader(f.FullName))
                {
                    string ln = file.ReadLine();
                    ln = file.ReadLine();
                    ln = file.ReadLine();
                    ln = file.ReadLine();

                    ln = file.ReadLine();
                    while (!string.IsNullOrEmpty(ln))
                    {
                        List<double> Values = ReadResultLine(ln);
                        BuildingModel building = GetBuildingById((int)Values[0]);
                        building.BuildingResult = BuildingResult.CreateFromFile(Values,building.Count);
                        ln = file.ReadLine();
                    }
                }
            }
            foreach (Neighbour Neigh in Neighbours)
            {
                Neigh.BuildingResult = GetResultOfListOfBuildings(Neigh.GetBuildingModels());
            }
            SetState(ProjectState.WriteStatsFiles);
        }
        private List<double> ReadResultLine(string line)
        {
            return line.Split(',').Select(s => string.IsNullOrEmpty(s) ? 0.0 : double.Parse(s)).ToList();
        }
        private BuildingResult GetResultOfListOfNeighbours(List<Neighbour> neighbours)
        {
            List<Neighbour> neighboursNulls = neighbours.Where(b => b.BuildingResult == null).ToList();
            if (neighboursNulls.Count > 0)
                return null;
            BuildingResult result = neighbours[0].BuildingResult;
            for (int i = 1; i < neighbours.Count; i++)
            {
                result = BuildingResult.Add(result, neighbours[i].BuildingResult);
            }
            return result;
        }

        private BuildingResult GetResultOfListOfBuildings(List<BuildingModel> buildingModels)
        {
            List<BuildingModel> buildingModelsNulls = buildingModels.Where(b => b.BuildingResult == null).ToList();
            if (buildingModelsNulls.Count > 0)
                return null;
            BuildingResult result = buildingModels[0].BuildingResult;
            for (int i = 1; i < buildingModels.Count; i++)
            {
                result = BuildingResult.Add(result, buildingModels[i].BuildingResult);
            }
            return result;
        }
        private List<BuildingModel> GetBuildingsByArchTypeGroup(ArchTypesGroup group)
        {
            List<BuildingModel> buildingsDict = new List<BuildingModel>();
            foreach (ArchType archType in group.ArchTypes)
            {
                buildingsDict.AddRange(GetBuildingsByArchType(archType));
            }
            return buildingsDict;
        }
        private List<BuildingModel> GetBuildingsByArchType(ArchType archType)
        {
            List<BuildingModel> buildingsDict = new List<BuildingModel>();
            foreach (Neighbour Neigh in Neighbours)
            {
                buildingsDict.AddRange(Neigh.BuildingInventory.LR_Buildings.Where(b=>b.ArchType == archType));
            }
            foreach (Neighbour Neigh in Neighbours)
            {
                buildingsDict.AddRange(Neigh.BuildingInventory.HR_Buildings.Where(b => b.ArchType == archType));
            }
            return buildingsDict;
        }
        private List<BuildingModel> GetAllBuildings()
        {
            List<BuildingModel> buildingsDict = new List<BuildingModel>();
            foreach (Neighbour Neigh in Neighbours)
            {
                buildingsDict.AddRange(Neigh.BuildingInventory.LR_Buildings);
            }
            foreach (Neighbour Neigh in Neighbours)
            {
                buildingsDict.AddRange(Neigh.BuildingInventory.HR_Buildings);
            }
            return buildingsDict;
        }
        private BuildingModel GetBuildingById(int Id )
        {
            foreach (Neighbour Neigh in Neighbours)
            {
                BuildingModel buildingModel = Neigh.BuildingInventory.LR_Buildings.FirstOrDefault(b=>b.ID == Id);
                if (buildingModel != null)
                    return buildingModel;
            }
            foreach (Neighbour Neigh in Neighbours)
            {
                BuildingModel buildingModel = Neigh.BuildingInventory.HR_Buildings.FirstOrDefault(b=>b.ID == Id);
                if (buildingModel != null)
                    return buildingModel;
            }
            return null;
        }
        private void WriteInputFiles()
        {
            WriteFile(Neighbours.SelectMany(N=>N.BuildingInventory.LR_Buildings),"LR_Models");
            WriteFile(Neighbours.SelectMany(N=>N.BuildingInventory.HR_Buildings),"HR_Models");
            WriteGridsFile();
            SetState(ProjectState.ReadAnalysisResults);
        }
        private void WriteFile(IEnumerable<BuildingModel> buildings, string FileName)
        {
            string filePath = GetFilePath(FileName+".csv");
            if (File.Exists(filePath))
                File.Delete(filePath);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                file.WriteLine("id,Latitude,Longitude,NumberOfStories,YearBuilt,OccupancyClass,PlanArea,StructureType,Footprint");
                foreach (BuildingModel BuildingModel in buildings)
                {
                    BuildingModel.WriteCSVInput(file);
                }
                file.Close();
            }
        }
        private void WriteGridsFile()
        {
            string filePath = GetFilePath( "GridsData.csv");
            if (File.Exists(filePath))
                File.Delete(filePath);
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                file.WriteLine("Station,Latitude,Longitude");
                int Id = 1;
                foreach (Neighbour neighbour in Neighbours)
                {
                    file.WriteLine($"{Id},{neighbour.PointLocation.X},{neighbour.PointLocation.Y}");
                    Id++;
                }
                file.Close();
            }
        }
        private void InitModes()
        {
            InitArchTypes();
            AssignModelsToNeighbours();
            SetState(ProjectState.WriteInputFiles);
        }
        private void AssignModelsToNeighbours(List<List<KeyValuePair<ArchTypesGroup, float>>> models, bool HR)
        {
            Neighbours.ForEach(N =>
            {
                switch (N.Usage)
                {
                    case "Commercial":
                        N.AssignModels(models[0],HR);
                        break;
                    case "Residential":
                        N.AssignModels(models[1],HR);
                        break;
                    case "Industrial":
                        N.AssignModels(models[2],HR);
                        break;
                    default:
                        N.AssignModels(models[3],HR);
                        break;
                }
            });
        }
        private void AssignModelsToNeighbours()
        {
            Neighbours.ForEach(N => N.BuildingInventory = new BuildingInventory());

            List <KeyValuePair<ArchTypesGroup, float>> CommercialRatios = new List<KeyValuePair<ArchTypesGroup, float>>();
            CommercialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[2], 0.25f));
            CommercialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[3], 0.25f));
            CommercialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[4], 0.25f));
            CommercialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[5], 0.25f));

            List<KeyValuePair<ArchTypesGroup, float>> ResidentialRatios = new List<KeyValuePair<ArchTypesGroup, float>>();
            ResidentialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[0], 0.50f));
            ResidentialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[2], 0.25f));
            ResidentialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[3], 0.15f));
            ResidentialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[4], 0.05f));
            ResidentialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[5], 0.05f));

            List<KeyValuePair<ArchTypesGroup, float>> IndustrialRatios = new List<KeyValuePair<ArchTypesGroup, float>>();
            IndustrialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[0], 0.30f));
            IndustrialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[1], 0.70f));
            IndustrialRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[2], 0.15f));

            List<KeyValuePair<ArchTypesGroup, float>> UniditinfiendRatios = new List<KeyValuePair<ArchTypesGroup, float>>();
            UniditinfiendRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[0], 0.40f));
            UniditinfiendRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[1], 0.10f));
            UniditinfiendRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[2], 0.15f));
            UniditinfiendRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[3], 0.15f));
            UniditinfiendRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[4], 0.10f));
            UniditinfiendRatios.Add(new KeyValuePair<ArchTypesGroup, float>(Groups[5], 0.10f));

            List<List<KeyValuePair<ArchTypesGroup, float>>> models = new List<List<KeyValuePair<ArchTypesGroup, float>>>
            {
                CommercialRatios,ResidentialRatios,IndustrialRatios,UniditinfiendRatios
            };
            AssignModelsToNeighbours(models,false);
            AssignModelsToNeighbours(models,true);
        }
        private void InitArchTypes()
        {
            Groups = new List<ArchTypesGroup>
            {
                new ArchTypesGroup
                {
                    Label = "RES",
                    HR = false,
                    ArchTypes = new List<ArchType>
                    {
                        new ArchType("LR_RES_F1",1,1981,"RES1","RM1", 1615),
                        new ArchType("LR_RES_F2",2,1995,"RES3", "C1",2690),
                        new ArchType("LR_RES_F3",3,2000,"RES3", "C2",4300),
                        new ArchType("MR_RES_F5",5,2002,"RES3","C1", 5382)
                    }
                },
                new ArchTypesGroup
                {
                    Label = "IND",
                    HR = false,
                    ArchTypes = new List<ArchType>
                    {
                        new ArchType("LR_IND_F1",1,2003,"IND2","S1",10674)
                    }
                },
                new ArchTypesGroup
                {
                    Label = "COM(3-7)",
                    HR = false,
                    ArchTypes = new List<ArchType>
                    {
                        new ArchType("LR_COM_F3",3,1990,"COM2","C1",5920),
                        new ArchType("MR_COM_F6",6,2005,"COM1","C1",5382),
                        new ArchType("MR_COM_F7",7,2010,"COM1","C2",5382)
                    }
                },
                new ArchTypesGroup
                {
                    Label = "COM(10-20)",
                    HR = true,
                    ArchTypes = new List<ArchType>
                    {
                        new ArchType("HR_ COM _F10",10,2000,"COM4","C2",9685),
                        new ArchType("HR_ COM _F15",15,2000,"COM4","C2",9685),
                        new ArchType("HR_ COM _F20",20,2000,"COM4","C2",9685)
                    }
                },
                new ArchTypesGroup
                {
                    Label = "COM(25-35)",
                    HR = true,
                    ArchTypes = new List<ArchType>
                    {
                        new ArchType("HR_ COM _F25",25,2005,"COM4","C2",9685),
                        new ArchType("HR_ COM _F30",30,2005,"COM4","C2",9685),
                        new ArchType("HR_ COM _F35",35,2005,"COM4","C2",9685)
                    }
                },
                new ArchTypesGroup
                {
                    Label = "COM(40-50)",
                    HR = true,
                    ArchTypes = new List<ArchType>
                    {
                        new ArchType("HR_ COM _F40",40,2010,"COM4","C2",9685),
                        new ArchType("HR_ COM _F45",45,2010,"COM4","C2",9685),
                        new ArchType("HR_ COM _F50",50,2010,"COM4","C2",9685)
                    }
                }
            };
        }
        private void ReadData()
        {
            List<Neighbour> ExcelData = ReadExcelData();
            List<List<Point3D>> PolygonsData = ReadPlogygonData();
            List<Point3D> Points = ReadPointsData();
            for (int i = 0; i < ExcelData.Count; i++)
            {
                ExcelData[i].Polygon = PolygonsData[i];
                ExcelData[i].PointLocation = Points[i];
            }
            Neighbours = ExcelData;
            SetState(ProjectState.InitModels);

            double dx = 0.25 * Math.Abs(maxX - minX);
            double dy = 0.25 * Math.Abs(maxY - minY);

            maxX += dx;
            minX -= dx;

            maxY += dy;
            minY -= dy;

            Console.WriteLine($"MaxX:{maxX},MinX:{minX}");
            Console.WriteLine($"MaxY:{maxY},MinY:{minY}");
        }
        private List<Point3D> ReadPointsData()
        {
            string allText = "";
            using (StreamReader rd = new StreamReader(GetFilePath("Temporary Places1.geojson")))
            {
                allText = rd.ReadToEnd();
            }
            dynamic dynObject = JsonConvert.DeserializeObject<dynamic>(allText);
            List<Point3D> pointList = new List<Point3D>();
            foreach (var Feature in dynObject["features"])
            {
                Point3D point = new Point3D();
                var PointData = Feature["geometry"]["coordinates"];
                point.X = float.Parse(Convert.ToString(PointData[0]));
                point.Y = float.Parse(Convert.ToString(PointData[1]));
                point.Z = float.Parse(Convert.ToString(PointData[2]));
                pointList.Add(point);
            }
            return pointList;
        }

        private List<List<Point3D>> ReadPlogygonData()
        {
            string allText = "";
            using (StreamReader rd = new StreamReader(GetFilePath("Temporary Places.geojson")))
            {
                allText = rd.ReadToEnd();
            }
            dynamic dynObject = JsonConvert.DeserializeObject<dynamic>(allText);
            List<List<Point3D>> polygonList = new List<List<Point3D>>();
            foreach (var Feature in dynObject["features"])
            {
                List<Point3D> Polygon = new List<Point3D>();
                foreach (var PointData in Feature["geometry"]["coordinates"][0])
                {
                    Point3D point = new Point3D();
                    point.X = float.Parse(Convert.ToString(PointData[0]));
                    point.Y = float.Parse(Convert.ToString(PointData[1]));
                    point.Z = float.Parse(Convert.ToString(PointData[2]));
                    Polygon.Add(point);

                    minX = Math.Min(point.X, minX);
                    maxX = Math.Max(point.X, maxX);

                    minY = Math.Min(point.Y, minY);
                    maxY = Math.Max(point.Y, maxY);
                }

                polygonList.Add(Polygon);
            }
            return polygonList;
        }

        private List<Neighbour> ReadExcelData()
        {
            Random r = new Random();
            List<Neighbour> ExcelData = new List<Neighbour>();
            using (StreamReader rd = new StreamReader(GetFilePath("Data.csv")))
            {
               rd.ReadLine();
               while(true)
               {
                    string line = rd.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        break;
                    string[]  arr = line.Split(',');
                    ExcelData.Add(new Neighbour
                    {
                        ID = int.Parse(arr[0]),
                        Name = arr[1],
                        Usage = arr[2],
                        Buildings = Math.Max((int)(90+ r.NextDouble() *10) ,int.Parse(arr[5])),
                    }); 
               }

            }
            return ExcelData;
        }
      
    }
}
