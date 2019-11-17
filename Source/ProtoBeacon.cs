using System;
using System.Collections.Generic;

namespace ESLDCore
{
    public class ProtoBeacon : IBeacon, IConfigNode
    {
        [KSPField(isPersistant = true)]
        public bool moduleIsEnabled = true;

        // Activated state.
        [KSPField(isPersistant = true)]
        public bool activated = false;

        // Beacon model (from part config).
        // Can be any string, but overrides cost parameters when equal to one of the original 4 models.
        [KSPField(isPersistant = true)]
        public string beaconModel = string.Empty;

        // Cost equation:
        // M=tonnage, D=tripdist, f=massFctr, e=massExp, c=coef, p=distPow, b=baseMult, l=yardstick, d=2 if D>distpenalty
        // COST = M*b + M^(1+f*M^e+d) * (D/sqrt(l))^(1/2^p) * c

        // Cost parameter (from part config). Coefficient for total cost. (Does not affect base cost)
        [KSPField]
        public float coef = 0.01f;

        // Cost parameter (from part config). Coefficient for mass exponent.
        [KSPField]
        public float massFctr = 0.0002f;

        // Cost parameter (from part config). Second exponent of mass. (M^(1+f*M^e))
        [KSPField]
        public float massExp = 1f;

        // Cost parameter (from part config). Root order of distance cost. (tripdist^(1/(2^distPow)))
        [KSPField]
        public float distPow = 1;

        // Cost parameter (from part config). Coefficient for base cost from mass.
        [KSPField]
        public float baseMult = 0.25f;

        // Cost parameter (from part config). Distance beyond which cost becomes prohibitive. (Zero for infinite)
        [KSPField]
        public float distPenalty = 0;

        // Cost parameter (from part config). Cost added simply for using the beacon.
        [KSPField]
        public float baseCost = 0;

        // Cost multiplier. Applies to entire base jump cost.
        [KSPField]
        public float multiplier = 1;

        [KSPField]
        public float jumpPrecision = 10;

        [KSPField]
        public bool canJumpSelf = false;

        [KSPField]
        public bool builtInHCU = false;

        [KSPField]
        public bool jumpTargetable = true;

        [KSPField(isPersistant = true)]
        public bool hasAMU = false;
        [KSPField(isPersistant = true)]
        public bool hasHCU = false;
        [KSPField(isPersistant = true)]
        public bool hasGMU = false;
        [KSPField(isPersistant = true)]
        public bool hasSCU = false;
        [KSPField(isPersistant = true)]
        public double massBonus = 1;
        // Per-beacon G limit.
        [KSPField(isPersistant = true)]
        public float gLimitEff = 0.5f;

        public List<ESLDJumpResource> JumpResources { get => jumpResources; }
        protected List<ESLDJumpResource> jumpResources = new List<ESLDJumpResource>();

        public bool UnsafeTransfer { get => !hasHCU; }
        public bool CarriesVelocity { get => !hasAMU; }
        public string Description { get => beaconModel; }
        public Vessel Vessel { get; set; }
        public float PathGLimit { get => gLimitEff; }

        public ProtoBeacon(ConfigNode protoPartModuleNode, ConfigNode protoPartConfigModuleNode = null)
        {
            if (protoPartConfigModuleNode != null)
                Load(protoPartConfigModuleNode);
            Load(protoPartModuleNode);
        }

        public void Load(ConfigNode node)
        {
            node.TryGetValue("activated", ref activated);
            node.TryGetValue("beaconModel", ref beaconModel);
            node.TryGetValue("coef", ref coef);
            node.TryGetValue("massFctr", ref massFctr);
            node.TryGetValue("massExp", ref massExp);
            node.TryGetValue("distPow", ref distPow);
            node.TryGetValue("baseMult", ref baseMult);
            node.TryGetValue("distPenalty", ref distPenalty);
            node.TryGetValue("baseCost", ref baseCost);
            node.TryGetValue("jumpPrecision", ref jumpPrecision);
            node.TryGetValue("canJumpSelf", ref canJumpSelf);
            node.TryGetValue("builtInHCU", ref builtInHCU);
            node.TryGetValue("jumpTargetable", ref jumpTargetable);
            node.TryGetValue("hasAMU", ref hasAMU);
            node.TryGetValue("hasHCU", ref hasHCU);
            node.TryGetValue("hasGMU", ref hasGMU);
            node.TryGetValue("hasSCU", ref hasSCU);
            node.TryGetValue("massBonus", ref massBonus);
            node.TryGetValue("moduleIsEnabled", ref moduleIsEnabled);
            node.TryGetValue("gLimitEff", ref gLimitEff);
            SerializationHelper.LoadObjects(jumpResources, ESLDBeacon.RnodeName, node, jr => jr.name);
        }

