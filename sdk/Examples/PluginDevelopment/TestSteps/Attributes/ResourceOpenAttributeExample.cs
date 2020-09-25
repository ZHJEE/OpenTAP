using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Resource Open Before Example",
        Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "TestStep that uses ResourceOpen attribute to show the different resource open modes.")]
    public class ResourceOpenBeforeAttributeExample : TestStep
    {
        public ResourceOpenBeforeAttributeExample() { }
        public BaseInstrument BaseInstr { get; set; }
        public override void Run() { }
    }

    [Display("Resource Open Parallel Example",
        Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "TestStep that uses ResourceOpen attribute to show the different resource open modes.")]
    public class ResourceOpenParallelAttributeExample : TestStep
    {
        public ResourceOpenParallelAttributeExample() { }
        public ParallelBaseInstrument BaseInstr { get; set; }

        public override void Run() { }
    }

    // Subinstr is open before base instr is open, a sleep delay is used to show this sequence.
    // Subinstr will close 2 sec after base instr is closed.
    public class BaseInstrument : Instrument
    {
        [ResourceOpen(ResourceOpenBehavior.Before)]
        public Instrument SubInstr { get; set; }

        [ResourceOpen(ResourceOpenBehavior.Ignore)]
        public Instrument IgnoreSubInstr { get; set; }

        public override void Open()
        {
            TapThread.Sleep(2000);
            Log.Info("Opening Base Instrument");
            base.Open();
            PrintSubInstrStatus();
        }

        public override void Close()
        {
            TapThread.Sleep(1000);  // Can be some operations that take eg. 1 sec just to show it is connected

            Log.Info("Closing Base Instrument");
            base.Close();
            TapThread.Sleep(2000);
            PrintSubInstrStatus();
        }

        public void PrintSubInstrStatus()
        {
            Log.Info("NormSubInstr connected: {0}", SubInstr.IsConnected);
            Log.Info("IgnoreSubInstr connected: {0}", IgnoreSubInstr.IsConnected);
        }
    }

    // Subinstr will open in parallel with base instr. To show the difference in opening time, base instr is delayed by 2 secs.
    // Subinstr will close as soon as connection is no longer needed. To show the sequence, base instr is delayed to close by 2 secs.
    public class ParallelBaseInstrument : Instrument
    {
        [ResourceOpen(ResourceOpenBehavior.InParallel)]
        public Instrument SubInstr { get; set; }

        [ResourceOpen(ResourceOpenBehavior.Ignore)]
        public Instrument IgnoreSubInstr { get; set; }

        public override void Open()
        {
            TapThread.Sleep(2000);
            Log.Info("Opening Base Instrument");
            base.Open();
            PrintSubInstrStatus();
        }

        public override void Close()
        {
            Log.Info("Closing Base Instrument");
            TapThread.Sleep(2000);
            base.Close();
            PrintSubInstrStatus();
        }

        public void PrintSubInstrStatus()
        {
            Log.Info("ParallelSubInstr connected: {0}", SubInstr.IsConnected);
            Log.Info("IgnoreSubInstr connected: {0}", IgnoreSubInstr.IsConnected);
        }
    }
}
