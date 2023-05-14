using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;
using Excel = Microsoft.Office.Interop.Excel;
using System.Reflection;
using Microsoft.Office.Interop.Excel;

namespace TempAnalysis.Utilities
{
    [DataContract]
    public class ExcelWriterUtiliy
    {
        [DataMember]
        public string MainFolderPath;

        public void InitExcelFile(out Excel.Application oXL, out Excel._Workbook oWB, params string[] SheetsNames)
        {
            oXL = new Excel.Application();
            oXL.Visible = false;
            oXL.UserControl = false;
            oWB = oXL.Workbooks.Add("");
            if (SheetsNames == null || SheetsNames.Length == 0)
                return;
            Excel._Worksheet oSheet = (Excel._Worksheet)oWB.ActiveSheet;
            oSheet.Name = SheetsNames[0];
            for (int i = 1; i < SheetsNames.Length; i++)
            {
                Excel._Worksheet Sheet = (Excel._Worksheet)(oWB.Sheets.Add(After: oWB.Sheets[oWB.Sheets.Count]));
                Sheet.Name = SheetsNames[i];
            }
        }
        public void SaveExcel(string folderName, string FileName, Excel.Application oXL, Excel._Workbook oWB)
        {
            string path = Path.Combine(folderName, FileName + ".xlsx");
            if (File.Exists(path))
                File.Delete(path);
            oXL.Visible = false;
            oXL.UserControl = false;
            oWB.SaveAs(path, Excel.XlFileFormat.xlWorkbookDefault, Type.Missing, Type.Missing,
                            false, false, Excel.XlSaveAsAccessMode.xlNoChange,
                            Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
            oWB.Close(true);
            oXL.Quit();
        }
        internal void WriteResults(List<CalibrationUnit> calibrationUnits)
        {
            WritePushoverCurves(calibrationUnits);
            WriteInterStoryDrifts(calibrationUnits);
            WriteComaprsionResults(calibrationUnits);
        }
        private void WriteComaprsionResults(List<CalibrationUnit> calibrationUnits)
        {
            Excel.Application oXL;
            Excel._Workbook oWB;
            InitExcelFile(out oXL, out oWB, "PushOver Results", "InterStorey Drifts", "Modal Results");
            WritePushOverComparsionResults(calibrationUnits, oWB);
            WriteInterStoreyDrifts(calibrationUnits, oWB);
            WriteModalStoreyDrifts(calibrationUnits, oWB);
            SaveExcel(MainFolderPath, "Comparsion", oXL, oWB);
        }
        private void WriteModalStoreyDrifts(List<CalibrationUnit> calibrationUnits, _Workbook oWB)
        {
            Excel._Worksheet oSheet = oWB.Sheets[3];
            string[,] Svalues = new string[2, 9];
            double[,] dvalues = new double[calibrationUnits.Count, 9];

            Svalues[0, 0] = "Detailed Model";
            Svalues[0, 2] = "Simplified Model";
            Svalues[0, 4] = "Erorrs";
            Svalues[0, 6] = "Chart";

            for (int i = 0; i < 3; i++)
            {
                Svalues[1, 2 * i + 0] = "T1";
                Svalues[1, 2 * i + 1] = "T2";
            }
            Svalues[1, 6] = "T1/T2";
            Svalues[1, 7] = "Alpha";
            Svalues[1, 8] = "Sigma";

            for (int i = 0; i < calibrationUnits.Count; i++)
            {
                ModalAnalysisResult modal = calibrationUnits[i].ModalAnalysisResult;
                List<List<double>> results = new List<List<double>> 
                {
                    modal.detailedModelPeriods,
                    modal.simplifiedModelPeriods,
                    modal.Error
                }; 
                int index = 0;
                foreach (List<double> result in results)
                {
                    dvalues[i, index] = result[0];
                    dvalues[i, index + 1] = result[1];
                    index += 2;
                }
                dvalues[i, index] = results[0][0]/ results[0][1];
                dvalues[i, index + 1] = calibrationUnits[i].Alpha;
                dvalues[i, index+2] = calibrationUnits[i].Sigma1;
            }
            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 2]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 3], oSheet.Cells[1, 4]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 5], oSheet.Cells[1, 6]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 7], oSheet.Cells[1, 9]].Merge(true);

            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 9]].Value2 = Svalues;
            oSheet.Range[oSheet.Cells[3, 1], oSheet.Cells[calibrationUnits.Count + 2, 9]].Value2 = dvalues;
            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 9]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();

        }
        private void WriteInterStoreyDrifts(List<CalibrationUnit> calibrationUnits, _Workbook oWB)
        {
            Excel._Worksheet oSheet = oWB.Sheets[2];
            string[,] Svalues = new string[2, 11];
            double[,] dvalues = new double[calibrationUnits.Count,11];

            Svalues[0,0] = "Detailed Model";
            Svalues[0,2] = "Simplified Model";
            Svalues[0,4] = "Erorrs";

            for (int i = 0; i < 3; i++)
            {
                Svalues[1,2*i+0] = "1st Floor";
                Svalues[1,2*i+1] = "2nd Floor";
            }

            Svalues[1, 6] = "Oy";
            Svalues[1, 7] = "Op";
            Svalues[1, 8] = "Meu";
            Svalues[1, 9] = "EI (KN.m2)";
            Svalues[1, 10] = "GA (KN.m2)";


            for (int i = 0; i < calibrationUnits.Count; i++)
            {
                CalibrationUnit modal = calibrationUnits[i];
                List<List<double>> results = new List<List<double>>
                {
                    modal.DetailedModel.PushOverResults.InterSotryDrifts,
                    modal.SimplifiedModel.PushOverResults.InterSotryDrifts,
                    modal.PushOverResultsComaprsion.Diffs
                };
                int index = 0;
                foreach (List<double> result in results)
                {
                    dvalues[i, index] = result[0];
                    dvalues[i, index + 1] = result[1];
                    index += 2;
                }

                dvalues[i,index+0] = modal.SimplifiedModel.Omega_y;
                dvalues[i,index+1] = modal.SimplifiedModel.Omega_P;
                dvalues[i,index+2] = modal.SimplifiedModel.Meu;
                dvalues[i,index+3] = modal.SimplifiedModel.EI / 1E3;
                dvalues[i,index+4] = modal.SimplifiedModel.GA / 1E3;
            }

            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 2]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 3], oSheet.Cells[1, 4]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 5], oSheet.Cells[1, 6]].Merge(true);

            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 11]].Value2 = Svalues;
            oSheet.Range[oSheet.Cells[3, 1], oSheet.Cells[calibrationUnits.Count + 2,11]].Value2 = dvalues;
            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 11]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();
        }
        private void WritePushOverComparsionResults(List<CalibrationUnit> calibrationUnits, _Workbook oWB)
        {
            Excel._Worksheet oSheet = oWB.Sheets[1];
            string[,] Svalues = new string[2, 18];
            double[,] dvalues = new double[calibrationUnits.Count, 18];

            Svalues[0,0] = "Detailed Model";
            Svalues[0,6] = "Simplified Model";
            Svalues[0,12] = "Erorrs";

            for (int i = 0; i < 2; i++)
            {
                Svalues[1,6*i+0] = "Peak Strength";
                Svalues[1,6*i+1] = "Yield Displacement";
                Svalues[1,6*i+2] = "Ultimate Displacement";
                Svalues[1,6*i+3] = "Ductility";
                Svalues[1,6*i+4] = "Curve Area";
                Svalues[1,6*i+5] = "Time";
            }
            Svalues[1, 12] = "Peak Strength";
            Svalues[1, 13] = "Ductility";
            Svalues[1, 14] = "Curve Area";
            Svalues[1, 15] = "Time";
            Svalues[1, 16] = "MSE";
            Svalues[1, 17] = "MSE/N";

            for (int i = 0; i < calibrationUnits.Count; i++)
            {
                CalibrationUnit Cal = calibrationUnits[i];
                List<PushOverResults> pushOverResults = new List<PushOverResults>
                {
                    Cal.DetailedModel.PushOverResults,
                    Cal.SimplifiedModel.PushOverResults,
                };
                int index = 0;
                foreach (PushOverResults pushOverResult in pushOverResults)
                {
                    dvalues[i, index+0] = pushOverResult.PeakStrength;
                    dvalues[i, index+1] = pushOverResult.YieldDisplacement;
                    dvalues[i, index+2] = pushOverResult.UltimateDisplacement;
                    dvalues[i, index+3] = pushOverResult.Ductility;
                    dvalues[i, index+4] = pushOverResult.CurveArea;
                    dvalues[i, index+5] = pushOverResult.Time;
                    index += 6;
                }
                dvalues[i,index+0] = Cal.PushOverResultsComaprsion.PeakStrengthError;
                dvalues[i,index+1] = Cal.PushOverResultsComaprsion.DuctilityError;
                dvalues[i,index+2] = Cal.PushOverResultsComaprsion.CurveAreaError;
                dvalues[i,index+3] = Cal.PushOverResultsComaprsion.TimeDevRatio;
                dvalues[i,index+4] = Cal.PushOverResultsComaprsion.GetAbsMSE();
                dvalues[i,index+5] = Cal.PushOverResultsComaprsion.MSE;
            }

            oSheet.Range[oSheet.Cells[1,1], oSheet.Cells[1,6]].Merge(true);
            oSheet.Range[oSheet.Cells[1,7], oSheet.Cells[1,12]].Merge(true);
            oSheet.Range[oSheet.Cells[1,13], oSheet.Cells[1,18]].Merge(true);

            oSheet.Range[oSheet.Cells[1,1], oSheet.Cells[2,18]].Value2 = Svalues;
            oSheet.Range[oSheet.Cells[3,1], oSheet.Cells[calibrationUnits.Count+2,18]].Value2 = dvalues;
            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 18]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();
        }
        private void WriteInterStoryDrifts(List<CalibrationUnit> calibrationUnits)
        {
            Excel.Application oXL;
            Excel._Workbook oWB;
            InitExcelFile(out oXL, out oWB, calibrationUnits.Select(x => x.DetailedModel.Name).ToArray());
            WriteInterStoryDrifts(calibrationUnits, oWB);
            SaveExcel(MainFolderPath, "InterStoryDrifts", oXL, oWB);
        }
        private void WriteInterStoryDrifts(List<CalibrationUnit> calibrationUnits, _Workbook oWB)
        {
            for (int i = 0; i < calibrationUnits.Count; i++)
            {
                PushOverResultsComaprsion Cal = calibrationUnits[i].PushOverResultsComaprsion;
                Excel._Worksheet oSheet = oWB.Sheets[i + 1];

                string[,] Svalues = new string[1, 3];
                Svalues[0, 0] = "Storey";
                Svalues[0, 1] = "Detailed Model";
                Svalues[0, 2] = "Simplified Model";

                int NumberofFloors = Cal.DetailedModelResult.InterSotryDrifts.Count;
                double[,] dvalues = new double[NumberofFloors+1, 3];
                for (int j = 0; j < NumberofFloors; j++)
                {
                    int Index = NumberofFloors - j;
                    dvalues[j, 0] = Index;
                    dvalues[j, 1] = Cal.DetailedModelResult.InterSotryDrifts[Index-1];
                    dvalues[j, 2] = Cal.SimplifiedModelResult.InterSotryDrifts[Index-1];
                }
                dvalues[NumberofFloors, 0] = 0;
                dvalues[NumberofFloors, 1] = 0;
                dvalues[NumberofFloors, 2] = 0;

                oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 3]].Value2 = Svalues;
                oSheet.Range[oSheet.Cells[2, 1], oSheet.Cells[NumberofFloors+2, 3]].Value2 = dvalues;

                Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 3]];
                oRng.Font.Bold = true;
                oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                oRng.EntireColumn.AutoFit();

                // Add chart.
                var charts = oSheet.ChartObjects() as
                    Microsoft.Office.Interop.Excel.ChartObjects;
                var chartObject = charts.Add(200, 10, 900, 900) as
                    Microsoft.Office.Interop.Excel.ChartObject;
                var chart = chartObject.Chart;

                // Set chart properties.
                chart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlXYScatterSmoothNoMarkers;
                SeriesCollection seriesCollection = (SeriesCollection)chart.SeriesCollection();

                Series s1 = seriesCollection.NewSeries();
                s1.Name = "Detailed Model";
                s1.XValues = oSheet.Range[oSheet.Cells[2,2], oSheet.Cells[NumberofFloors+2,2]];
                s1.Values = oSheet.Range[oSheet.Cells[2,1], oSheet.Cells[NumberofFloors+2,1]];
                Series s2 = seriesCollection.NewSeries();
                s2.Name = "Simplified Model";
                s2.XValues = oSheet.Range[oSheet.Cells[2,3], oSheet.Cells[NumberofFloors+2, 3]];
                s2.Values = oSheet.Range[oSheet.Cells[2,1], oSheet.Cells[NumberofFloors+2, 1]];
            }
        }

        private void WritePushoverCurves(List<CalibrationUnit> calibrationUnits)
        {
            Excel.Application oXL;
            Excel._Workbook oWB;
            InitExcelFile(out oXL, out oWB, calibrationUnits.Select(x => x.DetailedModel.Name).ToArray());
            WritePushoverCurves(calibrationUnits,oWB);
            SaveExcel(MainFolderPath, "PushOverResults", oXL, oWB);
        }
        private void WritePushoverCurves(List<CalibrationUnit> calibrationUnits, _Workbook oWB)
        {
            for (int i = 0; i < calibrationUnits.Count; i++)
            {
                PushOverResultsComaprsion Cal = calibrationUnits[i].PushOverResultsComaprsion;
                Excel._Worksheet oSheet = oWB.Sheets[i+1];
                int PointsCount = Cal.GetN();
                string[,] Svalues = new string[2, 4];
                double[,] dvalues = new double[PointsCount, 4];
                //double[,] dvalue2 = new double[2, 2];
                Svalues[0, 0] = "Detailed Model";
                Svalues[0, 2] = "Simplified Model";
                
                Svalues[1, 0] = "Disp (m)";
                Svalues[1, 1] = "Base Shear (KN)";
                Svalues[1, 2] = "Disp (m)";
                Svalues[1, 3] = "Base Shear (KN)";

                dvalues[0, 0] = dvalues[0, 1] = dvalues[0, 2] = dvalues[0, 3] = 0;
                for (int j = 0; j < PointsCount; j++)
                {
                    if (j < Cal.DetailedModelResult.CurveList.Count)
                    {
                        Point2D p = Cal.DetailedModelResult.CurveList[j];
                        dvalues[j,0] = Math.Round(p.X,3);
                        dvalues[j,1] = Math.Round(p.Y / 1000, 3);
                    }
                    else
                    {
                        dvalues[j,0] = 0;
                        dvalues[j,1] = 0;
                    }

                    if (j < Cal.SimplifiedModelResult.CurveList.Count)
                    {
                        Point2D p = Cal.SimplifiedModelResult.CurveList[j];
                        dvalues[j,2] = Math.Round(p.X, 3);
                        dvalues[j,3] = Math.Round(p.Y / 1000, 3);
                    }
                    else
                    {
                        dvalues[j,2] = 0;
                        dvalues[j,3] = 0;
                    }
                }
                //double DesignForce = Math.Round(calibrationUnits[i].DetailedModel.GetDesignBaseShear() / 1000, 3);
                //dvalue2[0, 0] = 0;
                //dvalue2[1, 0] = Cal.DetailedModelResult.CurveList.Last().X;
                //dvalue2[0, 1] = DesignForce;
                //dvalue2[1, 1] = DesignForce;

                oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[ 2, 4]].Value2 = Svalues;
                oSheet.Range[oSheet.Cells[3, 1], oSheet.Cells[PointsCount+2, 4]].Value2 = dvalues;
                //oSheet.Range[oSheet.Cells[3, 5], oSheet.Cells[4, 6]].Value2 = dvalue2;
                
                oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 2]].Merge(true);
                oSheet.Range[oSheet.Cells[1, 3], oSheet.Cells[1, 4]].Merge(true);

                Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 4]];
                oRng.Font.Bold = true;
                oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                oRng.EntireColumn.AutoFit();

                // Add chart.
                var charts = oSheet.ChartObjects() as
                    Microsoft.Office.Interop.Excel.ChartObjects;
                var chartObject = charts.Add(200, 10, 900, 900) as
                    Microsoft.Office.Interop.Excel.ChartObject;
                var chart = chartObject.Chart;

                // Set chart properties.
                chart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlXYScatterSmoothNoMarkers;
                SeriesCollection seriesCollection = (SeriesCollection)chart.SeriesCollection();

                int Count = Cal.DetailedModelResult.CurveList.Count;
                Series s1 = seriesCollection.NewSeries();
                s1.Name = "Detailed Model";
                s1.XValues = oSheet.Range[oSheet.Cells[3, 1], oSheet.Cells[Count + 2, 1]];
                s1.Values = oSheet.Range[oSheet.Cells[3, 2], oSheet.Cells[Count + 2, 2]];

                Count = Cal.SimplifiedModelResult.CurveList.Count;
                Series s2 = seriesCollection.NewSeries();
                s2.Name = "Simplified Model";
                s2.XValues = oSheet.Range[oSheet.Cells[3, 3], oSheet.Cells[Count + 2, 3]];
                s2.Values = oSheet.Range[oSheet.Cells[3, 4], oSheet.Cells[Count + 2, 4]];

                //Series s3 = seriesCollection.NewSeries();
                //s3.Name = "Design Base Shear";
                //s3.XValues = oSheet.Range[oSheet.Cells[3, 5], oSheet.Cells[4, 5]];
                //s3.Values = oSheet.Range[oSheet.Cells[3, 6], oSheet.Cells[4, 6]];
            }
        }
        private void WritePushoverCurves(List<DetailedModel> models, _Workbook oWB, bool TestSet)
        {
            for (int i = 0; i < models.Count; i++)
            {
                Excel._Worksheet oSheet = oWB.Sheets[i + 1];

                DetailedModel model = models[i];
                int PointsCount = model.AllPushOverResults.Max(x=>x.CurveList.Count);
                int Columns = 2 * model.AllPushOverResults.Count;
                string[,] Svalues = new string[2, Columns];
                double[,] dvalues = new double[PointsCount, Columns];
                //double[,] dvalue2 = new double[2, 2];
                for (int l = 0; l < model.AllPushOverResults.Count; l++)
                {
                    PushOverResults results = model.AllPushOverResults[l];
                    int C = 2 * l;
                    Svalues[0, C] = $"K = {results.K}";

                    double Norm1 = TestSet ? model.GetH()/100 :1.0;
                    double Norm2 = TestSet ? model.GetBuildingWeight() : 1000;
                    int percision = TestSet ? 7 : 3;
                    Svalues[1, C] = TestSet ? "Roof Drift (%)":"Disp (m)";
                    Svalues[1, C + 1] = TestSet ? "Base Shear /Building Weight": "Base Shear (KN)";
                    dvalues[0, C] = dvalues[0, C + 1] = 0;

                    for (int j = 0; j < PointsCount; j++)
                    {
                        if (j < results.CurveList.Count)
                        {
                            Point2D p = results.CurveList[j];
                            dvalues[j, C] = Math.Round(p.X / Norm1, percision);
                            dvalues[j, C + 1] = Math.Round(p.Y / Norm2, percision);
                        }
                        else
                        {
                            dvalues[j, C] = 0;
                            dvalues[j, C+1] = 0;
                        }
                    }
                }

                oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, Columns]].Value2 = Svalues;
                oSheet.Range[oSheet.Cells[3, 1], oSheet.Cells[PointsCount + 2, Columns]].Value2 = dvalues;

                for (int l = 0; l < Columns; l+=2)
                {
                    oSheet.Range[oSheet.Cells[1, l+1], oSheet.Cells[1, l+2]].Merge(true);
                }

                Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, Columns]];
                oRng.Font.Bold = true;
                oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                oRng.EntireColumn.AutoFit();

                // Add chart.
                var charts = oSheet.ChartObjects() as
                    Microsoft.Office.Interop.Excel.ChartObjects;
                var chartObject = charts.Add(200, 10, 900, 900) as
                    Microsoft.Office.Interop.Excel.ChartObject;
                var chart = chartObject.Chart;

                // Set chart properties.
                chart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlXYScatterSmoothNoMarkers;
                SeriesCollection seriesCollection = (SeriesCollection)chart.SeriesCollection();

                for (int l = 0; l < model.AllPushOverResults.Count; l++)
                {
                    PushOverResults results = model.AllPushOverResults[l];
                    int C = 2 * l;
                    int Count = results.CurveList.Count;

                    Series s1 = seriesCollection.NewSeries();
                    s1.Name = $"K = {results.K}";
                    s1.XValues = oSheet.Range[oSheet.Cells[3, C+1], oSheet.Cells[Count + 2, C+1]];
                    s1.Values = oSheet.Range[oSheet.Cells[3, C+2], oSheet.Cells[Count + 2, C+2]];
                }
            }
        }
        internal void WritePushOverCurves(List<DetailedModel> models, bool TestSet)
        {
            Excel.Application oXL;
            Excel._Workbook oWB;
            IList<string> list = models.Select(x => x.Name).ToList();
            list.Add("Summary");
            InitExcelFile(out oXL, out oWB, list.ToArray());
            WritePushoverCurves(models, oWB, TestSet);
            WritePushoverSummary(models,oWB, TestSet);
            SaveExcel(MainFolderPath, "PushOverCurves", oXL, oWB);
        }
        private void WritePushoverSummary(List<DetailedModel> models, _Workbook oWB, bool TestSet)
        {
            Excel._Worksheet oSheet = oWB.Sheets[models.Count + 1];
            
            int profiles = models[0].AllPushOverResults.Count;
            string[,] Svalues = new string[models.Count, 1]; 
            double[,] dvalues = new double[models.Count, profiles];

            for (int i = 0; i < models.Count; i++)
            {
                Svalues[i, 0] = models[i].Name;
                double yNorm = TestSet? models[i].GetBuildingWeight(): 1000;
                int percision = TestSet? 7: 3;

                for (int j = 0; j < models[i].AllPushOverResults.Count; j++)
                {
                    dvalues[i, j] = Math.Round( models[i].AllPushOverResults[j].PeakStrength / yNorm, percision);
                }
            }

            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[models.Count,1]].Value2 = Svalues;
            oSheet.Range[oSheet.Cells[1, 2], oSheet.Cells[models.Count, profiles + 1]].Value2 = dvalues;
        }
        internal void WriteResults(List<DetailedModel> models)
        {
            Excel.Application oXL;
            Excel._Workbook oWB;
            InitExcelFile(out oXL, out oWB, "Columns", "Modal Analysis", "Shear Walls", "Coupling Beams", "Cost Estimation");
            WriteGravityResults(models, oWB);
            WriteModeShapesResults(models, oWB);
            WriteShearWallsResults(models, oWB);
            WriteBeamsResults(models, oWB);
            WriterCostEstimations(models, oWB);
            SaveExcel(MainFolderPath, "DesignResults", oXL, oWB);
        }
        internal void WriterCostEstimations(List<DetailedModel> models, Excel._Workbook oWB)
        {
            string[,] Svalues = new string[models.Count + 2, 13];
            AddCostEstimationHeader1(ref Svalues);
            AddCostEstimationHeader2(ref Svalues);
            for (int i = 0; i < models.Count; i++)
            {
                GenerateModelCost(models[i], i + 2, ref Svalues);
            }
            Excel._Worksheet oSheet = oWB.Sheets[5];
            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[models.Count + 2, 13]].Value2 = Svalues;

            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 1]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 2], oSheet.Cells[1, 4]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 5], oSheet.Cells[1, 7]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 8], oSheet.Cells[1, 11]].Merge(true);
            oSheet.Range[oSheet.Cells[1, 12], oSheet.Cells[2, 12]].Merge(true);

            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[2, 13]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();
        }
        private void GenerateModelCost(DetailedModel model, int R, ref string[,] Svalues)
        {
            ModelEstimationEntity modelCost = model.CostEstimation;
            List<string> Titles = new List<string>();
            Titles.Add(model.Name);
            Titles.Add(modelCost.Columns_ConcreteVolume.ToString("F2"));
            Titles.Add(modelCost.Slabs_ConcreteVolume.ToString("F2"));
            Titles.Add(modelCost.Walls_ConcreteVolume.ToString("F2"));
            Titles.Add(modelCost.Columns_SteelWeight.ToString("F2"));
            Titles.Add(modelCost.Slabs_SteelWeight.ToString("F2"));
            Titles.Add(modelCost.Walls_SteelWeight.ToString("F2"));
            Titles.Add((modelCost.ConcreteCost / 1E+6).ToString("F2"));
            Titles.Add((modelCost.ReinforcementCost / 1E+6).ToString("F2"));
            Titles.Add((modelCost.TotalCost / 1E+6).ToString("F2"));
            Titles.Add((modelCost.CostPerFloor / 1E+6).ToString("F2"));
            Titles.Add(modelCost.CostPer_M2.ToString("F2"));
            Titles.Add(model.FloorDrifts.Any()? model.FloorDrifts.Max().ToString("E"): "0");
            for (int i = 0; i < Titles.Count; i++)
            {
                Svalues[R, i] = Titles[i];
            }
        }
        internal void AddCostEstimationHeader1(ref string[,] Svalues)
        {
            for (int i = 0; i < 13; i++)
            {
                Svalues[0, i] = "";
            }

            Svalues[0, 1] = "Concrete Volume (m3)";
            Svalues[0, 4] = "Steel Quantity (ton)";
            Svalues[0, 7] = "Structural Material Cost (million AED)";
            Svalues[0, 11] = "Avg. Cost per m2 (AED)";
            Svalues[0, 12] = "Drift";
        }
        internal void AddCostEstimationHeader2(ref string[,] Svalues)
        {
            List<string> Titles = new List<string>
            { "Ref. Building" , "Columns", "Slabs", "Walls",
              "Columns", "Slabs", "Walls", "Concrete", "Steel", "Total",
               "Avg. Per Floor" ,  "","Drift"};
            for (int i = 0; i < Titles.Count; i++)
            {
                Svalues[1, i] = Titles[i];
            }
        }
        private void WriteBeamsResults(List<DetailedModel> models, _Workbook oWB)
        {
            Excel._Worksheet oSheet = oWB.Sheets[4];
            int count = 1;
            models.ForEach(x=> count += x.FloorsGroups.Count);
            string[,] Svalues = new string[count, 8];
            AddCouplingBeamHeaders(ref Svalues);
            int i = 1;
            int j = 1;
            foreach (var model in models)
            {
                foreach (var floorsGroup in model.FloorsGroups)
                {
                    var Sec = floorsGroup.CouplingsBeams.CouplingBeamSection;
                    Svalues[i, 0] = "";
                    Svalues[i, 1] = "";
                    Svalues[i, 2] = $"[{floorsGroup.Floors.First().Index}:{floorsGroup.Floors.Last().Index}]";
                    Svalues[i, 3] = $"{Sec.SectionDepth.ToString("F2")} × {Sec.SectionWidth.ToString("F2")}";
                    Svalues[i, 4] = $"{Sec.NumberOfMainBars} Φ {Sec.MainSteelBars.Diameter}";
                    Svalues[i, 5] = $"{Sec.SideBarsRows} Φ {Sec.SideSteelBars.Diameter} ";
                    Svalues[i, 6] = $"{Sec.GetNumberOfLegs()} Legs - Φ {Sec.StirupsSteelBars.Diameter} / {(100 / Sec.StirrupsPerMeter).ToString("F2")} cm";
                    Svalues[i, 7] = (Sec.GetReinfRatio() * 100).ToString("F2");
                    i++;
                }
                Svalues[j, 0] = model.Name;
                Svalues[j, 1] = model.BeamLength.ToString("F2");
                j += model.FloorsGroups.Count;
            }
            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[count, 8]].Value2 = Svalues;
            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 8]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();
        }
        private void AddCouplingBeamHeaders(ref string[,] Svalues)
        {
            for (int i = 0; i < 8; i++)
            {
                Svalues[0, i] = "";
            }
            Svalues[0, 0] = "Ref.Building";
            Svalues[0, 1] = "L (m)";
            Svalues[0, 2] = "Floors";
            Svalues[0, 3] = "Beam Size (m × m)";
            Svalues[0, 4] = "Top / Bottom Reinforcement";
            Svalues[0, 5] = "Transverse Reinforcement";
            Svalues[0, 6] = "Ties in Y Drirection";
            Svalues[0, 7] = "ρ (%)";
        }
        private void WriteShearWallsResults(List<DetailedModel> models, _Workbook oWB)
        {
            Excel._Worksheet oSheet = oWB.Sheets[3];

            int Count = 1;
            models.ForEach(x=> Count += 2* x.FloorsGroups.Count);
            string[,] Svalues = new string[Count, 12];
            AddShearWallHeader(ref Svalues);
            int i = 1;
            int j = 1;
            foreach (var detailedModel in models)
            {
                foreach (var floorsGroup in detailedModel.FloorsGroups)
                {
                    WriteShearWallsResults("Edge", floorsGroup.OuterShearWalls.ShearWallReinforcement, ref Svalues, i);
                    WriteShearWallsResults("Interior", floorsGroup.InnerShearWalls.ShearWallReinforcement, ref Svalues, i + 1);
                    Svalues[i, 1] = $"[{floorsGroup.Floors.First().Index}:{floorsGroup.Floors.Last().Index}]";
                    i += 2;
                }
                Svalues[j, 0] = detailedModel.Name;
                j += 2 * detailedModel.FloorsGroups.Count;
            }
            
            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[Count, 12]].Value2 = Svalues;
            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 12]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();
        }
        private void WriteShearWallsResults(string Type, SpecialShearWallReinforcement section, ref string[,] Svalues, int i)
        {
            Svalues[i,0] = "";
            Svalues[i,1] = "";
            Svalues[i,2] = Type;
            Svalues[i,3] = section.GetLength().ToString("F2");
            Svalues[i,4] = section.GetThickness().ToString("F2");
            if (section.SpecialBoundary != null)
            {
                var BE = section.SpecialBoundary;
                var s = $" Φ {BE.StirrupsBars.SteelBars.Diameter} @ {(100 / BE.StirrupsBars.NumberOfBarsPerMeter).ToString("F2")} cm";
                Svalues[i, 5] = BE.LBE.ToString("F2");
                Svalues[i, 6] = $"{BE.GetTotalVerticalBars()} Φ {BE.VerticalBars.Diameter}";
                Svalues[i, 7] = $"{BE.No_VerticalTies +2}" + s;
                Svalues[i, 8] = $"{BE.No_AlongTies +2}" + s;
            }
            else 
            {
                Svalues[i, 5] = "-";
                Svalues[i, 6] = "-";
                Svalues[i, 7] = "-";
                Svalues[i, 8] = "-";
            }
            Svalues[i,9] = $"Φ {section.RHW.SteelBars.Diameter} @ {(100 / section.RHW.NumberOfBarsPerMeter).ToString("F2")} cm";
            Svalues[i,10] = $"Φ {section.R_VW.SteelBars.Diameter} @ {(100 / section.R_VW.NumberOfBarsPerMeter).ToString("F2")} cm";
            Svalues[i,11] = (section.GetLongitudinalReinfRatio() * 100.0).ToString("F2");
        }
        private void AddShearWallHeader(ref string[,] Svalues)
        {
            for (int i = 0; i < 12; i++)
            {
                Svalues[0, i] = "";
            }
            Svalues[0,0] = "Ref.Building";
            Svalues[0,1] = "Floors";
            Svalues[0,2] = "Wall Type";
            Svalues[0,3] = "L (m)";
            Svalues[0,4] = "T (m)";
            Svalues[0,5] = "LBE (m)";
            Svalues[0,6] = "RBE";
            Svalues[0,7] = "CBE,W";
            Svalues[0,8] = "CBE,L";
            Svalues[0,9] = "RHW";
            Svalues[0,10] = "RVW";
            Svalues[0,11] = "ρ (%)";
        }
        private void WriteModeShapesResults(List<DetailedModel> models, _Workbook oWB)
        {
            Excel._Worksheet oSheet = oWB.Sheets[2];
            int count = models.Sum(m=>m.ModeShapes.Count) + 1;
            string[,] Svalues = new string[count, 8];
            AddModeShapesHeader(ref Svalues);
            int i = 1;
            foreach (var model in models)
            {
                for (int j = 0; j < model.ModeShapes.Count; j++)
                {
                     var modeShape = model.ModeShapes[j];
                     Svalues[i + j, 0] = "";
                     Svalues[i + j, 1] = $"{j + 1}";
                     Svalues[i + j, 2] = modeShape.Lambda.ToString("F4");
                     Svalues[i + j, 3] = modeShape.Omega.ToString("F4");
                     Svalues[i + j, 4] = modeShape.Frequency.ToString("F4");
                     Svalues[i + j, 5] = modeShape.Period.ToString("F4");
                     Svalues[i + j, 6] = modeShape.SumMx.ToString("F2");
                     Svalues[i + j, 7] = modeShape.SumMy.ToString("F2");
                }
                Svalues[i, 0] = model.Name;
                i += model.ModeShapes.Count;
            }
            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[count, 8]].Value2 = Svalues;
            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 8]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();
        }
        private void AddModeShapesHeader(ref string[,] Svalues)
        {
            for (int i = 0; i < 8; i++)
            {
                Svalues[0, i] = "";
            }

            Svalues[0, 0] = "Ref.Building";
            Svalues[0, 1] = "Mode";
            Svalues[0, 2] = "ψ (rad/s)^2";
            Svalues[0, 3] = "Ω (rad/s)";
            Svalues[0, 4] = "Frequency (Hrz)";
            Svalues[0, 5] = "Period (s)";
            Svalues[0, 6] = "Cumulative Mx (%)";
            Svalues[0, 7] = "Cumulative My (%)";
        }
        private void WriteGravityResults(List<DetailedModel> models, Excel._Workbook oWB)
        {
            Excel._Worksheet oSheet = oWB.Sheets[1];
            int count = 1;
            models.ForEach(m=> count += 4* m.FloorsGroups.Count);
            string[,] Svalues = new string[count, 7];
            AddGravityHeaders(ref Svalues);

            int i = 1;
            int j = 1;
            foreach (var model in models)
            {
                foreach (var floorsGroup in model.FloorsGroups)
                {
                    WriteColumnsDetails(ref Svalues, i, floorsGroup.CornerColumns, "Corner");
                    WriteColumnsDetails(ref Svalues, i + 1, floorsGroup.OuterColumns, "Edge");
                    WriteColumnsDetails(ref Svalues, i + 2, floorsGroup.InnerColumns, "Interior");
                    WriteColumnsDetails(ref Svalues, i + 3, floorsGroup.CoreColumns, "Opening");
                    Svalues[i, 1] = $"[{floorsGroup.Floors.First().Index}:{floorsGroup.Floors.Last().Index}]";
                    i += 4;
                }
                Svalues[j, 0] = model.Name;
                j += 4 * model.FloorsGroups.Count;
            }
            
            oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[count,7]].Value2 = Svalues;
            Excel.Range oRng = oSheet.Range[oSheet.Cells[1, 1], oSheet.Cells[1, 7]];
            oRng.Font.Bold = true;
            oRng.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            oRng.EntireColumn.AutoFit();
        }
        private void WriteColumnsDetails(ref string[,] Svalues, int i, ColumnsGroup cornerColumns, string Title)
        {
            var section = cornerColumns.ColumnSection;
            string Depth = section.SectionDepth.ToString("F2");
            Svalues[i, 0] = "";
            Svalues[i, 1] = "";
            Svalues[i, 2] = Title;
            Svalues[i, 3] = $"{Depth} × {Depth}";
            Svalues[i, 4] = $"{section.NumberofBars} Φ {section.MainSteelBars.Diameter}";
            Svalues[i, 5] = $" {section.GetNumberOfLegs()} Legs-Φ {section.StirupsSteelBars.Diameter} @ {(100 /section.StirrupsPerMeter).ToString("F2")} cm";
            Svalues[i, 6] = (section.GetReinfRatio() * 100.0).ToString("F2");
        }
        private void AddGravityHeaders(ref string[,] Svalues)
        {
            for (int i = 0; i < 7; i++)
            {
                Svalues[0, i] = "";
            }

            Svalues[0, 0] = "Ref.Building";
            Svalues[0, 1] = "Floors";
            Svalues[0, 2] = "Column Type";
            Svalues[0, 3] = "Column Size (m × m)";
            Svalues[0, 4] = "Reinforcement";
            Svalues[0, 5] = "Ties in X and Y";
            Svalues[0, 6] = "ρ (%)";
        }
    }
}
