using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;
using TempAnalysis.Models.ModelMaterial;
using TempAnalysis.OpenseesCommand;

namespace TempAnalysis.Utilities
{
    [DataContract(IsReference = true)]
    public class InteractionDiagram
    {
        [DataMember]
        public List<Point2D> A_To_B_points = new List<Point2D>(); // pure Compression to // Compresion + Moment 
        [DataMember]
        public Point2D C; // pure Moment
        [DataMember]
        public Point2D D; // pure tension

        public InteractionDiagram()
        {

        }
        public bool IsSaveSection(double N, double M)
        {
            double M_abs = Math.Abs(M);
            if (Math.Abs(N) < 0.00001)
                return M_abs < C.X;
            else if (N > 0)
            {
                if (M_abs > A_To_B_points.Last().X)
                    return false;
                for (int i = 1; i < A_To_B_points.Count; i++)
                {
                    if (IsInRange(A_To_B_points[i-1], A_To_B_points[i], M_abs))
                        return N < GetNBetween(A_To_B_points.First(), A_To_B_points.Last(), M_abs);
                }
                return false;
            }
            else
                return N > GetNBetween(D,C, M_abs);
        }
        public bool IsInRange(Point2D A, Point2D B, double M)
        {
            return (Math.Abs(A.X - M) < 0.00001) || (Math.Abs(B.X - M) < 0.00001) || (M > A.X && M < B.X);
        }
        public double GetNBetween(Point2D A, Point2D B, double M)
        {
           return  B.Y + (A.Y -B.Y) * (B.X- M) / (B.X-A.X);
        }
    }
    

    [DataContract(IsReference = true)]
    public class ReinforcementUtility
    {
        [DataMember]
        public double SectionCover = 0.025;
        [DataMember]
        public double SteelDensity = 7.850; // t/m3
        [DataMember]
        public double minR0 = 0.01;
        [DataMember]
        public double max_Walls_Ro = 0.0375;
        [DataMember]
        public double maxR0 = 0.04;
        [DataMember]
        public double R0_increment = 0.0025;

        public List<SteelBarInfo> Bars = new List<SteelBarInfo>();
        public ReinforcementUtility()
        {
            Bars = new List<SteelBarInfo>
            {
            new SteelBarInfo(2,8),
            new SteelBarInfo(3,10),
            new SteelBarInfo(4,13),
            new SteelBarInfo(5,16),
            new SteelBarInfo(6,19),
            new SteelBarInfo(7,22),
            new SteelBarInfo(8,25),
            new SteelBarInfo(9,29),
            new SteelBarInfo(10,32),
            new SteelBarInfo(11,36),
            new SteelBarInfo(14,43),
            new SteelBarInfo(18,57),
            };
        }
        public SteelBarInfo GetSteelBarByID(int ID)
        {
            return Bars.FirstOrDefault(x=>x.ID == ID);
        }
        public SteelBarInfo GetSteelBarByDiameter(int Diameter)
        {
            return Bars.FirstOrDefault(x => x.Diameter == Diameter);
        }
        public double GetReinforcementVolumeInM3(SteelBarInfo bar, double Length, int count)
        {
            return bar.GetArea_M2() * Length * count;
        }
        public double GetReinforcementWeightfromVolumeIn_M3(double Volume)
        {
            return Volume * SteelDensity; 
        }
        public double GetShearWallStiffness(double Reinf, double wallLength, double WallWidth,ConCreteMaterial concMaterial, SteelMaterial MainSteel, double FloorHeight)
        {
            double I = WallWidth * Math.Pow(wallLength, 3) / 12.00;   
            double E = (1 - Reinf) * concMaterial.GetModulusOfElasticity() + Reinf * MainSteel.E0;
            return 12 * E * I / Math.Pow(FloorHeight, 3);
        }
        public double GetColumnStiffness(ConCreteMaterial concMaterial, SteelMaterial MainSteel, SquareColumnSection section,double FloorHeight)
        {
            double Reinf = section.GetReinfRatio();
            double E = (1 - Reinf) * concMaterial.GetModulusOfElasticity() + Reinf * MainSteel.E0;
            return 12 * E * section.GetI() / Math.Pow(FloorHeight,3);
        }
        public double GetM(double shear, double FloorHeight)
        {
            return 0.5 * FloorHeight * shear;
        }
        public double GetV(double columnStifness, double FloorHeight, double Cd)
        {
            return  columnStifness * 0.02 * FloorHeight/Cd;
        }
        
