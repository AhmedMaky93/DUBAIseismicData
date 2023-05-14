
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GISChapter
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(PropbabilisticParameter))]
    public class DeterminsticParameter
    {
        [DataMember]
        public double V1;
        [DataMember]
        public double V2;
        [DataMember]
        public double Mean;
        [DataMember]
        public int N;

        public DeterminsticParameter()
        {

        }
        public DeterminsticParameter(int _N, double _mean)
        {
            this.N = _N;
            this.Mean = _mean;
            this.V1 = _mean;
            this.V2 = _mean;
        }
        public DeterminsticParameter(int _N, double _mean, double _V1, double _V2)
        {
            this.N = _N;
            this.Mean = _mean;
            this.V1 = _V1;
            this.V2 = _V2;
        }
        public virtual string GetString()
        {
            return $"{Mean},-,{V1},{V2}";
        }
        public static int GetN(DeterminsticParameter p1, DeterminsticParameter p2)
        {
            return p1.N + p2.N;
        }
        public static double GetMean(DeterminsticParameter p1, DeterminsticParameter p2)
        {
            return (p1.N * p1.Mean + p2.N * p2.Mean) / (p1.N + p2.N);
        }
        public static DeterminsticParameter Add(DeterminsticParameter p1, DeterminsticParameter p2)
        {
            return new DeterminsticParameter(GetN(p1,p2), GetMean(p1,p2), Math.Min(p1.V1, p2.V1), Math.Max(p1.V2, p2.V2));
        }
             
    }

    [DataContract(IsReference = true)]
    public class PropbabilisticParameter : DeterminsticParameter
    {
        [DataMember]
        public double Std;
        

        public PropbabilisticParameter()
        {

        }
        public PropbabilisticParameter(int _N, double _mean, double _Std, double _V1, double _V2) :base(_N,_mean,_V1,_V2)
        {
            this.Std = _Std;
        }
        public static double GetStd(PropbabilisticParameter p1, PropbabilisticParameter p2)
        {
            return Math.Sqrt( (p1.N * Math.Pow(p1.Std,2) + p2.N * Math.Pow(p2.Std, 2)) / (p1.N + p2.N) );
        }
        public static PropbabilisticParameter Add(PropbabilisticParameter p1, PropbabilisticParameter p2)
        {
            return new PropbabilisticParameter(GetN(p1, p2), GetMean(p1, p2)
                , GetStd(p1,p2), Math.Min(p1.V1, p2.V1), Math.Max(p1.V2, p2.V2));
        }
        public override string GetString()
        {
            return $"{Mean},{Std},{V1},{V2}";
        }
    }

    [DataContract(IsReference = true)]
    public class BuildingResult
    {
        [DataMember]
        public int N;
        [DataMember]
        public PropbabilisticParameter RepairCost;
        [DataMember]
        public DeterminsticParameter Collapse_Prob;
        [DataMember]
        public DeterminsticParameter S_Repair;
        [DataMember]
        public DeterminsticParameter NSA_Repair;
        [DataMember]
        public DeterminsticParameter NSD_Repair;
        [DataMember]
        public PropbabilisticParameter RepairTime;
        [DataMember]
        public DeterminsticParameter Sev1_Injuries;
        [DataMember]
        public DeterminsticParameter Sev2_Injuries;
        [DataMember]
        public DeterminsticParameter Sev3_Injuries;
        [DataMember]
        public DeterminsticParameter Sev4_Injuries;
        [DataMember]
        public DeterminsticParameter Injuries;
        public BuildingResult()
        {

        }
        public void SetNofModels(int _N)
        {
            N = _N;
            RepairCost.N = N;
            Collapse_Prob.N = N;
            S_Repair.N = N;
            NSA_Repair.N = N;
            NSD_Repair.N = N;
            RepairTime.N = N;
            Sev1_Injuries.N = N;
            Sev2_Injuries.N = N;
            Sev3_Injuries.N = N;
            Sev4_Injuries.N = N;
            Injuries.N = N;
        }
        public static BuildingResult Add(BuildingResult R1, BuildingResult R2)
        {
            return new BuildingResult
            {
                N = R1.N + R2.N,
                RepairCost = PropbabilisticParameter.Add(R1.RepairCost, R2.RepairCost),
                Collapse_Prob = DeterminsticParameter.Add(R1.Collapse_Prob, R2.Collapse_Prob),
                S_Repair = DeterminsticParameter.Add(R1.S_Repair, R2.S_Repair),
                NSA_Repair = DeterminsticParameter.Add(R1.NSA_Repair, R2.NSA_Repair),
                NSD_Repair = DeterminsticParameter.Add(R1.NSD_Repair, R2.NSD_Repair),
                RepairTime = PropbabilisticParameter.Add(R1.RepairTime, R2.RepairTime),
                Sev1_Injuries = DeterminsticParameter.Add(R1.Sev1_Injuries, R2.Sev1_Injuries),
                Sev2_Injuries = DeterminsticParameter.Add(R1.Sev2_Injuries, R2.Sev2_Injuries),
                Sev3_Injuries = DeterminsticParameter.Add(R1.Sev3_Injuries, R2.Sev3_Injuries),
                Sev4_Injuries = DeterminsticParameter.Add(R1.Sev4_Injuries, R2.Sev4_Injuries),
                Injuries = DeterminsticParameter.Add(R1.Injuries, R2.Injuries)
            };
        }
        public static BuildingResult CreateFromFile(List<double> list, int N =1)
        {
            int Perc = 100;
            int i = 1;
            BuildingResult results = new BuildingResult();
            results.N = N;
            results.RepairCost = new PropbabilisticParameter(N,list[i] * Perc, list[i+1], list[i + 2] * Perc, list[i + 4] *Perc);
            i += 5;
            results.Collapse_Prob = new DeterminsticParameter(N,list[i] * Perc);
            i +=1;
            results.S_Repair = new DeterminsticParameter(N, list[i] * Perc);
            i += 11;
            results.NSA_Repair = new DeterminsticParameter(N, list[i] * Perc);
            i += 5;
            results.NSD_Repair = new DeterminsticParameter(N, list[i] * Perc);
            i += 5;
            results.RepairTime = new PropbabilisticParameter(N, list[i], list[i + 1], list[i + 2], list[i + 4]);
            i += 5;
            results.Sev1_Injuries = new PropbabilisticParameter(N, list[i] * Perc, list[i + 1], list[i + 2] * Perc, list[i + 4] * Perc);
            i += 5;
            results.Sev2_Injuries = new PropbabilisticParameter(N, list[i] * Perc, list[i + 1], list[i + 2] * Perc, list[i + 4] * Perc);
            i += 5;
            results.Sev3_Injuries = new PropbabilisticParameter(N, list[i] * Perc, list[i + 1], list[i + 2] * Perc, list[i + 4] * Perc);
            i += 5;
            results.Sev4_Injuries = new PropbabilisticParameter(N, list[i] * Perc, list[i + 1], list[i + 2] * Perc, list[i + 4] * Perc);
            results.Injuries = new DeterminsticParameter(N,
                results.Sev1_Injuries.Mean + results.Sev2_Injuries.Mean + results.Sev3_Injuries.Mean+ results.Sev4_Injuries.Mean);
            return results;
        }
    }
}
