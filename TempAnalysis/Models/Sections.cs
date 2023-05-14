using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models.ModelMaterial;
using TempAnalysis.OpenseesCommand;
using TempAnalysis.Utilities;

namespace TempAnalysis.Models
{
    public interface IRectSection
    {
        double GetThickness();
        double GetLength();
        double GetA();
        double GetLongitudinalSteelArea();
        List<KeyValuePair<double, double>> GetBarsLocations(double cover);
    }
    public interface IFiberSection
    {
        public RCSection GetFibers(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical);
        public double GetGJ(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical);
        
        public void AddLayerInLocation(List<Layer> SteelLayers, double xValue, double Loc, long MatID, int NoBars, double BarArea)
        {
            SteelLayers.Add(new Layer { Start = new Point2D(-xValue, Loc), End = new Point2D(xValue, Loc), MatID = MatID, NumOfBars = NoBars, FiberArea = BarArea });
            if (Math.Abs(Loc) < 1E-9)
                return;
            SteelLayers.Add(new Layer { Start = new Point2D(-xValue, -Loc), End = new Point2D(xValue, -Loc), MatID = MatID, NumOfBars = NoBars, FiberArea = BarArea });
        }
        public void AddLayerInLocationSpecial(List<Layer> SteelLayers, double xValue, double LBE, double Loc, long MatID, int NoBars, double BarArea)
        {
            SteelLayers.Add(new Layer { Start = new Point2D(xValue, -Loc), End = new Point2D(xValue - LBE, Loc), MatID = MatID, NumOfBars = NoBars, FiberArea = BarArea });
            if (Math.Abs(Loc) < 1E-9)
                return;
            SteelLayers.Add(new Layer { Start = new Point2D(-xValue + LBE, -Loc), End = new Point2D(-xValue, Loc), MatID = MatID, NumOfBars = NoBars, FiberArea = BarArea });
        }
        public void AddLayerInLocationVertical(List<Layer> SteelLayers, double xValue, double Loc, long MatID, int NoBars, double BarArea)
        {
            SteelLayers.Add(new Layer { Start = new Point2D(xValue, -Loc), End = new Point2D(xValue, Loc), MatID = MatID, NumOfBars = NoBars, FiberArea = BarArea });
            if (Math.Abs(Loc) < 1E-9)
                return;
            SteelLayers.Add(new Layer { Start = new Point2D(-xValue, -Loc), End = new Point2D(-xValue, Loc), MatID = MatID, NumOfBars = NoBars, FiberArea = BarArea });
        }
    }
    [DataContract(IsReference = true)]
    public class SteelBarInfo
    {
        [DataMember]
        public int ID;
        [DataMember]
        public int Diameter; //mm
        public SteelBarInfo()
        {

        }
        public SteelBarInfo(int Id, int diameter)
        {
            this.ID = Id;
            this.Diameter = diameter;
        }
        public double Diameter_M()
        {
            return Diameter / 1000.0;
        }
        public double GetArea_M2()
        {
            return Math.PI * Math.Pow(0.5 * Diameter / 1000.0, 2);
        }
    }

    [DataContract(IsReference = true)]
    public class ShellReinforcement
    {
        [DataMember]
        public int NumberOfBarsPerMeter;
        [DataMember]
        public SteelBarInfo SteelBars;

        public ShellReinforcement()
        {

        }
        public ShellReinforcement(int NumberOfBarsPerMeter, SteelBarInfo SteelBars)
        {
            this.NumberOfBarsPerMeter = NumberOfBarsPerMeter;
            this.SteelBars = SteelBars;
        }
        public double GetEqualThickness()
        {
            return SteelBars.GetArea_M2() * NumberOfBarsPerMeter;
        }
        public double GetArea_m2_PerMLength(double Length)
        {
            int N = (int)(NumberOfBarsPerMeter * Length) + 1;
            return N * SteelBars.GetArea_M2();
        }

    }

    [DataContract(IsReference = true)]
    public class SlabSection
    {
        [DataMember]
        public double Thickness;
        [DataMember]
        public ShellReinforcement AdditionalReinforcement;
        [DataMember]
        public ShellReinforcement DefaultReinforcement;
        [DataMember]
        public LayeredShell SectionCommand;
        [DataMember]
        public ElasticShellSection ElasticSection;
        public SlabSection()
        {

        }
        public void CreateSection(IDsManager IDM,long Reinflong, long ReinfTrans, long ConcMat)
        {
            double Cover = 0.025;
            double ReinfThickness = DefaultReinforcement.GetEqualThickness();
            double core = Thickness - 2 * (Cover + 2 * ReinfThickness);

            List<SectionLayer> Layers = new List<SectionLayer>()
            {
                new SectionLayer(ConcMat,Cover),
                new SectionLayer(Reinflong,ReinfThickness),
                new SectionLayer(ReinfTrans,ReinfThickness),
                new SectionLayer(ConcMat,core),
                new SectionLayer(ReinfTrans,ReinfThickness),
                new SectionLayer(Reinflong,ReinfThickness),
                new SectionLayer(ConcMat,Cover),
            };

            this.SectionCommand = new LayeredShell(IDM, Layers);

        }
        public void CreateElasticSection(IDsManager IDM, double Ec, double Nu, double Thickness)
        {
            this.ElasticSection = new ElasticShellSection(IDM)
            {
                E = Ec,
                nu = Nu,
                Rho = 1e-9,
                h = Thickness
            };
        }
    }

