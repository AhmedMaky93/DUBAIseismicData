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
    public class ModeShapeData
    {
        #region Members
        [DataMember]
        public int Index = 0;
        [DataMember]
        public double Lambda = 0;
        [DataMember]
        public double Omega = 0;
        [DataMember]
        public double Frequency = 0;
        [DataMember]
        public double Period = 0;
        [DataMember]
        public double Mx;
        [DataMember]
        public double My;
        [DataMember]
        public double Mrz;
        [DataMember]
        public double SumMx;
        [DataMember]
        public double SumMy;
        [DataMember]
        public double SumMrz;
        [DataMember]
        public List<double> X_values = new List<double>();
        [DataMember]
        public List<double> Y_values = new List<double>();
        [DataMember]
        public double Sa;
        #endregion

        #region Constructor
        public ModeShapeData()
        {

        }
        public ModeShapeData(int index)
        {
            Index = index;
        }
        #endregion
        public void Normalize()
        {
            double Alpha = 0;
            X_values.ForEach(x => Alpha += Math.Pow(x, 2));
            Y_values.ForEach(y => Alpha += Math.Pow(y, 2));
            
            Alpha = Math.Sqrt(Alpha);

            X_values = X_values.Select(x=> x/= Alpha).ToList();
            Y_values = Y_values.Select(y=> y/= Alpha).ToList();
        }
    }
    public interface IStructuralFloor
    {
        int Index { get; set; }
        List<double> MFs { get; set; }
        List<double> MVs { get; set; }
        List<double> MMs { get; set; }
        double Vd { get; set; }
        double Md { get; set; }
        double GetFloorMass();
        double GetDesginFloorForce(List<ModeShapeData> modeShapeData);
    }
    [DataContract(IsReference = true)]
    public class DetailedFloor: IStructuralFloor
    {
        [DataMember]
        public int _Index;
        [DataMember]
        public MasterNode MasterNode;
        [DataMember]
        public List<BaseNode> SlabNodes = new List<BaseNode>();
        [DataMember]
        public List<ShellElement> ShellElements = new List<ShellElement>();
        [DataMember]
        public double EarthquakeForce = 0.0;
        [DataMember]
        public double CouplingBeamShear = 0.0;

        [DataMember]
        public List<double> _MFs = new List<double>();
        [DataMember]
        public List<double> _MVs = new List<double>();
        [DataMember]
        public List<double> _MMs = new List<double>();
        [DataMember]
        public double _Vd;
        [DataMember]
        public double _Md;

        public int Index
        {
            get { return _Index; }
            set { _Index = value; }
        }
        public List<double> MFs
        {
            get { return _MFs; }
            set { _MFs = value; }
        }
        public List<double> MVs
        {
            get { return _MVs; }
            set { _MVs = value; }
        }
        public List<double> MMs
        {
            get { return _MMs; }
            set { _MMs = value; }
        }
        public double Vd
        {
            get { return _Vd; }
            set { _Vd = value; }
        }
        public double Md
        {
            get { return _Md; }
            set { _Md = value; }
        }

        public DetailedFloor()
        {

        }        
        public DetailedFloor(int Index)
        {
            this._Index = Index;
        }
        public double GetDesginFloorForce(List<ModeShapeData> modeShapeData)
        {
            double F = 0;
            double Over = 0;
            for (int i = 0; i < modeShapeData.Count; i++)
            {
                F += modeShapeData[i].Mx * _MFs[i];
                Over += modeShapeData[i].Mx;
            }
            return F / Over;
        }
        public double GetShearWallForce(double BuildingLength, double ForceShare, double MomentShare)
        {
            return (ForceShare + 0.05 * BuildingLength * MomentShare)* EarthquakeForce;
        }

        #region SlabNodes

        public double GetFloorMass()
        {
            return MasterNode.MassX;
        }
        public void CreateMasterNode(GridSystem Gridsystem, IDsManager IDM, double mass, double RotMassFactor, List<double> MassesMultpliers)
        {
            MasterNode floorMasterNode = Gridsystem.AddMasterNode(Index, IDM);
            floorMasterNode.StartIndex = SlabNodes.First().ID;
            floorMasterNode.EndIndex = SlabNodes.Last().ID;
            floorMasterNode.MassX = mass * MassesMultpliers[0];
            floorMasterNode.MassY = mass * MassesMultpliers[1];
            floorMasterNode.RoTMass = mass * RotMassFactor * MassesMultpliers[2];
            MasterNode = floorMasterNode;
        }
        public void CreateSlabMeshNodes(GridSystem Gridsystem, IDsManager IDM, LayoutUtility LayoutUtility, double ShearWallLength, double CouplingBeamLength)
        {
            LayoutUtility.CornerColumnsLocations.ForEach(Loc =>
            {
                SlabNodes.Add(Loc.ToNode(Gridsystem, NodeType.BaseNode, IDM, Index));
             });
            LayoutUtility.OuterColumnsLocations.ForEach(Loc =>
            {
                SlabNodes.Add(Loc.ToNode(Gridsystem, NodeType.BaseNode, IDM, Index));
            });
            LayoutUtility.HorizontalOuterWallsLocations.ForEach(Loc =>
            {
                SlabNodes.Add(Loc.ToNode(Gridsystem, NodeType.BaseNode, IDM, Index));
                SlabNodes.Add(Loc.Mirror().ToNode(Gridsystem, NodeType.BaseNode, IDM, Index));
            });
            LayoutUtility.InnerColumnsLocations.ForEach(Loc =>
            {
                SlabNodes.Add(Loc.ToNode(Gridsystem, NodeType.BaseNode, IDM, Index));
            });
            foreach (var node in LayoutUtility.GetSpecialInnerWallsNodes(Gridsystem, IDM, ShearWallLength, CouplingBeamLength, Index))
            {
                SlabNodes.Add(node);
            }
            LayoutUtility.CoreColumnsLocations.ForEach(Loc =>
            {
                SlabNodes.Add(Loc.ToNode(Gridsystem, NodeType.BaseNode, IDM, Index));
            });

        }
        
        public void ClearAllNodes()
        {
            SlabNodes.Clear();
            MasterNode = null;
        }
        internal void ClearShells()
        {
            ShellElements.Clear();
        }
        #endregion

        #region SlabElements
        private void WriteLoadAtLocation(StreamWriter file, Location location, double nodeLoad,GridSystem Gridsystem, IDsManager IDM, LayoutUtility layout)
        {
            BaseNode node = location.ToNode(Gridsystem, NodeType.BaseNode, IDM, Index);
            file.WriteLine($"load {node.ID} 0.0 {nodeLoad} 0.0 0.0 0.0 0.0;");
        }

        internal void WriteGravityLoadsCommands(StreamWriter file, GridSystem Gridsystem, IDsManager IDM, LayoutUtility layout, double ShearWallLength, double CouplingBeamLength, double[] P)
        {
            double nodeLoad = P[0];
            layout.CornerColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem,IDM,layout));
            
            nodeLoad = P[1];
            layout.OuterColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem,IDM,layout));

            layout.HorizontalOuterWallsLocations.ForEach(loc => {
                WriteLoadAtLocation(file, loc, nodeLoad, Gridsystem, IDM, layout);
                WriteLoadAtLocation(file, loc.Mirror(), nodeLoad, Gridsystem, IDM, layout);
            });

            nodeLoad =  P[2];
            layout.InnerColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem,IDM,layout));

            nodeLoad = 0.5 *  P[3];
            foreach (CoreWallLocation location in layout.InnerWallsLocations)
            {
                foreach (BaseNode Node in location.GetOuterShearWallLocationNodes(Gridsystem,layout, NodeType.BaseNode, IDM, Index, ShearWallLength, CouplingBeamLength, false)
                     .Concat(location.GetOuterShearWallLocationNodes(Gridsystem, layout, NodeType.BaseNode, IDM, Index, ShearWallLength, CouplingBeamLength, true)) )
                { 
                   file.WriteLine($"load {Node.ID} 0.0 {nodeLoad} 0.0 0.0 0.0 0.0;");
                }
            }
            nodeLoad = P[3];
            layout.CoreColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem,IDM,layout));
        }
        internal BaseNode GetLevelNode(Location Loc,Dictionary<Location,BaseNode> specialLocations, GridSystem Gridsystem, IDsManager IDM)
        {
            if (specialLocations.Keys.Any(l => l == Loc))
            {
                Location LocVar = specialLocations.Keys.FirstOrDefault(l => l == Loc);
                return specialLocations[LocVar];
            }
            else
            {
                return Loc.ToNode(Gridsystem, NodeType.BaseNode, IDM, Index);
            }
        }
        internal void CreateShells(GridSystem Gridsystem, IDsManager IDM, LayoutUtility LayoutUtility, Dictionary<Location,BaseNode> specialLocations, long SectionID, int X1, int X2, int Y1, int Y2)
        {
            for (int X =X1 + 1; X <= X2; X++)
            {
                for (int Y = Y1 + 1; Y <= Y2; Y++)
                {
                    List<BaseNode> nodes = new List<BaseNode>();
                    nodes.Add(GetLevelNode(new Location(X - 1, Y - 1), specialLocations, Gridsystem, IDM));
                    nodes.Add(GetLevelNode(new Location(X - 1, Y), specialLocations, Gridsystem, IDM));
                    nodes.Add(GetLevelNode(new Location(X, Y), specialLocations, Gridsystem, IDM));
                    nodes.Add(GetLevelNode(new Location(X, Y - 1), specialLocations, Gridsystem, IDM));
                    ShellElements.Add(new ShellElement(IDM, nodes, SectionID));
                }
            }
        }
        internal void WriteShellsCommand(StreamWriter writer)
        {
            ShellElements.ForEach(sh => sh.WriteCommand(writer));
        }



        #endregion
    }
}
