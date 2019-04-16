namespace Timetabling.Internal
{
    public class RoomAssignment
    {
        public RoomAssignment(int id, int penalty)
        {
            Id = id;
            Penalty = penalty;
        }

        public readonly int Id;

        public readonly int Penalty;
    }
}
