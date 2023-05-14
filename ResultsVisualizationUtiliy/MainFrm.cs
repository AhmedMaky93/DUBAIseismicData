using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TempAnalysis.Models;
using ZedGraph;

namespace ResultsVisualizationUtiliy
{
    public partial class MainFrm : Form
    {
        private bool Loaded = false;
        private Action<string> Logger;
        private Action<string> StateLogger;
        private Action<string> ParameterLogger;

        private ZedGraphControl zedGraphControl1;
        public MainFrm()
        {
            InitializeComponent();
            InitZedControlGrap();
            Logger += (string Text) => ConsoleLabel.Text = Text;
            StateLogger += (string Text) => StateLabel.Text = Text;
            ParameterLogger += (string Text) => ParametrsLog.Text = Text;
            backgroundWorker1.DoWork += (object sener, DoWorkEventArgs e) => RunTheScript();
        }

        private void InitZedControlGrap()
        {
            zedGraphControl1 = new ZedGraphControl();
            zedGraphControl1.Location = new System.Drawing.Point(356, 4);
            zedGraphControl1.Margin = new System.Windows.Forms.Padding(5);
            zedGraphControl1.Name = "zedGraphControl1";
            zedGraphControl1.ScrollGrace = 0D;
            zedGraphControl1.ScrollMaxX = 0D;
            zedGraphControl1.ScrollMaxY = 0D;
            zedGraphControl1.ScrollMaxY2 = 0D;
            zedGraphControl1.ScrollMinX = 0D;
            zedGraphControl1.ScrollMinY = 0D;
            zedGraphControl1.ScrollMinY2 = 0D;
            zedGraphControl1.Size = new System.Drawing.Size(709, 433);
            zedGraphControl1.TabIndex = 0;
            zedGraphControl1.UseExtendedPrintDialog = true;
            zedGraphControl1.IsEnableHZoom = false;
            zedGraphControl1.IsEnableVZoom = false;
            Controls.Add(zedGraphControl1);
        }

        public void RunTheScript()
        {
            bool RunFromStart = true;
            bool TestSet = false;
            Project pro = Project.GetIntance(RunFromStart);
            Stopwatch timer = new Stopwatch();
            timer.Start();
            pro.Run(RunFromStart, TestSet);
            timer.Stop();
            Project.LogAction("Project Saved.");
            Project.LogAction($"Accomplished in :{ timer.Elapsed}");
            BeginInvoke(new Action(()=>Close()));
        }

        public void UpdateLog(string text)=> BeginInvoke(Logger, text);  
        public void UpdateStateLog(string text)=> BeginInvoke(StateLogger, text);  
        public void UpdateParameterLog(string text)=> BeginInvoke(ParameterLogger, text);  

        private void MainFrm_Load(object sender, EventArgs e)
        {
            if (!Loaded)
            {
                Project.LogAction = UpdateLog;
                Project.StateLogAction = UpdateStateLog;
                Project.ParameterLogAction = UpdateParameterLog;
                backgroundWorker1.RunWorkerAsync();
                Loaded = true;
            }
        }
    }
}
