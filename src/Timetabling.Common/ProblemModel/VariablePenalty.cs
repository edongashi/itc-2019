namespace Timetabling.Common.ProblemModel
{
    public class VariablePenalty
    {
        public VariablePenalty(int @class, double[] values)
        {
            Class = @class;
            Values = values;
        }

        public readonly int Class;

        public readonly double[] Values;
    }
}