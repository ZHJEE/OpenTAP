using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
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
            
            foreach (var Value in Rows)
            {
                var val = StringConvertProvider.GetString(Value, CultureInfo.InvariantCulture);
                foreach (var set in sets)
                {
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

                
                var AdditionalParams = new ResultParameters();
                /*
                foreach (var disp in disps)
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name, Value));
*/
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