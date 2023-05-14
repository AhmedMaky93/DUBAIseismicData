using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models.ModelMaterial;
using TempAnalysis.OpenseesCommand;
using TempAnalysis.Utilities;
using TempAnalysis.OpenSeesTranslator;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using Microsoft.VisualBasic.FileIO;

namespace TempAnalysis.Models
{
    public enum PointType
    {
        DefaultType,
        Fixed,
        MasterNode
    }
    public enum Layout
    {
        Layout1,
        Layout2,
    }
    [DataContract(IsReference = true)]
    public class LayoutUtility
    {
        [DataMember]
        public double FloorHeight = 4;
        [DataMember]
        public double MajorSpacing = 6;
        [DataMember]
        public int Spans = 5;
        [DataMember]
        public int AdditionalReinfSquares = 24; // 4 * 0.75 + 4 * 0.25 + 3 * 4 * 1 + 4 * 4 * 0.5
        // N/mm2
        [DataMember]
        public double PL = 0.359 * 1000; //0.72 * 1000;
        [DataMember]
        public double DL = (2.036 + 0.5) * 1000; //(3.6 + 0.5) * 1000;
        [DataMember]
        public double LL = 2.394 * 1000; //2.4 * 1000;
        [DataMember]
        public double RLL = 0.958 * 1000;// 1000;
        [DataMember]
        public List<Location> CornerColumnsLocations = new List<Location>();
        [DataMember]
        public List<Location> OuterColumnsLocations = new List<Location>();
        [DataMember]
        public List<Location> InnerColumnsLocations = new List<Location>();
        [DataMember]
        public List<Location> CoreColumnsLocations = new List<Location>();
        [DataMember]
        public int Min = 0;
        [DataMember]
        public int Max = 5;
        [DataMember]
        public double CoreOpeningLength = 8.0;
        [DataMember]
        public List<Location> HorizontalOuterWallsLocations = new List<Location>();
        [DataMember]
        public List<CoreWallLocation> InnerWallsLocations = new List<CoreWallLocation>();
        [DataMember]
        public double PlasticHingeLength = 0.05;
        [DataMember]
        public double InnerWallForceShare = 0.125;
        [DataMember]
        public double InnerWallMomentShare = 0.25;// 0.5 * 9.0 /34.0;
        [DataMember]
        public double OuterWallForceShare = 0.125;
        [DataMember]
        public double OuterWallMomentShare = 0.25;//0.5 * 25.0 / 34.0;
        [DataMember]
        public double MaxOuterWallLength = 9.00;
        [DataMember]
        public double OuterWallLengthIncrement = 0.5;
        [DataMember]
        public double MaxInnerWallLength = 3;
        [DataMember]
        public double InnerWallLengthIncrement = 0.5;
        [DataMember]
        public int NumberOfFloorPerGroup = 5;
        
        public GridSystem Grs;

        public int No_CornerColumns
        { get { return CornerColumnsLocations.Count; } }

        public int No_OuterColumns
        { get { return OuterColumnsLocations.Count; } }

        public int No_InnerColumns
        { get { return InnerColumnsLocations.Count; } }

        public int No_CoreColumns
        { get { return CoreColumnsLocations.Count; } }

        public int No_OuterWalls
        { get { return 2 * HorizontalOuterWallsLocations.Count; } }

        public int No_InnerWalls
        { get { return 2 *InnerWallsLocations.Count; } }
        public LayoutUtility()
        {
            CornerColumnsLocations = new List<Location>()
            {
                new Location(0,0),
                new Location(0,5),
                new Location(5,0),
                new Location(5,5)
            };
            OuterColumnsLocations = new List<Location>()
            {
                new Location(2,0),
                new Location(2,5),
                new Location(3,0),
                new Location(3,5),
                new Location(0,2),
                new Location(0,3),
                new Location(5,2),
                new Location(5,3),
            };
            InnerColumnsLocations = new List<Location>()
            {
                new Location(1,1),
                new Location(1,2),
                new Location(1,3),
                new Location(1,4),
                
                new Location(2,1),
                new Location(2,4),
                new Location(3,1),
                new Location(3,4),

                new Location(4,1),
                new Location(4,2),
                new Location(4,3),
                new Location(4,4),
            };
            CoreColumnsLocations = new List<Location>() 
            {
                new Location(2,2),
                new Location(2,3),
                new Location(3,2),
                new Location(3,3),
            };
            HorizontalOuterWallsLocations = new List<Location>()
            {
                new Location(1,0),
                new Location(1,5),
                new Location(4,0),
                new Location(4,5)
            };
            InnerWallsLocations = new List<CoreWallLocation>()
            {
                new CoreWallLocation (true,true),
                new CoreWallLocation (false,true),
                new CoreWallLocation (true,false),
                new CoreWallLocation (false,false)
            };
        }
        public double GetOuterShearWallsMomentArm()
        {
            return 2.5 * MajorSpacing;
        }
        public double GetInnerShearWallsMomentArm()
        {
            return 0.5 * CoreOpeningLength;
        }
        public double GetFloorWidth()
        {
            return MajorSpacing * Spans;
        }
        internal int GetSlabSpans()
        {
            return Spans * Spans - 1;
        }
        public double GetNetArea()
        {
            return Math.Pow(GetFloorWidth(), 2) - Math.Pow(CoreOpeningLength, 2);
        }
        internal double GetRotMassFactor()
        {
            return (Math.Pow(GetFloorWidth(), 2) - Math.Pow(CoreOpeningLength, 2)) / 6;
        }

