﻿namespace UglyToad.PdfPig.Fonts.TrueType.Tables
{
    using System.Collections.Generic;
    using System.IO;
    using IO;
    using Util;

    /// <summary>
    /// The 'hmtx' table contains metric information for the horizontal layout each of the glyphs in the font.
    /// </summary>
    internal class HorizontalMetricsTable : ITrueTypeTable, IWriteable
    {
        public string Tag => TrueTypeHeaderTable.Hmtx;

        public TrueTypeHeaderTable DirectoryTable { get; }

        public IReadOnlyList<HorizontalMetric> HorizontalMetrics { get; }

        /// <summary>
        /// Some fonts may have an array of left side bearings following the <see cref="HorizontalMetrics"/>. 
        /// Generally, this array of left side bearings is used for a run of monospaced glyphs. 
        /// For example, it might be used for a Kanji font or for Courier. 
        /// The corresponding glyphs are assumed to have the same advance width as that found in the last entry in the <see cref="HorizontalMetrics"/>.
        /// </summary>
        public IReadOnlyList<short> AdditionalLeftSideBearings { get; }

        public HorizontalMetricsTable(TrueTypeHeaderTable directoryTable, 
            IReadOnlyList<HorizontalMetric> horizontalMetrics,
            IReadOnlyList<short> additionalLeftSideBearings)
        {
            DirectoryTable = directoryTable;
            HorizontalMetrics = horizontalMetrics;
            AdditionalLeftSideBearings = additionalLeftSideBearings;
        }

        public ushort GetAdvanceWidth(int index)
        {
            if (index < HorizontalMetrics.Count)
            {
                return HorizontalMetrics[index].AdvanceWidth;
            }

            // Monospaced fonts may not have a width for every glyph, the last metric is for subsequent glyphs.
            return HorizontalMetrics[HorizontalMetrics.Count - 1].AdvanceWidth;
        }

        public void Write(Stream stream)
        {
            for (var i = 0; i < HorizontalMetrics.Count; i++)
            {
                var metric = HorizontalMetrics[i];
                stream.WriteUShort(metric.AdvanceWidth);
                stream.WriteShort(metric.LeftSideBearing);
            }

            for (var i = 0; i < AdditionalLeftSideBearings.Count; i++)
            {
                var lsb = AdditionalLeftSideBearings[i];
                stream.WriteShort(lsb);
            }
        }

        /// <summary>
        /// The pair of horizontal metrics for an individual glyph.
        /// </summary>
        public struct HorizontalMetric
        {
            /// <summary>
            /// The advance width.
            /// </summary>
            public ushort AdvanceWidth { get; }

            /// <summary>
            /// The left side bearing.
            /// </summary>
            public short LeftSideBearing { get; }
            
            internal HorizontalMetric(ushort advanceWidth, short leftSideBearing)
            {
                AdvanceWidth = advanceWidth;
                LeftSideBearing = leftSideBearing;
            }
        }
    }
}