    [DataContract(IsReference = true)]
    public class SpecialBoundaryReinforcement
    {
        [DataMember]
        public double LBE = 0;
        [DataMember]
        public double Vertical_SBE_Length = 0;
        [DataMember]
        public SteelBarInfo VerticalBars;
        [DataMember]
        public ShellReinforcement StirrupsBars;
        [DataMember]
        public int No_AlongBars;
        [DataMember]
        public int No_VerticalBars;
        [DataMember]
        public int No_VerticalTies;
        [DataMember]
        public int No_AlongTies;

        public SpecialBoundaryReinforcement()
        {

        }
        internal double GetTransverseSteelVolume(double Thickness)
        {
            double cover = 2 * (0.025 + StirrupsBars.SteelBars.Diameter_M());
            double lockLength = 2 * 10 * StirrupsBars.SteelBars.Diameter_M();
            double B = Thickness - cover;
            double Length = 2 * (LBE + B) + lockLength;
            Length += No_AlongTies * (LBE + lockLength);
            Length += 2 * No_VerticalTies * (B + lockLength);

            int numbers = (int)(Vertical_SBE_Length * StirrupsBars.NumberOfBarsPerMeter) + 1;
            return numbers * StirrupsBars.SteelBars.GetArea_M2() * Length;
        }
        public int GetMinAlongTies()
        {
            return (No_VerticalBars % 2) == 0 ?  (No_VerticalBars / 2 ): (No_VerticalBars / 2 + 1);
        }
        public int GetMinVerticalTies()
        {
            int freebars = No_AlongBars - 2;
            return  (freebars % 2) == 0 ? (freebars / 2) : (freebars / 2 + 1);
        }
        public static double GetAreaSteelArea(int No_AlongBars, int No_VerticalBars, SteelBarInfo steelBarInfo  )
        {
            return 2 * (No_AlongBars + No_VerticalBars) * steelBarInfo.GetArea_M2();
        }
        public int GetTotalVerticalBars()
        {
            return 2 * (No_AlongBars + No_VerticalBars);
        }
        public void GetLocations(double cover, double offset,ref List<KeyValuePair<double, double>> Locations)
        {
            double CurrentLocation = cover + offset;
            CurrentLocation += VerticalBars.Diameter_M() / 2;
            double BarArea = VerticalBars.GetArea_M2();
            double spacing = (LBE - 2 * cover) / (No_AlongBars - 1);

            Locations.Add( new KeyValuePair<double, double>( CurrentLocation, (2+No_VerticalBars) * BarArea) );
            for (int i = 1; i < No_AlongBars-1; i++)
            {
                CurrentLocation += spacing;
                Locations.Add(new KeyValuePair<double, double>(CurrentLocation, 2 * BarArea));
            }
            CurrentLocation += spacing;
            Locations.Add(new KeyValuePair<double, double>(CurrentLocation, (2 + No_VerticalBars) * BarArea));
        }

        internal double GetSteelArea()
        {
            return GetTotalVerticalBars() * VerticalBars.GetArea_M2(); 
        }
    }

    [DataContract(IsReference = true)]
    public class SpecialShearWallReinforcement : IFiberSection, IRectSection
    {
        [DataMember]
        public double Length;
        [DataMember]
        public double Thickness;
        [DataMember]
        public SpecialBoundaryReinforcement SpecialBoundary;

        [DataMember]
        public ShellReinforcement R_VW; //Shear wall vertical Reinforcement;
        [DataMember]
        public ShellReinforcement RHW; //shear wall horizontal reinforcement.

