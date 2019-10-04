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
    }
}
