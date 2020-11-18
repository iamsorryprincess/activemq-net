using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace ActivemqNet
{
    internal class Serializer
    {
        public static string SerializeMessage<TMessage>(TMessage message) where TMessage : class, new()
        {
            try
            {
                using var stringWriter = new StringWriterWithEncoding();
                var serializer = new XmlSerializer(message.GetType());
                serializer.Serialize(stringWriter, message);
                return stringWriter.ToString();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        
        public static object DeserializeMessage(string message, Type messageType)
        {
            try
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(message));
                var serializer = new XmlSerializer(messageType);
                return serializer.Deserialize(ms);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    internal class StringWriterWithEncoding : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}