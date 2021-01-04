﻿namespace UglyToad.PdfPig.Writer
{
    using Core;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Merges PDF documents into each other.
    /// </summary>
    public static class PdfMerger
    {
        /// <summary>
        /// Merge two PDF documents together with the pages from <paramref name="file1"/> followed by <paramref name="file2"/>.
        /// </summary>
        public static byte[] Merge(string file1, string file2)
        {
            using (var output = new MemoryStream())
            {
                Merge(file1, file2, output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// Merge two PDF documents together with the pages from <paramref name="file1"/> followed by <paramref name="file2"/> into the output stream.
        /// </summary>
        public static void Merge(string file1, string file2, Stream output)
        {
            _ = file1 ?? throw new ArgumentNullException(nameof(file1));
            _ = file2 ?? throw new ArgumentNullException(nameof(file2));

            using (var stream1 = new StreamInputBytes(File.OpenRead(file1)))
            {
                using (var stream2 = new StreamInputBytes(File.OpenRead(file2)))
                {
                    PdfRearranger.Rearrange(new[] { stream1, stream2 }, PdfMerge.Instance, output);
                }
            }
        }

        /// <summary>
        /// Merge multiple PDF documents together with the pages in the order the file paths are provided.
        /// </summary>
        public static byte[] Merge(params string[] filePaths)
        {
            using (var output = new MemoryStream())
            {
                Merge(output, filePaths);
                return output.ToArray();
            }
        }

        /// <summary>
        /// Merge multiple PDF documents together with the pages in the order the file paths are provided into the output stream
        /// </summary>
        public static void Merge(Stream output, params string[] filePaths)
        {
            var streams = new List<StreamInputBytes>(filePaths.Length);
            try
            {
                for (var i = 0; i < filePaths.Length; i++)
                {
                    var filePath = filePaths[i] ?? throw new ArgumentNullException(nameof(filePaths), $"Null filepath at index {i}.");
                    streams.Add(new StreamInputBytes(File.OpenRead(filePath), true));
                }

                PdfRearranger.Rearrange(streams, PdfMerge.Instance, output);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream.Dispose();
                }
            }
        }

        /// <summary>
        /// Merge the set of PDF documents.
        /// </summary>
        public static byte[] Merge(IReadOnlyList<byte[]> files)
        {
            _ = files ?? throw new ArgumentNullException(nameof(files));

            using (var output = new MemoryStream())
            {
                PdfRearranger.Rearrange(files.Select(f => new ByteArrayInputBytes(f)).ToArray(), PdfMerge.Instance, output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// Merge the set of PDF documents into the output stream
        /// The caller must manage disposing the stream. The created PdfDocument will not dispose the stream.
        /// <param name="streams">
        /// A list of streams for the file contents, this must support reading and seeking.
        /// </param>
        /// <param name="output">Must be writable</param>
        /// </summary>
        public static void Merge(IReadOnlyList<Stream> streams, Stream output)
        {
            _ = streams ?? throw new ArgumentNullException(nameof(streams));
            _ = output ?? throw new ArgumentNullException(nameof(output));

            PdfRearranger.Rearrange(streams.Select(f => new StreamInputBytes(f, false)).ToArray(), PdfMerge.Instance, output);
        }

        class PdfMerge : IPdfArrangement
        {
            public static readonly PdfMerge Instance = new PdfMerge();
            private PdfMerge() { }

            public IEnumerable<(int FileIndex, IReadOnlyCollection<int> PageIndices)> GetArrangements(Dictionary<int, int> pagesCountPerFileIndex)
                => pagesCountPerFileIndex
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => (kvp.Key, (IReadOnlyCollection<int>)Enumerable.Range(1, kvp.Value).ToArray()));
        }
    }
}