        public bool IsColumnSectionIsSafe(SquareColumnSection section, double N, double Cd, double FloorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial MainSteel, SteelMaterial MildSteel )
        {
            if (section == null)
                return false;

            double stiffness = GetColumnStiffness(shearWallMaterial, MainSteel, section, FloorHeight);
            double V = GetV(stiffness, FloorHeight, Cd);

            double Vc = 0.75 * 2 * Math.Sqrt(shearWallMaterial.GetYieldStrength_MPA()) * Math.Pow(section.SectionDepth * 1000, 2);
            double VsMax = 0.75 * 8 * Math.Sqrt(shearWallMaterial.GetYieldStrength_MPA()) * Math.Pow(section.SectionDepth * 1000, 2);

            if (V > VsMax)
                return false;

            double M = GetM(V, FloorHeight);
            InteractionDiagram InteractioDiagram = CreateInteractionDiagram(section,shearWallMaterial,MainSteel,true);
            if (!InteractioDiagram.IsSaveSection(N,M))
                return false;
            if (!InteractioDiagram.IsSaveSection(N, 0))
                return false;

            CorrectShearReinf(ref section);
            if (V < Vc)
            {
                section.StirupsSteelBars = GetSteelBarByID(2);
                section.StirrupsPerMeter = 6;
                return true;
            }
            else
            {
               double Vs = Math.Min((V - Vc)/0.75, VsMax);
               double minS = 0.08;
               foreach (var strirrupsLayers in new List<int> { section.StrirrupsLayers , section.GetBarsInOneSide()/2})
               {
                   section.StrirrupsLayers = strirrupsLayers;
                   foreach (var ID in new List<int>() { 2, 3, 4 })
                   {
                       SteelBarInfo barInfo = GetSteelBarByID(ID);
                       double s = (section.GetNumberOfLegs() * barInfo.GetArea_M2() * MildSteel.YieldStrength * section.SectionDepth) / Vs;
                       if (s < minS)
                           continue;
                       section.StirupsSteelBars = barInfo;
                       section.StirrupsPerMeter = Math.Max(6, (int)Math.Round(1.0 / s, MidpointRounding.AwayFromZero) + 1);
                       return true;
                   }
               }
               section.StirupsSteelBars = GetSteelBarByID(4);
               section.StirrupsPerMeter = 11;
               section.RequireAdditionalStiffners = true;
               return true;
            }
        }
        public double GetMinSpacingBetweenLongBars(double diameter)
        {
            return Math.Max(1.5 * 2.54 / 100 , 1.5 * diameter);
        }
        public double SpacingBetweenLongBars(double d , double diameter,int numberPerSide)
        {
            return (d - numberPerSide * diameter)/(numberPerSide-1);
        }
        public void CorrectShearReinf(ref SquareColumnSection section)
        {
            double d = section.SectionDepth - 2 * (SectionCover + section.StirupsSteelBars.Diameter_M());
            section.StrirrupsLayers = (section.GetBarsInOneSide() / 2) / 2 + 1;
            double s = SpacingBetweenLongBars(d, section.MainSteelBars.Diameter_M(), section.NumberofBars / 4 + 1);
            List<double> dLayers = new List<double>() { d };
            for (int i = 1; i < section.StrirrupsLayers; i++)
            {
                d -= 4 * (s+ section.MainSteelBars.Diameter_M());
                d = Math.Max(d, s + 2 * section.MainSteelBars.Diameter_M());
                dLayers.Add(d);
            }
            section.Strirrips_D = dLayers;
        }
        public void CorrectShearReinf(ref BeamSection section, int Legs)
        {

            double b = section.SectionDepth - 2 * (SectionCover + section.StirupsSteelBars.Diameter_M());
            section.StrirrupsLayers = Legs;
            double s = SpacingBetweenLongBars(b, section.MainSteelBars.Diameter_M(), section.NumberOfMainBars);
            List<double> dLayers = new List<double>() { b };
            for (int i = 1; i < section.StrirrupsLayers; i++)
            {
                b -= 4 * (s + section.MainSteelBars.Diameter_M());
                b = Math.Max(b, s + 2 * section.MainSteelBars.Diameter_M());
                dLayers.Add(b);
            }
            section.Strirrips_D = dLayers;
        }
        public SquareColumnSection GenerateSection(double Length, double AsRatio, List<SteelBarInfo> MainSteelist)
        {
            if (!MainSteelist.Any())
                return null;
            SteelBarInfo MildSteel = this.GetSteelBarByID(3);
            int StirrpsPerMeter = 6;
            double d = Length - 2 * (SectionCover + MildSteel.Diameter_M());
            double As = AsRatio * Math.Pow(Length, 2);
            int index = 0;
            SteelBarInfo MainSteel = MainSteelist[0];
            int LongBars = (int)(Math.Round(As / MainSteel.GetArea_M2(), MidpointRounding.AwayFromZero));
            LongBars += (LongBars % 4) > 0 ? 4 - (LongBars % 4) : 0;
            while (SpacingBetweenLongBars(d, MainSteel.Diameter_M(), LongBars / 4 + 1) < 1.5 * GetMinSpacingBetweenLongBars(MainSteel.Diameter_M()))
            {
                index++;
                if (index == MainSteelist.Count)
                    return null;
                MainSteel = MainSteelist[index];

                As = AsRatio * Math.Pow(Length, 2);
                LongBars = (int)(Math.Round(As / MainSteel.GetArea_M2(), MidpointRounding.AwayFromZero));
                LongBars += (LongBars % 4) > 0 ? 4 - (LongBars % 4) : 0;
            }
            SquareColumnSection section  = new SquareColumnSection(Length, LongBars, MainSteel, StirrpsPerMeter, MildSteel);
            while(section.GetReinfRatio() > maxR0)
            {
                section.SectionDepth += Math.Round(section.GetLength() +0.05, 2, MidpointRounding.AwayFromZero);
            }
            return section;
        }
        public BeamSection GenerateBeamSection(double Depth, double Thickness, double AsRatio, List<SteelBarInfo> MainSteelist)
        {
            if (!MainSteelist.Any())
                return null;

            SteelBarInfo MildSteel = this.GetSteelBarByID(3);

            int StirrpsPerMeter = 6;
            double d = Depth - 2 * (SectionCover + MildSteel.Diameter_M());
            double b = Thickness - 2 * (SectionCover + MildSteel.Diameter_M());
            double As = AsRatio * Depth * Thickness;

            double MaxSpacing = 0.25;
            int index = 0;
            BeamSection section = null;
            while (index < MainSteelist.Count)
            {
                SteelBarInfo MainSteel = MainSteelist[index];
                SteelBarInfo SideBarsSteel = MainSteelist[Math.Max(index - 2, 0)];

                double minSpacing = 1.5 * GetMinSpacingBetweenLongBars(MainSteel.Diameter_M());
                double minSpacingSides =  Math.Max(0.15,1.5 * GetMinSpacingBetweenLongBars(MainSteel.Diameter_M()));

                int minMainBars = (int)(b / Math.Max(MaxSpacing, minSpacing)) + 1;
                int minSideBars = (int)(d / Math.Max(MaxSpacing, minSpacingSides));

                int maxMainBars = (int)(b / minSpacing) + 1;
                int maxSideBars = (int)(d / minSpacing);

                double As1 = BeamSection.GetSteelArea(minMainBars, MainSteel, minSideBars, SideBarsSteel);
                double As2 = BeamSection.GetSteelArea(maxMainBars, MainSteel, minSideBars, SideBarsSteel);
                double As3 = BeamSection.GetSteelArea(maxMainBars, MainSteel, maxSideBars, SideBarsSteel);

                if (As <= As1)
                {
                    section = new BeamSection(Depth, Thickness, MainSteel, minMainBars, SideBarsSteel, minSideBars, MildSteel, StirrpsPerMeter);
                    break;
                }
                else if (As <= As2)
                {
                    int dAs = (int)((As - As1) / MainSteel.GetArea_M2()) + 1;
                    maxMainBars = Math.Min(maxMainBars, minMainBars + dAs);
                    section = new BeamSection(Depth, Thickness, MainSteel, maxMainBars, SideBarsSteel, minSideBars, MildSteel, StirrpsPerMeter);
                    break;
                }
                else if (As <= As3)
                {
                    int dAs = (int)((As - As2) / SideBarsSteel.GetArea_M2()) + 1;
                    maxSideBars = Math.Min(maxSideBars, minSideBars + dAs);
                    section = new BeamSection(Depth, Thickness, MainSteel, maxMainBars, SideBarsSteel, maxSideBars, MildSteel, StirrpsPerMeter);
                    break;
                }
                else
                {
                    section = new BeamSection(Depth, Thickness, MainSteel, maxMainBars, SideBarsSteel, maxSideBars, MildSteel, StirrpsPerMeter);
                    index++;
                }
            }
            return section;
        }
        public List<double> GetAsRatios(double max)
        {
            double R = minR0;
            List<double> ratios = new List<double>() {R};
            while (R < max)
            {
                R += R0_increment;
                R = Math.Round(R,4,MidpointRounding.AwayFromZero);
                ratios.Add(R);
            }
            return ratios;
        }
        private List<double> GetAvailableThicknessFromWallLength(double wallLength, double FloorHeight)
        {
            double minThickness = RoundTo5cm(Math.Max(FloorHeight / 16.0, 0.25));
            minThickness = Math.Max(minThickness, RoundTo5cm(wallLength / 12.00));
            double MaxThickness = RoundTo5cm(wallLength /6.00);
            List<double> results = new List<double>();
            double Thickness = minThickness;
            while (Thickness < MaxThickness)
            {
                results.Add(Thickness);
                Thickness += 0.05;
            }
            results.Add(MaxThickness);
            return results;
        }
        public SpecialShearWallReinforcement GenerateSection(double AsRatio, double Length, double Thickness, List<SteelBarInfo> MainSteelist, List<SteelBarInfo> MildSteelist)
        {
            // set with minimum reinforcement
            double H_As = 0.0025 * Thickness;
            ShellReinforcement HorizontalReinf = null;
            foreach (var mildeSteel in MildSteelist)
            {
                int no_OfBars = Math.Max(5,(int)(0.5 * H_As / mildeSteel.GetArea_M2())+1);
                if (no_OfBars > 15)
                    continue;
                HorizontalReinf = new ShellReinforcement(no_OfBars, mildeSteel);
                break;
            }
            if (HorizontalReinf == null)
            {
                HorizontalReinf = new ShellReinforcement(15, MildSteelist.Last());
            }

            double SteelArea = Thickness * AsRatio * 0.5;
            ShellReinforcement VerticalReinf = null;
            foreach (var steelBarInfo in MainSteelist)
            {
                int numberOfBars = Math.Max( 5, (int)(SteelArea/steelBarInfo.GetArea_M2()));
                double spcaing = 1.00 / numberOfBars;
                if (spcaing > Math.Max(0.08, 1.5 * steelBarInfo.Diameter_M()))
                {
                    VerticalReinf = new ShellReinforcement(numberOfBars, steelBarInfo);
                    break;
                }
            }
            return new SpecialShearWallReinforcement(Length,Thickness, VerticalReinf, HorizontalReinf);
        }
        public SpecialShearWallReinforcement GetShearWallSectionForFirstFloor(ShearWallAppliedForces Forces,double MaxWallLength,double FloorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial MainSteel, SteelMaterial MildSteel, double StartLength)
        {
            List<SteelBarInfo> mainSteelInfos = Bars.Where(x => x.ID >= 4).ToList();
            List<SteelBarInfo> mildSteelInfos = Bars.Where(x => x.ID >= 2).Take(3).ToList();
            List<double> Lengthes = new List<double>();
            double Length = StartLength;
            Lengthes.Add(Math.Round(Length, 1, MidpointRounding.AwayFromZero));
            while (Length < MaxWallLength && Math.Abs(Length - MaxWallLength) > 0.00001)
            {
                Length += 0.5;
                Lengthes.Add(Math.Round(Length, 1, MidpointRounding.AwayFromZero));
            }
            SpecialShearWallReinforcement shearWallReinforcement = null;
            
            List<KeyValuePair<double, double>> Walls_Length_Thickness = new List<KeyValuePair<double, double>>();
            foreach (double WallLength in Lengthes)
            {
                foreach (double Thickness in GetAvailableThicknessFromWallLength(WallLength, FloorHeight))
                {
                    Walls_Length_Thickness.Add(new KeyValuePair<double, double>(WallLength, Thickness));
                }
            }
            Walls_Length_Thickness = Walls_Length_Thickness.OrderBy(LT => LT.Key * LT.Value).ToList();
            
            foreach (double AsRatio in GetAsRatios(max_Walls_Ro))
            {
                foreach (KeyValuePair<double, double> Wall_Length_Thickness in Walls_Length_Thickness)
                {
                    double WallLength = Wall_Length_Thickness.Key;
                    double Thickness = Wall_Length_Thickness.Value;

                    double Stiffness = GetShearWallStiffness(AsRatio, WallLength, Thickness, shearWallMaterial, MainSteel, FloorHeight);
                    bool SpecialBoundaryNeeded = IsSpecialBoundaryNeeded(WallLength, Thickness, Stiffness, Forces, FloorHeight);
                    shearWallReinforcement = GenerateSection(AsRatio, WallLength, Thickness, mainSteelInfos, mildSteelInfos);
                    if (shearWallReinforcement.R_VW == null)
                        continue;
                    if (SpecialBoundaryNeeded)
                    {
                        double AlphaC;
                        double C = GetCompressionZoneLength(WallLength, WallLength, Forces.M, Forces.NC, out AlphaC);
                        GenerateSpecialBoundaryReinforcement(shearWallReinforcement, shearWallMaterial, AsRatio, C, AlphaC, FloorHeight, Forces);
                    }
                    if (!CanSectionResistShear(shearWallReinforcement, Forces, shearWallMaterial))
                        continue;
                    DesignFullShearReinforcement(shearWallReinforcement, FloorHeight, Forces, shearWallMaterial, MildSteel, mildSteelInfos);
                    if (IsShearWallSectionIsSafe(shearWallReinforcement, Forces, FloorHeight, shearWallMaterial, MainSteel, MildSteel))
                        return shearWallReinforcement;
                }
            }
            return shearWallReinforcement;
        }

