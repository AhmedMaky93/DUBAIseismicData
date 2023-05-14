using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.OpenseesCommand;

namespace TempAnalysis.Models
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(MajorReferenceLine))]
    public class ReferenceLine
    {
        [DataMember]
        private double _value;
        [DataMember]
        private long _id;

        public double Value 
        {
            get { return _value; }
        }
        public long ID
        {
            get { return _id; }
        }
        public ReferenceLine()
        {

        }

        public ReferenceLine(long ID, double Value)
        {
            _id = ID;
            _value = Value;
        }

    }

    [DataContract(IsReference = true)]
    public class MajorReferenceLine : ReferenceLine
    {
        [DataMember]
        private string _name;

        public MajorReferenceLine()
        {

        }
        public MajorReferenceLine(long _ID, double Value, string Name): base(_ID,Value)
        {
            _name = Name;
        }
    }

    
    [DataContract(IsReference = true)]
    public class Cube
    {
        [DataMember]
        public MajorRange XRange;
        [DataMember]
        public MajorRange YRange;
        [DataMember]
        public MajorRange ZRange;
        [DataMember]
        public List<BaseNode> Nodes = new List<BaseNode>();
        [DataMember]
        public List<Cube> Childs = new List<Cube>();

        public IDsManager IDM;
        public Cube()
        {

        }
        public Cube(IDsManager _IDM, MajorRange XRange, MajorRange YRange, MajorRange ZRange)
        {
            this.XRange = XRange;
            this.YRange = YRange;
            this.ZRange = ZRange;
            IDM = _IDM;
        }
        public bool IsValuesInRange(double xV,double yV, double zV)
        {
            return XRange.InRange(xV) && YRange.InRange(yV) && ZRange.InRange(zV);
        }

        public BaseNode AddNode(NodeType NodeType, double xV, double yV, double zV)
        {
            ReferenceLine xLine = XRange.GetLineByValue(xV);
            ReferenceLine yLine = YRange.GetLineByValue(yV);
            ReferenceLine zLine = ZRange.GetLineByValue(zV);

            BaseNode Node = Nodes.FirstOrDefault(x=> x.xLine == xLine
                                                  && x.yLine == yLine 
                                                  && x.zLine == zLine);

            if (Node != null)
                return Node;

            switch (NodeType)
            {
                case NodeType.BaseNode:
                    Node = new BaseNode(IDM,xLine, yLine, zLine);
                    break;
                case NodeType.FixedNode:
                    Node = new FixedNode(IDM,xLine, yLine, zLine);
                    break;
                case NodeType.MasterNode:
                    Node = new MasterNode(IDM,xLine, yLine, zLine);
                    break;
            }

            Nodes.Add(Node);

            return Node;
        }
        
        public Cube DeepSearch(double xV, double yV, double zV)
        {
            if (!this.Childs.Any())
                return this;
            Cube ContainingChild = this.Childs.FirstOrDefault(x=>x.IsValuesInRange(xV,yV,zV));
            return ContainingChild.DeepSearch(xV, yV, zV);
        }
        
    }

    [DataContract(IsReference = true)]
    [KnownType(typeof(Range))]
    public class MajorRange
    {
        [DataMember]
        public MajorReferenceLine Start;
        [DataMember]
        public MajorReferenceLine End;


        public MajorRange()
        {

        }
        public MajorRange(MajorReferenceLine Start, MajorReferenceLine End)
        {
            this.Start = Start;
            this.End = End;
        }
        public bool InRange(double V)
        {
            double Tolerance = 1e-9;
            if (Math.Abs(V - Start.Value) < Tolerance)
                return true;

            if (Math.Abs(V - End.Value) < Tolerance)
                return true;

            return V > Start.Value && V < End.Value;
        }
        public virtual ReferenceLine GetLineByValue(double value)
        {
            double Tolerance = 1e-9;
            if (Math.Abs(Start.Value - value) < Tolerance)
                return Start;

            if (Math.Abs(End.Value - value) < Tolerance)
                return End;
            
            return null;

        }
    }
    [DataContract(IsReference = true)]
    public class Range : MajorRange
    {
        [DataMember]
        public List<ReferenceLine> IntermediateLines = new List<ReferenceLine>();

        public IDsManager IDM;
        public Range()
        {

        }
        public Range(MajorReferenceLine Start, MajorReferenceLine End,IDsManager _IDM, List<double> Distances) :base(Start,End)
        {
            IDM = _IDM;
            CreateInterMediateLines(Distances);
        }
        private void CreateInterMediateLines(List<double> Distances)
        {
            double Tolerance = 1e-9;
            foreach (double dist in Distances)
            {
                if (dist < Start.Value || Math.Abs(dist - Start.Value) < Tolerance)
                    continue;
                if (dist > End.Value || Math.Abs(dist - End.Value) < Tolerance)
                    continue;
                IntermediateLines.Add(new ReferenceLine((++IDM.LastLineId), dist));
            }
        }
        public override ReferenceLine GetLineByValue(double value)
        {
            double Tolerance = 1e-9;
            if (Math.Abs(Start.Value - value) < Tolerance)
                return Start;

            if (Math.Abs(End.Value - value) < Tolerance)
                return End;

            return IntermediateLines.FirstOrDefault(x => Math.Abs(x.Value - value) < Tolerance);
        }
    }
    [DataContract(IsReference = true)]
    public class Direction
    {
        [DataMember]
        private string _name;
        [DataMember]
        private List<MajorReferenceLine> _lines = new List<MajorReferenceLine>();

        public IDsManager IDM;
        public List<MajorReferenceLine> Lines
        {
            get { return _lines; }
            set { _lines = value; }
        }

        public Direction()
        {

        }
        public Direction(string Name, IDsManager _IDM, double StartValue, double EndValue, double majorSpace)
        {
            _name = Name;
            IDM = _IDM;
            CreateLines(StartValue, EndValue, majorSpace);
        }
        public void CreateLines(double StartValue, double EndValue, double majorSpace)
        {
            _lines = new List<MajorReferenceLine>();
            int majorReferencesCount = 0;
            double MajorValue = StartValue;
            while (MajorValue<EndValue)
            {
                _lines.Add(new MajorReferenceLine((++IDM.LastLineId), MajorValue, $"{_name}{(++majorReferencesCount)}"));
                MajorValue += majorSpace;
            }
            _lines.Add(new MajorReferenceLine((++IDM.LastLineId), EndValue, $"{_name}{(++majorReferencesCount)}"));
        }
        public MajorRange GetMajorRange()
        {
            return new MajorRange(_lines.First(), _lines.Last());
        }
        public List<Range> CreateRanges(IDsManager IDM, List<double> Distance)
        {
            List<Range> ranges = new List<Range>();
            for (int i = 1; i < _lines.Count; i++)
            {
                int j = i - 1;
                ranges.Add(new Range(_lines[j],_lines[i],IDM, Distance));
            }
            return ranges;
        }

    }

    [DataContract(IsReference = true)]
    public class GridSystem
    {
        [DataMember]
        public List<Direction> Directions = new List<Direction>();

        [DataMember]
        private List<Cube> _cubes = new List<Cube>();

        public IDsManager IDM;
        public GridSystem()
        {

        }
        public GridSystem(int NumOfFloor, double FloorHeight,IDsManager _IDM, double StartValue, double EndValue, double majorSpace, List<double> Distance)
        {
            this.IDM = _IDM;
            Directions = new List<Direction>();

            Directions.Add(new Direction("X",IDM, StartValue, EndValue, majorSpace));
            Directions.Add(new Direction("Y",IDM, StartValue, EndValue, majorSpace));
            Directions.Add(new Direction("Z",IDM,  0, NumOfFloor * FloorHeight, FloorHeight));

            _cubes = new List<Cube>();
            List<Range> xRanges = Directions[0].CreateRanges(IDM, Distance);
            List<Range> yRanges = Directions[1].CreateRanges(IDM, Distance);
            MajorRange yMajorRange = Directions[1].GetMajorRange();
            List<Range> zRanges = Directions[2].CreateRanges(IDM, Distance);
            MajorRange zMajorRange = Directions[2].GetMajorRange();

            foreach (Range xRange in xRanges)
            {
                Cube xCube = new Cube(IDM,xRange, yMajorRange, zMajorRange);
                foreach (Range yRange in yRanges)
                {
                    Cube yCube = new Cube(IDM,xRange, yRange, zMajorRange);
                    foreach (Range zRange in zRanges)
                    {
                        yCube.Childs.Add(new Cube(IDM,xRange, yRange, zRange));
                    }
                    xCube.Childs.Add(yCube);
                }
                _cubes.Add(xCube);
            }
        }
        public BaseNode AddNode(int xi, int yi, int zi, NodeType NodeType)
        { 
            return AddNode(NodeType, GetValue(0,xi), GetValue(1,yi), GetValue(2,zi));
        }
        public BaseNode AddNode(NodeType NodeType, MajorReferenceLine xV, MajorReferenceLine yV, MajorReferenceLine zV)
        {
           return AddNode(NodeType, xV.Value,yV.Value,zV.Value);
        }
        public MasterNode AddMasterNode(int floorIndex)
        {
            return AddNode(NodeType.MasterNode, 0, 0, GetValue(2,floorIndex)) as MasterNode;
        }
        public BaseNode AddNode(NodeType NodeType, double xV, double yV, double zV )
        {
            Cube containingCube = _cubes.FirstOrDefault(x=>x.IsValuesInRange(xV,yV,zV));
            if (containingCube == null)
                return null;
            containingCube = containingCube.DeepSearch(xV, yV, zV);
            return containingCube.AddNode(NodeType,xV,yV,zV);
        }

        public void ClearNodes()
        {
            _cubes.ForEach(x=>x.Nodes.Clear());
        }

        public double GetValue(int DirIndex, int LineIndex)
        {
            return this.Directions[DirIndex].Lines[LineIndex].Value;
        }

    }

   
}
