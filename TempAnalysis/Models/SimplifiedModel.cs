using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.OpenseesCommand;

namespace TempAnalysis.Models
{
    [DataContract(IsReference = true)]
    public class SimplifiedFloor: IStructuralFloor
    {
        [DataMember]
        public Node2D Node;
        [DataMember]
        public int _Index;
        [DataMember]
        public double GravityForce;
        [DataMember]
        public ZeroLengthElement ShearElement;
        [DataMember]
        public ZeroLengthElement MomentElement;
        [DataMember]
        public List<double> _MFs = new List<double>();
        [DataMember]
        public List<double> _MVs = new List<double>();
        [DataMember]
        public List<double> _MMs = new List<double>();
        [DataMember]
        public double _Vd;
        [DataMember]
        public double _Md;
        [DataMember]
        public OpenSeesMaterial ShearMaterial;
        [DataMember]
        public OpenSeesMaterial MomentMaterial;


        public int Index
        {
            get { return _Index; }
            set { _Index = value; }
        }
        public List<double> MFs 
        { 
            get { return _MFs; } 
            set { _MFs = value; } 
        }
        public List<double> MVs
        {
            get { return _MVs; }
            set { _MVs = value; }
        }
        public List<double> MMs
        {
            get { return _MMs; }
            set { _MMs = value; }
        }
        public double Vd
        {
            get { return _Vd; }
            set { _Vd = value; }
        }
        public double Md
        {
            get { return _Md; }
            set { _Md = value; }
        }

        public SimplifiedFloor()
        {

        }
        public SimplifiedFloor(int index)
        {
            this.Index = index;
        }
        internal void SetNonLinearMaterials(IDsManager IDM, double Omega_y, double Omega_p, double Mu, double EI, double GA)
        {
             double Vy = Vd * Omega_y;
             double My = Md * Omega_y;

             double uy = Vy / GA;
             double Setay = My / EI;

             double Vp = Vy * Omega_p;
             double Mp = My * Omega_p;

             double u_p = uy * Mu * Omega_p;
             double Seta_p = Setay * Mu * Omega_p;

             MaterialPart ShearTension = new MaterialPart
             {
                 P1 = new Utilities.Point2D(Vy, uy),
                 P2 = new Utilities.Point2D(Vp, u_p),
                 P3 = new Utilities.Point2D(Vp, Math.Max(1.0, 5.0 * u_p)),
             };
             MaterialPart MomentTension = new MaterialPart
             {
                 P1 = new Utilities.Point2D(My, Setay),
                 P2 = new Utilities.Point2D(Mp, Seta_p),
                 P3 = new Utilities.Point2D(Mp, Math.Max(1.0, 5.0 * Seta_p))
             };
             ShearMaterial = new HystericMaterial(IDM)
             {
                 Tension = ShearTension,
                 Compresion = ShearTension.Multiply(-1),
             };

             MomentMaterial = new HystericMaterial(IDM)
             {
                 Tension = MomentTension,
                 Compresion = MomentTension.Multiply(-1),
             };

        }

        public double GetFloorMass()
        {
            return Node.Mass;
        }
        public double GetDesginFloorForce(List<ModeShapeData> modeShapeData)
        {
            double F = 0;
            double Over = 0;
            for (int i = 0; i < modeShapeData.Count; i++)
            {
                F += modeShapeData[i].Mx * _MFs[i];
                Over += modeShapeData[i].Mx;
            }
            return F / Over;
        }
    }
    

    [DataContract(IsReference = true)]
    public class SimplifiedModel : IModalAnalysisModel
    {
        [DataMember]
        public string Name;
        [DataMember]
        public OpenSeesTranslator.OpenSeesTranslator OpenSeesTranslator;
        [DataMember]
        public IDsManager IDsManager;
        [DataMember]
        public Node2D BaseNode;
        [DataMember]
        public List<SimplifiedFloor> Floors = new List<SimplifiedFloor>();
        [DataMember]
        public List<ModeShapeData> ModeShapes = new List<ModeShapeData>();
        [DataMember]
        public ResponseParameters ResponseParameters = new ResponseParameters();
        [DataMember]
        public MasterNodesRegion MasterNodesRegion;
        [DataMember]
        public BaseNodesRegion BaseNodesRegion;
        [DataMember]
        public PushOverResults PushOverResults;
        [DataMember]
        public double Omega_y;
        [DataMember]
        public double Omega_P;
        [DataMember]
        public double Meu;
        [DataMember]
        public double EI;
        [DataMember]
        public double GA;

