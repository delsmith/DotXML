using System;
using System.IO;
using DotXML;

namespace Test_DotXML
{
    class Test_DotXML
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing XML dot-notation parser");
            string sample = "example.xml";
            DotXML.DotXML doc = new DotXML.DotXML();
            doc.LoadXml(File.ReadAllText(sample));

            var root = doc.body;
            var v = root.Item("profile");
            v = root.Item("profile.message");
            v = root.Item("profile.message.type");
            v = root.Item("profile.message.conversion");
            v = root.Item("profile.message.conversion.point");
            v = root.Item("profile.message.conversion.point[1].name");
            v = root.Item("profile.message.conversion.point.name");
        }
    }
}
