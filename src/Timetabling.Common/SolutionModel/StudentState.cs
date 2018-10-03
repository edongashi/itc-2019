namespace Timetabling.Common.SolutionModel
{
    public class StudentState
    {
        public StudentState(EnrollmentState[] enrollmentStates, int conflictingPairs)
        {
            EnrollmentStates = enrollmentStates;
            ConflictingPairs = conflictingPairs;
        }

        public readonly EnrollmentState[] EnrollmentStates;

        public readonly int ConflictingPairs;

        public bool SingleClass
        {
            get
            {
                switch (EnrollmentStates.Length)
                {
                    case 0:
                        return true;
                    case 1:
                        return EnrollmentStates[0].Subparts.Length <= 1;
                    default:
                        return false;
                }
            }
        }
    }
}