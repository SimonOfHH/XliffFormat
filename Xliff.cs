using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SimonOfHH.XliffFormat
{
#pragma warning disable CS8618

    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [XmlRoot("xliff", IsNullable = false, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class Xliff
    {
        public string SourceFilename { get; set; }
        public bool ShouldSerializeSourceFilename()
        {
            return false;
        }
        public XliffFile file { get; set; }
        [XmlAttribute]
        public Decimal version { get; set; }
        [XmlAttributeAttribute("schemaLocation", Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
        public string xsiSchemaLocation = "urn:oasis:names:tc:xliff:document:1.2 xliff-core-1.2-transitional.xsd";

        public void Serialize(string filename)
        {
            var xmlSerializer = new XmlSerializer(typeof(Xliff));
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };
            using (var writer = XmlWriter.Create(filename, settings))
            {
                xmlSerializer.Serialize(writer, this);
                writer.Close();
            }
        }
        public void Serialize(string filename, int maxEntriesPerFile)
        {
            if (this.file.body.group.transunit.Count() <= maxEntriesPerFile)
            {
                Serialize(filename);
                return;
            }
            Xliff split = Clone(this);
            var entries = new List<XliffFileBodyGroupTransunit>();
            int counter = 0;
            foreach (var entry in this.file.body.group.transunit)
            {
                entries.Add(entry);
                if (entries.Count() == maxEntriesPerFile)
                {
                    counter++;
                    split.file.body.group.transunit = entries.ToArray();
                    entries = new List<XliffFileBodyGroupTransunit>();
                    // Write new object to file
                    string newFilename = split.SourceFilename.Insert(split.SourceFilename.LastIndexOf("."), String.Format("_{0}", counter));
                    split.Serialize(newFilename);
                }
            }
            if (entries.Count() > 0)
            {
                counter++;
                split.file.body.group.transunit = entries.ToArray();
                // Write new object to file
                string newFilename = split.SourceFilename.Insert(split.SourceFilename.LastIndexOf("."), String.Format("_{0}", counter));
                split.Serialize(newFilename);
            }
        }
        public static Xliff Deserialize(string filename)
        {
            var stream = new StreamReader(filename).BaseStream;
            var xliff = Deserialize(stream);
            xliff.SourceFilename = filename;
            if (File.ReadAllText(filename).Contains("maxwidth=\"0\""))
                xliff.file.body.group.transunit.ToList().ForEach(c => c.IncludeMaxWithField = true);
            return xliff;
        }
        public static Xliff Deserialize(Stream inputStream)
        {
#pragma warning disable CS8600, CS8603
            var xmlReaderSettings = new XmlReaderSettings()
            {
#pragma warning disable CS8600
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = (XmlResolver)null
            };
            //using (XmlReader xmlReader = XmlReader.Create(inputStream, XmlUtilities.SafeXmlReaderSettings))
            using (XmlReader xmlReader = XmlReader.Create(inputStream, xmlReaderSettings))
                return (Xliff)new XmlSerializer(typeof(Xliff)).Deserialize(xmlReader);
#pragma warning restore CS8600, CS8603
        }
        public static Xliff Merge(Xliff[] xliffs)
        {
            if (xliffs == null)
                return null;

            var entries = new List<XliffFileBodyGroupTransunit>();
            foreach (var xliff in xliffs)
            {
                foreach (var transunit in xliff.file.body.group.transunit)
                {
                    entries.Add(transunit);
                }
            }
            var targetXliff = xliffs[0];
            targetXliff.file.body.group.transunit = entries.ToArray();
            return targetXliff;
        }

        /// <summary>
        /// Perform a deep copy of the object via serialization.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>A deep copy of the object.</returns>
        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", nameof(source));
            }

            // Don't serialize a null object, simply return the default for that object
            if (ReferenceEquals(source, null)) return default;

            Stream stream = new MemoryStream();
            IFormatter formatter = new BinaryFormatter();
#pragma warning disable SYSLIB0011
            formatter.Serialize(stream, source);
            stream.Seek(0, SeekOrigin.Begin);
            return (T)formatter.Deserialize(stream);
#pragma warning restore SYSLIB0011
        }

    }
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFile
    {
        public XliffFileHeader header { get; set; }
        public XliffFileBody body { get; set; }
        [XmlAttribute]
        public string datatype { get; set; }
        [XmlAttribute("source-language")]
        public string sourcelanguage { get; set; }
        [XmlAttribute("target-language")]
        public string targetlanguage { get; set; }
        [XmlAttribute]
        public string original { get; set; }
        [XmlAttribute("tool-id")]
        public string toolid { get; set; }
        [XmlAttribute("product-name")]
        public string productname { get; set; }
        [XmlAttribute("product-version")]
        public string productversion { get; set; }
        [XmlAttribute("build-num")]
        public string buildnum { get; set; }
    }
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFileBody
    {
        public XliffFileBodyGroup group { get; set; }
    }

    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFileBodyGroup
    {
        [XmlElement("trans-unit")]
        public XliffFileBodyGroupTransunit[] transunit { get; set; }
        [XmlAttribute]
        public string id { get; set; }
    }
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFileBodyGroupTransunit
    {
        private ushort maxwidthField;
        public bool IncludeMaxWithField { get; set; }
        public bool ShouldSerializeIncludeMaxWithField()
        {
            return false;
        }
        public string source { get; set; }
        public XliffFileBodyGroupTransunitTarget target { get; set; }
        [XmlElement("note")]
        public XliffFileBodyGroupTransunitNote[] note { get; set; }
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public ushort maxwidth
        {
            set { maxwidthField = value; }
            get { return maxwidthField; }
        }
        public bool ShouldSerializemaxwidth()
        {
            return (maxwidthField != 0) || IncludeMaxWithField;
        }
        [XmlAttribute("size-unit")]
        public string sizeunit { get; set; }
        [XmlAttribute]
        public string translate { get; set; }
        [XmlAttribute("al-object-target")]
        public string alobjecttarget { get; set; }
        [XmlAttribute(Form = XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string space { get; set; }
    }
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFileBodyGroupTransunitNote
    {
        [XmlAttribute]
        public string from { get; set; }
        [XmlAttribute]
        public string annotates { get; set; }
        [XmlAttribute]
        public byte priority { get; set; }
        [XmlText]
        public string Value { get; set; }
    }

    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFileBodyGroupTransunitTarget
    {
        [XmlAttribute]
        public string state { get; set; }
        [XmlAttribute("state-qualifier")]
        public string statequalifier { get; set; }
        [XmlText]
        public string Value { get; set; }
    }
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFileHeader
    {
        public XliffFileHeaderTool tool { get; set; }
    }

    [DebuggerStepThrough]
    [XmlType(AnonymousType = true, Namespace = "urn:oasis:names:tc:xliff:document:1.2")]
    [Serializable]
    public class XliffFileHeaderTool
    {
        [XmlAttribute("tool-id")]
        public string toolid { get; set; }
        [XmlAttribute("tool-name")]
        public string toolname { get; set; }
        [XmlAttribute("tool-version")]
        public string toolversion { get; set; }
        [XmlAttribute("tool-company")]
        public string toolcompany { get; set; }
    }
#pragma warning restore
}
