﻿namespace UglyToad.PdfPig.Writer.Fonts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Core;
    using Filters;
    using Geometry;
    using IO;
    using Logging;
    using Tokens;
    using PdfPig.Fonts;
    using PdfPig.Fonts.Exceptions;
    using PdfPig.Fonts.TrueType;
    using PdfPig.Fonts.TrueType.Tables;

    internal class TrueTypeWritingFont : IWritingFont
    {
        private readonly TrueTypeFontProgram font;
        private readonly IReadOnlyList<byte> fontFileBytes;

        private readonly object mappingLock = new object();
        private readonly Dictionary<char, byte> characterMapping = new Dictionary<char, byte>();
        private int characterMappingCounter = 1;

        public bool HasWidths { get; } = true;

        public string Name => font.Name;

        public TrueTypeWritingFont(TrueTypeFontProgram font, IReadOnlyList<byte> fontFileBytes)
        {
            this.font = font;
            this.fontFileBytes = fontFileBytes;
        }

        public bool TryGetBoundingBox(char character, out PdfRectangle boundingBox)
        {
            return font.TryGetBoundingBox(character, out boundingBox);
        }

        public bool TryGetAdvanceWidth(char character, out double width)
        {
            return font.TryGetBoundingAdvancedWidth(character, out width);
        }

        public TransformationMatrix GetFontMatrix()
        {
            var unitsPerEm = font.GetFontMatrixMultiplier();
            return TransformationMatrix.FromValues(1.0 / unitsPerEm, 0, 0, 1.0 / unitsPerEm, 0, 0);
        }

        public ObjectToken WriteFont(NameToken fontKeyName, Stream outputStream, BuilderContext context)
        {
            var b = TrueTypeCMapReplacer.ReplaceCMapTables(font, new ByteArrayInputBytes(fontFileBytes), characterMapping);

            // A symbolic font (one which contains characters not in the standard latin set) -
            // should contain a MacRoman (1, 0) or Windows Symbolic (3,0) cmap subtable which maps character codes to glyph id.
            var bytes = CompressBytes(b);
            var embeddedFile = new StreamToken(new DictionaryToken(new Dictionary<NameToken, IToken>
            {
                { NameToken.Length, new NumericToken(bytes.Length) },
                { NameToken.Length1, new NumericToken(b.Length) },
                { NameToken.Filter, new ArrayToken(new []{ NameToken.FlateDecode }) }
            }), bytes);

            var fileRef = context.WriteObject(outputStream, embeddedFile);

            var baseFont = NameToken.Create(font.TableRegister.NameTable.GetPostscriptName());

            var postscript = font.TableRegister.PostScriptTable;
            var hhead = font.TableRegister.HorizontalHeaderTable;

            var bbox = font.TableRegister.HeaderTable.Bounds;

            var scaling = 1000m / font.TableRegister.HeaderTable.UnitsPerEm;
            var descriptorDictionary = new Dictionary<NameToken, IToken>
            {
                { NameToken.Type, NameToken.FontDescriptor },
                { NameToken.FontName, baseFont },
                // TODO: get flags TrueTypeEmbedder.java
                { NameToken.Flags, new NumericToken((int)FontDescriptorFlags.Symbolic) },
                { NameToken.FontBbox, GetBoundingBox(bbox, scaling) },
                { NameToken.ItalicAngle, new NumericToken(postscript.ItalicAngle) },
                { NameToken.Ascent, new NumericToken(hhead.Ascent * scaling) },
                { NameToken.Descent, new NumericToken(hhead.Descent * scaling) },
                { NameToken.CapHeight, new NumericToken(90) },
                { NameToken.StemV, new NumericToken(90) },
                { NameToken.FontFile2, new IndirectReferenceToken(fileRef.Number) }
            };

            var os2 = font.TableRegister.Os2Table;
            if (os2 == null)
            {
                throw new InvalidFontFormatException("Embedding TrueType font requires OS/2 table.");
            }

            if (os2 is Os2Version2To4OpenTypeTable twoPlus)
            {
                descriptorDictionary[NameToken.CapHeight] = new NumericToken(twoPlus.CapHeight);
                descriptorDictionary[NameToken.Xheight] = new NumericToken(twoPlus.XHeight);
            }

            descriptorDictionary[NameToken.StemV] = new NumericToken(((decimal)bbox.Width) * scaling * 0.13m);

            var lastCharacter = 0;
            var widths = new List<NumericToken> { NumericToken.Zero };
            foreach (var kvp in characterMapping)
            {
                if (kvp.Value > lastCharacter)
                {
                    lastCharacter = kvp.Value;
                }

                var glyphId = font.WindowsUnicodeCMap.CharacterCodeToGlyphIndex(kvp.Key);
                var width = decimal.Round(font.TableRegister.HorizontalMetricsTable.GetAdvanceWidth(glyphId) * scaling, 2);

                widths.Add(new NumericToken(width));
            }

            var descriptor = context.WriteObject(outputStream, new DictionaryToken(descriptorDictionary));

            var toUnicodeCMap = ToUnicodeCMapBuilder.ConvertToCMapStream(characterMapping);
            var compressedToUnicodeCMap = CompressBytes(toUnicodeCMap);
            var toUnicode = context.WriteObject(outputStream, new StreamToken(new DictionaryToken(new Dictionary<NameToken, IToken>()
            {
                {NameToken.Length, new NumericToken(compressedToUnicodeCMap.Length)},
                {NameToken.Length1, new NumericToken(toUnicodeCMap.Count)},
                {NameToken.Filter, new ArrayToken(new[] {NameToken.FlateDecode})}
            }), compressedToUnicodeCMap));

            var dictionary = new Dictionary<NameToken, IToken>
            {
                { NameToken.Type, NameToken.Font },
                { NameToken.Subtype, NameToken.TrueType },
                { NameToken.BaseFont, baseFont },
                { NameToken.FontDescriptor, new IndirectReferenceToken(descriptor.Number) },
                { NameToken.FirstChar, new NumericToken(0) },
                { NameToken.LastChar, new NumericToken(lastCharacter) },
                { NameToken.Widths, new ArrayToken(widths) },
                {NameToken.ToUnicode, new IndirectReferenceToken(toUnicode.Number) }
            };

            var token = new DictionaryToken(dictionary);

            var result = context.WriteObject(outputStream, token);

            return result;
        }

        public byte GetValueForCharacter(char character)
        {
            lock (mappingLock)
            {
                if (characterMapping.TryGetValue(character, out var result))
                {
                    return result;
                }

                if (characterMappingCounter > byte.MaxValue)
                {
                    throw new NotSupportedException("Cannot support more than 255 separate characters in a simple TrueType font, please" +
                                                    " submit an issue since we will need to add support for composite fonts with multi-byte" +
                                                    " character identifiers.");
                }

                var value = (byte)characterMappingCounter++;

                characterMapping[character] = value;

                result = value;

                return result;
            }
        }

        private static byte[] CompressBytes(IReadOnlyList<byte> bytes)
        {
            using (var memoryStream = new MemoryStream(bytes.ToArray()))
            {
                var parameters = new DictionaryToken(new Dictionary<NameToken, IToken>());
                var flater = new FlateFilter(new DecodeParameterResolver(new NoOpLog()), new PngPredictor(), new NoOpLog());
                var result = flater.Encode(memoryStream, parameters, 0);
                return result;
            }
        }

        private static ArrayToken GetBoundingBox(PdfRectangle boundingBox, decimal scaling)
        {
            return new ArrayToken(new[]
            {
                new NumericToken((decimal)boundingBox.Left * scaling),
                new NumericToken((decimal)boundingBox.Bottom * scaling),
                new NumericToken((decimal)boundingBox.Right * scaling),
                new NumericToken((decimal)boundingBox.Top * scaling)
            });
        }
    }
}