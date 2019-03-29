using System;
using System.Linq;

namespace Timetabling.Common.ProblemModel
{
    public struct Variable
    {
        public Variable(int @class, int maxValue, VariableType type)
        {
            Class = @class;
            MaxValue = maxValue;
            Type = type;
        }

        public readonly VariableType Type;

        public readonly int Class;

        public readonly int MaxValue;

        public int[] Shuffle(Random random)
        {
            var result = new int[MaxValue];
            for (var i = 0; i < MaxValue; i++)
            {
                var j = random.Next(0, i + 1);
                if (i != j)
                {
                    result[i] = result[j];
                }

                result[j] = i;
            }

            return result;
        }
    }

    public enum VariableType
    {
        None,
        Time,
        Room
    }

    public struct CourseVariable
    {
        public CourseVariable(int student, int[][][] values)
        {
            Student = student;
            Values = values;
            LooseValues = values.SelectMany(c => c.SelectMany(s => s)).ToArray();
        }

        public readonly int Student;

        // Configurations -> Child Subparts -> Classes
        public readonly int[][][] Values;

        public readonly int[] LooseValues;
    }
}