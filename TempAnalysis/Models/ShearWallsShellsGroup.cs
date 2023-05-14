using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.OpenseesCommand;
using TempAnalysis.Utilities;

namespace TempAnalysis.Models
{
    [DataContract(IsReference =true)]
    public class ShearWallsGroup
    {
        [DataMember]
        public SpecialShearWallReinforcement ShearWallReinforcement;
        [DataMember]
        public ElementsRegion WallsRegion;
        [DataMember]
        public List<FrameNonLinearElement> Elements = new List<FrameNonLinearElement>();
        [DataMember]
        public List<FrameElasticElement> ElasticElements = new List<FrameElasticElement>();
        [DataMember]
        public FiberSection H_FiberSection;
        [DataMember]
        public FiberSection V_FiberSection;

        public ShearWallsGroup()
        {

        }
        public ShearWallsGroup(SpecialShearWallReinforcement shearWallReinforcement)
        {
            SetSection(shearWallReinforcement);
        }
        public void SetSection(SpecialShearWallReinforcement shearWallReinforcement)
        {
           ShearWallReinforcement = shearWallReinforcement;
        }
        public void CreateElements(IDsManager IDM, GridSystem grid, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial, List<Location> H_locations, string key, int startFloor, int EndFloor)
        {
            H_FiberSection = new FiberSection(IDM, ShearWallReinforcement, concrete01Material, steelMaterial);
            V_FiberSection = new FiberSection(IDM, ShearWallReinforcement, concrete01Material, steelMaterial,true);
            
            ElasticSectionProperties H_ElasticSection = ShearWallReinforcement.GenerateElasticProperties(concrete01Material, steelMaterial, false);
            ElasticSectionProperties V_ElasticSection = ShearWallReinforcement.GenerateElasticProperties(concrete01Material, steelMaterial, true);

            for (int i = startFloor; i <= EndFloor; i++)
            {
                foreach (var H_Location in H_locations)
                {
                    BaseNode Start = H_Location.ToNode(grid, NodeType.BaseNode, IDM, i - 1);
                    BaseNode End = H_Location.ToNode(grid, NodeType.BaseNode, IDM, i);
                    Elements.Add(new FrameNonLinearElement(IDM, Start, End, 1, H_FiberSection._ID));
                    ElasticElements.Add(new FrameElasticElement(IDM, Start, End, 1, H_ElasticSection));

                    Location V_Location = H_Location.Mirror();
                    Start = V_Location.ToNode(grid, NodeType.BaseNode, IDM, i - 1);
                    End = V_Location.ToNode(grid, NodeType.BaseNode, IDM, i);
                    Elements.Add(new FrameNonLinearElement(IDM, Start, End, 1, V_FiberSection._ID));
                    ElasticElements.Add(new FrameElasticElement(IDM, Start, End, 1, V_ElasticSection));
                }
            }
            WallsRegion = new ElementsRegion($"{key}_{startFloor}_{EndFloor}", IDM, Elements.First()._ID, ElasticElements.Last()._ID);
        }
        public void CreateElements(IDsManager IDM, LayoutUtility layout, GridSystem grid, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial, double ShearWallLength, double CouplingBeamLength, string key, int startFloor, int EndFloor)
        {
            H_FiberSection = new FiberSection(IDM, ShearWallReinforcement, concrete01Material, steelMaterial);
            V_FiberSection = new FiberSection(IDM, ShearWallReinforcement, concrete01Material, steelMaterial, true);

            ElasticSectionProperties H_ElasticSection = ShearWallReinforcement.GenerateElasticProperties(concrete01Material, steelMaterial, false);
            ElasticSectionProperties V_ElasticSection = ShearWallReinforcement.GenerateElasticProperties(concrete01Material, steelMaterial, true);

            for (int i = startFloor; i <= EndFloor; i++)
            {
                foreach (CoreWallLocation H_Location in layout.InnerWallsLocations)
                {
                    List<BaseNode> StartNodes = H_Location.GetOuterShearWallLocationNodes(grid, layout, NodeType.BaseNode, IDM, i - 1, ShearWallLength, CouplingBeamLength, false);
                    List<BaseNode> EndNodes = H_Location.GetOuterShearWallLocationNodes(grid, layout, NodeType.BaseNode, IDM, i, ShearWallLength, CouplingBeamLength, false);
                    for (int j = 0; j < StartNodes.Count; j++)
                    {
                        Elements.Add(new FrameNonLinearElement(IDM, StartNodes[j], EndNodes[j], 1, H_FiberSection._ID));
                        ElasticElements.Add(new FrameElasticElement(IDM, StartNodes[j], EndNodes[j], 1, H_ElasticSection));

                    }

                    StartNodes = H_Location.GetOuterShearWallLocationNodes(grid, layout, NodeType.BaseNode, IDM, i - 1, ShearWallLength, CouplingBeamLength, true);
                    EndNodes = H_Location.GetOuterShearWallLocationNodes(grid, layout, NodeType.BaseNode, IDM, i, ShearWallLength, CouplingBeamLength, true);
                    for (int j = 0; j < StartNodes.Count; j++)
                    {
                        Elements.Add(new FrameNonLinearElement(IDM, StartNodes[j], EndNodes[j], 1, V_FiberSection._ID));
                        ElasticElements.Add(new FrameElasticElement(IDM, StartNodes[j], EndNodes[j], 1, V_ElasticSection));
                    }
                }
            }

            if (layout.InnerWallsLocations.Any())
            { 
                WallsRegion = new ElementsRegion($"{key}_{startFloor}_{EndFloor}", IDM, Elements.First()._ID, ElasticElements.Last()._ID);
            }
        }
        public void WriteSection(StreamWriter file)
        {
            H_FiberSection?.WriteCommand(file);
            V_FiberSection?.WriteCommand(file);
        }
        public void ClearElements()
        {
            H_FiberSection = null;
            V_FiberSection = null;
            Elements.Clear();
            ElasticElements.Clear();
            WallsRegion = null;
        }

        internal double GetSteelVolume(double floorHeight)
        {
            SteelBarInfo Vertical = ShearWallReinforcement.R_VW.SteelBars;
            SpecialBoundaryReinforcement boundaryReinforcement = ShearWallReinforcement.SpecialBoundary;
            
            double volume = 2 * (ShearWallReinforcement.Length + ShearWallReinforcement.Thickness) * ShearWallReinforcement.RHW.GetArea_m2_PerMLength(floorHeight);
            if (boundaryReinforcement != null)
            {
                Vertical = boundaryReinforcement.VerticalBars.Diameter > Vertical.Diameter ? boundaryReinforcement.VerticalBars : Vertical;
                volume += 2 * boundaryReinforcement.GetTransverseSteelVolume(ShearWallReinforcement.Thickness);
            }
            volume += floorHeight + Math.Max(1.00, Vertical.Diameter_M() * 40) * ShearWallReinforcement.GetLongitudinalSteelArea();
            
            return volume;
        }

        internal void WriteCommands(StreamWriter file, bool Elastic)
        {
            if(Elastic)
                ElasticElements.ForEach(s => s.WriteCommand(file));
            else
                Elements.ForEach(s=>s.WriteCommand(file));
            WallsRegion?.WriteCommand(file);
        }

        internal double GetWallsWeight(double floorHeight, double density )
        {
            return ElasticElements.Count * floorHeight * ShearWallReinforcement.Thickness * ShearWallReinforcement.Length * density;
        }

        
    }
}
