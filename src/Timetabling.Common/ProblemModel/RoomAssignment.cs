namespace Timetabling.Common.ProblemModel
{
    public class RoomAssignment
    {
        public RoomAssignment(int id, int penalty)
        {
            Id = id;
            Penalty = penalty;
        }

        public int Id { get; }

        public int Penalty { get; }
    }
}