﻿namespace UglyToad.PdfPig.Writer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
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
    using Exceptions;
    using System.Linq;

    /// <summary>
    /// Merges PDF documents into each other.
    /// </summary>
    public static class PdfMerger
    {
        private static readonly ILog Log = new NoOpLog();

        private static readonly IFilterProvider FilterProvider = DefaultFilterProvider.Instance;

        /// <summary>
        /// Merge two PDF documents together with the pages from <paramref name="file1"/> followed by <paramref name="file2"/>.
        /// </summary>
        public static byte[] Merge(string file1, string file2)
        {
            using var output = new MemoryStream();
            Merge(file1, file2, output);
            return output.ToArray();
        }

        /// <summary>
        /// Merge two PDF documents together with the pages from <paramref name="file1"/> followed by <paramref name="file2"/> into the output stream.
        /// </summary>
        public static void Merge(string file1, string file2, Stream output)
        {
            _ = file1 ?? throw new ArgumentNullException(nameof(file1));
            _ = file2 ?? throw new ArgumentNullException(nameof(file2));

            using var stream1 = new StreamInputBytes(File.OpenRead(file1));
            using var stream2 = new StreamInputBytes(File.OpenRead(file2));
            Merge(new[] { stream1, stream2 }, output);
        }

        /// <summary>
        /// Merge multiple PDF documents together with the pages in the order the file paths are provided.
        /// </summary>
        public static byte[] Merge(params string[] filePaths)
        {
            using var output = new MemoryStream();
            Merge(output, filePaths);
            return output.ToArray();
        }

        /// <summary>
        /// Merge multiple PDF documents together with the pages in the order the file paths are provided into the output stream
        /// </summary>
        public static void Merge(Stream output, params string[] filePaths)
        {
            var streams = new List<StreamInputBytes>(filePaths.Length);
            try
            {
                for (var i = 0; i < filePaths.Length; i++)
                {
                    var filePath = filePaths[i] ?? throw new ArgumentNullException(nameof(filePaths), $"Null filepath at index {i}.");
                    streams.Add(new StreamInputBytes(File.OpenRead(filePath), true));
                }

                Merge(streams, output);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream.Dispose();
                }
            }
        }

        /// <summary>
        /// Merge the set of PDF documents.
        /// </summary>
        public static byte[] Merge(IReadOnlyList<byte[]> files)
        {
            _ = files ?? throw new ArgumentNullException(nameof(files));

            using var output = new MemoryStream();
            Merge(files.Select(f => new ByteArrayInputBytes(f)).ToArray(), output);
            return output.ToArray();
        }

        /// <summary>
        /// Merge the set of PDF documents into the output stream
        /// The caller must manage disposing the stream. The created PdfDocument will not dispose the stream.
        /// <param name="streams">
        /// A list of streams for the file contents, this must support reading and seeking.
        /// </param>
        /// <param name="output">Must be writable</param>
        /// </summary>
        public static void Merge(IReadOnlyList<Stream> streams, Stream output)
        {
            _ = streams ?? throw new ArgumentNullException(nameof(streams));
            _ = output ?? throw new ArgumentNullException(nameof(output));

            Merge(streams.Select(f => new StreamInputBytes(f, false)).ToArray(), output);
        }

        private static void Merge(IReadOnlyList<IInputBytes> files, Stream output)
        {
            const bool isLenientParsing = false;

            var documentBuilder = new DocumentMerger(output);

            foreach (var inputBytes in files)
            {
                var coreScanner = new CoreTokenScanner(inputBytes);

                var version = FileHeaderParser.Parse(coreScanner, isLenientParsing, Log);

                var crossReferenceParser = new CrossReferenceParser(Log, new XrefOffsetValidator(Log),
                    new Parser.Parts.CrossReference.CrossReferenceStreamParser(FilterProvider));

                CrossReferenceTable crossReference = null;

                // ReSharper disable once AccessToModifiedClosure
                var locationProvider = new ObjectLocationProvider(() => crossReference, inputBytes);

                var pdfScanner = new PdfTokenScanner(inputBytes, locationProvider, FilterProvider, NoOpEncryptionHandler.Instance);

                var crossReferenceOffset = FileTrailerParser.GetFirstCrossReferenceOffset(inputBytes, coreScanner, isLenientParsing);
                crossReference = crossReferenceParser.Parse(inputBytes, isLenientParsing, crossReferenceOffset, version.OffsetInFile, pdfScanner, coreScanner);

                var catalogDictionaryToken = ParseCatalog(crossReference, pdfScanner, out var encryptionDictionary);
                if (encryptionDictionary != null)
                {
                    throw new PdfDocumentEncryptedException("Unable to merge document with password");
                }

                var documentCatalog = CatalogFactory.Create(crossReference.Trailer.Root, catalogDictionaryToken, pdfScanner, isLenientParsing);

                documentBuilder.AppendDocument(documentCatalog, version.Version, pdfScanner);
            }

            documentBuilder.Build();
        }

        // This method is a basically a copy of the method UglyToad.PdfPig.Parser.PdfDocumentFactory.ParseTrailer()
        private static DictionaryToken ParseCatalog(CrossReferenceTable crossReferenceTable,
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

            return rootDictionary;
        }

        private class DocumentMerger
        {
            private const decimal DefaultVersion = 1.2m;

            private readonly PdfStreamWriter context;
            private readonly List<IndirectReferenceToken> pagesTokenReferences = new List<IndirectReferenceToken>();
            private readonly IndirectReferenceToken rootPagesReference;

            private decimal currentVersion = DefaultVersion;
            private int pageCount = 0;
            
            private readonly Dictionary<IndirectReferenceToken, IndirectReferenceToken> referencesFromDocument =
                new Dictionary<IndirectReferenceToken, IndirectReferenceToken>();

            public DocumentMerger(Stream baseStream)
            {
                context = new PdfStreamWriter(baseStream, false);
                rootPagesReference = context.ReserveNumberToken();
            }
 
            public void AppendDocument(Catalog documentCatalog, decimal version, IPdfTokenScanner tokenScanner)
            {
                currentVersion = Math.Max(version, currentVersion);

                var (pagesReference, count) = CopyPagesTree(documentCatalog.PageTree, rootPagesReference, tokenScanner);
                pageCount += count;
                pagesTokenReferences.Add(pagesReference);

                referencesFromDocument.Clear();
            }

            public void Build()
            {
                if (pagesTokenReferences.Count < 1)
                {
                    throw new PdfDocumentFormatException("Empty document");
                }

                var pagesDictionary = new DictionaryToken(new Dictionary<NameToken, IToken>
                {
                    { NameToken.Type, NameToken.Pages },
                    { NameToken.Kids, new ArrayToken(pagesTokenReferences) },
                    { NameToken.Count, new NumericToken(pageCount) }
                });

                var pagesRef = context.WriteToken( pagesDictionary, (int)rootPagesReference.Data.ObjectNumber);

                var catalog = new DictionaryToken(new Dictionary<NameToken, IToken>
                {
                    { NameToken.Type, NameToken.Catalog },
                    { NameToken.Pages, pagesRef }
                });

                var catalogRef = context.WriteToken(catalog);
                
                context.Flush(currentVersion, catalogRef);

                Close();
            }

            public void Close()
            {
                context.Dispose();
            }

            private (IndirectReferenceToken, int) CopyPagesTree(PageTreeNode treeNode, IndirectReferenceToken treeParentReference, IPdfTokenScanner tokenScanner)
            {
                Debug.Assert(!treeNode.IsPage);
                
                var currentNodeReference = context.ReserveNumberToken();

                var pageReferences = new List<IndirectReferenceToken>();
                var nodeCount = 0;
                foreach (var pageNode in treeNode.Children)
                {
                    IndirectReferenceToken newEntry;
                    if (!pageNode.IsPage)
                    {
                        var count = 0;
                        (newEntry, count) = CopyPagesTree(pageNode, currentNodeReference, tokenScanner);
                        nodeCount += count;
                    }
                    else
                    {
                        newEntry = CopyPageNode(pageNode, currentNodeReference, tokenScanner);
                        ++nodeCount;
                    }
                    
                    pageReferences.Add(newEntry);
                }

                var newPagesNode = new Dictionary<NameToken, IToken>
                {
                    { NameToken.Type, NameToken.Pages },
                    { NameToken.Kids, new ArrayToken(pageReferences) },
                    { NameToken.Count, new NumericToken(nodeCount) },
                    { NameToken.Parent, treeParentReference }
                };

                foreach (var pair in treeNode.NodeDictionary.Data)
                {
                    if (IgnoreKeyForPagesNode(pair))
                    {
                        continue;
                    }

                    newPagesNode[NameToken.Create(pair.Key)] = CopyToken(pair.Value, tokenScanner);
                }

                var pagesDictionary = new DictionaryToken(newPagesNode);

                return (context.WriteToken(pagesDictionary, (int)currentNodeReference.Data.ObjectNumber), nodeCount);
            }

            private IndirectReferenceToken CopyPageNode(PageTreeNode pageNode, IndirectReferenceToken parentPagesObject, IPdfTokenScanner tokenScanner)
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

                return context.WriteToken(new DictionaryToken(pageDictionary));
            }
            
            /// <summary>
            /// The purpose of this method is to resolve indirect reference. That mean copy the reference's content to the new document's stream
            /// and replace the indirect reference with the correct/new one
            /// </summary>
            /// <param name="tokenToCopy">Token to inspect for reference</param>
            /// <param name="tokenScanner">scanner get the content from the original document</param>
            /// <returns>A reference of the token that was copied. With all the reference updated</returns>
            private IToken CopyToken(IToken tokenToCopy, IPdfTokenScanner tokenScanner)
            {
                // This token need to be deep copied, because they could contain reference. So we have to update them.
                switch (tokenToCopy)
                {
                    case DictionaryToken dictionaryToken:
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
                    case ArrayToken arrayToken:
                    {
                        var newArray = new List<IToken>(arrayToken.Length);
                        foreach (var token in arrayToken.Data)
                        {
                            newArray.Add(CopyToken(token, tokenScanner));
                        }

                        return new ArrayToken(newArray);
                    }
                    case IndirectReferenceToken referenceToken:
                    {
                        if (referencesFromDocument.TryGetValue(referenceToken, out var newReferenceToken))
                        {
                            return newReferenceToken;
                        }
                        
                        var tokenObject = DirectObjectFinder.Get<IToken>(referenceToken.Data, tokenScanner);

                        Debug.Assert(!(tokenObject is IndirectReferenceToken));

                        var newToken = CopyToken(tokenObject, tokenScanner);
                        newReferenceToken = context.WriteToken(newToken);
                        
                        referencesFromDocument.Add(referenceToken, newReferenceToken);
                        
                        return newReferenceToken;
                    }
                    case StreamToken streamToken:
                    {
                        var properties = CopyToken(streamToken.StreamDictionary, tokenScanner) as DictionaryToken;
                        Debug.Assert(properties != null);

                        var bytes = streamToken.Data;
                        return new StreamToken(properties, bytes);
                    }
                    
                    case ObjectToken _:
                    {
                        // Since we don't write token directly to the stream.
                        // We can't know the offset. Therefore the token would be invalid
                        throw new NotSupportedException("Copying a Object token is not supported");
                    }
                }

                return tokenToCopy;
            }

            private static bool IgnoreKeyForPagesNode(KeyValuePair<string, IToken> token)
            {
                return string.Equals(token.Key, NameToken.Type.Data, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token.Key, NameToken.Kids.Data, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token.Key, NameToken.Count.Data, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token.Key, NameToken.Parent.Data, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}