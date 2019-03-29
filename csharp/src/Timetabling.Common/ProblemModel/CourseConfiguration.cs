using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Common.ProblemModel
{
    public class CourseConfiguration
    {
        public CourseConfiguration(int id, int courseId, Subpart[] subparts)
        {
            Id = id;
            CourseId = courseId;
            Subparts = subparts;
            Baseline = FindBaseline(subparts);
        }

        public readonly int Id;

        public readonly int CourseId;

        public readonly Subpart[] Subparts;

        public readonly int[] Baseline;

        public IEnumerable<Class> Classes => Subparts.SelectMany(s => s.Classes);

        private static int[] FindBaseline(Subpart[] subparts)
        {
            var count = subparts.Length;
            if (count == 0)
            {
                return null;
            }

            var solution = new(int index, int classid)[count];

            bool Set(int index)
            {
                var classes = subparts[index].Classes;
                for (var i = 0; i < classes.Length; i++)
                {
                    var @class = classes[i];
                    if (@class.Capacity <= 0)
                    {
                        continue;
                    }

                    var classId = @class.Id;
                    if (@class.ParentId >= 0)
                    {
                        var found = false;
                        for (var j = index - 1; j >= 0; j--)
                        {
                            if (solution[j].classid == @class.ParentId)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            continue;
                        }
                    }

                    solution[index] = (i, classId);
                    if (index < count - 1)
                    {
                        if (Set(index + 1))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }

                return false;
            }

            if (!Set(0))
            {
                return null;
            }

            return solution
                .Select(t => t.index)
                .ToArray();
        }
    }
}