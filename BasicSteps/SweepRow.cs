using System.Collections.Generic;

namespace OpenTap.Plugins.BasicSteps
{
    public class SweepRow
    {
        /// <summary> Gets or sets if the row is enabled.</summary>
        public bool Enabled { get; set; }
        
        /// <summary>
        /// The sweep step owning this row. This is needed to figure out which properties the object has.
        /// </summary>
        public SweepLoop2 Loop { get; set; }
        
        /// <summary> Dictionary for storing dynamic property values. </summary>
        public Dictionary<string, object> Values = new Dictionary<string, object>();
    }
}