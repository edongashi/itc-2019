namespace Timetabling.Common.SolutionModel
{
    public struct ClassOverride
    {
        public ClassOverride(int @class, int room, int time)
        {
            Class = @class;
            Room = room;
            Time = time;
        }

        public readonly int Class;

        public readonly int Room;

        public readonly int Time;
    }
}