        public SpecialShearWallReinforcement()
        {

        }
        public SpecialShearWallReinforcement(double length, double Thickness, ShellReinforcement R_VW, ShellReinforcement RHW, SpecialBoundaryReinforcement specialBoundaryReinforcement = null)
        {
            this.Length = length;
            this.Thickness = Thickness;
            this.R_VW = R_VW;
            this.RHW = RHW;
            this.SpecialBoundary = specialBoundaryReinforcement;
        }
        public List<KeyValuePair<double, double>> GetBarsLocations(double cover)
        {
            List<KeyValuePair<double, double>> Locations = new List<KeyValuePair<double, double>>();
            double CurrentLocation = cover;
            double MidLength = Length - 2 * cover;
            double BoundaryLength = SpecialBoundary == null ? 0 : SpecialBoundary.LBE;
            double spacing = 1.00 / R_VW.NumberOfBarsPerMeter;
            if (SpecialBoundary != null)
            {
                MidLength -= 2 * (BoundaryLength + spacing);
                SpecialBoundary.GetLocations(cover, 0,ref Locations);
                CurrentLocation = SpecialBoundary.LBE + spacing;
            }
            CurrentLocation += R_VW.SteelBars.Diameter_M() / 2;

            int Bars = (int)Math.Round(MidLength * R_VW.NumberOfBarsPerMeter, MidpointRounding.AwayFromZero) +1;
            double BarsArea = 2 * R_VW.SteelBars.GetArea_M2();
            for (int i = 0; i < Bars; i++)
            {
                Locations.Add(new KeyValuePair<double, double>(CurrentLocation, BarsArea));
                CurrentLocation += spacing;
            }

            if (SpecialBoundary != null)
            {
                SpecialBoundary.GetLocations(cover, Length - BoundaryLength,ref Locations);
            }
            return Locations;
        }
        public RCSection GetFibers(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical)
        {
            int NumberOfRows = (int)Math.Round(Length / 0.05, 1, MidpointRounding.ToEven);
            int NumberOfColumns = (int)Math.Round(Thickness / 0.05, 1, MidpointRounding.ToEven);

            // Without Special Steel for Now
            double SpecialBoundaryLength = SpecialBoundary == null ? 0 : SpecialBoundary.LBE;
            double BarArea = RHW.SteelBars.GetArea_M2();
            double cover = 0.025 + RHW.SteelBars.Diameter_M() / 2;
            double xValue = 0.5 * Length - SpecialBoundaryLength - cover;
            double yValue = 0.5 * Thickness - cover;

            IFiberSection pointer = this as IFiberSection;
            List<Layer> steelLayers = new List<Layer>();
            int NBars = 1 + (int)Math.Round((Length - 2* (SpecialBoundaryLength))/ RHW.NumberOfBarsPerMeter, MidpointRounding.AwayFromZero);
            pointer.AddLayerInLocation(steelLayers, xValue, yValue,SteelMat._ID, NBars, BarArea);
            if (SpecialBoundary != null)
            {
                cover = 0.025 + SpecialBoundary.VerticalBars.Diameter_M() / 2;
                BarArea = SpecialBoundary.VerticalBars.GetArea_M2();
                
                xValue = 0.5 * Length - cover;
                NBars = SpecialBoundary.No_AlongBars;
                pointer.AddLayerInLocationSpecial(steelLayers, xValue, SpecialBoundaryLength,yValue, SteelMat._ID, NBars, BarArea);
                
                NBars = SpecialBoundary.No_VerticalBars;
                yValue -= (Thickness - 2 * cover)/ (SpecialBoundary.No_VerticalBars+1); 
                pointer.AddLayerInLocationVertical(steelLayers,xValue, yValue, SteelMat._ID, NBars, BarArea);
            }

            xValue = 0.5 * Length;
            yValue = 0.5 * Thickness;
            
            RCSection section = new RCSection 
            { 
                ConcretePatch = new Patch 
                { 
                    MatID = ConcMat._ID,
                    NoOfRows = NumberOfRows,
                    NoOfColumns = NumberOfColumns,
                    Vertex = new List<Point2D> 
                    {
                        new Point2D(-xValue,-yValue),
                        new Point2D(xValue,-yValue),
                        new Point2D(xValue,yValue),
                        new Point2D(-xValue,yValue)
                    }
                },
                BarsLayers = steelLayers
            };
            if (Vertical)
                section.Rotate();
            return section;
        }
        public double GetJsForOne_RHW_Layer()
        {
            double SpecialBoundaryLength = SpecialBoundary == null ? 0 : SpecialBoundary.LBE;
            
            double BarArea = RHW.SteelBars.GetArea_M2();
            double BarI = Math.PI * Math.Pow(RHW.SteelBars.Diameter_M(), 4) / 64.0;
            double cover = 0.025 + RHW.SteelBars.Diameter_M() / 2;
            double xValue = 0.5 * Length - SpecialBoundaryLength - cover;
            double yValue = 0.5 * Thickness - cover;
            int NBars = 1 + (int)Math.Round((Length - 2 * (SpecialBoundaryLength)) / RHW.NumberOfBarsPerMeter, MidpointRounding.AwayFromZero);

            // about X 
            double Is = NBars * (2 * BarI + BarArea * Math.Pow(yValue, 2));
            // about Y
            double xSpacing = 2 * xValue / (NBars-1);
            for (int i = 1; i < NBars /2; i++)
            {
                Is += 2 * ( BarArea * Math.Pow(xValue - i * xSpacing, 2) );
            }
            if (SpecialBoundary != null)
            {
                BarArea = SpecialBoundary.VerticalBars.GetArea_M2();
                BarI = Math.PI * Math.Pow(SpecialBoundary.VerticalBars.Diameter_M(), 4) / 64.0;
                cover = 0.025 + SpecialBoundary.VerticalBars.Diameter_M() / 2;
                xValue = 0.5 * Length - cover;
                yValue = 0.5 * Thickness - cover;
                NBars = SpecialBoundary.No_VerticalBars;

                Is += NBars * (2 * BarI + BarArea * Math.Pow(yValue, 2));
                xSpacing = SpecialBoundaryLength / (NBars -1);
                for (int i = 1; i < NBars/2; i++)
                {
                    Is += 2 * (BarArea * Math.Pow(xValue - i * xSpacing, 2));
                }

                NBars = SpecialBoundary.No_AlongBars;
                Is += NBars * (2 * BarI + BarArea * Math.Pow(xValue, 2));

                double ySpacing = (2 * yValue) / (NBars + 1);
                yValue -= ySpacing;
                for (int i = 1; i < NBars/2; i++)
                {
                    Is += 2 * (BarArea * Math.Pow(yValue - i * ySpacing, 2));
                }

            }
            return Is;
        }
        public double GetJs()
        {
            // No Special Reinf for Now;
            return 2 * GetJsForOne_RHW_Layer();
        }
        public double GetGJ(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical)
        {
            double J = (Length * Math.Pow(Thickness,3) + Thickness * Math.Pow(Length, 3)) / 12.0;
            double Js = GetJs();
            return (J-Js) * ConcMat.GetG() + Js * SteelMat.GetG();
        }
        internal double GetI()
        {
            return Thickness * Math.Pow(Length, 3) / 12.0;
        }
        internal double GetLongitudinalReinfRatio()
        {
            double MidLength = Length;
            if (SpecialBoundary != null)
            {
                double spacing = 1.00 / R_VW.NumberOfBarsPerMeter;
                MidLength -= 2 * (SpecialBoundary.LBE + spacing);
            }
            return 2 * R_VW.GetArea_m2_PerMLength(MidLength) / (MidLength *Thickness);
        }
        internal double GetRo()
        {
           return GetLongitudinalSteelArea() / GetA();
        }

