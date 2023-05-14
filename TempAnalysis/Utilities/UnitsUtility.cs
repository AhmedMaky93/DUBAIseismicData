using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TempAnalysis.Utilities
{
    [DataContract(IsReference =true)]
    public class UnitsUtility
    {
        public UnitsUtility()
        {

        }
        public double Convertmm3_to_m3(double volume)
        {
            return volume / Math.Pow(1000,3);
        }
        public double Convertmm_to_m(double volume)
        {
            return volume / 1000;
        }
    }
}
