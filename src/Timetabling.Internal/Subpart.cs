namespace Timetabling.Internal
{
    public class Subpart
    {
        public Subpart(int id, Class[] classes)
        {
            Id = id;
            Classes = classes;
        }

        public readonly int Id;

        public readonly Class[] Classes;
    }
}