        public double GetA()
        {
            return Thickness * Length;
        }
        public double GetLongitudinalSteelArea()
        {
            double Area = 0;
            double MidLength = Length;
            if (SpecialBoundary != null)
            {
                double spacing = 1.00 / R_VW.NumberOfBarsPerMeter;
                MidLength -= 2 * (SpecialBoundary.LBE + spacing);
                Area += 2 * SpecialBoundary.GetSteelArea();
            }
            Area +=  2 * R_VW.GetArea_m2_PerMLength(MidLength);
            return Area;
        }
        public double GetThickness()
        {
            return Thickness;
        }
        public double GetLength()
        {
            return Length;
        }

        public double GetIx()
        {
            return Math.Pow(Length, 3) * Thickness / 12.0;
        }
        public double GetIy()
        {
            return Math.Pow(Thickness, 3) * Length / 12.0;
        }
        public ElasticSectionProperties GenerateElasticProperties(Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial, bool Vertical)
        {
           double Modifier = 1;// E5;
           double Reinf = GetRo();
           return new ElasticSectionProperties
           {
               A = GetA() * Modifier,
               E = (1 - Reinf) * concrete01Material.GetE() + Reinf * steelMaterial.E0,
               G = (1 - Reinf) * concrete01Material.GetG() + Reinf * steelMaterial.GetG(),
               Ix = (Vertical? GetIy(): GetIx() ) * Modifier,
               Iy = (Vertical ? GetIx() : GetIy()) * Modifier,
               J = (GetIx() + GetIy()) * Modifier
           };
        }
    }

    [DataContract(IsReference = true)]
    [KnownType(typeof(SquareColumnSection))]
    [KnownType(typeof(BeamSection))]
    public abstract class FrameElementSection : IFiberSection
    {
        [DataMember]
        public double SectionDepth;
        [DataMember]
        public int StirrupsPerMeter;
        [DataMember]
        public SteelBarInfo StirupsSteelBars;
        [DataMember]
        public int StrirrupsLayers = 1;
        [DataMember]
        public List<double> Strirrips_D = new List<double>();
        public FrameElementSection()
        {

        }
        public FrameElementSection(double sectionDepth, SteelBarInfo StirupsBars, int StirrupsPerMeter)
        {
            this.SectionDepth = sectionDepth;
            this.StirrupsPerMeter = StirrupsPerMeter;
            this.StirupsSteelBars = StirupsBars;
        }

