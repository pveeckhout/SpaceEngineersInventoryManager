using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersScripts
{
    class MiningStationAutomation
    {
        #region programming environment essential inits, DO NOT COPY TO GAME
        IMyGridTerminalSystem GridTerminalSystem;
        #endregion

        #region Config
        //OPERATIONAL
        const float ROTOR_RPM = 0.5f;
        const float DRILL_DOWN_SPEED = 0.006f;
        readonly List<float> DRILL_RADII = new List<float>() { 0, 5, 10 };
        const bool DEBUG = true;
        const bool FORCEROTOR_TORQUE = true;
        //BLOCK SETUP
        const string ROTOR_NAME = "Rotor";
        const string H_PISTON_NAME = "Horizontal Piston";
        const string V_PISTON_NAME = "Vertical Piston";
        const string ANTENNA_NAME = "Antenna";
        const string OUTPUTPANEL_NAME = "Output Panel";
        #endregion

        private IMyPistonBase HorizontalPiston { get; set; }
        private List<IMyPistonBase> VerticalPistons { get; set; }
        private List<IMyShipDrill> Drills { get; set; }
        private IMyMotorAdvancedStator Rotor { get; set; }
        private IMyRadioAntenna Antenna { get; set; }
        private IMyTextPanel OutputPanel { get; set; }

        private bool toStart = false;
        private int currentCircle = 0;
        private bool safetyRounds = false;
        private bool afteyRoundsFirstPass = false;

        void Main(string argument)
        {
            if (!(HorizontalPiston != null && VerticalPistons != null && Drills != null && Rotor != null && Antenna != null && OutputPanel != null))
            {
                Init();
            }

            if (toStart)
            {
                ToStart();
                return;
            }

            if (safetyRounds)
            {
                SafetyRounds();
                return;
            }

            Drill();
        }

        private void Drill()
        {
            //TODO:
        }

        private void SafetyRounds()
        {
            //clear the output
            ClearOutput();

            if (DEBUG)
                OutputToPanel("Started drilling the Safty rounds");

            var currentRotorposition = GetRotorPosition();
            if (DEBUG)
                OutputToPanel("CurrentRotorposition: " + currentRotorposition);

            //Move the horizontal piston to the circle defined
            MovePistonToPosition(HorizontalPiston, DRILL_RADII[currentCircle]);
            // set the rotor limit to 1 circle
            SetRotorLimits(0, 720);

            //turn the rotor to zero to start on first pass
            if (afteyRoundsFirstPass)
            {
                Rotor.SetValueFloat("Velocity", -ROTOR_RPM);
                afteyRoundsFirstPass = false;
            }

            if (currentRotorposition == 0)
            {
                if (currentCircle % 2 == 0)
                {
                    //do even circle
                    if (DEBUG)
                        OutputToPanel("do even safety circle");
                    Rotor.SetValueFloat("Velocity", ROTOR_RPM);
                }
                else
                {
                    if (DEBUG)
                        OutputToPanel("reached end of uneven circle, increasing count");
                    currentCircle++;
                }
            }
            else if (currentRotorposition == 720)
            {
                if (currentCircle % 2 != 0)
                {
                    //do uneven circle 
                    if (DEBUG)
                        OutputToPanel("do uneven safety circle");
                    Rotor.SetValueFloat("Velocity", -ROTOR_RPM);
                }
                else
                {
                    if (DEBUG)
                        OutputToPanel("reached end of even circle, increasing count");
                    currentCircle++;
                }
            }

            if (DEBUG)
                OutputToPanel(string.Format("currentCircle: {0}", currentCircle));

            //if current circle > circles defined go to back to start and safty cricles are done
            if (currentCircle > DRILL_RADII.Count)
            {
                if (DEBUG)
                    OutputToPanel("Ended Safety Cricles");
                ToStart();
                safetyRounds = false;
            }
        }

        private void ToStart()
        {
            ClearOutput();

            if (DEBUG)
                OutputToPanel("setting up starting conditions");

            bool working = false;
            //When the rotor is turning, stop it
            if (DEBUG)
                OutputToPanel("Setting  Rotor speed to 0RPM");
            Rotor.SetValueFloat("Velocity", 0f);

            //If the drills ar not on, turn on
            foreach (var drill in Drills)
            {
                if (DEBUG)
                    OutputToPanel(string.Format("turning on drill {0}", drill.CustomName));
                drill.GetActionWithName("OnOff_On").Apply(drill);
            }

            if (DEBUG)
                OutputToPanel(string.Format("Action in progress: {0}", working));

            //While the vPistons are not retracted, retract
            if (!working)
            {
                foreach (var piston in VerticalPistons)
                {

                    if (piston.CurrentPosition > 0)
                    {
                        if (DEBUG)
                            OutputToPanel(string.Format("moving Vertical Piston '{0}' to start position", piston.CustomName));

                        working = true;
                        MovePistonToPosition(piston, 0);
                    }
                }
            }
            if (DEBUG)
                OutputToPanel(string.Format("Action in progress: {0}", working));

            //if working, stop method
            if (working)
            {
                return;
            }

            //When the vPistons are retracted, retract the hPiston
            if (HorizontalPiston.CurrentPosition > 0)
            {
                if (DEBUG)
                    OutputToPanel(string.Format("moving Horizontal Piston to start position"));

                working = true;
                MovePistonToPosition(HorizontalPiston, 0);
            }

            if (DEBUG)
                OutputToPanel(string.Format("Action in progress: {0}", working));

            //if working, stop method
            if (working)
            {
                return;
            }

            //TODO: move rotor to 0 degree
            if (GetRotorPosition() != 0)
            {
                working = true;
                MoveRotorToPosition(0);
            }
            else
            {
                RemoveRotorLimits();
            }


            if (DEBUG)
                OutputToPanel(string.Format("reached end of Init() work in progress: {0}", working));

            //when the hPiston is retracted end to start and begin safety rounds
            toStart = working;
            //safetyRounds has been set to true during Init()
            //safetyRounds = true;
            //set firstpass on safety rounds tre
            afteyRoundsFirstPass = true;

        }

        private void Init()
        {
            //move drills to start after init
            toStart = true;
            //initiate the safety circles after init
            safetyRounds = true;
            //reset the current circle index
            currentCircle = 0;

            OutputPanel = GridTerminalSystem.GetBlockWithName(OUTPUTPANEL_NAME) as IMyTextPanel;

            if (DEBUG)
                OutputPanel.WritePublicText("INIT: clearing text\n");

            HorizontalPiston = GridTerminalSystem.GetBlockWithName(H_PISTON_NAME) as IMyPistonBase;
            VerticalPistons = new List<IMyPistonBase>();
            Drills = new List<IMyShipDrill>();
            Rotor = GridTerminalSystem.GetBlockWithName(ROTOR_NAME) as IMyMotorAdvancedStator;
            Antenna = GridTerminalSystem.GetBlockWithName(ANTENNA_NAME) as IMyRadioAntenna;

            if (FORCEROTOR_TORQUE)
                ForceRotorsTorque();

            var pistonTempList = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistonTempList);

            foreach (var vPiston in pistonTempList)
            {
                if (vPiston.CustomName.Contains(V_PISTON_NAME))
                    VerticalPistons.Add(vPiston as IMyPistonBase);
            }

            var drillTempList = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drillTempList);

            foreach (var drill in drillTempList)
            {
                Drills.Add(drill as IMyShipDrill);
            }

            if (DEBUG)
                OutputToPanel(string.Format("Initialized the blocks, found the horizontal piston: {0}, #{1} vertical pistons, #{2} drils, Rotor: {3}, Antena: {4}\n", HorizontalPiston != null, VerticalPistons.Count, Drills.Count, Rotor != null, Antenna != null));
        }

        private void OutputToPanel(string text)
        {
            if (!text.EndsWith("\n"))
                text += "\n";
            OutputPanel.WritePublicText(OutputPanel.GetPublicText() + text);
        }

        private void ClearOutput()
        {
            OutputPanel.WritePublicText("");
        }

        private float GetRotorPosition()
        {
            var currentposition = "";

            System.Text.RegularExpressions.Regex matchthis = new System.Text.RegularExpressions.Regex(@"^.+\n.+\:\s?(-?[0-9]+).*[\s\S]*$");
            System.Text.RegularExpressions.Match match = matchthis.Match(Rotor.DetailedInfo);
            if (match.Success)
            {
                currentposition = match.Groups[1].Value;
            }
            else
            {
                OutputToPanel("ERROR: The main rotor position could not parsed");
                OutputToPanel("ERROR: script stopped");
                throw new FormatException("The main rotor position could not parsed");
            }
            return float.Parse(currentposition);
        }

        private void ForceRotorsTorque()
        {
            if (DEBUG)
                OutputToPanel("Forcing rotor torque");

            Rotor.SetValueFloat("BrakingTorque", 36000000);
            Rotor.SetValueFloat("Torque", 10000000);
        }

        private void MoveRotorToPosition(float destinationPosition)
        {

            var currentPosition = GetRotorPosition();

            if (DEBUG)
            {
                OutputToPanel("Rotor Current rotor position: " + currentPosition);
                OutputToPanel("Rotor Destination rotor position: " + destinationPosition);
                OutputToPanel("Rotor current LowerLimit: " + Rotor.GetValueFloat("LowerLimit"));
                OutputToPanel("Rotor current UpperLimit: " + Rotor.GetValueFloat("UpperLimit"));
            }

            //set the limits
            Rotor.SetValueFloat("LowerLimit", destinationPosition);
            Rotor.SetValueFloat("UpperLimit", destinationPosition);

            //move the rotor to within the limits
            if (currentPosition == destinationPosition)
            {
                Rotor.SetValueFloat("Velocity", 0f);
                Rotor.GetActionWithName("OnOff_Off").Apply(Rotor); // Stop rotor
                if (DEBUG)
                    OutputToPanel("position reached, turning off");
            }
            else if (currentPosition < destinationPosition)
            {
                Rotor.GetActionWithName("OnOff_On").Apply(Rotor); // Start rotor 
                Rotor.SetValueFloat("Velocity", ROTOR_RPM);
                OutputToPanel("Rotor - currentPosition < destinationPosition ==> rotorspeed: " + -ROTOR_RPM + " RPM");
            }
            else if (currentPosition > destinationPosition)
            {
                Rotor.GetActionWithName("OnOff_On").Apply(Rotor); // Start rotor 
                Rotor.SetValueFloat("Velocity", -ROTOR_RPM);
                OutputToPanel("Rotor - currentPosition > destinationPosition ==> rotorspeed: " + ROTOR_RPM + " RPM");
            }
        }

        private void SetRotorLimits(float lower, float upper)
        {
            if (DEBUG)
            {
                OutputToPanel("Setting Rotor limits");
                OutputToPanel(string.Format("Rotor current limit: [{0},{1}]", Rotor.GetValueFloat("LowerLimit"), Rotor.GetValueFloat("UpperLimit")));
                OutputToPanel(string.Format("setting to [{0},{1}]", lower, upper));
            }

            Rotor.SetValueFloat("LowerLimit", lower);
            Rotor.SetValueFloat("UpperLimit", upper);
        }

        private void RemoveRotorLimits()
        {
            if (DEBUG)
                OutputToPanel("Resetting Rotor limits");

            SetRotorLimits(float.NegativeInfinity, float.PositiveInfinity);
        }

        private void MovePistonToPosition(IMyPistonBase piston, float destPosition)
        {
            if (DEBUG)
            {
                OutputToPanel(string.Format("Moving piston to: {0}", destPosition));
            }

            piston.SetValueFloat("LowerLimit", destPosition);
            piston.SetValueFloat("UpperLimit", destPosition);

            if (piston.CurrentPosition > destPosition)
            {
                piston.SetValueFloat("Velocity", -0.5f);
            }
            else
            {
                piston.SetValueFloat("Velocity", 0.5f);
            }
        }
    }
}
