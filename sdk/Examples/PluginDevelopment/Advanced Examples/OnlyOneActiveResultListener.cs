using System;
using System.Collections.Generic;
using System.IO;
using OpenTap;
namespace PluginDevelopment.Advanced_Examples
{
    /// <summary>
    /// This result listener only allows one to be connected to the same URL for a given test plan run otherwise an error will occur. 
    /// </summary>
    class OnlyOneActiveResultListener : ResultListener
    {
        /// <summary> Server address. </summary>
        public string Url { get; set; }
            
        // static so it is shared between all cloud listeners.
        static Dictionary<string, OnlyOneActiveResultListener> activeListeners = new Dictionary<string, OnlyOneActiveResultListener>();

        string sessionIdentifier;
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            sessionIdentifier = planRun.Hash + Url; // planRun hash + URL is used to discriminate.
            lock (activeListeners)
            {
                if(activeListeners.TryGetValue(sessionIdentifier, out OnlyOneActiveResultListener _))
                    throw new Exception("Another Cloud Listener already in use at the same address.");
                activeListeners.Add(sessionIdentifier, this);
            }
            base.OnTestPlanRunStart(planRun);
        }

        /// <summary> Cleanup </summary>
        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            base.OnTestPlanRunCompleted(planRun, logStream);
          
            lock (activeListeners)
                activeListeners.Remove(sessionIdentifier);
        }

    }
}