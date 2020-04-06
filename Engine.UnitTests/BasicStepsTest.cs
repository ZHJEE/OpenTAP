using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class BasicStepsTest
    {
        [TestCase(true, Verdict.Aborted, null)]
        [TestCase(false, Verdict.Error, null)]
        [TestCase(false, Verdict.Pass, Verdict.Pass)]
        public void TimeGuardStepTest(bool stopOnError, Verdict expectedVerdict, Verdict? verdictOnAbort)
        {
            var plan = new TestPlan();
            var guard = new TimeGuardStep {StopOnTimeout = stopOnError, Timeout = 0.05};
            if (verdictOnAbort != null)
                guard.TimeoutVerdict = verdictOnAbort.Value;
            
            // if this delay step runs to completion, the verdict of the test plan will be NotSet, failing the final assertion.
            var delay = new DelayStep {DelaySecs = 120};
            plan.ChildTestSteps.Add(guard);
            guard.ChildTestSteps.Add(delay);
            var run = plan.Execute();
            
            Assert.AreEqual(expectedVerdict, run.Verdict);
        }


        class PassThirdTime : TestStep
        {
            public int Iterations = 0;
            public override void PrePlanRun()
            {
                Iterations = 0;
                base.PrePlanRun();
            }

            public override void Run()
            {
                Iterations += 1;
                if (Iterations < 3)
                {
                    UpgradeVerdict(Verdict.Fail);
                }
                UpgradeVerdict(Verdict.Pass);
            }
        }
        
        [Test]
        [Pairwise]
        public void RepeatUntilPass([Values(true, false)] bool retry)
        {
            var step = new PassThirdTime();
            BreakConditionProperty.SetBreakCondition(step, BreakCondition.BreakOnFail);
            
            var rpt = new RepeatStep()
            {
                Action =  RepeatStep.RepeatStepAction.Until,
                TargetStep = step,
                TargetVerdict = Verdict.Pass,
                Retry = retry
            };
            rpt.ChildTestSteps.Add(step);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);

            var run = plan.Execute();

            if (retry)
            {
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.AreEqual(3, step.Iterations);
            }
            else
            {
                // break condition reached -> Error verdict.
                Assert.AreEqual(Verdict.Error, run.Verdict);
                Assert.AreEqual(1, step.Iterations);
            }
        }
        
        
        // These two cases are technically equivalent.
        [Test]
        [TestCase(Verdict.Error, RepeatStep.RepeatStepAction.While)]
        [TestCase(Verdict.Pass, RepeatStep.RepeatStepAction.Until)]
        public void RepeatWhileError(Verdict targetVerdict, RepeatStep.RepeatStepAction action)
        {
            var step = new PassThirdTime();
            BreakConditionProperty.SetBreakCondition(step, BreakCondition.BreakOnFail);
            
            var rpt = new RepeatStep()
            {
                Action =  action,
                TargetVerdict = targetVerdict,
                Retry = true
            };
            rpt.TargetStep = rpt; // target self. The Repeat Loop will inherit the verdict.
            rpt.ChildTestSteps.Add(step);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);

            var run = plan.Execute();

            Assert.AreEqual(Verdict.Pass, run.Verdict); 
            Assert.AreEqual(3, step.Iterations);
        }


        [Test]
        public void ScopeStepTest()
        {
            
            var diag = new DialogStep();
            var diag2 = new DialogStep();
            var scope = new SequenceStep();
            string parameter = "Scope\"" + DisplayAttribute.GroupSeparator + "Title"; // name intentionally weird to mess with the serializer.
            scope.ChildTestSteps.Add(diag);
            scope.ChildTestSteps.Add(diag2);
            var member = TypeData.GetTypeData(diag).GetMember("Title");
            DynamicMemberOperations.AddForwardedMember(scope, member, diag, parameter);
            DynamicMemberOperations.AddForwardedMember(scope, member, diag2, parameter);

            var annotation = AnnotationCollection.Annotate(scope);
            var titleMember = annotation.GetMember(parameter);
            titleMember.Get<IStringValueAnnotation>().Value = "New title";
            annotation.Write();
            Assert.AreEqual("New title", diag.Title);
            Assert.AreEqual("New title", diag2.Title);

            var plan = new TestPlan();
            plan.Steps.Add(scope);
            var str = new TapSerializer().SerializeToString(plan);
            var plan2 = (TestPlan)new TapSerializer().DeserializeFromString(str);
            var scope2 = plan2.Steps[0];
            var annotation2 = AnnotationCollection.Annotate(scope2);
            var titleMember2 = annotation2.GetMember(parameter);
            Assert.IsNotNull(titleMember2);
            titleMember2.Get<IStringValueAnnotation>().Value = "New Title 2";
            annotation2.Write();
            foreach (var step in scope2.ChildTestSteps.Cast<DialogStep>())
            {
                Assert.AreEqual(step.Title, "New Title 2");
            }

            var forwardedMember = TypeData.GetTypeData(scope2).GetMember(parameter);
            Assert.IsNotNull(forwardedMember);
            
            DynamicMemberOperations.RemoveForwardedmember(scope2, forwardedMember, scope2.ChildTestSteps[0], member);
            Assert.IsNotNull(TypeData.GetTypeData(scope2).GetMember(parameter));
            DynamicMemberOperations.RemoveForwardedmember(scope2, forwardedMember, scope2.ChildTestSteps[1], member);
            Assert.IsNull(TypeData.GetTypeData(scope2).GetMember(parameter)); // last 'Title' removed.
        }
    }
}