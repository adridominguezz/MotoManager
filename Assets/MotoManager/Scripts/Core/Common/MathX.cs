using System;

namespace MotoManager.Core.Common
{
    public static class MathX
    {
        public const double G = 9.81;

        public static double Clamp(double v, double min, double max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        public static double Clamp01(double v)
        {
            return v < 0 ? 0 : (v > 1 ? 1 : v);
        }

        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Clamp01(t);
        }

        public static double InverseLerp(double a, double b, double v)
        {
            if (Math.Abs(b - a) < 1e-12) return 0;
            return Clamp01((v - a) / (b - a));
        }

        public static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = InverseLerp(edge0, edge1, x);
            return t * t * (3 - 2 * t);
        }

        public static double Logistic(double x)
        {
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        /// <summary>Maps a 0-100 attribute to a symmetric factor around 1: 50 -> 1, 100 -> 1+spread, 0 -> 1-spread.</summary>
        public static double AttrFactor(double attribute0To100, double spread)
        {
            return 1.0 + (attribute0To100 - 50.0) / 50.0 * spread;
        }

        /// <summary>Normalised 0-100 attribute to [-1, 1].</summary>
        public static double Centered(double attribute0To100)
        {
            return (attribute0To100 - 50.0) / 50.0;
        }

        public static double MoveTowards(double current, double target, double maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        public static string FormatLapTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return "--:--.---";
            int minutes = (int)(seconds / 60);
            double rest = seconds - minutes * 60;
            return minutes + ":" + rest.ToString("00.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatGap(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "-";
            return "+" + seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatMoney(double euros)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            double abs = Math.Abs(euros);
            string sign = euros < 0 ? "-" : "";
            if (abs >= 1e6) return sign + "€" + (abs / 1e6).ToString("0.00", ci) + "M";
            if (abs >= 1e3) return sign + "€" + (abs / 1e3).ToString("0", ci) + "k";
            return sign + "€" + abs.ToString("0", ci);
        }
    }
}