        public void Save(ConfigNode node)
        {
            node.SetValue("activated", activated, true);
            node.SetValue("beaconModel", beaconModel, true);
            node.SetValue("hasAMU", hasAMU, true);
            node.SetValue("hasHCU", hasHCU, true);
            node.SetValue("hasGMU", hasGMU, true);
            node.SetValue("hasSCU", hasSCU, true);
            node.SetValue("massBonus", massBonus, true);
            if (!moduleIsEnabled)
                node.SetValue("moduleIsEnabled", moduleIsEnabled, true);
            node.SetValue("gLimitEff", gLimitEff, true);
            for (int i = 0; i < jumpResources.Count; i++)
            {
                ConfigNode resNode = node.AddNode(ESLDBeacon.RnodeName);
                jumpResources[i].Save(resNode);
            }
        }

        // Calculate base cost in units of Karborundum before penalties for a transfer.
        public float GetTripBaseCost(float tripdist, float tonnage)
            => ESLDBeacon.GetTripBaseCost(tripdist, tonnage, distPenalty, distPow, baseMult, massFctr, massExp, coef, baseCost, beaconModel, multiplier);

        public float GetTripFinalCost(float baseCost, Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null)
            => ESLDBeacon.GetTripFinalCost(baseCost, Vessel, target, destination, tonnage, hasSCU, hasAMU, hasHCU, HCUParts, multiplier);

        public List<string> GetCostModifiers(Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null)
            => ESLDBeacon.GetCostModifiers(Vessel, target, destination, tonnage, hasSCU, hasAMU, hasHCU, jumpResources, HCUParts, multiplier);

        public float GetCrewBonuses(string neededTrait, float maxBenefit, int countCap)
            => ESLDBeacon.GetCrewBonuses(Vessel, neededTrait, maxBenefit, countCap);

        // Calculate how far away from a beacon the ship will arrive.
        public float GetTripSpread(float tripdist)
            => ESLDBeacon.GetTripSpread(tripdist, jumpPrecision);

        public bool RequireResource(string res, double resAmount, bool consumeResource = false)
        => RequireResource(PartResourceLibrary.Instance.GetDefinition(res).id, resAmount, consumeResource);

        public bool RequireResource(int resID, double resAmount, bool consumeResource = false)
        {
            int resource = jumpResources.FindIndex(res => res.resID == resID);
            if (resource < 0)
                return false;
            if (!consumeResource && jumpResources[resource].fuelCheck && jumpResources[resource].fuelOnBoard >= resAmount)
                return true;
            return false;
        }

        public void Warp(Vessel target, Vessel destination, float precision, List<Part> unsafeParts = null)
        {

        }
    }

    public interface IBeacon
    {
        List<ESLDJumpResource> JumpResources { get; }
        bool UnsafeTransfer { get; }
        bool CarriesVelocity { get; }
        string Description { get; }
        float PathGLimit { get; }
        Vessel Vessel { get; }

        float GetTripBaseCost(float tripdist, float tonnage);
        float GetTripSpread(float tripdist);
        float GetTripFinalCost(float baseCost, Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null);
        float GetCrewBonuses(string neededTrait, float maxBenefit, int countCap);
        bool RequireResource(string res, double resAmount, bool consumeResource = false);
        bool RequireResource(int resID, double resAmount, bool consumeResource = false);
        List<string> GetCostModifiers(Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null);
        void Warp(Vessel target, Vessel destination, float precision, List<Part> unsafeParts = null);
        //void Warp(Vessel target, CelestialBody body, UnityEngine.Vector3 destination, UnityEngine.Vector3 velocity, List<Part> unsafeParts = null);
    }
}
