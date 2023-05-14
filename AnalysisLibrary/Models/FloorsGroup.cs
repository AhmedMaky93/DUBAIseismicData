using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.OpenseesCommand;
using TempAnalysis.Utilities;
using TempAnalysis.Models.ModelMaterial;

namespace TempAnalysis.Models
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(CoupliedShearWallAppliedForces))]
    public class ShearWallAppliedForces
    {
        [DataMember]
        public double NC;
        [DataMember]
        public double V;
        [DataMember]
        public double M;
        [DataMember]
        public double F;
        public ShearWallAppliedForces()
        {

        }
        public ShearWallAppliedForces(double F, double NC , double V ,double M)
        {
            this.F = F;
            this.NC = NC;
            this.V = V;
            this.M = M;
        }
        public virtual void Multiply(double Factor)
        {
            this.F *= Factor;
            this.NC *= Factor;
            this.V *= Factor;
            this.M *= Factor;
        }
        public override string ToString()
        {
            return $"NC:{NC.ToString("E")},V:{V.ToString("E")},M:{M.ToString("E")}";
        }
    }
    [DataContract(IsReference = true)]
    public class CoupliedShearWallAppliedForces : ShearWallAppliedForces
    {
        [DataMember]
        public double NT;
        [DataMember]
        public double BV;
        public CoupliedShearWallAppliedForces()
        {

        }
        public override void Multiply(double Factor)
        {
            base.Multiply(Factor);
            this.NT *= Factor;
            this.BV *= Factor;
        }
        public CoupliedShearWallAppliedForces(double F, double NC, double V, double M, double NT, double BV):base(F,NC, V,M)
        {
            this.NT = NT;
            this.BV = BV;
        }
        public override string ToString()
        {
            return $"NC:{NC.ToString("E")},NT:{NT.ToString("E")},V:{V.ToString("E")},M:{M.ToString("E")},BV:{BV.ToString("E")}";
        }
    }
    [DataContract(IsReference = true)]
    public class FloorsGroup
    {
        [DataMember]
        public string Name;
        [DataMember]
        public List<DetailedFloor> Floors = new List<DetailedFloor>();

        [DataMember]
        public ColumnsGroup CornerColumns;
        [DataMember]
        public ColumnsGroup OuterColumns;
        [DataMember]
        public ColumnsGroup InnerColumns;
        [DataMember]
        public ColumnsGroup CoreColumns;
        [DataMember]
        public ShearWallsGroup InnerShearWalls;
        [DataMember]
        public ShearWallsGroup OuterShearWalls;
        [DataMember]
        public BeamsGroup CouplingsBeams;

        [DataMember]
        public ShearWallAppliedForces OuterShearWallsForces;
        [DataMember]
        public CoupliedShearWallAppliedForces InnerShearWallsForces;

        public FloorsGroup()
        {

        }
        public FloorsGroup(int startFloorIndex, int EndFloorIndex)
        {
            this.Name = $"Floors_{startFloorIndex}_{EndFloorIndex}";
            Floors = new List<DetailedFloor>();
            for (int i = startFloorIndex; i <= EndFloorIndex; i++)
                Floors.Add(new DetailedFloor(i));
        }

        private double R(double v)
        {
            return Math.Round(v,3,MidpointRounding.AwayFromZero);
        }
        public void ConsoleWriteShearWallSection(double BeamLength)
        {
            var OS = OuterShearWalls.ShearWallReinforcement;
            var IS = InnerShearWalls.ShearWallReinforcement;
            Project.LogAction($"Outer:{R(OS.Length)}*{R(OS.Thickness)} ({R(OS.GetRo())})" +
                $", Inner:{R(IS.Length)}*{R(IS.Thickness)} ({R(IS.GetRo())})" +
                $", Beams:{R(BeamLength)}");
        }
        internal double CalcColumnsConcreteVolume(double floorHeight, int no_CornerColumns, int no_OuterColumns, int no_InnerColumns, int no_CoreColumns)
        {
            return Floors.Count * 
                ( CornerColumns.GetConcreteVolume(no_CornerColumns, floorHeight)
                + OuterColumns.GetConcreteVolume(no_OuterColumns, floorHeight)
                + InnerColumns.GetConcreteVolume(no_InnerColumns, floorHeight)
                + CoreColumns.GetConcreteVolume(no_CoreColumns, floorHeight));
        }
        internal double CalcColumnsSteelVolume(double floorHeight, int no_CornerColumns, int no_OuterColumns, int no_InnerColumns, int no_CoreColumns)
        {
            return Floors.Count *
                (CornerColumns.GetSteelVolume(no_CornerColumns, floorHeight)
                + OuterColumns.GetSteelVolume(no_OuterColumns, floorHeight)
                + InnerColumns.GetSteelVolume(no_InnerColumns, floorHeight)
                + CoreColumns.GetSteelVolume(no_CoreColumns, floorHeight));
        }

        internal double CalcWallsSteelVolume(double floorHeight, int no_OuterWalls, int no_InnerWalls, double BeamLength)
        {
            return Floors.Count * (no_OuterWalls * OuterShearWalls.GetSteelVolume(floorHeight)
                 + no_InnerWalls * InnerShearWalls.GetSteelVolume(floorHeight)
                 + no_InnerWalls/2 * CouplingsBeams.CouplingBeamSection.GetSteelVolume( BeamLength, InnerShearWalls.ShearWallReinforcement.Length));
        }

        internal double CalcWallsConcreteVolume(double floorHeight, int no_OuterWalls, int no_InnerWalls, double BeamLength)
        {
            return Floors.Count * (no_OuterWalls * floorHeight * OuterShearWalls.ShearWallReinforcement.GetA()
                 + no_InnerWalls * floorHeight * InnerShearWalls.ShearWallReinforcement.GetA()
                 + no_InnerWalls/2 * CouplingsBeams.CouplingBeamSection.GetA() * BeamLength);
        }

        public SquareColumnSection GetDefaultSquareSection(ReinforcementUtility ReinfUtil)
        {
            return new SquareColumnSection(0.35, 4, ReinfUtil.GetSteelBarByID(4), 6, ReinfUtil.GetSteelBarByID(3));
        }
        public SpecialShearWallReinforcement GetDefaultWallReinforcement(ReinforcementUtility ReinfUtil)
        {
            return new SpecialShearWallReinforcement(2, 0.25,
                new ShellReinforcement(6, ReinfUtil.GetSteelBarByID(4))
                , new ShellReinforcement(6, ReinfUtil.GetSteelBarByID(3)));
        }

        #region Walls
        public void CreateWallsElements(LayoutUtility layoutUtility, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial, double ShearWallLength, double CouplingBeamLength)
        {
            int start = Floors.First().Index;
            int end = Floors.Last().Index;
            OuterShearWalls.CreateElements_Horizontal(layoutUtility, concrete01Material, steelMaterial, layoutUtility.HorizontalOuterWallsLocations, "OuterWalls", start, end);
            InnerShearWalls.CreateElements_CoupledWalls(layoutUtility, concrete01Material, steelMaterial, ShearWallLength, CouplingBeamLength, "InnerWalls", start, end);
        }
        internal void ReadGravityResults(string folderPath, LoadCombinationFactors factors)
        {
            CornerColumns.ReadResults(folderPath, factors);
            OuterColumns.ReadResults(folderPath, factors);
            InnerColumns.ReadResults(folderPath, factors);
            CoreColumns.ReadResults(folderPath, factors);
        }
        public void WriteWallsElements(StreamWriter file, bool Elastic)
        {
            OuterShearWalls.WriteCommands(file, Elastic);
            InnerShearWalls.WriteCommands(file, Elastic);
            CouplingsBeams.WriteCommands(file, Elastic);
        }
        public void ClearWallsElements()
        {
            OuterShearWalls.ClearElements();
            InnerShearWalls.ClearElements();
            CouplingsBeams.ClearElements();
        }
        #endregion

        #region Beams
        public void CreatBeams(LayoutUtility layoutUtility, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial,double CouplingBeamLength)
        {
            int start = Floors.First().Index;
            int end = Floors.Last().Index;

            GridSystem GridSystem = layoutUtility.Grs;
            IDsManager IDM = GridSystem.IDM;

            List<List<BaseNode>> Locations = new List<List<BaseNode>>();
            for (int i = start; i <= end; i++)
            {
                double Z = GridSystem.GetValue(2, i);
                layoutUtility.InnerWallsLocations.ForEach(location =>
                {
                    List<Point2D> BeamPoints = new List<Point2D>();
                    BeamPoints.AddRange(location.GetWallInnerLocations(layoutUtility.CoreOpeningLength, CouplingBeamLength));
                    BeamPoints.AddRange(location.GetWallsNodes(layoutUtility.CoreOpeningLength, CouplingBeamLength));
                    BeamPoints.AddRange(location.GetCornerNodes(layoutUtility.CoreOpeningLength));
                    Locations.Add(BeamPoints.Select(x=> GridSystem.AddNode(NodeType.BaseNode, x.X, x.Y, Z)).ToList());
                });
            }
            if (layoutUtility.InnerWallsLocations.Any())
            { 
                CouplingsBeams.CreateElements(IDM, concrete01Material, steelMaterial, Locations, $"CouplingBeams_{start}_{end}", CouplingBeamLength);
            }
        }
        
        #endregion

        #region Columns
        public void ClearColumns()
        {
            CornerColumns.ClearColumns();
            OuterColumns.ClearColumns();
            InnerColumns.ClearColumns();
            CoreColumns.ClearColumns();
        }
        public void CreateColumns(LayoutUtility utility, Concrete02Material concMaterial, SteelUniAxialMaterial SteelMaterial)
        {
            int startFloorIndex = Floors.First().Index;
            int endFloorIndex = Floors.Last().Index;

            GridSystem grid = utility.Grs;
            CornerColumns.CreateColumns(grid, concMaterial, SteelMaterial, utility.CornerColumnsLocations, "CornerColumns", startFloorIndex, endFloorIndex);
            OuterColumns.CreateColumns(grid, concMaterial, SteelMaterial, utility.OuterColumnsLocations, "OuterColumns", startFloorIndex, endFloorIndex);
            InnerColumns.CreateColumns(grid, concMaterial, SteelMaterial, utility.InnerColumnsLocations, "InnerColumns", startFloorIndex, endFloorIndex);
            CoreColumns.CreateColumns(grid, concMaterial, SteelMaterial, utility.CoreColumnsLocations, "CoreColumns", startFloorIndex, endFloorIndex);
        }
        public void WriteColumns(StreamWriter file,bool Elastic)
        {
            CornerColumns.WriteColumns(file, Elastic);
            OuterColumns.WriteColumns(file, Elastic);
            InnerColumns.WriteColumns(file, Elastic);
            CoreColumns.WriteColumns(file, Elastic);
        }
        #endregion

        #region Sections
        public BeamSection GetDefaultBeamSection(double Length, double thickness, ReinforcementUtility ReinfUtil)
        {
            return new BeamSection(Length, thickness, ReinfUtil.GetSteelBarByID(4), 5, ReinfUtil.GetSteelBarByID(3), 3, ReinfUtil.GetSteelBarByID(3), 6);
        }
        public void InitSections(ReinforcementUtility ReinfUtil, double CoreOpeningLength, FloorsGroup SimilarGroup)
        {
            if (SimilarGroup == null)
            {
                CornerColumns = new ColumnsGroup(GetDefaultSquareSection(ReinfUtil));
                OuterColumns = new ColumnsGroup(GetDefaultSquareSection(ReinfUtil));
                InnerColumns = new ColumnsGroup(GetDefaultSquareSection(ReinfUtil));
                CoreColumns = new ColumnsGroup(GetDefaultSquareSection(ReinfUtil));

                OuterShearWalls = new ShearWallsGroup(GetDefaultWallReinforcement(ReinfUtil));
                InnerShearWalls = new ShearWallsGroup(GetDefaultWallReinforcement(ReinfUtil));

                double Length = CoreOpeningLength - 2 * InnerShearWalls.ShearWallReinforcement.Length;
                CouplingsBeams = new BeamsGroup(GetDefaultBeamSection(Length, InnerShearWalls.ShearWallReinforcement.Thickness, ReinfUtil));
            }
            else
            {
                CornerColumns = new ColumnsGroup(SimilarGroup.CornerColumns);
                OuterColumns = new ColumnsGroup(SimilarGroup.OuterColumns);
                InnerColumns = new ColumnsGroup(SimilarGroup.InnerColumns);
                CoreColumns = new ColumnsGroup(SimilarGroup.CoreColumns);

                OuterShearWalls = new ShearWallsGroup(SimilarGroup.OuterShearWalls);
                InnerShearWalls = new ShearWallsGroup(SimilarGroup.InnerShearWalls);

                CouplingsBeams = new BeamsGroup(SimilarGroup.CouplingsBeams);
            }
        }
        public void DefineColumnsDesignForces(List<double>[] P_forces)
        {
            CornerColumns.N_forces = P_forces[0].Select(x => Math.Abs(x)).ToList();
            OuterColumns.N_forces = P_forces[1].Select(x => Math.Abs(x)).ToList();
            InnerColumns.N_forces = P_forces[2].Select(x => Math.Abs(x)).ToList();
            CoreColumns.N_forces = P_forces[3].Select(x => Math.Abs(x)).ToList();
        }
        public void WriteSectionsCommands(StreamWriter writer)
        {
            CornerColumns.WriteSection(writer);
            OuterColumns.WriteSection(writer);
            InnerColumns.WriteSection(writer);
            CoreColumns.WriteSection(writer);

            OuterShearWalls.WriteSection(writer);
            InnerShearWalls.WriteSection(writer);
            CouplingsBeams.WriteSections(writer);
        }
        internal void WriteGravityRecorders(StreamWriter file)
        {
            CornerColumns.ColumnsRegion.WriteRecorders(file);
            OuterColumns.ColumnsRegion.WriteRecorders(file);
            InnerColumns.ColumnsRegion.WriteRecorders(file);
            CoreColumns.ColumnsRegion.WriteRecorders(file);

            //OuterShearWalls.WallsRegion.WriteRecorders(file);
            //InnerShearWalls.WallsRegion.WriteRecorders(file);
            CouplingsBeams.ClearElements();
        }
        internal void DesignGravityLoads(ReinforcementUtility ReinfUtil,double Cd, double FloorHeight,ConCreteMaterial shearWallMaterial,SteelMaterial MainSteel, SteelMaterial MildSteel, FloorsGroup previous)
        {
            int start = Floors.First().Index;
            int end = Floors.Last().Index;

            CornerColumns.DesignGravityLoads(ReinfUtil, Cd, FloorHeight, shearWallMaterial, MainSteel, MildSteel,previous?.CornerColumns.ColumnSection);
            Project.LogAction($"Corner Column:{start}-{end} {CornerColumns.ColumnSection}");
            
            OuterColumns.DesignGravityLoads(ReinfUtil, Cd, FloorHeight, shearWallMaterial, MainSteel, MildSteel,previous?.OuterColumns.ColumnSection);
            Project.LogAction($"Outer Column:{start}-{end} {OuterColumns.ColumnSection}");
            
            InnerColumns.DesignGravityLoads(ReinfUtil, Cd,FloorHeight, shearWallMaterial, MainSteel, MildSteel,previous?.InnerColumns.ColumnSection);
            Project.LogAction($"Inner Column:{start}-{end} {InnerColumns.ColumnSection}");
            
            CoreColumns.DesignGravityLoads(ReinfUtil, Cd, FloorHeight, shearWallMaterial, MainSteel, MildSteel,previous?.CoreColumns.ColumnSection);
            Project.LogAction($"Core Column:{start}-{end} {CoreColumns.ColumnSection}");
        }
        internal bool IsAllShearWallSafe(ReinforcementUtility utility, double FloorHeight,double BeamLength, ConCreteMaterial ShearWall, SteelMaterial MainSteel, SteelMaterial MildSteel)
        {
            return IsOuterShearWallAreSafe(utility,FloorHeight,ShearWall, MainSteel, MildSteel) 
                && IsInnerShearWallAreSafe(utility,FloorHeight,ShearWall, MainSteel, MildSteel)
                && IsCouplingBeamsAreSafe(utility,BeamLength, ShearWall, MainSteel, MildSteel);
        }
        private bool IsOuterShearWallAreSafe(ReinforcementUtility utility, double FloorHeight, ConCreteMaterial ShearWall, SteelMaterial MainSteel, SteelMaterial MildSteel)
        {
            return utility.IsShearWallSectionIsSafe(OuterShearWalls.ShearWallReinforcement,OuterShearWallsForces, FloorHeight, ShearWall,MainSteel,MildSteel);
        }
        private bool IsInnerShearWallAreSafe(ReinforcementUtility utility, double FloorHeight, ConCreteMaterial ShearWall, SteelMaterial MainSteel, SteelMaterial MildSteel)
        {
            return utility.IsShearWallSectionIsSafe(InnerShearWalls.ShearWallReinforcement, InnerShearWallsForces, FloorHeight, ShearWall, MainSteel, MildSteel);
        }
        private bool IsCouplingBeamsAreSafe(ReinforcementUtility Utility, double BeamLength, ConCreteMaterial ShearWall, SteelMaterial MainSteel, SteelMaterial MildSteel)
        {
            return Utility.IsSaveBeamSection(CouplingsBeams.CouplingBeamSection, BeamLength, InnerShearWalls.ShearWallReinforcement,InnerShearWallsForces.BV,ShearWall,MainSteel,MildSteel);
        }
        internal void DesignShearWalls(ReinforcementUtility reinforcementUtility, double InnerMaxLength, double innerIncrement, double OuterMaxLength, double outerIncrement, double floorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial mainReinMaterial, SteelMaterial mildSteelMaterial, FloorsGroup previousgroup)
        {
            DesignOuterShearWalls(reinforcementUtility, OuterMaxLength, floorHeight, shearWallMaterial,  mainReinMaterial,  mildSteelMaterial, previousgroup,outerIncrement);
            DesignInnerShearWalls(reinforcementUtility, InnerMaxLength, floorHeight, shearWallMaterial, mainReinMaterial, mildSteelMaterial, previousgroup,innerIncrement);
            CouplingsBeams.CouplingBeamSection = reinforcementUtility.GetCouplingBeamSection(shearWallMaterial, mainReinMaterial, mildSteelMaterial, InnerShearWalls.ShearWallReinforcement,InnerShearWallsForces.BV);
        }
        private void DesignOuterShearWalls(ReinforcementUtility ReinfUtil,double MaxLength, double floorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial mainReinMaterial, SteelMaterial mildSteelMaterial, FloorsGroup previousgroup, double increment)
        {
            if (previousgroup == null)
            {
                OuterShearWalls.ShearWallReinforcement.EnLarge(ReinfUtil, floorHeight, MaxLength, increment);
                OuterShearWalls.ShearWallReinforcement = ReinfUtil.GetShearWallSectionForFirstFloor(OuterShearWallsForces, MaxLength, floorHeight, shearWallMaterial, mainReinMaterial, mildSteelMaterial, OuterShearWalls.ShearWallReinforcement, increment);
            }
            else
            {
                OuterShearWalls.ShearWallReinforcement = ReinfUtil.GetShearWallSectionForNextFloor(OuterShearWallsForces, MaxLength, floorHeight, shearWallMaterial, mainReinMaterial, mildSteelMaterial, previousgroup.OuterShearWalls.ShearWallReinforcement, increment);
            }
        }
        private void DesignInnerShearWalls(ReinforcementUtility ReinfUtil, double MaxLength, double floorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial mainReinMaterial, SteelMaterial mildSteelMaterial, FloorsGroup previousgroup, double increment)
        {
            if (previousgroup == null)
            {
                InnerShearWalls.ShearWallReinforcement.EnLarge(ReinfUtil, floorHeight, MaxLength, increment);
                InnerShearWalls.ShearWallReinforcement = ReinfUtil.GetShearWallSectionForFirstFloor(InnerShearWallsForces, MaxLength, floorHeight, shearWallMaterial, mainReinMaterial, mildSteelMaterial, InnerShearWalls.ShearWallReinforcement, increment);
            }
            else
            {
                InnerShearWalls.ShearWallReinforcement = ReinfUtil.GetShearWallSectionForNextFloor(InnerShearWallsForces, MaxLength, floorHeight, shearWallMaterial, mainReinMaterial, mildSteelMaterial, previousgroup.InnerShearWalls.ShearWallReinforcement, increment);
            }
        }

        internal void MultiplyForces(double floorForceAmplificationFactor)
        {
            InnerShearWallsForces.Multiply(floorForceAmplificationFactor);
            OuterShearWallsForces.Multiply(floorForceAmplificationFactor);
        }
        internal double GetColumnsWeights(LayoutUtility layoutUtility, double BeamLength)
        {
            double FloorHeight = layoutUtility.FloorHeight;
            double Density = 24 * 1000;
            return CoreColumns.GetColumnsWeight(FloorHeight, Density) +
                CornerColumns.GetColumnsWeight(FloorHeight, Density) +
                OuterColumns.GetColumnsWeight(FloorHeight, Density) +
                InnerColumns.GetColumnsWeight(FloorHeight, Density) +
                OuterShearWalls.GetWallsWeight(2*layoutUtility.No_OuterWalls, FloorHeight, Density) +
                InnerShearWalls.GetWallsWeight(layoutUtility.No_InnerWalls, FloorHeight, Density) +
                CouplingsBeams.GetBeamsWeight(BeamLength, Density);
        }
        #endregion
    }
}
