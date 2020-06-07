using System;
using System.Collections.Generic;
using System.IO;
using DotXMLLib;
using System.Xml;
using System.Text;
using System.Threading.Tasks;

namespace EventHubReceiver
{
    public class IoT_Profile: DotXML 
    {
        public IoT_Profile(string filename) { base.LoadXml(File.ReadAllText(filename)); }

        internal static List<string> unsupported = new List<string>();
        internal static Dictionary<string, dynamic> profiles = new Dictionary<string, dynamic>();

        public static dynamic Load_Profile(string name)
        {
            if (profiles.ContainsKey(name))
                return profiles[name];

            if (unsupported.Contains(name))
                return null;

            string folder = PI_Extractor.settings.Item("Profile.profile_folder");
            if (folder != "")
            {
                string profile_name = $"{folder}\\profile.{name}.xml";
                if (File.Exists(profile_name))
                {
                    dynamic profile = new IoT_Profile(profile_name).Item("profile");
                    profiles[name] = profile;
                    return profiles[name];
                }
                else
                {
                    unsupported.Add(name);
                }
            }
            return null;
        }

        public static dynamic Msg_Profile(XNode profile, string type)
        {
            // there may be one message profile or a List
            dynamic message = profile.Item("message");

            if(message.GetType().Name == "XNode")
            {
                if (message.Item("type") == type)
                    return message;
                else
                    return null;
            }
            else
            {
                // List (return matching type or 'null')
                foreach( dynamic msg in message)
                {
                    if (msg.Item("type") == type)
                        return msg;
                }
                return null;
            }
        }

        public static Dictionary<string,XNode> Point_Info(XNode msg)
        {
            Dictionary<string, XNode> result = new Dictionary<string, XNode> { };

            dynamic point = msg.Item("point");
            if (point.GetType().Name == "XNode")
            {
                result[point.Item("name")] = point;
            }
            else {
                foreach (dynamic pt in point)
                {
                    result[pt.Item("name")] = pt;
                }
            }
            return result;
        }
    }
}
