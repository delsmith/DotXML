using System;
using DotXML;

namespace Test_DotXML
{
    class Test_DotXML
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string sample = "<profile><message id=\"msg_id\"><type>default</type><pointsource>IOT_TEST_DATA</pointsource><tag_pattern>//{server}/IOT.{device}.{point}.result</tag_pattern><time_style>unix</time_style><interval>-10m</interval><!-- or --><interval>//{server}/IOT.{device}.interval</interval><conversion><point><name>input_001</name><c0>125</c0><c1>0.6218</c1></point><point><name>input_002</name><c0>88</c0><c1>-1.62</c1></point></conversion></message></profile>";
            DotXML.DotXML doc = new DotXML.DotXML();
            doc.LoadXml(sample);
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
