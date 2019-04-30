using System;
using System.Collections.Generic;
using System.Linq;
using Timetabling.Internal.Utils;

namespace Timetabling.Internal.Specialized
{
    public abstract class CommonConstraint : IConstraint
    {
        private const int CacheCapacity = 4096;

        protected readonly int[] Classes;

        protected readonly HashSet<int> ClassesSet;

        internal readonly int Id;

        internal readonly bool Required;

        internal readonly int Penalty;

        internal int Difficulty
        {
            get => difficulty;
            set
            {
                difficulty = value;
                cache.Clear();
            }
        }

        protected bool SuppressCaching = false;

        protected CommonConstraint(int id, bool required, int penalty, int[] classes)
        {
            Id = id;
            Required = required;
            Penalty = penalty;
            Classes = classes.ToArray();
            ClassesSet = new HashSet<int>(classes);
            buffer = new (Room room, Schedule schedule)[Classes.Length];
            cache = new LruCache<CacheItem, (int hardPenalty, int softPenalty)>(CacheCapacity);
            lastResult = (-1, 0);

            var n = ClassesSet.Count;
            var pairs = n * (n - 1) / 2;
            WorstCase = Math.Max(1, required ? pairs : penalty * pairs);
        }

        public int WorstCase { get; }

        int IConstraint.Id => Id;

        ConstraintType IConstraint.Type => ConstraintType.Common;

        bool IConstraint.Required => Required;

        IEnumerable<int> IConstraint.Classes => Classes;

        public bool InvolvesClass(int @class)
        {
            return ClassesSet.Contains(@class);
        }

        private (int hardPenalty, int softPenalty) lastResult;

        private readonly (Room room, Schedule schedule)[] buffer;
        private readonly LruCache<CacheItem, (int hardPenalty, int softPenalty)> cache;
        private int difficulty;

        private class CacheItem : IEquatable<CacheItem>
        {
            private readonly (Room room, Schedule schedule)[] configuration;

            internal CacheItem((Room room, Schedule schedule)[] configuration)
            {
                this.configuration = configuration;
            }

            internal CacheItem Clone()
            {
                return new CacheItem(((Room room, Schedule schedule)[])configuration.Clone());
            }

            public bool Equals(CacheItem other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                var otherconfig = other.configuration;
                for (var i = 0; i < configuration.Length; i++)
                {
                    var (a1, b1) = configuration[i];
                    var (a2, b2) = otherconfig[i];
                    if (!ReferenceEquals(a1, a2) || !ReferenceEquals(b1, b2))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj.GetType() != GetType())
                {
                    return false;
                }

                return Equals((CacheItem)obj);
            }

            public override int GetHashCode()
            {
                var (r0, s0) = configuration[0];
                var hash = s0.GetHashCode();
                hash = CombineHashCodes(hash, r0?.GetHashCode() ?? 0);
                for (var i = 1; i < configuration.Length; i++)
                {
                    var (r, s) = configuration[i];
                    hash = CombineHashCodes(hash, s.GetHashCode());
                    hash = CombineHashCodes(hash, r?.GetHashCode() ?? 0);
                }

                return hash;
            }

            private static int CombineHashCodes(int h1, int h2)
            {
                return ((h1 << 5) + h1) ^ h2;
            }
        }

        public (int hardPenalty, int softPenalty) Evaluate(Problem problem, IClassStates s)
        {
            lock (cache)
            {
                var equals = true;
                for (var i = 0; i < Classes.Length; i++)
                {
                    var @class = Classes[i];
                    var room = s.GetRoom(@class);
                    var schedule = s.GetTime(@class);
                    var (currentRoom, currentSchedule) = buffer[i];
                    if (!ReferenceEquals(room, currentRoom) || !ReferenceEquals(schedule, currentSchedule))
                    {
                        buffer[i] = (room, schedule);
                        equals = false;
                    }
                }

                if (equals && lastResult.hardPenalty >= 0)
                {
                    return lastResult;
                }

                if (SuppressCaching)
                {
                    var newResult = Evaluate(problem, buffer);
                    newResult.hardPenalty += newResult.hardPenalty > 0 ? Difficulty : 0;
                    lastResult = newResult;
                    return newResult;
                }

                var cached = new CacheItem(buffer);
                if (cache.TryGet(cached, out var result))
                {
                    lastResult = result;
                    return result;
                }
                else
                {
                    var newResult = Evaluate(problem, buffer);
                    newResult.hardPenalty += newResult.hardPenalty > 0 ? Difficulty : 0;
                    cache.Add(cached.Clone(), newResult);
                    lastResult = newResult;
                    return newResult;
                }
            }
        }

        public virtual IEnumerable<int> EvaluateConflictingClasses(Problem problem, IClassStates solution)
        {
            return Classes;
        }

        protected abstract (int hardPenalty, int softPenalty) Evaluate(Problem problem, (Room room, Schedule schedule)[] configuration);
    }
}
