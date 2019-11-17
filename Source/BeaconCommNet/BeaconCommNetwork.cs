using CommNet;
using System;
using CommNetManagerAPI;
using ESLDCore;
using System.Linq;
using System.Collections.Generic;

namespace BeaconCommNet.CommNetLayer
{
    /// <summary>
    /// Extend the functionality of the KSP's CommNetwork (secondary model in the Model–view–controller sense)
    /// </summary>
    public class BeaconCommNetwork : CommNetwork
    {
        protected CommNetwork realNet;
        Dictionary<CommNode, double> distanceOffsets = new Dictionary<CommNode, double>();
        public BeaconCommNetwork()
        {
            this.realNet = CommNetManagerChecker.GetCommNetManagerNetwork();
        }
        /// <summary>
        /// Edit the connectivity between two potential nodes
        /// </summary>
        [CNMAttrAndOr(CNMAttrAndOr.options.OR)]
        [CNMAttrSequence(CNMAttrSequence.options.EARLY)]
        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            bool retValue = false;

            if (a.TryGetVessel(out Vessel aVessel) && b.TryGetVessel(out Vessel bVessel) && HasActiveBeacon(aVessel) && HasActiveBeacon(bVessel))
            {
                if (a.distanceOffset != 0 && !distanceOffsets.ContainsKey(a))
                    distanceOffsets.Add(a, a.distanceOffset);
                if (b.distanceOffset != 0 && !distanceOffsets.ContainsKey(b))
                    distanceOffsets.Add(b, b.distanceOffset);
                a.distanceOffset = double.NegativeInfinity;
                b.distanceOffset = double.NegativeInfinity;
                retValue = true;
            }

            return CommNetManagerChecker.CommNetManagerInstalled ? retValue : base.SetNodeConnection(a, b);
        }

        [CNMAttrSequence(CNMAttrSequence.options.POST)]
        [CNMAttrAndOr(CNMAttrAndOr.options.AND)]
        protected override bool TryConnect(CommNode a, CommNode b, double distance, bool aCanRelay, bool bCanRelay, bool bothRelay)
        {
            bool baseValue = true;
            if (!CommNetManagerChecker.CommNetManagerInstalled)
                baseValue = base.TryConnect(a, b, distance, aCanRelay, bCanRelay, bothRelay);

            if (a.TryGetVessel(out Vessel aVessel) && b.TryGetVessel(out Vessel bVessel) && HasActiveBeacon(aVessel) && HasActiveBeacon(bVessel))
            {
                if (distanceOffsets.TryGetValue(a, out a.distanceOffset))
                {
                    distanceOffsets.Remove(a);
                    if (a.distanceOffset == double.NegativeInfinity)
                    {
                        UnityEngine.Debug.LogWarning("ESLDBeaconCommNet: Somehow got a negInf. Setting to zero.");
                        a.distanceOffset = 0;
                    }
                }
                else
                    a.distanceOffset = 0;
                if (distanceOffsets.TryGetValue(b, out b.distanceOffset))
                {
                    distanceOffsets.Remove(b);
                    if (b.distanceOffset == double.NegativeInfinity)
                    {
                        UnityEngine.Debug.LogWarning("ESLDBeaconCommNet: Somehow got a negInf. Setting to zero.");
                        b.distanceOffset = 0;
                    }
                }
                else
                    b.distanceOffset = 0;
            }

            return baseValue;
        }

        public static bool HasActiveBeacon(Vessel vessel)
        {
            if (vessel.loaded)
            {
                return vessel.FindPartModulesImplementing<ESLDBeacon>().Any(beacon=>beacon.activated);
            }
            else
            {
                return vessel.protoVessel.protoPartSnapshots.Any(
                    protoPart => protoPart.modules.Any(protoModule =>
                        protoModule.moduleName == "ESLDBeacon" &&
                        protoModule.moduleValues.GetValue("activated") == "True"));
            }
        }
    }
}