        public List<ModeShapeData> _ModeShapes
        {
            get { return ModeShapes; }
            set { ModeShapes = value; }
        }
        
        public ResponseParameters _ResponseParameters
        {
            get { return ResponseParameters; }
            set { ResponseParameters = value; }
        }
        public MasterNodesRegion _MasterNodesRegion
        {
            get { return MasterNodesRegion; }
            set { MasterNodesRegion = value; }
        }
        public BaseNodesRegion _BaseNodesRegion
        {
            get { return BaseNodesRegion; }
            set { BaseNodesRegion = value; }
        }
        public int _NumOfFloors
        {
            get { return Floors.Count; }
        }
        public string _Name
        {
            get { return Name; }
        }
        public bool _3DModel
        {
            get
            {
                return false;
            }
        }
        public PushOverResults _PushOverResults
        {
            get { return PushOverResults; }
            set { PushOverResults = value; }
        }
        
        public void PostCalculateFloorForces()
        {
            double minBaseVd = 0.2 * Floors[0].Vd;
            double BaseMd = Floors[0].Md;

            int FMC = Math.Max(2, _NumOfFloors / 10);
            for (int i = 0; i < Floors.Count; i++)
            {
                SimplifiedFloor f = Floors[i];
                f.Vd = Math.Max(f.Vd, minBaseVd);
                f.Md = (i > FMC) ? 1.2 * f.Md : BaseMd;
            }
        }
        public List<IStructuralFloor> GetModelFloors()
        {
            return Floors.Cast<IStructuralFloor>().ToList();
        }
        internal async Task ClaibrateNonLinear()
        {
            for (int i = 0; i < Floors.Count; i++)
            {
                Floors[i].SetNonLinearMaterials(IDsManager, Omega_y, Omega_P, Meu, EI, GA);
            }
        }
        public SimplifiedModel()
        {

        }
        public void CreateMainElements(double FloorHeight, int NumberOfFloors, double TypicalFloorMass, double LastFloorMass, double TypicalFloorGravity, double LastFloorGravity)
        {
            IDsManager = new IDsManager();
            OpenSeesTranslator = new OpenSeesTranslator.OpenSeesTranslator();
            InitFloors(NumberOfFloors);
            CreateNodes(FloorHeight,TypicalFloorMass,LastFloorMass, TypicalFloorGravity, LastFloorGravity);
            CreateElements();
        }
        private void CreateElements()
        {
            Node2D PreviousNode = BaseNode;
            Floors.ForEach(f =>
            {
                f.ShearElement = new ZeroLengthElement(IDsManager, PreviousNode, f.Node);
                f.MomentElement = new ZeroLengthElement(IDsManager, PreviousNode, f.Node);
                PreviousNode = f.Node;
            });
        }
        internal void SetElasticElements(double EI, double GA)
        {
            this.EI = EI;
            this.GA = GA;
            ElasticMaterial EI_Material = new ElasticMaterial(IDsManager, EI);
            ElasticMaterial GA_Material = new ElasticMaterial(IDsManager, GA);
            Floors.ForEach(f=> {
                f.ShearMaterial = GA_Material;
                f.MomentMaterial = EI_Material;
            });
        }
        private void CreateNodes(double FloorHeight, double TypicalFloorMass, double LastFloorMass, double TypicalFloorGravity, double LastFloorGravity)
        {
            BaseNode = new Node2D(IDsManager, 0, 0);
            Floors.ForEach(f =>
            {
                f.Node = new Node2D(IDsManager, 0, FloorHeight * f.Index, TypicalFloorMass);
                f.GravityForce = TypicalFloorGravity;
            });

            SimplifiedFloor RoofFloor = Floors.Last();
            RoofFloor.Node.Mass = LastFloorMass;
            RoofFloor.GravityForce = LastFloorGravity;
            BaseNodesRegion = new BaseNodesRegion("Base Nodes",IDsManager, BaseNode.ID,BaseNode.ID);
            MasterNodesRegion = new MasterNodesRegion("Nodes", IDsManager
                , Floors.First().Node.ID, RoofFloor.Node.ID);
        }
        private void InitFloors(int numberOfFloors)
        {
            for (int i = 0; i < numberOfFloors; i++)
            {
                Floors.Add(new SimplifiedFloor(i+1));
            }
        }
        public List<double> GetModalForces()
        {
            return Floors.Select(x => x.GetDesginFloorForce(_ModeShapes)).ToList();
        }
        public async Task RunModalAnalysis()
        {
            OpenSeesTranslator.RunModel(this, new ModalAnalysisLoadCase(IDsManager,false));
        }
        public async Task RunPushOverResults(double power)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            OpenSeesTranslator.RunModel(this, new PushOverLoadCase(IDsManager, power));
            timer.Stop();
            this.PushOverResults.Time = timer.ElapsedMilliseconds;
        }
        public int GetNumOfModeShapes()
        {
            return _NumOfFloors;
        }
        public void WriteBaseNodesRecorderCommands(StreamWriter file, string FileName)
        {
            file.WriteLine($"recorder Node -file {FileName} -node {BaseNode.ID} -dof 1 reaction;");
        }
        public void MoveOpenseesFiles(string ModelFolderPath)
        {
            string exepath = Assembly.GetExecutingAssembly().Location;
            string openseesPath = Path.Combine(Path.Combine(Path.GetDirectoryName(exepath), "OpenSees"));

            string directoryName = Path.Combine(ModelFolderPath, "OpenSees");
            //FileSystem.CopyDirectory(openseesPath, directoryName);
            this.OpenSeesTranslator.FolderPath = Path.Combine(Path.Combine(directoryName, "bin"));
        }

