﻿namespace UglyToad.PdfPig.Fonts.Parser.Parts
{
    using System;
    using System.Collections.Generic;
    using CidFonts;
    using CompactFontFormat;
    using Exceptions;
    using Filters;
    using Geometry;
    using IO;
    using PdfPig.Exceptions;
    using PdfPig.Parser.Parts;
    using Tokenization.Scanner;
    using Tokens;
    using TrueType;
    using TrueType.Parser;
    using Util;

    internal class CidFontFactory
    {
        private readonly FontDescriptorFactory descriptorFactory;
        private readonly TrueTypeFontParser trueTypeFontParser;
        private readonly CompactFontFormatParser compactFontFormatParser;
        private readonly IFilterProvider filterProvider;
        private readonly IPdfTokenScanner pdfScanner;

        public CidFontFactory(IPdfTokenScanner pdfScanner, FontDescriptorFactory descriptorFactory, 
            TrueTypeFontParser trueTypeFontParser,
            CompactFontFormatParser compactFontFormatParser,
            IFilterProvider filterProvider)
        {
            this.pdfScanner = pdfScanner;
            this.descriptorFactory = descriptorFactory;
            this.trueTypeFontParser = trueTypeFontParser;
            this.compactFontFormatParser = compactFontFormatParser;
            this.filterProvider = filterProvider;
        }

        public ICidFont Generate(DictionaryToken dictionary, bool isLenientParsing)
        {
            var type = dictionary.GetNameOrDefault(NameToken.Type);
            if (!NameToken.Font.Equals(type))
            {
                throw new InvalidFontFormatException($"Expected \'Font\' dictionary but found \'{type}\'");
            }

            var widths = ReadWidths(dictionary);

            var defaultWidth = default(double?);
            if (dictionary.TryGet(NameToken.Dw, pdfScanner, out NumericToken defaultWidthToken))
            {
                defaultWidth = defaultWidthToken.Double;
            }

            var verticalWritingMetrics = ReadVerticalDisplacements(dictionary);

            FontDescriptor descriptor = null;
            if (TryGetFontDescriptor(dictionary, out var descriptorDictionary))
            {
                descriptor = descriptorFactory.Generate(descriptorDictionary, pdfScanner, isLenientParsing);
            }

            var fontProgram = ReadDescriptorFile(descriptor);

            var baseFont = dictionary.GetNameOrDefault(NameToken.BaseFont);

            var systemInfo = GetSystemInfo(dictionary);

            var subType = dictionary.GetNameOrDefault(NameToken.Subtype);
            if (NameToken.CidFontType0.Equals(subType))
            {
                return new Type0CidFont(fontProgram, type, subType, baseFont, systemInfo, descriptor, verticalWritingMetrics, widths, defaultWidth);
            }

            if (NameToken.CidFontType2.Equals(subType))
            {
                var cidToGid = GetCharacterIdentifierToGlyphIndexMap(dictionary, isLenientParsing);

                return new Type2CidFont(type, subType, baseFont, systemInfo, descriptor, fontProgram, verticalWritingMetrics, widths, defaultWidth, cidToGid);
            }

            return null;
        }

        private bool TryGetFontDescriptor(DictionaryToken dictionary, out DictionaryToken descriptorDictionary)
        {
            descriptorDictionary = null;

            if (!dictionary.TryGet(NameToken.FontDescriptor, out var baseValue))
            {
                return false;
            }

            var descriptor = DirectObjectFinder.Get<DictionaryToken>(baseValue, pdfScanner);

            descriptorDictionary = descriptor;

            return true;
        }

        private ICidFontProgram ReadDescriptorFile(FontDescriptor descriptor)
        {
            if (descriptor?.FontFile == null)
            {
                return null;
            }

            var fontFileStream = DirectObjectFinder.Get<StreamToken>(descriptor.FontFile.ObjectKey, pdfScanner);

            if (fontFileStream == null)
            {
                return null;
            }

            var fontFile = fontFileStream.Decode(filterProvider);

            switch (descriptor.FontFile.FileType)
            {
                case DescriptorFontFile.FontFileType.TrueType:
                    var input = new TrueTypeDataBytes(new ByteArrayInputBytes(fontFile));
                    return trueTypeFontParser.Parse(input);
                case DescriptorFontFile.FontFileType.FromSubtype:
                    {
                        if (!DirectObjectFinder.TryGet(descriptor.FontFile.ObjectKey, pdfScanner, out StreamToken str))
                        {
                            throw new NotSupportedException("Cannot read CID font from subtype.");
                        }

                        if (!str.StreamDictionary.TryGet(NameToken.Subtype, out NameToken subtypeName))
                        {
                            throw new PdfDocumentFormatException($"The font file stream did not contain a subtype entry: {str.StreamDictionary}.");
                        }

                        if (subtypeName == NameToken.CidFontType0C
                            || subtypeName == NameToken.Type1C)
                        {
                            var bytes = str.Decode(filterProvider);
                            var font = compactFontFormatParser.Parse(new CompactFontFormatData(bytes));
                            return font;
                        }

                        if (subtypeName == NameToken.OpenType)
                        {

                        }
                        else
                        {
                            throw new PdfDocumentFormatException($"Unexpected subtype for CID font: {subtypeName}.");
                        }

                        throw new NotSupportedException($"Cannot read CID font from subtype: {subtypeName}.");
                    }
                default:
                    throw new NotSupportedException("Currently only TrueType fonts are supported.");
            }
        }

        private IReadOnlyDictionary<int, double> ReadWidths(DictionaryToken dict)
        {
            var widths = new Dictionary<int, double>();

            if (!dict.TryGet(NameToken.W, out var widthsItem) || !(widthsItem is ArrayToken widthArray))
            {
                return widths;
            }

            var size = widthArray.Data.Count;
            var counter = 0;
            while (counter < size)
            {
                var firstCode = (NumericToken)widthArray.Data[counter++];
                var next = widthArray.Data[counter++];
                if (DirectObjectFinder.TryGet(next, pdfScanner, out ArrayToken array))
                {
                    var startRange = firstCode.Int;
                    var arraySize = array.Data.Count;

                    for (var i = 0; i < arraySize; i++)
                    {
                        var width = (NumericToken)array.Data[i];
                        widths[startRange + i] = width.Double;
                    }
                }
                else
                {
                    var secondCode = (NumericToken)next;
                    var rangeWidth = (NumericToken)widthArray.Data[counter++];
                    var startRange = firstCode.Int;
                    var endRange = secondCode.Int;
                    var width = rangeWidth.Double;
                    for (var i = startRange; i <= endRange; i++)
                    {
                        widths[i] = width;
                    }
                }
            }

            return widths;
        }

        private static VerticalWritingMetrics ReadVerticalDisplacements(DictionaryToken dict)
        {
            var verticalDisplacements = new Dictionary<int, double>();
            var positionVectors = new Dictionary<int, PdfVector>();

            // The default position vector and displacement vector are specified by the DW2 entry.
            VerticalVectorComponents dw2;
            if (!dict.TryGet(NameToken.Dw2, out var dw2Token) || !(dw2Token is ArrayToken arrayVerticalComponents))
            {
                dw2 = VerticalVectorComponents.Default;
            }
            else
            {
                var position = ((NumericToken)arrayVerticalComponents.Data[0]).Double;
                var displacement = ((NumericToken)arrayVerticalComponents.Data[1]).Double;

                dw2 = new VerticalVectorComponents(position, displacement);
            }

            // vertical metrics for individual CIDs.
            if (dict.TryGet(NameToken.W2, out var w2Token) && w2Token is ArrayToken w2)
            {
                for (var i = 0; i < w2.Data.Count; i++)
                {
                    var c = (NumericToken)w2.Data[i];
                    var next = w2.Data[++i];

                    if (next is ArrayToken array)
                    {
                        for (var j = 0; j < array.Data.Count; j++)
                        {
                            var cid = c.Int + j;
                            // ReSharper disable InconsistentNaming
                            var w1y = (NumericToken)array.Data[j];
                            var v1x = (NumericToken)array.Data[++j];
                            var v1y = (NumericToken)array.Data[++j];

                            verticalDisplacements[cid] = w1y.Double;

                            positionVectors[cid] = new PdfVector(v1x.Double, v1y.Double);
                        }
                    }
                    else
                    {
                        var first = c.Int;
                        var last = ((NumericToken)next).Int;
                        var w1y = (NumericToken)w2.Data[++i];
                        var v1x = (NumericToken)w2.Data[++i];
                        var v1y = (NumericToken)w2.Data[++i];
                        // ReSharper restore InconsistentNaming

                        for (var cid = first; cid <= last; cid++)
                        {
                            verticalDisplacements[cid] = w1y.Double;

                            positionVectors[cid] = new PdfVector(v1x.Double, v1y.Double);
                        }
                    }
                }
            }

            return new VerticalWritingMetrics(dw2, verticalDisplacements, positionVectors);
        }

        private CharacterIdentifierSystemInfo GetSystemInfo(DictionaryToken dictionary)
        {
            if (!dictionary.TryGet(NameToken.CidSystemInfo, out var cidEntry))
            {
                throw new InvalidFontFormatException($"No CID System Info was found in the CID Font dictionary: {dictionary}");
            }

            if (cidEntry is DictionaryToken cidDictionary)
            {

            }
            else
            {
                cidDictionary =
                    DirectObjectFinder.Get<DictionaryToken>(cidEntry, pdfScanner);
            }

            var registry = SafeKeyAccess(cidDictionary, NameToken.Registry);
            var ordering = SafeKeyAccess(cidDictionary, NameToken.Ordering);
            var supplement = cidDictionary.GetIntOrDefault(NameToken.Supplement);

            return new CharacterIdentifierSystemInfo(registry, ordering, supplement);
        }

        private CharacterIdentifierToGlyphIndexMap GetCharacterIdentifierToGlyphIndexMap(DictionaryToken dictionary, bool isLenientParsing)
        {
            if (!dictionary.TryGet(NameToken.CidToGidMap, out var entry))
            {
                return new CharacterIdentifierToGlyphIndexMap();
            }

            if (entry is NameToken name)
            {
                if (!name.Equals(NameToken.Identity) && !isLenientParsing)
                {
                    throw new InvalidOperationException($"The CIDToGIDMap in a Type 0 font should have the value /Identity, instead got: {name}.");
                }

                return new CharacterIdentifierToGlyphIndexMap();
            }

            var stream = DirectObjectFinder.Get<StreamToken>(entry, pdfScanner);

            var bytes = stream.Decode(filterProvider);

            return new CharacterIdentifierToGlyphIndexMap(bytes);
        }

        private string SafeKeyAccess(DictionaryToken dictionary, NameToken keyName)
        {
            if (!dictionary.TryGet(keyName, out var token))
            {
                return string.Empty;
            }

            if (token is StringToken str)
            {
                return str.Data;
            }

            if (token is HexToken hex)
            {
                return hex.Data;
            }

            if (token is IndirectReferenceToken obj)
            {
                if (DirectObjectFinder.TryGet(obj, pdfScanner, out StringToken stringToken))
                {
                    return stringToken.Data;
                }

                if (DirectObjectFinder.TryGet(obj, pdfScanner, out HexToken hexToken))
                {
                    return hexToken.Data;
                }

                throw new PdfDocumentFormatException($"Could not get key for name: {keyName} in {dictionary}.");
            }

            return string.Empty;
        }
    }
}
