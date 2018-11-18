﻿using System.Collections.Generic;
using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.SolutionModel;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class MaxBreaks : ConstraintBase
    {
        public MaxBreaks(int r, int s, bool required, int penalty, int[] classes)
            : base(required, penalty, classes)
        {
            R = r;
            S = s;
        }

        public readonly int R;

        public readonly int S;

        public override ConstraintType Type => ConstraintType.Time;

        public override (double hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var problem = s.Problem;
            var nrWeeks = problem.NumberOfWeeks;
            var nrDays = problem.DaysPerWeek;
            var totalOverflows = 0;
            var blocks = new List<Block>();
            var mergedBlocks = new List<Block>();
            for (var w = 0; w < nrWeeks; w++)
            {
                for (var d = 0; d < nrDays; d++)
                {
                    blocks.Clear();
                    mergedBlocks.Clear();

                    // ReSharper disable once ForCanBeConvertedToForeach
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    for (var i = 0; i < Classes.Length; i++)
                    {
                        var ci = s.GetTime(Classes[i]);
                        if ((ci.Days & (1u << d)) == 0u
                            || (ci.Weeks & (1u << w)) == 0u)
                        {
                            continue;
                        }

                        blocks.AddSorted(new Block(ci.Start, ci.End));
                    }

                    var count = blocks.Count;
                    if (count == 0)
                    {
                        continue;
                    }

                    mergedBlocks.Add(blocks[0]);
                    for (var i = 1; i < count; i++)
                    {
                        var top = mergedBlocks.Peek();
                        var current = blocks[i];
                        var end = top.End + S;
                        if (end < current.Start)
                        {
                            // No overlap.
                            mergedBlocks.Add(current);
                        }
                        // Overlap
                        else if (end < current.End)
                        {
                            // We need to expand range.
                            mergedBlocks[count - 1] = new Block(top.Start, current.End);
                        }
                    }

                    if (mergedBlocks.Count > R + 1)
                    {
                        totalOverflows += mergedBlocks.Count - R - 1;
                    }
                }
            }

            return Required
                ? ((double)totalOverflows / nrWeeks, Penalty * totalOverflows / nrWeeks)
                : (0d, Penalty * totalOverflows / nrWeeks);
        }
    }
}