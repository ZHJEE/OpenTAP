using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTap.Cli;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{

    public class TestPlanPerformanceTest
    {
        class DeferringResultStep : TestStep
        {
            static double result1 = 5;
            static double result2 = 5;

            public override void Run()
            {
                Results.Defer(() => { Results.Publish("Test", new {X = result1, Y = result2}); });
            }
        }

        class VirtualPropertiesStep : TestStep
        {
            public virtual string X { get; set; }
            public virtual string Y { get; set; }
            public virtual double Z { get; set; }
            [Browsable(false)] public virtual double[] Values { get; set; } = new double[1024];

            public override void Run()
            {

            }
        }

        public class ManySettingsStep : TestStep
        {
            public int A { get; set; } = 123;
            public int[] B { get; set; } = new[] {1, 2, 3};
            public Instrument[] C { get; set; } = Array.Empty<Instrument>();
            public Instrument[] D { get; set; } = Array.Empty<Instrument>();
            public Instrument[] E { get; set; } = Array.Empty<Instrument>();
            public string F { get; set; } = "Hello world!!";

            [EnabledIf(nameof(A), 123)]
            public Enabled<string> G { get; set; } = new Enabled<string>() {Value = "Hello"};

            [EnabledIf(nameof(A), 123)] public List<string> H { get; set; } = new List<string> {"1 2 3"};
            [EnabledIf(nameof(A), 123)] public Enabled<double> I { get; set; } = new Enabled<double>();
            [EnabledIf(nameof(A), 123)] public ITestStep Step { get; set; }

            public override void Run()
            {

            }
        }


        public void GeneralPerformanceTest(int count)
        {
            void buildSequence(ITestStepParent parent, int levels)
            {
                parent.ChildTestSteps.Add(new ManySettingsStep());
                //parent.ChildTestSteps.Add(new DeferringResultStep());
                parent.ChildTestSteps.Add(new VirtualPropertiesStep());
                for (int i = 0; i < levels; i++)
                {
                    var seq = new SequenceStep();
                    parent.ChildTestSteps.Add(seq);
                    buildSequence(seq, levels / 2);
                }
            }

            var plan = new TestPlan();
            buildSequence(plan, 6);
            var total = Utils.FlattenHeirarchy(plan.ChildTestSteps, x => x.ChildTestSteps).Count();

            plan.Execute(); // warm up

            TimeSpan timeSpent = TimeSpan.Zero;

            for (int i = 0; i < count; i++)
            {

                timeSpent += plan.Execute().Duration;

            }


            var proc = Process.GetCurrentProcess();
            var time = proc.TotalProcessorTime;
            var time2 = DateTime.Now - proc.StartTime;
            var spentMs = timeSpent.TotalMilliseconds / count;
            Console.WriteLine("Time spent per plan: {0}ms", spentMs);
            Console.WriteLine("Time spent per step: {0}ms", spentMs / total);
        }
    }

    [Display("profile")]
    public class ProfileAction : ICliAction
    {
        [CommandLineArgument("time-span")] public bool ProfileTimeSpanToString { get; set; }

        [CommandLineArgument("test-plan")] public bool ProfileTestPlan { get; set; }

        [CommandLineArgument("iterations")] public int Iterations { get; set; } = 1000;

        public int Execute(CancellationToken cancellationToken)
        {
            if (ProfileTimeSpanToString)
            {
                StringBuilder sb = new StringBuilder();
                ShortTimeSpan.FromSeconds(0.01).ToString(sb);
                var sw = Stopwatch.StartNew();


                for (int i = 0; i < Iterations; i++)
                {
                    //ShortTimeSpan.FromSeconds(0.01 * i).ToString(sb);
                    ShortTimeSpan.FromSeconds(0.01 * i).ToString(sb);
                    if (i % 10 == 0)
                        sb.Clear();
                }

                Console.WriteLine("TimeSpan: {0}ms", sw.ElapsedMilliseconds);
            }

            if (ProfileTestPlan)
                new TestPlanPerformanceTest().GeneralPerformanceTest(Iterations);

            return 0;
        }
    }
}