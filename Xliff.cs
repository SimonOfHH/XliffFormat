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
            var stream = new StreamReader(filename, System.Text.Encoding.UTF8).BaseStream;
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
        public void CleanXliff()
        {
            var newEntries = new List<XliffFileBodyGroupTransunit>();
            foreach (var entry in this.file.body.group.transunit)
            {
                #pragma warning disable CS8625
                entry.alobjecttarget = null;
                #pragma warning restore CS8625
                bool entryOk = true;
                entryOk = entryOk && !String.IsNullOrEmpty(entry.source);
                if (entryOk)
                    newEntries.Add(entry);
            }
            this.file.body.group.transunit = newEntries.ToArray();
        }
        public void NormalizeIds()
        {
            foreach (var entry in this.file.body.group.transunit)
            {
                entry.id = entry.id.ToLower();
            }
        }
        public void ReduceToDistinct()
        {
            this.file.body.group.transunit = this.file.body.group.transunit.ToList().GroupBy(x => x.source).Select(g => g.First()).ToArray();
        }
        public void SaveEntriesAsCsv(string filename)
        {
            SaveEntriesAsCsv(filename, false, false);
        }

        public void SaveEntriesAsCsv(string filename, bool distinct, bool splitWords)
        {
            var entries = new List<string>();
            foreach (var entry in this.file.body.group.transunit)
            {
                entries.Add(entry.source);
            }
            if (splitWords)
            {
                var newEntries = new List<string>();
                foreach (var entry in entries)
                {
                    var split = entry.Split(' ');
                    foreach (var element in split)
                    {
                        if (!String.IsNullOrEmpty(element) && (!new[] { ",", ".", ":", ";", "@", "%", "?", "!" }.Contains(element)))
                        {
                            newEntries.Add(element);
                        }
                    }
                }
                entries = newEntries;
            }
            if (distinct) entries = entries.Distinct().ToList();
            using (var writer = new StreamWriter(filename))
            {
                foreach (var entry in entries)
                {
                    writer.WriteLine(entry);
                }
                writer.Close();
            }
        }
        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="xliffs">TODO</param>
        public static Xliff Merge(Xliff[] xliffs)
        {
            if (xliffs == null)
            #pragma warning disable CS8603
                return null;
            #pragma warning restore CS8603

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
        /// Adds or updates all "trans-unit"-entries in this object with the corresponding entries from "source" where target state = "translated"
        /// </summary>
        /// <param name="source">The Xliff-object to get the translated "trans-unit"-entries from.</param>
        public void UpsertTranslatedEntries(Xliff source)
        {
            foreach (var entry in source.file.body.group.transunit.Where(x => x.target.state == "translated"))
            {
                var targetEntry = this.file.body.group.transunit.FirstOrDefault(x => x.id_Clean == entry.id_Clean);
                if (targetEntry != null)
                {
                    var index = this.file.body.group.transunit.ToList().IndexOf(targetEntry);
                    this.file.body.group.transunit[index] = entry;
                }
                else
                {
                    var list = this.file.body.group.transunit.ToList();
                    list.Add(entry);
                    this.file.body.group.transunit = list.ToArray();
                }
            }
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="compare">TODO</param>
        public void RemoveEntriesThatExistInOtherXliff(Xliff compare)
        {
            var newEntries = new List<XliffFileBodyGroupTransunit>();
            var compareList = compare.file.body.group.transunit.ToList();
            var compareIdList = compareList.Select(x => x.id_Clean).ToList();
            foreach (var entry in this.file.body.group.transunit)
            {
                if (!compareIdList.Contains(entry.id_Clean))
                {
                    newEntries.Add(entry);
                }
            }
            this.file.body.group.transunit = newEntries.ToArray();
        }

        public void ReplaceEntriesWithEntriesFromOtherXliff(Xliff compare)
        {
            var sourceList = this.file.body.group.transunit.ToList();
            var compareList = compare.file.body.group.transunit.ToList();
            var compareIdList = compareList.Select(x => x.id_Clean).ToList();
            foreach (var entry in this.file.body.group.transunit)
            {
                if (compareIdList.Contains(entry.id_Clean))
                {
                    sourceList[sourceList.IndexOf(entry)] = compareList.First(x => x.id_Clean == entry.id_Clean);
                }
            }
            this.file.body.group.transunit = sourceList.ToArray();
        }
        public void ReplaceEntriesWithEntriesFromOtherXliffBasedOnValues(Xliff compare, bool onlyNotTranslated = true)
        {
            var sourceList = this.file.body.group.transunit.ToList();
            //if (onlyNotTranslated)
            //    sourceList = sourceList.Where(x => x.target.state != "translated").ToList();
            var compareList = compare.file.body.group.transunit.ToList();
            var compareIdList = compareList.Select(x => x.id_Clean).ToList();
            foreach (var entry in this.file.body.group.transunit)
            {
                if (onlyNotTranslated)
                    if (entry.target.state == "translated")
                        continue;
                var result = compareList.FirstOrDefault(x => x.source == entry.source);
                if (result != null)
                {
                    //sourceList[sourceList.IndexOf(entry)].source = result.source;
                    sourceList[sourceList.IndexOf(entry)].target = result.target;
                }
            }
            this.file.body.group.transunit = sourceList.ToArray();
        }
        public void ReplaceSourceWithTargetFronItherXliff(Xliff compare)
        {
            var sourceList = this.file.body.group.transunit.ToList();
            var compareList = compare.file.body.group.transunit.ToList();
            var compareIdList = compareList.Select(x => x.id_Clean).ToList();
            foreach (var entry in this.file.body.group.transunit)
            {
                if (compareIdList.Contains(entry.id_Clean))
                {
                    sourceList[sourceList.IndexOf(entry)].source = compareList.First(x => x.id_Clean == entry.id_Clean).target.Value;
                }
            }
            this.file.body.group.transunit = sourceList.ToArray();
        }
        public void ReplaceSourceWithSourceFronItherXliff(Xliff compare)
        {
            var sourceList = this.file.body.group.transunit.ToList();
            var compareList = compare.file.body.group.transunit.ToList();
            var compareIdList = compareList.Select(x => x.id_Clean).ToList();
            foreach (var entry in this.file.body.group.transunit)
            {
                if (compareIdList.Contains(entry.id_Clean))
                {
                    sourceList[sourceList.IndexOf(entry)].source = compareList.First(x => x.id_Clean == entry.id_Clean).source;
                }
            }
            this.file.body.group.transunit = sourceList.ToArray();
        }
        public void AddTargetFromOtherXliff(Xliff compare)
        {
            var sourceList = this.file.body.group.transunit.ToList();
            var sourceIdList = sourceList.Select(x => x.id_Clean.ToString()).ToList();
            var compareList = compare.file.body.group.transunit.ToList();
            var compareIdList = compareList.Select(x => x.id_Clean.ToString()).ToList();
            foreach (var entry in this.file.body.group.transunit)
            {
                if (compareIdList.Contains(entry.id_Clean))
                {
                    sourceList[sourceList.IndexOf(entry)].target = compareList.First(x => x.id_Clean == entry.id_Clean).target;
                }
            }
            this.file.body.group.transunit = sourceList.ToArray();
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
            #pragma warning disable CS8603
            if (ReferenceEquals(source, null)) return default;
            #pragma warning restore CS8603

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
        public string id_Clean
        {
            get { return this.id.ToLower(); }
        }
        public bool ShouldSerializeid_Clean()
        {
            return false;
        }
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
