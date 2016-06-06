using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace ESLDCore
{
    public class ESLDHailer : PartModule
    {
        protected Rect BeaconWindow;
        protected Rect ConfirmWindow;
        public ESLDBeacon nearBeacon = null;
        public Vessel farBeaconVessel = null;
        public ESLDBeacon farBeacon = null;
        public string farBeaconModel = "";
        public Dictionary<ESLDBeacon, Vessel> farTargets = new Dictionary<ESLDBeacon, Vessel>();
        public Dictionary<ESLDBeacon, string> nearBeacons = new Dictionary<ESLDBeacon, string>();
        public double precision;
        public OrbitDriver oPredictDriver = null;
        public OrbitRenderer oPredict = null;
        public Transform oOrigin = null;
        public LineRenderer oDirection = null;
        public GameObject oDirObj = null;
        public double lastRemDist;
        public bool wasInMapView;
        public bool nbWasUserSelected = false;
        public bool isJumping = false;
        public bool isActive = false;
        public int currentBeaconIndex;
        public string currentBeaconDesc;
        public double HCUCost = 0;
        public HailerButton masterClass = null;
        private ESLDJumpResource primaryResource = null;
        bool drawConfirmOn = false;
        bool drawGUIOn = false;
        Logger log = new Logger("ESLDCore:ESLDHailer: ");

        // GUI Open?
        [KSPField(guiName = "GUIOpen", isPersistant = true, guiActive = false)]
        public bool guiopen;

        [KSPField(guiName = "Beacon", guiActive = false)]
        public string hasNearBeacon;

        [KSPField(guiName = "Beacon Distance", guiActive = false, guiUnits = "m")]
        public double nearBeaconDistance;

        [KSPField(guiName = "Drift", guiActive = false, guiUnits = "m/s")]
        public double nearBeaconRelVel;

        // Calculate Jump Velocity Offset
        public static Vector3d getJumpVelOffset(Vessel near, Vessel far, ESLDBeacon beacon)
        {
            Vector3d farRealVelocity = far.orbit.vel;
            CelestialBody farRefbody = far.mainBody;
            while (farRefbody.flightGlobalsIndex != 0) // Kerbol
            {
                farRealVelocity += farRefbody.orbit.vel;
                farRefbody = farRefbody.referenceBody;
            }
            Vector3d nearRealVelocity = near.orbit.vel;
            CelestialBody nearRefbody = near.mainBody;
            if (near.mainBody.flightGlobalsIndex == far.mainBody.flightGlobalsIndex)
            {
                farRealVelocity -= far.orbit.vel;
//              log.debug("In-system transfer, disregarding far beacon velocity.");
            }
            while (nearRefbody.flightGlobalsIndex != 0) // Kerbol
            {
                nearRealVelocity += nearRefbody.orbit.vel;
                nearRefbody = nearRefbody.referenceBody;
            }
            return nearRealVelocity - farRealVelocity;
        }

        // Mapview Utility
        private MapObject findVesselBody(Vessel craft)
        {
            int cInst = craft.mainBody.GetInstanceID();
//          foreach (MapObject mobj in MapView.FindObjectsOfType<MapObject>())
            foreach (MapObject mobj in MapView.MapCamera.targets)
            {
                if (mobj.celestialBody == null) continue;
                if (mobj.celestialBody.GetInstanceID() == cInst)
                {
                    return mobj;
                }
            }
            return null;
        }

        // Show exit orbital predictions
        private void showExitOrbit(Vessel near, Vessel far)
        {
            // Recenter map, save previous state.
            wasInMapView = MapView.MapIsEnabled;
            if (!MapView.MapIsEnabled) MapView.EnterMapView();
            log.debug("Finding target.");
            MapObject farTarget = findVesselBody(far);
            if (farTarget != null) MapView.MapCamera.SetTarget(farTarget);
            Vector3 mapCamPos = ScaledSpace.ScaledToLocalSpace(MapView.MapCamera.transform.position);
            Vector3 farTarPos = ScaledSpace.ScaledToLocalSpace(farTarget.transform.position);
            float dirScalar = Vector3.Distance(mapCamPos, farTarPos);
            log.debug("Initializing, camera distance is " + dirScalar);

            // Initialize projection stuff.
            log.debug("Beginning orbital projection.");
            Vector3d exitTraj = getJumpVelOffset(near, far, nearBeacon);
            oPredictDriver = new OrbitDriver();
            oPredictDriver.orbit = new Orbit();
            oPredictDriver.orbit.referenceBody = far.mainBody;
            oPredictDriver.referenceBody = far.mainBody;
            oPredictDriver.upperCamVsSmaRatio = 999999;  // Took forever to figure this out - this sets at what zoom level the orbit appears.  Was causing it not to appear at small bodies.
            oPredictDriver.lowerCamVsSmaRatio = 0.0001f;
            oPredictDriver.orbit.UpdateFromStateVectors(far.orbit.pos, exitTraj, far.mainBody, Planetarium.GetUniversalTime());
            oPredictDriver.orbit.Init();
            Vector3d p = oPredictDriver.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            Vector3d v = oPredictDriver.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime());
            oPredictDriver.orbit.h = Vector3d.Cross(p, v);
            oPredict = MapView.MapCamera.gameObject.AddComponent<OrbitRenderer>();
            oPredict.upperCamVsSmaRatio = 999999;
            oPredict.lowerCamVsSmaRatio = 0.0001f;
            oPredict.celestialBody = far.mainBody;
            oPredict.driver = oPredictDriver;
            oPredictDriver.Renderer = oPredict;
            
            // Splash some color on it.
            log.debug("Displaying orbital projection.");
            oPredict.driver.drawOrbit = true;
            oPredict.driver.orbitColor = Color.red;
            oPredict.orbitColor = Color.red;
            oPredict.drawIcons = OrbitRenderer.DrawIcons.OBJ_PE_AP;
            oPredict.drawMode = OrbitRenderer.DrawMode.REDRAW_AND_RECALCULATE;

            // Directional indicator.
            /*
            float baseWidth = 20.0f;
            double baseStart = 10;
            double baseEnd = 50;
            oDirObj = new GameObject("Indicator");
            oDirObj.layer = 10; // Map layer!
            oDirection = oDirObj.AddComponent<LineRenderer>();
            oDirection.useWorldSpace = false;
            oOrigin = null;
            foreach (Transform sstr in ScaledSpace.Instance.scaledSpaceTransforms)
            {
                if (sstr.name == far.mainBody.name)
                {
                    oOrigin = sstr;
                    log.debug("Found origin: " + sstr.name);
                    break;
                }
            }
            oDirection.transform.parent = oOrigin;
            oDirection.transform.position = ScaledSpace.LocalToScaledSpace(far.transform.position);
            oDirection.material = new Material(Shader.Find("Particles/Additive"));
            oDirection.SetColors(Color.clear, Color.red);
            if (dirScalar / 325000 > baseWidth) baseWidth = dirScalar / 325000f;
            oDirection.SetWidth(baseWidth, 0.01f);
            log.debug("Base Width set to " + baseWidth);
            oDirection.SetVertexCount(2);
            if (dirScalar / 650000 > baseStart) baseStart = dirScalar / 650000;
            if (dirScalar / 130000 > baseEnd) baseEnd = dirScalar / 130000;
            log.debug("Base Start set to " + baseStart);
            log.debug("Base End set to " + baseEnd);
            oDirection.SetPosition(0, Vector3d.zero + exitTraj.xzy.normalized * baseStart);
            oDirection.SetPosition(1, exitTraj.xzy.normalized * baseEnd);
            oDirection.enabled = true;
             */
        }


        // Update said predictions
        private void updateExitOrbit(Vessel near, Vessel far)
        {
            // Orbit prediction is broken for now FIXME!!
            /*
            float baseWidth = 20.0f;
            double baseStart = 10;
            double baseEnd = 50;
            Vector3 mapCamPos = ScaledSpace.ScaledToLocalSpace(MapView.MapCamera.transform.position);
            MapObject farTarget = MapView.MapCamera.target;
            Vector3 farTarPos = ScaledSpace.ScaledToLocalSpace(farTarget.transform.position);
            float dirScalar = Vector3.Distance(mapCamPos, farTarPos);
            Vector3d exitTraj = getJumpOffset(near, far, nearBeacon);
            oPredict.driver.referenceBody = far.mainBody;
            oPredict.driver.orbit.referenceBody = far.mainBody;
            oPredict.driver.pos = far.orbit.pos;
            oPredict.celestialBody = far.mainBody;
            oPredictDriver.orbit.UpdateFromStateVectors(far.orbit.pos, exitTraj, far.mainBody, Planetarium.GetUniversalTime());

            oDirection.transform.position = ScaledSpace.LocalToScaledSpace(far.transform.position);
            if (dirScalar / 325000 > baseWidth) baseWidth = dirScalar / 325000f;
            oDirection.SetWidth(baseWidth, 0.01f);
            if (dirScalar / 650000 > baseStart) baseStart = dirScalar / 650000;
            if (dirScalar / 130000 > baseEnd) baseEnd = dirScalar / 130000;
//          log.debug("Camera distance is " + dirScalar + " results: " + baseWidth + " " + baseStart + " " + baseEnd);
            oDirection.SetPosition(0, Vector3d.zero + exitTraj.xzy.normalized * baseStart);
            oDirection.SetPosition(1, exitTraj.xzy.normalized * baseEnd);
            oDirection.transform.eulerAngles = Vector3d.zero;
            */
        }

        // Back out of orbital predictions.
        private void hideExitOrbit(OrbitRenderer showOrbit)
        {
            // Orbit prediction is broken for now FIXME!!

            /*showOrbit.drawMode = OrbitRenderer.DrawMode.OFF;
            showOrbit.driver.drawOrbit = false;
            showOrbit.drawIcons = OrbitRenderer.DrawIcons.NONE;

            oDirection.enabled = false;

            foreach (MapObject mobj in MapView.MapCamera.targets)
            {
                if (mobj.vessel == null) continue;
                if (mobj.vessel.GetInstanceID() == FlightGlobals.ActiveVessel.GetInstanceID())
                {
                    MapView.MapCamera.SetTarget(mobj);
                }
            }*/
            if (MapView.MapIsEnabled && !wasInMapView) MapView.ExitMapView();
        }

        // Find parts that need a HCU to transfer.
        private Dictionary<Part, string> getHCUParts(Vessel craft)
        {
            HCUCost = 0;
            Array highEnergyResources = new string[14] { "karborundum", "uranium", "uraninite", "plutonium", "antimatter", "thorium", "nuclear", "exotic", "actinides", "chargedparticles", "fluorine", "lqdhe3", "tritium", "thf4"};
            Dictionary<Part, string> HCUParts = new Dictionary<Part, string>();
            foreach (Part vpart in vessel.Parts)
            {
                foreach (PartResource vres in vpart.Resources)
                {
                    foreach (string checkr in highEnergyResources)
                    if (vres.resourceName.ToLower().Contains(checkr) && vres.amount > 0)
                    {
                        if (HCUParts.Keys.Contains<Part>(vpart)) continue;
                        HCUCost += (vres.info.density * vres.amount * 0.1) / 0.0058;
                        HCUParts.Add(vpart, vres.resourceName);
                    }
                }
            }
            HCUCost += craft.GetCrewCount() * 0.9 / 1.13;
            HCUCost = Math.Round(HCUCost * 100) / 100;
            return HCUParts;
        }

        // Find loaded beacons.  Only in physics distance, since otherwise they're too far out.
        private ESLDBeacon ScanForNearBeacons()
        {
            nearBeacons.Clear();
            Fields["hasNearBeacon"].guiActive = true;
            ESLDBeacon nearBeaconCandidate = null;
            int candidateIndex = 0;
            string candidateDesc = "";
            foreach (ESLDBeacon selfBeacon in vessel.FindPartModulesImplementing<ESLDBeacon>())
            {
                if (selfBeacon.beaconModel == "IB1" && selfBeacon.activated)
                {
                    nearBeaconDistance = 0;
                    nearBeaconRelVel = 0;
                    Fields["nearBeaconDistance"].guiActive = false;
                    Fields["nearBeaconRelVel"].guiActive = false;
                    hasNearBeacon = "Onboard";
                    return selfBeacon;
                }
            }
            double closest = 3000;
            foreach (Vessel craft in FlightGlobals.Vessels)
            {
                if (!craft.loaded) continue;                // Eliminate far away craft.
                if (craft == vessel) continue;                      // Eliminate current craft.
                if (craft == FlightGlobals.ActiveVessel) continue;
                if (craft.FindPartModulesImplementing<ESLDBeacon>().Count == 0) continue; // Has beacon?
                foreach (ESLDBeacon craftbeacon in craft.FindPartModulesImplementing<ESLDBeacon>())
                {
                    if (!craftbeacon.activated) { continue; }   // Beacon active?
                    if (craftbeacon.beaconModel == "IB1") { continue; } // Jumpdrives can't do remote transfers.
                    string bIdentifier = craftbeacon.beaconModel + " (" + craft.vesselName + ")";
                    nearBeacons.Add(craftbeacon, bIdentifier);
                    int nbIndex = nearBeacons.Count - 1;
                    nearBeaconDistance = Math.Round(Vector3d.Distance(vessel.GetWorldPos3D(), craft.GetWorldPos3D()));
                    if (closest > nearBeaconDistance)
                    {
                        nearBeaconCandidate = craftbeacon;
                        candidateIndex = nbIndex;
                        candidateDesc = bIdentifier;
                        closest = nearBeaconDistance;
                    }
                }
            }
            if (nearBeacon != null && nearBeacon.vessel.loaded && nbWasUserSelected && nearBeacon.activated) // If we've already got one, just update the display.
            {
                nearBeaconDistance = Math.Round(Vector3d.Distance(vessel.GetWorldPos3D(), nearBeacon.vessel.GetWorldPos3D()));
                nearBeaconRelVel = Math.Round(Vector3d.Magnitude(vessel.obt_velocity - nearBeacon.vessel.obt_velocity) * 10) / 10;
                return nearBeacon;
            }
            if (nearBeacons.Count > 0) // If we hadn't selected one previously return the closest one.
            {
                nbWasUserSelected = false;
                Vessel craft = nearBeaconCandidate.vessel;
                Fields["nearBeaconDistance"].guiActive = true;
                nearBeaconDistance = Math.Round(Vector3d.Distance(vessel.GetWorldPos3D(), craft.GetWorldPos3D()));
                Fields["nearBeaconRelVel"].guiActive = true;
                nearBeaconRelVel = Math.Round(Vector3d.Magnitude(vessel.obt_velocity - craft.obt_velocity) * 10) / 10;
                hasNearBeacon = "Present";
                currentBeaconIndex = candidateIndex;
                currentBeaconDesc = candidateDesc;
                return nearBeaconCandidate;
            }
            hasNearBeacon = "Not Present";
            Fields["nearBeaconDistance"].guiActive = false;
            Fields["nearBeaconRelVel"].guiActive = false;
            nearBeacon = null;
            return null;
        }

        // Finds beacon targets.  Only starts polling when the GUI is open.
        public void listFarBeacons()
        {
            farTargets.Clear();
            foreach (Vessel craft in FlightGlobals.Vessels.FindAll(
                (Vessel c) =>
                c.loaded == false &&
                c != vessel &&
                c != FlightGlobals.ActiveVessel &&
                c.situation == Vessel.Situations.ORBITING))
            {
                foreach (ProtoPartSnapshot ppart in craft.protoVessel.protoPartSnapshots)
                {
                    foreach (ProtoPartModuleSnapshot pmod in ppart.modules.FindAll(
                        (ProtoPartModuleSnapshot p) => p.moduleName == "ESLDBeacon"))
                    {
                        if (pmod.moduleValues.GetValue("activated") == "True")
                        {
                            ESLDBeacon protoBeacon = new ESLDBeacon(pmod.moduleValues, ppart.partInfo.partConfig.GetNodes("MODULE")[ppart.modules.IndexOf(pmod)]);
                            protoBeacon.activated = true;
                            farTargets.Add(protoBeacon, craft);
                        }
                    }
                }
            }
        }

        public override void OnUpdate()
        {
            if (isActive)
            {
                var startState = hasNearBeacon;
                nearBeacon = ScanForNearBeacons();
                if (nearBeacon == null)
                {
                    if (startState != hasNearBeacon)
                    {
                        HailerGUIClose();
                    }
                    Events["HailerGUIClose"].active = false;
                    Events["HailerGUIOpen"].active = false;
                }
                else
                {
                    if (nearBeacon.jumpResources.Count > 0)
                        primaryResource = nearBeacon.jumpResources.ElementAt(0);
                    else
                        primaryResource = null;
                    Events["HailerGUIClose"].active = false;
                    Events["HailerGUIOpen"].active = true;
                }
            }
            
        }

        // Screen 1 of beacon interface, displays beacons and where they go along with some fuel calculations. 
        private void BeaconInterface(int GuiId)
        {
            if (!vessel.isActiveVessel) HailerGUIClose();
            GUIStyle buttonHasFuel = new GUIStyle(GUI.skin.button);
            buttonHasFuel.padding = new RectOffset(8, 8, 8, 8);
            buttonHasFuel.normal.textColor = buttonHasFuel.focused.textColor = Color.green;
            buttonHasFuel.hover.textColor = buttonHasFuel.active.textColor = Color.white;

            GUIStyle buttonNoFuel = new GUIStyle(GUI.skin.button);
            buttonNoFuel.padding = new RectOffset(8, 8, 8, 8);
            buttonNoFuel.normal.textColor = buttonNoFuel.focused.textColor = Color.red;
            buttonNoFuel.hover.textColor = buttonNoFuel.active.textColor = Color.yellow;

            GUIStyle buttonNoPath = new GUIStyle(GUI.skin.button);
            buttonNoPath.padding = new RectOffset(8, 8, 8, 8);
            buttonNoPath.normal.textColor = buttonNoFuel.focused.textColor = Color.gray;
            buttonNoPath.hover.textColor = buttonNoFuel.active.textColor = Color.gray;

            GUIStyle buttonNeutral = new GUIStyle(GUI.skin.button);
            buttonNeutral.padding = new RectOffset(8, 8, 8, 8);
            buttonNeutral.normal.textColor = buttonNoFuel.focused.textColor = Color.white;
            buttonNeutral.hover.textColor = buttonNoFuel.active.textColor = Color.white;

            GUILayout.BeginVertical(HighLogic.Skin.scrollView);
            if (farTargets.Count() < 1 || nearBeacon == null)
            {
                GUILayout.Label("No active beacons found.");
            }
            else
            {
                double tonnage = vessel.GetTotalMass();
                Vessel nbparent = nearBeacon.vessel;
                string nbModel = nearBeacon.beaconModel;
                nearBeacon.checkOwnTechBoxes();
                //double nbfuel = nearBeacon.fuelOnBoard;
                double driftpenalty = Math.Round(Math.Pow(Math.Floor(nearBeaconDistance / 200), 2) + Math.Floor(Math.Pow(nearBeaconRelVel, 1.5)) * nearBeacon.getCrewBonuses(nbparent,"Pilot",0.5,5));
                if (driftpenalty > 0) GUILayout.Label("+" + driftpenalty + "% due to Drift.");
                Dictionary<Part, string> HCUParts = getHCUParts(vessel);
                if (!nearBeacon.hasHCU)
                {
                    if (vessel.GetCrewCount() > 0 || HCUParts.Count > 0) GUILayout.Label("WARNING: This beacon has no active Heisenkerb Compensator.");
                    if (vessel.GetCrewCount() > 0) GUILayout.Label("Transfer will kill crew.");
                    if (HCUParts.Count > 0) GUILayout.Label("Some resources will destabilize.");
                }
                foreach (KeyValuePair<ESLDBeacon, Vessel> ftarg in farTargets)
                {
                    double tripdist = Vector3d.Distance(nbparent.GetWorldPos3D(), ftarg.Value.GetWorldPos3D());
                    double tripcost = nearBeacon.getTripBaseCost(tripdist, tonnage);
                    double sciBonus = nearBeacon.getCrewBonuses(nbparent, "Scientist", 0.5, 5);
                    if (nearBeacon.hasSCU)
                    {
                        if (driftpenalty == 0 && sciBonus >= 0.9)
                        {
                            tripcost *= 0.9;
                        }
                        if (sciBonus < 0.9 || (sciBonus < 1 && driftpenalty > 0))
                        {
                            tripcost *= sciBonus;
                        }

                    }
                    if (tripcost == 0) continue;
                    tripcost += tripcost * (driftpenalty * .01);
                    if (nearBeacon.hasAMU) tripcost += nearBeacon.getAMUCost(vessel, ftarg.Value, tonnage);
                    double adjHCUCost = HCUCost;
                    if (nearBeacon.beaconModel == "IB1") adjHCUCost = Math.Round((HCUCost - (tripcost * 0.02)) * 100) / 100;
                    if (nearBeacon.hasHCU) tripcost += adjHCUCost;
                    tripcost = Math.Round(tripcost * 100) / 100;
                    string targetSOI = ftarg.Value.mainBody.name;
                    double targetAlt = Math.Round(ftarg.Value.altitude / 1000);
                    GUIStyle fuelstate = buttonNoFuel;
                    string blockReason = "";
                    string blockRock = "";
                    bool affordable = true;
                    foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                    {
                        if (tripcost * Jresource.ratio > Jresource.fuelOnBoard)
                        {
                            affordable = false;
                            break;
                        }
                    }
                    if (affordable) // Show blocked status only for otherwise doable transfers.
                    {
                        fuelstate = buttonHasFuel;
                        KeyValuePair<string, CelestialBody> checkpath = masterClass.HasTransferPath(nbparent, ftarg.Value, nearBeacon.gLimitEff);
                        if (checkpath.Key != "OK")
                        {
                            fuelstate = buttonNoPath;
                            blockReason = checkpath.Key;
                            blockRock = checkpath.Value.name;
                        }
                    }
                    if (GUILayout.Button(ftarg.Key.beaconModel + " " + ftarg.Value.vesselName + "(" + targetSOI + ", " + targetAlt + "km) | " + tripcost, fuelstate))
                    {
                        if (fuelstate == buttonHasFuel)
                        {
                            farBeaconVessel = ftarg.Value;
                            farBeacon = ftarg.Key;
                            farBeaconModel = farBeacon.beaconModel;
                            drawConfirm();
                            if (!nearBeacon.hasAMU) showExitOrbit(vessel, farBeaconVessel);
                            //RenderingManager.AddToPostDrawQueue(4, new Callback(drawConfirm));
                            drawConfirmOn = true;
                            //RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
                            drawGUIOn = false;
                            Events["HailerGUIClose"].active = false;
                            Events["HailerGUIOpen"].active = true;
                        }
                        else
                        {
                            log.info("Current beacon has a g limit of " + nearBeacon.gLimitEff);
                            string messageToPost = "I can't tell why the jump won't work. Please report this error.";
                            if (!affordable)
                            {
                                foreach(ESLDJumpResource Jresource in nearBeacon.jumpResources)
                                {
                                    if (tripcost * Jresource.ratio <= Jresource.fuelOnBoard)
                                        continue;
                                    messageToPost = "Cannot Warp: Origin beacon has " + Jresource.fuelOnBoard + " of " + tripcost * Jresource.ratio + " " + Jresource.name + " required to warp.";
                                }
                            }
                            string thevar = (blockRock == "Mun" || blockRock == "Sun") ? "the " : string.Empty;
                            if (fuelstate == buttonNoPath && blockReason == "Gravity") messageToPost = "Cannot Warp: Path of transfer intersects a high-gravity area around " + thevar + blockRock + ".";
                            if (fuelstate == buttonNoPath && blockReason == "Proximity") messageToPost = "Cannot Warp: Path of transfer passes too close to " + thevar + blockRock + ".";
                            ScreenMessages.PostScreenMessage(messageToPost, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                            log.debug(messageToPost);
                        }
                    }
                }
            }
            if(nearBeacons.Count > 1)
            {
                GUILayout.Label("Current Beacon: " + currentBeaconDesc);
                if (currentBeaconIndex >= nearBeacons.Count) currentBeaconIndex = nearBeacons.Count - 1;
                int nextIndex = currentBeaconIndex + 1;
                if (nextIndex >= nearBeacons.Count) nextIndex = 0;
                if (GUILayout.Button("Next Beacon (" + (currentBeaconIndex + 1) + " of " + nearBeacons.Count + ")", buttonNeutral))
                {
                    nbWasUserSelected = true;
                    nearBeacon = nearBeacons.ElementAt(nextIndex).Key;
                    currentBeaconDesc = nearBeacons.ElementAt(nextIndex).Value;
                    currentBeaconIndex = nextIndex;
                }
            }
            if (GUILayout.Button("Close Beacon Interface", buttonNeutral))
            {
                HailerGUIClose();
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

        }

        private void ConfirmInterface(int GuiID) // Second beacon interface window.  
        {

            GUIStyle buttonNeutral = new GUIStyle(GUI.skin.button);
            buttonNeutral.padding = new RectOffset(8, 8, 8, 8);
            buttonNeutral.normal.textColor = buttonNeutral.focused.textColor = Color.white;
            buttonNeutral.hover.textColor = buttonNeutral.active.textColor = Color.white;

            GUIStyle labelHasFuel = new GUIStyle(GUI.skin.label);
            labelHasFuel.normal.textColor = Color.green;

            GUIStyle labelNoFuel = new GUIStyle(GUI.skin.label);
            labelNoFuel.normal.textColor = Color.red;
            GUILayout.BeginVertical(HighLogic.Skin.scrollView);
            if (nearBeacon != null)
            {
                double tripdist = Vector3d.Distance(nearBeacon.vessel.GetWorldPos3D(), farBeaconVessel.GetWorldPos3D());
                double tonnage = vessel.GetTotalMass();
                Vessel nbparent = nearBeacon.vessel;
                string nbModel = nearBeacon.beaconModel;
                double tripcost = nearBeacon.getTripBaseCost(tripdist, tonnage);
                double driftpenalty = Math.Pow(Math.Floor(nearBeaconDistance / 200), 2) + Math.Floor(Math.Pow(nearBeaconRelVel, 1.5));
                if (driftpenalty > 0) GUILayout.Label("+" + driftpenalty + "% due to Drift.");
                Dictionary<Part, string> HCUParts = getHCUParts(vessel);
                if (!nearBeacon.hasHCU)
                {
                    if (vessel.GetCrewCount() > 0 || HCUParts.Count > 0) GUILayout.Label("WARNING: This beacon has no active Heisenkerb Compensator.", labelNoFuel);
                    if (vessel.GetCrewCount() > 0) GUILayout.Label("Transfer will kill crew.", labelNoFuel);
                    if (HCUParts.Count > 0)
                    {
                        GUILayout.Label("These resources will destabilize in transit:", labelNoFuel);
                        foreach (KeyValuePair<Part, string> hcuresource in HCUParts)
                        {
                            GUILayout.Label(hcuresource.Key.name + " - " + hcuresource.Value, labelNoFuel);
                        }
                    }
                }
                GUILayout.Label("Confirm Warp:");
                var basecost = Math.Round(tripcost * 100) / 100;
                string tempLabel;
                tempLabel = "Base Cost: ";
                foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                {
                    tempLabel += basecost * Jresource.ratio + " " + Jresource.name;
                    if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                        tempLabel += ", ";
                }
                GUILayout.Label(tempLabel + ".");
                double sciBonus = nearBeacon.getCrewBonuses(nbparent, "Scientist", 0.5, 5);
                if (nearBeacon.hasSCU)
                {
                    if (driftpenalty == 0 && sciBonus >= 0.9)
                    {
                        GUILayout.Label("Superconducting Coil Array reduces cost by 10%.");
                        tripcost *= 0.9;
                    }
                    if (sciBonus < 0.9 || (sciBonus < 1 && driftpenalty > 0))
                    {
                        double dispBonus = Math.Round((1-sciBonus) * 100);
                        GUILayout.Label("Scientists on beacon vessel reduce cost by " + dispBonus + "%.");
                        tripcost *= sciBonus;
                    }
                    
                }
                if (driftpenalty > 0) GUILayout.Label("Relative speed and distance to beacon adds " + driftpenalty + "%.");
                tripcost += tripcost * (driftpenalty * .01);
                tripcost = Math.Round(tripcost * 100) / 100;
                if (nearBeacon.hasAMU)
                {
                    double AMUCost = nearBeacon.getAMUCost(vessel, farBeaconVessel, tonnage);
                    tempLabel = "AMU Compensation adds ";
                    foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                    {
                        tempLabel += AMUCost * Jresource.ratio + " " + Jresource.name;
                        if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    GUILayout.Label(tempLabel + ".");
                    tripcost += AMUCost;
                }
                if (nearBeacon.hasHCU)
                {
                    double adjHCUCost = HCUCost;
                    if (nearBeacon.beaconModel == "IB1") adjHCUCost = Math.Round((HCUCost - (tripcost * 0.02)) * 100) / 100;
                    tempLabel = "HCU Shielding adds ";
                    foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                    {
                        tempLabel += adjHCUCost * Jresource.ratio + " " + Jresource.name;
                        if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    GUILayout.Label(tempLabel + ".");
                    tripcost += adjHCUCost;
                }
                tempLabel = "Total Cost: ";
                foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                {
                    tempLabel += tripcost * Jresource.ratio + " " + Jresource.name;
                    if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                        tempLabel += ", ";
                }
                GUILayout.Label(tempLabel + ".");
                GUILayout.Label("Destination: " + farBeaconVessel.mainBody.name + " at " + Math.Round(farBeaconVessel.altitude / 1000) + "km.");
                precision = farBeacon.getTripSpread(tripdist);
                GUILayout.Label("Transfer will emerge within " + precision + "m of destination beacon.");
                if (farBeaconVessel.altitude - precision <= farBeaconVessel.mainBody.Radius * 0.1f || farBeaconVessel.altitude - precision <= farBeaconVessel.mainBody.atmosphereDepth)
                {
                    GUILayout.Label("Arrival area is very close to " + farBeaconVessel.mainBody.name + ".", labelNoFuel);
                }
                if (!nearBeacon.hasAMU)
                {
                    Vector3d transferVelOffset = getJumpVelOffset(vessel, farBeaconVessel, nearBeacon) - farBeaconVessel.orbit.vel;
                    GUILayout.Label("Velocity relative to exit beacon will be " + Math.Round(transferVelOffset.magnitude) + "m/s.");
                }
                double retTripCost = 0;
                bool fuelcheck = false;
                bool affordReturn = true;
                ESLDBeacon cheapFarBeacon = new ESLDBeacon();
                List<ESLDBeacon> beaconsOnFarBeaconVessel = farTargets.Where(p => p.Value == farBeaconVessel).Select(p => p.Key).ToList();
                foreach (ESLDBeacon tempFarBeacon in beaconsOnFarBeaconVessel)
                {
                    if (retTripCost == 0)
                    {
                        retTripCost = tempFarBeacon.getTripBaseCost(tripdist, tonnage);
                        cheapFarBeacon = tempFarBeacon;
                    }
                    else
                    {
                        double tempCost = tempFarBeacon.getTripBaseCost(tripdist, tonnage);
                        if (tempCost < retTripCost)
                        {
                            retTripCost = tempCost;
                            cheapFarBeacon = tempFarBeacon;
                        }
                    }
                }
                fuelcheck = cheapFarBeacon.jumpResources.All((ESLDJumpResource jr) => jr.fuelCheck);
                string fuelmessage = "Destination beacon's fuel could not be checked.";
                if (fuelcheck)
                {
                    fuelmessage = "Destination beacon has ";
                    foreach (ESLDJumpResource Jresource in cheapFarBeacon.jumpResources)
                    {
                        fuelmessage += Jresource.fuelOnBoard + " " + Jresource.name;
                        if (cheapFarBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            fuelmessage += ", ";
                        if (retTripCost * Jresource.ratio > Jresource.fuelOnBoard)
                            affordReturn = false;
                    }
                }
                GUILayout.Label(fuelmessage+".");
                retTripCost = Math.Round(retTripCost * 100) / 100;
                if (fuelcheck && affordReturn)
                {
                    tempLabel = "Destination beacon can make return trip using ";
                    foreach (ESLDJumpResource Jresource in cheapFarBeacon.jumpResources)
                    {
                        tempLabel += retTripCost * Jresource.ratio + " " + Jresource.name;
                        if (cheapFarBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    tempLabel += " (base cost).";
                    GUILayout.Label(tempLabel, labelHasFuel);
                }
                else if (fuelcheck)
                {
                    tempLabel = "Destination beacon would need ";
                    foreach (ESLDJumpResource Jresource in cheapFarBeacon.jumpResources)
                    {
                        tempLabel += retTripCost * Jresource.ratio + " " + Jresource.name;
                        if (cheapFarBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    tempLabel += " (base cost) for return trip using active beacons.";
                    GUILayout.Label(tempLabel, labelNoFuel);
                }
                if (oPredict != null) updateExitOrbit(vessel, farBeaconVessel);
                if (GUILayout.Button("Confirm and Warp", buttonNeutral))
                {
                    //RenderingManager.RemoveFromPostDrawQueue(4, new Callback(drawConfirm));
                    drawConfirmOn = false;
                    HailerGUIClose();
                    if (oPredict != null) hideExitOrbit(oPredict);
                    // Check transfer path one last time.
                    KeyValuePair<string, CelestialBody> checkpath = masterClass.HasTransferPath(nbparent, farBeaconVessel, nearBeacon.gLimitEff); // One more check for a clear path in case they left the window open too long.
                    bool finalPathCheck = false;
                    if (checkpath.Key == "OK") finalPathCheck = true;
                    // Check fuel one last time.
                    fuelcheck = nearBeacon.jumpResources.All((ESLDJumpResource Jresource) =>
                        nearBeacon.requireResource(nbparent, Jresource.name, tripcost * Jresource.ratio, false));
                    if (fuelcheck && finalPathCheck) // Fuel is valid for and path is clear.
                    {
                        // Pay fuel
                        foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                            nearBeacon.requireResource(nbparent, Jresource.name, tripcost * Jresource.ratio, true);
                        // Buckle up!
                        if (!nearBeacon.hasHCU) // Penalize for HCU not being present/online.
                        {
                            List<ProtoCrewMember> crewList = new List<ProtoCrewMember>();
                            List<Part> crewParts = new List<Part>();
                            foreach (Part vpart in vessel.Parts)
                            {
                                foreach (ProtoCrewMember crew in vpart.protoModuleCrew)
                                {
                                    crewParts.Add(vpart);
                                    crewList.Add(crew);
                                }
                            }
                            for (int i = crewList.Count - 1; i >= 0; i--)
                            {
                                if (i >= crewList.Count)
                                {
                                    if (crewList.Count == 0) break;
                                    i = crewList.Count - 1;
                                }
                                ProtoCrewMember tempCrew = crewList[i];
                                crewList.RemoveAt(i);
                                ScreenMessages.PostScreenMessage(tempCrew.name + " was killed in transit!", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                                crewParts[i].RemoveCrewmember(tempCrew);
                                crewParts.RemoveAt(i);
                                tempCrew.Die();
                            }
                            HCUParts = getHCUParts(vessel);
                            List<Part> HCUList = new List<Part>();
                            foreach (KeyValuePair<Part, string> HCUPart in HCUParts)
                            {
                                HCUList.Add(HCUPart.Key);
                            }
                            HCUParts.Clear();
                            for (int i = HCUList.Count - 1; i >= 0; i--)
                            {
                                if (i >= HCUList.Count)
                                {
                                    if (HCUList.Count == 0) break;
                                    i = HCUList.Count - 1;
                                }
                                Part tempPart = HCUList[i];
                                HCUList.RemoveAt(i);
                                tempPart.explosionPotential = 1;
                                tempPart.explode();
                                tempPart.Die();
                            }
                        }
                        masterClass.dazzle();
                        Vector3d transferVelOffset = getJumpVelOffset(vessel, farBeaconVessel, nearBeacon);
                        if (nearBeacon.hasAMU) transferVelOffset = farBeaconVessel.orbit.vel;
                        Vector3d spread = ((UnityEngine.Random.onUnitSphere + UnityEngine.Random.insideUnitSphere) / 2) * (float)precision;

                        OrbitDriver vesOrb = vessel.orbitDriver;
                        Orbit orbit = vesOrb.orbit;
                        Orbit newOrbit = new Orbit(orbit.inclination, orbit.eccentricity, orbit.semiMajorAxis, orbit.LAN, orbit.argumentOfPeriapsis, orbit.meanAnomalyAtEpoch, orbit.epoch, orbit.referenceBody);
                        newOrbit.UpdateFromStateVectors (farBeaconVessel.orbit.pos + spread, transferVelOffset, farBeaconVessel.mainBody, Planetarium.GetUniversalTime ());
                        vessel.Landed = false;
                        vessel.Splashed = false;
                        vessel.landedAt = string.Empty;

                        OrbitPhysicsManager.HoldVesselUnpack(60);

                        List<Vessel> allVessels = FlightGlobals.Vessels;
                        foreach (Vessel v in allVessels.AsEnumerable()) {
                            if (v.packed == false)
                                v.GoOnRails ();
                        }

                        CelestialBody oldBody = vessel.orbitDriver.orbit.referenceBody;

                        orbit.inclination = newOrbit.inclination;
                        orbit.eccentricity = newOrbit.eccentricity;
                        orbit.semiMajorAxis = newOrbit.semiMajorAxis;
                        orbit.LAN = newOrbit.LAN;
                        orbit.argumentOfPeriapsis = newOrbit.argumentOfPeriapsis;
                        orbit.meanAnomalyAtEpoch = newOrbit.meanAnomalyAtEpoch;
                        orbit.epoch = newOrbit.epoch;
                        orbit.referenceBody = newOrbit.referenceBody;
                        orbit.Init();
                        orbit.UpdateFromUT(Planetarium.GetUniversalTime());
                        if (orbit.referenceBody != newOrbit.referenceBody)
                            vesOrb.OnReferenceBodyChange?.Invoke (newOrbit.referenceBody);

                        vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
                        vessel.orbitDriver.vel = vessel.orbit.vel;

                        if (vessel.orbitDriver.orbit.referenceBody != oldBody)
                            GameEvents.onVesselSOIChanged.Fire (new GameEvents.HostedFromToAction<Vessel, CelestialBody> (vessel, oldBody, vessel.orbitDriver.orbit.referenceBody));
//                        nbparent.GoOnRails();
//                        vessel.GoOnRails();
//                        vessel.situation = Vessel.Situations.ORBITING;
//                        vessel.orbit.UpdateFromStateVectors(farBeaconVessel.orbit.pos + spread, transferVelOffset, farBeaconVessel.mainBody, Planetarium.GetUniversalTime());
//                        vessel.orbit.Init();
//                        vessel.orbit.UpdateFromUT(Planetarium.GetUniversalTime());
//                        vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
//                        vessel.orbitDriver.vel = vessel.orbit.vel;
                    }
                    else if (!fuelcheck && finalPathCheck)
                    {
                        ScreenMessages.PostScreenMessage("Jump failed!  Origin beacon did not have enough fuel to execute transfer.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    }
                    else if (!finalPathCheck)
                    {
                        ScreenMessages.PostScreenMessage("Jump Failed!  Transfer path has become obstructed.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    }
                    else
                    {
                        ScreenMessages.PostScreenMessage("Jump Failed!  Origin beacon cannot complete transfer.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }
            else
            {
                if (oPredict != null) hideExitOrbit(oPredict);
            }
            if (!vessel.isActiveVessel)
            {
                //RenderingManager.RemoveFromPostDrawQueue(4, new Callback(drawConfirm));
                drawConfirmOn = false;
                if (oPredict != null) hideExitOrbit(oPredict);
            }
            if (GUILayout.Button("Back", buttonNeutral))
            {
                //RenderingManager.RemoveFromPostDrawQueue(4, new Callback(drawConfirm));
                drawConfirmOn = false;
                HailerGUIOpen();
                if (oPredict != null) hideExitOrbit(oPredict);
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void OnGUI()
        {
            if (drawGUIOn)
                drawGUI();
            if (drawConfirmOn)
                drawConfirm();
            if (!drawGUIOn && !drawConfirmOn)
                farTargets.Clear();
        }

        private void drawGUI()
        {
            if (farTargets.Count() == 0)
                listFarBeacons();
            BeaconWindow = GUILayout.Window(1, BeaconWindow, BeaconInterface, "Warp Information", GUILayout.MinWidth(400), GUILayout.MinHeight(200));
            if ((BeaconWindow.x == 0) && (BeaconWindow.y == 0))
            {
                BeaconWindow = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            }
        }

        private void drawConfirm()
        {
            ConfirmWindow = GUILayout.Window(2, ConfirmWindow, ConfirmInterface, "Pre-Warp Confirmation", GUILayout.MinWidth(400), GUILayout.MinHeight(200));
            if ((ConfirmWindow.x == 0) && (ConfirmWindow.y == 0))
            {
                ConfirmWindow = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            }
        }

        [KSPEvent(name = "HailerActivate", active = true, guiActive = true, guiName = "Initialize Hailer")]
        public void HailerActivate()
        {
//          part.force_activate();
            isActive = true;
            Events["HailerActivate"].active = false;
            Events["HailerDeactivate"].active = true;
            ScanForNearBeacons();
        }
        [KSPEvent(name = "HailerGUIOpen", active = false, guiActive = true, guiName = "Beacon Interface")]
        public void HailerGUIOpen()
        {
            //RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
            drawGUIOn = true;
            Events["HailerGUIOpen"].active = false;
            Events["HailerGUIClose"].active = true;
            guiopen = true;
        }
        [KSPEvent(name = "HailerGUIClose", active = false, guiActive = true, guiName = "Close Interface")]
        public void HailerGUIClose()
        {
            //RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
            drawGUIOn = false;
            Events["HailerGUIClose"].active = false;
            Events["HailerGUIOpen"].active = true;
            guiopen = false;
        }
        [KSPEvent(name = "HailerDeactivate", active = false, guiActive = true, guiName = "Shut Down Hailer")]
        public void HailerDeactivate()
        {
//          part.deactivate();
            isActive = false;
            if (oPredict != null) hideExitOrbit(oPredict);
            HailerGUIClose();
            //RenderingManager.RemoveFromPostDrawQueue(4, new Callback(drawConfirm));
            drawConfirmOn = false;
            Events["HailerDeactivate"].active = false;
            Events["HailerActivate"].active = true;
            Events["HailerGUIOpen"].active = false;
            Events["HailerGUIClose"].active = false;
            Fields["hasNearBeacon"].guiActive = false;
            Fields["nearBeaconDistance"].guiActive = false;
            Fields["nearBeaconRelVel"].guiActive = false;
        }
    }
}
