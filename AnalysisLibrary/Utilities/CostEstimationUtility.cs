using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;
using TempAnalysis.Utilities;

namespace TempAnalysis
{
    [DataContract(IsReference = true)]
    public class ModelEstimationEntity
    {
        [DataMember]
        public double Slabs_ConcreteVolume = 0.0;
        [DataMember]
        public double Slabs_SteelWeight = 0.0;
        [DataMember]
        public double Columns_ConcreteVolume = 0.0;
        [DataMember]
        public double Columns_SteelWeight = 0.0;
        [DataMember]
        public double Walls_ConcreteVolume = 0.0;
        [DataMember]
        public double Walls_SteelWeight = 0.0;

        [DataMember]
        public double ConcreteVolume = 0.0;
        [DataMember]
        public double ReinforcementWeight = 0.0;
        [DataMember]
        public double ReinforcementCost = 0.0;
        [DataMember]
        public double ConcreteCost = 0.0;
        [DataMember]
        public double TotalCost = 0.0;
        [DataMember]
        public double CostPerFloor = 0.0;
        [DataMember]
        public double CostPer_M2 = 0.0;

        public ModelEstimationEntity()
        {

        }
        public double R(double v)
        {
            return Math.Round(v, 2, MidpointRounding.AwayFromZero);
        }
        public override string ToString()
        {
            StringBuilder strBuilder = new StringBuilder();
            strBuilder.AppendLine($"Concrete : Volume {R(ConcreteVolume)} M^3, Cost {R(ConcreteCost)} AED");
            strBuilder.AppendLine($"Steel : Weight {R(ReinforcementWeight)} Ton, Cost {R(ReinforcementCost)} AED");
            strBuilder.AppendLine($"Total : Cost {R(TotalCost)} AED, Cost/Floor {R(CostPerFloor)} AED, Cost/m^2 {R(CostPer_M2)} AED");
            return strBuilder.ToString();
        }
        public async Task CalcCost(DetailedModel detailedModel)
        {
            AddSlabsEstimation(detailedModel);
            AddColumnsEstimations(detailedModel);
            AddWallsEstimations(detailedModel);
            // shearWall
            // beams
            ConcreteVolume = Slabs_ConcreteVolume + Columns_ConcreteVolume + Walls_ConcreteVolume;
            ConcreteCost = detailedModel.MateialsInfo.SlabMaterial.CalculateEstimateFromVolumeinM3(Slabs_ConcreteVolume)
                + detailedModel.MateialsInfo.ColumnMaterial.CalculateEstimateFromVolumeinM3(Columns_ConcreteVolume)
                 + detailedModel.MateialsInfo.ShearWallMaterial.CalculateEstimateFromVolumeinM3(Walls_ConcreteVolume);

            ReinforcementWeight = Slabs_SteelWeight + Columns_SteelWeight + Walls_SteelWeight;
            ReinforcementCost = detailedModel.MateialsInfo.MainReinMaterial.CalculateEstimateFromWeightInTons(ReinforcementWeight);

            TotalCost = ConcreteCost + ReinforcementCost;
            CostPerFloor = TotalCost / ((double)detailedModel.NumOfFloors);
            CostPer_M2 = CostPerFloor / detailedModel.LayoutUtility.GetNetArea();
        }
        public void AddColumnsEstimations(DetailedModel detailedModel)
        {
            LayoutUtility LayoutUtility = detailedModel.LayoutUtility;
            ReinforcementUtility ReinforcementUtility = detailedModel.ReinforcementUtility;

            foreach (FloorsGroup fg in detailedModel.FloorsGroups)
            {
                Columns_ConcreteVolume += fg.CalcColumnsConcreteVolume(LayoutUtility.FloorHeight, LayoutUtility.No_CornerColumns,
                                   LayoutUtility.No_OuterColumns , LayoutUtility.No_InnerColumns, LayoutUtility.No_CoreColumns);

                Columns_SteelWeight += ReinforcementUtility.GetReinforcementWeightfromVolumeIn_M3(
                    fg.CalcColumnsSteelVolume(LayoutUtility.FloorHeight, LayoutUtility.No_CornerColumns,
                                   LayoutUtility.No_OuterColumns, LayoutUtility.No_InnerColumns, LayoutUtility.No_CoreColumns));
            }
        }
        public void AddWallsEstimations(DetailedModel detailedModel)
        {
            LayoutUtility LayoutUtility = detailedModel.LayoutUtility;
            ReinforcementUtility ReinforcementUtility = detailedModel.ReinforcementUtility;

            foreach (FloorsGroup fg in detailedModel.FloorsGroups)
            {
                Walls_ConcreteVolume += fg.CalcWallsConcreteVolume(LayoutUtility.FloorHeight, LayoutUtility.No_OuterWalls, LayoutUtility.No_InnerWalls,detailedModel.BeamLength);

                Walls_SteelWeight += ReinforcementUtility.GetReinforcementWeightfromVolumeIn_M3(
                    fg.CalcWallsSteelVolume(LayoutUtility.FloorHeight, LayoutUtility.No_OuterWalls, LayoutUtility.No_InnerWalls, detailedModel.BeamLength));
            }
        }
        public void AddSlabsEstimation(DetailedModel detailedModel)
        {
            LayoutUtility LayoutUtility = detailedModel.LayoutUtility;
            ReinforcementUtility ReinforcementUtility = detailedModel.ReinforcementUtility;
            SlabSection slabSection = detailedModel.SlabSection;

            Slabs_ConcreteVolume = detailedModel.NumOfFloors * slabSection.Thickness * LayoutUtility.GetNetArea();


            double SlabSteelVolume = ReinforcementUtility.GetReinforcementVolumeInM3(slabSection.DefaultReinforcement.SteelBars, (LayoutUtility.MajorSpacing + 0.5)
                                    , 2 * LayoutUtility.GetSlabSpans() * GetBaresCountInDistance(LayoutUtility.MajorSpacing, slabSection.DefaultReinforcement));

            int count = 2 * (LayoutUtility.No_CornerColumns + LayoutUtility.No_CoreColumns);
            SlabSteelVolume += ReinforcementUtility.GetReinforcementVolumeInM3(detailedModel.SlabSection.AdditionalReinforcement.SteelBars
                                    , LayoutUtility.MajorSpacing / 6.0
                                    , count * GetBaresCountInDistance(LayoutUtility.MajorSpacing / 6.0, slabSection.AdditionalReinforcement));

            count = LayoutUtility.No_OuterColumns;
            SlabSteelVolume += ReinforcementUtility.GetReinforcementVolumeInM3(detailedModel.SlabSection.AdditionalReinforcement.SteelBars
                                    , LayoutUtility.MajorSpacing / 6.0
                                    , count * GetBaresCountInDistance(LayoutUtility.MajorSpacing / 3.0, slabSection.AdditionalReinforcement));

            count = 2 * LayoutUtility.No_InnerColumns;
            SlabSteelVolume += ReinforcementUtility.GetReinforcementVolumeInM3(detailedModel.SlabSection.AdditionalReinforcement.SteelBars
                                    , LayoutUtility.MajorSpacing / 3.0
                                    , count * GetBaresCountInDistance(LayoutUtility.MajorSpacing / 3.0, slabSection.AdditionalReinforcement));

            count = LayoutUtility.No_OuterColumns + 2 * LayoutUtility.No_CoreColumns;
            SlabSteelVolume += ReinforcementUtility.GetReinforcementVolumeInM3(detailedModel.SlabSection.AdditionalReinforcement.SteelBars
                                    , LayoutUtility.MajorSpacing / 3.0
                                    , count * GetBaresCountInDistance(LayoutUtility.MajorSpacing / 6.0, slabSection.AdditionalReinforcement));

            count = LayoutUtility.No_OuterWalls;
            SlabSteelVolume += ReinforcementUtility.GetReinforcementVolumeInM3(detailedModel.SlabSection.AdditionalReinforcement.SteelBars
                                    , LayoutUtility.MajorSpacing / 6.0
                                    , count * GetBaresCountInDistance(detailedModel.GetOuterShearWallLength(), slabSection.AdditionalReinforcement));

            count = LayoutUtility.No_InnerWalls;
            SlabSteelVolume += ReinforcementUtility.GetReinforcementVolumeInM3(detailedModel.SlabSection.AdditionalReinforcement.SteelBars
                                    , LayoutUtility.MajorSpacing / 6.0
                                    , count * GetBaresCountInDistance(detailedModel.GetInnerShearWallFullLength(), slabSection.AdditionalReinforcement));

            Slabs_SteelWeight = detailedModel.NumOfFloors * ReinforcementUtility.GetReinforcementWeightfromVolumeIn_M3(SlabSteelVolume);
        }

        public int GetBaresCountInDistance(double distance, ShellReinforcement Reinforcement)
        {
            return 1 + (int)(distance * Reinforcement.NumberOfBarsPerMeter);
        }
    }

    
}
