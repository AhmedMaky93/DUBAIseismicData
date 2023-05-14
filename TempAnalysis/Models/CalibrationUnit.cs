using ResultsVisualizationUtiliy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.OpenseesCommand;
using TempAnalysis.Utilities;

namespace TempAnalysis.Models
{
    [DataContract(IsReference = true)]
    public class PushOverResults
    {
        public List<List<double>> Displacements = new List<List<double>>();

        [DataMember]
        public double K;
        [DataMember]
        public List<Point2D> CurveList = new List<Point2D>();
        [DataMember]
        public List<double> InterSotryDrifts = new List<double>();
        [DataMember]
        public ErrorsRange InterSotryDriftsRanges = new ErrorsRange();
        [DataMember]
        public double YieldDisplacement;
        [DataMember]
        public double UltimateDisplacement;
        [DataMember]
        public double PeakStrength;
        [DataMember]
        public double Ductility;
        [DataMember]
        public double CurveArea;
        [DataMember]
        public double Time;
        [DataMember]
        public Point2D MaxV;

        public PushOverResults()
        {

        }
        public void SetInterStoryDrifts(double FloorHeight)
        {
            int Steps = Math.Min(CurveList.Count, Displacements.Count);
            List<double> LastDisplacments = Displacements[Steps - 1];
            InterSotryDrifts = new List<double>();
            InterSotryDrifts.Add(LastDisplacments[0]);
            for (int i = 1; i < LastDisplacments.Count; i++)
            {
                InterSotryDrifts.Add((LastDisplacments[i] - LastDisplacments[i - 1]) / FloorHeight);
            }
            InterSotryDriftsRanges = new ErrorsRange(InterSotryDrifts);
        }
        public void ResetCurveArea(int CurveCount)
        {
            CurveArea = 0;
            CurveList.Insert(0, new Point2D(0, 0));
            for (int i = 1; i < CurveCount; i++)
            {
                Point2D p1 = CurveList[i - 1];
                Point2D p2 = CurveList[i];
                CurveArea += (p2.X - p1.X) * (p1.Y + p2.Y) / 2.0;
            }
        }
        public Point2D GetTangetPoint(double Ratio)
        {
            double randomY = Ratio * PeakStrength;

            double minError = double.MaxValue;
            Point2D point = CurveList[0];
            for (int i = 1; i < CurveList.Count; i++)
            {
                double Error = CurveList[i].Y - randomY;
                if (Math.Abs(Error)< minError)
                {
                    minError = Math.Abs(Error);
                    point = CurveList[i];
                }

                if (Error > 0)
                    break;
            }
            return point;
        }
        public double GetStrengthAt(double x) 
        {
            double minError = double.MaxValue;
            Point2D point = CurveList[0];
            for (int i = 1; i < CurveList.Count; i++)
            {
                double Error = CurveList[i].X - x;
                if (Math.Abs(Error) < minError)
                {
                    minError = Math.Abs(Error);
                    point = CurveList[i];
                }
                if (Error > 0)
                    break;
            }
            return point.Y;
        }
        public Point2D GetYieldPoint()
        {
            Point2D p1 = GetTangetPoint(0.1);
            double Slope1 = p1.Y / p1.X;
            Point2D p2 = p1;
            int index1 = CurveList.IndexOf(p1);
            for (int i = index1 + 1; i < CurveList.Count; i++)
            {
                double slope2 = (CurveList[i].Y - CurveList[i-1].Y) / (CurveList[i].X - CurveList[i-1].X);
                double Error = Math.Abs((slope2 - Slope1) / Slope1);
                if (Error > 0.2)
                    break;
                p2 = CurveList[i];
            }

            return p2;
        }
        public double GetYieldStrength()
        {
            return GetYieldPoint().Y;
        }
        public void SetProperties()
        {
            if (CurveList == null || CurveList.Count == 0)
                return;
            MaxV = CurveList[0];
            for (int i = 1; i < CurveList.Count; i++)
            {
                if (CurveList[i].Y > MaxV.Y && Math.Abs(CurveList[i].Y - MaxV.Y) > 1e-9)
                    MaxV = CurveList[i];
            }

            PeakStrength = MaxV.Y;
            ResetCurveArea(CurveList.Count);
            Line2D topLine = new Line2D(new Point2D(0.0, MaxV.Y), new Point2D(CurveList.Last().X, MaxV.Y));

            
            Line2D initLine = new Line2D(CurveList[0], GetTangetPoint(0.1));
            Point2D intersect;
            initLine.IntersectWith(topLine, out intersect);
            YieldDisplacement = intersect.X;
            Line2D Vmax2Line = topLine;
            int index = CurveList.IndexOf(MaxV);
            double y = MaxV.Y;
            intersect = CurveList.Last();
            if (index < CurveList.Count - 1)
            {
                var PlasticCurve = CurveList.GetRange(index, CurveList.Count - index);
                y = Math.Max(PlasticCurve.Min(x => x.Y), 0.8 * MaxV.Y);
                Vmax2Line = new Line2D(new Point2D(0.0, y), new Point2D(intersect.X, y));

                for (int i = 0; i < PlasticCurve.Count - 1; i++)
                {
                    Line2D curveSegement = new Line2D(PlasticCurve[i], PlasticCurve[i + 1]);
                    Point2D point;
                    if (Vmax2Line.IntersectWith(curveSegement, out point) && curveSegement.IsOnSegment(point))
                    {
                        intersect = point;
                        break;
                    }
                }
            }
            UltimateDisplacement = intersect.X;
            Ductility = UltimateDisplacement / YieldDisplacement;
        }

