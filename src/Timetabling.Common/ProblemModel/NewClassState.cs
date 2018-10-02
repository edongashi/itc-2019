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

        public int Class { get; }

        public int Room { get; }

        public int Time { get; }
    }
}