using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;

namespace TempAnalysis.OpenseesCommand
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(BaseNodesRegion))]
    [KnownType(typeof(Nodes2DRegion))]
    [KnownType(typeof(MasterNodesRegion))]
    [KnownType(typeof(ElementsRegion))]
    public class Region : BaseCommand
    {
        [DataMember]
        public long ID;
        [DataMember]
        public string Name;
        [DataMember]
        public long StartIndex;
        [DataMember]
        public long EndIndex;

        public Region()
        {

        }
        public Region(string Name, IDsManager IDM, long Start, long End)
        {
            ID = ++IDM.LastRegionId;
            this.Name = Name;
            this.StartIndex = Start;
            this.EndIndex = End;
        }
        public override void WriteCommand(StreamWriter writer)
        {
        }
        public virtual void WriteRecorders(StreamWriter writer)
        { 
        }
        public string GetFilePath(string FolderPath)
        {
            return Path.Combine(FolderPath,$"{Name}.out" );
        }
        public virtual void ReadResults(string FolderPath, LoadCombinationFactors factors)
        {
            
        }
        public static bool ConvertLineToDoubleList(string line, out List<double> doubleList)
        {
            doubleList = new List<double>();
            char[] whitespace = new char[] { ' ', '\t' };
            var strings = line.Split(whitespace).Where(x => !string.IsNullOrEmpty(x)).Select(s => double.Parse(s)).ToList();
            if (!strings.Any())
                return false;
            foreach (var stringValue in strings)
            {
                doubleList.Add(stringValue);
            }
            return doubleList.Any();

        }
    }

    [DataContract(IsReference = true)]
    public class Nodes2DRegion : Region
    {
        public Nodes2DRegion()
        {

        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"region {ID} -nodeRange {StartIndex} {EndIndex};");
        }
        public Nodes2DRegion(string Name, IDsManager IDM, long Start, long End) : base(Name, IDM, Start, End)
        {

        }
    }
    [DataContract(IsReference = true)]
    public class BaseNodesRegion : Region
    {
        public BaseNodesRegion()
        {

        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"region {ID} -nodeRange {StartIndex} {EndIndex};");
        }
        public BaseNodesRegion(string Name,IDsManager IDM, long Start, long End):base(Name, IDM, Start,End) 
        {

        }
    }

    [DataContract(IsReference = true)]
    public class MasterNodesRegion : Region
    {
        public MasterNodesRegion()
        {

        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"region {ID} -nodeRange {StartIndex} {EndIndex};");
        }
        public MasterNodesRegion(string Name, IDsManager IDM, long Start, long End) : base(Name, IDM, Start, End)
        {

        }
    }

    [DataContract(IsReference = true)]
    public class ElementsRegion : Region
    {
        [DataMember]
        public Dictionary<LoadCombinationFactors, double> SelectedValues = new Dictionary<LoadCombinationFactors, double>();
        public ElementsRegion()
        {

        }
        public ElementsRegion(string Name, IDsManager IDM, long Start, long End) : base(Name, IDM, Start, End)
        {

        }
        public override void WriteCommand(StreamWriter writer)
        {
            writer.WriteLine($"region {ID} -eleRange {StartIndex} {EndIndex};");
        }
        public override void WriteRecorders(StreamWriter writer)
        {
            writer.WriteLine($"recorder EnvelopeElement -file {Name}.out -region {ID} localForce;");
        }
        public override void ReadResults(string FolderPath, LoadCombinationFactors factors)
        {
            string filePath = GetFilePath(FolderPath);
            if (!File.Exists(filePath))
                return;

            double N = 0;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs, Encoding.Default))
                {
                    string line = sr.ReadLine();
                    List<double> doubleList;

                    while (!string.IsNullOrEmpty(line) && ConvertLineToDoubleList(line, out doubleList))
                    {
                        for (int i = 0; i < doubleList.Count; i+=6)
                        {
                            N = Math.Max(N, doubleList[i]);
                        }
                        line = sr.ReadLine();
                    }
                    sr.Close();
                }
                fs.Close();
            }
            SelectedValues.Add(factors,N);

        }
    }

}
