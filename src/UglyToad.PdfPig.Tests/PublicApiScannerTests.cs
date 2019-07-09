﻿namespace UglyToad.PdfPig.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using PdfPig.Graphics.Operations;
    using Xunit;

    public class PublicApiScannerTests
    {
        [Fact]
        public void OnlyExposedApiIsPublic()
        {
            var assembly = typeof(PdfDocument).Assembly;

            var types = assembly.GetTypes();

            var publicTypeNames = new List<string>();

            foreach (var type in types)
            {
                // Skip coverage measuring instrumentation classes.
                if (type.FullName.StartsWith("Coverlet", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (type.IsPublic)
                {
                    publicTypeNames.Add(type.FullName);
                }
            }

            var expected = new List<string>
            {
                "UglyToad.PdfPig.AcroForms.Fields.AcroChoiceFieldFlags",
                "UglyToad.PdfPig.AcroForms.Fields.AcroChoiceOption",
                "UglyToad.PdfPig.AcroForms.Fields.AcroComboBoxField",
                "UglyToad.PdfPig.AcroForms.Fields.AcroFieldBase",
                "UglyToad.PdfPig.AcroForms.Fields.AcroFieldCommonInformation",
                "UglyToad.PdfPig.AcroForms.Fields.AcroListBoxField",
                "UglyToad.PdfPig.AcroForms.Fields.AcroTextField",
                "UglyToad.PdfPig.AcroForms.Fields.AcroTextFieldFlags",
                "UglyToad.PdfPig.Annotations.Annotation",
                "UglyToad.PdfPig.Annotations.AnnotationBorder",
                "UglyToad.PdfPig.Annotations.AnnotationFlags",
                "UglyToad.PdfPig.Annotations.AnnotationType",
                "UglyToad.PdfPig.Content.Catalog",
                "UglyToad.PdfPig.Content.DocumentInformation",
                "UglyToad.PdfPig.Content.Letter",
                "UglyToad.PdfPig.Content.Page",
                "UglyToad.PdfPig.Content.PageRotationDegrees",
                "UglyToad.PdfPig.Content.PageSize",
                "UglyToad.PdfPig.Content.Word",
                "UglyToad.PdfPig.Content.TextLine",
                "UglyToad.PdfPig.Content.TextDirection",
                "UglyToad.PdfPig.Core.TransformationMatrix",
                "UglyToad.PdfPig.CrossReference.CrossReferenceTable",
                "UglyToad.PdfPig.CrossReference.CrossReferenceType",
                "UglyToad.PdfPig.CrossReference.TrailerDictionary",
                "UglyToad.PdfPig.DocumentLayoutAnalysis.Distances",
                "UglyToad.PdfPig.DocumentLayoutAnalysis.MathExtensions",
                "UglyToad.PdfPig.DocumentLayoutAnalysis.NearestNeighbourWordExtractor",
                "UglyToad.PdfPig.DocumentLayoutAnalysis.RecursiveXYCut",
                "UglyToad.PdfPig.DocumentLayoutAnalysis.XYNode",
                "UglyToad.PdfPig.DocumentLayoutAnalysis.XYLeaf",
                "UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException",
                "UglyToad.PdfPig.Exceptions.PdfDocumentFormatException",
                "UglyToad.PdfPig.Fonts.DescriptorFontFile",
                "UglyToad.PdfPig.Fonts.Exceptions.InvalidFontFormatException",
                "UglyToad.PdfPig.Fonts.FontDescriptor",
                "UglyToad.PdfPig.Fonts.FontDescriptorFlags",
                "UglyToad.PdfPig.Fonts.FontStretch",
                "UglyToad.PdfPig.Fonts.Standard14Font",
                "UglyToad.PdfPig.Geometry.PdfPath",
                "UglyToad.PdfPig.Geometry.PdfPoint",
                "UglyToad.PdfPig.Geometry.PdfLine",
                "UglyToad.PdfPig.Geometry.PdfRectangle",
                "UglyToad.PdfPig.Graphics.Core.LineCapStyle",
                "UglyToad.PdfPig.Graphics.Core.LineDashPattern",
                "UglyToad.PdfPig.Graphics.Core.LineJoinStyle",
                "UglyToad.PdfPig.Graphics.Core.RenderingIntent",
                "UglyToad.PdfPig.Graphics.Core.TextRenderingMode",
                "UglyToad.PdfPig.Graphics.CurrentFontState",
                "UglyToad.PdfPig.Graphics.CurrentGraphicsState",
                "UglyToad.PdfPig.Graphics.IOperationContext",
                "UglyToad.PdfPig.Graphics.Operations.ClippingPaths.ModifyClippingByEvenOddIntersect",
                "UglyToad.PdfPig.Graphics.Operations.ClippingPaths.ModifyClippingByNonZeroWindingIntersect",
                "UglyToad.PdfPig.Graphics.Operations.Compatibility.BeginCompatibilitySection",
                "UglyToad.PdfPig.Graphics.Operations.Compatibility.EndCompatibilitySection",
                "UglyToad.PdfPig.Graphics.Operations.CloseAndStrokePath",
                "UglyToad.PdfPig.Graphics.Operations.CloseFillPathEvenOddRuleAndStroke",
                "UglyToad.PdfPig.Graphics.Operations.CloseFillPathNonZeroWindingAndStroke",
                "UglyToad.PdfPig.Graphics.Operations.EndPath",
                "UglyToad.PdfPig.Graphics.Operations.FillPathEvenOddRule",
                "UglyToad.PdfPig.Graphics.Operations.FillPathEvenOddRuleAndStroke",
                "UglyToad.PdfPig.Graphics.Operations.FillPathNonZeroWinding",
                "UglyToad.PdfPig.Graphics.Operations.FillPathNonZeroWindingAndStroke",
                "UglyToad.PdfPig.Graphics.Operations.FillPathNonZeroWindingCompatibility",
                "UglyToad.PdfPig.Graphics.Operations.General.SetColorRenderingIntent",
                "UglyToad.PdfPig.Graphics.Operations.General.SetFlatnessTolerance",
                "UglyToad.PdfPig.Graphics.Operations.General.SetLineCap",
                "UglyToad.PdfPig.Graphics.Operations.General.SetLineDashPattern",
                "UglyToad.PdfPig.Graphics.Operations.General.SetLineJoin",
                "UglyToad.PdfPig.Graphics.Operations.General.SetLineWidth",
                "UglyToad.PdfPig.Graphics.Operations.General.SetMiterLimit",
                "UglyToad.PdfPig.Graphics.Operations.IGraphicsStateOperation",
                "UglyToad.PdfPig.Graphics.Operations.InlineImages.BeginInlineImage",
                "UglyToad.PdfPig.Graphics.Operations.InlineImages.BeginInlineImageData",
                "UglyToad.PdfPig.Graphics.Operations.InlineImages.EndInlineImage",
                "UglyToad.PdfPig.Graphics.Operations.InvokeNamedXObject",
                "UglyToad.PdfPig.Graphics.Operations.MarkedContent.BeginMarkedContent",
                "UglyToad.PdfPig.Graphics.Operations.MarkedContent.BeginMarkedContentWithProperties",
                "UglyToad.PdfPig.Graphics.Operations.MarkedContent.DesignateMarkedContentPoint",
                "UglyToad.PdfPig.Graphics.Operations.MarkedContent.DesignateMarkedContentPointWithProperties",
                "UglyToad.PdfPig.Graphics.Operations.MarkedContent.EndMarkedContent",
                "UglyToad.PdfPig.Graphics.Operations.PaintShading",
                "UglyToad.PdfPig.Graphics.Operations.PathConstruction.AppendDualControlPointBezierCurve",
                "UglyToad.PdfPig.Graphics.Operations.PathConstruction.AppendEndControlPointBezierCurve",
                "UglyToad.PdfPig.Graphics.Operations.PathConstruction.AppendRectangle",
                "UglyToad.PdfPig.Graphics.Operations.PathConstruction.AppendStartControlPointBezierCurve",
                "UglyToad.PdfPig.Graphics.Operations.PathConstruction.AppendStraightLineSegment",
                "UglyToad.PdfPig.Graphics.Operations.PathConstruction.BeginNewSubpath",
                "UglyToad.PdfPig.Graphics.Operations.PathConstruction.CloseSubpath",
                "UglyToad.PdfPig.Graphics.Operations.SetNonStrokeColor",
                "UglyToad.PdfPig.Graphics.Operations.SetNonStrokeColorAdvanced",
                "UglyToad.PdfPig.Graphics.Operations.SetNonStrokeColorDeviceCmyk",
                "UglyToad.PdfPig.Graphics.Operations.SetNonStrokeColorDeviceGray",
                "UglyToad.PdfPig.Graphics.Operations.SetNonStrokeColorDeviceRgb",
                "UglyToad.PdfPig.Graphics.Operations.SetNonStrokeColorSpace",
                "UglyToad.PdfPig.Graphics.Operations.SetStrokeColorDeviceCmyk",
                "UglyToad.PdfPig.Graphics.Operations.SetStrokeColor",
                "UglyToad.PdfPig.Graphics.Operations.SetStrokeColorAdvanced",
                "UglyToad.PdfPig.Graphics.Operations.SetStrokeColorDeviceGray",
                "UglyToad.PdfPig.Graphics.Operations.SetStrokeColorDeviceRgb",
                "UglyToad.PdfPig.Graphics.Operations.SetStrokeColorSpace",
                "UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState.ModifyCurrentTransformationMatrix",
                "UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState.Pop",
                "UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState.Push",
                "UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState.SetGraphicsStateParametersFromDictionary",
                "UglyToad.PdfPig.Graphics.Operations.StrokePath",
                "UglyToad.PdfPig.Graphics.Operations.TextObjects.BeginText",
                "UglyToad.PdfPig.Graphics.Operations.TextObjects.EndText",
                "UglyToad.PdfPig.Graphics.Operations.TextPositioning.MoveToNextLine",
                "UglyToad.PdfPig.Graphics.Operations.TextPositioning.MoveToNextLineWithOffset",
                "UglyToad.PdfPig.Graphics.Operations.TextPositioning.MoveToNextLineWithOffsetSetLeading",
                "UglyToad.PdfPig.Graphics.Operations.TextPositioning.SetTextMatrix",
                "UglyToad.PdfPig.Graphics.Operations.TextShowing.MoveToNextLineShowText",
                "UglyToad.PdfPig.Graphics.Operations.TextShowing.MoveToNextLineShowTextWithSpacing",
                "UglyToad.PdfPig.Graphics.Operations.TextShowing.ShowText",
                "UglyToad.PdfPig.Graphics.Operations.TextShowing.ShowTextsWithPositioning",
                "UglyToad.PdfPig.Graphics.Operations.TextState.SetCharacterSpacing",
                "UglyToad.PdfPig.Graphics.Operations.TextState.SetFontAndSize",
                "UglyToad.PdfPig.Graphics.Operations.TextState.SetHorizontalScaling",
                "UglyToad.PdfPig.Graphics.Operations.TextState.SetTextLeading",
                "UglyToad.PdfPig.Graphics.Operations.TextState.SetTextRenderingMode",
                "UglyToad.PdfPig.Graphics.Operations.TextState.SetTextRise",
                "UglyToad.PdfPig.Graphics.Operations.TextState.SetWordSpacing",
                "UglyToad.PdfPig.Graphics.Operations.TextState.Type3SetGlyphWidth",
                "UglyToad.PdfPig.Graphics.Operations.TextState.Type3SetGlyphWidthAndBoundingBox",
                "UglyToad.PdfPig.Graphics.TextMatrices",
                "UglyToad.PdfPig.IndirectReference",
                "UglyToad.PdfPig.IO.IInputBytes",
                "UglyToad.PdfPig.Logging.ILog",
                "UglyToad.PdfPig.ParsingOptions",
                "UglyToad.PdfPig.PdfDocument",
                "UglyToad.PdfPig.Structure",
                "UglyToad.PdfPig.Tokens.ArrayToken",
                "UglyToad.PdfPig.Tokens.BooleanToken",
                "UglyToad.PdfPig.Tokens.CommentToken",
                "UglyToad.PdfPig.Tokens.DictionaryToken",
                "UglyToad.PdfPig.Tokens.HexToken",
                "UglyToad.PdfPig.Tokens.IDataToken`1",
                "UglyToad.PdfPig.Tokens.IndirectReferenceToken",
                "UglyToad.PdfPig.Tokens.IToken",
                "UglyToad.PdfPig.Tokens.NameToken",
                "UglyToad.PdfPig.Tokens.NullToken",
                "UglyToad.PdfPig.Tokens.NumericToken",
                "UglyToad.PdfPig.Tokens.ObjectToken",
                "UglyToad.PdfPig.Tokens.StreamToken",
                "UglyToad.PdfPig.Tokens.StringToken",
                "UglyToad.PdfPig.Util.IWordExtractor",
                "UglyToad.PdfPig.Writer.PdfDocumentBuilder",
                "UglyToad.PdfPig.Writer.PdfPageBuilder",
                "UglyToad.PdfPig.Writer.TokenWriter",
                "UglyToad.PdfPig.XObjects.XObjectImage"
            };

            foreach (var publicTypeName in publicTypeNames)
            {
                Assert.True(expected.Contains(publicTypeName), $"Type should not be public: {publicTypeName}.");
            }

            foreach (var expectedPublicType in expected)
            {
                Assert.True(publicTypeNames.Contains(expectedPublicType), $"Type should be public: {expectedPublicType}.");
            }
        }

        [Fact]
        public void AllSpecificationOperatorsArePresent()
        {
            var assembly = typeof(PdfDocument).Assembly;

            var types = assembly.GetTypes().Where(x => typeof(IGraphicsStateOperation).IsAssignableFrom(x) && !x.IsInterface);

            var expectedSymbols = new[]
            {
                "b",
                "B",
                "b*",
                "B*",
                "BDC",
                "BI",
                "BMC",
                "BT",
                "BX",
                "c",
                "cm",
                "CS",
                "cs",
                "d",
                "d0",
                "d1",
                "Do",
                "DP",
                "EI",
                "EMC",
                "ET",
                "EX",
                "f",
                "F",
                "f*",
                "G",
                "g",
                "gs",
                "h",
                "i",
                "ID",
                "j",
                "J",
                "K",
                "k",
                "l",
                "m",
                "M",
                "MP",
                "n",
                "q",
                "Q",
                "re",
                "RG",
                "rg",
                "ri",
                "s",
                "S",
                "SC",
                "sc",
                "SCN",
                "scn",
                "sh",
                "T*",
                "Tc",
                "Td",
                "TD",
                "Tf",
                "Tj",
                "TJ",
                "TL",
                "Tm",
                "Tr",
                "Ts",
                "Tw",
                "Tz",
                "v",
                "w",
                "W",
                "W*",
                "y",
                "'",
                "\""
            };

            var symbols = new List<string>();

            foreach (var type in types)
            {
                var symbol = type.GetField("Symbol", BindingFlags.Public | BindingFlags.Static);
                symbols.Add(symbol.GetValue(null).ToString());
            }

            foreach (var expectedSymbol in expectedSymbols)
            {
                Assert.True(symbols.Contains(expectedSymbol), $"There is no operation defined with the symbol: {expectedSymbol}.");
            }
        }
    }
}
