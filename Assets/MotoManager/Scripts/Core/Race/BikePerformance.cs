using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Race
{
    /// <summary>Derived 0-100 performance attributes of a specific bike.</summary>
    [Serializable]
    public class BikePerformance
    {
        public readonly double[] Values = new double[Enum.GetValues(typeof(PerfAttr)).Length];

        public double this[PerfAttr a]
        {
            get { return Values[(int)a]; }
            set { Values[(int)a] = value; }
        }

        public static BikePerformance Average()
        {
            var p = new BikePerformance();
            for (int i = 0; i < p.Values.Length; i++) p.Values[i] = 50;
            return p;
        }

        /// <summary>
        /// Translates component levels into performance using the weights of the bike class.
        /// Production-derived classes blend in the homologated road bike, prototypes don't.
        /// </summary>
        public static BikePerformance Compute(Motorcycle bike, BikeClassDefinition bikeClass)
        {
            var perf = new BikePerformance();
            foreach (PerfAttr attr in Enum.GetValues(typeof(PerfAttr)))
            {
                double value = 50;
                Dictionary<string, double> weights;
                if (bikeClass != null && bikeClass.PerformanceWeights.TryGetValue(attr, out weights) && weights.Count > 0)
                {
                    double sum = 0, wsum = 0;
                    foreach (var kv in weights)
                    {
                        sum += bike.Component(kv.Key) * kv.Value;
                        wsum += Math.Abs(kv.Value);
                    }
                    if (wsum > 0) value = sum / wsum;
                }
                double share = bikeClass != null ? bikeClass.ProductionBaseShare : 0;
                double baseValue;
                if (share > 0 && bike.ProductionBase.TryGetValue(attr, out baseValue))
                    value = value * (1 - share) + baseValue * share;
                perf[attr] = MathX.Clamp(value, 1, 100);
            }
            return perf;
        }
    }
}
