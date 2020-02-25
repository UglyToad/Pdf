﻿namespace UglyToad.PdfPig.Writer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Content;
    using Core;
    using CrossReference;
    using Encryption;
    using Filters;
    using Logging;
    using Parser;
    using Parser.FileStructure;
    using Parser.Parts;
    using Tokenization.Scanner;
    using Tokens;
    using UglyToad.PdfPig.Exceptions;
    using UglyToad.PdfPig.Graphics.Operations;
    using UglyToad.PdfPig.Writer.Fonts;

    /// <summary>
    /// Merges PDF documents into each other.
    /// </summary>
    public static class PdfMerger
    {
        private static readonly ILog Log = new NoOpLog();

        private static readonly IFilterProvider FilterProvider = new MemoryFilterProvider(new DecodeParameterResolver(Log),
            new PngPredictor(), Log);

        /// <summary>
        /// Merge two PDF documents together with the pages from <see cref="file1"/>
        /// followed by <see cref="file2"/>.
        /// </summary>
        public static byte[] Merge(string file1, string file2)
        {
            if (file1 == null)
            {
                throw new ArgumentNullException(nameof(file1));
            }

            if (file2 == null)
            {
                throw new ArgumentNullException(nameof(file2));
            }

            return Merge(new[]
            {
                File.ReadAllBytes(file1),
                File.ReadAllBytes(file2)
            });
        }

        /// <summary>
        /// Merge the set of PDF documents.
        /// </summary>
        public static byte[] Merge(IReadOnlyList<byte[]> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            const bool isLenientParsing = true;

            var documentBuilder = new DocumentMerger();

            foreach (var file in files)
            {
                var inputBytes = new ByteArrayInputBytes(file);
                var coreScanner = new CoreTokenScanner(inputBytes);

                var version = FileHeaderParser.Parse(coreScanner, true, Log);

                var bruteForceSearcher = new BruteForceSearcher(inputBytes);
                var xrefValidator = new XrefOffsetValidator(Log);
                var objectChecker = new XrefCosOffsetChecker(Log, bruteForceSearcher);

                var crossReferenceParser = new CrossReferenceParser(Log, xrefValidator, objectChecker, new Parser.Parts.CrossReference.CrossReferenceStreamParser(FilterProvider));

                var crossReferenceOffset = FileTrailerParser.GetFirstCrossReferenceOffset(inputBytes, coreScanner, isLenientParsing);

                var objectLocations = bruteForceSearcher.GetObjectLocations();

                CrossReferenceTable crossReference = null;

                var locationProvider = new ObjectLocationProvider(() => crossReference, bruteForceSearcher);
                // I'm not using the BruteForceObjectLocationProvider because, the offset that it give are wrong by +2
                // var locationProvider = new BruteForcedObjectLocationProvider(objectLocations);

                var pdfScanner = new PdfTokenScanner(inputBytes, locationProvider, FilterProvider, NoOpEncryptionHandler.Instance);

                crossReference = crossReferenceParser.Parse(inputBytes, isLenientParsing, crossReferenceOffset, version.OffsetInFile, pdfScanner, coreScanner);

                var trailerDictionary = crossReference.Trailer;

                var (trailerRef, catalogDictionaryToken) = ParseCatalog(crossReference, pdfScanner, out var encryptionDictionary);

                if (encryptionDictionary != null)
                {
                    // TODO: Find option of how to pass password for the documents...
                    throw new PdfDocumentEncryptedException("Unable to merge document with password");
                    // pdfScanner.UpdateEncryptionHandler(new EncryptionHandler(encryptionDictionary, trailerDictionary, new[] { string.Empty }));
                }

                var objectsLocation = bruteForceSearcher.GetObjectLocations();

                var root = pdfScanner.Get(trailerDictionary.Root);

                var tokens = new List<IToken>();

                pdfScanner.Seek(0);
                while (pdfScanner.MoveNext())
                {
                    tokens.Add(pdfScanner.CurrentToken);
                }

                if (!(tokens.Count == objectLocations.Count))
                {
                    // Do we really need to check this?
                    throw new PdfDocumentFormatException("Something whent wrong while reading file");
                }

                var documentCatalog = CatalogFactory.Create(crossReference.Trailer.Root, catalogDictionaryToken, pdfScanner, isLenientParsing);

                documentBuilder.AppendDocument(documentCatalog, pdfScanner);
            }

            return documentBuilder.Build();
        }

        // This method is a basically a copy of the method UglyToad.PdfPig.Parser.PdfDocumentFactory.ParseTrailer()
        private static (IndirectReference, DictionaryToken) ParseCatalog(CrossReferenceTable crossReferenceTable,
            IPdfTokenScanner pdfTokenScanner,
            out EncryptionDictionary encryptionDictionary)
        {
            encryptionDictionary = null;

            if (crossReferenceTable.Trailer.EncryptionToken != null)
            {
                if (!DirectObjectFinder.TryGet(crossReferenceTable.Trailer.EncryptionToken, pdfTokenScanner,
                    out DictionaryToken encryptionDictionaryToken))
                {
                    throw new PdfDocumentFormatException($"Unrecognized encryption token in trailer: {crossReferenceTable.Trailer.EncryptionToken}.");
                }

                encryptionDictionary = EncryptionDictionaryFactory.Read(encryptionDictionaryToken, pdfTokenScanner);
            }

            var rootDictionary = DirectObjectFinder.Get<DictionaryToken>(crossReferenceTable.Trailer.Root, pdfTokenScanner);

            if (!rootDictionary.ContainsKey(NameToken.Type))
            {
                rootDictionary = rootDictionary.With(NameToken.Type, NameToken.Catalog);
            }

            return (crossReferenceTable.Trailer.Root, rootDictionary);
        }

        private class DocumentMerger
        {
            private MemoryStream Memory = new MemoryStream();

            private readonly BuilderContext Context = new BuilderContext();

            private readonly List<IndirectReferenceToken> DocumentPages = new List<IndirectReferenceToken>();

            private IndirectReferenceToken RootPagesIndirectReference;

            public DocumentMerger()
            {
                var reserved = Context.ReserveNumber();
                RootPagesIndirectReference = new IndirectReferenceToken(new IndirectReference(reserved, 0));

                WriteHeaderToStream();
            }

            private void WriteHeaderToStream()
            {
                // Copied from UglyToad.PdfPig.Writer.PdfDocumentBuilder
                WriteString("%PDF-1.7", Memory);

                // Files with binary data should contain a 2nd comment line followed by 4 bytes with values > 127
                Memory.WriteText("%");
                Memory.WriteByte(169);
                Memory.WriteByte(205);
                Memory.WriteByte(196);
                Memory.WriteByte(210);
                Memory.WriteNewLine();
            }
 
            public void AppendDocument(Catalog documentCatalog, IPdfTokenScanner tokenScanner)
            {
                if (Memory == null)
                {
                    throw new ObjectDisposedException("Merger disposed already");
                }

                var pagesReference = CopyPagesTree(documentCatalog.PageTree, RootPagesIndirectReference, tokenScanner);
                DocumentPages.Add(new IndirectReferenceToken(pagesReference.Number));
            }

            private ObjectToken CopyPagesTree(PageTreeNode treeNode, IndirectReferenceToken treeParentReference, IPdfTokenScanner tokenScanner)
            {
                Debug.Assert(!treeNode.IsPage);

                var currentNodeReserved = Context.ReserveNumber();
                var currentNodeReference = new IndirectReferenceToken(new IndirectReference(currentNodeReserved, 0));

                var pageReferences = new List<IndirectReferenceToken>();
                foreach (var pageNode in treeNode.Children)
                {
                    IndirectReference newEntry;
                    if (!pageNode.IsPage)
                        newEntry = CopyPagesTree(pageNode, currentNodeReference, tokenScanner).Number;
                    else 
                        newEntry = CopyPageNode(pageNode, currentNodeReference, tokenScanner).Number;

                    pageReferences.Add(new IndirectReferenceToken(newEntry));
                }

                var contentDictionary = new Dictionary<NameToken, IToken>
                {
                    { NameToken.Type, NameToken.Pages },
                    { NameToken.Kids, new ArrayToken(pageReferences) },
                    { NameToken.Count, new NumericToken(pageReferences.Count) },
                    { NameToken.Parent, treeParentReference }
                };

                // Copy page tree properties, if there any that doesn't conflict with the new ones
                foreach(var set in treeNode.NodeDictionary.Data)
                {
                    var nameToken = NameToken.Create(set.Key);

                    // We don't want to override any value
                    if (contentDictionary.ContainsKey(nameToken))
                        continue;

                    contentDictionary.Add(NameToken.Create(nameToken), CopyToken(set.Value, tokenScanner));
                }

                var pagesToken = new DictionaryToken(contentDictionary);

                return Context.WriteObject(Memory, pagesToken, currentNodeReserved);
            }

            private ObjectToken CopyPageNode(PageTreeNode pageNode, IndirectReferenceToken parentPagesObject, IPdfTokenScanner tokenScanner)
            {
                Debug.Assert(pageNode.IsPage);

                var pageDictionary = new Dictionary<NameToken, IToken>
                {
                    {NameToken.Parent, parentPagesObject},
                };

                foreach (var setPair in pageNode.NodeDictionary.Data)
                {
                    var name = setPair.Key;
                    var token = setPair.Value;

                    if (name == NameToken.Parent)
                    {
                        // Skip Parent token, since we have to reassign it
                        continue;
                    }

                    pageDictionary.Add(NameToken.Create(name), CopyToken(token, tokenScanner));
                }

                return Context.WriteObject(Memory, new DictionaryToken(pageDictionary));
            }

            /// <summary>
            /// The purpose of this method is to resolve indirect reference. That mean copy the reference's content to the new document's stream
            /// and replace the indirect reference with the correct/new one
            /// </summary>
            /// <param name="tokenToCopy">Token to inspect for reference</param>
            /// <param name="tokenScanner">scanner get the content from the original document</param>
            /// <returns>A copy of the token with all his content copied to the new document's stream</returns>
            private IToken CopyToken(IToken tokenToCopy, IPdfTokenScanner tokenScanner)
            {
                if (tokenToCopy is DictionaryToken dictionaryToken)
                {
                    var newContent = new Dictionary<NameToken, IToken>();
                    foreach (var setPair in dictionaryToken.Data)
                    {
                        var name = setPair.Key;
                        var token = setPair.Value;
                        newContent.Add(NameToken.Create(name), CopyToken(token, tokenScanner));
                    }

                    return new DictionaryToken(newContent);
                }
                else if (tokenToCopy is ArrayToken arrayToken)
                {
                    var newArray = new List<IToken>(arrayToken.Length);
                    foreach (var token in arrayToken.Data)
                    {
                        newArray.Add(CopyToken(token, tokenScanner));
                    }

                    return new ArrayToken(newArray);
                }
                else if (tokenToCopy is IndirectReferenceToken referenceToken)
                {
                    var tokenObject = DirectObjectFinder.Get<IToken>(referenceToken.Data, tokenScanner);

                    // Is this even a allowed?
                    Debug.Assert(!(tokenObject is IndirectReferenceToken));

                    var newToken = CopyToken(tokenObject, tokenScanner);
                    var objToken = Context.WriteObject(Memory, newToken);
                    return new IndirectReferenceToken(objToken.Number);
                }
                else if (tokenToCopy is StreamToken streamToken)
                {
                    //Note: Unnecessary
                    var properties = CopyToken(streamToken.StreamDictionary, tokenScanner) as DictionaryToken;
                    Debug.Assert(properties != null);
                    return new StreamToken(properties, new List<byte>(streamToken.Data));
                }
                else // Non Complex Token - BooleanToken, NumericToken, NameToken, Etc...
                {
                    // TODO: Should we do a deep copy of this tokens?
                    return tokenToCopy;
                }
            }

            public byte[] Build()
            {
                if (Memory == null)
                {
                    throw new ObjectDisposedException("Merger disposed already");
                }

                if (DocumentPages.Count < 1)
                {
                    throw new PdfDocumentFormatException("Empty document");
                }

                var pagesDictionary = new DictionaryToken(new Dictionary<NameToken, IToken>
                {
                    { NameToken.Type, NameToken.Pages },
                    { NameToken.Kids, new ArrayToken(DocumentPages) },
                    { NameToken.Count, new NumericToken(DocumentPages.Count) }
                });

                var pagesRef = Context.WriteObject(Memory, pagesDictionary, (int)RootPagesIndirectReference.Data.ObjectNumber);

                var catalog = new DictionaryToken(new Dictionary<NameToken, IToken>
                {
                    { NameToken.Type, NameToken.Catalog },
                    { NameToken.Pages, new IndirectReferenceToken(pagesRef.Number) }
                });

                var catalogRef = Context.WriteObject(Memory, catalog);

                TokenWriter.WriteCrossReferenceTable(Context.ObjectOffsets, catalogRef, Memory, null);
                
                var bytes = Memory.ToArray();

                Close();

                return bytes;
            }

            public void Close()
            {
                if (Memory == null)
                    return;

                Memory.Dispose();
                Memory = null;
            }

            // Note: This method is copied from UglyToad.PdfPig.Writer.PdfDocumentBuilder
            private static void WriteString(string text, MemoryStream stream, bool appendBreak = true)
            {
                var bytes = OtherEncodings.StringAsLatin1Bytes(text);
                stream.Write(bytes, 0, bytes.Length);
                if (appendBreak)
                {
                    stream.WriteNewLine();
                }
            }
        }

        // Currently unused becauase, brute force search give the wrong offset (+2)
        private class BruteForcedObjectLocationProvider : IObjectLocationProvider
        {
            private readonly Dictionary<IndirectReference, long> objectLocations;
            private readonly Dictionary<IndirectReference, ObjectToken> cache = new Dictionary<IndirectReference, ObjectToken>();

            public BruteForcedObjectLocationProvider(IReadOnlyDictionary<IndirectReference, long> objectLocations)
            {
                this.objectLocations = objectLocations.ToDictionary(x => x.Key, x => x.Value);
            }

            public bool TryGetOffset(IndirectReference reference, out long offset)
            {
                var result = objectLocations.TryGetValue(reference, out offset);
                //offset -= 2;
                return result;
            }

            public void UpdateOffset(IndirectReference reference, long offset)
            {
                objectLocations[reference] = offset;
            }

            public bool TryGetCached(IndirectReference reference, out ObjectToken objectToken)
            {
                return cache.TryGetValue(reference, out objectToken);
            }

            public void Cache(ObjectToken objectToken, bool force = false)
            {
                if (!TryGetOffset(objectToken.Number, out var offsetExpected) || force)
                {
                    cache[objectToken.Number] = objectToken;
                }

                if (offsetExpected != objectToken.Position)
                {
                    return;
                }

                cache[objectToken.Number] = objectToken;
            }
        }
    }
}