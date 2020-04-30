//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
namespace OpenTap
{

    /// <summary>
    /// This class represents a set of external test plan parameters that can be defined when a test plan is loaded.
    /// </summary>
    public class ExternalParameter
    {
        /// <summary>
        /// The name of this entry.
        /// </summary>
        public string Name { get; }

        TestPlan plan;
        /// <summary> Maps test step to member infos. </summary>
        public IEnumerable<KeyValuePair<ITestStep, IEnumerable<IMemberData>>> Properties
            => member.Members
                .Select(x => new KeyValuePair<ITestStep, IEnumerable<IMemberData>>((ITestStep)x.Source, new []{x.Member}));


        /// <summary>
        /// Gets the list of PropertyInfos associated with this mask entry.
        /// </summary>
        public IEnumerable<IMemberData> PropertyInfos => Properties
            .SelectMany(x => x.Value)
            .Distinct();

        /// <summary>
        /// Gets or sets the value of the combined properties. The setter requires the types to be the same or IConvertibles.
        /// </summary>
        public object Value
        {
            get  =>  member.GetValue(plan);
            set => member.SetValue(plan, value);
        }

        internal void Clean(HashSet<ITestStep> steps)
        {
            var members = member.Members;
            foreach (var item in members.Where(x => steps.Contains(x.Source) == false))
                DynamicMemberOperations.UnparameterizeMember(plan, member, item.Source, item.Member);
        }

        /// <summary>
        /// Gets the property that is bound by the step with ID step.
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        public List<IMemberData> GetProperties(ITestStep step)
        {
            return Properties.Where(x => x.Key == step).SelectMany(x => x.Value).ToList();
        }

        IForwardedMemberData member;

        /// <summary>Constructor for the ExternalParameter.</summary>
        /// <param name="Plan"></param>
        /// <param name="Name"></param>
        public ExternalParameter(TestPlan Plan, string Name)
        {
            this.plan = Plan;
            this.Name = Name;
            member = TypeData.GetTypeData(plan).GetMember(Name) as IForwardedMemberData;
        }

        internal ExternalParameter(TestPlan plan, IForwardedMemberData forwardedMember)
        {
            this.plan = plan;
            this.Name = forwardedMember.Name;
            member = forwardedMember;
        }

        /// <summary>
        /// Adds a property to the external parameters.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="property"></param>
        public void Add(ITestStep step, IMemberData property)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            plan.ExternalParameters.Add(step, property, Name);
        }

        /// <summary>
        /// Removes a step from the external parameters.
        /// </summary>
        /// <param name="step"></param>
        public void Remove(ITestStep step)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            var members = member.Members;
            foreach (var item in members.Where(x => step == x.Source))
                DynamicMemberOperations.UnparameterizeMember(plan, TypeData.GetTypeData(plan).GetMember(Name), item.Source, item.Member);
        }
    }

    /// <summary> External test plan parameters. </summary>
    public class ExternalParameters
    {

        /// <summary> Gets the list of external test plan parameters. </summary>
        public IReadOnlyList<ExternalParameter> Entries
        {
            get
            {
                var fwd = TypeData.GetTypeData(plan).GetMembers().OfType<IForwardedMemberData>();
                return fwd.Select(x => new ExternalParameter(plan, x)).ToList();
            }
        }

        readonly TestPlan plan;

        /// <summary>Constructor for the ExternalParameters.</summary>
        /// <param name="plan"></param>
        public ExternalParameters(TestPlan plan)
        {
            this.plan = plan;
        }
        

        /// <summary> Adds a step property to the external test plan parameters.</summary>
        /// <param name="step"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="Name"></param>
        public ExternalParameter Add(ITestStep step, IMemberData propertyInfo, string Name = null)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (propertyInfo == null) // As it otherwise won't raise exception right away.
                throw new ArgumentNullException(nameof(propertyInfo));
            var existing = Find(step, propertyInfo);
            if (existing != null)
                return existing;
            if (Name == null)
                Name = propertyInfo.GetDisplayAttribute().Name;

            DynamicMemberOperations.ParameterizeMember(plan, propertyInfo, step, Name);
            return Get(Name);
        }

        /// <summary> Removes a step property from the external parameters. </summary>
        /// <param name="step"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="Name"></param>
        public void Remove(ITestStep step, IMemberData propertyInfo, string Name = null)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if(propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));

            var tpType = TypeData.GetTypeData(plan);
            IForwardedMemberData fwd;
            if (Name != null)
                fwd = tpType.GetMember(Name) as IForwardedMemberData;
            else
                fwd = findForwardedMember(step, propertyInfo);

            if (fwd == null) return;
            DynamicMemberOperations.UnparameterizeMember(plan, fwd, step, propertyInfo);
        }


        /// <summary>
        /// Ensures that each entry test step is also present the test plan.
        /// </summary>
        public void Clean()
        {
            var steps = Utils.FlattenHeirarchy(plan.ChildTestSteps, step => step.ChildTestSteps).ToHashSet();
            foreach (var entry in Entries.ToList())
            {
                entry.Clean(steps);
            }
        }

        /// <summary> Gets an entry by name. </summary>
        /// <param name="externalParameterName"></param>
        /// <returns></returns>
        public ExternalParameter Get(string externalParameterName)
        {
            var member = TypeData.GetTypeData(plan).GetMember(externalParameterName) as IForwardedMemberData;
            if(member != null)
                return new ExternalParameter(plan, member);
            return null;
        }

        /// <summary>
        /// Finds the external parameter that is defined by 'step' and 'property'. If no external parameter is found null is returned.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public ExternalParameter Find(ITestStep step, IMemberData property)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if(property == null)
                throw new ArgumentNullException(nameof(property));
            var fwd = findForwardedMember(step, property);
            if(fwd != null)
                return new ExternalParameter(plan, fwd);
            return null;
        }
        
        /// <summary>
        /// Finds the external parameter that is defined by 'step' and 'property'. If no external parameter is found null is returned.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        IForwardedMemberData findForwardedMember(ITestStep step, IMemberData property)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if(property == null)
                throw new ArgumentNullException(nameof(property));
            
            var forwardedMembers = TypeData.GetTypeData(plan).GetMembers().OfType<IForwardedMemberData>();
            foreach (var fwd in forwardedMembers)
            {
                if (fwd.Members.Contains((step, property)))
                    return fwd;
            }
            return null;
        }

    }
}
