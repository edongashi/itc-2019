﻿using System;
using System.Collections.Generic;
using System.Linq;
using Timetabling.Common.SolutionModel;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel.Constraints.Internal
{
    public abstract class RoomConstraint : IConstraint
    {
        private const int CacheCapacity = 4096;

        protected readonly int[] Classes;

        protected readonly HashSet<int> ClassesSet;

        public readonly int Id;

        public readonly bool Required;

        public readonly int Penalty;

        public int Difficulty
        {
            get => difficulty;
            set
            {
                difficulty = value;
                cache.Clear();
            }
        }

        protected bool SuppressCaching = false;

        protected RoomConstraint(int id, bool required, int penalty, int[] classes)
        {
            Id = id;
            Required = required;
            Penalty = penalty;
            Classes = classes.ToArray();
            ClassesSet = new HashSet<int>(classes);
            buffer = new Room[Classes.Length];
            cache = new LruCache<CacheItem, (int hardPenalty, int softPenalty)>(CacheCapacity);
            lastResult = (-1, 0);
        }

        int IConstraint.Id => Id;

        ConstraintType IConstraint.Type => ConstraintType.Room;

        bool IConstraint.Required => Required;

        IEnumerable<int> IConstraint.Classes => Classes;

        public bool InvolvesClass(int @class)
        {
            return ClassesSet.Contains(@class);
        }

        private (int hardPenalty, int softPenalty) lastResult;

        private readonly Room[] buffer;
        private readonly LruCache<CacheItem, (int hardPenalty, int softPenalty)> cache;
        private int difficulty;

        private class CacheItem : IEquatable<CacheItem>
        {
            private readonly Room[] configuration;

            public CacheItem(Room[] configuration)
            {
                this.configuration = configuration;
            }

            public CacheItem Clone()
            {
                return new CacheItem((Room[])configuration.Clone());
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
                    if (!ReferenceEquals(configuration[i], otherconfig[i]))
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
                var hash = configuration[0].GetHashCode();
                for (var i = 1; i < configuration.Length; i++)
                {
                    hash = CombineHashCodes(hash, configuration[i].GetHashCode());
                }

                return hash;
            }

            private static int CombineHashCodes(int h1, int h2)
            {
                return ((h1 << 5) + h1) ^ h2;
            }
        }

        public (int hardPenalty, int softPenalty) Evaluate(ISolution s)
        {
            var equals = true;
            for (var i = 0; i < Classes.Length; i++)
            {
                var room = s.GetRoom(Classes[i]);
                if (!ReferenceEquals(room, buffer[i]))
                {
                    buffer[i] = room;
                    equals = false;
                }
            }

            if (equals && lastResult.hardPenalty >= 0)
            {
                return lastResult;
            }

            if (SuppressCaching)
            {
                var newResult = Evaluate(s.Problem, buffer);
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
                var newResult = Evaluate(s.Problem, buffer);
                newResult.hardPenalty += newResult.hardPenalty > 0 ? Difficulty : 0;
                cache.Add(cached.Clone(), newResult);
                lastResult = newResult;
                return newResult;
            }
        }

        public virtual IEnumerable<int> EvaluateConflictingClasses(ISolution solution)
        {
            return Classes;
        }

        protected abstract (int hardPenalty, int softPenalty) Evaluate(Problem problem, Room[] configuration);

        public Solution TryFix(Solution solution, Random random)
        {
            throw new NotImplementedException();
        }
    }
}