        #region Walls Nodes Creation
        protected List<double> GetValuesList(double v1, double v2, double spacing)
        {
            List<double> values = new List<double>();
            double Tolerance = 1e-9;
            double v = v1;
            while (v < v2 || Math.Abs(v - v2) < Tolerance)
            {
                values.Add(v);
                v += spacing;
            }
            return values;
        }
        public List<double> GetFloorInnerDistances(int FloorIndex, double Increment, bool IncludeEnds = false)
        {
            double StartLevel = Grs.GetValue(2, FloorIndex - 1);
            double EndLevel = Grs.GetValue(2, FloorIndex);

            if (!IncludeEnds)
            {
                StartLevel += Increment;
                EndLevel -= Increment;
            }
            return GetValuesList(StartLevel, EndLevel, Increment);
        }
        protected List<List<BaseNode>> GetNodesForWallBetweenFloorLevel(Location Loc, int FloorIndex, double WallLength, bool Horizontal, bool IncludeEnds = false)
        {
            List<double> Distances = GetFloorInnerDistances(FloorIndex, OuterWallLengthIncrement, IncludeEnds);
            List<List<BaseNode>> Nodes = new List<List<BaseNode>>();
            Distances.ForEach(Dist => Nodes.Add(GetNodesForWallAtFloorLevel(Loc, NodeType.BaseNode,Dist,WallLength, Horizontal)));
            return Nodes;
        }
        public List<ShellElement> GetShellMesh(IDsManager IDM ,List<List<BaseNode>> Nodes)
        {
            List<ShellElement> Shells = new List<ShellElement>();
            for (int i = 1; i < Nodes.Count; i++)
            {
                List<BaseNode> BottomRow = Nodes[i - 1];
                List<BaseNode> TopRow = Nodes[i];

                for (int j = 1; j < BottomRow.Count; j++)
                {
                    List<BaseNode> ShellNodes = new List<BaseNode>
                    {
                        BottomRow[j-1],
                        BottomRow[j],
                        TopRow[j],
                        TopRow[j-1]
                    };
                    Shells.Add(new ShellElement(IDM, ShellNodes));
                }
            }
            return Shells;
        }
        public List<ShellElement> GetShellsForWallBetweenFloorLevel(Location Loc, int FloorIndex, double WallLength, bool Horizontal)
        {
            return GetShellMesh(Grs.IDM, GetNodesForWallBetweenFloorLevel(Loc, FloorIndex, WallLength, Horizontal, true));
        }
        public List<BaseNode> GetNodesForWall(Location Loc, int LevelIndex, double WallLength, bool Horizontal)
        {
            return GetNodesForWall(Loc.ToPoint(Grs),LevelIndex == 0? NodeType.FixedNode: NodeType.BaseNode,Grs.GetValue(2,LevelIndex),WallLength,Horizontal);
        }
        protected List<BaseNode> GetNodesForWall(Point2D point, NodeType nodeType, double Z, double WallLength, bool Horizontal)
        {
            double x0 = point.X;
            double y0 = point.Y;

            double MaxLength = WallLength / 2;
            List<double> NodesDistances = GetValuesList(-MaxLength, MaxLength, OuterWallLengthIncrement);
            List<BaseNode> NodesList = new List<BaseNode>();
            if (Horizontal)
                NodesDistances.ForEach(Distance => NodesList.Add(Grs.AddNode(nodeType, x0 + Distance, y0, Z)));
            else
                NodesDistances.ForEach(Distance => NodesList.Add(Grs.AddNode(nodeType, x0, y0 + Distance, Z)));

            return NodesList;
        }
        protected List<BaseNode> GetNodesForWallAtFloorLevel(Location Loc, NodeType nodeType, double Z, double WallLength, bool Horizontal)
        {
            return GetNodesForWall(Loc.ToPoint(Grs), nodeType, Z, WallLength, Horizontal);
        }
        public List<BaseNode> GetNodesForAllOuterShearWallAtCertainLevel(int LevelIndex)
        {
            NodeType nodeType = LevelIndex == 0 ? NodeType.FixedNode : NodeType.BaseNode;
            double Z = Grs.GetValue(2, LevelIndex);
            List<BaseNode> Nodes = new List<BaseNode>();
            HorizontalOuterWallsLocations.ForEach(Loc =>
            {
                Nodes.Add(Loc.ToNode(Grs, LevelIndex));
                Nodes.Add(Loc.Mirror().ToNode(Grs, LevelIndex));
            });
            return Nodes;
        }
        public List<BaseNode> GetNodesForAllOuterShearWallAtCertainLevel(int LevelIndex, double WallLength)
        {
            return GetNodesForAllOuterShearWallAtCertainElevation(Grs.GetValue(2, LevelIndex)
                , LevelIndex == 0 ? NodeType.FixedNode : NodeType.BaseNode, WallLength);
        }
        public List<BaseNode> GetInnerNodesForAllOuterShearWallAtCertainFloor(int LevelIndex, double WallLength)
        {
            List<BaseNode> Nodes = new List<BaseNode>();
            GetFloorInnerDistances(LevelIndex,OuterWallLengthIncrement).ForEach(Z => Nodes.AddRange(GetNodesForAllOuterShearWallAtCertainElevation(Z, NodeType.BaseNode, WallLength)));
            return Nodes;
        }
        public List<BaseNode> GetNodesForAllOuterShearWallAtCertainElevation(double Z, NodeType nodeType, double WallLength)
        {
            List<BaseNode> Nodes = new List<BaseNode>();
            HorizontalOuterWallsLocations.ForEach(Loc =>
            {
                Nodes.AddRange(GetNodesForWallAtFloorLevel(Loc, nodeType, Z, WallLength, true));
                Nodes.AddRange(GetNodesForWallAtFloorLevel(Loc.Mirror(), nodeType, Z, WallLength, false));
            });
            return Nodes;
        }

        #endregion

        #region CoreShearWall
        internal List<BaseNode> GetCoreShearWallsColumnNodes(int LevelIndex, double BeamLength)
        {
            NodeType nodeType = LevelIndex == 0 ? NodeType.FixedNode : NodeType.BaseNode;
            List<BaseNode> Nodes = new List<BaseNode>();
            InnerWallsLocations.ForEach(loc => Nodes.AddRange(loc.GetWallsNodes(CoreOpeningLength,BeamLength).Select(p=>p.ToNode(Grs, nodeType, LevelIndex))));
            return Nodes;
        }
        internal List<BaseNode> GetCoreShearWallsLevelNodes(int LevelIndex, double BeamLength)
        {
            NodeType nodeType = LevelIndex == 0 ? NodeType.FixedNode : NodeType.BaseNode;
            List<BaseNode> Nodes = new List<BaseNode>();
            InnerWallsLocations.ForEach(loc =>
            {
                List<Point2D> points = new List<Point2D>();
                points.AddRange(loc.GetWallInnerLocations(CoreOpeningLength, BeamLength));
                points.AddRange(loc.GetWallsNodes(CoreOpeningLength, BeamLength));
                if (loc.Horizontal)
                    points.AddRange(loc.GetCornerNodes(CoreOpeningLength));
                Nodes.AddRange(points.Select(p => p.ToNode(Grs, nodeType, LevelIndex)));
            });
            return Nodes;
        }
        protected List<double> GetCoreShearWallNodesDistances(double HalfbeamLength)
        {
            return GetValuesList(HalfbeamLength, CoreOpeningLength / 2, InnerWallLengthIncrement);
        }
        public List<List<BaseNode>> GetNodeNodesOfCoreWallAtCertainElevation(CoreWallLocation Location, double Z, NodeType nodeType, List<double> Distances)
        {
            // Two List for the right and left of openeing
            List<List<BaseNode>> AllNodes = new List<List<BaseNode>>();
            Location.GetNodesPointsSymmetric(CoreOpeningLength, Distances).ForEach(Points =>
            {
                AllNodes.Add(Points.Select(p=> Grs.AddNode(nodeType,p.X,p.Y, Z)).ToList());
            });
            return AllNodes;
        }
        public List<ShellElement> GetShellsForShearWall(CoreWallLocation Loc, int LevelIndex, double BeamLength)
        {
            List<List<BaseNode>> Nodes1 = new List<List<BaseNode>>();
            List<List<BaseNode>> Nodes2 = new List<List<BaseNode>>();
            GetFloorInnerDistances(LevelIndex, InnerWallLengthIncrement, true).ForEach(dist => 
            GetNodesSeparatedForAllCoreShearWallAtCertainElevation(Loc,Nodes1, Nodes2,dist,BeamLength));
            return GetShellMesh(Grs.IDM,Nodes1).Concat(GetShellMesh(Grs.IDM, Nodes2)).ToList();
        }
        public List<BaseNode> GetInnerNodesForAllCoreShearWallAtCertainFloor(int LevelIndex, double BeamLength)
        {
            List<BaseNode> Nodes = new List<BaseNode>();
            GetFloorInnerDistances(LevelIndex,InnerWallLengthIncrement).ForEach(Z => Nodes.AddRange(GetNodesForAllCoreShearWallAtCertainElevation(Z, NodeType.BaseNode, BeamLength)));
            return Nodes;
        }
        public List<BaseNode> GetNodesForAllCoreShearWallAtCertainLevel(int LevelIndex, double BeamLength)
        {
            return GetNodesForAllCoreShearWallAtCertainElevation(Grs.GetValue(2, LevelIndex)
                , LevelIndex == 0 ? NodeType.FixedNode : NodeType.BaseNode, BeamLength);
        }
        public void GetNodesSeparatedForAllCoreShearWallAtCertainElevation(CoreWallLocation Loc,List<List<BaseNode>> Nodes1, List<List<BaseNode>> Nodes2, double Z, double BeamLength)
        {
            List<double> Distances = GetCoreShearWallNodesDistances(BeamLength/2);
            List<List<BaseNode>> Nodes = GetNodeNodesOfCoreWallAtCertainElevation(Loc, Z, NodeType.BaseNode,Distances);
            Nodes1.Add(Nodes[0]);
            Nodes2.Add(Nodes[1]);
        }
        public List<BaseNode> GetNodesForAllCoreShearWallAtCertainElevation(double Z, NodeType nodeType, double BeamLength)
        {
            List<BaseNode> Nodes = new List<BaseNode>();
            List<double> Horizontal_Distances = GetCoreShearWallNodesDistances(BeamLength/2);
            List<double> Vertical_Distances = new List<double>(Horizontal_Distances);
            Vertical_Distances.RemoveAt(Vertical_Distances.Count-1);
            InnerWallsLocations.ForEach(Loc =>
            {
                GetNodeNodesOfCoreWallAtCertainElevation(Loc, Z, nodeType, Loc.Horizontal ? Horizontal_Distances : Vertical_Distances).ForEach( Points => Nodes.AddRange(Points));
            });
            return Nodes;
        }
        public List<List<BaseNode>> GetCornerNodesForCoreShearWallBeamsAtCertainElevation(double BeamLength, int LevelIndex)
        {
            NodeType nodeType = LevelIndex == 0 ? NodeType.FixedNode : NodeType.BaseNode;
            double Z = Grs.GetValue(2, LevelIndex);

            List<List<BaseNode>> Nodes = new List<List<BaseNode>>();
            List<double> Distances = GetCoreShearWallNodesDistances(BeamLength/2);
            InnerWallsLocations.ForEach(Loc =>
            {
                GetNodeNodesOfCoreWallAtCertainElevation(Loc, Z, nodeType, Distances).ForEach(Points => Nodes.Add(Points));
            });
            return Nodes;
        }

