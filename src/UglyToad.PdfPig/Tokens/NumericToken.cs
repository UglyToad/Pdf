﻿namespace UglyToad.PdfPig.Tokens
{
    using System.Globalization;

    /// <inheritdoc />
    /// <summary>
    /// PDF supports integer and real numbers. Integer objects represent mathematical integers within a certain interval centered at 0. 
    /// Real objects  approximate mathematical real numbers, but with limited range and precision.
    /// This token represents both types and they are used interchangeably in the specification.
    /// </summary>
    public class NumericToken : IDataToken<decimal>
    {
        /// <inheritdoc />
        public decimal Data { get; }

        /// <summary>
        /// Whether the number represented has a non-zero decimal part.
        /// </summary>
        public bool HasDecimalPlaces => decimal.Floor(Data) != Data;

        /// <summary>
        /// The value of this number as an <see langword="int"/>.
        /// </summary>
        public int Int => (int) Data;
        
        /// <summary>
        /// The value of this number as a <see langword="long"/>.
        /// </summary>
        public long Long => (long) Data;

        /// <summary>
        /// The value of this number as a <see langword="double"/>.
        /// </summary>
        public double Double => (double) Data;

        /// <summary>
        /// Create a <see cref="NumericToken"/>.
        /// </summary>
        /// <param name="value">The number to represent.</param>
        public NumericToken(decimal value)
        {
            Data = value;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Data.ToString(NumberFormatInfo.InvariantInfo);
        }
    }
}