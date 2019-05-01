namespace Timetabling.Internal
{
    public class ClassConflicts
    {
        public readonly int Time;

        public readonly int Room;

        public ClassConflicts(int time, int room)
        {
            Time = time;
            Room = room;
        }

        public int Total => Time + Room;

        public ClassConflicts Increment(int dt, int dr)
        {
            return new ClassConflicts(Time + dt, Room + dr);
        }
    }
}
