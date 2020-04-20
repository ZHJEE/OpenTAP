using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display("Sweep Loop 2", "Table based loop that sweeps the value of its parameters based on a set of values.", "Flow Control")]
    public class SweepLoop2 : LoopTestStep
    {
        public IEnumerable<IMemberData> SweepProperties =>
            TypeData.GetTypeData(this).GetMembers().OfType<IForwardedMemberData>().Where(x =>
                x.HasAttribute<UnsweepableAttribute>() == false && x.Writable && x.Readable);
        
        SweepRowCollection rows = new SweepRowCollection();
        [DeserializeOrder(1)] // this should be deserialized as the last thing.
        public SweepRowCollection Rows 
        { 
            get => rows;
            set
            {
                rows = value;
                rows.Loop = this;
            }
        } 
        
        public SweepLoop2()
        {
            Rows.Loop = this;
        }
        
        public override void Run()
        {
            base.Run();

            var sets = SweepProperties.ToArray();
            var originalValues = sets.Select(set => set.GetValue(this)).ToArray();

            var disps = SweepProperties.Select(x => x.GetDisplayAttribute()).ToList();
            string names = string.Join(", ", disps.Select(x => x.Name));
            
            if (disps.Count > 1)
                names = string.Format("{{{0}}}", names);
            var rowType = Rows.Select(x => TypeData.GetTypeData(x)).FirstOrDefault();
            foreach (var Value in Rows)
            {
                if (Value.Enabled == false) continue;
                var AdditionalParams = new ResultParameters();

                
                foreach (var set in sets)
                {
                    var mem = rowType.GetMember(set.Name);
                    var val = StringConvertProvider.GetString(mem.GetValue(Value), CultureInfo.InvariantCulture);
                    var disp = mem.GetDisplayAttribute();
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name, val));

                    try
                    {
                        var value = StringConvertProvider.FromString(val, set.TypeDescriptor, this, CultureInfo.InvariantCulture);
                        set.SetValue(this, value);
                    }
                    catch (TargetInvocationException ex)
                    {
                        Log.Error("Unable to set '{0}' to value '{2}': {1}", set.GetDisplayAttribute().Name, ex.InnerException.Message, Value);
                        Log.Debug(ex.InnerException);
                    }
                }
                // Notify that values might have changes
                OnPropertyChanged("");
                
                 Log.Info("Running child steps with {0} = {1} ", names, Value);

                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested).ToList();
                if (BreakLoopRequested.IsCancellationRequested) break;
                runs.ForEach(r => r.WaitForCompletion());
            }
            for (int i = 0; i < sets.Length; i++)
                sets[i].SetValue(this, originalValues[i]);
        }
    }
}