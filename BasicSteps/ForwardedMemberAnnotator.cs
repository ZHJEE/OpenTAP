using System.Collections;
using System.Linq;

namespace OpenTap.Plugins.BasicSteps
{
    public class ForwardedMemberAnnotator : IAnnotator
    {
        public double Priority => 20;
        class SubAvailable : IAvailableValuesAnnotation
        {
            public IEnumerable AvailableValues => sub.Get<IAvailableValuesAnnotation>().AvailableValues;

            AnnotationCollection sub;
            public SubAvailable(AnnotationCollection subcol) => sub = subcol;
        }

        class SubSuggested : ISuggestedValuesAnnotation
        {
            AnnotationCollection subcol;
            public SubSuggested(AnnotationCollection subcol) => this.subcol = subcol;
            public IEnumerable SuggestedValues => subcol.Get<ISuggestedValuesAnnotation>().SuggestedValues;
        }

        class SubAccess : IAccessAnnotation
        {
            private AnnotationCollection sub;

            public bool IsReadOnly => sub.Get<IAccessAnnotation>().IsReadOnly;
            public bool IsVisible => sub.Get<IAccessAnnotation>().IsVisible;
            public SubAccess(AnnotationCollection sub) => this.sub = sub;
        }

        class SubMember : IOwnedAnnotation
        {
            AnnotationCollection sub;

            public SubMember(AnnotationCollection sub)
            {
                this.sub = sub;
            }
            public void Read(object source)
            {
                sub.Read();
            }

            public void Write(object source)
            {
                sub.Write();
            }
        }

        public void Annotate(AnnotationCollection annotation)
        {
            var member = annotation.Get<IMemberAnnotation>()?.Member as IForwardedMemberData;
            if (member == null) return;
            
            var subannotation = AnnotationCollection.Annotate(member.Members.Select(x => x.Item1).ToArray());
            annotation.Add(new SubMember(subannotation));
            var subMembers = subannotation.Get<IMembersAnnotation>();
            var firstmem = member.Members.First().Item2;
            var thismember = subMembers.Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == firstmem);
            if (thismember == null) return;

            annotation.RemoveType<IAccessAnnotation>();
            annotation.Add(new SubAccess(thismember));

            IAvailableValuesAnnotation avail = annotation.Get<IAvailableValuesAnnotation>();
            if (avail != null) annotation.Add(new SubAvailable(thismember));

            ISuggestedValuesAnnotation suggested = annotation.Get<ISuggestedValuesAnnotation>();
            if (suggested != null) annotation.Add(new SubSuggested(thismember));
        }
    }
}