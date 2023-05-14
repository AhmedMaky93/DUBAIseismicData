using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.OpenseesCommand;

namespace TempAnalysis.Models.ModelMaterial
{
    [DataContract(IsReference = true)]
    public class ConCreteMaterial
    {
        [DataMember]
        public string Name;
        [DataMember]
        public double YieldStrength;
        [DataMember]
        public double CostPerM3;
        public ConCreteMaterial()
        {

        }
        public double GetYieldStrength_MPA()
        {
            return YieldStrength / (1E+6);
        }
        public double GetUltimateStrain()
        {
            double fcMPa = GetYieldStrength_MPA();
            if (fcMPa < 30)
            { return 0.0036; }
            else if (fcMPa > 120)
            { return 0.0027; }
            else
            { return 0.0036- (fcMPa - 30)* Math.Pow(10,-5); }
        }
        public double GetYieldStrain()
        {
            return 2 * YieldStrength / GetModulusOfElasticity();
        }
        public double GetUltimateStrength()
        {
            return 1.15 * YieldStrength;
        }
        public double GetModulusOfElasticity()
        {
            return 4700 * Math.Sqrt(GetYieldStrength_MPA()) * 1E+6;
        }
        public double Getnu()
        {
            return 0.2;
        }
        public double GetG()
        {
            return GetModulusOfElasticity() / (2 * (1 + Getnu()));
        }

        public double CalculateEstimateFromVolumeinM3(double volume)
        {
            return volume * CostPerM3;
        }
        public string GetName()
        {
            return (YieldStrength / (1.0E+6)).ToString("0.0") + "Mpa:Concrete";
        }
        public ConCreteMaterial(double yieldStrength, double M3Cost)
        {
            YieldStrength = yieldStrength;
            CostPerM3 = M3Cost;
        }
    }

    [DataContract(IsReference = true)]
    public class SteelMaterial
    {
        [DataMember]
        public string Name;
        [DataMember]
        public double YieldStrength;
        [DataMember]
        public double CostPerTon;
        [DataMember]
        public double E0 = 2.0E+11;

        public SteelMaterial()
        {

        }
        public double CalculateEstimateFromWeightInTons(double Weight)
        {
            return Weight * CostPerTon;
        }
        
        public SteelMaterial(string Name , double yieldStrength, double TonCost)
        {
            this.Name = Name;
            this.YieldStrength = yieldStrength;
            this.CostPerTon = TonCost;
        }

        public double GetYieldStrain()
        {
            return YieldStrength / E0;
        }

        public double Getnu()
        {
            return 0.3;
        }
        public double GetG()
        {
            return E0 / (2 * (1 + Getnu()));
        }
    }

    [DataContract(IsReference = true)]
    public class ModelMaterialsInfo
    {
        [DataMember]
        public ConCreteMaterial SlabMaterial;
        [DataMember]
        public ConCreteMaterial ShearWallMaterial;
        [DataMember]
        public ConCreteMaterial ColumnMaterial;
        [DataMember]
        public SteelMaterial MainReinMaterial;
        [DataMember]
        public SteelMaterial MildSteelMaterial;

        //[DataMember]
        //public ElasticIsotropic SlabCommandMaterial;
        [DataMember]
        public SteelUniAxialMaterial MainSteelMaterial;
        [DataMember]
        public Concrete02Material DefaultConcreteMaterial;
        [DataMember]
        public Concrete02Material  ColumnConcreteMaterial;
        [DataMember]
        public PlateRebarMaterial LongitidinalRebar;
        [DataMember]
        public PlateRebarMaterial TransvereRebar;
        [DataMember]
        public PlateFromPlaneStressMaterial ConcreteSlabMaterial;

        public ModelMaterialsInfo()
        {

        }
        public ModelMaterialsInfo(ConCreteMaterial SlabMaterial
              , ConCreteMaterial ShearWallMaterial
              , SteelMaterial MildSteelMaterial
              , SteelMaterial MainReinMaterial
              , ConCreteMaterial _ColumnMaterial)
        {
            this.SlabMaterial = SlabMaterial;
            this.ShearWallMaterial = ShearWallMaterial;
            this.MildSteelMaterial = MildSteelMaterial;
            this.MainReinMaterial = MainReinMaterial;
            this.ColumnMaterial = _ColumnMaterial;
        }
        internal void CreateMaterialsCommands(IDsManager IDM)
        {
            MainSteelMaterial = new SteelUniAxialMaterial(IDM, MainReinMaterial.YieldStrength);
            DefaultConcreteMaterial = new Concrete02Material(IDM, ShearWallMaterial.YieldStrength);
            ColumnConcreteMaterial = new Concrete02Material(IDM, ColumnMaterial.YieldStrength);
            //SlabCommandMaterial = new ElasticIsotropic(IDM,SlabMaterial.GetModulusOfElasticity(),SlabMaterial.Getnu());
            LongitidinalRebar = new PlateRebarMaterial(IDM, MainSteelMaterial, 90);
            TransvereRebar = new PlateRebarMaterial(IDM, MainSteelMaterial, 0);
            ConcreteSlabMaterial = new PlateFromPlaneStressMaterial(IDM,
                new PlaneStressUserMaterial(IDM, ColumnMaterial.YieldStrength));
        }
        internal void ClearMaterialsCommands()
        {
            MainSteelMaterial = null;
            DefaultConcreteMaterial = null;
            ColumnConcreteMaterial = null;
            //SlabCommandMaterial = null;
            LongitidinalRebar = null;
            TransvereRebar = null;
            ConcreteSlabMaterial = null;
        }
        internal void WriteMaterialsCommands(StreamWriter file)
        {
            MainSteelMaterial?.WriteCommand(file);
            DefaultConcreteMaterial?.WriteCommand(file);
            ColumnConcreteMaterial?.WriteCommand(file);
            //SlabCommandMaterial?.WriteCommand(file);
            LongitidinalRebar?.WriteCommand(file);
            TransvereRebar?.WriteCommand(file);
            ConcreteSlabMaterial?.WriteCommand(file);
        }

    }
}
