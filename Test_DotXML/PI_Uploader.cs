using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Json_Decoder;
using DotXMLLib;
using OSIsoft.AF;
using OSIsoft.AF.Data;
using OSIsoft.AF.Time;
using OSIsoft.AF.Asset;
using OSIsoft.AF.PI;

namespace EventHubReceiver
{
    public partial class PI_Extractor
    {
        /// PI_Upload: upload requested measurements to OSI-PI via AF-SDK API
        ///  upload logic:
        ///     extract the source, device type and dvice name
        ///     select the profile for the device type, loading it if needed (flag unsupported if no profile found)
        ///

        internal static DateTime unix0time = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        internal static int hourMark = -1;

        internal static DateTime ConvertTime(string time, string style)
        {
            switch (style) {
                case "unix":
                    long lTime = long.TryParse(time, out lTime) ? lTime : double.TryParse(time, out double dTime) ? (long)dTime : 0;
                    return unix0time.AddSeconds(lTime);
                default:
                    return DateTime.Parse(time);
            }
        }

        public static dynamic settings;
        public static void PI_UploadTest(dynamic _settings, dynamic msg)
        {
            settings = _settings;
            PI_Upload(null, msg);
        }

        public static void PI_Upload( PIServer server, dynamic msg )
        {
            // skip empty message
            if (msg == null)
                return;

            // fetch the device details
            string server_name = server != null ? server.Name : "AZSAW0225";

            string source = msg.Item("source");
            string device_type = msg.Item("device_type");
            string msg_type = msg.Item("message_type", "default");
            string device = msg.Item("device");

            // load the 'profile' to extract details from a message
            dynamic profile = IoT_Profile.Load_Profile(device_type); 
            if(profile == null)
            {
                // log error
                Console.WriteLine($"No profile for device type '{device_type}'");
                return;
            }

            dynamic msg_profile = IoT_Profile.Msg_Profile(profile, msg_type);
            if(msg_profile == null)
            {
                // log error
                Console.WriteLine($"No profile for message type '{msg_type}'");
                return;
            }

            string pointsource = msg_profile.Item("pointsource");
            string tag_pattern = msg_profile.Item("tag_pattern");
            string time_style = msg_profile.Item("message.time_style","unix");
            var time = msg.Item("time");
            DateTime sampleTime = ConvertTime( $"{time}", time_style );
            if (sampleTime.Hour != hourMark)
            {
                hourMark = sampleTime.Hour;
                Console.WriteLine($"{sampleTime} : {source}.{device_type}.{device}");
            }
            Dictionary<string,DotXML.XNode> pt_info = IoT_Profile.Point_Info(msg_profile);
            if(pt_info == null)
            {
                // log error
                return;
            }

            /*  extract scalar points from message and create transactions to update tags
             *  for each member of pt_info, 
             *      fetch the new value, 
             *      if present
             *          apply scaling
             *          compose the tag name
             *          build the PI Update request
             *          queue the PI update
             */
            foreach(dynamic point in pt_info.Values)
            {
                // build the tag name
                string field_name = point.Item("item");
                string alias = point.Item("alias");
                string tag = tag_pattern
                    .Replace("{source}", source)
                    .Replace("{device_type}", device_type)
                    .Replace("{device}", device)
                    .Replace("{point}", alias ?? field_name);
                tag = tag.ToUpper();
                var Value = msg.Item(field_name);
                //UploadPoint(point, Value, tag, pointsource, sampleTime);
                Console.WriteLine($"{sampleTime}  {tag,-56}   {Value}");
            }

            // if expecting a data vector, unpack it (e.g. WaterWatch)
            dynamic series_spec = msg_profile.Item("series");
            string series_name = series_spec?.Item("name") ?? "";
            dynamic samples = msg.Item(series_name);
            if( samples?.GetType().Name == "JArray")
            {
                foreach (dynamic sample in samples)
                {
                    time = sample.Item("time");
                    sampleTime = ConvertTime($"{time}", time_style);
                    foreach ( dynamic point in series_spec.Item("point"))
                    {
                        string field_name = point.Item("item");
                        string alias = point.Item("alias");
                        string tag = tag_pattern
                            .Replace("{source}", source)
                            .Replace("{device_type}", device_type)
                            .Replace("{device}", device)
                            .Replace("{point}", alias ?? field_name);
                        tag = tag.ToUpper();
                        var Value = sample.Item(field_name);
                        //UploadPoint(point, Value, tag, pointsource, sampleTime);
                        Console.WriteLine($"{sampleTime}  {tag,-56}   {Value}");
                    }
                }
            }

        } // end PI_Upload()
    }
}
