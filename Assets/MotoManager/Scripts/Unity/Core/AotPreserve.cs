using System.Collections.Generic;
using MotoManager.Core.Data;
using UnityEngine.Scripting;

namespace MotoManager.App
{
    /// <summary>
    /// IL2CPP compiles generics ahead of time. Newtonsoft creates these dictionary types by reflection while
    /// reading data and saves, so they are instantiated here once to make sure the native code exists.
    /// Never called.
    /// </summary>
    [Preserve]
    public static class AotPreserve
    {
        [Preserve]
        public static void Instantiate()
        {
            new Dictionary<PerfAttr, double>();
            new Dictionary<PerfAttr, Dictionary<string, double>>();
            new Dictionary<FacilityType, int>();
            new Dictionary<InfractionType, PenaltyDefinition>();
            new Dictionary<int, string>();
            new Dictionary<string, List<int>>();
            new Dictionary<string, Dictionary<string, double>>();
            new List<RidingStyle>();
            new List<Pyramid>();
            new List<TireCompoundSpec>();
        }
    }
}
