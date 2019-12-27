﻿namespace UglyToad.PdfPig.Export.PAGE
{
    using System;
    using System.CodeDom.Compiler;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Xml.Serialization;

    public partial class PageXmlDocument
    {
        /// <remarks/>
        [EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [GeneratedCodeAttribute("xsd", "4.6.1055.0")]
        [SerializableAttribute()]
        [DebuggerStepThroughAttribute()]
        [DesignerCategoryAttribute("code")]
        [XmlTypeAttribute(Namespace = "http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15")]
        public class PageXmlLabels
        {

            private PageXmlLabel[] labelField;

            private string externalModelField;

            private string externalIdField;

            private string prefixField;

            private string commentsField;

            /// <summary>
            /// A semantic label / tag
            /// </summary>
            [XmlElementAttribute("Label")]
            public PageXmlLabel[] Labels
            {
                get
                {
                    return this.labelField;
                }
                set
                {
                    this.labelField = value;
                }
            }

            /// <summary>
            /// Reference to external model / ontology / schema
            /// </summary>
            [XmlAttributeAttribute("externalModel")]
            public string ExternalModel
            {
                get
                {
                    return this.externalModelField;
                }
                set
                {
                    this.externalModelField = value;
                }
            }

            /// <summary>
            /// E.g. an RDF resource identifier (to be used as subject or object of an RDF triple)
            /// </summary>
            [XmlAttributeAttribute("externalId")]
            public string ExternalId
            {
                get
                {
                    return this.externalIdField;
                }
                set
                {
                    this.externalIdField = value;
                }
            }

            /// <summary>
            /// Prefix for all labels (e.g. first part of an URI)
            /// </summary>
            [XmlAttributeAttribute("prefix")]
            public string Prefix
            {
                get
                {
                    return this.prefixField;
                }
                set
                {
                    this.prefixField = value;
                }
            }

            /// <remarks/>
            [XmlAttributeAttribute("comments")]
            public string Comments
            {
                get
                {
                    return this.commentsField;
                }
                set
                {
                    this.commentsField = value;
                }
            }
        }
    }
}
