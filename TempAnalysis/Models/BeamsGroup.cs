using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.OpenseesCommand;

namespace TempAnalysis.Models
{
    [DataContract(IsReference =true)]
    public class BeamsGroup
    {
        [DataMember]
        public BeamSection CouplingBeamSection;
        [DataMember]
        public ElementsRegion CoreRegion;
        [DataMember]
        public List<FrameNonLinearElement> CoreElements = new List<FrameNonLinearElement>();
        [DataMember]
        public List<FrameElasticElement> ElasticCoreElements = new List<FrameElasticElement>();
        [DataMember]
        public SectiongAggregator Section;

        public BeamsGroup()
        {

        }
        public BeamsGroup(BeamSection BeamSection)
        {
            CouplingBeamSection = BeamSection;
        }
        public void CreateElements(IDsManager IDM, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial, List<List<BaseNode>> Locations, string name, double BeamLength)
        {
            Section = new SectiongAggregator(IDM
                , new FiberSection(IDM, CouplingBeamSection, concrete01Material, steelMaterial)
                , CouplingBeamSection.GetVzMaterial(IDM, concrete01Material, steelMaterial, BeamLength));
            ElasticSectionProperties elasticSectionProperties = CouplingBeamSection.GenerateElasticProperties(concrete01Material,steelMaterial);
            CoreElements = new List<FrameNonLinearElement>();
            ElasticCoreElements = new List<FrameElasticElement>();
            foreach (List<BaseNode> beamNodes in Locations)
            {
                CoreElements.Add(new FrameNonLinearElement(IDM, beamNodes[0], beamNodes[1], 2, Section._ID));
                ElasticCoreElements.Add(new FrameElasticElement(IDM, beamNodes[0], beamNodes[1], 2, elasticSectionProperties));
            }
            if (Locations.Any())
            { 
                CoreRegion = new ElementsRegion(name, IDM, CoreElements.First()._ID, ElasticCoreElements.Last()._ID);
            }
        }
        public void WriteSections(StreamWriter file)
        {
            Section?.WriteCommand(file);
        }
        public void WriteCommands(StreamWriter file, bool Elastic)
        {
            if (Elastic)
                ElasticCoreElements.ForEach(x => x.WriteCommand(file));
            else
                CoreElements.ForEach(x=>x.WriteCommand(file));
            CoreRegion?.WriteCommand(file);
        }
        public void ClearElements()
        {
            CoreElements.Clear();
            ElasticCoreElements.Clear();
            CoreRegion = null;
        }

        internal double GetBeamsWeight(double beamLength, double density)
        {
            return ElasticCoreElements.Count * density * beamLength * CouplingBeamSection.SectionDepth * CouplingBeamSection.SectionWidth;
        }
    }

    
}