        public List<BaseNode> GetCornerNodesForAllCoreShearWallAtCertainElevation(double BeamLength, int LevelIndex)
        {
            NodeType nodeType = LevelIndex == 0 ? NodeType.FixedNode : NodeType.BaseNode;
            double Z = Grs.GetValue(2, LevelIndex);

            List<BaseNode> Nodes = new List<BaseNode>();
            List<double> Horizontal_Distances = new List<double> { BeamLength/2, CoreOpeningLength /2 };
            List<double> Vertical_Distances = new List<double> { BeamLength/2};
            InnerWallsLocations.ForEach(Loc =>
            {
                GetNodeNodesOfCoreWallAtCertainElevation(Loc, Z, nodeType,Loc.Horizontal? Horizontal_Distances: Vertical_Distances).ForEach(Points => Nodes.AddRange(Points));
            });
            return Nodes;
        }

        #endregion

        #region ColumnsNodes
        
        internal List<BaseNode> GetAllColumnsNodesAtLevel(int Index)
        {
            NodeType nodeType = Index == 0 ? NodeType.FixedNode : NodeType.BaseNode;

            List<BaseNode> NodesList = new List<BaseNode>();
            CornerColumnsLocations.ForEach(x => NodesList.Add(x.ToNode(Grs, Index)));
            OuterColumnsLocations.ForEach(x => NodesList.Add(x.ToNode(Grs, Index)));
            InnerColumnsLocations.ForEach(x => NodesList.Add(x.ToNode(Grs, Index)));
            CoreColumnsLocations.ForEach(x => NodesList.Add(x.ToNode(Grs, Index)));
            return NodesList;
        }
        internal List<double> GetInterDisatances(double BeamLength)
        {
            double f1 = BeamLength / 2;
            double f3 = CoreOpeningLength / 2;
            double f2 = (f1+ f3) / 2;
            return new List<double> { f1 , f2 , f3 };
        }
        internal List<double> GetGridIntermediateDistances(double BeamLength)
        {
            List<double> AllDistances = new List<double>() { 0.0 };
            List<double> SideDistances = GetInterDisatances(BeamLength);
            AllDistances.AddRange(SideDistances);
            AllDistances.AddRange(SideDistances.Select(x=> -x));
            AllDistances.Sort();
            return AllDistances;
        }
        #endregion

    }

    [DataContract(IsReference = true)]
    public class IDsManager
    {
        [DataMember]
        public long LastNodeId=0;
        [DataMember]
        public long LastLineId=0;
        [DataMember]
        public long LastRegionId=0;
        [DataMember]
        public long LastElementId=0;
        [DataMember]
        public long LastMaterialId=0;
        [DataMember]
        public long LastSectionId=0;
        [DataMember]
        public long LastLoadCaseId=0;
        
        public IDsManager()
        {

        }
        public void Clear()
        {
           LastNodeId =0;
           LastLineId=0;
           LastRegionId=0;
           LastElementId=0;
           LastMaterialId=0;
           LastSectionId=0;
           LastLoadCaseId = 0;
        }
    }
    [DataContract(IsReference = true)]
    public class ResponseParameters
    {
        [DataMember]
        public double R = 6;
        [DataMember]
        public double Ie= 1;
        [DataMember]
        public double SDs = 1.1;
        [DataMember]
        public double SD1 = 0.74;
        [DataMember]
        public double Cd = 5;
        // Ss = 1.65 , S1 = 0.65
        // Fa = 1 , Fb = 1.7
        // SDS = 2/3 * Fa * Ss = 1.1
        // SD1 = 2/3 * Fb * S1 = 0.74
        public ResponseParameters()
        {

        }
        public double GetSa(double T)
        {
            double Ts = SD1 / SDs;
            double T0 = 0.2 * Ts;
            if (T < T0)
                return SDs*(0.4+0.6*T/T0);
            else if (T >= T0 && T <= Ts)
                return SDs;
            else
                return SD1/T;
        }
        public double GetCs()
        {
            return 0;
        }


    }
    public interface IModalAnalysisModel
    {
        string _Name { get; }
        MasterNodesRegion _MasterNodesRegion { get; set; }
        BaseNodesRegion _BaseNodesRegion { get; set; }
        PushOverResults _PushOverResults { get; set; }
        int _NumOfFloors { get; }
        List<ModeShapeData> _ModeShapes { get; set; }
        ResponseParameters _ResponseParameters { get; set; }
        void MoveOpenseesFiles(string ModelFolderPath);
        int GetNumOfModeShapes();
        void WriteNodes(StreamWriter file);
        void WriteMateials(StreamWriter file);
        void WriteFrameElements(StreamWriter file, bool Elastic);
        void WriteWallsElements(StreamWriter file, bool Elastic);
        void PostCalculateFloorForces();
        void WriteLoadSurfacesCommands(StreamWriter file, LoadCombinationFactors factors);
        double GetH();
        long GetRoofNodeID();
        List<IStructuralFloor> GetModelFloors();
        List<double> GetModalForces(List<ModeShapeData> _ModeShapes);

    }

