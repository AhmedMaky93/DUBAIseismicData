﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;
using TempAnalysis.Utilities;

namespace TempAnalysis.OpenseesCommand
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(SteelUniAxialMaterial))]
    [KnownType(typeof(PlateRebarMaterial))]
    [KnownType(typeof(ElasticIsotropic))]
    [KnownType(typeof(ElasticPPMaterial))]
    [KnownType(typeof(ConcreteUniaxialMaterial))]
    [KnownType(typeof(Concrete02Material))]
    [KnownType(typeof(ParallelMaterial))]
    [KnownType(typeof(HystericMaterial))]
    [KnownType(typeof(ElasticMaterial))]
    [KnownType(typeof(PlaneStressUserMaterial))]
    [KnownType(typeof(PlateFromPlaneStressMaterial))]
    public abstract class OpenSeesMaterial : BaseCommand
    {
        [DataMember]
        public long _ID;

        public OpenSeesMaterial()
        {

        }
        public OpenSeesMaterial(IDsManager IDM)
        {
            _ID = ++IDM.LastMaterialId;
        }
        public virtual void WritePythonCommand(StreamWriter writer, int Tabs)
        {
            
        }
    }

    [DataContract(IsReference = true)]
    public class SteelUniAxialMaterial : OpenSeesMaterial
    {
        [DataMember]
        public double Fy;
        [DataMember]
        public double E0 = 2.0E+11;

        public SteelUniAxialMaterial()
        {

        }
        public SteelUniAxialMaterial(IDsManager IDM, double fy) : base(IDM)
        {
            this.Fy = fy;
        }
        public override void WriteCommand(StreamWriter writer)
        {
           //writer.WriteLine($"uniaxialMaterial Steel02 {_ID} {Ts(Fy)} {Ts(E0)} 0 18.5 0.925 0.15;");
           //writer.WriteLine($"uniaxialMaterial Steel01 {_ID} {Ts(Fy)} {Ts(E0)} 0.02;");
           writer.WriteLine($"uniaxialMaterial ElasticPP {_ID} {Ts(E0)} {Ts(GetSigmay())} {Ts(-GetSigmay())};");
        }
        public double GetSigmay()
        {
            return Fy /E0;
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
    public class PlateRebarMaterial : OpenSeesMaterial
    {
        [DataMember]
        public SteelUniAxialMaterial Steel01;
        [DataMember]
        public int Angles;

        public PlateRebarMaterial()
        {

        }
        public PlateRebarMaterial(IDsManager IDM, SteelUniAxialMaterial steel01, int angles) : base(IDM)
        {
            this.Steel01 = steel01;
            this.Angles = angles;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"nDMaterial PlateRebar {_ID} {Steel01._ID} {Angles};");
        }
    }

    [DataContract(IsReference = true)]
    public class ElasticIsotropic : OpenSeesMaterial
    {
        [DataMember]
        public double E;
        [DataMember]
        public double nu;
        public ElasticIsotropic()
        {

        }
        public ElasticIsotropic(IDsManager IDM, double E , double nu) : base(IDM)
        {
            this.E = E;
            this.nu = nu;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"nDMaterial ElasticIsotropic {_ID} {Ts(E)} {Ts(nu)};");
        }
    }

    [DataContract(IsReference = true)]
    public class ConcreteUniaxialMaterial : OpenSeesMaterial
    {
        [DataMember]
        public double Fc;
        public ConcreteUniaxialMaterial()
        {

        }
        public ConcreteUniaxialMaterial(IDsManager IDM, double Fc) : base(IDM)
        {
            this.Fc = Fc;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            double CUS = GetUltimateStrain();
            double US = 0.55 * CUS;
            double E = 2 * Fc / US;
            writer.WriteLine($"uniaxialMaterial ElasticPP {_ID} {Ts(E)} {Ts(0.06 * US)} {Ts(-0.7 * US)};");
        }
        public double GetFcu()
        {
            return -0.65 * Fc;
        }
        public double GetFt()
        {
            return 0.1 * Fc;
        }
        public double Getnu()
        {
            return 0.2;
        }
        public double GetG()
        {
            return GetE() / (2 * (1 + Getnu()));
        }
        public double GetE()
        {
            return 4700 * Math.Sqrt(Fc / (1.0E+6)) * (1.0E+6);
        }
        public double GetYieldStrain()
        {
            return 2 * Fc / GetE();
        }
        public double GetUltimateStrain()
        {
            double fcMPa = Fc / (1.0E+6);
            if (fcMPa < 30)
            { return 0.0036; }
            else if (fcMPa > 120)
            { return 0.0027; }
            else
            { return 0.0036 - (fcMPa - 30) * Math.Pow(10, -5); }
        }

    }

    [DataContract(IsReference = true)]
    public class Concrete02Material : OpenSeesMaterial
    {
        [DataMember]
        public double Fc;
        public Concrete02Material()
        {

        }
        public Concrete02Material(IDsManager IDM, double Fc) :base(IDM)
        {
            this.Fc = Fc;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            double CUS = GetUltimateStrain();
            double US = 0.55 * CUS;
            double E = 2 * Fc / US;
            writer.WriteLine($"uniaxialMaterial ElasticPP {_ID} {Ts(E)} {Ts(0.06 * US)} {Ts(-0.7 * US)};");
            //writer.WriteLine($"uniaxialMaterial Concrete02 {_ID} {Ts(-Fc)} {Ts(-GetUltimateStrain()/2)} {Ts(GetFcu())} {Ts(-2 *GetUltimateStrain())} {0.2} {Ts(GetFt())} {Ts(GetFt() / 0.0002)};");
            //writer.WriteLine($"uniaxialMaterial Concrete01 {_ID} {Ts(-Fc)} {Ts(-GetYieldStrain())} {Ts(GetFcu())} {Ts(GetUltimateStrain())};");
        }
        public double GetFcu()
        {
            return -0.80 * Fc;
        }
        public double GetFt()
        {
            return 0.1 * Fc;
        }
        public double Getnu()
        {
            return 0.2;
        }
        public double GetG()
        {
            return GetE() / (2 * (1 + Getnu()));
        }
        public double GetE()
        {
            return 4700 * Math.Sqrt(Fc / (1.0E+6)) * (1.0E+6);
        }
        public double GetYieldStrain()
        {
            return 2 * Fc / GetE();
        }
        public double GetUltimateStrain()
        {
            double fcMPa = Fc / (1.0E+6);
            if (fcMPa < 30)
            { return 0.0036; }
            else if (fcMPa > 120)
            { return 0.0027; }
            else
            { return 0.0036 - (fcMPa - 30) * Math.Pow(10, -5); }
        }

    }

    [DataContract(IsReference = true)]
    public class PlaneStressUserMaterial : OpenSeesMaterial
    {
        [DataMember]
        public Concrete02Material AdMaterial;
        public PlaneStressUserMaterial()
        {

        }
        public PlaneStressUserMaterial(IDsManager IDM, Concrete02Material AdMaterial) : base(IDM)
        {
            this.AdMaterial = AdMaterial;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"nDMaterial PlaneStressUserMaterial {_ID} 40 7 {Ts(AdMaterial.Fc)} {Ts(AdMaterial.GetFt())} {Ts(AdMaterial.GetFcu())} {Ts(-AdMaterial.GetYieldStrain())} {Ts(-AdMaterial.GetUltimateStrain())} {Ts(0.0015)} {0.2};");
            //writer.WriteLine($"nDMaterial PlaneStressUserMaterial {_ID} 40 7 {Ts(AdMaterial.Fc)} {AdMaterial.Ts(AdMaterial.GetFt())} {Ts(AdMaterial.GetFcu())} {Ts(-AdMaterial.GetUltimateStrain()/2)} {Ts(-2* AdMaterial.GetUltimateStrain())} {Ts(0.0015)} {0.2};");
            //writer.WriteLine($"nDMaterial PlaneStressUserMaterial {_ID} 40 7 {Ts(AdMaterial.Fc)} {0.0} {Ts(AdMaterial.GetFcu())} {Ts(-AdMaterial.GetYieldStrain())} {Ts(-AdMaterial.GetUltimateStrain())} {0.0} {0.0};");
        }
    }

    [DataContract(IsReference = true)]
    public class PlateFromPlaneStressMaterial : OpenSeesMaterial
    {
        [DataMember]
        public PlaneStressUserMaterial PlaneStressUserMaterial;

        public PlateFromPlaneStressMaterial()
        {

        }
        public PlateFromPlaneStressMaterial(IDsManager IDM, PlaneStressUserMaterial planeStressUserMaterial) : base(IDM)
        {
            PlaneStressUserMaterial = planeStressUserMaterial;
        }

        public double Getnu()
        {
            return 0.2;
        }
        public double GetG()
        {
            return GetE() / (2 * (1 + Getnu()));
        }

        public double GetE()
        {
            return PlaneStressUserMaterial.AdMaterial.GetE();
        }

        public override void WriteCommand(StreamWriter writer)
        {
            PlaneStressUserMaterial?.WriteCommand(writer);
            writer.WriteLine($"nDMaterial PlateFromPlaneStress {_ID} {PlaneStressUserMaterial._ID} 0.132010520833333E+11;");
        }
    }
    [DataContract(IsReference = true)]
    public class ParallelMaterial : OpenSeesMaterial
    {
        [DataMember]
        public long Mat1;
        [DataMember]
        public long Mat2;

        public ParallelMaterial()
        {

        }
        public ParallelMaterial(IDsManager IDM, long Mat1, long Mat2):base(IDM)
        {
            this.Mat1 = Mat1;
            this.Mat2 = Mat2;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"uniaxialMaterial Parallel {_ID} {Mat1} {Mat2};");
        }
    }

    [DataContract(IsReference = true)]
    public class ElasticPPMaterial : OpenSeesMaterial
    {
        [DataMember]
        public double E;
        [DataMember]
        public double ElasticStrain;

        public ElasticPPMaterial()
        {

        }
        public ElasticPPMaterial(IDsManager IDM, double E, double elasticStrain) : base(IDM)
        {
            this.E = E;
            this.ElasticStrain = elasticStrain;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"uniaxialMaterial ElasticPP {_ID} {Ts(E)} {Ts(ElasticStrain)} {Ts(-ElasticStrain)};");
        }
    }
    [DataContract(IsReference = true)]
    public class ElasticMaterial : OpenSeesMaterial
    {
        [DataMember]
        public double E;

        public ElasticMaterial()
        {

        }
        public ElasticMaterial(IDsManager IDM,double E ):base(IDM)
        {
            this.E = E;
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"uniaxialMaterial Elastic {_ID} {Ts(E)};");
        }
    }
    [DataContract(IsReference = true)]
    public class HystericMaterial : OpenSeesMaterial
    {
        [DataMember]
        public MaterialPart Tension;
        [DataMember]
        public MaterialPart Compresion;
        [DataMember]
        public double Pinching = 0.6;
        [DataMember]
        public double beta = 0.4;

        public HystericMaterial()
        {

        }
        public HystericMaterial(IDsManager IDM):base(IDM)
        {
        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"uniaxialMaterial Hysteretic {_ID} {Tension} {Compresion} {Pinching} {Pinching} 0.0 0.0 {beta};");
        }
        public override void WritePythonCommand(StreamWriter writer, int Tabs)
        {
            string tabs = "";
            for (int i = 0; i < Tabs; i++)
            {
                tabs += "\t";
            }
            string Command = $"ops.uniaxialMaterial('Hysteretic', {_ID}, {Tension.ToPy()}, {Compresion.ToPy()}, 0.6, 0.6, 0.0, 0.0, 0.4)";
            writer.WriteLine(tabs+ Command);

        }
    }

    [DataContract(IsReference = true)]
    public class MaterialPart
    {
        // X for Stress and Y for Strain
        [DataMember]
        public Point2D P1;
        [DataMember]
        public Point2D P2;
        [DataMember]
        public Point2D P3;
        public MaterialPart()
        {

        }
        public override string ToString()
        {
            return  ($"{P1} {P2}")+ (P3 == null ? "":$" {P3}");
        }
        public MaterialPart Multiply(double f)
        {
            return new MaterialPart { P1 = P1.Multiply(f), P2 = P2.Multiply(f), P3 = (P3 == null? null: P3.Multiply(f)) };
        }
        public string ToPy()
        {
            double xf = 1;// 0.000225;
            return ($"{P1.ToPy(xf)}, {P2.ToPy(xf)}") + (P3 == null ? "" : $", {P3.ToPy(xf)}");
        }

    }

}
