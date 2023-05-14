using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GISChapter
{
    [DataContract(IsReference = true)]
    public class Point3D
    {
        [DataMember]
        public double X { get; set; }
        [DataMember]
        public double Y { get; set; }
        [DataMember]
        public double Z { get; set; }

        public string ToText()
        {
            return $"[{X},{Y}]";
        }
    }

    [DataContract(IsReference = true)]
    public class BuildingModel
    {
        [DataMember]
        public long ID;
        [DataMember]
        public int Count;
        [DataMember]
        public ArchType ArchType;
        [DataMember]
        public Point3D Center;
        [DataMember]
        public List<Point3D> polygon;
        [DataMember]
        public BuildingResult BuildingResult;

        internal void CalcPolygon()
        {
            polygon = new List<Point3D>();
            float DiffLength = (float)Math.Sqrt(ArchType.PlanArea);
            float xf = 1.0f / 364000.0f;
            float yf = 1.0f / 288200.0f;
            for (int i = 0; i < 4; i++)
            {
                float Angle = (float)Math.PI / 2.0f * i;
                polygon.Add( new Point3D
                {
                    X = Center.X + Math.Cos(Angle) * DiffLength * xf,
                    Y = Center.Y + Math.Sin(Angle) * DiffLength * yf,
                    Z = Center.Z
                });
            }
        }
        internal void WriteCSVInput(StreamWriter file)
        {
            file.WriteLine($"{ID},{Center.X},{Center.Y},{ArchType.WriteCSVInput()},{WriteFootPrint()}");
        }
        private string WriteFootPrint()
        {
            string points = polygon[0].ToText();
            for (int i = 1; i < polygon.Count; i++)
            {
                points += "," + polygon[i].ToText();
            }
            return "\"{\"\"type\"\":\"\"Feature\"\",\"\"geometry\"\":{ \"\"type\"\":\"\"Polygon\"\",\"\"coordinates\"\":[[" +
                points + "]]},\"\"properties\"\":{}}\"";
        }
    }

    [DataContract(IsReference = true)]
    public class BuildingInventory
    {
        [DataMember]
        public List<BuildingModel> LR_Buildings = new List<BuildingModel>();
        [DataMember]
        public List<BuildingModel> HR_Buildings = new List<BuildingModel>();

        internal void AssignLocations(Point3D pointLocation)
        {
            AssignLocations(pointLocation, LR_Buildings);
            AssignLocations(pointLocation, HR_Buildings);
        }
        private void AssignLocations(Point3D pointLocation, List<BuildingModel> Buildings)
        {
            if (!Buildings.Any())
                return;
            float Area = Buildings.Max(x => x.ArchType.PlanArea);
            float DiffLength = 5.0f * (float)Math.Sqrt(Area);
            Buildings[0].Center = pointLocation;
            float xf = 1.0f/ 364000.0f;
            float yf = 1.0f/ 288200.0f;
            for (int i = 1; i < Buildings.Count; i++)
            {
                float Angle = (float)Math.PI / 4.0f * (i - 1);
                Buildings[i].Center = new Point3D
                {
                    X = pointLocation.X + Math.Cos(Angle)* DiffLength * xf ,
                    Y = pointLocation.Y + Math.Sin(Angle)* DiffLength * yf,
                    Z = pointLocation.Z 
                };
            }

            Buildings.ForEach(b => b.CalcPolygon());

        }
    }
    [DataContract(IsReference = true)]
    public class Neighbour
    {
        public static long Building_ID = 0;
        [DataMember]
        public int ID { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Usage { get; set; }
        [DataMember]
        public int Buildings { get; set; }
        [DataMember]
        public  List<Point3D> Polygon { get; set; } = new List<Point3D>();

        internal void WriteJSON(StreamWriter file, bool Last)
        {
            string Space = "\t" + "\t";
            string Space2  = Space + "\t";
            string Space3 = Space2 + "\t";
            string Space4 = Space3 + "\t";
            string Space5 = Space4 + "\t";

            file.WriteLine(Space + "{");

            file.WriteLine(Space2 + "\"type\": \"Feature\",");
            file.WriteLine(Space2 + $"\"id\": {ID},");

            file.WriteLine(Space2 + "\"properties\": {");
            file.WriteLine(Space3 + $"\"Name\": \"{Name}\",");
            file.WriteLine(Space3 + $"\"Usage\": \"{Usage}\"");
            file.WriteLine(Space2 + "},");

            file.WriteLine(Space2 + "\"geometry\": {");

            file.WriteLine(Space3 + $"\"type\": \"Polygon\",");
            file.WriteLine(Space3 + $"\"coordinates\": [");

            file.WriteLine(Space4 + "[");
            int i = 1;
            for (; i < Polygon.Count-1; i++)
            {
                file.WriteLine(Space5 + $"[{Polygon[i].X},{Polygon[i].Y},{Polygon[i].Z}],");
            }
            i = Polygon.Count - 1;
            file.WriteLine(Space5 + $"[{Polygon[i].X},{Polygon[i].Y},{Polygon[i].Z}]");
            file.WriteLine(Space4 + "]");
            file.WriteLine(Space3 + "]");
            file.WriteLine(Space2 + "}");
            file.WriteLine(Space + "}"+ (Last?"":","));
        }

        [DataMember]
        public Point3D PointLocation { get; set; }
        [DataMember]
        public BuildingInventory BuildingInventory { get; set; } 
        [DataMember]
        public BuildingResult BuildingResult { get; set; }
        public void AssignModels(List<KeyValuePair<ArchTypesGroup,float>> GroupsRatios, bool HR)
        {
            foreach (KeyValuePair<ArchTypesGroup,float> pair in GroupsRatios)
            {
                ArchTypesGroup group = pair.Key;
                if (group.HR != HR)
                    continue;
                float ratio = pair.Value;
                int count = (int)(ratio * Buildings / (float)group.ArchTypes.Count);
                List<BuildingModel> buildingModels = group.ArchTypes.Select(a => new BuildingModel { ID = ++Building_ID , ArchType = a, Count = count }).ToList();
                if (HR)
                    BuildingInventory.HR_Buildings.AddRange(buildingModels);
                else
                    BuildingInventory.LR_Buildings.AddRange(buildingModels);
            }
            BuildingInventory.AssignLocations(PointLocation);
        }
        public List<BuildingModel> GetBuildingModels() 
        {
            return BuildingInventory.LR_Buildings.Concat(BuildingInventory.HR_Buildings).ToList();
        }
    }

    [DataContract(IsReference = true)]
    public class ArchType
    {
        [DataMember]
        public string Label { get; set; }
        [DataMember]
        public int YearBuilt { get; set; }
        [DataMember]
        public int NumberOfStories { get; set; }
        [DataMember]
        public string Occupancy { get; set; }
        [DataMember]
        public string StructureType { get; set; }
        [DataMember]
        public float PlanArea { get; set; }
        public ArchType()
        {

        }
        public ArchType(string Label, int NumberOfStories, int YearBuilt, string Occupancy, string StructureType, float PlanArea)
        {
            this.Label = Label;
            this.NumberOfStories = NumberOfStories;
            this.YearBuilt = YearBuilt;
            this.Occupancy = Occupancy;
            this.StructureType = StructureType;
            this.PlanArea = PlanArea;
        }
        internal string WriteCSVInput()
        { 
            return $"{ NumberOfStories},{YearBuilt},{Occupancy},{PlanArea},{StructureType}";
        }
    }
    [DataContract(IsReference = true)]
    public class ArchTypesGroup
    {
        [DataMember]
        public string Label { get; set; }
        [DataMember]
        public bool HR { get; set; }
        [DataMember]
        public List<ArchType> ArchTypes { get; set; }
    }
}
