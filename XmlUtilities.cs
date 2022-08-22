using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SimonOfHH.XliffFormat
{
    public static class XmlUtilities
    {
        public static XmlReaderSettings SafeXmlReaderSettings { get; } = new XmlReaderSettings()
        {
            #pragma warning disable CS8600
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = (XmlResolver)null
            #pragma warning restore
        };

        public static XmlReaderSettings CreateSafeXmlReaderSettings()
        {
            #pragma warning disable CS8600
            return new XmlReaderSettings()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = (XmlResolver)null
            };
            #pragma warning restore
        }

        public static XDocument GetSafeXDocument(Stream stream)
        {
            using (XmlReader reader = XmlReader.Create(stream, XmlUtilities.SafeXmlReaderSettings))
                return XDocument.Load(reader);
        }

        public static XmlDocument GetSafeXmlDocument(Stream stream)
        {
            using (XmlReader reader = XmlReader.Create(stream, XmlUtilities.SafeXmlReaderSettings))
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(reader);
                return xmlDocument;
            }
        }

        public static XmlDocument GetSafeXmlDocument(
          string fileContent,
          Encoding fileEncoding)
        {
            using (MemoryStream memoryStream = new MemoryStream(fileEncoding.GetBytes(fileContent)))
                return XmlUtilities.GetSafeXmlDocument((Stream)memoryStream);
        }
    }
}
