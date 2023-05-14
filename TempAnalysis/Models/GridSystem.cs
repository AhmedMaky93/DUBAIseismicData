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

        public ReferenceLine(IDsManager IDM, double Value)
        {
            _id = ++IDM.LastLineId;
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
        public MajorReferenceLine(IDsManager IDM, double Value, string Name): base(IDM,Value)
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

        public Cube()
        {

        }
        public Cube(MajorRange XRange, MajorRange YRange, MajorRange ZRange)
        {
            this.XRange = XRange;
            this.YRange = YRange;
            this.ZRange = ZRange;
        }
        public bool IsValuesInRange(double xV,double yV, double zV)
        {
            return XRange.InRange(xV) && YRange.InRange(yV) && ZRange.InRange(zV);
        }

        public BaseNode AddNode(NodeType NodeType, IDsManager IDM, double xV, double yV, double zV)
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

        public Range()
        {

        }
        public Range(MajorReferenceLine Start, MajorReferenceLine End,IDsManager IDM, List<double> distances = null) :base(Start,End)
        {
            CreateInterMediateLines(IDM, distances);
        }
        private void CreateInterMediateLines(IDsManager IDM,List<double> distances)
        {
            if (distances == null)
                return;
            double Tolerance = 1e-9;
            foreach (var dist in distances)
            {
                if (dist < Start.Value || Math.Abs(dist - Start.Value) < Tolerance)
                    continue;

                if (dist > End.Value || Math.Abs(dist - End.Value) < Tolerance)
                    continue;

                IntermediateLines.Add(new ReferenceLine(IDM, dist));
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

        public List<MajorReferenceLine> Lines
        {
            get { return _lines; }
            set { _lines = value; }
        }

        public Direction()
        {

        }
        public Direction(string Name, IDsManager IDM, double StartValue, double EndValue, double majorSpace)
        {
            _name = Name;
            CreateLines(IDM, StartValue, EndValue, majorSpace);
        }
        public void CreateLines(IDsManager IDM,double StartValue, double EndValue, double majorSpace)
        {
            _lines = new List<MajorReferenceLine>();
            int majorReferencesCount = 0;
            double MajorValue = StartValue;
            while (MajorValue<EndValue)
            {
                _lines.Add(new MajorReferenceLine(IDM, MajorValue, $"{_name}{(++majorReferencesCount)}"));
                MajorValue += majorSpace;
            }
            _lines.Add(new MajorReferenceLine(IDM, EndValue, $"{_name}{(++majorReferencesCount)}"));
        }
        public MajorRange GetMajorRange()
        {
            return new MajorRange(_lines.First(), _lines.Last());
        }
        public List<Range> CreateRanges(IDsManager IDM,List<double> SpecialDistances = null)
        {
            List<Range> ranges = new List<Range>();
            for (int i = 1; i < _lines.Count; i++)
            {
                int j = i - 1;
                ranges.Add(new Range(_lines[j],_lines[i],IDM, SpecialDistances));
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

        public GridSystem()
        {

        }
        public GridSystem(int NumOfFloor, double FloorHeight,IDsManager IDM, double StartValue, double EndValue,List<double> Distances, double majorSpace)
        {
            Directions = new List<Direction>();

            Directions.Add(new Direction("X",IDM, StartValue, EndValue, majorSpace));
            Directions.Add(new Direction("Y",IDM, StartValue, EndValue, majorSpace));
            Directions.Add(new Direction("Z",IDM,  0, NumOfFloor * FloorHeight, FloorHeight));

            _cubes = new List<Cube>();
            List<Range> xRanges = Directions[0].CreateRanges(IDM, Distances);
            List<Range> yRanges = Directions[1].CreateRanges(IDM, Distances);
            MajorRange yMajorRange = Directions[1].GetMajorRange();
            List<Range> zRanges = Directions[2].CreateRanges(IDM);
            MajorRange zMajorRange = Directions[2].GetMajorRange();


            foreach (Range xRange in xRanges)
            {
                Cube xCube = new Cube(xRange, yMajorRange, zMajorRange);
                foreach (Range yRange in yRanges)
                {
                    Cube yCube = new Cube(xRange, yRange, zMajorRange);
                    foreach (Range zRange in zRanges)
                    {
                        yCube.Childs.Add(new Cube(xRange, yRange, zRange));
                    }
                    xCube.Childs.Add(yCube);
                }
                _cubes.Add(xCube);
            }
        }
        public BaseNode AddNode(int xi, int yi, int zi, NodeType NodeType, IDsManager IDM)
        { 
            return AddNode(NodeType, IDM, GetValue(0,xi), GetValue(1,yi), GetValue(2,zi));
        }
        public BaseNode AddNode(NodeType NodeType, IDsManager IDM, MajorReferenceLine xV, MajorReferenceLine yV, MajorReferenceLine zV)
        {
           return AddNode(NodeType, IDM, xV.Value,yV.Value,zV.Value);
        }
        public MasterNode AddMasterNode(int floorIndex, IDsManager IDM)
        {
            return AddNode(NodeType.MasterNode, IDM, 0, 0, GetValue(2,floorIndex)) as MasterNode;
        }
        public BaseNode AddNode(NodeType NodeType, IDsManager IDM, double xV, double yV, double zV )
        {
            Cube containingCube = _cubes.FirstOrDefault(x=>x.IsValuesInRange(xV,yV,zV));
            if (containingCube == null)
                return null;
            containingCube = containingCube.DeepSearch(xV, yV, zV);
            return containingCube.AddNode(NodeType,IDM,xV,yV,zV);
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
