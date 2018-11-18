using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Timetabling.Common.ProblemModel.Constraints;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel
{
    public static class ProblemParser
    {
        public static Problem FromXml(Stream stream)
        {
            var element = XDocument.Load(stream).Root;
            return ToProblem(element);
        }

        public static Problem ToProblem(XElement element)
        {
            var xoptimization = element.Element("optimization");
            return new Problem(
                element.RequiredAttribute("name"),
                element.RequiredInteger("nrWeeks"),
                element.RequiredInteger("nrDays"),
                element.RequiredInteger("slotsPerDay"),
                xoptimization.RequiredInteger("time"),
                xoptimization.RequiredInteger("room"),
                xoptimization.RequiredInteger("distribution"),
                xoptimization.RequiredInteger("student"),
                element
                    .Element("rooms")
                    .Elements("room")
                    .Select(ToRoom)
                    .ToArray(),
                element
                    .Element("courses")
                    .Elements("course")
                    .Select(ToCourse)
                    .ToArray(),
                element
                    .Element("students")
                    .Elements("student")
                    .Select(ToStudent)
                    .ToArray(),
                element
                    .Element("distributions")
                    .Elements("distribution")
                    .Select(ToConstraint)
                    .ToArray());
        }

        public static Room ToRoom(XElement element)
        {
            return new Room(
                element.RequiredId(),
                element.RequiredInteger("capacity"),
                element.Elements("unavailable").Select(ToSchedule).ToArray(),
                element.Elements("travel").Select(ToTravelTime).ToArray());
        }

        public static Schedule ToSchedule(XElement element)
        {
            return new Schedule(
                element.RequiredBinary("weeks"),
                element.RequiredBinary("days"),
                element.RequiredInteger("start"),
                element.RequiredInteger("length"));
        }

        public static TravelTime ToTravelTime(XElement element)
        {
            return new TravelTime(
                element.RequiredInteger("room") - 1,
                element.RequiredInteger("value"));
        }

        public static Course ToCourse(XElement element)
        {
            var id = element.RequiredId();
            return new Course(
                id,
                element
                    .Elements("config")
                    .Select(e => ToCourseConfiguration(e, id))
                    .ToArray());
        }

        public static CourseConfiguration ToCourseConfiguration(XElement element, int courseId)
        {
            return new CourseConfiguration(
                element.RequiredId(),
                courseId,
                element
                    .Elements("subpart")
                    .Select(e => ToSubpart(e, courseId))
                    .ToArray());
        }

        public static Subpart ToSubpart(XElement element, int courseId)
        {
            return new Subpart(
                element.RequiredId(),
                courseId,
                element
                    .Elements("class")
                    .Select(e => ToClass(e, courseId))
                    .ToArray());
        }

        public static Class ToClass(XElement element, int courseId)
        {
            return new Class(
                element.RequiredId(),
                element.OptionalInteger("parent", 0) - 1,
                courseId,
                element.OptionalInteger("limit", int.MaxValue),
                element
                    .Elements("room")
                    .Select(ToRoomAssignment)
                    .ToArray(),
                element
                    .Elements("time")
                    .Select(ToScheduleAssignment)
                    .ToArray());
        }

        public static RoomAssignment ToRoomAssignment(XElement element)
        {
            return new RoomAssignment(
                element.RequiredId(),
                element.RequiredInteger("penalty"));
        }

        public static ScheduleAssignment ToScheduleAssignment(XElement element)
        {
            return new ScheduleAssignment(
                element.RequiredBinary("weeks"),
                element.RequiredBinary("days"),
                element.RequiredInteger("start"),
                element.RequiredInteger("length"),
                element.RequiredInteger("penalty"));
        }

        public static Student ToStudent(XElement element)
        {
            return new Student(
                element.RequiredId(),
                element
                    .Elements("course")
                    .Select(e => e.RequiredId())
                    .ToArray());
        }

        public static IConstraint ToConstraint(XElement element)
        {
            var type = element.RequiredAttribute("type");
            var required = element.OptionalAttribute("required") == "true";
            var penalty = element.OptionalInteger("penalty", 0);
            var index = type.IndexOf("(");
            int[] args = new int[0];
            if (index != -1)
            {
                args = type
                    .Substring(index + 1)
                    .TrimLastChar()
                    .Split(',')
                    .Select(int.Parse)
                    .ToArray();
                type = type.Substring(0, index);

            }

            var classes = element
                .Elements("class")
                .Select(e => e.RequiredId())
                .ToArray();

            switch (type)
            {
                case nameof(SameStart):
                    return new SameStart(required, penalty, classes);
                case nameof(SameTime):
                    return new SameTime(required, penalty, classes);
                case nameof(SameDays):
                    return new SameDays(required, penalty, classes);
                case nameof(SameWeeks):
                    return new SameWeeks(required, penalty, classes);
                case nameof(SameRoom):
                    return new SameRoom(required, penalty, classes);
                case nameof(Overlap):
                    return new Overlap(required, penalty, classes);
                case nameof(SameAttendees):
                    return new SameAttendees(required, penalty, classes);
                case nameof(Precedence):
                    return new Precedence(required, penalty, classes);
                case nameof(WorkDay):
                    return new WorkDay(args[0], required, penalty, classes);
                case nameof(MinGap):
                    return new MinGap(args[0], required, penalty, classes);
                case nameof(MaxDays):
                    return new MaxDays(args[0], required, penalty, classes);
                case nameof(MaxDayLoad):
                    return new MaxDayLoad(args[0], required, penalty, classes);
                case nameof(MaxBreaks):
                    return new MaxBreaks(args[0], args[1], required, penalty, classes);
                case nameof(MaxBlock):
                    return new MaxBlock(args[0], args[1], required, penalty, classes);
                case nameof(DifferentTime):
                    return new DifferentTime(required, penalty, classes);
                case nameof(DifferentDays):
                    return new DifferentDays(required, penalty, classes);
                case nameof(DifferentWeeks):
                    return new DifferentWeeks(required, penalty, classes);
                case nameof(DifferentRoom):
                    return new DifferentRoom(required, penalty, classes);
                case nameof(NotOverlap):
                    return new NotOverlap(required, penalty, classes);
                default:
                    throw new InvalidOperationException("Invalid constraint type.");
            }
        }

        private static string TrimLastChar(this string str)
        {
            return str.Substring(0, str.Length - 1);
        }
    }
}
