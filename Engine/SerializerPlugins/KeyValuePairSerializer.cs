//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> For serializing/deserializing KeyValuePairs (mostly for use with Dictionaries). 
    /// It requires that the generic arguments of the KeyValuePair can be serialized.</summary>
    internal class KeyValuePairSerializer : TapSerializerPlugin
    {
        /// <summary> Creates a Key and a Value node in the XML.</summary>
        public override bool Serialize(XElement node, object obj, ITypeData _expectedType)
        {
            if(obj == null || false == obj.GetType().DescendsTo(typeof(KeyValuePair<,>)))
            {
                return false;
            }

            if (_expectedType is TypeData expectedType2 && expectedType2.Type is Type expectedType)
            {

                var key = new XElement("Key");
                var value = new XElement("Value");
                bool keyok = Serializer.Serialize(key, _expectedType.GetMember("Key").GetValue(obj), TypeData.FromType(expectedType.GetGenericArguments()[0]));
                bool valueok = Serializer.Serialize(value, _expectedType.GetMember("Value").GetValue(obj), TypeData.FromType(expectedType.GetGenericArguments()[1]));
                if (!keyok || !valueok)
                    return false;
                node.Add(key);
                node.Add(value);
                return true;
            }
            return false;
        }

        /// <summary> Looks for a Key and a Value node in the XML. </summary>
        public override bool Deserialize(XElement node, ITypeData _t, Action<object> setter)
        {
            Type t = (_t as TypeData)?.Type;
            if (t == null) return false;
            if (false == t.DescendsTo(typeof(KeyValuePair<,>)))
                return false;

            var key = node.Element("Key");
            var value = node.Element("Value");
            bool gotkey = false, gotvalue = false;
            object key_value = null, value_value = null;

            // this is a bit complicated
            // because the evaluation of deserialization of 
            // sub items might be defered.
            bool keyok = Serializer.Deserialize(key, x =>
            {
                key_value = x;
                gotkey = true;
                if (gotvalue)
                    setter(Activator.CreateInstance(t, key_value, value_value));
            }, t.GetGenericArguments()[0]);
            if (!keyok)
                return false;
            bool valueok = Serializer.Deserialize(value, x =>
            {
                value_value = x;
                gotvalue = true;
                if (gotkey)
                    setter(Activator.CreateInstance(t, key_value, value_value));
            } , t.GetGenericArguments()[1]);
            
            return valueok;
        }
    }

}