        public void WriteMateials(StreamWriter file)
        {
            List<OpenSeesMaterial> materials = new List<OpenSeesMaterial>();
            Floors.ForEach(f => materials.Add(f.ShearMaterial));
            Floors.ForEach(f => materials.Add(f.MomentMaterial));
            materials = materials.Distinct().ToList();
            materials.ForEach(m=> m.WriteCommand(file));
        }
        public void WriteLoadSurfacesCommands(StreamWriter file, LoadCombinationFactors factors)
        {
            //// static ID for gravity loads
            //file.WriteLine("pattern Plain 1 Linear {");
            //FloorsGroups.ForEach(fg =>
            //{
            //    fg.Floors.ForEach(f => f.WriteGravityLoadsCommands(file, Gridsystem, IDsManager, LayoutUtility, GetInnerShearWallLength(), BeamLength,
            //        new double[] { GetCornerColumnFloorLoad(f.Index, factors), GetOuterColumnFloorLoad(f.Index, factors)
            //        , GetInnerColumnFloorLoad(f.Index, factors), GetCoreColumnFloorLoad(f.Index, factors) }));
            //});
            //file.WriteLine("}");
            //file.WriteLine("puts \"Gravity Loads Added\" ");
        }
        public void WriteWallsElements(StreamWriter file, bool elastic)
        {
        }
        public void WriteFrameElements(StreamWriter file, bool elastic)
        {
            List<int> shearDirs = new List<int>() { 1,2};
            List<int> momentDirs = new List<int>() { 3 };

            Floors.ForEach(f =>
            {
                f.ShearElement?.WriteCommand(file, shearDirs, f.ShearMaterial);
                f.MomentElement?.WriteCommand(file, momentDirs, f.MomentMaterial);
            });
        }
        public void WriteNodes(StreamWriter file)
        {
            file.WriteLine($"wipe;  # clear opensees model:{Name}");
            file.WriteLine($"wipeAnalysis;  # clear opensees model:{Name}");
            file.WriteLine("model basic -ndm 2 -ndf 2;  # 2 dimensions, 3 dof per node");
            
            BaseNode?.WriteCommand(file);
            BaseNodesRegion?.WriteCommand(file);
            file.WriteLine($"fix {BaseNode.ID} 1 1;");

            Floors.ForEach(f => f.Node?.WriteCommand(file));
            MasterNodesRegion?.WriteCommand(file);
        }

        
        public double GetH()
        {
            return Floors.Last().Node.Y;
        }
        public long GetRoofNodeID()
        {
            return Floors.Last().Node.ID;
        }

    }
}