        internal double GetSlope()
        {
            Point2D p = GetTangetPoint(0.1);
            return p.Y/ p.X;
        }
        internal double GetCurveAreaOfPlasticZone()
        {
            Point2D p = GetYieldPoint();
            double minY = p.Y;
            int start = CurveList.IndexOf(p) + 1;
            double Area = 0;
            int i = 0;
            double Y = 0;
            double X = 0;
            for (i = start; i < CurveList.Count-1; i++)
            {
                Y = CurveList[i].Y - minY;
                X = 0.5 * (CurveList[i+1].X - CurveList[i-1].X);
                Area += Y * X;
            }
            i = CurveList.Count - 1;
            Y = CurveList[i].Y - minY;
            X = 0.5 * (CurveList[i].X - CurveList[i - 1].X);
            Area += Y * X;
            return Area;
        }

        internal double GetLocationWithPeakRatio(double udiff)
        {
            return GetTangetPoint(1-Math.Abs(1 - udiff)).X;
        }
    }

    [DataContract(IsReference = true)]
    public class PushOverResultsComaprsion
    {
        [DataMember]
        public PushOverResults DetailedModelResult;
        [DataMember]
        public PushOverResults SimplifiedModelResult;
        [DataMember]
        public ErrorsRange ErrorsInterSotryDriftsRanges;
        [DataMember]
        public List<double> Diffs;
        [DataMember]
        public double MSE;
        [DataMember]
        public double SlopeError;
        [DataMember]
        public double DuctilityError;
        [DataMember]
        public double PeakStrengthError;
        [DataMember]
        public double CurveAreaError;
        [DataMember]
        public double TimeDevRatio;

        public PushOverResultsComaprsion()
        {

        }
        public PushOverResultsComaprsion(PushOverResults detailedModelResult)
        {
            DetailedModelResult = detailedModelResult;
        }
        public void UpdateValues()
        {
            Diffs = new List<double>();
            int Count = Math.Min(DetailedModelResult.InterSotryDrifts.Count, SimplifiedModelResult.InterSotryDrifts.Count);
            for (int i = 0; i < Count; i++)
            {
                Diffs.Add(GetError(DetailedModelResult.InterSotryDrifts[i], SimplifiedModelResult.InterSotryDrifts[i]));
            }
            ErrorsInterSotryDriftsRanges = new ErrorsRange(Diffs);
            TimeDevRatio = SimplifiedModelResult.Time / DetailedModelResult.Time;
            CurveAreaError = GetError(DetailedModelResult.CurveArea, SimplifiedModelResult.CurveArea);
            PeakStrengthError = GetError(DetailedModelResult.PeakStrength, SimplifiedModelResult.PeakStrength);
            DuctilityError = GetError(DetailedModelResult.Ductility, SimplifiedModelResult.Ductility);
            SetSlopeError();
            SetMSE();
        }
        public void Update(PushOverResults simplifiedModelResult)
        {
            SimplifiedModelResult = simplifiedModelResult;
            UpdateValues();
            UpdateFrm();
        }

