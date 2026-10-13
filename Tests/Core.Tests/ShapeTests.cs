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

        [Fact]
        public void RealCentreLinesAreDrawnAsIsAndMatchTheOfficialLength()
        {
            int count = 0;
            foreach (var c in TestData.Db.Circuits.Values.Where(x => string.IsNullOrEmpty(x.Layout) && x.Shape != null))
            {
                count++;
                var track = TrackModel.Get(c);
                var geo = new TrackGeometry(track);
                Assert.NotNull(track.Shape);
                // The drawing is the real line, closed by construction, and as long as the official lap (data sanity).
                Assert.Equal(0, geo.ClosureError);
                Assert.InRange(geo.GeometricLength / c.LengthM, 0.97, 1.03);
                Assert.InRange(c.RadiusScale, 0.6, 1.7);
                // Track limits flagged at the first and last corners; direction of travel as declared.
                Assert.True(track.Segments.Count(s => s.TrackLimits) >= 2, c.Id);
                Assert.Equal(c.Clockwise ? -1 : 1, geo.Orientation);
                // Every corner's middle lies on a bend of the real line, turning the same way.
                foreach (var s in track.Segments.Where(s => s.IsCorner && Math.Abs(s.AngleDeg) > 30))
                {
                    double turn = 0, x, y, h, prev;
                    geo.Evaluate(s.Start, 0, out x, out y, out prev);
                    for (double d = s.Start + 2; d < s.End; d += 2)
                    {
                        geo.Evaluate(d, 0, out x, out y, out h);
                        double dh = h - prev;
                        while (dh > Math.PI) dh -= 2 * Math.PI;
                        while (dh < -Math.PI) dh += 2 * Math.PI;
                        turn += dh;
                        prev = h;
                    }
                    Assert.True(Math.Sign(turn) == Math.Sign(s.AngleDeg) && Math.Abs(turn * 180 / Math.PI) > Math.Abs(s.AngleDeg) * 0.5,
                                c.Id + " corner " + s.CornerNumber + ": drawn " + (turn * 180 / Math.PI).ToString("0") + " vs " + s.AngleDeg.ToString("0"));
                }
            }
            Assert.True(count >= 10, "real shapes: " + count);
        }

        [Theory]
        [InlineData("mugello", false)]
        [InlineData("jerez", true)]
        // Phillip Island's and Assen's hand-authored layouts miss closing by ~2.2 km, so their drawings must bend corners as well as
        // straights (to keep the visual speed even) and cannot round-trip exactly; Valencia covers the lat/lon path.
        [InlineData("valencia", true)]
        [InlineData("motegi", true)]
        [InlineData("catalunya", false)]
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