        private void GenerateSpecialBoundaryReinforcement(SpecialShearWallReinforcement shearWallReinforcement, ConCreteMaterial shearWallMaterial, double wallAsRatio, double c, double alphaC, double FloorHeight, ShearWallAppliedForces forces)
        {
            SpecialBoundaryReinforcement SpecialBoundary = new SpecialBoundaryReinforcement();
            
            #region BarsList
            List<SteelBarInfo> mainSteelInfos = new List<SteelBarInfo>() { shearWallReinforcement.R_VW.SteelBars };
            int index = Bars.IndexOf(shearWallReinforcement.R_VW.SteelBars);
            if (index < Bars.Count-1)
                mainSteelInfos.Add(Bars[index + 1]);
            #endregion

            double AsRatio = Math.Min(3 * wallAsRatio, maxR0);
            
            double Special_Length = 0;
            if (FloorHeight / shearWallReinforcement.Length >= 2.0)
                Special_Length = Math.Max(shearWallReinforcement.Length,forces.M/(4* forces.V));
            if (alphaC > 0.2 * shearWallMaterial.YieldStrength)
                Special_Length = shearWallReinforcement.Length / 2.0;
            Special_Length = Math.Min(shearWallReinforcement.Length/2.0, Special_Length);

            
            double columnLength = RoundTo5cm(Math.Min(0.3 * shearWallReinforcement.Length, Math.Max(c / 2.0, c - 0.1 * shearWallReinforcement.Length)));
            SpecialBoundary.LBE = columnLength;

            double As = AsRatio * columnLength * shearWallReinforcement.Thickness;

            columnLength -= 2 * SectionCover;
            double b = shearWallReinforcement.Thickness - 2 * SectionCover;

            foreach (var steelInfo in mainSteelInfos)
            {
                SpecialBoundary.VerticalBars = steelInfo;

                int RequiredNoBars = (int)(As/steelInfo.GetArea_M2());
                RequiredNoBars = RequiredNoBars % 2 == 0 ? RequiredNoBars : RequiredNoBars+1;

                double MinSpacing = 1.5 * GetMinSpacingBetweenLongBars(steelInfo.Diameter_M());
                double MaxSpacing = Math.Max(1.0 / shearWallReinforcement.R_VW.NumberOfBarsPerMeter, MinSpacing);
                int minAlongBars = ((int)(columnLength / MaxSpacing)) +1;
                int maxAlongBars = ((int)(columnLength / MinSpacing))+1;

                int minCrossBars = Math.Max(0,((int)(b / MaxSpacing)) - 1);
                int maxCrossBars = Math.Max(0,((int)(b / MinSpacing)) - 1);

                if (RequiredNoBars <= 2* (minAlongBars + minCrossBars) ) 
                {
                    SpecialBoundary.No_VerticalBars = minCrossBars;
                    SpecialBoundary.No_AlongBars = minAlongBars;
                    break;
                }
                else if (RequiredNoBars <= 2 * (maxAlongBars + minCrossBars))
                {
                    maxAlongBars = Math.Min(maxAlongBars, (RequiredNoBars - 2 * minCrossBars) / 2); 
                    SpecialBoundary.No_VerticalBars = minCrossBars;
                    SpecialBoundary.No_AlongBars = maxAlongBars;
                    break;
                }
                else if (RequiredNoBars < 2*(maxAlongBars + maxCrossBars))
                {
                    maxCrossBars = Math.Min(maxCrossBars, (RequiredNoBars - 2 * maxAlongBars) / 2);
                    SpecialBoundary.No_VerticalBars = maxCrossBars;
                    SpecialBoundary.No_AlongBars = maxAlongBars;
                    break;
                }
                else
                {
                    SpecialBoundary.No_VerticalBars = maxCrossBars;
                    SpecialBoundary.No_AlongBars = maxAlongBars;
                }
            }

            SpecialBoundary.Vertical_SBE_Length = Special_Length;
            SpecialBoundary.No_VerticalTies = SpecialBoundary.GetMinVerticalTies();

            shearWallReinforcement.SpecialBoundary = SpecialBoundary;
        }

