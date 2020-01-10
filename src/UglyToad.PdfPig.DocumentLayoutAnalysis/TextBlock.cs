﻿namespace UglyToad.PdfPig.DocumentLayoutAnalysis
{
    using Content;
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A block of text.
    /// </summary>
    public class TextBlock
    {
        /// <summary>
        /// The text of the block.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// The text direction of the block.
        /// </summary>
        public TextDirection TextDirection { get; }

        /// <summary>
        /// The rectangle completely containing the block.
        /// </summary>
        public PdfRectangle BoundingBox { get; }

        /// <summary>
        /// The text lines contained in the block.
        /// </summary>
        public IReadOnlyList<TextLine> TextLines { get; }

        /// <summary>
        /// The reading order index. Starts at 0. A value of -1 means the block is not ordered.
        /// </summary>
        public int ReadingOrder { get; private set; }

        /// <summary>
        /// Create a new <see cref="TextBlock"/>.
        /// </summary>
        /// <param name="lines"></param>
        public TextBlock(IReadOnlyList<TextLine> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (lines.Count == 0)
            {
                throw new ArgumentException("Empty lines provided.", nameof(lines));
            }

            ReadingOrder = -1;

            TextLines = lines;

            Text = string.Join(" ", lines.Select(x => x.Text));

            var minX = lines.Min(x => x.BoundingBox.Left);
            var minY = lines.Min(x => x.BoundingBox.Bottom);
            var maxX = lines.Max(x => x.BoundingBox.Right);
            var maxY = lines.Max(x => x.BoundingBox.Top);
            BoundingBox = new PdfRectangle(minX, minY, maxX, maxY);

            TextDirection = lines[0].TextDirection;
        }

        internal void SetReadingOrder(int readingOrder)
        {
            if (readingOrder < -1)
            {
                throw new ArgumentException("The reading order should be more or equal to -1. A value of -1 means the block is not ordered.", nameof(readingOrder));
            }
            this.ReadingOrder = readingOrder;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Text;
        }
    }
}
