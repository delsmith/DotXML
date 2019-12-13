using System;
using System.IO;
using Json_Decoder;
using DotXMLLib;
using EventHubReceiver;

namespace Test_DotXML
{
    class Test_DotXML
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing IOT EventHub Data extraction profile");

            // load the application settings
            string settingsFileName = "settings.IOT-EH.xml";
            dynamic settings = null;
            try { settings = (new DotXML(settingsFileName)).Item("Settings"); }
            catch (Exception e) { Console.WriteLine($"missing settings file\n{e.Message}"); }

            // load test data and unpack it
            string dataFile = args[0];
            string json_text = File.ReadAllText(dataFile);
            foreach( string msg_text in json_text.Split('\n'))
            {
                dynamic msg = Json.Parse(msg_text);
                PI_Extractor.PI_UploadTest(settings, msg);
            }
        }
    }
}
