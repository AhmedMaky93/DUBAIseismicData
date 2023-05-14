using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TempAnalysis.OpenseesCommand
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(Node2D))]
    [KnownType(typeof(BaseNode))]
    [KnownType(typeof(MasterNode))]
    [KnownType(typeof(FixedNode))]
    [KnownType(typeof(Region))]
    [KnownType(typeof(Nodes2DRegion))]
    [KnownType(typeof(BaseNodesRegion))]
    [KnownType(typeof(MasterNodesRegion))]
    [KnownType(typeof(ElementsRegion))]
    [KnownType(typeof(OpenSeesMaterial))]
    [KnownType(typeof(SteelUniAxialMaterial))]
    [KnownType(typeof(PlateRebarMaterial))]
    [KnownType(typeof(ElasticIsotropic))]
    [KnownType(typeof(Section))]
    [KnownType(typeof(ElasticShellSection))]
    [KnownType(typeof(FiberSection))]
    [KnownType(typeof(Element))]
    [KnownType(typeof(ZeroLengthElement))]
    [KnownType(typeof(FrameElement))]
    [KnownType(typeof(FrameNonLinearElement))]
    [KnownType(typeof(FrameElasticElement))]
    [KnownType(typeof(FlexuralHinge))]
    [KnownType(typeof(Concrete02Material))]
    [KnownType(typeof(ParallelMaterial))]
    [KnownType(typeof(HystericMaterial))]
    [KnownType(typeof(ElasticMaterial))]
    [KnownType(typeof(ElasticPPMaterial))]
    [KnownType(typeof(FlexuralHingeMaterials))]
    [KnownType(typeof(ShellElement))]
    [KnownType(typeof(PlaneStressUserMaterial))]
    [KnownType(typeof(PlateFromPlaneStressMaterial))]
    [KnownType(typeof(LayeredShell))]
    [KnownType(typeof(SectiongAggregator))]
    public abstract class BaseCommand
    {
        public abstract void WriteCommand(StreamWriter writer);

        public BaseCommand()
        {

        }
        public string Ts(double V)
        {
            return V.ToString("E");
        }
    }
}
