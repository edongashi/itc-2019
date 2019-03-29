using System;
using System.Collections.Generic;
using Timetabling.Common.ProblemModel.Constraints.Internal;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel.Constraints
{
    public class MaxBlock : TimeConstraint
    {
        public MaxBlock(int id, int m, int s, bool required, int penalty, int[] classes)
            : base(id, required, penalty, classes)
        {
            M = m;
            S = s;
        }

        public readonly int M;

        public readonly int S;

        protected override (int hardPenalty, int softPenalty) Evaluate(Problem problem, Schedule[] configuration)
        {
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
                        var ci = configuration[i];
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
                    var merged = false;
                    for (var i = 1; i < count; i++)
                    {
                        var top = mergedBlocks.Peek();
                        var current = blocks[i];
                        var topEnd = top.End + S;
                        if (topEnd < current.Start)
                        {
                            // No overlap.
                            if (merged)
                            {
                                mergedBlocks.Add(current);
                                merged = false;
                            }
                            else
                            {
                                mergedBlocks[mergedBlocks.Count - 1] = current;
                            }
                        }
                        // Overlap
                        else
                        {
                            // We need to expand range.
                            mergedBlocks[mergedBlocks.Count - 1] = new Block(top.Start, Math.Max(top.End, current.End));
                            merged = true;
                        }
                    }

                    var mergedCount = mergedBlocks.Count;
                    if (!merged)
                    {
                        mergedCount--;
                    }

                    for (var i = 0; i < mergedCount; i++)
                    {
                        if (mergedBlocks[i].Length > M)
                        {
                            totalOverflows++;
                        }
                    }
                }
            }

            return Required
                ? (totalOverflows > 0 ? Math.Max(1, totalOverflows / nrWeeks) : 0, 0)
                : (0, Penalty * totalOverflows / nrWeeks);
        }
    }
}
