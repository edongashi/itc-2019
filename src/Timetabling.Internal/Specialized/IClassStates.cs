namespace Timetabling.Internal.Specialized
{
    public interface IClassStates
    {
        ScheduleAssignment GetTime(int @class);

        Room GetRoom(int @class);
    }
}