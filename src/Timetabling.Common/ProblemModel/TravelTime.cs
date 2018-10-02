namespace Timetabling.Common.ProblemModel
{
    public struct TravelTime
    {
        public TravelTime(int roomId, int value)
        {
            RoomId = roomId;
            Value = value;
        }

        public int RoomId { get; }

        public int Value { get; }
    }
}