        public static double RoundTo5cm(double value)
        {
           return Math.Round(value * 20.00, MidpointRounding.AwayFromZero) / 20.00;
        }
        public bool IsSaveBeamSection(BeamSection section, double BeamLength, SpecialShearWallReinforcement shearWallReinforcement, double V, ConCreteMaterial shearWallMaterial,SteelMaterial mainReinMaterial, SteelMaterial mildSteelMaterial)
        {
            if (!CanSectionResistShear(section, V, shearWallMaterial))
                return false;

            double L = (BeamLength + shearWallReinforcement.Length) / 2.0;
            double M = V * L;

            if (M > GetMomentCapacity(section, shearWallMaterial, mainReinMaterial))
                return false;

            double Vc = 0.75 * 2 * Math.Sqrt(shearWallMaterial.GetYieldStrength_MPA()) * section.SectionDepth * section.SectionWidth * Math.Pow(1000, 2);
            if (V < Vc)
                return true;

            double Vs = (V - Vc) / 0.75;
            double s = (section.GetNumberOfLegs() * section.StirupsSteelBars.GetArea_M2() * mildSteelMaterial.YieldStrength * section.SectionDepth) / Vs;
            double minS = 0.08;

            if (s < minS)
                return false;
            return true; 
        }

        private double GetMomentCapacity(BeamSection section, ConCreteMaterial shearWallMaterial, SteelMaterial MainSteel)
        {
            double C = Get_C_Distance(section.SectionDepth,MainSteel,shearWallMaterial.GetUltimateStrain());
            double Cover = SectionCover + section.StirupsSteelBars.Diameter_M() + section.MainSteelBars.Diameter_M() / 2.0;
            return 0.9 * GetPureMomentPoint(section, section.GetBarsLocations(Cover), C, shearWallMaterial, MainSteel);
        }

