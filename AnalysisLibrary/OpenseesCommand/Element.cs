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
    [DataContract(IsReference = true)]
    [KnownType(typeof(ZeroLengthElement))]
    [KnownType(typeof(FrameElement))]
    [KnownType(typeof(FrameNonLinearElement))]
    [KnownType(typeof(FrameElasticElement))]
    [KnownType(typeof(FlexuralHinge))]
    [KnownType(typeof(ShellElement))]
    public abstract class Element : BaseCommand
    {
        [DataMember]
        public long _ID;

        public Element()
        {

        }
        public Element(IDsManager IDM)
        {
            _ID = ++IDM.LastElementId;
        }
        
        public override void WriteCommand(StreamWriter writer)
        {
        }
    }

    [DataContract(IsReference = true)]
    public class ZeroLengthElement : Element
    {
        [DataMember]
        public Node2D Start;
        [DataMember]
        public Node2D End;
        public ZeroLengthElement()
        {

        }
        public ZeroLengthElement(IDsManager IDM, Node2D start, Node2D end)
        {
            _ID = ++IDM.LastElementId;
            Start = start;
            End = end;
        }
        public void WriteCommand(StreamWriter writer, long shearMat, long MomentMat)
        {
            writer.WriteLine($"element zeroLength {_ID} {Start.ID} {End.ID} -mat {shearMat} {shearMat} {MomentMat} {MomentMat} -dir 2 3 5 6 -orient 0. 1. 0. 0. 0. 1. -doRayleigh ;");
        }

    }

    [DataContract(IsReference = true)]
    [KnownType(typeof(FrameNonLinearElement))]
    [KnownType(typeof(FrameElasticElement))]
    [KnownType(typeof(FlexuralHinge))]
    abstract public class FrameElement : Element
    {
        [DataMember]
        public BaseNode StartNode;
        [DataMember]
        public BaseNode EndNode;
        [DataMember]
        public int GomTransForm;

        public FrameElement()
        {

        }
        public FrameElement(IDsManager IDM, BaseNode Start, BaseNode End, int Geom) : base(IDM)
        {
            StartNode = Start;
            EndNode = End;
            GomTransForm = Geom;
        }
    }
    [DataContract(IsReference = true)]
    public class FrameElasticElement : FrameElement
    {
        [DataMember]
        public ElasticSectionProperties Properties;
        public FrameElasticElement()
        {

        }
        public FrameElasticElement(IDsManager IDM, BaseNode Start, BaseNode End, int Geom, ElasticSectionProperties Properties) : base(IDM, Start, End, Geom)
        {
            this.Properties = Properties;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"element elasticBeamColumn {_ID} {StartNode.ID} {EndNode.ID} {Properties} {GomTransForm};");
        }
    }

    [DataContract(IsReference = true)]
    public class FrameNonLinearElement : FrameElement
    {
        [DataMember]
        public long SectionID;
        [DataMember]
        public bool Vertical;
        public FrameNonLinearElement()
        {

        }
        public FrameNonLinearElement(IDsManager IDM, BaseNode Start, BaseNode End, int Geom, long sectionID) : base(IDM, Start, End, Geom)
        {
            StartNode = Start;
            EndNode = End;
            SectionID = sectionID;
            GomTransForm = Geom;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"element dispBeamColumn {_ID} {StartNode.ID} {EndNode.ID} {5} {SectionID} {GomTransForm};");
        }
    }

    [DataContract(IsReference = true)]
    public class FlexuralHingeMaterials:BaseCommand
    {
        [DataMember]
        public ElasticPPMaterial Moment;
        [DataMember]
        public ElasticPPMaterial Shear;
        [DataMember]
        public ElasticMaterial Torsion;
        public FlexuralHingeMaterials()
        {

        }
        public override string ToString()
        {
            return $"-mat {Shear._ID} {Torsion._ID} {Moment._ID} -dir {3} {4} {5}";
        }
        public override void WriteCommand(StreamWriter writer)
        {
            Moment?.WriteCommand(writer);
            Shear?.WriteCommand(writer);
            Torsion?.WriteCommand(writer);
        }
    }
    [DataContract(IsReference = true)]
    public class FlexuralHinge : FrameElement
    {
        [DataMember]
        public FlexuralHingeMaterials Materials;

        public FlexuralHinge()
        {

        }
        public FlexuralHinge(IDsManager IDM, BaseNode Start, BaseNode End, int Geom, FlexuralHingeMaterials Materials) :base(IDM,Start,End,Geom)
        {
            this.Materials = Materials;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"element zeroLength {_ID} {StartNode.ID} {EndNode.ID} {Materials} {WriteOrient()};");
        }
        public string WriteOrient()
        {
            // opensees Coor :  Norm Coor
            double dx = EndNode.xLine.Value - StartNode.xLine.Value;
            double dy = EndNode.zLine.Value - StartNode.zLine.Value;
            double dz = EndNode.yLine.Value - StartNode.yLine.Value;

            double Max = Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dz));
            dx /= Max;
            dy /= Max;
            dz /= Max;

            return $"-orient {dx} {dy} {dz} {dz} {dy} {dx}";
        }
    }

    [DataContract(IsReference = true)]
    public class ShellElement: Element
    {
        [DataMember]
        public List<BaseNode> Nodes = new List<BaseNode>();

        public ShellElement()
        {

        }
        public ShellElement(IDsManager IDM, List<BaseNode> Nodes) :base(IDM)
        {
            this.Nodes = Nodes;
            
        }
        public void WriteCommand(StreamWriter writer,long SectionID)
        {
            writer.WriteLine($"element ShellMITC4 {_ID} {Nodes[0].ID} {Nodes[1].ID} {Nodes[2].ID} {Nodes[3].ID} {SectionID};");
        }
    }
}
