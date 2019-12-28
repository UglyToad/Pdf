﻿namespace UglyToad.PdfPig.Tests.Writer.Fonts
{
    using System.Collections.Generic;
    using PdfPig.Fonts.Parser;
    using PdfPig.IO;
    using PdfPig.Util;
    using PdfPig.Writer.Fonts;
    using Xunit;

    public class ToUnicodeCMapBuilderTests
    {
        [Fact]
        public void WritesValidCMap()
        {
            var mappings = new Dictionary<char, byte>
            {
                {'1', 1},
                {'=', 2},
                {'H', 7},
                {'a', 12},
                {'2', 25}
            };

            var cmapStream = ToUnicodeCMapBuilder.ConvertToCMapStream(mappings);

            var str = OtherEncodings.BytesAsLatin1String(cmapStream);

            Assert.NotNull(str);

            var result = new CMapParser().Parse(new ByteArrayInputBytes(cmapStream), false);

            Assert.Equal(1, result.CodespaceRanges.Count);

            var range = result.CodespaceRanges[0];

            Assert.Equal(1, range.CodeLength);
            Assert.Equal(0, range.StartInt);
            Assert.Equal(byte.MaxValue, range.EndInt);

            Assert.Equal(mappings.Count, result.BaseFontCharacterMap.Count);

            foreach (var keyValuePair in result.BaseFontCharacterMap)
            {
                var match = mappings[keyValuePair.Value[0]];

                Assert.Equal(match, keyValuePair.Key);
            }
        }
    }
}
