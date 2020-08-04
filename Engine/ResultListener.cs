//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;

namespace OpenTap
{
    /// <summary>
    /// Abstract class forming the basis for all ResultListeners.
    /// </summary>
    public abstract class ResultListener : Resource, IResultListener, IEnabledResource
    {
        bool isEnabled = true;
        ///<summary> Gets or sets if this resource is enabled.</summary>
        [Browsable(false)]
        public bool IsEnabled {

            get => isEnabled;
            set
            {
                var oldValue = isEnabled;
                isEnabled = value;
                onEnabledChanged(oldValue, value);
            }
        }
        
        /// <summary> Called when IsEnabled is changed. </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected virtual void onEnabledChanged(bool oldValue, bool newValue)
        {

        }

        /// <summary>
        /// Called when a test plan starts.
        /// </summary>
        /// <param name="planRun">Test plan run parameters.</param>
        public virtual void OnTestPlanRunStart(TestPlanRun planRun)
        {
        }

        /// <summary>
        /// Called when test plan finishes. At this point no more results will be sent to the result listener from the test plan run.  
        /// </summary>
        /// <param name="planRun">Test plan run parameters.</param>
        /// <param name="logStream">The log file from the test plan run as a stream.</param>
        public virtual void OnTestPlanRunCompleted(TestPlanRun planRun, System.IO.Stream logStream)
        {
        }

        /// <summary>
        /// Called just before a test step is started.
        /// </summary>
        /// <param name="stepRun"></param>
        public virtual void OnTestStepRunStart(TestStepRun stepRun)
        {
        }

        /// <summary>
        /// Called when a test step run is completed.
        /// Result might still be propagated to the result listener after this event.
        /// </summary>
        /// <param name="stepRun">Step run parameters.</param>
        public virtual void OnTestStepRunCompleted(TestStepRun stepRun)
        {
        }

        /// <summary>
        /// Called when a result is received.
        /// </summary>
        /// <param name="stepRunId"> Step run ID.</param>
        /// <param name="result">Result structure.</param>
        public virtual void OnResultPublished(Guid stepRunId, ResultTable result)
        {
        }
    }

    /// <summary>
    /// Instructs the ResultListener not to save the 
    /// public property value as metadata for TestStep results.
    /// </summary>
    [Obsolete("This attribute is no longer in use and will be removed in a later version.")]
    [AttributeUsage(AttributeTargets.Property)]
    public class ResultListenerIgnoreAttribute : Attribute
    {

    }

    /// <summary>
    /// Represents a result parameter.
    /// </summary>
    [DebuggerDisplay("{Name} = {Value}")]
    [DataContract]
    public class ResultParameter : IParameter
    {
        /// <summary>
        /// Name of parameter.
        /// </summary>
        [DataMember]
        public readonly string Name;
        /// <summary>
        /// Pretty name of the parameter.  
        /// </summary>
        [DataMember]
        public readonly string Group;
        /// <summary>
        /// Value of the parameter. If null, the value is the string "NULL".  
        /// </summary>
        [DataMember]
        public IConvertible Value;
        /// <summary>
        /// Indicates the parameter came from a test step in a parent level above the initial object.  
        /// </summary>
        [DataMember]
        public readonly int ParentLevel;

        IConvertible IParameter.Value => Value;

        string IAttributedObject.Name => Name;

        string IParameter.Group => Group;

        string IAttributedObject.ObjectType => "Parameter";

        /// <summary> Gets if this result is metadata. </summary>
        public bool IsMetaData => MacroName != null;

        /// <summary> null or the macro name representation of the ResultParameter. This will make it possible to insert the parameter value into a string. <see cref="MacroString"/></summary>
        public readonly string MacroName;

        /// <summary> Creates a result parameter with default group.</summary>
        public ResultParameter(string name, IConvertible value)
        {
            Name = name;
            Value = value ?? "NULL";
            Group = "";
            MacroName = null;
        }