        private void UpdateFrm()
        {
            
        }

        private void SetSlopeError()
        {
            SlopeError = GetError(DetailedModelResult.GetSlope(), SimplifiedModelResult.GetSlope());
        }

        public int GetN()
        {
            return Math.Max(DetailedModelResult.CurveList.Count, SimplifiedModelResult.CurveList.Count) ;
        }
        public double GetCriticalMSE()
        {
            return Math.Sqrt(Math.Pow(0.01,2) * GetN()) / GetN();
        }
        public double GetAbsMSE()
        {
            return MSE * GetN();
        }
        public void GetDifferentArea(out double A1, out double A2)
        {
            A1 = 0;
            A2 = 0;

            int count1 = DetailedModelResult.CurveList.Count;
            int count2 = SimplifiedModelResult.CurveList.Count;

            double Y1 = 0;
            double Y2 = 0;
            int Max = count1 - 1;
            for (int i = 1; i < Max; i++)
            {
                Y1 = DetailedModelResult.CurveList[i].Y;
                Y2 = i >= count2 ? 0 : SimplifiedModelResult.CurveList[i].Y;

                if (Y1 > Y2)
                    A1 = 0.5 * Math.Abs(Y1 - Y2) * (DetailedModelResult.CurveList[i+1].X - DetailedModelResult.CurveList[i-1].X);
                else
                    A2 = 0.5 * Math.Abs(Y1 - Y2) * (DetailedModelResult.CurveList[i+1].X - DetailedModelResult.CurveList[i-1].X);
            }

            Y1 = DetailedModelResult.CurveList[Max].Y;
            Y2 = count1 >= count2 ? 0 : SimplifiedModelResult.CurveList[Max].Y;

            if (Y1 > Y2)
                A1 = 0.5 * Math.Abs(Y1 - Y2) * (DetailedModelResult.CurveList[Max].X - DetailedModelResult.CurveList[Max - 1].X);
            else
                A2 = 0.5 * Math.Abs(Y1 - Y2) * (DetailedModelResult.CurveList[Max].X - DetailedModelResult.CurveList[Max - 1].X);
        }
        private void SetMSE()
        {
            int count1 = DetailedModelResult.CurveList.Count;
            int count2 = SimplifiedModelResult.CurveList.Count;
            int count = Math.Max(count1,count2);
            
            MSE = 0;
            
            for (int i = 0; i < count; i++)
            {
                double Y1 = i>= count1 ? 0: DetailedModelResult.CurveList[i].Y;
                double Y2 = i>= count2 ? 0: SimplifiedModelResult.CurveList[i].Y;
                MSE += Math.Pow(GetError(Y1, Y2),2);
            }
            MSE = Math.Sqrt(MSE)/count;
        }
        public double GetError(double TrueValue, double ErroredValue)
        {
            if (Math.Abs(TrueValue) < 1E-9)
                return 0.0;
            return (ErroredValue - TrueValue) / TrueValue;
        }
        internal void CloseRepresentationForm()
        {
        }
    }

    [DataContract(IsReference = true)]
    public class ErrorsRange
    {
        [DataMember]
        public double Min;
        [DataMember]
        public double Max;
        [DataMember]
        public double Average;
        public ErrorsRange()
        {

        }
        public ErrorsRange(List<double> Values)
        {
            List<double> AbsValues = Values.Select(x => Math.Abs(x)).ToList();
            Min = AbsValues.Min();
            Max = AbsValues.Max();
            Average = AbsValues.Average();
        }

    }

    [DataContract(IsReference = true)]
    public class ModalAnalysisResult
    {
        [DataMember]
        public List<double> detailedModelPeriods = new List<double>();
        [DataMember]
        public List<double> simplifiedModelPeriods = new List<double>();
        [DataMember]
        public List<double> Error = new List<double>();

