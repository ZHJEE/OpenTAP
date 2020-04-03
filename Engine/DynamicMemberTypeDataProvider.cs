using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace OpenTap
{
    public interface IDynamicMembersProvider
    {
        IMemberData[] DynamicMembers { get; set; }
    }

    public class DynamicMember : IMemberData
    {
        public virtual IEnumerable<object> Attributes { get; set; } = Array.Empty<object>();
        public string Name { get; set; }
        public ITypeData DeclaringType { get; set; }
        public ITypeData TypeDescriptor { get; set; }
        public bool Writable { get; set; }
        public bool Readable { get; set; }

        public object DefaultValue;

        ConditionalWeakTable<object, object> dict = new ConditionalWeakTable<object, object>();

        public virtual void SetValue(object owner, object value)
        {
            dict.Remove(owner);
            if (object.Equals(value, DefaultValue) == false)
                dict.Add(owner, value);
        }

        public virtual object GetValue(object owner)
        {
            if (dict.TryGetValue(owner, out object value))
                return value;
            return DefaultValue;
        }

        class ForwardedMember : DynamicMember, IForwardedMemberData
        {
            public ForwardedMember(object source, IMemberData member, string name)
            {
                this.source = source;
                this.member = member;
                Name = name;
            }
            
            static XmlIgnoreAttribute xmlignore = new XmlIgnoreAttribute();
            public override IEnumerable<object> Attributes => member.Attributes.Append(xmlignore);
            readonly object source;
            readonly IMemberData member;

            public override object GetValue(object owner)
            {
                var result = member.GetValue(source);
                if (AdditionalMembers != null)
                {
                    foreach (var (s, m) in AdditionalMembers)
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
                member.SetValue(source, value);
                if (AdditionalMembers != null)
                {
                    foreach (var (s, m) in AdditionalMembers)
                        m.SetValue(s, value);
                }
            } 
            
            public IEnumerable<(object Source, IMemberData Member)> Members
            {
                get { yield return (source, member); }
            }

            public void AddAdditionalMember(object source, IMemberData member)
            {
                if (AdditionalMembers == null)
                    AdditionalMembers = new List<(object, IMemberData)>();
                AdditionalMembers.Add((source, member));
            }

            List<(object, IMemberData)> AdditionalMembers = null;
        }
        
        public static IMemberData AddForwardedMember(object target, IMemberData member, object source, string name)
        {
            var td = TypeData.GetTypeData(target);
            var member2 = td.GetMember(name);
            if (member2 == null)
            {
                var members =
                    (IMemberData[]) DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.GetValue(target) ?? new IMemberData[0];
                
                member2 = new ForwardedMember(source, member, name)
                {
                    TypeDescriptor = member.TypeDescriptor,
                    DeclaringType = td,
                    Writable = member.Writable,
                    Readable = member.Readable
                };
                Array.Resize(ref members, members.Length + 1);
                members[members.Length - 1] = member2;
                DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.SetValue(target, members);
            }
            else
            {
                if (member2 is ForwardedMember fw)
                    fw.AddAdditionalMember(source, member);
                else
                    throw new Exception("A member by that name already exists.");
            }
            return member2;
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
                Attributes = new Attribute[]{new XmlIgnoreAttribute(), new AnnotationExcludeAttribute()},
                Writable = true,
                Readable = true,
                TypeDescriptor = TypeData.FromType(typeof((Object,IMemberData)[]))
            };

            static IMemberData[] extraMembers = {AbortCondition, DescriptionMember, DynamicMembers};

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
            if (obj is ITestStep)
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
        IEnumerable<(object Source, IMemberData Member)> Members { get; }
    }
}