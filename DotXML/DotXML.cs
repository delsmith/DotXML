using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace DotXML
{
    /// <summary>
    /// Wrapper for XmlDocument class to support dot-notation reference
    ///     use Parse(XmlDocument) to build an object tree
    ///         <tags> are converted to name:value pairs
    ///         unless there are multiple instances within a container 
    ///         in which case they are converted to an array of name:value pairs
    ///         
    ///         TODO: elementary values are decoded a long, double, bool, string or null
    ///         
    ///         TODO: attributes are added as a node called 'attr'
    ///         
    ///     use Item("node.array[index].item") to access objects
    ///         the Item returned can be a node, array or element
    ///         
    /// </summary>
    public class DotXML : XmlDocument
    {
        public XNode body = null;
        public override void LoadXml(string xml)
        {
            base.LoadXml(xml);
            body = new XNode { };
            Parse(ref body, ChildNodes);
        }

        internal static dynamic Parse(ref XNode root, XmlNodeList Content)
        {
            // construct an object 'tree' of the XML document elements
            foreach(XmlNode Node in Content)
            {
                string name = Node.Name;
                dynamic Value = null;
                XNode Attrs = null;

                if (Node.Attributes.Count > 0)
                {
                    Attrs = new XNode { };
                    foreach (XmlAttribute Attr in Node.Attributes)
                    {
                        Attrs[Attr.Name] = Attr.Value;
                    }
                }

                // extract the node Value
                if ( Node.ChildNodes.Count == 0)
                    Value = Node.Value;
                else if(Node.ChildNodes.Count == 1 && Node.FirstChild.Name == "#text")
                    Value = Node.FirstChild.Value;
                else
                {
                    XNode branch = new XNode { };
                    Value = Parse(ref branch, Node.ChildNodes);
                    if (Attrs != null)
                    {
                        Value["attr"] = Attrs;
                    }
                }

                // add the item to the tree
                if( ! root.ContainsKey(name))
                {
                    root[name] = Value;
                }
                // if multiple items of this name convert then to XList item
                else if( root[name].GetType().Name != "XList")
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
            return root;
        }
        public class XList : List<dynamic>
        {
            public dynamic Item(int index) => (index >= 0 && index < Count) ? this[index] : null;
            public dynamic Item(string key) => null;     // invalid reference
        }

        public class XNode : Dictionary<string, dynamic>
        {
            public dynamic Item(int index) => null;     // invalid reference
            public dynamic Item(string key)
            {
                if (key.IndexOf('.') < 0)
                {
                    if (key.IndexOf('[') < 0)
                        // fetch the referenced element
                        return (this.ContainsKey(key) ? this[key] : null);
                    else
                    {
                        string node = key.Substring(0, key.IndexOf('['));
                        int index = parse_index(key);
                        return (index >= 0) ? this[node].Item(index) : null;
                    }
                }
                else
                {
                    // extract node name and key 
                    string[] nodes = key.Split(".".ToCharArray(), 2);
                    dynamic element = this.Item(nodes[0]);
                    return element?.Item(nodes[1]);
                }
            }
            internal static int parse_index(string input)
            {
                string pattern = @"\[([^\[\]]+)\]";
                var content = Regex.Match(input, pattern).Groups[1].Value;
                return (int.TryParse(content, out int index)) ? index : -1;
            }
        }
    }
}
