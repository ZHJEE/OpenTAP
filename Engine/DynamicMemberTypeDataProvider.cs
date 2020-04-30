using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>  This interface speeds up accessing dynamic members as it avoids having to access a global table to store the information. </summary>
    interface IDynamicMembersProvider
    {
        IMemberData[] DynamicMembers { get; set; }
    }

    /// <summary>  Dynamic member operations. </summary>
    public static class DynamicMemberOperations
    {
        /// <summary> Parameterizes a member from one object unto another. If the name matches something already forwarded, the member will be added to that. </summary>
        /// <param name="target"> The object on which to add a new member. </param>
        /// <param name="member"> The member to forward. </param>
        /// <param name="source"> The owner of the forwarded member. </param>
        /// <param name="name"> The name of the new property. If null, the name of 'member' will be used.</param>
        /// <returns></returns>
        public static IParameterizedMemberData ParameterizeMember(object target, IMemberData member, object source, string name) =>
            DynamicMember.ParameterizeMember(target, member, source, name);

        /// <summary> Removes a parameterization of a member. </summary>
        /// <param name="target"> The object owning the dynamic member.</param>
        /// <param name="forwardedMember"> The forwarded member owned by 'target'. </param>
        /// <param name="aliasedMember"> The aliased member owned by the source. </param>
        /// <param name="source"> The source of the member. </param>
        public static void UnparameterizeMember(object target, IMemberData forwardedMember, object source, IParameterizedMemberData aliasedMember) =>
            DynamicMember.UnparameterizeMember(target, forwardedMember, aliasedMember, source);
    }
    
    class DynamicMember : IMemberData
    {
        public virtual IEnumerable<object> Attributes { get; set; } = Array.Empty<object>();
        public string Name { get; set; }
        public ITypeData DeclaringType { get; set; }
        public ITypeData TypeDescriptor { get; set; }
        public bool Writable { get; set; }
        public bool Readable { get; set; }

        public object DefaultValue;

        readonly ConditionalWeakTable<object, object> dict = new ConditionalWeakTable<object, object>();

        public virtual void SetValue(object owner, object value)
        {
            dict.Remove(owner);
            if (Equals(value, DefaultValue) == false)
                dict.Add(owner, value);
        }

        public virtual object GetValue(object owner)
        {
            if (dict.TryGetValue(owner, out object value))
                return value;
            return DefaultValue;
        }

        class ForwardedMember : DynamicMember, IParameterizedMemberData
        {
            public ForwardedMember(object source, IMemberData member, string name)
            {
                var _name = name.Split('\\');
                this.source = source;
                this.member = member;
                Name = name;
                
                var disp = member.GetDisplayAttribute();
                displayAttribute = new DisplayAttribute(_name[_name.Length - 1].Trim(), disp.Description, Order: disp.Order,
                        Groups: _name.Take(_name.Length - 1).Select(x => x.Trim()).ToArray());
            }

            DisplayAttribute displayAttribute;
            public override IEnumerable<object> Attributes => member.Attributes.Select(x =>
            {
                if(x is DisplayAttribute)
                    return displayAttribute;
                return x;
            });
            object source;
            IMemberData member;

            bool calculateMemberAverage = false;
            public override object GetValue(object owner)
            {
                var result = member.GetValue(source);
                
                if (calculateMemberAverage)
                {
                    // disabled because it really does not make any sense to do.
                    // the value of parameters should always be synced up.
                    foreach (var (s, m) in additionalMembers)
                    {
                        var nextResult = m.GetValue(s);
                        if (Equals(nextResult, result) == false)
                            return null;
                    }
                }

                return result;
            }

            public override void SetValue(object owner, object value)
            {
                // this gets a bit complicated now.
                // we have to ensure that the value is not just same object type, but not the same object
                // in some cases. Thence we need special cloning.
                bool strConvertSuccess = false;
                string str = null;
                strConvertSuccess = StringConvertProvider.TryGetString(value, out str);

                TapSerializer serializer = null;
                string serialized = null;
                if (!strConvertSuccess && value != null)
                {
                    serializer = new TapSerializer();
                    try
                    {
                        serialized = serializer.SerializeToString(value);
                    }
                    catch
                    {
                    }
                }

                int count = 0;
                if (additionalMembers != null)
                    count = additionalMembers.Count;

                for (int i = -1; i < count; i++)
                {
                    var context = i == -1 ? source : additionalMembers[i].Source;
                    var _member = i == -1 ? member : additionalMembers[i].Member;
                    try
                    {
                        object setVal = value;
                        if (strConvertSuccess)
                        {
                            if (StringConvertProvider.TryFromString(str, TypeDescriptor, context, out setVal) == false)
                                setVal = value;
                        }
                        else if (serialized != null)
                        {
                            try
                            {
                                setVal = serializer.DeserializeFromString(serialized);
                            }
                            catch
                            {
                            }
                        }

                        _member.SetValue(context, setVal); // This will throw an exception if it is not assignable.
                    }
                    catch

                    {
                        object _value = value;
                        if (_value != null)
                            _member.SetValue(context, _value); // This will throw an exception if it is not assignable.
                    }
                }
            } 
            
            public IEnumerable<(object Source, IMemberData Member)> Members
            {
                get
                {
                    yield return (source, member);
                    if (additionalMembers != null)
                        foreach (var item in additionalMembers)
                            yield return item;
                }
            }

            public void AddAdditionalMember(object source, IMemberData member)
            {
                if (additionalMembers == null)    
                    additionalMembers = new List<(object, IMemberData)>();
                additionalMembers.Add((source, member));
            }

            List<(object Source, IMemberData Member)> additionalMembers = null;

            /// <summary>
            /// removes a forwarded member. If it was the original member, the first additional member will be used.
            /// If no additional members are present, then true will be returned, signalling that the forwarded member no longer exists.
            /// </summary>
            /// <param name="aliasedMember">The forwarded member.</param>
            /// <param name="_source">The object owning 'aliasedMember'</param>
            /// <returns></returns>
            public bool RemoveMember(IMemberData aliasedMember, object _source)
            {
                if (_source == source && Equals(aliasedMember, member))
                {
                    if (additionalMembers == null || additionalMembers.Count == 0)
                    {
                        source = null;
                        return true;
                    }
                    (source, member) = additionalMembers[0];
                    additionalMembers.RemoveAt(0);
                }
                else
                {
                    additionalMembers.Remove((_source, aliasedMember));
                }

                return false;
            }
        }

        public static void AddDynamicMember(object target, IMemberData member)
        {
            var members =
                (IMemberData[]) DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.GetValue(target) ?? new IMemberData[0];
                
            
            Array.Resize(ref members, members.Length + 1);
            members[members.Length - 1] = member;
            DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.SetValue(target, members);
        }

        public static void RemovedDynamicMember(object target, IMemberData member)
        {
            var members =
                (IMemberData[]) DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.GetValue(target);
            members = members.Where(x => !Equals(x,member)).ToArray();
            DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.SetValue(target, members);
        }
        
        public static IParameterizedMemberData ParameterizeMember(object target, IMemberData member, object source, string name)
        {
            if(target == null) throw new ArgumentNullException(nameof(target));
            if(member == null) throw new ArgumentNullException(nameof(member));
            if(source == null) throw new ArgumentNullException(nameof(source));
            if (name == null) name = member.GetDisplayAttribute()?.GetFullName() ?? member.Name;
            var td = TypeData.GetTypeData(target);
            var _member2 = td.GetMember(name);
            IParameterizedMemberData member2 = _member2 as IParameterizedMemberData;
            ;
            if (_member2  == null)
            {
                member2 = new ForwardedMember(source, member, name)
                {
                    TypeDescriptor = member.TypeDescriptor,
                    DeclaringType = td,
                    Writable = member.Writable,
                    Readable = member.Readable
                };
                
                AddDynamicMember(target, member2);
            }
            else
            {
                if (_member2 is ForwardedMember fw)
                    fw.AddAdditionalMember(source, member);
                else
                    throw new Exception("A member by that name already exists.");
            }
            return member2;
        }

        public static void UnparameterizeMember(object target, IMemberData _forwardedMember, IParameterizedMemberData aliasedMember, object source)
        {
            if (_forwardedMember == null) throw new ArgumentNullException(nameof(_forwardedMember));
            if (target == null) throw new ArgumentNullException(nameof(target));
            var fw = _forwardedMember as ForwardedMember;
            if (fw == null)
                throw new Exception($"Member {_forwardedMember.Name} is not a forwarded member.");
            if (fw.RemoveMember(aliasedMember, source))
                RemovedDynamicMember(target, fw);
        }
    }

    internal class DynamicMemberTypeDataProvider : IStackedTypeDataProvider
    {
        class BreakConditionDynamicMember : DynamicMember
        {
            public override void SetValue(object owner, object value)
            {
                if (owner is IBreakConditionProvider bc)
                {
                    bc.BreakCondition = (BreakCondition) value;
                    return;
                }

                base.SetValue(owner, value);
            }

            public override object GetValue(object owner)
            {
                if (owner is IBreakConditionProvider bc)
                    return bc.BreakCondition;
                return base.GetValue(owner);
            }
        }


        class DescriptionDynamicMember : DynamicMember
        {
            public override void SetValue(object owner, object value)
            {
                if (owner is IDescriptionProvider bc)
                {
                    bc.Description = (string) value;
                    return;
                }

                base.SetValue(owner, value);
            }

            public override object GetValue(object owner)
            {
                string result;
                if (owner is IDescriptionProvider bc)
                    result = bc.Description;
                else
                    result = (string) base.GetValue(owner);
                if (result == null)
                    result = TypeData.GetTypeData(owner).GetDisplayAttribute().Description;
                return result;
            }
        }

        class DynamicMembersMember : DynamicMember
        {
            public override void SetValue(object owner, object value)
            {
                if (owner is IDynamicMembersProvider bc)
                {
                    bc.DynamicMembers = (IMemberData[]) value;
                    return;
                }

                base.SetValue(owner, value);
            }

            public override object GetValue(object owner)
            {
                IMemberData[] result;
                if (owner is IDynamicMembersProvider bc)
                    result = bc.DynamicMembers;
                else
                    result = (IMemberData[]) base.GetValue(owner);

                return result;
            }
        }

        class DynamicTestStepTypeData : ITypeData
        {
            public DynamicTestStepTypeData(TestStepTypeData innerType, object target)
            {
                BaseType = innerType;
                Target = target;
            }

            readonly object Target;

            public IEnumerable<object> Attributes => BaseType.Attributes;
            public string Name => BaseType.Name;
            public ITypeData BaseType { get; }
            public IEnumerable<IMemberData> GetMembers()
            {
                var extra = (IMemberData[])TestStepTypeData.DynamicMembers.GetValue(Target);
                if (extra != null)
                    return BaseType.GetMembers().Concat(extra);
                return BaseType.GetMembers();
            }

            public IMemberData GetMember(string name)
            {
                var extra = (IMemberData[])TestStepTypeData.DynamicMembers.GetValue(Target);
                return extra.FirstOrDefault(x => x.Name == name) ?? BaseType.GetMember(name);
            }

            public object CreateInstance(object[] arguments) => BaseType.CreateInstance(arguments);

            public bool CanCreateInstance => BaseType.CanCreateInstance;
        }

        internal class TestStepTypeData : ITypeData
        {
            internal static readonly DynamicMember AbortCondition = new BreakConditionDynamicMember
            {
                Name = "BreakConditions",
                DefaultValue = BreakCondition.Inherit,
                Attributes = new Attribute[]
                {
                    new DisplayAttribute("Break Conditions",
                        "When enabled, specify new break conditions. When disabled conditions are inherited from the parent test step or the engine settings.",
                        "Common", 20001.1),
                    new UnsweepableAttribute()
                },
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable = true,
                TypeDescriptor = TypeData.FromType(typeof(BreakCondition))
            };

            internal static readonly DynamicMember DescriptionMember = new DescriptionDynamicMember
            {
                Name = "Description",
                DefaultValue = null,
                Attributes = new Attribute[]
                {
                    new DisplayAttribute("Description", "A short description of this test step.", "Common",
                        20001.2),
                    new LayoutAttribute(LayoutMode.Normal, 3, 5),
                    new UnsweepableAttribute()
                },
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable = true,
                TypeDescriptor = TypeData.FromType(typeof(string))
            };
            
            internal static readonly DynamicMember DynamicMembers = new DynamicMembersMember()
            {
                Name = "ForwardedMembers",
                DefaultValue = null,
                DeclaringType = TypeData.FromType((typeof(TestStepTypeData))),
                Attributes = new Attribute[]{new XmlIgnoreAttribute(), new AnnotationIgnoreAttribute()},
                Writable = true,
                Readable = true,
                TypeDescriptor = TypeData.FromType(typeof((Object,IMemberData)[]))
            };

            static IMemberData[] extraMembers = {AbortCondition, DynamicMembers}; //, DescriptionMember // Future: Include Description Member

            public TestStepTypeData(ITypeData innerType)
            {
                this.innerType = innerType;
            }

            public override bool Equals(object obj)
            {
                if (obj is TestStepTypeData td2)
                    return td2.innerType.Equals(innerType);
                return base.Equals(obj);
            }

            public override int GetHashCode() => innerType.GetHashCode() * 157489213;

            readonly ITypeData innerType;
            public IEnumerable<object> Attributes => innerType.Attributes;
            public string Name => innerType.Name;
            public ITypeData BaseType => innerType;

            public IEnumerable<IMemberData> GetMembers()
            {
                return innerType.GetMembers().Concat(extraMembers);
            }

            public IMemberData GetMember(string name)
            {
                if (name == AbortCondition.Name) return AbortCondition;
                return innerType.GetMember(name);
            }

            public object CreateInstance(object[] arguments)
            {
                return innerType.CreateInstance(arguments);
            }

            public bool CanCreateInstance => innerType.CanCreateInstance;
        }

        // memorize for reference equality.
        static ConditionalWeakTable<ITypeData, TestStepTypeData> dict =
            new ConditionalWeakTable<ITypeData, TestStepTypeData>();
        static TestStepTypeData getStepTypeData(ITypeData subtype) =>
            dict.GetValue(subtype, x => new TestStepTypeData(x));

        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            var subtype = stack.GetTypeData(identifier);
            if (subtype.DescendsTo(typeof(ITestStep)))
            {
                var result = getStepTypeData(subtype);
                return result;
            }

            return subtype;
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            if (obj is ITestStep || obj is TestPlan)
            {
                var subtype = stack.GetTypeData(obj);
                var result = getStepTypeData(subtype);
                if (TestStepTypeData.DynamicMembers.GetValue(obj) is IMemberData[])
                    return new DynamicTestStepTypeData(result, obj);
                return result;
            }
            return null;
        }
        public double Priority { get; } = 10;
    }
    
    /// <summary> An IMemberData that shadows multiple other members.</summary>
    public interface IForwardedMemberData : IMemberData
    {
        /// <summary>  The shadowed members. </summary>
        IEnumerable<(object Source, IMemberData Member)> Members { get; }
    }

    /// <summary>  This type of forwarded members represents a perameterization of object members unto another object. </summary>
    public interface IParameterizedMemberData : IForwardedMemberData
    {

    }
}