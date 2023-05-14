using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace RenameFiles
{
    class Program
    {
        static void Main(string[] args)
        {
            string mainPath = "F:\\My study\\BackUp\\Data\\MyData\\Records";
            string toFolderpath = Path.Combine(mainPath, "All");
            for (int i = 1; i < 6; i++)
            {
                string Folderpath = Path.Combine(mainPath,$"E{i}");
                DirectoryInfo d = new DirectoryInfo(Folderpath); //Assuming Test is your Folder
                foreach (FileInfo f in d.GetFiles("*.csv"))
                {
                    if (!f.Name.StartsWith("site"))
                        continue;
                    // OpenFiles,
                    float Factor =0;
                    string RecordFileName = "";
                    using (StreamReader file = new StreamReader(f.FullName))
                    {
                        string ln = file.ReadLine();
                        ln = file.ReadLine();
                        string[] arr = ln.Split(",");
                        RecordFileName = arr[0];
                        Factor = float.Parse(arr[1]) * 90;
                    }

                    // Read Data From json
                    string allText = "";
                    using (StreamReader rd = new StreamReader(Path.Combine(Path.Combine(mainPath, $"E{i}"), RecordFileName + ".json")))
                    {
                        allText = rd.ReadToEnd();
                    }
                    dynamic dynObject = JsonConvert.DeserializeObject<dynamic>(allText);
                    float PGA_x = dynObject["PGA_x"];
                    float PGA_y = dynObject["PGA_y"];
                    float dT = dynObject["dT"];
                    List<float> data_x = new List<float>();
                    List<float> data_y = new List<float>();
                    foreach (float value in dynObject["data_x"])
                    {
                        data_x.Add(value * Factor);
                    }
                    foreach (float value in dynObject["data_y"])
                    {
                        data_y.Add(value * Factor);
                    }

                    string NewFileName = f.Name.Replace(".csv","").Replace("site", $"site_{i}_");
                    // Write Json
                    using (StreamWriter file = new StreamWriter(Path.Combine(toFolderpath, NewFileName+".json"), false))
                    {
                        file.WriteLine("{");
                        file.WriteLine("\t"+$"\"dT\": {dT},");
                        WriteList(file, "data_x", data_x);
                        WriteList(file, "data_y", data_y);
                        file.WriteLine("\t"+$"\"name\": \"{NewFileName}\"");
                        file.WriteLine("}");
                        file.Close();
                    }

                    // Write CSV
                    using (StreamWriter file = new StreamWriter(Path.Combine(toFolderpath, NewFileName+".csv"), false))
                    {
                        file.WriteLine("TH_file"); 
                        file.WriteLine(NewFileName); 
                        file.Close();
                    }

                }
            }
            Console.WriteLine("Hello World!");
        }
        public static void WriteList(StreamWriter file,string ListName, List<float> list )
        {
            file.WriteLine("\t" + $" \"{ListName}\": [");
            for (int i = 0; i < list.Count-1; i++)
            {
                file.WriteLine("\t" + "\t" + $"{list[i]},");
            }
            file.WriteLine("\t" + "\t" + $"{list[list.Count - 1]}");
            file.WriteLine("\t" + "],");
        }
    }
}
