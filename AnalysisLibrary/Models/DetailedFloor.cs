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
        public double DiaphramForce = 0.0;
        [DataMember]
        public double CouplingBeamShear = 0.0;

        [DataMember]
        public List<double> _MFs = new List<double>();
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
        public double GetShearWallForce(double BuildingLength, double ForceShare, double MomentShare, double momentArm)
        {
            return DiaphramForce * (ForceShare + MomentShare * 0.05 * BuildingLength / momentArm);
        }

        #region SlabNodes

        public double GetFloorMass()
        {
            return MasterNode.MassX;
        }
        public void CreateMasterNode(GridSystem Gridsystem, double mass, double RotMassFactor, int[] factors)
        {
            MasterNode floorMasterNode = Gridsystem.AddMasterNode(Index);
            floorMasterNode.StartIndex = SlabNodes.First().ID;
            floorMasterNode.EndIndex = SlabNodes.Last().ID;
            floorMasterNode.MassX = mass * factors[0];
            floorMasterNode.MassY = mass * factors[1];
            floorMasterNode.RoTMass = mass * RotMassFactor * factors[2];
            MasterNode = floorMasterNode;
        }
        public void CreateSlabMeshNodes(LayoutUtility LayoutUtility, double CouplingBeamLength)
        {
            SlabNodes.AddRange(LayoutUtility.GetNodesForAllOuterShearWallAtCertainLevel(Index));
            SlabNodes.AddRange(LayoutUtility.GetCoreShearWallsLevelNodes(Index, CouplingBeamLength));
            SlabNodes.AddRange(LayoutUtility.GetAllColumnsNodesAtLevel(Index));
        }
        
        public void ClearAllNodes()
        {
            SlabNodes.Clear();
            //WallNodes.Clear();
            MasterNode = null;
        }
        #endregion

        #region SlabElements
        private void WriteLoadAtNode(StreamWriter file, BaseNode node, double nodeLoad)
        {
            file.WriteLine($"load {node.ID} 0.0 {nodeLoad} 0.0 0.0 0.0 0.0;");
        }
        private void WriteLoadAtLocation(StreamWriter file, Location location, double nodeLoad,GridSystem Gridsystem)
        {
            WriteLoadAtNode(file, location.ToNode(Gridsystem, Index), nodeLoad);
        }
        internal void WriteGravityLoadsCommands(StreamWriter file, LayoutUtility layout, double CouplingBeamLength, double[] P)
        {
            GridSystem Gridsystem = layout.Grs;
            double nodeLoad = P[0];
            layout.CornerColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem));
            
            nodeLoad = P[1];
            layout.OuterColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem));
            layout.HorizontalOuterWallsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc, nodeLoad, Gridsystem));

            nodeLoad =  P[2];
            layout.InnerColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem));

            nodeLoad = 0.5 *  P[3];
            layout.GetCoreShearWallsColumnNodes(Index, CouplingBeamLength).ForEach(node => WriteLoadAtNode(file,node, nodeLoad));

            nodeLoad = P[3];
            layout.CoreColumnsLocations.ForEach(loc=> WriteLoadAtLocation(file,loc,nodeLoad,Gridsystem));
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
                return Loc.ToNode(Gridsystem, Index);
            }
        }
        #endregion
    }
}
