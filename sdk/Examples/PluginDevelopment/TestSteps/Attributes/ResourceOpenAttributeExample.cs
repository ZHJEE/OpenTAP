using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Resource Open Management Example",
    Groups: new[] { "Examples", "Plugin Development", "Attributes" },
    Description: "TestStep that uses ResourceOpen attribute to show the different resource open modes.")]
    public class ResourceOpenAttributeExample : TestStep
    {
        public ResourceOpenAttributeExample() { }
        public BaseInstrument BaseInstr { get; set; }

        public override void Run()
        {
            BaseInstr.Open();
        }
    }

    public class BaseInstrument : Instrument
    {
        [ResourceOpen(ResourceOpenBehavior.Before)]
        public NormInstr NormSubInstr { get; set; }

        [ResourceOpen(ResourceOpenBehavior.InParallel)]
        public ParallelInstr ParallelSubInstr { get; set; }

        [ResourceOpen(ResourceOpenBehavior.Ignore)]
        public IgnoreInstr IgnoreSubInstr { get; set; }

        public override void Open()
        {
            base.Open();
            PrintSubInstrStatus();
        }

        public override void Close()
        {
            base.Close();
            PrintSubInstrStatus();
        }

        public void PrintSubInstrStatus()
        {
            Log.Info("normSubInstr connected: {0}", NormSubInstr.IsConnected);
            Log.Info("parallelSubInstr connected: {0}", ParallelSubInstr.IsConnected);
            Log.Info("ignoreSubInstr connected: {0}", IgnoreSubInstr.IsConnected);
        }
    }

    public class NormInstr : Instrument
    {
        public override void Open()
        {
            base.Open();
            TapThread.Sleep(2000);
        }
    }

    public class ParallelInstr : Instrument
    {
        public override void Open()
        {
            base.Open();
            TapThread.Sleep(2000);
        }
    }

    public class IgnoreInstr : Instrument
    {
        public override void Open()
        {
            base.Open();
            TapThread.Sleep(2000);
        }
    }
}
