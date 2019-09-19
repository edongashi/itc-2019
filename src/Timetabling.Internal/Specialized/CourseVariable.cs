using System.Linq;

namespace Timetabling.Internal.Specialized
{
    public class CourseVariable
    {
        internal CourseVariable(
            int variableId,
            int student, 
            int courseId,
            int courseIndex,
            Chain[] chains)
        {
            VariableId = variableId;
            StudentId = student;
            CourseId = courseId;
            CourseIndex = courseIndex;
            ClassChains = chains;
            ChainCount = chains.Length;
        }

        public readonly int VariableId;

        public readonly int StudentId;

        public readonly int CourseId;

        public readonly int CourseIndex;

        public readonly Chain[] ClassChains;

        public readonly int ChainCount;
    }
}