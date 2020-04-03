using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace OpenTap.Plugins.BasicSteps
{
    class ExpandedMemberData : IMemberData, IForwardedMemberData
    {
        public override bool Equals(object obj)
        {
            if (obj is ExpandedMemberData mem)
            {
                return object.Equals(mem.DeclaringType, DeclaringType) && object.Equals(mem.Name, Name);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return DeclaringType.GetHashCode() ^ Name.GetHashCode();
        }

        public ITypeData DeclaringType { get; set; }

        public IEnumerable<object> Attributes { get; private set; }

        public string Name { get; set; }

        public bool Writable => true;

        public bool Readable => true;

        public ITypeData TypeDescriptor { get; set; }

        public object GetValue(object owner)
        {
            var tpr = owner as TestPlanReference;
            var ep = tpr.ForwardedParameters.FirstOrDefault(x => x.Name == epName);

            var Member = ep.PropertyInfos.First();
            TypeDescriptor = Member.TypeDescriptor;
            return ep.Value;
        }

        public ExternalParameter ExternalParameter
        {
            get
            {
                var tpr = (this.DeclaringType as ExpandedTypeData).Object;
                var ep = tpr.ForwardedParameters.FirstOrDefault(x => x.Name == epName);
                return ep;
            }
        }

        string epName;

        public void SetValue(object owner, object value)
        {
            var tpr = owner as TestPlanReference;
            var ep = tpr.ForwardedParameters.FirstOrDefault(x => x.Name == epName);
            var Member = ep.PropertyInfos.First();
            TypeDescriptor = Member.TypeDescriptor;
            ep.Value = value;
        }

        public ExpandedMemberData(ExternalParameter ep, string name)
        {
            Name = name;
            var Member = ep.PropertyInfos.First();
            epName = ep.Name;
            TypeDescriptor = Member.TypeDescriptor;
            var attrs = Member.Attributes.ToList();
            attrs.RemoveIf<object>(x => x is DisplayAttribute);
            var dis = Member.GetDisplayAttribute();
            var groups = dis.Group;
            if (groups.FirstOrDefault() != "Settings")
                groups = new[] {"Settings"}.Append(dis.Group).ToArray();
            attrs.Add(new DisplayAttribute(ep.Name, Description: dis.Description, Order: 5, Groups: groups));
            if (attrs.Any(x => x is ColumnDisplayNameAttribute))
            {
                var colAttr = (ColumnDisplayNameAttribute) attrs.FirstOrDefault(x => x is ColumnDisplayNameAttribute);
                attrs.Remove(colAttr);

                var newColAttr = new ColumnDisplayNameAttribute(ep.Name, colAttr.Order, colAttr.IsReadOnly);
                attrs.Add(newColAttr);
            }

            Attributes = attrs;
        }

        public IEnumerable<(object, IMemberData)> Members =>
            ExternalParameter.Properties.SelectMany(x => x.Value.Select(y => ((object)x.Key, y)));
    }

    class ExpandedTypeData : ITypeData
    {
        private static readonly Regex propRegex = new Regex(@"^prop(?<index>[0-9]+)$", RegexOptions.Compiled);

        public override bool Equals(object obj)
        {
            if (obj is ExpandedTypeData exp)
                return exp.Object == Object;
            return false;
        }

        public override int GetHashCode() => Object.GetHashCode() ^ 0x1111234;

        public ITypeData InnerDescriptor;
        public TestPlanReference Object;

        public string Name => ExpandMemberDataProvider.exp + InnerDescriptor.Name;

        public IEnumerable<object> Attributes => InnerDescriptor.Attributes;

        public ITypeData BaseType => InnerDescriptor;

        public bool CanCreateInstance => InnerDescriptor.CanCreateInstance;

        public object CreateInstance(object[] arguments)
        {
            return InnerDescriptor.CreateInstance(arguments);
        }

        private IMemberData ResolveLegacyName(string memberName)
        {
            ExpandedMemberData result = null; // return null if no valid expanded member data gets set

            // The following code is only for legacy purposes where properties which were not valid would get a valid 
            // name like: prop0, prop1, prop73, where the number after the prefix prop would be the actual index in the
            // ForwardedParameters array.
            Match m = propRegex.Match(memberName);
            if (m.Success)
            {
                int index = 0;
                try
                {
                    index = int.Parse(m.Groups["index"].Value);
                    if (index >= 0 && index < Object.ForwardedParameters.Length)
                    {
                        var ep = Object.ForwardedParameters[index];
                        // return valid expanded member data
                        result = new ExpandedMemberData(ep, ep.Name) {DeclaringType = this};
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        public IMemberData GetMember(string memberName)
        {
            var mem = GetMembers().FirstOrDefault(x => x.Name == memberName);
            return mem ?? ResolveLegacyName(memberName);
        }

        string names = "";
        IMemberData[] savedMembers = null;

        public IEnumerable<IMemberData> GetMembers()
        {
            var names2 = string.Join(",", Object.ForwardedParameters.Select(x => x.Name));
            if (names == names2 && savedMembers != null) return savedMembers;
            List<IMemberData> members = new List<IMemberData>();

            for (int i = 0; i < Object.ForwardedParameters.Length; i++)
            {
                var ep = Object.ForwardedParameters[i];
                members.Add(new ExpandedMemberData(ep, ep.Name) {DeclaringType = this});
            }

            var innerMembers = InnerDescriptor.GetMembers();
            foreach (var mem in innerMembers)
                members.Add(mem);
            savedMembers = members.ToArray();
            names = names2;
            return members;
        }
    }


    public class ExpandMemberDataProvider : ITypeDataProvider
    {
        public double Priority => 1;
        internal const string exp = "ref@";

        public ITypeData GetTypeData(string identifier)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeData.GetTypeData(identifier.Substring(exp.Length));
                if (tp != null)
                {
                    return new ExpandedTypeData() {InnerDescriptor = tp, Object = null};
                }
            }

            return null;
        }

        static ConditionalWeakTable<TestPlanReference, ExpandedTypeData> types =
            new ConditionalWeakTable<TestPlanReference, ExpandedTypeData>();

        ExpandedTypeData getExpandedTypeData(TestPlanReference step)
        {
            var expDesc = new ExpandedTypeData();
            expDesc.InnerDescriptor = TypeData.FromType(typeof(TestPlanReference));
            expDesc.Object = step;
            return expDesc;
        }

        public ITypeData GetTypeData(object obj)
        {
            if (obj is TestPlanReference exp)
                return types.GetValue(exp, getExpandedTypeData);
            return null;
        }
    }
}