﻿namespace UglyToad.PdfPig.Writer.Fonts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using IO;
    using PdfPig.Fonts.TrueType;
    using PdfPig.Fonts.TrueType.Parser;
    using PdfPig.Fonts.TrueType.Tables;
    using PdfPig.Fonts.TrueType.Tables.CMapSubTables;
    using Util;

    internal static class TrueTypeSubsetter
    { 
        /*
         * The PDF specification requires the following 10 tables:
         * glyf
         * head
         * hhea
         * hmtx
         * loca
         * maxp
         * cvt
         * fpgm
         * prep
         * cmap
         */
        private static readonly IReadOnlyList<string> RequiredTags = new[]
        {
            TrueTypeHeaderTable.Cmap,
            // TrueTypeHeaderTable.Cvt,
            // TrueTypeHeaderTable.Fpgm,
            TrueTypeHeaderTable.Glyf,
            TrueTypeHeaderTable.Head,
            TrueTypeHeaderTable.Hhea,
            TrueTypeHeaderTable.Hmtx,
            TrueTypeHeaderTable.Loca,
            TrueTypeHeaderTable.Maxp,
            TrueTypeHeaderTable.Prep
        };

        private static readonly TrueTypeFontParser Parser
            = new TrueTypeFontParser();

        public static byte[] Subset(byte[] fontBytes, TrueTypeSubsetEncoding newEncoding)
        {
            if (fontBytes == null)
            {
                throw new ArgumentNullException(nameof(fontBytes));
            }

            if (newEncoding == null)
            {
                throw new ArgumentNullException(nameof(newEncoding));
            }

            var font = Parser.Parse(new TrueTypeDataBytes(new ByteArrayInputBytes(fontBytes)));

            var indexMapping = GetIndexMapping(font, newEncoding);

            var directoryEntries = new DirectoryEntry[RequiredTags.Count];

            using (var stream = new MemoryStream())
            {
                var offsetSubtable = new TrueTypeOffsetSubtable((byte)RequiredTags.Count);
                offsetSubtable.Write(stream);

                // The table directory follows the offset subtable.

                // Entries in the table directory must be sorted in ascending order by tag (case sensitive).
                // Each table in the font file must have its own table directory entry.
                for (var i = 0; i < RequiredTags.Count; i++)
                {
                    var tag = RequiredTags[i];
                    var entry = new DirectoryEntry(tag, stream.Position, font.TableHeaders[tag]);
                    entry.DummyHeader.Write(stream);
                    directoryEntries[i] = entry;
                }

                TrueTypeGlyphTableSubsetter.NewGlyphTable newGlyphTable = null;
                // Write the actual tables.
                for (var i = 0; i < directoryEntries.Length; i++)
                {
                    var entry = directoryEntries[i];

                    // TODO: place on a % 4 boundary offset.

                    entry.OutputTableOffset = stream.Position;

                    if (entry.Tag == TrueTypeHeaderTable.Cmap)
                    {
                        var cmapTable = GetCMapTable(font, entry, indexMapping);
                        cmapTable.Write(stream);
                    }
                    else if (entry.Tag == TrueTypeHeaderTable.Glyf)
                    {
                        newGlyphTable = TrueTypeGlyphTableSubsetter.SubsetGlyphTable(font, fontBytes, indexMapping);
                        stream.Write(newGlyphTable.Bytes, 0, newGlyphTable.Bytes.Length);
                    }
                    else if (entry.Tag == TrueTypeHeaderTable.Hmtx)
                    {
                        var hmtx = GetHorizontalMetricsTable(font, entry, indexMapping);
                        hmtx.Write(stream);
                    }
                    else if (entry.Tag == TrueTypeHeaderTable.Loca)
                    {
                        if (newGlyphTable == null)
                        {
                            throw new InvalidOperationException();
                        }

                        var table = new IndexToLocationTable(entry.DummyHeader, IndexToLocationTable.EntryFormat.Long,
                            newGlyphTable.GlyphOffsets.Select(x => (long)x).ToArray());
                        table.Write(stream);
                    }
                    else if (entry.Tag == TrueTypeHeaderTable.Head)
                    {
                        // Update indexToLoc format.
                        var headBytes = GetRawInputTableBytes(fontBytes, entry);
                        WriteUShort(headBytes, headBytes.Length - 4, 1);
                        stream.Write(headBytes, 0, headBytes.Length);
                    }
                    else if (entry.Tag == TrueTypeHeaderTable.Hhea)
                    {
                        // Update number of h metrics.
                        var hheaBytes = GetRawInputTableBytes(fontBytes, entry);
                        WriteUShort(hheaBytes, hheaBytes.Length - 2, (ushort)indexMapping.Length);
                        stream.Write(hheaBytes, 0, hheaBytes.Length);
                    }
                    else if (entry.Tag == TrueTypeHeaderTable.Maxp)
                    {
                        // Update number of glyphs.
                        var maxpBytes = GetRawInputTableBytes(fontBytes, entry);
                        WriteUShort(maxpBytes, 4, (ushort)indexMapping.Length);
                        stream.Write(maxpBytes, 0, maxpBytes.Length);
                    }
                    else
                    {
                        // Copy table as-is.
                        var buffer = GetRawInputTableBytes(fontBytes, entry);
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    entry.Length = (uint)(stream.Position - entry.OutputTableOffset);
                }

                using (var inputBytes = new StreamInputBytes(stream, false))
                {
                    // Update the table directory.
                    for (var i = 0; i < directoryEntries.Length; i++)
                    {
                        var entry = directoryEntries[i];

                        var actualHeaderExceptChecksum = new TrueTypeHeaderTable(entry.Tag, 0, (uint)entry.OutputTableOffset, entry.Length);

                        var checksum = TrueTypeChecksumCalculator.Calculate(inputBytes, actualHeaderExceptChecksum);

                        var actualHeader = new TrueTypeHeaderTable(entry.Tag, checksum, (uint)entry.OutputTableOffset, entry.Length);

                        stream.Seek(entry.OutputEntryOffset, SeekOrigin.Begin);

                        actualHeader.Write(stream);
                    }
                }

                // TODO: whole font checksum.
                var result = stream.ToArray();
                var canparse = Parser.Parse(new TrueTypeDataBytes(result));
                return result;
            }
        }

        private static OldToNewGlyphIndex[] GetIndexMapping(TrueTypeFontProgram font, TrueTypeSubsetEncoding newEncoding)
        {
            var result = new OldToNewGlyphIndex[newEncoding.Characters.Count + 1];

            result[0] = new OldToNewGlyphIndex(0, 0, '\0');

            var previousCMap = font.MacRomanCMap ?? font.WindowsUnicodeCMap ?? font.WindowsSymbolCMap;

            if (previousCMap == null)
            {
                throw new InvalidOperationException("Cannot subset font due to missing cmap subtables.");
            }

            for (var i = 0; i < newEncoding.Characters.Count; i++)
            {
                var character = newEncoding.Characters[i];

                var oldIndex = (ushort)previousCMap.CharacterCodeToGlyphIndex(character);
                result[i + 1] = new OldToNewGlyphIndex(oldIndex, (ushort)(i + 1), character);
            }

            return result;
        }

        private static CMapTable GetCMapTable(TrueTypeFontProgram font, DirectoryEntry entry, OldToNewGlyphIndex[] encoding)
        {
            var data = new byte[256];
            for (var i = 0; i < data.Length; i++)
            {
                if (i < encoding.Length)
                {
                    data[i] = encoding[i].NewIndex;
                }
                else
                {
                    break;
                }
            }

            var cmap = new CMapTable(font.TableRegister.CMapTable.Version, entry.DummyHeader, new []
            {
                new ByteEncodingCMapTable(TrueTypeCMapPlatform.Macintosh, 0, 0, data)
            });

            return cmap;
        }

        private static HorizontalMetricsTable GetHorizontalMetricsTable(TrueTypeFontProgram font, DirectoryEntry entry, OldToNewGlyphIndex[] encoding)
        {
            var current = font.TableRegister.HorizontalMetricsTable;

            var newMetrics = new HorizontalMetricsTable.HorizontalMetric[encoding.Length];

            for (var i = 0; i < encoding.Length; i++)
            {
                var mapping = encoding[i];
                // TODO: might be an additional lsb only.
                var value = current.HorizontalMetrics[mapping.OldIndex];
                newMetrics[i] = value;
            }

            return new HorizontalMetricsTable(entry.DummyHeader, newMetrics, EmptyArray<short>.Instance);
        }

        private static byte[] GetRawInputTableBytes(byte[] font, DirectoryEntry entry)
        {
            var buffer = new byte[entry.InputHeader.Length];
            Array.Copy(font, entry.InputHeader.Offset, buffer, 0, buffer.Length);
            return buffer;
        }

        private static void WriteUShort(byte[] array, int offset, ushort value)
        {
            array[offset] = (byte)(value >> 8);
            array[offset + 1] = (byte)(value >> 0);
        }

        private class DirectoryEntry
        {
            public string Tag { get; }

            public long OutputEntryOffset { get; }

            public TrueTypeHeaderTable InputHeader { get; }

            public TrueTypeHeaderTable DummyHeader { get; }

            public long OutputTableOffset { get; set; }

            public uint Length { get; set; }

            public DirectoryEntry(string tag, long outputEntryOffset, TrueTypeHeaderTable inputHeader)
            {
                Tag = tag;
                OutputEntryOffset = outputEntryOffset;
                InputHeader = inputHeader;
                DummyHeader = TrueTypeHeaderTable.GetEmptyHeaderTable(tag);
            }
        }

        public struct OldToNewGlyphIndex
        {
            public ushort OldIndex { get; }

            public byte NewIndex { get; }

            public char Represents { get; }

            public OldToNewGlyphIndex(ushort oldIndex, ushort newIndex, char represents)
            {
                OldIndex = oldIndex;
                NewIndex = (byte)newIndex;
                Represents = represents;
            }

            public override string ToString()
            {
                return $"{Represents}: From {OldIndex} To {NewIndex}.";
            }
        }
    }

    internal class TrueTypeSubsetEncoding
    {
        public IReadOnlyList<char> Characters { get; }

        public TrueTypeSubsetEncoding(IReadOnlyList<char> characters)
        {
            Characters = characters;
        }
    }
}

