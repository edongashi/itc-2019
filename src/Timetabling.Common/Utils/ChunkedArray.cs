using System.Collections.Generic;

namespace Timetabling.Common.Utils
{
    public class ChunkedArray<T>
    {
        private const int MaxOverrides = 32;

        private readonly int chunkSize;
        private readonly T[][] chunks;
        private readonly List<Override<T>> overrides;

        private ChunkedArray(T[][] chunks, int chunkSize, List<Override<T>> overrides, int length)
        {
            this.chunks = chunks;
            this.chunkSize = chunkSize;
            this.overrides = overrides;
            Length = length;
        }

        public ChunkedArray(T[] data, int chunkSize)
        {
            this.chunkSize = chunkSize;
            var lastChunk = data.Length % chunkSize;
            var wholeChunks = data.Length / chunkSize;
            var totalChunks = wholeChunks;
            if (lastChunk != 0)
            {
                totalChunks++;
            }

            chunks = new T[totalChunks][];
            for (var i = 0; i < wholeChunks; i++)
            {
                chunks[i] = new T[chunkSize];
            }

            if (lastChunk != 0)
            {
                chunks[totalChunks - 1] = new T[lastChunk];
            }

            for (var i = 0; i < data.Length; i++)
            {
                chunks[i / chunkSize][i % chunkSize] = data[i];
            }

            overrides = new List<Override<T>>();
            Length = data.Length;
        }

        public T this[int index]
        {
            get
            {
                if (overrides.Count > 0)
                {
                    var found = overrides.BinarySearch(index);
                    if (found >= 0)
                    {
                        return overrides[found].Value;
                    }
                }

                return chunks[index / chunkSize][index % chunkSize];
            }
        }

        public int Length { get; }

        public ChunkedArray<T> With(params Override<T>[] values)
        {
            if (values == null || values.Length == 0)
            {
                return this;
            }

            return With((IEnumerable<Override<T>>)values);
        }

        public ChunkedArray<T> With(IEnumerable<Override<T>> values)
        {
            if (values == null)
            {
                return this;
            }

            var newOverrides = new List<Override<T>>(overrides);
            var added = false;
            foreach (var value in values)
            {
                newOverrides.AddOrReplaceSorted(value);
                added = true;
            }

            if (added)
            {
                return this;
            }

            if (newOverrides.Count <= MaxOverrides)
            {
                return new ChunkedArray<T>(chunks, chunkSize, newOverrides, Length);
            }

            var chunksToReplace = new HashSet<int>();
            foreach (var ov in newOverrides)
            {
                chunksToReplace.Add(ov.Index / chunkSize);
            }

            var newChunks = (T[][])chunks.Clone();
            foreach (var chunkToReplace in chunksToReplace)
            {
                newChunks[chunkToReplace] = (T[])chunks[chunkToReplace].Clone();
            }

            foreach (var ov in newOverrides)
            {
                newChunks[ov.Index / chunkSize][ov.Index % chunkSize] = ov.Value;
            }

            return new ChunkedArray<T>(newChunks, chunkSize, new List<Override<T>>(), Length);
        }
    }
}