        public ModalAnalysisResult()
        {

        }
        public ModalAnalysisResult(List<ModeShapeData> detailedModelModes, List<ModeShapeData> SimplifiedModelModes)
        {
            detailedModelPeriods = new List<double>();
            detailedModelPeriods.Add(detailedModelModes[0].Period);
            detailedModelPeriods.Add(detailedModelModes[1].Period);

            simplifiedModelPeriods = new List<double>();
            simplifiedModelPeriods.Add(SimplifiedModelModes[0].Period);
            simplifiedModelPeriods.Add(SimplifiedModelModes[1].Period);

            Error.Add(GetError(detailedModelPeriods[0], simplifiedModelPeriods[0]));
            Error.Add(GetError(detailedModelPeriods[1], simplifiedModelPeriods[1]));
        }
        public static double GetError(double TrueValue, double ErroredValue)
        {
            if (Math.Abs(TrueValue) < 1E-9)
                return 0.0;
            return (ErroredValue - TrueValue) / TrueValue;
        }

    }
    [DataContract(IsReference = true)]
    public class CalibrationUnit
    {
        [DataMember]
        public DetailedModel DetailedModel;
        [DataMember]
        public SimplifiedModel SimplifiedModel;
        [DataMember]
        public double Alpha;
        [DataMember]
        public double Sigma1;
        [DataMember]
        public ModalAnalysisResult ModalAnalysisResult;
        [DataMember]
        public PushOverResultsComaprsion PushOverResultsComaprsion;
        public CalibrationUnit()
        {

        }
        public CalibrationUnit(DetailedModel detailedModel)
        {
            DetailedModel = detailedModel;
        }
        public double GetValue(double Min, double V, double Max)
        {
            return Math.Max(Min, Math.Min(V, Max));
        }

        protected async Task CalibrateLinearParameters(List<Point2D> alpha_Sigma1)
        {
            double Alpha_min = 0.05;
            double Alpha_max = 10;

            double A = Alpha_min;
            double B = Alpha_max;
            Alpha = A;
            double E_A = await TryAlpha2(alpha_Sigma1);
            Alpha = B;
            double E_B = await TryAlpha2(alpha_Sigma1);

            while (true)
            {
                double C = (A + B) / 2.0;
                Alpha = C;
                double Error = await TryAlpha2(alpha_Sigma1);

                if (Math.Abs(Error) < 1e-5)
                    break;
                if (ISameSign(E_A, Error))
                {
                    Error = E_A;
                    A = C;
                }
                else if (ISameSign(E_B, Error))
                {
                    Error = E_B;
                    B = C;
                }
            }
            await SimplifiedModel.RunModalAnalysis();
        }

