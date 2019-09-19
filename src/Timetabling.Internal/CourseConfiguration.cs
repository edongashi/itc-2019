using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Internal
{
    public class CourseConfiguration
    {
        public CourseConfiguration(int id, Subpart[] subparts)
        {
            Id = id;
            Subparts = subparts;
            Baseline = FindBaseline(subparts);
            ClassChains = FindChains(subparts);
        }

        public readonly int Id;

        public readonly Subpart[] Subparts;

        internal readonly int[] Baseline;

        internal readonly int[][] ClassChains;

        internal IEnumerable<Class> Classes => Subparts.SelectMany(s => s.Classes);

        private static int[][] FindChains(Subpart[] subparts)
        {
            var count = subparts.Length;
            if (count == 0)
            {
                return new int[0][];
            }

            List<List<(int index, int classId)>> chains = new List<List<(int index, int classId)>>();

            void Chain(int level, List<(int index, int classId)> current)
            {
                if (level == count)
                {
                    chains.Add(current);
                    return;
                }

                var classes = subparts[level].Classes;
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
                        for (var j = level - 1; j >= 0; j--)
                        {
                            if (current[j].classId == @class.ParentId)
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

                    var clone = current.ToList();
                    clone.Add((i, classId));
                    Chain(level + 1, clone);
                }
            }

            Chain(0, new List<(int index, int classId)>());
            return chains
                .Select(outer => outer.Select(inner => inner.index).ToArray())
                .ToArray();
        }

        private static int[] FindBaseline(Subpart[] subparts)
        {
            var count = subparts.Length;
            if (count == 0)
            {
                return null;
            }

            var solution = new (int index, int classid)[count];

            bool Set(int level)
            {
                var classes = subparts[level].Classes;
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
                        for (var j = level - 1; j >= 0; j--)
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

                    solution[level] = (i, classId);
                    if (level < count - 1)
                    {
                        if (Set(level + 1))
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
