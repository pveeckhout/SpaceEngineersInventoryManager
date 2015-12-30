using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersScripts
{
    public class SolarManager
    {
        #region programming environment essential inits, DO NOT COPY TO GAME
        IMyGridTerminalSystem GridTerminalSystem;
        #endregion

        #region Script Configuration
        const string SOLARFARM_TAG = "[SOL]";
        const string SOLARFARM_MAINROTOR_NAME = "Main Rotor";
        const string SOLARFARM_MIRRORROTOR_NAME = "Mirror Rotor";
        const string SOLARFARM_OUTPUTPANEL_DEBUG_NAME = SOLARFARM_TAG + " Debug Panel";
        const string SOLARFARM_OUTPUTPANEL_STATUS_NAME = SOLARFARM_TAG + " Status Panel";
        const float ROTOR_SPEED = 1.5f;
        const float MAINROTOR_MINPOSITION = 0f;
        const float MAINROTOR_MAXPOSITION = 180f;
        const float POWER_THRESHOLD_BATTERY_CUTOF = 120; // in kW
        const string MANAGED_BATTERY_NAME = SOLARFARM_TAG + " Managed Battery";
        #endregion

        #region Script Vars
        private static OutputAgent SolarManagerOutputAgent { get; set; }
        private bool NightMode { get; set; }
        #endregion

        public void Main()
        {
            //init the outputListener
            SolarManagerOutputAgent = new OutputAgent(GridTerminalSystem);

            //get all block groups
            var blockGroups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(blockGroups);

            Output(OutputAgent.OutputLevel[0], "#blockGroups found: " + blockGroups.Count);

            // filter out the solar farms
            for (int index = blockGroups.Count - 1; index >= 0; index--)
            {
                if (!blockGroups[index].Name.ToLower().Contains(SOLARFARM_TAG.ToLower()))
                {
                    blockGroups.RemoveAt(index);
                }
            }

            Output(OutputAgent.OutputLevel[1], "#Solar Farms: " + blockGroups.Count);

            var solarfarms = new List<SolarFarm>();

            //determine for each solarfarm what needs to happen.
            foreach (var solarfarmBlockGroup in blockGroups)
            {
                var solarfarm = new SolarFarm(solarfarmBlockGroup.Name, solarfarmBlockGroup.Blocks);
                solarfarms.Add(solarfarm);

                if (!NightMode)
                {
                    solarfarm.RotorPair.SetRotorPairPosition(solarfarm.RotorPair.RotorPositions()[0] + 3);
                    if (solarfarm.RotorPair.RotorPositions()[0] >= MAINROTOR_MAXPOSITION)
                    {
                        NightMode = true;
                    }
                }
                else
                {
                    solarfarm.RotorPair.SetRotorPairPosition(solarfarm.RotorPair.RotorPositions()[0] - 3);
                    if (solarfarm.RotorPair.RotorPositions()[0] <= MAINROTOR_MINPOSITION)
                    {
                        NightMode = false;
                    }
                }

            }

            //build list of managed batteries
            var batteries = new List<IMyTerminalBlock>();

            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);

            for (int i = batteries.Count - 1; i >= 0; i--)
            {
                if (!batteries[i].CustomName.ToLower().Contains(MANAGED_BATTERY_NAME.ToLower()))
                {
                    batteries.RemoveAt(i);
                }
            }

            if (batteries.Count > 0)
            {
                //check if batteries need to be charging or discharging
                var SumOfPower = 0f;
                foreach (var farm in solarfarms)
                {
                    SumOfPower += farm.GetTotalMaxCurrentSolarPowerOutput();
                }

                if (SumOfPower >= POWER_THRESHOLD_BATTERY_CUTOF)
                {
                    ChargeBatteries(batteries);
                }
                else
                {
                    DischargeBatteries(batteries);
                }
            }
        }

        private void DischargeBatteries(List<IMyTerminalBlock> batteries)
        {
            foreach (var battery in batteries)
            {
                //turn on
                battery.GetActionWithName("OnOff_On").Apply(battery);

                //check if recharging or discharging
                bool recharge = true;

                string batteryInfo = battery.DetailedInfo;
                recharge = batteryInfo.Contains("recharged");

                //if it is, switch it out of recharge mode.
                if (recharge)
                {
                    battery.GetActionWithName("Recharge").Apply(battery);
                }

                //discharge
                battery.GetActionWithName("Discharge").Apply(battery);
            }
        }

        private void ChargeBatteries(List<IMyTerminalBlock> batteries)
        {
            foreach (var battery in batteries)
            {
                //turn on
                battery.GetActionWithName("OnOff_On").Apply(battery);

                //check if recharging or discharging
                bool recharge = true;

                string batteryInfo = battery.DetailedInfo;
                recharge = batteryInfo.Contains("recharged");

                //if it isnt, switch it out of discharge mode.
                if (recharge)
                {
                    battery.GetActionWithName("Discharge").Apply(battery);
                }

                //recharge
                battery.GetActionWithName("Recharge").Apply(battery);
            }
        }

        public static void Output(string outputLevel, string message)
        {
            SolarManagerOutputAgent.Output(outputLevel, message);
        }

        public class RotorPair
        {
            public IMyMotorAdvancedStator MainRotor { get; private set; }
            public IMyMotorAdvancedStator MirrorRotor { get; private set; }
            public bool InverseMirror { get; private set; }
            public bool ForceAutoTorque { get; private set; }

            public RotorPair(IMyMotorAdvancedStator mainRotor, IMyMotorAdvancedStator mirrorRotor, bool inverseMirror = true, bool forceAutoTorque = true)
            {
                if (mainRotor == null)
                {
                    Output(OutputAgent.OutputLevel[0], "ERROR: The main rotor cannot be null");
                    Output(OutputAgent.OutputLevel[1], "ERROR: script stopped");
                    throw new ArgumentNullException("The main rotor cannot be null");
                }

                if (mirrorRotor == null)
                {
                    Output(OutputAgent.OutputLevel[0], "INFO: The mirror rotor is null");
                }

                MainRotor = mainRotor;
                MirrorRotor = mirrorRotor;
                InverseMirror = inverseMirror;
                ForceAutoTorque = forceAutoTorque;
            }

            public void SetRotorPairPosition(float destinationPosition)
            {
                Output(OutputAgent.OutputLevel[0], "INFO: Setting the rotor Positions");

                if (ForceAutoTorque)
                    ForceRotorsTorque();

                var currentPositions = RotorPositions();

                Output(OutputAgent.OutputLevel[0], "INFO: Main Rotor - Current rotor position: " + currentPositions[0]);
                Output(OutputAgent.OutputLevel[0], "INFO: Main Rotor - Destination rotor position: " + destinationPosition);

                //set the limits
                MainRotor.SetValueFloat("LowerLimit", destinationPosition);
                MainRotor.SetValueFloat("UpperLimit", destinationPosition);

                //move the rotor to within the limits
                if (currentPositions[0] == destinationPosition)
                {
                    MainRotor.SetValueFloat("Velocity", 0f);
                    MainRotor.GetActionWithName("OnOff_Off").Apply(MainRotor); // Stop rotor 
                    Output(OutputAgent.OutputLevel[0], "INFO: Main Rotor - position reached, turning off");
                }
                else if (currentPositions[0] < destinationPosition)
                {
                    MainRotor.GetActionWithName("OnOff_On").Apply(MainRotor); // Start rotor 
                    MainRotor.SetValueFloat("Velocity", ROTOR_SPEED);
                    Output(OutputAgent.OutputLevel[0], "INFO: Main Rotor - currentPosition < destinationPosition ==> rotorspeed: " + -ROTOR_SPEED + " RPM");
                }
                else if (currentPositions[0] > destinationPosition)
                {
                    MainRotor.GetActionWithName("OnOff_On").Apply(MainRotor); // Start rotor 
                    MainRotor.SetValueFloat("Velocity", -ROTOR_SPEED);
                    Output(OutputAgent.OutputLevel[0], "INFO: Main Rotor - currentPosition > destinationPosition ==> rotorspeed: " + ROTOR_SPEED + " RPM");
                }

                if (MirrorRotor != null)
                {
                    if (InverseMirror)
                    {
                        Output(OutputAgent.OutputLevel[0], "INFO: Mirror Rotor - Invers of MainRotor");
                        destinationPosition = -destinationPosition;
                    }

                    Output(OutputAgent.OutputLevel[0], "INFO: Mirror Rotor - Current rotor position: " + currentPositions[1]);
                    Output(OutputAgent.OutputLevel[0], "INFO: Mirror Rotor - Destination rotor position: " + destinationPosition);

                    //set the limits
                    MirrorRotor.SetValueFloat("LowerLimit", destinationPosition);
                    MirrorRotor.SetValueFloat("UpperLimit", destinationPosition);

                    //move the rotor to within the limits
                    if (currentPositions[1] == destinationPosition)
                    {
                        MirrorRotor.SetValueFloat("Velocity", 0f);
                        MirrorRotor.GetActionWithName("OnOff_Off").Apply(MirrorRotor); // Stop rotor
                        Output(OutputAgent.OutputLevel[0], "INFO: Mirror Rotor - position reached, turning off");
                    }
                    else if (currentPositions[1] < destinationPosition)
                    {
                        MirrorRotor.GetActionWithName("OnOff_On").Apply(MirrorRotor); // Start rotor 
                        MirrorRotor.SetValueFloat("Velocity", ROTOR_SPEED);
                        Output(OutputAgent.OutputLevel[0], "INFO: Mirror Rotor - currentPosition < destinationPosition ==> rotorspeed: " + -ROTOR_SPEED + " RPM");
                    }
                    else if (currentPositions[1] > destinationPosition)
                    {
                        MirrorRotor.GetActionWithName("OnOff_On").Apply(MirrorRotor); // Start rotor 
                        MirrorRotor.SetValueFloat("Velocity", -ROTOR_SPEED);
                        Output(OutputAgent.OutputLevel[0], "INFO: Mirror Rotor - currentPosition > destinationPosition ==> rotorspeed: " + ROTOR_SPEED + " RPM");
                    }
                }
            }

            public void ForceRotorsTorque()
            {
                Output(OutputAgent.OutputLevel[0], "INFO: Forcing rotor torque");

                MainRotor.SetValueFloat("BrakingTorque", 36000000);
                MainRotor.SetValueFloat("Torque", 10000000);
                if (MirrorRotor != null)
                {
                    MirrorRotor.SetValueFloat("BrakingTorque", 36000000);
                    MirrorRotor.SetValueFloat("Torque", 10000000);
                }
            }

            public float[] RotorPositions()
            {
                var currentPositions = new float[2] { 0f, 0f };
                var currentposition = "";

                System.Text.RegularExpressions.Regex matchthis = new System.Text.RegularExpressions.Regex(@"^.+\n.+\:\s?(-?[0-9]+).*[\s\S]*$");
                System.Text.RegularExpressions.Match match = matchthis.Match(MainRotor.DetailedInfo);
                if (match.Success)
                {
                    currentposition = match.Groups[1].Value;
                }
                else
                {
                    Output(OutputAgent.OutputLevel[0], "ERROR: The main rotor position could not parsed");
                    Output(OutputAgent.OutputLevel[1], "ERROR: script stopped");
                    throw new FormatException("The main rotor position could not parsed");
                }
                currentPositions[0] = float.Parse(currentposition);

                if (MirrorRotor != null)
                {
                    match = matchthis.Match(MirrorRotor.DetailedInfo);
                    if (match.Success)
                    {
                        currentposition = match.Groups[1].Value;
                    }
                    else
                    {
                        Output(OutputAgent.OutputLevel[0], "ERROR: The mirror rotor position could not parsed");
                        Output(OutputAgent.OutputLevel[1], "ERROR: script stopped");
                        throw new FormatException("The mirror rotor position could not parsed");
                    }
                    currentPositions[1] = float.Parse(currentposition);
                }
                else
                {
                    currentPositions = new float[1] { currentPositions[0] };
                }

                return currentPositions;
            }
        }

        public class SolarFarm
        {
            public RotorPair RotorPair { get; private set; }
            public List<IMySolarPanel> SolarPanels { get; private set; }

            public SolarFarm(RotorPair rotorPair, List<IMySolarPanel> solarPanels)
            {
                RotorPair = rotorPair;
                SolarPanels = solarPanels;
            }

            public SolarFarm(string blockGroupName, List<IMyTerminalBlock> blocks)
            {
                var mainRotors = new List<IMyMotorAdvancedStator>();
                var mirrorRotors = new List<IMyMotorAdvancedStator>();

                SolarPanels = new List<IMySolarPanel>();

                foreach (var block in blocks)
                {
                    if (block is IMySolarPanel)
                    {
                        SolarPanels.Add(block as IMySolarPanel);
                        continue;
                    }

                    if (block is IMyMotorAdvancedStator)
                    {
                        if (block.CustomName.ToLower().Contains(SOLARFARM_MAINROTOR_NAME.ToLower()))
                        {
                            mainRotors.Add(block as IMyMotorAdvancedStator);
                        }
                        if (block.CustomName.ToLower().Contains(SOLARFARM_MIRRORROTOR_NAME.ToLower()))
                        {
                            mirrorRotors.Add(block as IMyMotorAdvancedStator);
                        }
                    }
                }

                Output(OutputAgent.OutputLevel[0], blockGroupName + " contains " + SolarPanels.Count + " solar panels");

                if (mainRotors.Count == 0)
                {
                    Output(OutputAgent.OutputLevel[0], "ERROR: No main rotor found for solar farm with block group name '" + blockGroupName + "'");
                    Output(OutputAgent.OutputLevel[1], "ERROR: No main rotor found for solar farm with block group name '" + blockGroupName + "'");
                    throw new Exception("ERROR: No main rotor found for solar farm with block group name '" + blockGroupName + "'");
                }
                if (mainRotors.Count > 1)
                {
                    Output(OutputAgent.OutputLevel[0], "ERROR: No main rotor found for solar farm with block group name '" + blockGroupName + "'");
                    Output(OutputAgent.OutputLevel[1], "ERROR: No main rotor found for solar farm with block group name '" + blockGroupName + "'");
                    throw new Exception("ERROR: No main rotor found for solar farm with block group name '" + blockGroupName + "'");
                }
                if (mirrorRotors.Count > 1)
                {
                    Output(OutputAgent.OutputLevel[0], "No mirror rotor found for solar farm with block group name '" + blockGroupName + "'");
                }

                if (mirrorRotors.Count == 0)
                {
                    RotorPair = new RotorPair(mainRotors[0], null);
                }
                else
                {
                    RotorPair = new RotorPair(mainRotors[0], mirrorRotors[0]);
                }
            }

            public float GetMaxMaxCurrentSolarPowerOutput()
            {
                var max = float.MinValue;

                var outputs = GetCurrentMaxSolarPowerOutput();


                foreach (var output in outputs)
                {
                    if (output > max)
                        max = output;
                }

                return max;
            }

            public float GetMinMaxCurrentSolarPowerOutput()
            {
                var min = float.MaxValue;

                var outputs = GetCurrentMaxSolarPowerOutput();


                foreach (var output in outputs)
                {
                    if (output < min)
                        min = output;
                }

                return min;
            }

            public float GetTotalMaxCurrentSolarPowerOutput()
            {
                var sum = 0f;

                foreach (var output in GetCurrentMaxSolarPowerOutput())
                {
                    sum += output;
                }

                return sum;
            }

            public float GetAverageMaxCurrentSolarPowerOutput()
            {
                return GetTotalMaxCurrentSolarPowerOutput() / (float)GetCurrentMaxSolarPowerOutput().Count;
            }

            public List<float> GetCurrentMaxSolarPowerOutput()
            {
                var outputs = new List<float>(SolarPanels.Count);

                for (int i = 0; i < outputs.Count; i++)
                {
                    var pwr = -1f;

                    string value = "";
                    string type = "";
                    System.Text.RegularExpressions.Regex matchthis = new System.Text.RegularExpressions.Regex(@"^.+\n.+\:\s?([0-9\.]+)\s(.*)\n.+$");
                    System.Text.RegularExpressions.Match match = matchthis.Match(SolarPanels[i].DetailedInfo);
                    if (match.Success)
                    {
                        value = match.Groups[1].Value;
                        type = match.Groups[2].Value;
                    }
                    else
                    {
                        Output(OutputAgent.OutputLevel[0], "ERROR: Can't parse DetailedInfo with regex");
                        Output(OutputAgent.OutputLevel[1], "ERROR: script stopped");
                        throw new Exception("Can't parse DetailedInfo with regex");
                    }
                    bool test = float.TryParse(value, out pwr); // Get power into variable 
                    if (type == "W") { pwr /= 1000; } // Make sure power is in kW
                    if (type == "MW") { pwr *= 1000; } // Make sure power is in kW 
                    if (!test)
                    {
                        Output(OutputAgent.OutputLevel[0], "ERROR: Can't parse power reading from solar panel: " + value);
                        Output(OutputAgent.OutputLevel[1], "ERROR: script stopped");
                        throw new Exception("Can't parse power reading from solar panel: " + value);
                    }
                }

                return outputs;
            }
        }

        public class OutputAgent
        {
            public static string[] OutputLevel = new string[] { "DEBUG", "STATUS" };

            public Dictionary<string, List<IMyTextPanel>> OutputPanels { get; private set; }

            public OutputAgent(IMyGridTerminalSystem GridTerminalSystem)
            {
                OutputPanels = new Dictionary<string, List<IMyTextPanel>>();

                foreach (var level in OutputLevel)
                {
                    OutputPanels.Add(level, new List<IMyTextPanel>());
                }

                var allPanels = new List<IMyTerminalBlock>();

                GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(allPanels);

                foreach (var panel in allPanels)
                {
                    if (panel.CustomName.ToLower().Contains(SOLARFARM_OUTPUTPANEL_DEBUG_NAME))
                    {
                        (panel as IMyTextPanel).WritePublicText("");
                        (panel as IMyTextPanel).WritePublicTitle("[SOL] Debug Output");
                        RegisterOutputPanel(OutputLevel[0], (panel as IMyTextPanel));
                        continue;
                    }
                    if (panel.CustomName.ToLower().Contains(SOLARFARM_OUTPUTPANEL_DEBUG_NAME))
                    {
                        (panel as IMyTextPanel).WritePublicText("");
                        (panel as IMyTextPanel).WritePublicTitle("[SOL] Status Output");
                        RegisterOutputPanel(OutputLevel[1], (panel as IMyTextPanel));
                        continue;
                    }
                }
            }

            public void RegisterOutputPanel(string outputlevel, IMyTextPanel panel)
            {
                List<IMyTextPanel> panelList;
                if (OutputPanels.TryGetValue(outputlevel, out panelList))
                {
                    if (!panelList.Contains(panel))
                    {
                        panelList.Add(panel);
                    }
                }
                else
                {
                    OutputPanels.Add(outputlevel, new List<IMyTextPanel>() { panel });
                }
            }

            public bool UnregisterOutputPanel(string outputlevel, IMyTextPanel panel)
            {
                bool succes = false;

                List<IMyTextPanel> panelList;
                if (OutputPanels.TryGetValue(outputlevel, out panelList))
                {
                    if (panelList.Contains(panel))
                    {
                        panelList.Remove(panel);
                        succes = true;
                    }
                }

                return succes;
            }

            public void Output(string outputLevel, string message)
            {
                foreach (var panel in OutputPanels[outputLevel])
                {
                    panel.WritePublicText(panel.GetPublicText() + message + "\n");
                }
            }
        }
    }
}