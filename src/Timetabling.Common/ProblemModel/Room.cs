namespace Timetabling.Common.ProblemModel
{
    public class Room
    {
        public Room(int id, int capacity, Schedule[] unavailableSchedules, TravelTime[] travelTimes)
        {
            Id = id;
            Capacity = capacity;
            UnavailableSchedules = unavailableSchedules;
            TravelTimes = travelTimes;
        }

        public int Id { get; }

        public int Capacity { get; }

        public Schedule[] UnavailableSchedules { get; }

        public TravelTime[] TravelTimes { get; }
    }
}