        /// <summary>  Initializes a new instance of ResultParameter.</summary>
        public ResultParameter(string group, string name, IConvertible value, string metadataName)
        {
            Group = group;
            Name = name;
            Value = value ?? "NULL";
            MacroName = metadataName;
        }
        /// <summary>
        /// Initializes a new instance of ResultParameter.
        /// </summary>
        public ResultParameter(string group, string name, IConvertible value, MetaDataAttribute metadata = null, int parentLevel = 0)
        {
            Group = group;
            Name = name;
            Value = value ?? "NULL";
            ParentLevel = parentLevel;
            if (metadata != null)
            {
                MacroName = metadata.MacroName ?? "";
            }
            else
            {
                MacroName = null;
            }
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            var parameter = obj as ResultParameter;
            return parameter != null &&
                   Name == parameter.Name &&
                   Group == parameter.Group &&
                   EqualityComparer<IConvertible>.Default.Equals(Value, parameter.Value) &&
                   ParentLevel == parameter.ParentLevel &&
                   MacroName == parameter.MacroName;
        }

        /// <summary>
        /// Calculates a hash code for the current object.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var hashCode = -1808396095;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Group);
            hashCode = hashCode * -1521134295 + EqualityComparer<IConvertible>.Default.GetHashCode(Value);
            hashCode = hashCode * -1521134295 + ParentLevel.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(MacroName);
            return hashCode;
        }

