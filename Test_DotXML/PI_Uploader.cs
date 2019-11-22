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
                    long lTime = (long.TryParse(time, out lTime)) ? lTime : 0;
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
            // fetch the device details
            string server_name = server != null ? server.Name : "AZSAW0225";

            string source = msg.Item("source");
            string device_type = msg.Item("device_type");
            string msg_type = msg.Item("msg_type", "default");
            string device = msg.Item("device");

            // load the 'profile' to extract details from a message
            dynamic profile = IoT_Profile.Load_Profile(device_type); 
            if(profile == null)
            {
                // log error
                return;
            }

            dynamic msg_profile = IoT_Profile.Msg_Profile(profile, msg_type);
            if(msg_profile == null)
            {
                // log error
                return;
            }

            Dictionary<string,DotXML.XNode> pt_info = IoT_Profile.Point_Info(msg_profile);
            if(pt_info == null)
            {
                // log error
                return;
            }

            string time_style = msg_profile.Item("message.time_style","unix");
            var time = msg.Item("time");
            DateTime sampleTime = ConvertTime( $"{time}", time_style );

            string tag_pattern = msg_profile.Item("tag_pattern");

            /*  extract points from message and create transactions to update tags
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
                string field_name = point.Item("name");
                string alias = point.Item("alias");
                var Value = msg.Item(field_name);
                if (Value != null)
                {
                    // TODO: apply scaling
                    // TODO: build the tag name
                    string tag = tag_pattern.Replace("{server}",$"{server_name}").Replace("{device}",$"{device}");
                    tag = tag.Replace("{point}", alias ?? field_name);
                    Console.WriteLine($"Tag: {tag}, Time: {sampleTime}, Value: {Value}");
                }
            }

            //// the other details may be device-dependent
            //switch (device_type)
            //{
            //    case "site_sentinel":
            //        long seqNumber = msg.Item("seqNumber");
            //        string lqi = msg.Item("lqi");
            //        double latitude = msg.Item("lat");
            //        double longitude = msg.Item("long");
            //        long result_001 = msg.Item("data.RESULT_001");
            //        long result_002 = msg.Item("data.RESULT_002");
            //        long modbus = msg.Item("data.MODBUS");
            //        long counter = msg.Item("data.COUNTER");
            //        long input_001 = msg.Item("data.INPUT_001");
            //        long input_002 = msg.Item("data.INPUT_002");
            //        long scan_timed = msg.Item("data.SCAN_TIMED");
            //        long scan_fast = msg.Item("data.SCAN_FAST");
            //        double volts = msg.Item("data.VOLTS");
            //        break;
            //    default:
            //        break;
            //}

            #region exclude
            // build a lookup table of tags for this site, indexed by signal name
            //IEnumerable<PIPoint> piPoints = PIPoint.FindPIPoints(server, $"{prefix}*", null, null);
            //Dictionary<string, PIPoint> sigList = new Dictionary<string, PIPoint> { };
            //foreach (var p in piPoints) sigList[p.Name.Substring(prefix.Length + 1)] = p;

            //// get the list of required signal names
            //List<string> signals = settings.Values("signals");

            //// create a lookup table, indexed by signal name, to contain value series
            //Dictionary<string, AFValues> values = new Dictionary<string, AFValues> { };

            //// process time-stamped list of records containing signal values
            //for (int n = 0; n < data.Count; n++)
            //{
            //    // decode the timestamp
            //    var rec = (JDict)data[n];
            //    var timestamp = DateTime.Parse((string)rec["timestampUtc"], null, DateTimeStyles.AssumeUniversal);

            //    // add measurements to the corresponding series
            //    foreach (string key in rec.Keys)
            //    {
            //        // translate the 'key' to a 'signal' ID
            //        string signal = key.ToLower();
            //        if (key.IndexOf('_') >= 0) signal = signal.Substring(0, key.IndexOf('_'));
            //        foreach (string pSig in sigList.Keys) { if (signal == pSig.ToLower().Replace("_", "")) { signal = pSig; break; } }

            //        // exclude signals that are: 'null'; not in the specified list; not defined as a PI Point
            //        if (rec[key] == null || !(signals.Count == 0 || signals.Contains("*") || signals.Contains(signal)) || !sigList.ContainsKey(signal))
            //            continue;

            //        PIPoint p = sigList[signal];
            //        AFValue value;
            //        // if first value for this signal, create a 'series'
            //        if (!values.ContainsKey(signal))
            //            values[signal] = new AFValues();
            //        // add this measurement to the time series
            //        switch (p.PointType)
            //        {
            //            case PIPointType.Float16:
            //            case PIPointType.Float32:
            //            case PIPointType.Float64:
            //                value = new AFValue((double)rec[key]);
            //                break;
            //            case PIPointType.Digital:
            //                value = new AFValue((int)rec[key]);
            //                break;
            //            case PIPointType.Int16:
            //            case PIPointType.Int32:
            //                value = new AFValue((int)rec[key]);
            //                break;
            //            case PIPointType.String:
            //            default:
            //                value = new AFValue((string)rec[key]);
            //                break;
            //        }
            //        try
            //        {
            //            value.Timestamp = (AFTime)timestamp;
            //            values[signal].Add(value);
            //        }
            //        catch (Exception e) { Console.WriteLine(e.Message); }
            //    }
            //}

            //// All records scanned, upload the data
            //foreach (string signal in values.Keys)
            //{
            //    OutQueue.Enqueue(new PI_Update(sigList[signal], values[signal]));
            //}
            //// Throttle back if Upload queue gets too long
            //if (OutQueue.Count > 350)
            //{
            //    if (!limited)
            //        Console.WriteLine($"Uploader queue limit reached");
            //    limited = true;
            //    Thread.Sleep(350);
            //}
            #endregion

        } // end PI_Upload()

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
