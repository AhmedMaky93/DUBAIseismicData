using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;
using TempAnalysis.Models.ModelMaterial;
using TempAnalysis.Utilities;

namespace TempAnalysis.OpenseesCommand
{
    [DataContract(IsReference =true)]
    [KnownType(typeof(FiberSection))]
    [KnownType(typeof(ElasticShellSection))]
    [KnownType(typeof(LayeredShell))]
    [KnownType(typeof(SectiongAggregator))]
    public abstract class Section: BaseCommand
    {
        [DataMember]
        public long _ID;
        public Section()
        {

        }
        public Section(IDsManager IDM)
        {
            _ID = ++IDM.LastSectionId;
        }
    }
    
    [DataContract(IsReference = true)]
    public class ElasticShellSection: Section
    {
        [DataMember]
        public double E;
        [DataMember]
        public double nu;
        [DataMember]
        public double h;
        [DataMember]
        public double Rho;

        public ElasticShellSection()
        {

        }
        public ElasticShellSection(IDsManager IDM):base(IDM)
        {

        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"section ElasticMembranePlateSection {_ID} {Ts(E)} {Ts(nu)} {Ts(h)} {Ts(Rho)};");
        }
    }

    [DataContract(IsReference = true)]
    public class SectionLayer
    {
        [DataMember]
        public long MatID;
        [DataMember]
        public double Thickness;
        public SectionLayer()
        {

        }
        public SectionLayer(long MatID, double Thickness)
        {
            this.MatID = MatID;
            this.Thickness = Thickness;
        }

        public override string ToString()
        {
            return $" {MatID} {Thickness}";
        }
    }

    [DataContract(IsReference = true)]
    public class LayeredShell : Section
    {
        [DataMember]
        public List<SectionLayer> Layers = new List<SectionLayer>();
        [DataMember]
        public double Thickness;

        public LayeredShell()
        {

        }
        public LayeredShell(IDsManager IDM, List<SectionLayer> Layers) : base(IDM)
        {
            this.Layers = Layers;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            StringBuilder layersString = new StringBuilder();
            foreach (var layer in Layers)
            {
                layersString.Append(layer.ToString());
            }
            writer.WriteLine($"section LayeredShell {_ID} {Layers.Count}{layersString};");
        }
    }
    [DataContract(IsReference = true)]
    [KnownType(typeof(SpecialShearWallReinforcement))]
    [KnownType(typeof(FrameElementSection))]
    [KnownType(typeof(SquareColumnSection))]
    [KnownType(typeof(BeamSection))]
    public class FiberSection : Section
    {
        [DataMember]
        public IFiberSection Section;
        [DataMember]
        public SteelUniAxialMaterial SteelMat;
        [DataMember]
        public Concrete02Material ConcMat;
        [DataMember]
        public bool Vertical;
        public FiberSection()
        {
                
        }
        public FiberSection(IDsManager IDM, IFiberSection Section, Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool vertical =false) :base(IDM)
        {
            this.Section = Section;
            this.ConcMat = ConcMat;
            this.SteelMat = SteelMat;
            this.Vertical = vertical;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            RCSection RcSection = Section.GetFibers(ConcMat,SteelMat, Vertical);
            double GJ = Section.GetGJ(ConcMat, SteelMat,Vertical);
            writer.WriteLine($"section Fiber {_ID} -GJ {Ts(GJ)}"+" {");
            RcSection?.WriteCommand(writer);
            writer.WriteLine("}");
        }
    }

    [DataContract]
    public class SectiongAggregator : Section
    {
        [DataMember]
        public FiberSection FiberSection;
        [DataMember]
        public ElasticPPMaterial ShearMaterial;
        public SectiongAggregator()
        {

        }
        public SectiongAggregator(IDsManager IDM, FiberSection fiberSection, ElasticPPMaterial shearMaterial) :base(IDM)
        {
            FiberSection = fiberSection;
            ShearMaterial = shearMaterial;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            ShearMaterial?.WriteCommand(writer);
            FiberSection?.WriteCommand(writer);
            writer.WriteLine($"section Aggregator {_ID} {ShearMaterial._ID} Vz -section {FiberSection._ID};");
        }
    }
    [DataContract(IsReference = true)]
    public class RCSection
    {
        [DataMember]
        public Patch ConcretePatch;
        [DataMember]
        public List<Layer> BarsLayers = new List<Layer>();

        public RCSection()
        {

        }
        public void Rotate()
        {
            ConcretePatch?.Rotate();
            BarsLayers.ForEach(l => l.Rotate());
        }
        public void WriteCommand(StreamWriter writer)
        {
            ConcretePatch?.WriteCommand(writer);
            BarsLayers.ForEach(l => l.WriteCommand(writer));
        }
    }
    [DataContract(IsReference = true)]
    public class Patch 
    {
        [DataMember]
        public long MatID;
        [DataMember]
        public int NoOfRows;
        [DataMember]
        public int NoOfColumns;
        [DataMember]
        public List<Point2D> Vertex;

        public Patch()
        {

        }
        public void Rotate()
        {
            int oldRows = NoOfRows;
            NoOfRows = NoOfColumns;
            NoOfColumns = oldRows;

            Vertex.ForEach(v=>v.Rotate());
            List<Point2D> oldVertex = new List<Point2D>(Vertex);

            Vertex[0] = oldVertex[Vertex.Count - 1];
            for (int i = 1; i < Vertex.Count; i++)
            {
                Vertex[i] = oldVertex[i-1];
            }
        }
        public void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"patch quad {MatID} {NoOfRows} {NoOfColumns} {Vertex[0]} {Vertex[1]} {Vertex[2]} {Vertex[3]};");
        }
    }

    [DataContract(IsReference = true)]
    public class Layer
    {
        [DataMember]
        public Point2D Start;
        [DataMember]
        public Point2D End;
        [DataMember]
        public long MatID;
        [DataMember]
        public int NumOfBars;
        [DataMember]
        public double FiberArea;

        public Layer()
        {

        }
        public void Rotate() 
        {
           Start.Rotate();
           End.Rotate();
        }
        public void WriteCommand(StreamWriter writer)
        {
            string FiberAreaString = FiberArea.ToString("E");
            writer.WriteLine($"layer straight {MatID} {NumOfBars} {FiberAreaString} {Start} {End};");
        }
    }

}
