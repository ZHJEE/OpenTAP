using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenTap.Plugins.BasicSteps
{
    
    [AllowAnyChild]
    public class ScopeStep : TestStep
    {
        public class ScopeItem
        {
            ScopeStep Owner;
            public ScopeItem(ScopeStep owner) => Owner = owner;

            /// <summary>
            /// Not child steps of TestPlanReference or ScopeStep.
            /// </summary>
            public IEnumerable<ITestStep> AvailableSteps 
                => Utils.FlattenHeirarchy(Owner.ChildTestSteps, x => (x is TestPlanReference || x is ScopeStep) ? Enumerable.Empty<ITestStep>() : x.ChildTestSteps);
            [AvailableValues(nameof(AvailableSteps))]
            public ITestStep Step { get; set; }

            public IEnumerable<IMemberData> AvailableMembers =>
                Step == null ? Array.Empty<IMemberData>() : TypeData.GetTypeData(Step).GetMembers();
            [AvailableValues(nameof(AvailableMembers))]
            public IMemberData Member { get; set; }
        }
        
        public List<ScopeItem> Items { get; set; }
        
        public ScopeStep()
        {
            Items = new List<ScopeItem> {new ScopeItem(this)};
        }

        public override void Run()
        {
            RunChildSteps();
        }
    }
}