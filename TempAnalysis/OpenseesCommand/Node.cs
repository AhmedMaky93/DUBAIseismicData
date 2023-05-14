using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;

namespace TempAnalysis.OpenseesCommand
{
    public enum NodeType
    {
        BaseNode,
        FixedNode,
        MasterNode
    }

    [DataContract(IsReference = true)]
    public class Node2D : BaseCommand
    {
        [DataMember]
        public long ID;
        [DataMember]
        public double X;
        [DataMember]
        public double Y;
        [DataMember]
        public double Mass;
        public Node2D()
        {

        }
        public Node2D(IDsManager IDM, double x, double y, double mass =0.0)
        {
            ID = ++IDM.LastNodeId;
            X = x;
            Y = y;
            Mass = mass;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            string command = $"node {ID} {X} {Y}";
            if (Math.Abs(Mass) > 1e-10)
                command += $" -mass {Ts(Mass)} 0.0 0.0";
            writer.WriteLine($"{command};");
        }
    }
    [DataContract(IsReference = true)]
    [KnownType(typeof(MasterNode))]
    [KnownType(typeof(FixedNode))]
    public class BaseNode : BaseCommand
    {
        [DataMember]
        public long ID;
        [DataMember]
        public ReferenceLine xLine;
        [DataMember]
        public ReferenceLine yLine;
        [DataMember]
        public ReferenceLine zLine;
        
        public BaseNode()
        {

        }
        public BaseNode(IDsManager IDM,ReferenceLine xLine, ReferenceLine yLine, ReferenceLine zLine)
        {
            ID = ++IDM.LastNodeId;
            this.xLine = xLine;
            this.yLine = yLine;
            this.zLine = zLine;
        }
        public string GetCorrdinates()
        {
            return $"{xLine.Value} {zLine.Value} {yLine.Value}";
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"node {ID} {GetCorrdinates()};");
        }
    }

    [DataContract(IsReference = true)]
    public class MasterNode : BaseNode
    {
        [DataMember]
        public long StartIndex;
        [DataMember]
        public long EndIndex;
        [DataMember]
        public double MassX;
        [DataMember]
        public double MassY;
        [DataMember]
        public double RoTMass;
        public MasterNode()
        {

        }
        public MasterNode(IDsManager IDM, ReferenceLine xLine, ReferenceLine yLine, ReferenceLine zLine)
            : base(IDM, xLine, yLine, zLine)
        {
        }
        public string GetMassString()
        {
            return $"-mass {Ts(MassX)} 0.0 {Ts(MassY)} 0.0 {Ts(RoTMass)} 0.0";
        }
        public void WriteMassCommand(StreamWriter writer)
        {
           writer.WriteLine($"node {ID} {xLine.Value} {zLine.Value} {yLine.Value} {GetMassString()};");
        }
        public override void WriteCommand(StreamWriter writer)
        {
            //writer.WriteLine($"fixY {zLine.Value} 0  1  0  1  0  1;");
            WriteMassCommand(writer);
            StringBuilder str = new StringBuilder();
            for (long i = StartIndex; i <= EndIndex; i++)
            {
                str.Append($" {i}");
            }
            writer.WriteLine($"rigidDiaphragm {2} {ID}{str};");
            writer.WriteLine($"fix {ID} 0  1  0  1  0  1;");
        }
    }
    [DataContract(IsReference = true)]
    public class FixedNode : BaseNode
    {
        public FixedNode()
        {

        }
        public FixedNode(IDsManager IDM,ReferenceLine xLine, ReferenceLine yLine, ReferenceLine zLine)
            :base(IDM,xLine,yLine,zLine)
        {
        }

    }
         
}
