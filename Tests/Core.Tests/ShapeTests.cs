using System;
using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    /// <summary>Real centre lines (e.g. OpenStreetMap) must turn into equivalent simulation layouts.</summary>
    public class ShapeTests
    {
        private readonly ITestOutputHelper _out;

        public ShapeTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static CircuitDefinition FromDrawnShape(CircuitDefinition original, bool latLon)
        {
            var geo = new TrackGeometry(TrackModel.Get(original));
            var shape = new List<double[]>();
            int stride = Math.Max(1, (int)Math.Round(10 / geo.Spacing)); // like map nodes every ~10 m
            for (int i = 0; i < geo.PointCount - 1; i += stride)
            {
                if (!latLon) shape.Add(new[] { geo.X[i], geo.Y[i] });
                else shape.Add(new[] { 44.0 + geo.Y[i] / 111132.0, 11.0 + geo.X[i] / (111320.0 * Math.Cos(44.0 * Math.PI / 180)) });
            }
            var copy = JsonSettings.DeepClone(original);
            copy.Id = original.Id + (latLon ? "_ll" : "_shape");
            copy.Layout = null;
            copy.Shape = shape;
            copy.ShapeIsLatLon = latLon;
            copy.ShapeStart = shape[0];
            copy.Clockwise = geo.Orientation < 0;
            return copy;
        }

        private static double RefLap(CircuitDefinition c)
        {
            var db = TestData.Db;
            var cfg = new RaceConfig { Rules = db.GetRules("motogp", 2026), Circuit = c, Tuning = db.Tuning, Weather = new WeatherSetup { AirTemp = c.AverageAirTempC } };
            var rider = new Rider { Id = "ref" };
            foreach (var n in RiderAttributes.Names) rider.Attributes.Set(n, 75);
            var bike = BikePerformance.Average();
            for (int i = 0; i < bike.Values.Length; i++) bike.Values[i] = 65;
            return RaceSimulation.EstimateLap(cfg, new RaceEntry { Rider = rider, Bike = bike, SetupQuality = 0.6, FrontCompound = TireCompound.Medium, RearCompound = TireCompound.Medium });
        }

        [Theory]
        [InlineData("mugello", false)]
        [InlineData("jerez", true)]
        [InlineData("assen", false)]
        [InlineData("phillipisland", true)]
        public void ShapeRoundTripGivesEquivalentCircuit(string id, bool latLon)
        {
            var original = TestData.Db.Circuits[id];
            var derived = FromDrawnShape(original, latLon);
            var a = TrackModel.Get(original);
            var b = TrackModel.Get(derived);
            var gb = new TrackGeometry(b);
            double turn = b.Segments.Sum(s => s.AngleDeg);
            double lapA = RefLap(original), lapB = RefLap(derived);
            _out.WriteLine($"{id}: corners {a.CornerCount} -> {b.CornerCount}, turn {turn:0}, closure {gb.ClosureError:0.0} m, lap {lapA:0.00} -> {lapB:0.00}");
            _out.WriteLine(ShapeConverter.ToLayout(derived));
            Assert.InRange(Math.Abs(Math.Abs(turn) - 360), 0, 15);
            Assert.True(gb.ClosureError < 0.04 * b.Length, "derived layout should close by itself: " + gb.ClosureError);
            Assert.InRange(lapB / lapA, 0.96, 1.04);
        }
    }
}