        protected async Task CalibrateNonLinearParameters(List<double> bestValues)
        {
            List<double> CurrentValues = bestValues;
            double minMSE = double.MaxValue;
            double ydiff = 0.3;
            while (ydiff < 0.7)
            {
                double Udiff = 0.98;
                CurrentValues = bestValues;
                while (Udiff < 1.04)
                {
                    CurrentValues = await PushOverTrial(ydiff, Udiff, CurrentValues);
                    if (PushOverResultsComaprsion.MSE < minMSE)
                    {
                        minMSE = PushOverResultsComaprsion.MSE;
                        bestValues = CurrentValues;
                    }
                    Udiff += 0.02;
                }
                ydiff += 0.05;
            }
            await RunPushOverCurve(bestValues);
        }
        public async Task StartCalibration(List<Point2D> alpha_Sigma1, string folderpath)
        {
            SimplifiedModel = new SimplifiedModel { Name = DetailedModel.Name, ResponseParameters = DetailedModel.ResponseParameters };
            SimplifiedModel.CreateMainElements(DetailedModel.LayoutUtility.FloorHeight, DetailedModel.NumOfFloors
                                    , DetailedModel.GetFloorMass(1), DetailedModel.GetFloorMass(DetailedModel.NumOfFloors)
                                    , DetailedModel.GetFloorGravityLoad(1), DetailedModel.GetFloorGravityLoad(DetailedModel.NumOfFloors));

            SimplifiedModel.MoveOpenseesFiles(DetailedModel.GetModelDirPath(folderpath));
            this.PushOverResultsComaprsion = new PushOverResultsComaprsion(DetailedModel.PushOverResults);

            await CalibrateLinearParameters(alpha_Sigma1);

            List<double> bestValues = new List<double> { 1.1, 1.5, 2 };
            await RunPushOverCurve(bestValues);

            //await CalibrateNonLinearParameters(bestValues);

            this.PushOverResultsComaprsion.CloseRepresentationForm();
            await RunSimplifiedModelModalAnalysis();
        }
        private bool ISameSign(double e1, double e2)
        {
            return e1 / e2 > 0;
        }
        private async Task<List<double>> PushOverTrial(double Ydiff, double Udiff, List<double> bestValues)
        {
            double minMSE = double.MaxValue;
            List<double> CurrentValues = bestValues;
            double Yd = PushOverResultsComaprsion.DetailedModelResult.GetYieldStrength();
            double PeakDetailed = PushOverResultsComaprsion.DetailedModelResult.PeakStrength;
            Yd += Ydiff * (PeakDetailed - Yd);
            int Trials = 0;
            while (true)
            {
                Trials++;
                if (Trials > 50)
                    break;
                double Ys = PushOverResultsComaprsion.SimplifiedModelResult.GetYieldStrength();
                double Ratio = Yd / Ys;
                Ratio = Ratio > 1.00 ? Math.Min(Ratio, 1.1) : Math.Max(Ratio, 0.9);
                double Value0 = Ratio * SimplifiedModel.Omega_y;
                if (Math.Abs(1 - Value0 / CurrentValues[0]) < 1e-5)
                    break;
                CurrentValues[0] = Value0;
                await RunPushOverCurve(CurrentValues);
                double error = Math.Abs(1 - Yd / Ys);
                if (error < minMSE)
                {
                    minMSE = error;
                    bestValues[0] = CurrentValues[0];
                }
                if (error < 1e-5)
                    break;
            }
            CurrentValues[0] = bestValues[0];

            double Peakdetailed = PeakDetailed * Udiff;
            double Dx = PushOverResultsComaprsion.DetailedModelResult.GetLocationWithPeakRatio(Udiff);
            double critical = PushOverResultsComaprsion.GetCriticalMSE();

            Trials = 0;
            while (PushOverResultsComaprsion.MSE > critical)
            {
                Trials++;
                if (Trials > 100)
                    break;
                double PeakSimplified = PushOverResultsComaprsion.SimplifiedModelResult.PeakStrength;
                double Sx = PushOverResultsComaprsion.SimplifiedModelResult.MaxV.X;

                bool PeakAdjusted = Math.Abs(1 - Peakdetailed / PeakSimplified) < 1e-5;

                double Ratio = Peakdetailed / PeakSimplified;
                Ratio = Ratio > 1.00 ? Math.Min(Ratio, 1.1) : Math.Max(Ratio, 0.9);
                double Value1 = Ratio * SimplifiedModel.Omega_P;
                double Value2 = CurrentValues[2];
                if (Math.Abs(1 - Dx / Sx) < 1e-5)
                {
                    double A1 = 0;
                    double A2 = 0;
                    PushOverResultsComaprsion.GetDifferentArea(out A1, out A2);
                    Ratio = A1 / A2;
                }
                else
                {
                    Ratio = Dx / Sx;
                }

                if (PeakAdjusted && Math.Abs(1 - Ratio) < 1e-5)
                    break;
                Ratio = Ratio > 1.00 ? Math.Min(Ratio, 1.1) : Math.Max(Ratio, 0.9);
                Value2 = Ratio * SimplifiedModel.Meu;

                if (Math.Abs(1 - Value1 / CurrentValues[1]) < 1e-5 && Math.Abs(1 - Value2 / CurrentValues[2]) < 1e-5)
                    break;

                CurrentValues[1] = Value1;
                CurrentValues[2] = Value2;
                await RunPushOverCurve(CurrentValues);

                if (PushOverResultsComaprsion.MSE < minMSE)
                {
                    minMSE = PushOverResultsComaprsion.MSE;
                    bestValues = CurrentValues;
                }
            }
            return bestValues;
        }
        private async Task RunPushOverCurve(List<double> Values)
        {
            SimplifiedModel.Omega_y = Values[0];
            SimplifiedModel.Omega_P = Values[1];
            SimplifiedModel.Meu = Values[2];

            await SimplifiedModel.ClaibrateNonLinear();
            await RunPushOverCurve();
        }
        private async Task RunPushOverCurve()
        {
            await SimplifiedModel.RunPushOverResults(DetailedModel.PushOverResults.K);
            PushOverResultsComaprsion.Update(SimplifiedModel.PushOverResults);
        }
        