        internal BeamSection GetCouplingBeamSection(ConCreteMaterial shearWallMaterial, SteelMaterial mainReinMaterial, SteelMaterial mildSteelMaterial, SpecialShearWallReinforcement shearWallReinforcement, double bV)
        {
            double WallLength = shearWallReinforcement.Length;
            double WallThickness = shearWallReinforcement.Thickness;
            double BeamLength = Math.Max(1.00, Math.Round(WallLength * 0.5, MidpointRounding.AwayFromZero) / 2.00);

            List<SteelBarInfo> mainSteelInfos = Bars.Where(x => x.ID >=Math.Min(4, shearWallReinforcement.R_VW.SteelBars.ID-2)).ToList();

            BeamSection beamSection = null;
            double Depth = 1.5;
            while (Depth < 2.00)
            { 
                foreach (double AsRatio in GetAsRatios(max_Walls_Ro))
                {
                    beamSection = GenerateBeamSection(Depth, WallThickness, AsRatio, mainSteelInfos);
                    if (!CanSectionResistShear(beamSection, bV, shearWallMaterial))
                        continue;
                    DesignShearReinforcement(beamSection, bV, shearWallMaterial, mildSteelMaterial);
                    if (IsSaveBeamSection(beamSection, BeamLength, shearWallReinforcement, bV, shearWallMaterial, mainReinMaterial, mildSteelMaterial))
                        return beamSection;
                    else
                        beamSection = null;
                }
                Depth = Math.Round(Depth +=0.1, 1, MidpointRounding.AwayFromZero);
            }
            return beamSection;
        }
        public double GetCompressionZoneLength(double WallLength, double WallWidth, double M, double N, out double AlphaC)
        {
            double z = WallWidth * Math.Pow(WallLength, 2) / 6.0;
            double A = WallWidth * WallLength;
            AlphaC = Math.Max(Math.Abs(M) / z + N / A, 0);
            double AlphaT = Math.Max(Math.Abs(M) / z - N / A, 0);
            return AlphaC * WallLength / (AlphaC + AlphaT);
        }
        public bool IsSpecialBoundaryNeeded(double WallLength, double WallWidth, double WallStiffnes, ShearWallAppliedForces Forces, double floorheight)
        {
            double limit = floorheight / WallLength;
            if (limit >= 2.0 || Math.Abs( limit -2.0) < 0.0001)
                return true;
            double AlphaC;
            double C = GetCompressionZoneLength(WallLength, WallWidth, Forces.M, Forces.NC, out AlphaC);

            double Dispalacement = Math.Max(0.005, Forces.F / WallStiffnes);
            limit = WallLength / (600 * 1.5 * Dispalacement);
            return C > limit || Math.Abs(C-limit) < 0.0001;
        }
        public SpecialShearWallReinforcement GetShearWallSectionForNextFloor(ShearWallAppliedForces Forces, double MaxWallLength, double FloorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial MainSteel, SteelMaterial MildSteel, SpecialShearWallReinforcement PreviousSection)
        {
            if (PreviousSection == null)
                return GetShearWallSectionForFirstFloor(Forces, MaxWallLength, FloorHeight, shearWallMaterial, MainSteel, MildSteel,3.00);

            double WallLength = PreviousSection.Length;
            double PreThickness = PreviousSection.Thickness;
            double PreAsRatio = PreviousSection.GetLongitudinalReinfRatio();
            List<SteelBarInfo> mainSteelInfos = new List<SteelBarInfo>();
            if (PreviousSection.R_VW.SteelBars.ID > 4)
                mainSteelInfos.Add(Bars[Bars.IndexOf(PreviousSection.R_VW.SteelBars) - 1]);
            mainSteelInfos.Add(PreviousSection.R_VW.SteelBars);

            List<SteelBarInfo> mildSteelInfos = new List<SteelBarInfo>();
            if (PreviousSection.RHW.SteelBars.ID > 3)
                mildSteelInfos.Add(Bars[Bars.IndexOf(PreviousSection.RHW.SteelBars) - 1]);
            mildSteelInfos.Add(PreviousSection.RHW.SteelBars);

            foreach (double AsRatio in GetAsRatios(max_Walls_Ro).Where(x => x < PreAsRatio || Math.Abs(x - PreAsRatio) < 0.0000001).Where(x => Math.Abs(x - PreAsRatio) < 0.006))
            {
                foreach (double Thickness in GetAvailableThicknessFromWallLength(WallLength, FloorHeight).Where(t=> t < PreThickness || Math.Abs(t- PreThickness) < 0.001).Where(t => Math.Abs(t - PreThickness) < 0.051))
                {
                    double Stiffness = GetShearWallStiffness(AsRatio, WallLength, Thickness, shearWallMaterial, MainSteel, FloorHeight);
                    bool SpecialBoundaryNeeded = IsSpecialBoundaryNeeded(WallLength, Thickness, Stiffness, Forces, FloorHeight);
                    SpecialShearWallReinforcement shearWallReinforcement = GenerateSection(AsRatio, WallLength, Thickness, mainSteelInfos, mildSteelInfos);
                    if (shearWallReinforcement.R_VW == null)
                        continue;
                    if (SpecialBoundaryNeeded)
                    {
                        double AlphaC;
                        double C = GetCompressionZoneLength(WallLength, WallLength, Forces.M, Forces.NC, out AlphaC);
                        GenerateSpecialBoundaryReinforcement(shearWallReinforcement,shearWallMaterial, AsRatio, C, AlphaC, FloorHeight, Forces);
                    }
                    if (!CanSectionResistShear(shearWallReinforcement, Forces, shearWallMaterial))
                        continue;
                    DesignFullShearReinforcement(shearWallReinforcement, FloorHeight, Forces, shearWallMaterial, MildSteel, mildSteelInfos);
                    if (IsShearWallSectionIsSafe(shearWallReinforcement, Forces, FloorHeight, shearWallMaterial, MainSteel, MildSteel))
                        return shearWallReinforcement;
                }
            }

            return PreviousSection;
        }
        public bool IsShearWallVerticalReinforcementSafe(SpecialShearWallReinforcement shearWallReinforcement, ShearWallAppliedForces Forces, ConCreteMaterial shearWallMaterial, SteelMaterial mainSteel)
        {
            InteractionDiagram Diagram = CreateInteractionDiagram(shearWallReinforcement, shearWallMaterial,mainSteel);
            if (!Diagram.IsSaveSection(Forces.NC,Forces.M))
                return false;
            if (Forces is CoupliedShearWallAppliedForces)
            {
                CoupliedShearWallAppliedForces AdForces = Forces as CoupliedShearWallAppliedForces;
                if (!Diagram.IsSaveSection(-AdForces.NT,AdForces.M))
                    return false;
            }
            return true;
        }
        public void MN_CalCulate(IRectSection Section, List<KeyValuePair<double,double>> BarsLocation, double ConCStrain, double C, ConCreteMaterial shearWallMaterial, SteelMaterial mainSteel, out double N2 ,  out double M2)
        {
            
            // Point B Compression + Moment
            double d = Section.GetLength() - SectionCover; // for cover;
            double sigmaS2 = mainSteel.YieldStrength / mainSteel.E0;

            double a = 0.85 * C;
            double CC = 0.85 * shearWallMaterial.YieldStrength * a * Section.GetThickness();
            
            M2 = CC * (C - 0.5 * a);
            double CS1 = 0;
            double TS2 = 0;

            foreach (var BarLocationArea in BarsLocation)
            {
                double Location = BarLocationArea.Key;
                double As1 = BarLocationArea.Value;
                if (Location < C)
                {
                    double CenDistance = (C - Location);
                    double sigmaS1 = ConCStrain * CenDistance / C;
                    double fs1 = sigmaS1 > sigmaS2 ? mainSteel.YieldStrength : sigmaS1 * mainSteel.YieldStrength / sigmaS2;
                    double Force = As1 * fs1;
                    CS1 += Force;
                    M2 += Force * CenDistance;
                }
                else
                {
                    double CenDistance = (Location - C);
                    double Force = As1 * mainSteel.YieldStrength * CenDistance / (d - C);
                    TS2 += Force;
                    M2 += Force * CenDistance;
                }
            }
            N2 = Math.Max(0, CS1 + CC - TS2);
        }
        public double GetPureMomentPoint(IRectSection Section, List<KeyValuePair<double,double>> BarsLocation, double C, ConCreteMaterial shearWallMaterial, SteelMaterial mainSteel)
        {
            int NegativeIndex = 0;
            double NegativeValue = 0; 

            int PositiveIndex = 0;
            for (int i = 0; i < BarsLocation.Count; i++)
            {
                if (BarsLocation[i].Key > C)
                {
                    PositiveIndex = i;
                    break;
                }
            }
            double PositiveValue = 0;
            int Current = (PositiveIndex - NegativeIndex) / 2;
            double concUltimatStrain = shearWallMaterial.GetUltimateStrain();
            double N = 0;
            double M = 0;
            double _C = C;

            while ((PositiveIndex-NegativeIndex)>1)
            {
                _C = BarsLocation[Current].Key;
                MN_CalCulate(Section, BarsLocation, concUltimatStrain, _C,shearWallMaterial,mainSteel, out N, out M);
                if (N < 0)
                {
                    NegativeIndex = Current;
                    NegativeValue = N;
                }
                else
                {
                    PositiveIndex = Current;
                    PositiveValue = N;
                }
                Current = (PositiveIndex - NegativeIndex) / 2;
            }
            _C = BarsLocation[NegativeIndex].Key + (BarsLocation[PositiveIndex].Key - BarsLocation[NegativeIndex].Key) * Math.Abs(NegativeValue)/(Math.Abs(NegativeValue) + Math.Abs(PositiveValue));
            MN_CalCulate(Section, BarsLocation, concUltimatStrain, _C, shearWallMaterial, mainSteel, out N, out M);
            return M;
        }
        public double Get_C_Distance(double SectionDepth, SteelMaterial mainSteel, double concStrain)
        {
            double sigmaS2 = mainSteel.YieldStrength / mainSteel.E0;
            double d = SectionDepth - SectionCover; // for cover;
            return d * concStrain / (sigmaS2 + concStrain);
        }
        public InteractionDiagram CreateInteractionDiagram(IRectSection section, ConCreteMaterial shearWallMaterial, SteelMaterial mainSteel, bool CompressionOnly= false)
        {
            InteractionDiagram ND = new InteractionDiagram();
            List<KeyValuePair<double,double>> BarsLocation = section.GetBarsLocations(SectionCover);
            // point A pure compression 
            double Ag = section.GetA();
            double As = section.GetLongitudinalSteelArea();
            double N1 = As * mainSteel.YieldStrength + 0.85 * shearWallMaterial.YieldStrength * (Ag - As);
            ND.A_To_B_points.Add(new Point2D(0, 0.65 * N1));

            double C =0;
            double concYieldStrain = shearWallMaterial.GetYieldStrain();
            double concUltimateStrain = shearWallMaterial.GetUltimateStrain();

            int Size = 20;
            for (int i = 0; i <= Size; i++)
            {
                double strain = concYieldStrain +  (concUltimateStrain - concYieldStrain)*( ((double)i) / ((double)Size));
                C  = Get_C_Distance(section.GetLength(), mainSteel, strain);
                double N2 = 0;
                double M2 = 0;
                MN_CalCulate(section, BarsLocation, strain, C, shearWallMaterial, mainSteel, out N2, out M2);
                ND.A_To_B_points.Add(new Point2D(0.65 * M2, 0.65 * N2));
                //ND.A_To_B_points.Add(new Point2D(0.9 * M2, 0.65 * N2));
            }
            if (CompressionOnly)
            {
                ND.C = new Point2D(ND.A_To_B_points.Last().X,0);
                ND.C = new Point2D(0,0);
                return ND;
            }
            else 
            { 
                ND.C = new Point2D(0.9 * GetPureMomentPoint(section, BarsLocation, C, shearWallMaterial, mainSteel),0);
                // point D pure Tension
                ND.D = new Point2D(0, -0.9 * As * mainSteel.YieldStrength);
                return ND;
            }
        }
        public bool IsShearWallSectionIsSafe(SpecialShearWallReinforcement shearWallReinforcement, ShearWallAppliedForces Forces, double floorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial mainSteel, SteelMaterial mildSteel)
        {
            if (!IsShearWallVerticalReinforcementSafe(shearWallReinforcement, Forces,shearWallMaterial,mainSteel))
                return false;
            if(!IsShearWallShearReinforcementSafe(shearWallReinforcement, floorHeight, Forces, shearWallMaterial, mildSteel))
                return false;
            return true;
        }
        public bool CanSectionResistShear(BeamSection BeamSection, double  V, ConCreteMaterial shearWallMaterial)
        {
            return V < 0.75 * 8 * Math.Sqrt(shearWallMaterial.GetYieldStrength_MPA()) * BeamSection.SectionDepth * BeamSection.SectionDepth * Math.Pow(1000, 2);
        }
        public bool CanSectionResistShear(SpecialShearWallReinforcement shearWallReinforcement, ShearWallAppliedForces forces, ConCreteMaterial shearWallMaterial)
        {
            double Vmax = 0.75 * 8 * Math.Sqrt(shearWallMaterial.GetYieldStrength_MPA()) * shearWallReinforcement.Length * shearWallReinforcement.Thickness * Math.Pow(1000, 2);
            return forces.V < Vmax;
        }
        public double GetShearWallVc(SpecialShearWallReinforcement shearWallReinforcement, double FloorHeight, ShearWallAppliedForces forces, ConCreteMaterial shearWallMaterial)
        {
            double Ag = shearWallReinforcement.Thickness * shearWallReinforcement.Length;
            double alpha = 0;
            double LengthRatio = FloorHeight / shearWallReinforcement.Length;
            if (LengthRatio < 1.15)
                alpha = 3.0;
            else if (LengthRatio > 2)
                alpha = 2.0;
            else
                alpha = 3.0 - 2 * (LengthRatio - 1.5);

            if (forces is CoupliedShearWallAppliedForces)
            {
                CoupliedShearWallAppliedForces TForces = forces as CoupliedShearWallAppliedForces;
                alpha = Math.Min(alpha, Math.Max(2 * (1 - TForces.NT / Ag), 0));
            }
            return 0.75 * alpha * Math.Sqrt(shearWallMaterial.GetYieldStrength_MPA()) * Ag * Math.Pow(1000, 2);
        }
        public void DesignShearReinforcement(BeamSection beamSection, double V, ConCreteMaterial shearWallMaterial, SteelMaterial mildSteelMaterial)
        {
            double Vc = 0.75 * 2 * Math.Sqrt(shearWallMaterial.GetYieldStrength_MPA())* beamSection.SectionDepth * beamSection.SectionWidth * Math.Pow(1000, 2);
            double minReinf = 0.00025 * beamSection.GetA();
            double Vs = 0;
            if (V < Vc)
                Vs = minReinf;
            else
                Vs = Math.Max(minReinf,(V - Vc) / 0.75);

            double minS = 0.06;
            foreach (var strirrupsLayers in new List<int> { beamSection.NumberOfMainBars / 4, beamSection.NumberOfMainBars/2 })
            {
                beamSection.StrirrupsLayers = strirrupsLayers;
                CorrectShearReinf(ref beamSection, strirrupsLayers);
                foreach (var ID in new List<int>() { 2, 3, 4})
                {
                    SteelBarInfo barInfo = GetSteelBarByID(ID);
                    double s = (beamSection.GetNumberOfLegs() * barInfo.GetArea_M2() * mildSteelMaterial.YieldStrength * beamSection.SectionDepth) / Vs;
                    if (s < minS)
                        continue;
                    beamSection.StirupsSteelBars = barInfo;
                    beamSection.StirrupsPerMeter = Math.Max(6, (int)Math.Round(1.0 / s, MidpointRounding.AwayFromZero) + 1);
                    return;
                }
            }
            beamSection.StirupsSteelBars = GetSteelBarByID(4);
            beamSection.StirrupsPerMeter = 11;
        }
        public void DesignSpecialShearReinforcement(SpecialShearWallReinforcement shearWallReinforcement, ConCreteMaterial shearWallMaterial, SteelMaterial mildSteel)
        {
            SpecialBoundaryReinforcement specialReinf = shearWallReinforcement.SpecialBoundary;
            if (specialReinf == null)
                return;
            double SAch = (shearWallReinforcement.SpecialBoundary.LBE - 2 * SectionCover) * (shearWallReinforcement.Thickness - 2 * SectionCover);
            double SAg = (shearWallReinforcement.SpecialBoundary.LBE) * shearWallReinforcement.Thickness;
            double SVT = Math.Max(0.09, 0.3 * (SAg / SAch - 1)) * shearWallMaterial.YieldStrength / mildSteel.YieldStrength;

            List<SteelBarInfo> MildSteelInfoS = new List<SteelBarInfo>();
            MildSteelInfoS.Add(shearWallReinforcement.RHW.SteelBars);
            int index = Bars.IndexOf(shearWallReinforcement.RHW.SteelBars);
            if (shearWallReinforcement.RHW.SteelBars.ID < 4)
                MildSteelInfoS.Add(Bars[index+1]);
            foreach (var MildSteelInfo in MildSteelInfoS)
            {
                double minSpacing = Math.Max(0.06, 1.5 * MildSteelInfo.Diameter_M());
                int maxNumber = Math.Max(5, (int)(1.00 / minSpacing));
                int innerNumber = shearWallReinforcement.RHW.NumberOfBarsPerMeter;

                while (innerNumber <= maxNumber)
                {
                    double Specialspacing = 1.00 / innerNumber;
                    double MaxSpecialReinf = MildSteelInfo.GetArea_M2() * (2 + specialReinf.No_VerticalBars) / (Specialspacing * shearWallReinforcement.Thickness);
                    if (MaxSpecialReinf > SVT)
                    {
                        int NoOfTies = (int)(SVT * Specialspacing * shearWallReinforcement.Thickness / (0.75 * MildSteelInfo.GetArea_M2())) - 1;
                        NoOfTies = NoOfTies % 2 == 0 ? NoOfTies : NoOfTies + 1;
                        NoOfTies = Math.Max(specialReinf.GetMinAlongTies(), Math.Min(NoOfTies, specialReinf.No_VerticalBars));
                        specialReinf.No_AlongTies = NoOfTies;
                        specialReinf.StirrupsBars = new ShellReinforcement(innerNumber, MildSteelInfo);
                        return;
                    }
                    innerNumber++;
                }

                specialReinf.No_AlongTies = specialReinf.No_VerticalBars;
                specialReinf.StirrupsBars = new ShellReinforcement(maxNumber, MildSteelInfo);
            }
            
        }
        public void DesignFullShearReinforcement(SpecialShearWallReinforcement shearWallReinforcement, double FloorHeight, ShearWallAppliedForces forces, ConCreteMaterial shearWallMaterial, SteelMaterial mildSteel, List<SteelBarInfo> MildSteelist)
        {
            DesignShearReinforcement(shearWallReinforcement,FloorHeight,forces,shearWallMaterial,mildSteel, MildSteelist);
            DesignSpecialShearReinforcement(shearWallReinforcement, shearWallMaterial,mildSteel);
        }
        public void DesignShearReinforcement(SpecialShearWallReinforcement shearWallReinforcement, double FloorHeight, ShearWallAppliedForces forces, ConCreteMaterial shearWallMaterial, SteelMaterial mildSteel, List<SteelBarInfo> MildSteelist)
        {
            double MaxSpacing = Math.Min(3*shearWallReinforcement.Thickness,0.25);
            //double MinSpacing = 

            // get VC
            double Ag = shearWallReinforcement.Thickness * shearWallReinforcement.Length; 
            double VC = GetShearWallVc(shearWallReinforcement,FloorHeight,forces,shearWallMaterial);
            if (VC > forces.V)
                return;
            double VT = forces.V - VC;
            double minRo = 0.0025;

            foreach (SteelBarInfo MildSteelInfo in MildSteelist)
            {
                double minSpacing = Math.Max(0.05, 1.5 * MildSteelInfo.Diameter_M());
                int Number = Math.Max(5, (int)(1.0/ MaxSpacing));
                int maxNumber = Math.Max(5, (int)(1.0/ minSpacing));
                double d = shearWallReinforcement.Length - 2 * SectionCover;
                while (Number <= maxNumber)
                {
                    double spacing = 1.00 / Number;
                    double As = 2 * Number * MildSteelInfo.Diameter_M();
                    double Ro = As / shearWallReinforcement.Thickness;
                    if (Ro < minRo)
                        continue;
                    double VTR = 0.75 * d  * mildSteel.YieldStrength * As/ spacing;
                    if (VTR > VT)
                    {
                        shearWallReinforcement.RHW = new ShellReinforcement(Number, MildSteelInfo);
                        return;
                    }
                    Number++;
                }
                shearWallReinforcement.RHW = new ShellReinforcement(Number, MildSteelInfo);
            }

        }
        private bool IsShearWallShearReinforcementSafe(SpecialShearWallReinforcement shearWallReinforcement, double FloorHeight, ShearWallAppliedForces forces, ConCreteMaterial shearWallMaterial, SteelMaterial mildSteel)
        {
            if (!CanSectionResistShear(shearWallReinforcement,forces,shearWallMaterial))
                return false;

            double Ag = shearWallReinforcement.Thickness * shearWallReinforcement.Length;
            double VC = GetShearWallVc(shearWallReinforcement, FloorHeight, forces, shearWallMaterial);
            if (VC > forces.V)
                return true;

            double spacing = 100.0 / shearWallReinforcement.RHW.NumberOfBarsPerMeter;
            double Ro = 2 * shearWallReinforcement.RHW.NumberOfBarsPerMeter * shearWallReinforcement.RHW.SteelBars.Diameter_M() / (spacing * shearWallReinforcement.Thickness);
            double VT = 0.75 * Ag * mildSteel.YieldStrength * Ro;
            return (VC+VT) > forces.V;
        }

