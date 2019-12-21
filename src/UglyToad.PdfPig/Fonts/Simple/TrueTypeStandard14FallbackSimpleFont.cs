﻿namespace UglyToad.PdfPig.Fonts.Simple
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Encodings;
    using IO;
    using Tokens;
    using TrueType;

    /// <summary>
    /// Some TrueType fonts use both the Standard 14 descriptor and the TrueType font from disk.
    /// </summary>
    internal class TrueTypeStandard14FallbackSimpleFont : IFont
    {
        private static readonly TransformationMatrix DefaultTransformation =
            TransformationMatrix.FromValues(1 / 1000.0, 0, 0, 1 / 1000.0, 0, 0);

        private readonly FontMetrics fontMetrics;
        private readonly Encoding encoding;
        private readonly TrueTypeFontProgram font;
        private readonly MetricOverrides overrides;

        public NameToken Name { get; }

        public bool IsVertical { get; } = false;

        public TrueTypeStandard14FallbackSimpleFont(NameToken name, FontMetrics fontMetrics, Encoding encoding, TrueTypeFontProgram font,
            MetricOverrides overrides)
        {
            this.fontMetrics = fontMetrics;
            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            this.font = font;
            this.overrides = overrides;
            Name = name;
        }

        public int ReadCharacterCode(IInputBytes bytes, out int codeLength)
        {
            codeLength = 1;
            return bytes.CurrentByte;
        }

        public bool TryGetUnicode(int characterCode, out string value)
        {
            value = null;

            // If the font is a simple font that uses one of the predefined encodings MacRomanEncoding, MacExpertEncoding, or WinAnsiEncoding...

            //  Map the character code to a character name.
            var encodedCharacterName = encoding.GetName(characterCode);

            // Look up the character name in the Adobe Glyph List.
            try
            {
                value = GlyphList.AdobeGlyphList.NameToUnicode(encodedCharacterName);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public CharacterBoundingBox GetBoundingBox(int characterCode)
        {
            var width = 0.0;

            var fontMatrix = GetFontMatrix();

            if (font != null && font.TryGetBoundingBox(characterCode, out var bounds))
            {
                bounds = fontMatrix.Transform(bounds);

                if (overrides?.TryGetWidth(characterCode, out width) != true)
                {
                    width = bounds.Width;
                }
                else
                {
                    width = DefaultTransformation.TransformX(width);
                }

                return new CharacterBoundingBox(bounds, width);
            }

            var name = encoding.GetName(characterCode);
            var metrics = fontMetrics.CharacterMetrics[name];

            if (overrides?.TryGetWidth(characterCode, out width) != true)
            {
                width = fontMatrix.TransformX(metrics.WidthX);
            }
            else
            {
                width = DefaultTransformation.TransformX(width);
            }

            bounds = fontMatrix.Transform(metrics.BoundingBox);

            return new CharacterBoundingBox(bounds, width);
        }

        public TransformationMatrix GetFontMatrix()
        {
            if (font?.TableRegister.HeaderTable != null)
            {
                var scale = (double)font.GetFontMatrixMultiplier();

                return TransformationMatrix.FromValues(1 / scale, 0, 0, 1 / scale, 0, 0);
            }

            return DefaultTransformation;
        }

        public class MetricOverrides
        {
            public int? FirstCharacterCode { get; }

            public IReadOnlyList<double> Widths { get; }

            public bool HasOverriddenMetrics { get; }

            public MetricOverrides(int? firstCharacterCode, IReadOnlyList<double> widths)
            {
                FirstCharacterCode = firstCharacterCode;
                Widths = widths;
                HasOverriddenMetrics = FirstCharacterCode.HasValue && Widths != null
                    && Widths.Count > 0;
            }

            public bool TryGetWidth(int characterCode, out double width)
            {
                width = 0;

                if (!HasOverriddenMetrics || !FirstCharacterCode.HasValue)
                {
                    return false;
                }

                var index = characterCode - FirstCharacterCode.Value;

                if (index < 0 || index >= Widths.Count)
                {
                    return false;
                }

                width = Widths[index];

                return true;
            }
        }
    }
}