    [DataContract(IsReference = true)]
    public class DetailedModel : IModalAnalysisModel
    {
        [DataMember]
        public LayoutUtility LayoutUtility;
        [DataMember]
        public ReinforcementUtility ReinforcementUtility;
        [DataMember]
        public UnitsUtility UnitsUtility;
        [DataMember]
        public OpenSeesTranslator.OpenSeesTranslator OpenSeesTranslator;
        [DataMember]
        public IDsManager IDsManager;
        [DataMember]
        public string Name;
        [DataMember]
        public GridSystem Gridsystem;
        [DataMember]
        public int NumOfFloors;
        [DataMember]
        public ModelEstimationEntity CostEstimation;
        [DataMember]
        public SlabSection SlabSection;
        [DataMember]
        public ModelMaterialsInfo MateialsInfo;
        [DataMember]
        public List<FloorsGroup> FloorsGroups = new List<FloorsGroup>();
        [DataMember]
        public BaseNodesRegion BaseNodesRegion;
        [DataMember]
        public List<FixedNode> BaseLevelNodes = new List<FixedNode>();
        [DataMember]
        public MasterNodesRegion MasterNodesRegion;
        [DataMember]
        public ResponseParameters ResponseParameters = new ResponseParameters();
        [DataMember]
        public List<ModeShapeData> ModeShapes = new List<ModeShapeData>();
        [DataMember]
        public double BeamLength = 1.00;
        [DataMember]
        public PushOverResults PushOverResults;
        [DataMember]
        public List<PushOverResults> AllPushOverResults = new List<PushOverResults>();
        [DataMember]
        public List<double> FloorDrifts = new List<double>();
        [DataMember]
        public double FloorForceAmplificationFactor = 1.00;
        [DataMember]
        public int[] MassFactors = new int[3];

        public List<ModeShapeData> _ModeShapes
        {
            get { return ModeShapes; }
            set { ModeShapes = value; }
        }
        public ResponseParameters _ResponseParameters
        {
            get { return ResponseParameters; }
            set { ResponseParameters = value; }
        }
        public MasterNodesRegion _MasterNodesRegion
        {
            get { return MasterNodesRegion; }
            set { MasterNodesRegion = value; }
        }
        public BaseNodesRegion _BaseNodesRegion
        {
            get { return BaseNodesRegion; }
            set { BaseNodesRegion = value; }
        }
        public int _NumOfFloors
        {
            get { return NumOfFloors; }
        }
        public string _Name
        {
            get { return Name; }
        }
        public bool _3DModel
        {
            get
            {
                return MassFactors.Count(x => x == 1) > 1;
            }
        }
        public PushOverResults _PushOverResults
        {
            get { return PushOverResults; }
            set { PushOverResults = value; }
        }
        public DetailedModel()
        {

        }
        public DetailedModel(string Name, int NumOfFloors, ModelMaterialsInfo MateialsInfo, LayoutUtility LayoutUtility)
        {
            this.Name = Name;
            Project.LogAction($"Creating Model:{Name}");
            this.NumOfFloors = NumOfFloors;
            this.MateialsInfo = MateialsInfo;

            this.LayoutUtility = LayoutUtility;
            ReinforcementUtility = new ReinforcementUtility();
            UnitsUtility = new UnitsUtility();
            OpenSeesTranslator = new OpenSeesTranslator.OpenSeesTranslator();
            IDsManager = new IDsManager();
            MassFactors = new int[] { 1, 1, 1 };
        }
        public List<double> GetModalForces(List<ModeShapeData> ModeShapes)
        {
            return GetModelFloors().Select(x => x.GetDesginFloorForce(ModeShapes)).ToList();
        }
        public List<ModeShapeData> GetXModeSahpes()
        {
            return ModeShapes.Where(x => x.Mx > 1e-9).OrderByDescending(x => x.Mx).ToList();
        }
        public double GetT1_T2Ratio()
        {
            var ModeShapes = GetXModeSahpes();
            return ModeShapes[0].Period / ModeShapes[1].Period;
        }
        public void MoveOpenseesFiles(string ModelFolderPath)
        {
            string exepath = Assembly.GetExecutingAssembly().Location;
            string openseesPath = Path.Combine(Path.Combine(Path.GetDirectoryName(exepath), "OpenSees"));

            string directoryName = Path.Combine(ModelFolderPath, "OpenSees");
            FileSystem.CopyDirectory(openseesPath, directoryName);
            this.OpenSeesTranslator.FolderPath = Path.Combine(Path.Combine(directoryName, "bin"));
        }
        public string GetModelDirPath(string FolderPath)
        {
            string ModelFolderPath = Path.Combine(FolderPath, Name);
            DirectoryInfo dir = new DirectoryInfo(ModelFolderPath);
            if (!dir.Exists)
                dir.Create();
            return ModelFolderPath;
        }
        public List<IStructuralFloor> GetModelFloors()
        {
            return FloorsGroups.SelectMany(x => x.Floors).Cast<IStructuralFloor>().ToList();
        }
        public void InitModel(string FolderPath, DetailedModel preModel)
        {
            string ModelFolderPath = GetModelDirPath(FolderPath);
            MoveOpenseesFiles(ModelFolderPath);
            Project.LogAction($"{Name}: Folder Created");
            InitSections(preModel);
            Project.LogAction($"{Name}: Grid System Created");
        }
        public void GenerateGridSystem()
        {
            double halfWidth = LayoutUtility.GetFloorWidth() / 2;
            Gridsystem = new GridSystem(NumOfFloors, LayoutUtility.FloorHeight, IDsManager, -halfWidth, halfWidth
                , LayoutUtility.MajorSpacing, LayoutUtility.GetGridIntermediateDistances(BeamLength));
            LayoutUtility.Grs = Gridsystem;
        }
        public async Task RunLateralLoads()
        {
            ReCreateOpenSeesElements();
            OpenSeesTranslator.RunModel(this, new DriftAnalysis(IDsManager));
        }

