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

        //internal static void UploadPoint(dynamic point, dynamic Value, string tag, string pointsource, DateTime sampleTime)
        //{
        //    if (Value != null)
        //    {

        //        PIPoint p = null;
        //        try
        //        {
        //            p = PIPoint.FindPIPoint(server, tag);
        //        }
        //        catch (PIException e)
        //        {
        //            try
        //            {
        //                p = CreatePIPoint(server, pointsource, tag, point);
        //            }
        //            catch
        //            {
        //                Console.WriteLine($"Find/Create PI Point exception\n{e.Message}");
        //                return;
        //            }
        //        }

        //        AFValue value;
        //        try
        //        {
        //            switch (p.PointType)
        //            {
        //                case PIPointType.Float16:
        //                case PIPointType.Float32:
        //                case PIPointType.Float64:
        //                    value = new AFValue((double)Value);
        //                    break;
        //                case PIPointType.Int16:
        //                case PIPointType.Int32:
        //                    value = new AFValue((int)Value);
        //                    break;
        //                case PIPointType.Digital:
        //                case PIPointType.String:
        //                default:
        //                    value = new AFValue(Value.ToString());
        //                    break;
        //            }
        //            value.Timestamp = (AFTime)sampleTime;
        //        }
        //        catch (PIException e)
        //        {
        //            Console.WriteLine($"Conversion exception\n{e.Message}");
        //            return;
        //        }

        //        try
        //        {
        //            Console.WriteLine($"Tag: {tag:S32}, Time: {sampleTime}, Value: {Value}");
        //            p.UpdateValue(value, updateOption, bufferOption);
        //        }
        //        catch (PIException e)
        //        {
        //            Console.WriteLine($"PI Point Update exception\n{e.Message}");
        //            return;
        //        }
        //        finally { }
        //    }
        //}

        internal static PIPoint CreatePIPoint(PIServer server, string pointsource, string tag, DotXML.XNode pointSpec)
        {
            Dictionary<string, Object> attributes = new Dictionary<string, object> { };
            string pointtype = pointSpec.Item("type") ?? "Float32";
            attributes.Add("pointtype", pointtype);
            attributes.Add("engunits", pointSpec.Item("engunits") ?? "");
            attributes.Add("pointsource", pointsource ?? "");
            attributes.Add("descriptor", tag.Replace('.',' ').Replace('_',' '));
            attributes.Add("excdev", 0);
            attributes.Add("compdev", 0);
            attributes.Add("shutdown", 0);
            attributes.Add("location5", 1);
            if( pointtype.ToLower() == "digital")
                attributes.Add("digitalset", pointSpec.Item("digitalset") ?? "");
            try
            {
                PIPoint p = server.CreatePIPoint(tag, attributes);
                Console.WriteLine($"Point {p.Name} created");
                return p;
            }
            catch(Exception e)
            {
                Console.WriteLine($"PIPoint Creation failed\n{e.Message}");
                return null;
            }
        }

        internal class PI_Update
        {
            internal readonly PIPoint Point;
            internal readonly AFValues Values;

            internal PI_Update(PIPoint p, AFValues v) { Point = p; Values = v; }
        }

        internal static ConcurrentQueue<PI_Update> OutQueue = null;
        //static AFUpdateOption updateOption;
        //static AFBufferOption bufferOption;

        //private static Thread thUpload;
        //private static bool Start_Uploader(PIServer server)
        //{
        //    // don't start twice
        //    if (OutQueue != null)
        //        return false;

        //    // create the transaction queue
        //    OutQueue = new ConcurrentQueue<PI_Update> { };

        //    // start the Uploader thread
        //    thUpload = new Thread(() => Uploader(server));
        //    thUpload.Start();
        //    while (!thUpload.IsAlive) Thread.Sleep(100);

        //    // define OSI-PI update parameters
        //    bool compress = (settings.Item("compression").ToLower() == "true");
        //    updateOption = compress ? AFUpdateOption.Insert : AFUpdateOption.InsertNoCompression;
        //    bufferOption = AFBufferOption.BufferIfPossible;

        //    return true;
        //}

        //private static bool Stop_Uploader()
        //{
        //    if (thUpload.IsAlive)
        //    {
        //        // assuming listeners have shut down, wait for queue to empty
        //        while (OutQueue.Count > 0) Thread.Sleep(330);

        //        // wait for the worker thread to finish
        //        while (thUpload.IsAlive) Thread.Sleep(330); 

        //        Console.WriteLine("PI Uploader Stopped");
        //    }
        //    return true;
        //}

        //// This function is started in its own thread and runs as long as it is 'enabled'
        //private static void Uploader(PIServer server)
        //{
        //    int MaxQueue = 0;
        //    int Count = 0;
        //    while (enabled || OutQueue.Count > 0)
        //    {
        //        if (OutQueue.TryDequeue(out PI_Update next))
        //        {
        //            MaxQueue = Math.Max(OutQueue.Count, MaxQueue);
        //            Count++;
        //            PIPoint p = next.Point;
        //            AFValues v = next.Values;
        //            AFErrors<AFValue> result = p.UpdateValues(v, updateOption, bufferOption);
        //            Thread.Sleep(10);

        //            if (result != null && result.HasErrors)
        //                foreach (var item in result.Errors)
        //                    Console.WriteLine($"  AFValue '{item.Key}': {item.Value}");
        //        }
        //        else
        //        {
        //            Thread.Sleep(100);
        //        }
        //        Thread.Sleep(1);
        //    }
        //    Console.WriteLine($"Uploader exiting. Transactions: {Count:D6}, Max Queue: {MaxQueue:D4}");
        //}
    }
}
