﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;

namespace DotXMLLib
{
    /// <summary>
    /// Wrapper for XmlDocument class to support dot-notation reference
    ///     use 
    ///         (DotXML)doc.LoadXML(text)
    ///     to build an object tree
    ///         * <tags> are converted to name:value pairs (XDict)
    ///             unless there are multiple instances within a container 
    ///             in which case they are converted to an array of name:value pairs (XArray)
    ///         
    ///         * attributes are added as name:value pairs to a node called 'attr'
    ///         
    ///         * elementary values are decoded as long, double, bool, string or null
    ///         
    ///     use Item("<node>.<array>[<index>].<item>") to access the named item
    ///         the Item returned can be a XNode, XArray or element
    ///         
    ///     use Item("<node>.attr.<name>" to access the named attribute
    ///         
    /// </summary>
    public class DotXML : XmlDocument
    {
        public XNode body;

        public DotXML(string filename) { LoadXml(File.ReadAllText(filename)); }

        public DotXML()
        {
            body = null;
        }

        public override void LoadXml(string xml)
        {
            base.LoadXml(xml);
            body = new XNode { };
            Parse(ref body, this);
        }

        #region Parse XML document
        // convert elementary value to primary Type
        internal static dynamic Element( string Value)
        {
            if (long.TryParse(Value, out long lRresult))
                return lRresult;
            else if (double.TryParse(Value, out double dResult))
                return dResult;
            else if (Value == null || Value.ToLower() == "null")
                return null;
            else if (Value.ToLower() == "true")
                return true;
            else if (Value.ToLower() == "false")
                return false;
            else
                return Value;
        }

        // construct an object 'tree' of the XML document elements
        internal static dynamic Parse(ref XNode root, XmlNode Node)
        {
            // Add attributes list if present
            if( Node.Attributes!= null && Node.Attributes.Count > 0 )
            {
                XNode Attrs = new XNode { };
                foreach (XmlAttribute Attr in Node.Attributes)
                {
                    Attrs[Attr.Name] = Attr.Value;
                }
                root["attr"] = Attrs;
            }

            // add children: branches or elements
            foreach (XmlNode Child in Node.ChildNodes )
            {
                string name = Child.Name;
                dynamic Value = null;

                // extract the node Value
                if ( Child.ChildNodes.Count == 0)
                    root[name] = Element(Child.Value);
                else if(Child.ChildNodes.Count == 1 && Child.FirstChild.Name == "#text")
                    root[name] = Element(Child.FirstChild.Value);
                else
                {
                    XNode branch = new XNode { };
                    Value = Parse(ref branch, Child);

                    // add the item to the tree
                    if (!root.ContainsKey(name))
                    {
                        root[name] = Value;
                    }
                    // if multiple items of this name convert then to XList item
                    else if (root[name].GetType().Name != "XList")
                    {
                        dynamic First = root[name];
                        root.Remove(name);
                        root[name] = new XList { First, Value };
                    }
                    else
                    {
                        root[name].Add(Value);
                    }
                }

            }
            return root;
        }
        #endregion

        #region dot-notation access methods
        public dynamic Item(string tag) => body.Item(tag);
        public dynamic Item(string tag, string dflt) => body.Item(tag) ?? dflt;
        public dynamic Item(string tag, dynamic dflt) => body.Item(tag) ?? dflt;

        public class XList : List<dynamic>
        {
            public dynamic Item(int index) => (index >= 0 && index < Count) ? this[index] : null;
            public dynamic Item(string key) => null;     // invalid reference
        }

        public class XNode : Dictionary<string, dynamic>
        {
            public dynamic Item(string key, string dflt) => Item(key) ?? dflt;
            public dynamic Item(string key, dynamic dflt) => Item(key) ?? dflt;

            public dynamic Item(int index) => null;     // invalid reference
            // this method is recursive
            public dynamic Item(string key)
            {
                if (key.IndexOf('.') < 0)
                {
                    if (key.IndexOf('[') < 0)
                    {
                        // fetch the referenced element
                        dynamic Element = null;
                        if( this.ContainsKey(key) ) Element = this[key];
                        try
                        {
                            return Element["Value"];
                        }
                        catch
                        {
                            return Element;
                        }
                    }
                    else
                    {
                        string node = key.Substring(0, key.IndexOf('['));
                        int index = parse_index(key);
                        return (index >= 0) ? this[node].Item(index) : null;
                    }
                }
                else
                {
                    // extract node name and key (return 'null' if undefined)
                    string[] nodes = key.Split(".".ToCharArray(), 2);
                    dynamic element = this.Item(nodes[0]);
                    return element?.Item(nodes[1]);
                }
            }
            internal static int parse_index(string input)
            {
                // pattern to extract what is between '[' and ']' that is not '[' or ']'
                string pattern = @"\[([^\[\]]+)\]";
                var content = Regex.Match(input, pattern).Groups[1].Value;
                return (int.TryParse(content, out int index)) ? index : -1;
            }
        }
        #endregion

    }
}