        public SquareColumnSection GetSectionForFirstFLoor(double N, double Cd, double FloorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial MainSteel, SteelMaterial MildSteel)
        {
            List<SteelBarInfo> mainSteelInfos = Bars.Where(x => x.ID >= 4).ToList();
            SquareColumnSection section = null;
            double term = minR0 * MainSteel.YieldStrength + 0.85 * shearWallMaterial.YieldStrength * (1 - minR0);
            double Length = Math.Max(0.45, RoundTo5cm(Math.Sqrt(N / (0.65 * term))));
            while (true)
            {
                foreach (double AsRatio in GetAsRatios(max_Walls_Ro))
                {
                    section = GenerateSection(Length, AsRatio, mainSteelInfos);
                    if (IsColumnSectionIsSafe(section, N, Cd, FloorHeight, shearWallMaterial, MainSteel, MildSteel))
                        return section;
                }
                Length = Math.Round(section.GetLength() + 0.05, 2, MidpointRounding.AwayFromZero);
            }
            return section;
        }
        public SquareColumnSection GetSectionForNextFLoor(double N, double Cd, double FloorHeight, ConCreteMaterial shearWallMaterial, SteelMaterial MainSteel, SteelMaterial MildSteel, SquareColumnSection previousSection)
        {
            if (previousSection == null)
                return GetSectionForFirstFLoor(N,Cd, FloorHeight, shearWallMaterial, MainSteel, MildSteel);

            double PreAsRatio = previousSection.GetReinfRatio();
            List<SteelBarInfo> mainSteelInfos = new List<SteelBarInfo>();
            if (previousSection.MainSteelBars.ID > 4)
            {
                int index = Bars.IndexOf(previousSection.MainSteelBars);
                mainSteelInfos.Add(Bars[index - 1]);
            }
            mainSteelInfos.Add(previousSection.MainSteelBars);

            foreach (double AsRatio in GetAsRatios(max_Walls_Ro).Where(x => x < PreAsRatio || Math.Abs(x- PreAsRatio) < 0.00001).Where(x => Math.Abs(x - PreAsRatio) < 0.006))
            {
                double term = AsRatio * MainSteel.YieldStrength + 0.85 * shearWallMaterial.YieldStrength * (1 - AsRatio);
                double Length = Math.Max( Math.Max(previousSection.SectionDepth - 0.1, 0.35), RoundTo5cm(Math.Sqrt(N / (0.65 * term))) );
                Length = Math.Round(Length, 2, MidpointRounding.AwayFromZero);

                while(Length < previousSection.SectionDepth || Math.Abs(Length - previousSection.SectionDepth)< 0.001)
                {
                    SquareColumnSection section = GenerateSection(Length, AsRatio, mainSteelInfos);
                    if (IsColumnSectionIsSafe(section, Cd, N, FloorHeight, shearWallMaterial, MainSteel, MildSteel))
                        return section;
                    Length = Math.Round(section.GetLength() + 0.05, 2, MidpointRounding.AwayFromZero);
                }
            }
            return previousSection;
        }
        public SquareColumnSection GenerateSquareColumnSection(double Length, SquareColumnSection upperSection = null)
        {
            double coverLength = 0.025;
            SteelBarInfo MildSteel = upperSection == null ? this.GetSteelBarByID(3) : upperSection.StirupsSteelBars;
            int StirrpsPerMeter = upperSection == null ? 6 : upperSection.StirrupsPerMeter;

            SteelBarInfo MainSteel = upperSection == null ? this.GetSteelBarByID(4) : upperSection.MainSteelBars;
            int LongBars = upperSection == null ? 4 : upperSection.NumberofBars;

            double d = Length - 2 * (coverLength + MildSteel.Diameter_M());
            double As = 0.03 * Math.Pow(Length, 2);
            LongBars = (int)(Math.Round(As / MainSteel.GetArea_M2(), MidpointRounding.AwayFromZero));
            LongBars += LongBars % 4;
            while (SpacingBetweenLongBars(d, MainSteel.Diameter_M(), LongBars / 4 + 1) < GetMinSpacingBetweenLongBars(MainSteel.Diameter_M()))
            {
                MainSteel = GetSteelBarByID(MainSteel.ID + 1);
                if (MainSteel == null)
                    return null;

                As = 0.03 * Math.Pow(Length, 2);
                LongBars = (int)(Math.Round(As / MainSteel.GetArea_M2(), MidpointRounding.AwayFromZero));
                LongBars += LongBars % 4;
            }

            return new SquareColumnSection(Length, LongBars, MainSteel, StirrpsPerMeter, MildSteel);
        }
    }
}
