namespace Timetabling.Internal.Specialized
{
    public struct Variable
    {
        internal Variable(int @class, int maxValue, VariableType type)
        {
            Class = @class;
            MaxValue = maxValue;
            Type = type;
        }

        public readonly VariableType Type;

        public readonly int Class;

        public readonly int MaxValue;
    }
}