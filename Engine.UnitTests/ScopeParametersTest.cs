using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ScopeParametersTest
    {

        [Test]
        public void ScopeStepTest()
        {

            var diag = new DialogStep() {UseTimeout = true};
            var diag2 = new DialogStep();
            var scope = new SequenceStep();
            string parameter = "Scope\"" + DisplayAttribute.GroupSeparator + "Title"; // name intentionally weird to mess with the serializer.
            scope.ChildTestSteps.Add(diag);
            scope.ChildTestSteps.Add(diag2);
            var member = TypeData.GetTypeData(diag).GetMember("Title");
            DynamicMemberOperations.AddForwardedMember(scope, member, diag, parameter);
            DynamicMemberOperations.AddForwardedMember(scope, member, diag2, parameter);
            DynamicMemberOperations.AddForwardedMember(scope, TypeData.GetTypeData(diag).GetMember("Timeout"), diag, "Group\\The Timeout");

            var annotation = AnnotationCollection.Annotate(scope);
            var titleMember = annotation.GetMember(parameter);
            titleMember.Get<IStringValueAnnotation>().Value = "New title";
            annotation.Write();
            Assert.AreEqual("New title", diag.Title);
            Assert.AreEqual("New title", diag2.Title);
            
            var timeoutMember = annotation.GetMember("Group\\The Timeout");
            Assert.IsFalse(timeoutMember.Get<IAccessAnnotation>().IsReadOnly);
            Assert.AreEqual("Group", TypeData.GetTypeData(scope).GetMember("Group\\The Timeout").GetDisplayAttribute().Group[0]);

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
            
            DynamicMemberOperations.RemoveForwardedMember(scope2, forwardedMember, scope2.ChildTestSteps[0], member);
            Assert.IsNotNull(TypeData.GetTypeData(scope2).GetMember(parameter));
            DynamicMemberOperations.RemoveForwardedMember(scope2, forwardedMember, scope2.ChildTestSteps[1], member);
            Assert.IsNull(TypeData.GetTypeData(scope2).GetMember(parameter)); // last 'Title' removed.
        }

        [Test]
        public void MultiLevelScopeSerialization()
        {
            var plan = new TestPlan();
            var seq1 = new SequenceStep();
            var seq2 = new SequenceStep();
            var delay = new DelayStep();
            plan.ChildTestSteps.Add(seq1);
            seq1.ChildTestSteps.Add(seq2);
            seq2.ChildTestSteps.Add(delay);

            var member1 = DynamicMemberOperations.AddForwardedMember(seq2,
                TypeData.GetTypeData(delay).GetMember(nameof(DelayStep.DelaySecs)), delay, "delay");
            DynamicMemberOperations.AddForwardedMember(seq1, member1, seq2, null);
            var str = new TapSerializer().SerializeToString(plan);

            var plan2 = (TestPlan)new TapSerializer().DeserializeFromString(str);
            var member2 = TypeData.GetTypeData(plan2.ChildTestSteps[0]).GetMember(member1.Name);
            var val = member2.GetValue(plan2.ChildTestSteps[0]);
            Assert.AreEqual(delay.DelaySecs, val);
        }

        [Test]
        public void SweepLoop2Test()
        {
            var plan = new TestPlan();
            var sweep = new SweepLoop2();
            var diag = new DialogStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(diag);
            sweep.Rows.Add(new SweepRow());

            DynamicMemberOperations.AddForwardedMember(sweep,
                TypeData.GetTypeData(diag).GetMember(nameof(DialogStep.Message)), diag, null);

            var td1 = TypeData.GetTypeData(sweep.Rows[0]);
            var members = td1.GetMembers().ToArray();
            members.Last().SetValue(sweep.Rows[0], "hej hej");
            var str = new TapSerializer().SerializeToString(plan);
            var plan2 = (TestPlan)new TapSerializer().DeserializeFromString(str);
            var sweep2 = (SweepLoop2) plan2.Steps[0];
            var td2 = TypeData.GetTypeData(sweep2);
            var members2 = td2.GetMembers();
            var rows = sweep2.Rows;
            Assert.AreEqual(1, rows.Count);
            var msgmem = TypeData.GetTypeData(rows[0]).GetMember("Message");
            Assert.AreEqual("hej hej", msgmem.GetValue(rows[0]));

            var annotated = AnnotationCollection.Annotate(sweep2);
            var messageMember = annotated.GetMember("Message");
            Assert.IsFalse(messageMember.Get<IEnabledAnnotation>().IsEnabled);


        }
    }
}