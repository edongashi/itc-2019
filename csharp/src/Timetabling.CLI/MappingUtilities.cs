using System.Collections.Generic;
using System.Xml.Linq;
using Timetabling.Common.Utils;

namespace Timetabling.CLI
{
    public static class MappingUtilities
    {
        public static (XElement, Mapping) MapForward(XElement root)
        {
            var instance = new XElement(root);

            var mapping = new Mapping();
            var roomPointer = 0;
            var classPointer = 0;
            var subpartPointer = 0;
            var configPointer = 0;
            var coursePointer = 0;
            var studentPointer = 0;

            foreach (var room in instance.Element("rooms").Elements("room"))
            {
                var roomId = room.RequiredInteger("id");
                mapping.ForwardRooms[roomId] = ++roomPointer;
                room.SetAttributeValue("id", roomPointer);
            }

            foreach (var course in instance.Element("courses").Elements("course"))
            {
                var courseId = course.RequiredInteger("id");
                mapping.ForwardCourses[courseId] = ++coursePointer;
                course.SetAttributeValue("id", coursePointer);
                foreach (var config in course.Elements("config"))
                {
                    var configId = config.RequiredInteger("id");
                    mapping.ForwardConfigurations[configId] = ++configPointer;
                    config.SetAttributeValue("id", configPointer);
                    foreach (var subpart in config.Elements("subpart"))
                    {
                        var subpartId = subpart.RequiredInteger("id");
                        mapping.ForwardSubparts[subpartId] = ++subpartPointer;
                        subpart.SetAttributeValue("id", subpartPointer);
                        foreach (var @class in subpart.Elements("class"))
                        {
                            var classId = @class.RequiredInteger("id");
                            mapping.ForwardClasses[classId] = ++classPointer;
                            @class.SetAttributeValue("id", classPointer);
                            foreach (var room in @class.Elements("room"))
                            {
                                var roomId = room.RequiredInteger("id");
                                room.SetAttributeValue("id", mapping.ForwardRooms[roomId]);
                            }
                        }
                    }
                }
            }

            foreach (var distribution in instance.Element("distributions").Elements("distribution"))
            {
                foreach (var @class in distribution.Elements("class"))
                {
                    var classId = @class.RequiredInteger("id");
                    @class.SetAttributeValue("id", mapping.ForwardClasses[classId]);
                }
            }

            foreach (var student in instance.Element("students").Elements("student"))
            {
                var studentId = student.RequiredInteger("id");
                mapping.ForwardStudents[studentId] = ++studentPointer;
                student.SetAttributeValue("id", studentPointer);
                foreach (var course in student.Elements("course"))
                {
                    var courseId = course.RequiredInteger("id");
                    course.SetAttributeValue("id", mapping.ForwardCourses[courseId]);
                }
            }

            foreach (var @class in instance.Element("courses").Descendants("class"))
            {
                var parent = @class.OptionalInteger("parent", -1);
                if (parent != -1)
                {
                    @class.SetAttributeValue("parent", mapping.ForwardClasses[parent]);
                }
            }

            foreach (var travel in instance.Element("rooms").Descendants("travel"))
            {
                var roomId = travel.RequiredInteger("room");
                travel.SetAttributeValue("room", mapping.ForwardRooms[roomId]);
            }

            mapping.AssignReverses();
            return (instance, mapping);
        }

        public static XElement MapBackwards(XElement root, Mapping mapping)
        {
            var solution = new XElement(root);
            foreach (var @class in solution.Elements("class"))
            {
                var classId = @class.RequiredInteger("id");
                var classRoom = @class.OptionalInteger("room", -1);
                @class.SetAttributeValue("id", mapping.BackwardClasses[classId]);
                if (classRoom != -1)
                {
                    @class.SetAttributeValue("room", mapping.BackwardRooms[classRoom]);
                }

                foreach (var student in @class.Elements("student"))
                {
                    var studentId = student.RequiredInteger("id");
                    student.SetAttributeValue("id", mapping.BackwardStudents[studentId]);
                }
            }

            return solution;
        }
    }

    public class Mapping
    {
        public static Dictionary<TValue, TKey> Reverse<TKey, TValue>(IDictionary<TKey, TValue> source)
        {
            var dictionary = new Dictionary<TValue, TKey>();
            foreach (var entry in source)
            {
                if (!dictionary.ContainsKey(entry.Value))
                {
                    dictionary.Add(entry.Value, entry.Key);
                }
            }

            return dictionary;
        }

        public Mapping()
        {
        }

        public Mapping(
            Dictionary<int, int> forwardRooms,
            Dictionary<int, int> forwardClasses,
            Dictionary<int, int> forwardSubparts,
            Dictionary<int, int> forwardConfigurations,
            Dictionary<int, int> forwardCourses,
            Dictionary<int, int> forwardStudents)
        {
            ForwardRooms = forwardRooms;
            ForwardClasses = forwardClasses;
            ForwardSubparts = forwardSubparts;
            ForwardConfigurations = forwardConfigurations;
            ForwardCourses = forwardCourses;
            ForwardStudents = forwardStudents;
            AssignReverses();
        }

        public void AssignReverses()
        {
            BackwardRooms = Reverse(ForwardRooms);
            BackwardClasses = Reverse(ForwardClasses);
            BackwardSubparts = Reverse(ForwardSubparts);
            BackwardConfigurations = Reverse(ForwardConfigurations);
            BackwardCourses = Reverse(ForwardCourses);
            BackwardStudents = Reverse(ForwardStudents);
        }

        public Dictionary<int, int> ForwardRooms { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> ForwardClasses { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> ForwardSubparts { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> ForwardConfigurations { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> ForwardCourses { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> ForwardStudents { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> BackwardRooms { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> BackwardClasses { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> BackwardSubparts { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> BackwardConfigurations { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> BackwardCourses { get; set; } = new Dictionary<int, int>();

        public Dictionary<int, int> BackwardStudents { get; set; } = new Dictionary<int, int>();
    }
}