        public abstract double GetA();
        public abstract double GetIx();
        public abstract double GetIy();
        public abstract double GetLongitudinalSteelArea();
        public double GetReinfRatio()
        {
            return GetLongitudinalSteelArea() / GetA();
        }
        public virtual int GetNumberOfLegs()
        {
            return 2 * StrirrupsLayers;
        }
        public virtual RCSection GetFibers(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool vertical)
        {
            return null;
        }
        public virtual double GetGJ(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical)
        {
            return 0;
        }
        public double GetStrirrupsLength(double cover)
        {
            double D = SectionDepth - 2 * cover;

            double Length = 0;
            foreach (var B in Strirrips_D)
            {
                Length += 2 * (B + D) + 2 * 10 * StirupsSteelBars.Diameter_M();
            }
            return Length;
        }

    }
    
    [DataContract(IsReference = true)]
    public class SquareColumnSection : FrameElementSection, IRectSection
    {
        [DataMember]
        public SteelBarInfo MainSteelBars;
        [DataMember]
        public int NumberofBars;
        [DataMember]
        public bool RequireAdditionalStiffners = false;
        public SquareColumnSection()
        {

        }
        public SquareColumnSection(double length, int numOfNars, SteelBarInfo mainsteelBars, int StirrupsPerMeter, SteelBarInfo StirrupsSteelBars)
            : base(length, StirrupsSteelBars, StirrupsPerMeter)
        {
            MainSteelBars = mainsteelBars;
            NumberofBars = numOfNars;
        }
        public override double GetA()
        {
            return Math.Pow(SectionDepth, 2);
        }
        public double GetI()
        {
            return Math.Pow(SectionDepth, 4) / 12.0;
        }
        public override double GetIy()
        {
            return GetI();
        }
        public override double GetIx()
        {
            return GetI();
        }
        public int GetBarsInOneSide()
        {
            return NumberofBars / 4 + 1;
        }
        public double GetAofoneSide()
        {
           return GetBarsInOneSide() * MainSteelBars.GetArea_M2();
        }
        
        public override string ToString()
        {
            string s = $"{SectionDepth}*{SectionDepth} = {NumberofBars}@{MainSteelBars.Diameter} - stirupps: {StirrupsPerMeter}/m @{StirupsSteelBars.Diameter}-{GetNumberOfLegs()} Legs";
            if(RequireAdditionalStiffners)
                s+= $"- Unsafe Shear";
            return s; 
        }
        public double GetIs()
        {
            double Is = NumberofBars * Math.PI * Math.Pow(MainSteelBars.Diameter_M(),4) / 64.0 ;
            double Area = MainSteelBars.GetArea_M2();
            double cover = 0.025 + MainSteelBars.Diameter_M() / 2;
            double xValue = SectionDepth / 2 - cover;
            int BarsInOneSide = GetBarsInOneSide();
            double spacing = (SectionDepth - 2 * cover) / BarsInOneSide;

            Is += 2* BarsInOneSide * Area * Math.Pow(xValue, 2);
            for (int i = 1; i < BarsInOneSide / 2; i++)
            {
                Is += 4 * Area * Math.Pow(xValue - i * spacing, 2);
            }
            return Is;
        }
        public override RCSection GetFibers(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical)
        {
            double BarArea = MainSteelBars.GetArea_M2();
            double cover = 0.025 + MainSteelBars.Diameter_M() / 2;
            double b = 0.5 * SectionDepth;
            double xValue = b - cover;
            
            int BarsInOneSide = GetBarsInOneSide();
            double spacing = (SectionDepth - 2 * cover) / (BarsInOneSide-1);
            List<double> InRowLocations = new List<double>();

            if (BarsInOneSide % 2 == 1)
                InRowLocations.Add(0);

            for (int i = 1; i < BarsInOneSide / 2; i++)
            {
                InRowLocations.Add(xValue - i * spacing);
            }

            List<Layer> SteelLayers = new List<Layer>();
            IFiberSection pointer = this as IFiberSection;
            InRowLocations.ForEach(Loc => pointer.AddLayerInLocation(SteelLayers, xValue, Loc, SteelMat._ID, 2, BarArea));
            pointer.AddLayerInLocation(SteelLayers, xValue, xValue, SteelMat._ID, BarsInOneSide, BarArea);

            //int Rows = (int) Math.Round(SectionDepth / 0.05,1, MidpointRounding.ToEven);
            int Rows = GetBarsInOneSide();
            return new RCSection
            {
                ConcretePatch = new Patch
                {
                    MatID = ConcMat._ID,
                    NoOfRows = Rows,
                    NoOfColumns = Rows,
                    Vertex = new List<Point2D>
                    {
                        new Point2D(-b, -b),
                        new Point2D(b, -b),
                        new Point2D(b, b),
                        new Point2D(-b, b),
                    }
                },
                BarsLayers = SteelLayers
            };
        }
        public override double GetGJ(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical)
        {
            double Js = 2 * GetIs();
            double J = 2 * GetI() - Js;
            return J * ConcMat.GetG() + Js * SteelMat.GetG();
        }
        public override double GetLongitudinalSteelArea()
        {
            return NumberofBars * MainSteelBars.GetArea_M2();
        }
        public double GetThickness()
        {
            return SectionDepth;
        }
        public double GetLength()
        {
            return SectionDepth;
        }
        public List<KeyValuePair<double, double>> GetBarsLocations(double cover)
        {
            List<KeyValuePair<double, double>> result = new List<KeyValuePair<double, double>>();
            int NumberAside = GetBarsInOneSide();
            double As1 = NumberAside * MainSteelBars.GetArea_M2();
            double As2 = 2 * MainSteelBars.GetArea_M2();
            double de = SectionDepth - 2 * cover;
            double spacing = de / (NumberAside - 1);
            double currentLocation = de;
            result.Add(new KeyValuePair<double, double>(currentLocation, As1));
            for (int i = 0; i < NumberAside-1; i++)
            {
                currentLocation += spacing;
                result.Add(new KeyValuePair<double, double>(currentLocation, As2));
            }
            currentLocation += spacing;
            result.Add(new KeyValuePair<double, double>(currentLocation, As1));
            return result;

        }

