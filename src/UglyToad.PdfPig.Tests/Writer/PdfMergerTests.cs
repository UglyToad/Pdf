﻿namespace UglyToad.PdfPig.Tests.Writer
{
    using Integration;
    using PdfPig.Writer;
    using Xunit;

    public class PdfMergerTests
    {
        [Fact]
        public void CanMerge2SimpleDocuments()
        {
            var one = IntegrationHelpers.GetDocumentPath("Single Page Simple - from inkscape.pdf");
            var two = IntegrationHelpers.GetDocumentPath("Single Page Simple - from open office.pdf");

            var result = PdfMerger.Merge(one, two);

            // FIX: Enable UseLenianParseOff
            using (var document = PdfDocument.Open(result/*, ParsingOptions.LenientParsingOff */))
            {
                Assert.Equal(2, document.NumberOfPages);

                Assert.Equal(1.7m, document.Version);
            }
        }
    }
}