        public async Task SolveGravityLoad()
        {
            ReCreateOpenSeesElements();
            OpenSeesTranslator.RunModel(this, new GravityLoadCase(IDsManager));
        }
        public async Task RunModalAnalysis(bool Elastic = true)
        {
            ReCreateOpenSeesElements();
            OpenSeesTranslator.RunModel(this, new ModalAnalysisLoadCase(IDsManager, Elastic));
        }
        public async Task RunPushoverResults()
        {
            ReCreateOpenSeesElements();
            AllPushOverResults = new List<PushOverResults>();
            double[] K_Values = new double[] { -1, 1, 2 };
            //double[] K_Values =  new double[] { 1.0 };
            foreach (double K in K_Values)
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                OpenSeesTranslator.RunModel(this, new PushOverLoadCase(IDsManager, K));
                timer.Stop();
                this.PushOverResults.Time = timer.ElapsedMilliseconds;
                AllPushOverResults.Add(PushOverResults);
            }
            this.PushOverResults = AllPushOverResults.OrderBy(x => x.PeakStrength).First();
            //this.PushOverResults = AllPushOverResults[0];
        }
        public int GetNumOfModeShapes()
        {
            return _3DModel ? 10 : 4;
        }
        public async Task DesignColumnsForGravityLoads(string FolderPath, bool TestSet, DetailedModel PreviousModel)
        {
            InitModel(FolderPath, PreviousModel);
            GravityLoadCase cases = new GravityLoadCase(IDsManager);
            FloorsGroups.ForEach(fg => fg.DefineColumnsDesignForces(GetFloorSpanPAccumlative(fg.Floors.First().Index, cases.LoadCombinationsFactors)));
            if (TestSet)
            {
                SetTestModelsColumnsSection();
                return;
            }
            double Cd = ResponseParameters.Cd;
            FloorsGroup Previousgroup = null;
            for (int i = 0; i < FloorsGroups.Count; i++)
            {
                FloorsGroups[i].DesignGravityLoads(this.ReinforcementUtility, Cd, LayoutUtility.FloorHeight, MateialsInfo.ColumnMaterial, MateialsInfo.MainReinMaterial, MateialsInfo.MildSteelMaterial, Previousgroup);
                Previousgroup = FloorsGroups[i];
            }
        }
        private void SetTestModelsColumnsSection()
        {
            List<SquareColumnSection[]> sections = new List<SquareColumnSection[]>();
            switch (_NumOfFloors)
            {
                case 6:
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.711,16,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,12,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,16,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,20,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    break;
                case 9:
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.711,16,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,12,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,16,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,20,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    break;
                case 12:
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.864,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.864,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.864,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.864,12,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(9),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.813,12,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.762,8,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    sections.Add(new SquareColumnSection[] {
                        new SquareColumnSection(0.711,16,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,12,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,16,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                        new SquareColumnSection(0.711,20,ReinforcementUtility.GetSteelBarByID(10),5,ReinforcementUtility.GetSteelBarByID(4)),
                    });
                    break;
            }
            SetColumnsSections(sections);
        }
        public void SetColumnsSections(List<SquareColumnSection[]> sections)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                FloorsGroups[i].CornerColumns.ColumnSection = sections[i][0];
                FloorsGroups[i].OuterColumns.ColumnSection = sections[i][1];
                FloorsGroups[i].InnerColumns.ColumnSection = sections[i][2];
                FloorsGroups[i].CoreColumns.ColumnSection = sections[i][3];
            }
        }

        public void DesignShearWalls()
        {
            FloorsGroup Previousgroup = null;
            for (int i = 0; i < FloorsGroups.Count; i++)
            {
                FloorsGroups[i].DesignShearWalls(ReinforcementUtility, LayoutUtility.MaxInnerWallLength, LayoutUtility.InnerWallLengthIncrement, LayoutUtility.MaxOuterWallLength,
                    LayoutUtility.OuterWallLengthIncrement, LayoutUtility.FloorHeight, MateialsInfo.ShearWallMaterial, MateialsInfo.MainReinMaterial, MateialsInfo.MildSteelMaterial, Previousgroup);
                Previousgroup = FloorsGroups[i];
            }
            ResetCouplingBeamLength();
        }
        internal async Task CalculateShearWallLengthes(string folderpath, bool TestSet)
        {
            if (TestSet)
            {
                SetTestModelsShearWallSections();
                await RunModalAnalysis();
                return;
            }
            int i = 1;
            while (true)
            {
                Project.LogAction($"{Name},ShearWalls, Lateral Loads, Trial: {i}");
                await RunModalAnalysis();
                if (IsAllShearWallSafe())
                {
                    FloorsGroups[0].ConsoleWriteShearWallSection(BeamLength);
                    break;
                }
                DesignShearWalls();
                i++;
            }
            if (!TestSet)
            {
                while (true)
                {
                    Project.LogAction($"{Name},ShearWalls, Lateral Loads, Trial: {i}");
                    await RunLateralLoads();
                    if (IsAllShearWallSafe())
                    {
                        FloorsGroups[0].ConsoleWriteShearWallSection(BeamLength);
                        break;
                    }
                    DesignShearWalls();
                    i++;
                }
            }
        }
        private void SetTestModelsShearWallSections()
        {
            List<SpecialShearWallReinforcement> sections = new List<SpecialShearWallReinforcement>();

            switch (_NumOfFloors)
            {
                case 6:
                    sections.Add(new SpecialShearWallReinforcement(4.6, 0.41, new ShellReinforcement(10, ReinforcementUtility.GetSteelBarByID(9)), new ShellReinforcement(8, ReinforcementUtility.GetSteelBarByID(6))));
                    sections.Add(new SpecialShearWallReinforcement(4.6, 0.36, new ShellReinforcement(8, ReinforcementUtility.GetSteelBarByID(7)), new ShellReinforcement(5, ReinforcementUtility.GetSteelBarByID(5))));
                    break;
                case 9:
                    sections.Add(new SpecialShearWallReinforcement(5.8, 0.51, new ShellReinforcement(10, ReinforcementUtility.GetSteelBarByID(9)), new ShellReinforcement(6, ReinforcementUtility.GetSteelBarByID(6))));
                    sections.Add(new SpecialShearWallReinforcement(5.8, 0.46, new ShellReinforcement(8, ReinforcementUtility.GetSteelBarByID(9)), new ShellReinforcement(5, ReinforcementUtility.GetSteelBarByID(6))));
                    sections.Add(new SpecialShearWallReinforcement(5.8, 0.41, new ShellReinforcement(6, ReinforcementUtility.GetSteelBarByID(6)), new ShellReinforcement(5, ReinforcementUtility.GetSteelBarByID(6))));
                    break;
                case 12:
                    sections.Add(new SpecialShearWallReinforcement(7, 0.61, new ShellReinforcement(10, ReinforcementUtility.GetSteelBarByID(9)), new ShellReinforcement(5, ReinforcementUtility.GetSteelBarByID(6))));
                    sections.Add(new SpecialShearWallReinforcement(7, 0.56, new ShellReinforcement(10, ReinforcementUtility.GetSteelBarByID(7)), new ShellReinforcement(5, ReinforcementUtility.GetSteelBarByID(6))));
                    sections.Add(new SpecialShearWallReinforcement(7, 0.51, new ShellReinforcement(8, ReinforcementUtility.GetSteelBarByID(7)), new ShellReinforcement(5, ReinforcementUtility.GetSteelBarByID(6))));
                    sections.Add(new SpecialShearWallReinforcement(7, 0.46, new ShellReinforcement(6, ReinforcementUtility.GetSteelBarByID(5)), new ShellReinforcement(5, ReinforcementUtility.GetSteelBarByID(5))));
                    break;
            }
            SetShearWallsSections(sections);
        }
        public void SetShearWallsSections(List<SpecialShearWallReinforcement> sections)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                FloorsGroups[i].OuterShearWalls.ShearWallReinforcement = sections[i];
            }
        }
        public void CreateAllOpensSeesElements()
        {
            double OuterShearWallLength = GetOuterShearWallLength();
            double ShearWallLength = GetInnerShearWallLength();
            GenerateGridSystem();
            CreateModelNodes();
            MateialsInfo.CreateMaterialsCommands(IDsManager);

            Concrete02Material ShearWallMaterial = MateialsInfo.DefaultConcreteMaterial;
            Concrete02Material ColumnMaterial = MateialsInfo.ColumnConcreteMaterial;
            SteelUniAxialMaterial SteelMaterial = MateialsInfo.MainSteelMaterial;

            FloorsGroups.ForEach(fg => fg.CreateColumns(LayoutUtility, ColumnMaterial, SteelMaterial));
            FloorsGroups.ForEach(fg => fg.CreateWallsElements(LayoutUtility, ShearWallMaterial, SteelMaterial, ShearWallLength, BeamLength));
            FloorsGroups.ForEach(fg => fg.CreatBeams(LayoutUtility, ShearWallMaterial, SteelMaterial, BeamLength));
            Project.LogAction($"{Name}: Model Elements Created.");
        }
        public double GetOuterShearWallLength()
        {
            if (!LayoutUtility.HorizontalOuterWallsLocations.Any())
                return 0;
            return FloorsGroups[0].OuterShearWalls.ShearWallReinforcement.Length;
        }

        public double GetInnerShearWallFullLength()
        {
            return 2 * GetInnerShearWallLength() + BeamLength;
        }
        public double GetInnerShearWallLength()
        {
            if (!LayoutUtility.InnerWallsLocations.Any())
                return 0;
            return FloorsGroups[0].InnerShearWalls.ShearWallReinforcement.Length;
        }
        public void ResetCouplingBeamLength()
        {
            if (!LayoutUtility.InnerWallsLocations.Any())
                BeamLength = 0;
            else
                BeamLength = (LayoutUtility.CoreOpeningLength - 2 * GetInnerShearWallLength());
        }
        private void CreateModelNodes()
        {
            // base nodes
            BaseLevelNodes.AddRange(LayoutUtility.GetAllColumnsNodesAtLevel(0).Cast<FixedNode>());
            BaseLevelNodes.AddRange(LayoutUtility.GetNodesForAllOuterShearWallAtCertainLevel(0).Cast<FixedNode>());
            BaseLevelNodes.AddRange(LayoutUtility.GetCoreShearWallsColumnNodes(0, BeamLength).Cast<FixedNode>());

            BaseNodesRegion = new BaseNodesRegion("BaseNodesRegion", this.IDsManager,
                        BaseLevelNodes.First().ID, BaseLevelNodes.Last().ID);

            double RotMassFactor = LayoutUtility.GetRotMassFactor();
            LoadCombinationFactors factors = new LoadCombinationFactors(1, 0, 0);
            FloorsGroups.ForEach(fg => fg.Floors.ForEach(f => f.CreateSlabMeshNodes(LayoutUtility, BeamLength)));
            FloorsGroups.ForEach(fg => fg.Floors.ForEach(f => f.CreateMasterNode(Gridsystem, GetFloorMass(f.Index), RotMassFactor, MassFactors)));
            MasterNodesRegion = new MasterNodesRegion("MasterNodesRegion", this.IDsManager
                , FloorsGroups.First().Floors.First().MasterNode.ID
                , FloorsGroups.Last().Floors.Last().MasterNode.ID);
        }
        private double GetPerimeterPointLoad(double Length, int floorIndex, LoadCombinationFactors factors)
        {
            if (floorIndex == NumOfFloors)
                return 0;
            return -LayoutUtility.FloorHeight * LayoutUtility.PL * Length * (factors.D - GetEVFactor(factors));
        }
        private double GetPerimeterLoadOnFloor(int floorIndex, LoadCombinationFactors factors)
        {
            double Length = 4 * (LayoutUtility.CoreOpeningLength + LayoutUtility.GetFloorWidth());
            return GetPerimeterPointLoad(Length, floorIndex, factors);
        }
        private double GetFloorSpanP(double AreaCovered, int floorIndex, LoadCombinationFactors factors)
        {
            return -AreaCovered * GetDistributedLoads(floorIndex, factors);// N/m2
        }
        private double GetCornerColumnFloorLoad(int floorIndex, LoadCombinationFactors factors)
        {
            double CornerArea = 0.25 * Math.Pow(LayoutUtility.MajorSpacing, 2);
            double WallLength = LayoutUtility.MajorSpacing;
            return GetFloorSpanP(CornerArea, floorIndex, factors) + GetPerimeterPointLoad(WallLength, floorIndex, factors);
        }
        private double GetOuterColumnFloorLoad(int floorIndex, LoadCombinationFactors factors)
        {
            double OuterArea = 0.5 * Math.Pow(LayoutUtility.MajorSpacing, 2);
            double WallLength = LayoutUtility.MajorSpacing;
            return GetFloorSpanP(OuterArea, floorIndex, factors) + GetPerimeterPointLoad(WallLength, floorIndex, factors);
        }
        private double GetInnerColumnFloorLoad(int floorIndex, LoadCombinationFactors factors)
        {
            double InnerArea = Math.Pow(LayoutUtility.MajorSpacing, 2);
            return GetFloorSpanP(InnerArea, floorIndex, factors);
        }
        private double GetCoreColumnFloorLoad(int floorIndex, LoadCombinationFactors factors)
        {
            double WallLength = (Math.Max(LayoutUtility.CoreOpeningLength, LayoutUtility.MajorSpacing));
            double L2 = 0.5 * (LayoutUtility.MajorSpacing - Math.Abs(WallLength - LayoutUtility.MajorSpacing) / 2.00);
            double L1 = WallLength / 2.00;
            double CoreColumnArea = Math.Pow(L2, 2) + 2 * L1 * L2;
            return GetFloorSpanP(CoreColumnArea, floorIndex, factors);
        }
        private List<double>[] GetFloorSpanPAccumlative(int floorIndex, List<LoadCombinationFactors> factors)
        {
            List<double>[] result = new List<double>[4];
            result[0] = factors.Select(fac => GetCornerColumnFloorLoad(NumOfFloors, fac) + (NumOfFloors - floorIndex) * GetCornerColumnFloorLoad(floorIndex, fac)).ToList();
            result[1] = factors.Select(fac => GetOuterColumnFloorLoad(NumOfFloors, fac) + (NumOfFloors - floorIndex) * GetOuterColumnFloorLoad(floorIndex, fac)).ToList();
            result[2] = factors.Select(fac => GetInnerColumnFloorLoad(NumOfFloors, fac) + (NumOfFloors - floorIndex) * GetInnerColumnFloorLoad(floorIndex, fac)).ToList();
            result[3] = factors.Select(fac => GetCoreColumnFloorLoad(NumOfFloors, fac) + (NumOfFloors - floorIndex) * GetCoreColumnFloorLoad(floorIndex, fac)).ToList();
            return result;
        }
        public double GetEVFactor(LoadCombinationFactors factors)
        {
            return factors.E * 0.2 * ResponseParameters.SDs * ResponseParameters.R / ResponseParameters.Ie;
        }
        private double GetDistributedLoads(int floorIndex, LoadCombinationFactors factors)
        {
            if (floorIndex == NumOfFloors)
                return LayoutUtility.DL * (factors.D - GetEVFactor(factors)) + LayoutUtility.RLL * factors.L;
            else
                return LayoutUtility.DL * (factors.D - GetEVFactor(factors)) + LayoutUtility.LL * factors.L;
        }
        public double GetFloorMass(int floorIndex)
        {
            LoadCombinationFactors factors = new LoadCombinationFactors(1, 0, 0);
            return (LayoutUtility.GetNetArea() * GetDistributedLoads(floorIndex, factors)
                 + GetPerimeterLoadOnFloor(floorIndex, factors)) / 9.81;// N/m2
        }
        public double GetFloorMass2(int floorIndex)
        {
            LoadCombinationFactors factors = new LoadCombinationFactors(1, 1, 0);
            return (LayoutUtility.GetNetArea() * GetDistributedLoads(floorIndex, factors)
                 + GetPerimeterLoadOnFloor(floorIndex, factors)) / 9.81;// N/m2
        }
        public double GetFloorGravityLoad(int floorIndex)
        {
            LoadCombinationFactors factors = new LoadCombinationFactors(1, 1, 0);
            return (LayoutUtility.GetNetArea() * GetDistributedLoads(floorIndex, factors)
                 + GetPerimeterLoadOnFloor(floorIndex, factors));// N 
        }
        public void ClearAllOpenSeesElements()
        {
            IDsManager.Clear();
            ClearAllNodes();
            ClearAllMaterials();
            FloorsGroups.ForEach(fg => fg.ClearColumns());
            FloorsGroups.ForEach(fg => fg.ClearWallsElements());
            Project.LogAction($"{Name}: Model Cleared.");
        }
        private void ClearAllMaterials()
        {
            MateialsInfo.ClearMaterialsCommands();
        }
        private void ClearAllNodes()
        {
            Gridsystem?.ClearNodes();
            BaseLevelNodes.Clear();
            FloorsGroups.ForEach(g => {
                g.Floors.ForEach(f =>
                {
                    f.ClearAllNodes();
                });
            });
            BaseNodesRegion = null;
            MasterNodesRegion = null;
        }
        public void ReCreateOpenSeesElements()
        {
            ClearAllOpenSeesElements();
            CreateAllOpensSeesElements();
        }
        public void InitSections(DetailedModel preModel)
        {
            InitSlabSection();
            InitFloors(preModel);
            ResetCouplingBeamLength();
        }
        private void InitFloors(DetailedModel preModel)
        {
            FloorsGroups = new List<FloorsGroup>();
            int GroupingNum = LayoutUtility.NumberOfFloorPerGroup;
            int numOfGroups = NumOfFloors / GroupingNum;
            for (int i = 0; i < numOfGroups; i++)
            {
                FloorsGroup fg = new FloorsGroup(i * GroupingNum + 1, (i + 1) * GroupingNum);
                FloorsGroup other = null;
                if (preModel != null)
                {
                    if (preModel.FloorsGroups.Count > i)
                        other = preModel.FloorsGroups[i];
                    else
                        other = preModel.FloorsGroups.Last();
                }
                fg.InitSections(this.ReinforcementUtility, LayoutUtility.CoreOpeningLength, other);
                FloorsGroups.Add(fg);
            }
        }
        private void InitSlabSection()
        {
            var MainBars = ReinforcementUtility.GetSteelBarByID(4);
            var AdditionalBars = ReinforcementUtility.GetSteelBarByID(5);
            SlabSection = new SlabSection()
            {
                Thickness = 0.2,
                DefaultReinforcement = new ShellReinforcement(7, MainBars),
                AdditionalReinforcement = new ShellReinforcement(6, AdditionalBars),
            };
        }
        public async Task CalcCost()
        {
            CostEstimation = new ModelEstimationEntity();
            await CostEstimation.CalcCost(this);
            Project.LogAction($"Model: {Name}");
            Project.LogAction($"Model: {CostEstimation}");
        }
        public void WriteMateials(StreamWriter file)
        {
            file.WriteLine("geomTransf PDelta 1 1 0 0;"); //for All columns
            file.WriteLine("geomTransf Linear 2 0 1 0;"); //for All Beams
            MateialsInfo.WriteMaterialsCommands(file);
            FloorsGroups.ForEach(fg => fg.WriteSectionsCommands(file));
            //FloorsGroups.ForEach(fg => fg.Floors.ForEach(f => f.WriteShellsCommand(file)));
        }
        public void WriteWallsElements(StreamWriter file, bool Elastic)
        {
            FloorsGroups.ForEach(fg => fg.WriteWallsElements(file, Elastic));
        }
        public void WriteFrameElements(StreamWriter file, bool Elastic)
        {
            FloorsGroups.ForEach(fg => fg.WriteColumns(file, Elastic));
        }
        public void WriteNodes(StreamWriter file)
        {
            file.WriteLine($"wipe;  # clear opensees model:{Name}");
            file.WriteLine($"wipeAnalysis;  # clear opensees model:{Name}");
            file.WriteLine("model basic -ndm 3 -ndf 6;  # 2 dimensions, 3 dof per node");

            BaseLevelNodes.ForEach(x => x.WriteCommand(file));
            file.WriteLine("fixY 0.0 1 1 1 1 1 1; ");

            BaseNodesRegion?.WriteCommand(file);
            FloorsGroups.ForEach(fg => {
                fg.Floors.ForEach(f => {
                    //f.WallNodes.ForEach(node => node.WriteCommand(file));
                    f.SlabNodes.ForEach(node => node.WriteCommand(file));
                });
            });
            FloorsGroups.ForEach(fg =>
            {
                fg.Floors.ForEach(f => f.MasterNode.WriteCommand(file));
            });
            MasterNodesRegion?.WriteCommand(file);
        }

        public double[] GetFLoorGravityLoads(int Index, LoadCombinationFactors factors)
        {
            return new double[] { GetCornerColumnFloorLoad(Index, factors), GetOuterColumnFloorLoad(Index, factors)
                    , GetInnerColumnFloorLoad(Index, factors), GetCoreColumnFloorLoad(Index, factors) };
        }
        public void WriteLoadSurfacesCommands(StreamWriter file, LoadCombinationFactors factors)
        {
            // static ID for gravity loads
            file.WriteLine("pattern Plain 1 Linear {");
            FloorsGroups.ForEach(fg =>
            {
                fg.Floors.ForEach(f => f.WriteGravityLoadsCommands(file, LayoutUtility, BeamLength,
                    GetFLoorGravityLoads(f.Index, factors)));
            });
            file.WriteLine("}");
            file.WriteLine("puts \"Gravity Loads Added\" ");

            if (Math.Abs(factors.E) < 1e-10)
                return;

            FloorForceAmplificationFactor = 1.0;
            double BaseShear = GetDesignBaseShear();
            double modalShear = GetFloorDesignForces().Sum();
            if (BaseShear > modalShear)
            {
                FloorForceAmplificationFactor = BaseShear / modalShear;
            }

            file.WriteLine("pattern Plain 2 Linear {");
            FloorsGroups.ForEach(fg =>
            {
                fg.Floors.ForEach(f => file.WriteLine($"load {f.MasterNode.ID} {f.DiaphramForce} 0.0 0.0 0.0 0.0 0.0;"));
            });
            file.WriteLine("}");
            file.WriteLine("puts \"Earthquake Loads Added\" ");
        }
        internal void WriteGravityRecorders(StreamWriter file)
        {
            FloorsGroups.ForEach(fg => fg.WriteGravityRecorders(file));
        }

        public void PostCalculateFloorForces()
        {
            CalcModalForces();
            CalculateDiaphramDesignForce();
            CalculateCouplingBeamsForces();
            CalculateShearWallsForces();
        }
        public void CalcModalForces()
        {
            double ratio = 0.01 * 9.81;
            double Ta = GetTa();
            double CuTa = 1.4 * Ta;

            GetModelFloors().ForEach(f =>
            {
                f.MFs = new List<double>();
                _ModeShapes.ForEach(mode =>
                {
                    double sa = _ResponseParameters.GetSa(Math.Min(mode.Period, CuTa));
                    f.MFs.Add(mode.X_values[f.Index - 1] * sa * mode.Mx * f.GetFloorMass() * ratio);
                });
            });
        }
        private bool IsAllShearWallSafe()
        {
            return FloorsGroups.All(fg => fg.IsAllShearWallSafe(ReinforcementUtility, LayoutUtility.FloorHeight
                , BeamLength, MateialsInfo.ShearWallMaterial, MateialsInfo.MainReinMaterial, MateialsInfo.MildSteelMaterial));
        }
        private void CalculateCouplingBeamsForces()
        {
            List<DetailedFloor> AllFloors = FloorsGroups.SelectMany(x => x.Floors).OrderBy(f => f.Index).ToList();
            int n = AllFloors.Count - 1;
            double SumV = 0;

            double L = (GetInnerShearWallLength() + BeamLength)/2;
            if (L < 1e-9)
            {
                for (int i = n; i > -1; i--)
                {
                    AllFloors[i].CouplingBeamShear = 0;
                }
            }
            else
            {
                for (int i = n; i > -1; i--)
                {
                    double Mot = 0;
                    for (int j = n; j > i - 1; j--)
                    {
                        double Height = (j - i + 1) * LayoutUtility.FloorHeight;
                        double InnerForce = AllFloors[j].GetShearWallForce(LayoutUtility.GetFloorWidth(), LayoutUtility.InnerWallForceShare, LayoutUtility.InnerWallMomentShare, LayoutUtility.GetInnerShearWallsMomentArm());
                        Mot += InnerForce * Height;
                    }
                    double V = Mot / L - SumV;
                    AllFloors[i].CouplingBeamShear = V;
                    SumV += V;
                }
            }
        }
        private void CalculateShearWallsForces()
        {
            List<DetailedFloor> AllFloors = FloorsGroups.SelectMany(x => x.Floors).OrderBy(f => f.Index).ToList();
            LoadCombinationFactors factors = new LoadCombinationFactors(1.2, 1, 1);

            FloorsGroups.ForEach(fg => {
                double N_out = 0;
                double N_inner_Comp = 0;
                double N_inner_Tension = 0;

                double V_out = 0;
                double V_inner = 0;
                double Sum_VB = 0;

                double M_out = 0;

                double MaxApplied_InnerForce = 0;
                double MaxApplied_OuterForce = 0;

                int start = fg.Floors.First().Index;
                for (int i = start; i <= NumOfFloors; i++)
                {
                    double P1 = -GetOuterColumnFloorLoad(i, factors);
                    double P2 = -0.5 * GetCoreColumnFloorLoad(i, factors);
                    N_out += P1;
                    double BeaM_V = AllFloors[i - 1].CouplingBeamShear;
                    // positive for compression;
                    N_inner_Comp += (P2 + BeaM_V);
                    N_inner_Tension += (P2 - BeaM_V);

                    double OuterShearWallForce = AllFloors[i - 1].GetShearWallForce(LayoutUtility.GetFloorWidth(), LayoutUtility.OuterWallForceShare, LayoutUtility.OuterWallMomentShare, LayoutUtility.GetOuterShearWallsMomentArm());
                    MaxApplied_OuterForce = Math.Max(OuterShearWallForce, MaxApplied_OuterForce);
                    double InnerShearWallForce = AllFloors[i - 1].GetShearWallForce(LayoutUtility.GetFloorWidth(), LayoutUtility.InnerWallForceShare, LayoutUtility.InnerWallMomentShare, LayoutUtility.GetInnerShearWallsMomentArm());
                    MaxApplied_InnerForce = Math.Max(InnerShearWallForce, MaxApplied_InnerForce);

                    V_out += OuterShearWallForce;
                    V_inner += InnerShearWallForce;
                    Sum_VB += BeaM_V;

                    double Height = (i - start + 1) * LayoutUtility.FloorHeight;
                    M_out += OuterShearWallForce * Height;
                }

                double M_inner = 0.5 * Sum_VB * (BeamLength + fg.InnerShearWalls.ShearWallReinforcement.Length);
                fg.OuterShearWallsForces = new ShearWallAppliedForces(MaxApplied_OuterForce, N_out, V_out, M_out);
                Project.LogAction($"{fg.Name}_OuterWalls:{fg.OuterShearWallsForces}");
                fg.InnerShearWallsForces = new CoupliedShearWallAppliedForces(MaxApplied_InnerForce, N_inner_Comp, V_inner, M_inner, N_inner_Tension, fg.Floors.Max(f => f.CouplingBeamShear));
                Project.LogAction($"{fg.Name}_InnerWalls:{fg.InnerShearWallsForces}");
            });
        }

        public double GetCs()
        {
            double min = Math.Max(0.044 * ResponseParameters.SDs * ResponseParameters.Ie, 0.01);
            if (ResponseParameters.SD1 > 0.6)
                min = Math.Max(min, 0.5 * ResponseParameters.SD1 * ResponseParameters.Ie / ResponseParameters.R);

            double Ta = GetTa();
            double CuTa = 1.4 * GetTa();
            return Math.Max(min, ResponseParameters.GetSa(Math.Min(ModeShapes[0].Period, CuTa)) * ResponseParameters.Ie / ResponseParameters.R);
        }
        public double GetBuildingWeight()
        {
            return GetModelMass() * 9.81;
        }
        public double GetModelMass()
        {
           return GetFloorMass(NumOfFloors) + (NumOfFloors - 1) * GetFloorMass(1);
        }
        public double GetDesignBaseShear()
        {
            return GetBuildingWeight() * GetCs();
        }
        public List<double> GetFloorDesignForces()
        {
            return FloorsGroups.SelectMany(x => x.Floors).OrderBy(f => f.Index).Select(x=>x.DiaphramForce).ToList();
        }
        private void CalculateDiaphramDesignForce()
        {
            List<DetailedFloor> AllFloors = FloorsGroups.SelectMany(x => x.Floors).OrderBy(f => f.Index).ToList();
            double t1 = ResponseParameters.Ie / ResponseParameters.R;
            double term = 9.81 * ResponseParameters.SDs * t1;
            for (int i = AllFloors.Count - 1; i > -1; i--)
            {
                double FloorMass = GetFloorMass(AllFloors[i].Index);
                double Force = AllFloors[i].GetDesginFloorForce(_ModeShapes) * t1;
                Force = Math.Max(Force, 0.2 * term * FloorMass);
                Force = Math.Min(Force, 0.4 * term * FloorMass);
                AllFloors[i].DiaphramForce = Force;
            }
            SetShearWallsForces();
        }
        private void SetShearWallsForces()
        {
            int No =  LayoutUtility.HorizontalOuterWallsLocations.Count;
            int Ni =  LayoutUtility.InnerWallsLocations.Count;

            double Lo = GetOuterShearWallLength();
            double Li = GetInnerShearWallLength();
            
            double To = FloorsGroups[0].OuterShearWalls.ShearWallReinforcement.Thickness;
            double Ti = FloorsGroups[0].InnerShearWalls.ShearWallReinforcement.Thickness;

            double Ao = Lo * To; 
            double Ai = Li * Ti;

            double At = No * Ao + Ni * Ai;
            LayoutUtility.OuterWallForceShare = No == 0 ? 0 : Ao / At;
            LayoutUtility.InnerWallForceShare = Ni == 0 ? 0 : Ai / At;

            No *= 2;
            Ni *= 2;

            double Mo = LayoutUtility.GetOuterShearWallsMomentArm();
            double Mi = LayoutUtility.GetInnerShearWallsMomentArm();

            double Io = Lo * Math.Pow(To, 3) / 12.0 + Ao * Math.Pow(Mo,2);
            double Ii = Li * Math.Pow(Ti, 3) / 12.0 + Ai * Math.Pow(Mi,2);

            double It = No * Io + Ni * Ii;
            LayoutUtility.OuterWallMomentShare = No == 0 ? 0 : Io / It;
            LayoutUtility.InnerWallMomentShare = Ni == 0 ? 0 : Ii / It;

        }
        internal double GetTa()
        {
            return 0.03 * Math.Pow(GetH(),0.9);   
        }

        public double GetRo()
        {
            return GetModelMass() / GetH();
        }
        public double GetH()
        {
            return NumOfFloors * LayoutUtility.FloorHeight;
        }
        public long GetRoofNodeID()
        {
            return FloorsGroups.Last().Floors.Last().MasterNode.ID;
        }
        
    }
}