        /// <summary> Clones a single instance of ResultParameter. </summary>
        /// <returns></returns>
        public ResultParameter Clone() => (ResultParameter) MemberwiseClone();
    }

    /// <summary>
    /// A collection of parameters related to the results.
    /// </summary>
    public class ResultParameters : IReadOnlyList<ResultParameter>
    {
        IConvertible[] Value = Array.Empty<IConvertible>();
        string[] Name = Array.Empty<string>();
        
        ///<summary> it is much faster to search an array of ints than an array of strings. Hence, this contains the hashes
        /// of all the item in Name. </summary>
        int[] NameHashes = Array.Empty<int>();

        string[] group = null;
        string[] macroName = null;
        
        /// <summary>  This dictionary might be set if there are enough elements.  </summary>
        Dictionary<string, int> indexer = null;
        int nextIndex = -1;

        /// <summary>
        /// Gets the parameter with the given index.
        /// </summary>
        /// <param name="index"></param>
        public ResultParameter this[int index] => new ResultParameter(group?[index], Name[index], Value[index], macroName?[index]);

        /// <summary> Gets a ResultParameter by name. </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ResultParameter Find(string name)
        {
            int idx = FindIndex(name);
            if (idx == -1) return null;
            return this[idx];
        }

        Dictionary<string, int> getIndexer()
        {
            Dictionary<string, int> newIndexer;
            if(indexer == null) newIndexer = new Dictionary<string, int>();
            else newIndexer = new Dictionary<string, int>(indexer);
            for (int i = newIndexer.Count; i <= nextIndex; i++)
            {
                var n = Name[i];
                newIndexer.Add(n, i);
            }

            return newIndexer;
        }


        int FindIndex(string name)
        {
            // optimized FindIndex with lookup support. n = number of elements.
            // it does not resize the dictionary every time, because that is not thread safe.
            // instead it uses Array.IndexOf and then adds dictionary when the size becomes so big that it makes sense.
            // 8 seems to be the sweet spot, 4 is slower.
            // n < 8: Use Array.IndexOf, indexer == null;
            // n < 16: elements 0-7 are in the indexer, 7-15  
            var namehash = name.GetHashCode();
            if (Count < 16)
            {
                var count = Count;
                var hashes = NameHashes;
                int idx2 = Array.IndexOf(hashes, namehash, 0, Math.Min(count, hashes.Length));
                if (idx2 == -1)
                    return -1;
                if (Equals(Name[idx2], name))
                    return idx2;
                // collision
                return Array.IndexOf(Name, name, 0, Count);
            }

            if (indexer == null)
            {
                lock (this.resizeLock)
                {
                    if (indexer == null)
                    {
                        // create a new dict, dictionary is not thread safe.
                        indexer = getIndexer();
                    }
                }
            }

            if (indexer.TryGetValue(name, out int index))
                return index;
            var add = Count - indexer.Count;
            if (add < 16)
            {
                int idx2 = Array.IndexOf(NameHashes, namehash,indexer.Count, add);
                if (idx2 == -1)
                    return -1;
                if (Equals(Name[idx2], name))
                    return idx2;
                // collision
                return Array.IndexOf(Name, name, indexer.Count, add);
            }

            lock (this.resizeLock)
            {
                indexer = getIndexer();
            }
            if (indexer.TryGetValue(name, out int idx))
                return idx;
            return -1;    
        } 

        /// <summary>
        /// Gets the parameter with the key name.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IConvertible this[string key]
        {
            get
            {
                var idx = FindIndex(key);
                if (idx == -1) return null;
                return Value[idx];
            }
            set => Add(key, value);
        }

        static void getMetadataFromObject(object res, string nestedName, ICollection<ResultParameter> output)
        {
            GetPropertiesFromObject(res, output, nestedName);
        }
        
        static void getMetadataFromObject(object res, string nestedName, ResultParameters output)
        {
            GetPropertiesFromObject(res, output, nestedName, true);
        }

        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every property on the inputted 
        /// object decorated with <see cref="MetaDataAttribute"/>.
        /// </summary>
        public static ResultParameters GetMetadataFromObject(object res)
        {
            if (res == null)
                throw new ArgumentNullException("res");
            var parameters = new ResultParameters();
            getMetadataFromObject(res, "", parameters);
            return parameters;
        }
        
        /// <summary> Load the metadata from an object. </summary>
        public void IncludeMetadataFromObject(object res, string prefix = "")
        {
            if (res == null)
                throw new ArgumentNullException(nameof(res));
            getMetadataFromObject(res, prefix, this);
        }

        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every property on every 
        /// <see cref="ComponentSettings"/> implementation decorated with <see cref="MetaDataAttribute"/>. 
        /// </summary>
        public static ResultParameters GetComponentSettingsMetadata(bool expandComponentSettings = false)
        {
            var componentSettings = PluginManager //get component settings instances (lazy)
                .GetPlugins<ComponentSettings>()
                .Select(ComponentSettings.GetCurrent)
                .Where(o => o != null)
                .Cast<object>();

            var r = new ResultParameters();
            
            if (expandComponentSettings)
                componentSettings = componentSettings.Concat(componentSettings.OfType<IEnumerable>().SelectMany(c => (IEnumerable<object>)c));

            foreach (var comp in componentSettings)
                getMetadataFromObject(comp, "" , r);
            return r;
        }

        
        /// <summary>
        /// Lazily pull result parameters from component settings. Reduces the number of component settings XML that needs to be deserialized.
        /// </summary>
        /// <param name="includeObjects">If objects in componentsettingslists should be included.</param>
        /// <returns></returns>
        internal static IEnumerable<ResultParameters> GetComponentSettingsMetadataLazy(bool includeObjects)
        {
            TypeData engineSettingsType = TypeData.FromType(typeof(EngineSettings));
            AssemblyData engineAssembly = engineSettingsType.Assembly;

            int orderer(TypeData tp)
            {
                // always start with EngineSettings
                // prefer engine assemblies 
                // then loaded assemblies
                // then everything else.
                if (tp == engineSettingsType)
                    return 3;
                if (tp.Assembly == engineAssembly)
                    return 2;
                if (tp.Assembly.Status == LoadStatus.Loaded)
                    return 1;
                return 0;
            }

            var types = TypeData.FromType(typeof(ComponentSettings))
                .DerivedTypes.OrderByDescending(orderer).ToArray();

            foreach (var tp in types)
            {
                var t = tp.Load();
                if (tp.CanCreateInstance == false) continue;
                var componentSetting = ComponentSettings.GetCurrent(t);
                if (componentSetting != null)
                {
                    yield return GetMetadataFromObject(componentSetting);
                }
            }
            if (includeObjects == false) yield break;
            foreach (var tp in types)
            {
                var t = tp.Load();
                var componentSetting = ComponentSettings.GetCurrent(t);
                if(componentSetting is IEnumerable elements)
                {
                    foreach(var elem in elements)
                    {
                        yield return GetMetadataFromObject(elem);
                    }
                }
            }
        }


        /// <summary>
        /// Adds a new parameter to the resultParams list. if the parameter value is of the type Resource, every parameter from it is added, but not the origin object.
        /// </summary>
        static void GetParams(string group, string name, object value, MetaDataAttribute metadata, ICollection<ResultParameter> output)
        {
            if (value is IResource)
            {
                string resval = value.ToString();

                output.Add(new ResultParameter(group, name, resval, metadata));
                getMetadataFromObject(value, name + "/",output);
                return;
            }
            if (value == null)
            {
                value = "NULL";
            }

            var parentName = name;

            IConvertible val;
            if (value is IConvertible)
                val = value as IConvertible;
            else if((val = StringConvertProvider.GetString(value)) == null)
                val = value.ToString();

            output.Add( new ResultParameter(group, parentName, val, metadata));
        }

        static void GetParams(string group, string name, object value, MetaDataAttribute metadata, ResultParameters output)
        {
            if (value is IResource)
            {
                string resval = value.ToString();

                output.Add(group, name, resval, metadata);
                getMetadataFromObject(value, name + "/", output);
                return;
            }
            if (value == null)
            {
                value = "NULL";
            }

            var parentName = name;

            IConvertible val = value as IConvertible ?? StringConvertProvider.GetString(value) ?? value.ToString();
            output.Add(group, parentName, val, metadata);
        }
        
        static ConcurrentDictionary<ITypeData, (IMemberData member, string group, string name, MetaDataAttribute
            metadata)[]> propertiesLookup =
            new ConcurrentDictionary<ITypeData, (IMemberData member, string group, string name, MetaDataAttribute
                metadata)[]>();

        static (IMemberData member, string group, string name, MetaDataAttribute metadata)[] GetParametersMap(
            ITypeData type)
        {
            if (propertiesLookup.TryGetValue(type, out var result))
                return result;
            var lst = new List<(IMemberData member, string group, string name, MetaDataAttribute metadata)>();
            foreach (var prop in type.GetMembers())
            {
                if (!prop.Readable)
                    continue;
                if (prop.HasAttribute<NonMetaDataAttribute>())
                    continue;

                var metadataAttr = prop.GetAttribute<MetaDataAttribute>();
                if (metadataAttr == null)
                {
                    // if metadataAttr is specified, all we require is that we can read and write it. 
                    // Otherwise normal rules applies:

                    if (prop.Writable == false)
                        continue; // Don't add Properties with XmlIgnore attribute
                    
                    if (prop.HasAttribute<System.Xml.Serialization.XmlIgnoreAttribute>())
                        continue;

                    if (!prop.IsBrowsable())
                        continue;
                }

                var display = prop.GetDisplayAttribute();
                var metadata = prop.GetAttribute<MetaDataAttribute>();
                if (metadata != null && string.IsNullOrWhiteSpace(metadata.MacroName))
                    metadata = new MetaDataAttribute(metadata.PromptUser, display.Name);

                var name = display.Name.Trim();
                string group = null;

                if (display.Group.Length == 1) group = display.Group[0].Trim();

                lst.Add((prop, group, name, metadata));
            }

            result = lst.ToArray();
            propertiesLookup[type] = result;
            return result;
        }
        
        private static void GetPropertiesFromObject(object obj, ResultParameters output, string namePrefix = "", bool onlyMetadata = false)
        {
            if (obj == null)
                return;
            var type = TypeData.GetTypeData(obj);
            foreach (var (prop, group, name, metadata) in GetParametersMap(type))
            {
                if (onlyMetadata && metadata == null) continue;
                object value = prop.GetValue(obj);
                if (value == null)
                    continue;
                GetParams(group, namePrefix + name, value, metadata, output);
            }
        }
        
        private static void GetPropertiesFromObject(object obj, ICollection<ResultParameter> output, string namePrefix = "")
        {
            if (obj == null)
                return;
            var type = TypeData.GetTypeData(obj);
            foreach (var (prop, group, name, metadata) in GetParametersMap(type))
            {
                object value = prop.GetValue(obj);
                if (value == null)
                    continue;
                GetParams(group, namePrefix + name, value, metadata, output);
            }
        }

        internal static void UpdateParams(ResultParameters parameters, object obj, string namePrefix = "")
        {
            if (obj == null)
                return;
            var type = TypeData.GetTypeData(obj);
            var p = GetParametersMap(type);
            foreach (var (prop, group, _name, metadata) in p)
            {
                if (prop.GetAttribute<MetaDataAttribute>()?.Frozen == true) continue; 
                var name = namePrefix + _name;
                object value = prop.GetValue(obj);
                if (value == null)
                    continue;
                
                if (value is IResource)
                {
                    string resval = value.ToString();
                    parameters.Overwrite(name, resval, group, metadata);
                    UpdateParams(parameters, value, name);
                    continue;
                }
                
                IConvertible val = value as IConvertible ?? StringConvertProvider.GetString(value) ?? value.ToString();
                parameters.Overwrite(name, val, group, metadata);
            }
        }

        internal void Overwrite(string name, IConvertible value, string group, MetaDataAttribute metadata)
        {
            Add(group, name, value, metadata);
        }

        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every setting of the inputted 
        /// TestStep.
        /// </summary>
        public static ResultParameters GetParams(ITestStep step)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            var parameters = new ResultParameters();
            GetPropertiesFromObject(step, parameters);
            return parameters;
        }
        /// <summary>
        /// Initializes a new instance of the ResultParameters class.
        /// </summary>
        public ResultParameters() { }

        /// <summary>
        /// Initializes a new instance of the ResultParameters class.
        /// </summary>
        public ResultParameters(IEnumerable<ResultParameter> items) => AddRange(items);

        /// <summary>
        /// Returns a dictionary containing all the values in this list indexed by their <see cref="ResultParameter.Name"/>.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            foreach (ResultParameter val in this)
            {
                if(values.ContainsKey(val.Name) == false)
                    values.Add(val.Name, val.Value);
            }
            return values;
        }

        /// <summary>
        /// Returns the number of result parameters.
        /// </summary>
        public int Count => nextIndex + 1;

        /// <summary>
        /// Adds a new element to the parameters. (synchronized).
        /// </summary>
        /// <param name="parameter"></param>
        public void Add(ResultParameter parameter) => Add(parameter.Group, parameter.Name, parameter.Value, parameter.MacroName);

        /// <summary>
        /// Adds a new element
        /// </summary>
        /// <param name="group"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="metadata"></param>
        public void Add(string group, string name, IConvertible value, MetaDataAttribute metadata)
        {
            add(group, name, value, metadata?.MacroName);
        }
        /// <summary>  Adds a new element </summary>

        public void Add(string group, string name, IConvertible value, string metadataName)
        {
            add(group, name, value, metadataName);
        }
        
        readonly object resizeLock = new object();

        /// <summary> Add a new element. </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void Add(string name, IConvertible value) => add(null, name, value, null);

        long registeredCount = 0;
        void add(string group, string name, IConvertible value, string metadataName)
        {
            var idx = FindIndex(name);
            if (idx >= 0)
            {
                Value[idx] = value;
            }
            else
            {
                
                if (nextIndex + 1>= registeredCount)
                {
                    lock (resizeLock)
                    {
                        if (nextIndex + 1 >= Interlocked.Read(ref registeredCount))
                        {
                            
                            Array.Resize(ref Name, Name.Length + 6);
                            Array.Resize(ref Value, Value.Length + 6);
                            Array.Resize(ref NameHashes, NameHashes.Length + 6);
                            if (this.group != null)
                                Array.Resize(ref this.group, this.group.Length + 6);
                            if (macroName != null)
                                Array.Resize(ref macroName, macroName.Length + 6);
                            Interlocked.Add(ref registeredCount, 6);
                        }
                    }
                }

                idx = Interlocked.Increment(ref nextIndex);
                
                Name[idx] = name;
                NameHashes[idx] = name.GetHashCode();
                Value[idx] = value;
                if (group != null) 
                {
                    if(this.group == null)
                        this.group = new string[Name.Length];
                    this.group[idx] = group;
                }
                
                if(metadataName != null)
                {
                    if(macroName == null)
                        macroName = new string[Name.Length];
                    macroName[idx] = metadataName;
                }
            }
        }

        
        /// <summary> Adds a range of result parameters. </summary>
        /// <param name="parameters"></param>
        public void AddRange(IEnumerable<ResultParameter> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            foreach (var par in parameters)
                Add(par);
        }

        IEnumerator<ResultParameter> getEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator<ResultParameter> IEnumerable<ResultParameter>.GetEnumerator() => getEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => getEnumerator();

        /// <summary> Copies all the data inside a ResultParameters instance. </summary>
        /// <returns></returns>
        internal ResultParameters Clone() => new ResultParameters {Name = Name.ToArray(), Value = Value.ToArray(), NameHashes = NameHashes.ToArray(), @group = @group.ToArray(), macroName = macroName.ToArray()};

        internal IConvertible GetIndexed(string name, ref int index)
        {
            if(index == -1)
                index = FindIndex(name);
            return Value[index];
        }

        internal void SetIndexed(string name, ref int index, IConvertible value)
        {
            if(index == -1)
                index = FindIndex(name);
            if (index == -1)
                Add(name, value);
            else
                Value[index] = value;
        }
    }
    class NonMetaDataAttribute : Attribute
    {
        
    }
}
