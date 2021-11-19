using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Timetabling.Internal
{
    public static class Config
    {
        // Search

        public static int MaxTimeout = 1_000_000;

        public static int ExtraTimeoutPerClassVariable = 300;

        public static int MaxLocalTimeout = 3_000_000;

        public static int LocalBanAfterTimeout = 500_000;

        public static bool RollingEffect = true;

        // FStun

        public static double FStunGamma = 0.95;

        // Constraint focused search (random walks)

        public static bool FocusedSearchEnabled = true;

        public static double FocusedSearchTemperatureInitial = 1E-2;

        public static double FocusedSearchTemperatureChange = 0.99999;

        public static int FocusedSearchMaxConstraints = 3;

        public static int FocusedSearchMinWeight = 4;

        public static int FocusedSearchTimeoutMax = 500_000;

        // Infeasible operators distribution

        public static int InfeasibleTimeMutationOccurrences = 1;

        public static int InfeasibleRoomMutationOccurrences = 1;

        public static int InfeasibleVariableMutationOccurrences = 2;

        public static int InfeasibleEnrollmentMutationOccurrences = 0;

        public static int InfeasibleDoubleEnrollmentMutationOccurrences = 0;

        // Feasible operators distribution

        public static int FeasibleTimeMutationOccurrences = 1;

        public static int FeasibleRoomMutationOccurrences = 1;

        public static int FeasibleVariableMutationOccurrences = 2;

        public static int FeasibleEnrollmentMutationOccurrences = 2;

        public static int FeasibleDoubleEnrollmentMutationOccurrences = 1;

        // Temperature

        public static double TemperatureInitial = 1E-3;

        public static double TemperatureRestart = 1E-4;

        public static double TemperatureReload = 1E-6;

        public static double TemperatureBeta = 6E-3;

        public static double TemperatureBetaUnfeasible = 3E-3;

        // Solution evaluation

        public static double InfeasibleSolutionHardPenaltyFactor = 0.01;

        public static double InfeasibleSolutionRoundingPrecision = 0.01;

        // Penalization

        public static double HardPenalizationFlat = 0.004;

        public static double HardPenalizationRate = 1.1;

        public static double HardPenalizationPressure = 0.0;

        public static double HardPenalizationDecay = 0.9;

        public static double SoftPenalizationRate = 1.1;

        public static double SoftPenalizationFlat = 1E-3;

        public static double SoftPenalizationConflicts = 1E-2;

        public static double SoftPenalizationStudentsTime = 1E-2;

        public static double SoftPenalizationStudentsRoom = 1E-3;

        public static double SoftPenalizationAssignment = 1E-2;

        public static double SoftPenalizationDecayFlat = 1E-3;

        public static double SoftPenalizationDecayRate = 0.9;

        // Methods

        private static object Parse(string value, Type type)
        {
            if (type == typeof(string))
                return value;
            if (type == typeof(int))
                return int.Parse(value.Replace("_", ""));
            if (type == typeof(double))
                return double.Parse(
                    value.Replace("_", ""),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture);
            if (type == typeof(bool))
                return bool.Parse(value);
            throw new InvalidOperationException("Unexpected field type.");
        }

        public static void Load(string source)
        {
            var fields = typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public);

            void AssignValue(string[] parts)
            {
                if (parts.Length != 2) throw new InvalidOperationException("Invalid config line.");
                var name = parts[0];
                var value = parts[1];
                var field = fields.FirstOrDefault(p => p.Name == name);
                if (field == null) throw new InvalidOperationException($"Unknown config property '{name}`'.");
                var parsedValue = Parse(value, field.FieldType);
                field.SetValue(null, parsedValue);
            }

            Regex.Split(source, @"\r?\n")
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("#"))
                .Select(x => x.Split('=').Select(part => part.Trim()).ToArray())
                .ToList()
                .ForEach(AssignValue);
        }

        public static string Serialize()
        {
            var fields = typeof(Config)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Select(x => $"{x.Name} = {x.GetValue(null)}");
            return string.Join("\n", fields);
        }
    }
}
