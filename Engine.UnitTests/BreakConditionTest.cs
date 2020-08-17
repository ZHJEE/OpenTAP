using System;
using System.Threading;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class BreakConditionsTest
    {
        public class TestStepFailsTimes : TestStep
        {
            public int fails = 0;
            public int timesRun = 0;

            public override void PrePlanRun()
            {
                fails = 5;
                timesRun = 0;
                base.PrePlanRun();
            }

            public override void Run()
            {
                timesRun += 1;
                if (fails > 0)
                {
                    fails -= 1;
                    throw new Exception("Intended failure!");
                }

                UpgradeVerdict(Verdict.Pass);
            }
        }

        // The new abort condition system gives many possibilities.
        // 1. everything inherits (same as without)
        // 2. step not allowed to fail
        // 3. and or step not allowed to error
        // 4. step allowed to fail
        // 5. and or step allowed to error
        // Retry:
        // 2. Single step retry until not fail.
        //    - Step passes
        //    - Step creates an error
        //    - Step never passes -> break?
        // 3. Parent step retry until child steps pass
        // what about inconclusive??


        [TestCase(Verdict.Error, InternalBreakCondition.BreakOnError)]
        [TestCase(Verdict.Fail, InternalBreakCondition.BreakOnFail)]
        [TestCase(Verdict.Inconclusive, InternalBreakCondition.BreakOnInconclusive)]
        [TestCase(Verdict.Inconclusive,
            InternalBreakCondition.BreakOnInconclusive | InternalBreakCondition.BreakOnError)]

        public void TestStepBreakOnError(Verdict verdictOutput, object _condition)
        {
            // _condition arg cannot be a BreakCondition as BreakCondition is not public.
            InternalBreakCondition condition = (InternalBreakCondition) _condition;
            var l = new PlanRunCollectorListener();
            TestPlan plan = new TestPlan();
            var verdict = new VerdictStep
            {
                VerdictOutput = verdictOutput
            };
            BreakConditionProperty.SetBreakCondition(verdict, condition);
            var verdict2 = new VerdictStep
            {
                VerdictOutput = Verdict.Pass
            };
            
            plan.Steps.Add(verdict);
            plan.Steps.Add(verdict2);
            var run = plan.Execute(new[] {l});
            Assert.AreEqual(verdictOutput, run.Verdict);
            Assert.AreEqual(1, l.StepRuns.Count);
            Assert.AreEqual(InternalBreakCondition.Inherit, BreakConditionProperty.GetBreakCondition(verdict2));
        }

        [TestCase(Verdict.Pass, EngineSettings.AbortTestPlanType.Step_Error, 2)]
        [TestCase(Verdict.Fail, EngineSettings.AbortTestPlanType.Step_Error, 2)]
        [TestCase(Verdict.Error, EngineSettings.AbortTestPlanType.Step_Error, 1)]
        [TestCase(Verdict.Fail, EngineSettings.AbortTestPlanType.Step_Fail, 1)]
        [TestCase(Verdict.Fail,
            EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, 1)]
        [TestCase(Verdict.Error,
            EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, 1)]
        [TestCase(Verdict.Pass,
            EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, 2)]
        public void EngineInheritedConditions(Verdict verdictOutput, EngineSettings.AbortTestPlanType abortTestPlanType,
            int runCount)
        {
            Verdict finalVerdict = verdictOutput;
            var prev = EngineSettings.Current.AbortTestPlan;
            try
            {
                EngineSettings.Current.AbortTestPlan = abortTestPlanType;
                var l = new PlanRunCollectorListener();
                TestPlan plan = new TestPlan();
                var verdict = new VerdictStep
                {
                    VerdictOutput = verdictOutput
                };
                BreakConditionProperty.SetBreakCondition(verdict, InternalBreakCondition.Inherit);
                var verdict2 = new VerdictStep
                {
                    VerdictOutput = Verdict.Pass
                };
                plan.Steps.Add(verdict);
                plan.Steps.Add(verdict2);
                var run = plan.Execute(new[] {l});
                Assert.AreEqual(finalVerdict, run.Verdict);
                Assert.AreEqual(runCount, l.StepRuns.Count);
                Assert.AreEqual(InternalBreakCondition.Inherit, BreakConditionProperty.GetBreakCondition(verdict2));
            }
            finally
            {
                EngineSettings.Current.AbortTestPlan = prev;
            }
        }

        [Test]
        public void EngineInheritedConditions2()
        {
            Verdict verdictOutput = Verdict.Fail;
            EngineSettings.AbortTestPlanType abortTestPlanType = EngineSettings.AbortTestPlanType.Step_Fail;
            int runCount = 1;
            Verdict finalVerdict = verdictOutput;
            var prev = EngineSettings.Current.AbortTestPlan;
            try
            {
                EngineSettings.Current.AbortTestPlan = abortTestPlanType;
                var l = new PlanRunCollectorListener();
                TestPlan plan = new TestPlan();
                var verdict = new VerdictStep
                {
                    VerdictOutput = verdictOutput,
                };
                BreakConditionProperty.SetBreakCondition(verdict, InternalBreakCondition.Inherit | InternalBreakCondition.BreakOnError);
                var verdict2 = new VerdictStep
                {
                    VerdictOutput = Verdict.Pass
                };
                plan.Steps.Add(verdict);
                plan.Steps.Add(verdict2);
                var run = plan.Execute(new[] {l});
                Assert.AreEqual(finalVerdict, run.Verdict);
                Assert.AreEqual(runCount, l.StepRuns.Count);
                Assert.AreEqual(InternalBreakCondition.Inherit, BreakConditionProperty.GetBreakCondition(verdict2));
            }
            finally
            {
                EngineSettings.Current.AbortTestPlan = prev;
            }
        }

        /// <summary>  This step overrides the verdict of the child steps. </summary>
        [AllowAnyChild]
        class VerdictOverrideStep : TestStep
        {
            public Verdict OutputVerdict { get; set; } = Verdict.Pass;
            public override void Run()
            {
                foreach (var step in EnabledChildSteps)
                    RunChildStep(step, throwOnError: false);
                this.Verdict = Verdict.Pass;
            }
        }

        [Test]
        public void StepsCanOverrideVerdicts()
        {
            var plan = new TestPlan();
            var stepCatch = new VerdictOverrideStep { OutputVerdict = Verdict.Pass };
            var verdict = new VerdictStep { VerdictOutput = Verdict.Error };
            plan.ChildTestSteps.Add(stepCatch);
            stepCatch.ChildTestSteps.Add(verdict);

            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }

        public class TestStepWaitInfinite : TestStep
        {
            public Semaphore Sem = new Semaphore(0,1);
        
            public override void Run()
            {
                Sem.Release();
                TapThread.Sleep(TimeSpan.MaxValue);
            }
        }

        public class TestStepAbortPlan : TestStep
        {
            public BreakConditionsTest.TestStepWaitInfinite WaitFor { get; set; }
            public override void Run()
            {
                WaitFor?.Sem.WaitOne();
                PlanRun.MainThread.Abort();
            }
        }

        [Test]
        public void TestStepWaitInfiniteAndAbortTest()
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            var abort = new TestStepAbortPlan(){WaitFor =  new TestStepWaitInfinite()};
            parallel.ChildTestSteps.Add(abort);
            parallel.ChildTestSteps.Add(abort.WaitFor);
            plan.ChildTestSteps.Add(parallel);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Aborted, run.Verdict);
            foreach (var step in parallel.ChildTestSteps)
            {
                Assert.AreEqual(Verdict.Aborted, step.Verdict);    
            }
        }

        [Test]
        public void TestPlanBreakConditions()
        {
            var plan = new TestPlan();
            
            var errorStep = new VerdictStep() {VerdictOutput = Verdict.Error};
            var failStep = new VerdictStep() {VerdictOutput = Verdict.Fail};
            var inconclusiveStep = new VerdictStep() {VerdictOutput = Verdict.Inconclusive};
            var passStep = new VerdictStep() {VerdictOutput = Verdict.Pass};
            
            plan.Steps.Add(errorStep);
            plan.Steps.Add(failStep);
            plan.Steps.Add(inconclusiveStep);
            plan.Steps.Add(passStep);
            
            // break on fail, this means that 'passStep' will not get executed 
            BreakConditionProperty.SetBreakCondition(plan, InternalBreakCondition.BreakOnError);
            var col = new PlanRunCollectorListener();
            plan.Execute(new []{col});
            Assert.AreEqual(1, col.StepRuns.Count);
            
            BreakConditionProperty.SetBreakCondition(plan, InternalBreakCondition.BreakOnFail);
            col = new PlanRunCollectorListener();
            plan.Execute(new []{col});
            Assert.AreEqual(2, col.StepRuns.Count);
            
            BreakConditionProperty.SetBreakCondition(plan, InternalBreakCondition.BreakOnInconclusive);
            col = new PlanRunCollectorListener();
            plan.Execute(new []{col});
            Assert.AreEqual(3, col.StepRuns.Count);
            
            BreakConditionProperty.SetBreakCondition(plan, 0);
            col = new PlanRunCollectorListener();
            plan.Execute(new []{col});
            Assert.AreEqual(4, col.StepRuns.Count);
            
        }
        
    }
}