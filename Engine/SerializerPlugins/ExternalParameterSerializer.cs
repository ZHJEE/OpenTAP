//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for External parameters. </summary>
    public class ExternalParameterSerializer : TapSerializerPlugin
    {
        /// <summary>
        /// Structure for holding data about <see cref="TestPlan.ExternalParameters"/>
        /// </summary>
        public struct ExternalParamData
        {
            /// <summary>
            /// The object
            /// </summary>
            public ITestStep Object;
            /// <summary>
            /// The external param property.
            /// </summary>
            public IMemberData Property;
            /// <summary>
            ///  The name of the external test plan parameter.
            /// </summary>
            public string Name;
        }
        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return 2; } }
        
        List<XElement> currentNode = new List<XElement>();

        /// <summary>
        /// Stores the data if a test plan was not serialized but the external keyword was used. 
        /// </summary>
        public readonly List<ExternalParamData> UnusedExternalParamData = new List<ExternalParamData>();

        /// <summary>
        /// Pre-Loaded external parameter Name/Value sets.
        /// </summary>
        public readonly Dictionary<string, string> PreloadedValues = new Dictionary<string, string>();

        static readonly XName External = "external";
        static readonly XName Scope = "Scope";
        static readonly XName Parameter = "Parameter";

        bool loadScopeParameter(Guid scope, ITestStep step, IMemberData member, string parameter)
        {
            ITestStepParent parent;
            if (scope == Guid.Empty)
            {
                parent = step.GetParent<TestPlan>();
            }
            else
            {
                ITestStep subparent = step.Parent as ITestStep;
                while (subparent != null)
                {
                    if (subparent.Id == scope)
                        break;
                    subparent = subparent.Parent as ITestStep;
                }
                parent = subparent;
            }

            if (parent == null) return false;
            DynamicMemberOperations.ParameterizeMember(parent, member, step, parameter);
            return true;
        }

        XElement rootNode;
        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize(XElement elem, ITypeData t, Action<object> setter)
        {
            if (rootNode == null && t.DescendsTo(typeof(TestPlan)))
            {
                rootNode = elem;
                TestPlan _plan;
                bool ok = Serializer.Deserialize(elem,  x=>
                {
                    _plan = (TestPlan)x;
                    setter(_plan);
                    Serializer.DeferLoad(() =>
                    {
                        foreach (var value in PreloadedValues)
                        {
                            var ext = _plan.ExternalParameters.Get(value.Key);
                            try
                            {
                                ext.Value = value.Value;
                            }
                            catch
                            {

                            }
                        }    
                    });
                }, t);

                return ok;
            }
            if (elem.HasAttributes == false || currentNode.Contains(elem)) return false;

            var parameter = elem.Attribute(External)?.Value ?? elem.Attribute(Parameter)?.Value;
            if (string.IsNullOrWhiteSpace(parameter)) return false;
            var stepSerializer = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
            var step = stepSerializer?.Object as ITestStep;
            if (step == null) return false;
            var member = stepSerializer.CurrentMember;

            Guid.TryParse(elem.Attribute(Scope)?.Value, out Guid scope);
            if (!loadScopeParameter(scope, step, member, parameter))
                Serializer.DeferLoad(() => loadScopeParameter(scope, step, member, parameter));
            if (scope != Guid.Empty) return false;
            var plan = Serializer.SerializerStack.OfType<TestPlanSerializer>().FirstOrDefault()?.Plan;
            if (plan == null)
            {
                currentNode.Add(elem);
                try
                {
                    UnusedExternalParamData.Add(new ExternalParamData
                    {
                        Object = (ITestStep) stepSerializer.Object, Property = stepSerializer.CurrentMember,
                        Name = parameter
                    });
                    return Serializer.Deserialize(elem, setter, t);
                }
                finally
                {
                    currentNode.Remove(elem);
                }
            }

            currentNode.Add(elem);
            try
            {
                
                bool ok = Serializer.Deserialize(elem, setter, t);
                var extParam = plan.ExternalParameters.Get(parameter);

                if (ok && extParam != null)
                {
                    Serializer.DeferLoad(() => { extParam.Value = extParam.Value; });
                }

                if (PreloadedValues.ContainsKey(parameter))
                {
                    if (extParam != null)
                    {
                        // If there is a  preloaded value, use that.
                        extParam.Value = PreloadedValues[extParam.Name];
                    }
                    else
                    {
                        Serializer.DeferLoad(() =>
                        {
                            extParam = plan.ExternalParameters.Get(parameter);
                            if(extParam != null)
                                extParam.Value = extParam.Value;
                        });
                    }
                }

                return ok;
            }
            finally
            {
                currentNode.Remove(elem);
            }
        }

        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object obj, ITypeData expectedType)
        {
            if (currentNode.Contains(elem)) return false;
            

            ObjectSerializer objSerializer = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
            if (objSerializer == null || objSerializer.CurrentMember == null || false == objSerializer.Object is ITestStep)
                return false;

            
            ITestStep step = (ITestStep)objSerializer.Object;
            
            var member = objSerializer.CurrentMember;
            // here I need to check if any of its parent steps are forwarding 
            // its member data.

            ITestStepParent forwardingParent = step.Parent;
            IMemberData forwardingMember = null;
            while (forwardingParent != null && forwardingMember == null)
            {
                var members = TypeData.GetTypeData(forwardingParent).GetMembers().OfType<IForwardedMemberData>();
                forwardingMember = members.FirstOrDefault(x => x.Members.Any(y => y.Source == step && y.Member == member));
                if (forwardingMember == null)
                    forwardingParent = forwardingParent.Parent;
            }

            if (forwardingMember == null) return false;

            elem.SetAttributeValue(Parameter, forwardingMember.Name);
            if (forwardingParent is ITestStep parentStep)
                elem.SetAttributeValue(Scope, parentStep.Id.ToString());
            // skip
            try
            {
                currentNode.Add(elem);
                return Serializer.Serialize(elem, obj, expectedType);
            }
            finally
            {
                currentNode.Remove(elem);
            }
        }
    }
}
