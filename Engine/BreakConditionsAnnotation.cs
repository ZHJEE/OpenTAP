using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTap
{
    internal class BreakConditions : Enabled<BreakConditions.Values>
    {
        [Flags]
        public enum Values
        {
            /// <summary> If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
            [Display("On Error", "If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step.")]
            BreakOnError = 2,
            /// <summary> If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
            [Display("On Fail", "If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step.")]
            BreakOnFail = 4,
            /// <summary> If a step completes with verdict 'Inconclusive' the step should break execution.</summary>
            [Display("On Inconclusive", "If a step completes with verdict 'inconclusive', stop execution of any subsequent steps at this level, and return control to the parent step.")]
            BreakOnInconclusive = 8,
        }

        public override bool IsEnabled
        {
            get => Conditions.HasFlag(InternalBreakCondition.Inherit) == false;
            set
            {
                Conditions = Conditions.SetFlag(InternalBreakCondition.Inherit, !value);
            }
        }

        public override Values Value
        {
            get => (Values)(int)Conditions.SetFlag(InternalBreakCondition.Inherit, false);
            set => Conditions = (InternalBreakCondition)(int)value | ((!IsEnabled) ? InternalBreakCondition.Inherit : 0);
        }

        internal InternalBreakCondition Conditions;

        internal BreakConditions(InternalBreakCondition cond )
        {
            Conditions = cond;
        }

        public BreakConditions()
        {
            Conditions = InternalBreakCondition.Inherit;
        }

        public override bool Equals(object obj)
        {
            if (obj is BreakConditions other)
            {
                return Conditions.Equals(other.Conditions);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Conditions.GetHashCode();
        }
    }


    class BreakConditionsAnnotation : IOwnedAnnotation, IStringReadOnlyValueAnnotation
    {
        object source;
        public void Read(object source)
        {
            this.source = source;
        }

        public void Write(object source)
        {
        }

        public string Value
        {
            get
            {
                if (source is BreakConditions condition)
                {
                    if (condition.IsEnabled)
                    {
                        if (condition.Value != 0)
                            return condition.Value.ToString();
                    }
                    return InternalBreakCondition.Inherit.ToString();
                }
                return source.ToString();
            }
        }
    }
}