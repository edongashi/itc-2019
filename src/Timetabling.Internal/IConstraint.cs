﻿using System.Collections.Generic;
using Timetabling.Internal.Specialized;

namespace Timetabling.Internal
{
    public interface IConstraint
    {
        int Id { get; }

        ConstraintType Type { get; }

        bool Required { get; }

        IEnumerable<int> Classes { get; }

        bool InvolvesClass(int @class);

        (int hardPenalty, int softPenalty) Evaluate(Problem problem, IClassStates s);
    }
}