        public ElasticSectionProperties GenerateElasticProperties(Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial)
        {
            double Modifier = 1;// E5;
            double Reinf = GetReinfRatio();
            return new ElasticSectionProperties
            {
                A = GetA() * Modifier,
                E = (1 - Reinf) * concrete01Material.GetE() + Reinf * steelMaterial.E0,
                G = (1 - Reinf) * concrete01Material.GetG() + Reinf * steelMaterial.GetG(),
                Ix = GetIx() * Modifier,
                Iy = GetIy() * Modifier,
                J = (GetIx() + GetIy()) * Modifier
            };   
        }
    }

    [DataContract(IsReference = true)]
    public class BeamSection : FrameElementSection, IRectSection
    {
        [DataMember]
        public double SectionWidth;
        [DataMember]
        public SteelBarInfo MainSteelBars;
        [DataMember]
        public int NumberOfMainBars; // (oneSides)
        [DataMember]
        public SteelBarInfo SideSteelBars;
        [DataMember]
        public int SideBarsRows;
        public BeamSection()
        {

        }
        public BeamSection(double sectionDepth, double secWidth,SteelBarInfo mainsteelBars, int numOfNars, SteelBarInfo sideSteelBars, int sideBarsRows, SteelBarInfo StirupsBars, int StirrupsPerMeter)
            :base(sectionDepth, StirupsBars, StirrupsPerMeter)
        {
            SectionWidth = secWidth;
            MainSteelBars = mainsteelBars;
            NumberOfMainBars = numOfNars;
            SideSteelBars = sideSteelBars;
            SideBarsRows = sideBarsRows;
        }

        public override double GetA()
        {
           return SectionDepth* SectionWidth;
        }
        public override double GetIy()
        {
            return SectionWidth * Math.Pow(SectionDepth, 3) / 12;
        }
        public override double GetIx()
        {
            return SectionDepth * Math.Pow(SectionWidth, 3) / 12 ;
        }
        public static double GetSteelArea(int NumberOfMainBars, SteelBarInfo MainSteelBars, int SideBarsRows, SteelBarInfo SideSteelBars)
        {
            return 2 * (NumberOfMainBars * MainSteelBars.GetArea_M2() + SideBarsRows * SideSteelBars.GetArea_M2());
        }
        
