using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models.ModelMaterial;
using TempAnalysis.OpenseesCommand;
using TempAnalysis.Utilities;

namespace TempAnalysis.Models
{

    [DataContract(IsReference = true)]
    public class ColumnsGroup
    {
        [DataMember]
        public List<double> N_forces = new List<double>();
        [DataMember]
        public SquareColumnSection ColumnSection;
        [DataMember]
        public List<FrameNonLinearElement> Columns = new List<FrameNonLinearElement>();
        [DataMember]
        public List<FrameElasticElement> ElasticColumns = new List<FrameElasticElement>();
        [DataMember]
        public ElementsRegion ColumnsRegion;
        [DataMember]
        public ElasticSectionProperties section;
        [DataMember]
        public FiberSection FiberSection;

        public ColumnsGroup()
        {

        }
        public ColumnsGroup(SquareColumnSection Section)
        {
            ColumnSection = Section;
        }
        public ColumnsGroup(ColumnsGroup other):this(new SquareColumnSection(other.ColumnSection))
        {
        }

        public void CreateColumns(GridSystem grid, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial, List<Location> locations, string key, int startFloor, int EndFloor)
        {
            IDsManager IDM = grid.IDM;
            FiberSection = new FiberSection(IDM, ColumnSection, concrete01Material,steelMaterial);
            ElasticSectionProperties properties = ColumnSection.GenerateElasticProperties(concrete01Material,steelMaterial);
            for (int i = startFloor; i <= EndFloor; i++)
            {
                foreach (var location in locations)
                {
                    BaseNode Start = location.ToNode(grid, i - 1);
                    BaseNode End = location.ToNode(grid, i);
                    Columns.Add(new FrameNonLinearElement(IDM, Start, End, 1, FiberSection._ID));
                    ElasticColumns.Add(new FrameElasticElement(IDM, Start, End,1, properties));
                }
            }
            if (Columns.Any())
            { 
                ColumnsRegion = new ElementsRegion($"{key}_{startFloor}_{EndFloor}", IDM, Columns.First()._ID, ElasticColumns.Last()._ID);
            }
        }
        public void WriteSection(StreamWriter file)
        {
            FiberSection?.WriteCommand(file);
        }
        public void ClearColumns()
        {
            FiberSection = null;
            Columns.Clear();
            ElasticColumns.Clear();
            ColumnsRegion = null;
        }
        public void WriteColumns(StreamWriter file,bool Elastic)
        {
            if(Elastic)
                ElasticColumns.ForEach(c=>c.WriteCommand(file));
            else
                Columns.ForEach(c=>c.WriteCommand(file));
            ColumnsRegion?.WriteCommand(file);
        }
        internal void ReadResults(string folderPath, LoadCombinationFactors factors)
        {
            ColumnsRegion?.ReadResults(folderPath,factors);
        }
        internal void DesignGravityLoads(ReinforcementUtility ReinfUtil, double Cd, double FloorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial MainSteel, SteelMaterial MildSteel, SquareColumnSection previousSection)
        {
            if (previousSection == null)
            {
               ColumnSection = ReinfUtil.GetSectionForFirstFLoor(N_forces.Max(), Cd, FloorHeight,shearWallMaterial,MainSteel,MildSteel);
            }
            else
            {
                ColumnSection = ReinfUtil.GetSectionForNextFLoor(N_forces.Max(), Cd, FloorHeight, shearWallMaterial, MainSteel, MildSteel,previousSection);
            }
        }

        internal double GetConcreteVolume(int no_Columns, double floorHeight)
        {
            return no_Columns * ColumnSection.GetA() * floorHeight;
        }
        internal double GetSteelVolume(int no_Columns, double floorHeight)
        {
            double cover = 0.025 + ColumnSection.StirupsSteelBars.Diameter_M();
            return no_Columns * ((floorHeight + Math.Max(1.00, 40 * ColumnSection.MainSteelBars.Diameter_M())) * ColumnSection.GetLongitudinalSteelArea()
                                   + ColumnSection.GetStrirrupsLength(cover) * ColumnSection.StirupsSteelBars.GetArea_M2() * floorHeight *(6 + ColumnSection.StirrupsPerMeter) /2.0);
        }

        internal double GetColumnsWeight(double floorHeight, double Density)
        {
            return floorHeight * Math.Pow(ColumnSection.SectionDepth, 2) * ElasticColumns.Count * Density; 
        }
    }

}
