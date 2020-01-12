﻿namespace UglyToad.PdfPig.DocumentLayoutAnalysis.Export
{
    using Content;
    using Core;
    using DocumentLayoutAnalysis;
    using Graphics.Colors;
    using PAGE;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using System.Xml.Serialization;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
    using Util;

    /// <summary>
    /// PAGE-XML 2019-07-15 (XML) text exporter.
    /// <para>See https://github.com/PRImA-Research-Lab/PAGE-XML </para>
    /// </summary>
    public class PageXmlTextExporter : ITextExporter
    {
        private readonly IPageSegmenter pageSegmenter;
        private readonly IWordExtractor wordExtractor;
        private readonly IReadingOrderDetector readingOrderDetector;

        private readonly double scale;
        private readonly string indentChar;

        private int lineCount;
        private int wordCount;
        private int glyphCount;
        private int regionCount;
        private int groupOrderCount;

        private List<PageXmlDocument.PageXmlRegionRefIndexed> orderedRegions;

        /// <summary>
        /// PAGE-XML 2019-07-15 (XML) text exporter.
        /// <para>See https://github.com/PRImA-Research-Lab/PAGE-XML </para>
        /// </summary>
        /// <param name="wordExtractor"></param>
        /// <param name="pageSegmenter"></param>
		/// <param name="readingOrderDetector"></param>
        /// <param name="scale"></param>
        /// <param name="indent">Indent character.</param>
        public PageXmlTextExporter(IWordExtractor wordExtractor, IPageSegmenter pageSegmenter, IReadingOrderDetector readingOrderDetector = null, double scale = 1.0, string indent = "\t")
        {
            this.wordExtractor = wordExtractor;
            this.pageSegmenter = pageSegmenter;
            this.readingOrderDetector = readingOrderDetector;
            this.scale = scale;
            this.indentChar = indent;
        }

        /// <summary>
        /// Get the PAGE-XML (XML) string of the pages layout.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="includePaths">Draw <see cref="PdfPath"/>s present in the page.</param>
        public string Get(PdfDocument document, bool includePaths = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the PAGE-XML (XML) string of the pages layout. Excludes <see cref="PdfPath"/>s.
        /// </summary>
        /// <param name="page"></param>
        public string Get(Page page)
        {
            return Get(page, false);
        }

        /// <summary>
        /// Get the PAGE-XML (XML) string of the pages layout.
        /// </summary>
        /// <param name="page"></param>
        /// <param name="includePaths">Draw <see cref="PdfPath"/>s present in the page.</param>
        public string Get(Page page, bool includePaths)
        {
            lineCount = 0;
            wordCount = 0;
            glyphCount = 0;
            regionCount = 0;
            groupOrderCount = 0;
            orderedRegions = new List<PageXmlDocument.PageXmlRegionRefIndexed>();

            PageXmlDocument pageXmlDocument = new PageXmlDocument()
            {
                Metadata = new PageXmlDocument.PageXmlMetadata()
                {
                    Created = DateTime.UtcNow,
                    LastChange = DateTime.UtcNow,
                    Creator = "PdfPig",
                    Comments = pageSegmenter.GetType().Name + "|" + wordExtractor.GetType().Name,
                },
                PcGtsId = "pc-" + page.GetHashCode()
            };

            pageXmlDocument.Page = ToPageXmlPage(page, includePaths);

            return Serialize(pageXmlDocument);
        }

        private string PointToString(PdfPoint point, double height)
        {
            double x = Math.Round(point.X * scale);
            double y = Math.Round((height - point.Y) * scale);
            return (x > 0 ? x : 0).ToString("0") + "," + (y > 0 ? y : 0).ToString("0");
        }

        private string ToPoints(IEnumerable<PdfPoint> points, double height)
        {
            return string.Join(" ", points.Select(p => PointToString(p, height)));
        }

        private string ToPoints(PdfRectangle pdfRectangle, double height)
        {
            return ToPoints(new[] { pdfRectangle.BottomLeft, pdfRectangle.TopLeft, pdfRectangle.TopRight, pdfRectangle.BottomRight }, height);
        }

        private PageXmlDocument.PageXmlCoords ToCoords(PdfRectangle pdfRectangle, double height)
        {
            return new PageXmlDocument.PageXmlCoords()
            {
                Points = ToPoints(pdfRectangle, height)
            };
        }

        /// <summary>
        /// PageXml Text colour in RGB encoded format
        /// <para>(red value) + (256 x green value) + (65536 x blue value).</para> 
        /// </summary>
        private string ToRgbEncoded(IColor color)
        {
            var rgb = color.ToRGBValues();
            int red = (int)Math.Round(255f * (float)rgb.r);
            int green = 256 * (int)Math.Round(255f * (float)rgb.g);
            int blue = 65536 * (int)Math.Round(255f * (float)rgb.b);
            int sum = red + green + blue;

            // as per below, red and blue order might be inverted... var colorWin = System.Drawing.Color.FromArgb(sum);
            return sum.ToString();
        }

        private PageXmlDocument.PageXmlPage ToPageXmlPage(Page page, bool includePaths)
        {
            var pageXmlPage = new PageXmlDocument.PageXmlPage()
            {
                ImageFilename = "unknown",
                ImageHeight = (int)Math.Round(page.Height * scale),
                ImageWidth = (int)Math.Round(page.Width * scale),
            };

            var regions = new List<PageXmlDocument.PageXmlRegion>();

            var words = page.GetWords(wordExtractor).ToList();
            if (words.Count > 0)
            {
                var blocks = pageSegmenter.GetBlocks(words);

                if (readingOrderDetector != null)
                {
                    blocks = readingOrderDetector.Get(blocks).ToList();
                }

                regions.AddRange(blocks.Select(b => ToPageXmlTextRegion(b, page.Height)));

                if (orderedRegions.Any())
                {
                    pageXmlPage.ReadingOrder = new PageXmlDocument.PageXmlReadingOrder()
                    {
                        Item = new PageXmlDocument.PageXmlOrderedGroup()
                        {
                            Items = orderedRegions.ToArray(),
                            Id = "g" + groupOrderCount++
                        }
                    };
                }
            }

            var images = page.GetImages().ToList();
            if (images.Count > 0)
            {
                regions.AddRange(images.Select(i => ToPageXmlImageRegion(i, page.Height)));
            }

            if (includePaths)
            {
                var graphicalElements = page.ExperimentalAccess.Paths.Select(p => ToPageXmlLineDrawingRegion(p, page.Height));
                if (graphicalElements.Where(g => g != null).Count() > 0)
                {
                    regions.AddRange(graphicalElements.Where(g => g != null));
                }
            }

            pageXmlPage.Items = regions.ToArray();
            return pageXmlPage;
        }

        private PageXmlDocument.PageXmlLineDrawingRegion ToPageXmlLineDrawingRegion(PdfPath pdfPath, double height)
        {
            var bbox = pdfPath.GetBoundingRectangle();
            if (bbox.HasValue)
            {
                regionCount++;
                return new PageXmlDocument.PageXmlLineDrawingRegion()
                {
                    Coords = ToCoords(bbox.Value, height),
                    Id = "r" + regionCount
                };
            }
            return null;
        }

        private PageXmlDocument.PageXmlImageRegion ToPageXmlImageRegion(IPdfImage pdfImage, double height)
        {
            regionCount++;
            var bbox = pdfImage.Bounds;
            return new PageXmlDocument.PageXmlImageRegion()
            {
                Coords = ToCoords(bbox, height),
                Id = "r" + regionCount
            };
        }

        private PageXmlDocument.PageXmlTextRegion ToPageXmlTextRegion(TextBlock textBlock, double height)
        {
            regionCount++;
            string regionId = "r" + regionCount;

            if (readingOrderDetector != null && textBlock.ReadingOrder > -1)
            {
                orderedRegions.Add(new PageXmlDocument.PageXmlRegionRefIndexed()
                {
                    RegionRef = regionId,
                    Index = textBlock.ReadingOrder
                });
            }

            return new PageXmlDocument.PageXmlTextRegion()
            {
                Coords = ToCoords(textBlock.BoundingBox, height),
                Type = PageXmlDocument.PageXmlTextSimpleType.Paragraph,
                TextLines = textBlock.TextLines.Select(l => ToPageXmlTextLine(l, height)).ToArray(),
                TextEquivs = new[] { new PageXmlDocument.PageXmlTextEquiv() { Unicode = textBlock.Text } },
                Id = regionId
            };
        }

        private PageXmlDocument.PageXmlTextLine ToPageXmlTextLine(TextLine textLine, double height)
        {
            lineCount++;
            return new PageXmlDocument.PageXmlTextLine()
            {
                Coords = ToCoords(textLine.BoundingBox, height),
                Production = PageXmlDocument.PageXmlProductionSimpleType.Printed,
                Words = textLine.Words.Select(w => ToPageXmlWord(w, height)).ToArray(),
                TextEquivs = new[] { new PageXmlDocument.PageXmlTextEquiv() { Unicode = textLine.Text } },
                Id = "l" + lineCount
            };
        }

        private PageXmlDocument.PageXmlWord ToPageXmlWord(Word word, double height)
        {
            wordCount++;
            return new PageXmlDocument.PageXmlWord()
            {
                Coords = ToCoords(word.BoundingBox, height),
                Glyphs = word.Letters.Select(l => ToPageXmlGlyph(l, height)).ToArray(),
                TextEquivs = new[] { new PageXmlDocument.PageXmlTextEquiv() { Unicode = word.Text } },
                Id = "w" + wordCount
            };
        }

        private PageXmlDocument.PageXmlGlyph ToPageXmlGlyph(Letter letter, double height)
        {
            glyphCount++;
            return new PageXmlDocument.PageXmlGlyph()
            {
                Coords = ToCoords(letter.GlyphRectangle, height),
                Ligature = false,
                Production = PageXmlDocument.PageXmlProductionSimpleType.Printed,
                TextStyle = new PageXmlDocument.PageXmlTextStyle()
                {
                    FontSize = (float)letter.FontSize,
                    FontFamily = letter.FontName,
                    TextColourRgb = ToRgbEncoded(letter.Color),
                },
                TextEquivs = new[] { new PageXmlDocument.PageXmlTextEquiv() { Unicode = letter.Value } },
                Id = "c" + glyphCount
            };
        }

        private string Serialize(PageXmlDocument pageXmlDocument)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(PageXmlDocument));
            var settings = new XmlWriterSettings()
            {
                Encoding = System.Text.Encoding.UTF8,
                Indent = true,
                IndentChars = indentChar,
            };

            using (var memoryStream = new System.IO.MemoryStream())
            using (var xmlWriter = XmlWriter.Create(memoryStream, settings))
            {
                serializer.Serialize(xmlWriter, pageXmlDocument);
                return System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }
        }

        /// <summary>
        /// Deserialize an <see cref="PageXmlDocument"/> from a given PAGE format XML document.
        /// </summary>
        public static PageXmlDocument Deserialize(string xmlPath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(PageXmlDocument));

            using (var reader = XmlReader.Create(xmlPath))
            {
                return (PageXmlDocument)serializer.Deserialize(reader);
            }
        }
    }
}