        public override double GetLongitudinalSteelArea()
        {
            return GetSteelArea(NumberOfMainBars, MainSteelBars, SideBarsRows, SideSteelBars);
        }
        public ElasticSectionProperties GenerateElasticProperties(Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial)
        {
            double Modifier = 1;// E5;
            double Reinf = GetReinfRatio();
            return new ElasticSectionProperties
            {
                A = GetA() * Modifier,
                E = (1 - Reinf) * concrete01Material.GetE() + Reinf * steelMaterial.E0,
                G = (1 - Reinf) * concrete01Material.GetG() + Reinf * steelMaterial.GetG(),
                Ix = GetIx() * Modifier,
                Iy = GetIy() * Modifier,
                J = (GetIx() + GetIy()) * Modifier
            };   
        }
        public override RCSection GetFibers(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool vertical)
        {
            double cover = 0.025 + ( MainSteelBars.Diameter_M() + StirupsSteelBars.Diameter_M()) / 2;
            double spacing = (SectionDepth - 2 * cover) / (SideBarsRows+1);
            List<double> InRowLocations = new List<double>();
            double b = 0.5 * SectionWidth;
            double h = 0.5 * SectionDepth;
            double xValue = b - cover;
            double yValue = h - cover;

            for (int i = 0; i < SideBarsRows / 2; i++)
            {
                InRowLocations.Add(yValue - (i+1) * spacing);
            }

            if (SideBarsRows % 2 == 1)
                InRowLocations.Add(0);

            List<Layer> SteelLayers = new List<Layer>();
            IFiberSection pointer = this as IFiberSection;
            InRowLocations.ForEach(Loc => pointer.AddLayerInLocation(SteelLayers, xValue, Loc, SteelMat._ID, 2, SideSteelBars.GetArea_M2()));
            pointer.AddLayerInLocation(SteelLayers, xValue, yValue, SteelMat._ID, NumberOfMainBars, MainSteelBars.GetArea_M2());

            int Rows = (int)Math.Round(SectionWidth / 0.05, 1, MidpointRounding.ToEven);
            int columns = (int)Math.Round(SectionDepth / 0.05, 1, MidpointRounding.ToEven);
            return new RCSection
            {
                ConcretePatch = new Patch
                {
                    MatID = ConcMat._ID,
                    NoOfRows = Rows,
                    NoOfColumns = columns,
                    Vertex = new List<Point2D>
                    {
                        new Point2D(-b, -h),
                        new Point2D(b, -h),
                        new Point2D(b, h),
                        new Point2D(-b, h),
                    }
                },
                BarsLayers = SteelLayers
            };
        }
        public override double GetGJ(Concrete02Material ConcMat, SteelUniAxialMaterial SteelMat, bool Vertical)
        {
            double Js = GetIxS() + GetIyS();
            double J = GetIx() + GetIy() - Js;
            return J * ConcMat.GetG() + Js * SteelMat.GetG();
        }
        private double GetIyS()
        {
            double cover = 0.025 + MainSteelBars.Diameter_M() / 2;
            double xValue = SectionWidth / 2 - cover;
            double yValue = SectionDepth / 2 - cover;

            // sideBars
            double Is = 2 * SideBarsRows * Math.PI * Math.Pow(SideSteelBars.Diameter_M(), 4) / 64.0;
            Is += 2 * SideBarsRows * SideSteelBars.GetArea_M2() * Math.Pow(xValue, 2);

            // mainBars
            Is = 2 * NumberOfMainBars * Math.PI * Math.Pow(MainSteelBars.Diameter_M(), 4) / 64.0;
            double Area = MainSteelBars.GetArea_M2();
            double spacing = (SectionWidth - 2 * cover) / (NumberOfMainBars-1);
            for (int i = 0; i < NumberOfMainBars / 2; i++)
            {
                Is += 4 * Area * Math.Pow(xValue - (i + 1) * spacing, 2);
            }
            return Is;
        }
        private double GetIxS()
        {
            double cover = 0.025 + MainSteelBars.Diameter_M() / 2;
            double xValue = SectionWidth / 2 - cover;
            double yValue = SectionDepth / 2 - cover;

            // sideBars
            double Is = 2 * SideBarsRows * Math.PI * Math.Pow(SideSteelBars.Diameter_M(), 4) / 64.0;
            double Area = SideSteelBars.GetArea_M2();
            double spacing = (SectionDepth - 2 * cover) / (SideBarsRows + 1);

            Is += 2 * SideBarsRows * Area * Math.Pow(xValue, 2);
            for (int i = 0; i < SideBarsRows / 2; i++)
            {
                Is += 4 * Area * Math.Pow(xValue - (i+1) * spacing, 2);
            }
            // mainBars
            Is = 2 * NumberOfMainBars * Math.PI * Math.Pow(MainSteelBars.Diameter_M(), 4) / 64.0;
            Is += 2 * NumberOfMainBars * MainSteelBars.GetArea_M2() * Math.Pow(yValue, 2);
            return Is;
        }
        public ElasticPPMaterial GetVzMaterial(IDsManager IDM, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial, double BeamLength)
        {
            Point2D VzP = GetVz(concrete01Material, steelMaterial,BeamLength);
            return new ElasticPPMaterial(IDM, VzP.Y, VzP.X);
        }
        internal FlexuralHingeMaterials GetFlexuralMaterials(IDsManager IDM, Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial,double BeamLength)
        {
            double Reinf = GetReinfRatio();
            double GJ = GetGJ(concrete01Material, steelMaterial, false) * 1E-5;
            Point2D MyP = GetMy(concrete01Material, steelMaterial);
            Point2D VzP = GetVz(concrete01Material, steelMaterial,BeamLength);
            return new FlexuralHingeMaterials
            {
                Moment = new ElasticPPMaterial(IDM, MyP.Y, MyP.X),
                Shear = new ElasticPPMaterial(IDM, VzP.Y, VzP.X),
                Torsion = new ElasticMaterial(IDM, GJ)
            };
        }
        public Point2D GetMy(Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial)
        {
            double cover = 0.025;
            double d = SectionDepth - cover; // for cover;
            double concUltimateStrain = concrete01Material.GetUltimateStrain();

            // Quadratic Equation:
            double SteelArea = NumberOfMainBars * MainSteelBars.GetArea_M2();
            double SteelStiffnes = concrete01Material.GetUltimateStrain() * SteelArea * steelMaterial.E0;
            double TermA = 0.85 * 0.85 * concrete01Material.Fc * SectionWidth;
            double TermB = SteelStiffnes - SteelArea * steelMaterial.Fy;
            double TermC = SteelStiffnes * cover;

            double C = (-TermB + Math.Sqrt(4 * TermA * TermC)) / ( 2 * TermA);
            double a = 0.85 * C;
            double CS1 = SteelStiffnes * (C-cover)/C;
            double CC = 0.85 * concrete01Material.Fc * a * SectionWidth;
            double sigmaS2 = steelMaterial.GetSigmay();
            double Ts2 = steelMaterial.Fy * SteelArea;

            double steelArm = 0.5 * SectionDepth - cover;
            double conArm = a > 0.5 * SectionDepth ? 0.5 * (a -SectionDepth) : 0.5 * a + (0.5 * SectionDepth - a);
            double M2 = CS1 * steelArm + CC * conArm + Ts2 * steelArm;

            return new(M2, concUltimateStrain / C);
        }
        public Point2D GetVz(Concrete02Material concrete01Material, SteelUniAxialMaterial steelMaterial,double BeamLength)
        {
            double fcMPa = concrete01Material.Fc / (1.0E+6);
            double Vc = 0.75 * 2 * Math.Sqrt(fcMPa) * SectionDepth * SectionWidth * 1.0E+6;
            double VsMax = 0.75 * 8 * Math.Sqrt(fcMPa) * SectionDepth * SectionWidth * 1.0E+6;
            double Vsteel = GetNumberOfLegs() * StirupsSteelBars.GetArea_M2() * StirrupsPerMeter * steelMaterial.Fy * SectionDepth;
            double V = Math.Min(Vsteel+ Vc, VsMax);

            double Reinf = GetReinfRatio();
            double A = GetA();
            double G = (1 - Reinf) * concrete01Material.GetG() + Reinf * steelMaterial.GetG();
            
            return new Point2D(V, V * BeamLength / ( G * A ));
        }