        private async Task RunSimplifiedModelModalAnalysis()
        {
            await SimplifiedModel.RunModalAnalysis();
            ModalAnalysisResult = new ModalAnalysisResult(DetailedModel.ModeShapes, SimplifiedModel.ModeShapes);
        }
        private async Task<double> TryAlpha2(List<Point2D> alpha_Sigma1)
        {
            Sigma1 = GetSigma1(alpha_Sigma1);
            double EI = (DetailedModel.ModeShapes[0].Lambda * DetailedModel.GetRo() * Math.Pow(DetailedModel.GetH(), 4)) / (Math.Pow(Sigma1, 2) * (Math.Pow(Sigma1, 2) + Math.Pow(Alpha, 2)));
            double GA = Math.Pow(Alpha / DetailedModel.GetH(), 2) * EI;
            SimplifiedModel.SetElasticElements(EI, GA);
            await RunSimplifiedModelModalAnalysis();
            await RunPushOverCurve();
            return ModalAnalysisResult.GetError(PushOverResultsComaprsion.DetailedModelResult.GetSlope() , PushOverResultsComaprsion.SimplifiedModelResult.GetSlope());
        }
        private double GetAlpha(double T1T2Ratio, List<Point2D> alpha_TRatio)
        {
            double MaxRatio = alpha_TRatio.Select(p => p.Y).Max();
            double MinRatio = alpha_TRatio.Select(p => p.Y).Min();

            double Ratio = Math.Max(MinRatio, Math.Min(T1T2Ratio, MaxRatio));
            for (int i = 1; i < alpha_TRatio.Count; i++)
            {
                if (IsInRange(alpha_TRatio[i - 1].Y, alpha_TRatio[i].Y, Ratio))
                {
                    return Interopelate(alpha_TRatio[i - 1].Y, alpha_TRatio[i].Y, Ratio, alpha_TRatio[i - 1].X, alpha_TRatio[i].X);
                }
            }
            return 0;
        }
        public double Interopelate(double a, double b, double value, double Va, double Vb)
        {
            double Da = Math.Abs(value - a);
            double Db = Math.Abs(value - b);
            return (Va * Db + Vb * Da) / (Db + Da);
        }
        public bool IsInRange(double a, double b, double value)
        {
            double Tolerance = 1E-10;
            double min = Math.Min(a, b);
            double max = Math.Max(a, b);

            return Math.Abs(value - min) < Tolerance || Math.Abs(value - max) < Tolerance ||
                (value > min && value < max);
        }
        private double GetSigma1(List<Point2D> alpha_Sigma1)
        {
            double MaxAlpha = alpha_Sigma1.Select(p => p.X).Max();
            double MinAlpha = alpha_Sigma1.Select(p => p.X).Min();

            double Ratio = Math.Max(MinAlpha, Math.Min(Alpha, MaxAlpha));
            for (int i = 1; i < alpha_Sigma1.Count; i++)
            {
                if (IsInRange(alpha_Sigma1[i - 1].X, alpha_Sigma1[i].X, Ratio))
                {
                    return Interopelate(alpha_Sigma1[i - 1].X, alpha_Sigma1[i].X, Ratio, alpha_Sigma1[i - 1].Y, alpha_Sigma1[i].Y);
                }
            }
            return 0;
        }


    }

}

