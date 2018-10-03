namespace Timetabling.Common.ProblemModel
{
    public class Variable
    {
        public Variable(int @class, int maxValue)
        {
            Class = @class;
            MaxValue = maxValue;
        }

        public readonly int Class;

        public readonly int MaxValue;
    }
}