        public double GetThickness()
        {
            return SectionDepth;
        }
        public double GetLength()
        {
            return SectionWidth;
        }

        public List<KeyValuePair<double, double>> GetBarsLocations(double cover)
        {
            List<KeyValuePair<double, double>> Locations = new List<KeyValuePair<double, double>>();

            double TopBarsArea = NumberOfMainBars * MainSteelBars.GetArea_M2();
            double SideBarsArea = 2 * SideSteelBars.GetArea_M2();

            double spacing = (SectionDepth - 2 * cover) / (SideBarsRows + 1);
            double CurrentLocation = cover;
            Locations.Add(new KeyValuePair<double, double>(CurrentLocation, TopBarsArea));

            for (int i = 0; i < SideBarsRows; i++)
            {
                CurrentLocation += spacing;
                Locations.Add(new KeyValuePair<double, double>(CurrentLocation, SideBarsArea));
            }

            CurrentLocation += spacing;
            Locations.Add(new KeyValuePair<double, double>(CurrentLocation, TopBarsArea));
            return Locations;
        }

        internal double GetSteelVolume(double beamLength, double Walllength)
        {
            double Length = beamLength + 2 * Walllength;
            double BarsLength = Length + SectionDepth;
            if (BarsLength > 12.0)
                BarsLength += 1.00;

            double volume = 2 * BarsLength * MainSteelBars.GetArea_M2() * NumberOfMainBars;

            int numberStirrups = (int) (StirrupsPerMeter * Length) +1;
            volume += GetStrirrupsLength(0.025) * numberStirrups * StirupsSteelBars.GetArea_M2();
            return volume;
        }
    }

    [DataContract(IsReference = true)]
    public class ElasticSectionProperties
    {
        [DataMember]
        public double A;
        [DataMember]
        public double E;
        [DataMember]
        public double G;
        [DataMember]
        public double J;
        [DataMember]
        public double Ix;
        [DataMember]
        public double Iy;

        public ElasticSectionProperties()
        {

        }
        public override string ToString()
        {
            return $"{A} {E} {G} {J} {Ix} {Iy}";
        }
    }
}
