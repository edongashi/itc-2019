namespace Timetabling.Internal.Specialized
{
    internal class StudentState
    {
        internal StudentState(EnrollmentState[] enrollmentStates, int conflictingPairs)
        {
            EnrollmentStates = enrollmentStates;
            ConflictingPairs = conflictingPairs;
        }

        internal readonly EnrollmentState[] EnrollmentStates;

        internal readonly int ConflictingPairs;

        internal bool SingleClass
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
