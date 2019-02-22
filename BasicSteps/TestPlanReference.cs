//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Test Plan Reference", "References a test plan from an external file directly, without having to store the test plan as steps in the current test plan.", "Flow Control")]
    [AllowAnyChild]
    public class TestPlanReference : TestStep
    {
        /// <summary>
        /// Mapping between step GUIDs. Oldversion -> New Version.
        /// </summary>
        public class GuidMapping
        {
            public Guid Guid1 { get; set; }
            public Guid Guid2 { get; set; }
        }

        /// <summary>
        /// This counter is used to prevent recursive TestPlan references.
        /// </summary>
        private static int LevelCounter = 0;

        ITestStepParent parent;
        // The PlanDir of 'this' should be ignored when calculating Filepath, so the MacroString context is set to the parent.
        [XmlIgnore]
        public override ITestStepParent Parent { get { return parent; } set { Filepath.Context = value; parent = value; } }

        MacroString filepath = new MacroString();
        string currentlyLoaded = null;
        [Display("Referenced Plan", Order: 0, Description: "A file path pointing to a test plan which will be imported as readonly test steps.")]
        [Browsable(true)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "TapPlan")]
        [SerializationOrder(Order = 1.0)]
        public MacroString Filepath
        {
            get => filepath; 
            set {

                filepath = value;
                filepath.Context = this;
                try
                {
                    var rp = TapSerializer.GetObjectDeserializer(this)?.ReadPath;
                    if (rp != null) rp = System.IO.Path.GetDirectoryName(rp);
                    var fp = filepath.Expand(testPlanDir: rp);
                    
                    if (currentlyLoaded != fp)
                    {
                        LoadTestPlan();
                        currentlyLoaded = fp;
                    }
                }
                catch { }
            }
        }
        
        public string Path
        {
            get { return Filepath.Expand(); }
        }

        bool isExpandingPlanDir = false;
        [MetaData(macroName: "TestPlanDir")]
        public string PlanDir
        {
            get
            {
                if (isExpandingPlanDir) return null;
                isExpandingPlanDir = true;
                try
                {
                    var exp = new MacroString(this) { Text = Filepath.Text }.Expand();
                    if (string.IsNullOrWhiteSpace(exp))
                        return "";
                    var path = System.IO.Path.GetFullPath(exp);
                    return string.IsNullOrWhiteSpace(path) ? "" : System.IO.Path.GetDirectoryName(path);
                }
                catch
                {
                    return "";
                }
                finally
                {
                    isExpandingPlanDir = false;
                }
            }
        }

        [XmlIgnore]
        [Browsable(false)]
        public new TestStepList ChildTestSteps
        {
            get { return base.ChildTestSteps; }
            set { base.ChildTestSteps = value; }
        }

        public TestPlanReference()
        {
            ChildTestSteps.IsReadOnly = true;
            Filepath = new MacroString(this);
            Rules.Add(() => (string.IsNullOrWhiteSpace(Filepath) || File.Exists(Filepath)), "File does not exist.", "Filepath");
            StepMapping = new List<GuidMapping>();
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();

            if (string.IsNullOrWhiteSpace(Filepath))
                throw new OperationCanceledException(string.Format("Execution aborted by {0}. No test plan configured.", Name));
        }

        public override void Run()
        {
            foreach (var run in RunChildSteps())
                UpgradeVerdict(run.Verdict);
        }

        [ThreadStatic]
        static List<GuidMapping> CurrentMappings;

        void UpdateStep()
        {
            if(CurrentMappings == null)
                CurrentMappings = new List<GuidMapping>();
            // Load GUID mappings which is every two GUIDS between <Guids/> and <Guids/>
            var mapping = new Dictionary<Guid, Guid>();
            var allMapping = this.mapping.Concat(CurrentMappings).ToList();
            foreach (var mapitem in allMapping)
            {
                mapping[mapitem.Guid1] = mapitem.Guid2;
            }
            ITestStep parent = this;
            while(parent != null)
            {
                if(parent is TestPlanReference tpr)
                {
                    foreach(var mp in tpr.mapping)
                    {
                        mapping[mp.Guid1] = mp.Guid2;
                    }
                }
                parent = parent.Parent as ITestStep;
            }

            object testplandir = null;
            var currentSerializer = TapSerializer.GetObjectDeserializer(this);
            if (currentSerializer != null && currentSerializer.ReadPath != null)
                testplandir = System.IO.Path.GetDirectoryName(currentSerializer.ReadPath);
            
            var Data = Filepath.Expand(testPlanDir: testplandir as string);
            Data = Data.Replace('\\', '/');
            if (!File.Exists(Data))
            {
                Log.Warning("File does not exist: \"{0}\"", Data);
                return;
            }
            ChildTestSteps.Clear();
            var prevc = CurrentMappings;
            try
            {
                LevelCounter++;
                try
                {
                    if (LevelCounter > 16)
                        throw new Exception("Test plan reference level is too high. You might be trying to load a recursive test plan.");

                    var newSerializer = new TapSerializer();
                    if (currentSerializer != null)
                        newSerializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues.MergeInto(currentSerializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues);
                    var ext = newSerializer.GetSerializer<ExternalParameterSerializer>();
                    ForwardedParameters.ToList().ForEach(e =>
                    {
                        ext.PreloadedValues[e.Name] = StringConvertProvider.GetString(e.Value);
                    });

                    CurrentMappings = allMapping;

                    TestPlan tp = (TestPlan)newSerializer.DeserializeFromFile(Data);

                    ForwardedParameters = tp.ExternalParameters.Entries.ToArray();

                    var flatSteps = Utils.FlattenHeirarchy(tp.ChildTestSteps, x => x.ChildTestSteps);

                    StepIdMapping = flatSteps.ToDictionary(x => x, x => x.Id);

                    foreach (var step in flatSteps)
                    {
                        Guid id;
                        if (mapping.TryGetValue(step.Id, out id))
                            step.Id = id;
                    }
                    ChildTestSteps.AddRange(tp.ChildTestSteps);

                    var plan = GetParent<TestPlan>();

                    foreach (var step in RecursivelyGetChildSteps(TestStepSearch.All))
                    {
                        step.IsReadOnly = true;
                        step.ChildTestSteps.IsReadOnly = true;
                        step.OnPropertyChanged("");
                    }
                }
                finally
                {
                    LevelCounter--;
                    CurrentMappings = prevc;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unable to read '{0}'.", Filepath.Text);
                Log.Error(ex);
            }
        }
        
        /// <summary> Used to determine if a step ID has changed from the time it was loaded to the time it is saved. </summary>
        Dictionary<ITestStep, Guid> StepIdMapping { get; set; }
        public List<GuidMapping> mapping;

        [Browsable(false)]
        public List<GuidMapping> StepMapping
        {
            get {
                if (StepIdMapping == null)
                    return new List<GuidMapping>();
                var subMappings = Utils.FlattenHeirarchy(ChildTestSteps, x => x.ChildTestSteps).OfType<TestPlanReference>().SelectMany(x => (IEnumerable< KeyValuePair < ITestStep, Guid > >)x.StepIdMapping ?? Array.Empty<KeyValuePair<ITestStep, Guid>>()).ToArray();
                return StepIdMapping.Concat(subMappings).Select(x => new GuidMapping { Guid2 = x.Key.Id, Guid1 = x.Value }).Where(x => x.Guid1 != x.Guid2).ToList(); }
            set { mapping = value; }
        }

        [Browsable(true)]
        [Display("Load Test Plan", Order: 1, Description: "Load the selected test plan.")]
        public void LoadTestPlan()
        {
            loadTestPlan();
        }
        
        
        public void loadTestPlan()
        {
            ChildTestSteps.Clear();
            
            if (string.IsNullOrWhiteSpace(Filepath))
            {
                ForwardedParameters = Array.Empty<ExternalParameter>();

                //Log.Warning("No test plan configured.");
                return;
            }
            
            UpdateStep();
        }
        internal ExternalParameter[] ForwardedParameters { get; private set; } = Array.Empty<ExternalParameter>();
    }

    public class ExpandedMemberData : IMemberInfo
    {
        public override bool Equals(object obj)
        {
            if(obj is ExpandedMemberData mem)
            {
                return object.Equals(mem.DeclaringType, DeclaringType) && object.Equals(mem.Name, Name);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return DeclaringType.GetHashCode() ^ Name.GetHashCode();
        }

        public ITypeInfo DeclaringType { get; set; }

        public IEnumerable<object> Attributes { get; private set; }
        
        public string Name { get; set; }

        public bool Writable => true;

        public bool Readable => true;

        public ITypeInfo TypeDescriptor { get; set; }

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
                var tpr = (this.DeclaringType as ExpandedTypeInfo).Object as TestPlanReference;
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

        static DelayDeserializeAttribute delaySerializeAttribute = new DelayDeserializeAttribute();
        static BrowsableAttribute nonBrowsable = new BrowsableAttribute(false);
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
                groups = new[] { "Settings" }.Append(dis.Group).ToArray();
            attrs.Add(new DisplayAttribute(ep.Name, Description: dis.Description, Order:5, Groups: groups));
            Attributes = attrs;
        }

    }

    public class ExpandedTypeInfo : ITypeInfo
    {
        public override bool Equals(object obj)
        {
            if(obj is ExpandedTypeInfo exp)
                return exp.Object == Object;
            return false;
        }

        public override int GetHashCode()
        {
            return Object.GetHashCode() ^ 0x1111234;
        }

        public ITypeInfo InnerDescriptor;
        public TestPlanReference Object;
        public ITypeInfoProvider Provider { get; set; }

        public string Name => ExpandMemberDataProvider.exp + InnerDescriptor.Name;

        public IEnumerable<object> Attributes => InnerDescriptor.Attributes;

        public ITypeInfo BaseType => InnerDescriptor;

        public bool CanCreateInstance => InnerDescriptor.CanCreateInstance;

        public object CreateInstance(object[] arguments)
        {
            return InnerDescriptor.CreateInstance(arguments);
        }
        
        bool validName(string epName)
        {
            if (epName == null || epName.Length == 0) return false;
            if(char.IsDigit(epName[0])) return false;
            foreach (var c in epName)
            {
                if (false == (char.IsLetterOrDigit(c) || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }

        public IMemberInfo GetMember(string memberName)
        {
            
            for(int i = 0; i < Object.ForwardedParameters.Length; i++)
            {
                var ep = Object.ForwardedParameters[i];
                string epName = ep.Name;
                
                if (!validName(epName))
                {
                    epName = "prop" + i;
                }

                if (epName == memberName)
                    return new ExpandedMemberData(ep, epName) { DeclaringType = this};
            }
            return InnerDescriptor.GetMember(memberName);
        }

        public IEnumerable<IMemberInfo> GetMembers()
        {
            List<IMemberInfo> members = new List<IMemberInfo>();
            var innerMembers = InnerDescriptor.GetMembers();
            for(int i = 0; i < Object.ForwardedParameters.Length; i++)
            {
                var ep = Object.ForwardedParameters[i];
                string epName = ep.Name;
                if (!validName(epName))
                {
                    epName = "prop" + i; 
                }
                
                members.Add(new ExpandedMemberData(ep, epName) { DeclaringType = this});
            }
            
            foreach (var mem in innerMembers)
                members.Add(mem);
            return members;
        }
    }

    public class ExpandMemberDataProvider : ITypeInfoProvider
    {
        public double Priority => 1;
        internal const string exp = "ref@";
        public void GetTypeInfo(TypeInfoResolver resolver, string identifier)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeInfo.GetTypeInfo(identifier.Substring(exp.Length));
                if (tp != null)
                {
                    resolver.Stop(new ExpandedTypeInfo() { InnerDescriptor = tp, Object = null, Provider = this });
                }
            }
        }

        public void GetTypeInfo(TypeInfoResolver res, object obj)
        {
            if (obj is TestPlanReference exp)
            {
                res.Iterate(obj);
                if (res.FoundType != null)
                {
                    var expDesc = new ExpandedTypeInfo();
                    expDesc.InnerDescriptor = res.FoundType;
                    expDesc.Provider = this;
                    expDesc.Object = exp;
                    res.Stop(expDesc);
                }
            }
        }
    }
}
