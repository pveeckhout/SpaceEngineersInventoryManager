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

        /*
        *
        * COPY FROM HERE
        *
        */

        #region Config
        //OPERATIONAL
        const float ROTOR_RPM = 0.5f;
        const float DRILL_DOWN_SPEED = 0.006f;
        readonly List<float> DRILL_RADII = new List<float>() { 0f, 3.5f, 7f, 10f }; //Drills can technically do a 5 wide trench, to be sure nu small floating rocks are left, do smaller intervals.
        const bool DEBUG = true;
        const bool FORCEROTOR_TORQUE = true;
        //BLOCK SETUP
        const string ROTOR_NAME = "Rotor";
        const string H_PISTON_NAME = "Horizontal Piston";
        const string V_PISTON_NAME = "Vertical Piston";
        const string ANTENNA_NAME = "Antenna";
        const string OUTPUTPANEL_NAME = "Output Panel";
        const string IRONEXTRACTOR_NAME = "Iron Extractor";
        const string CARGOCONTAINER_NAME = "Large Cargo Container";
        const float TARGET_DEPTH = 2f;
        #endregion

        private IMyPistonBase HorizontalPiston { get; set; }
        private List<IMyPistonBase> VerticalPistons { get; set; }
        private List<IMyShipDrill> Drills { get; set; }
        private IMyMotorAdvancedStator Rotor { get; set; }
        private IMyRadioAntenna Antenna { get; set; }
        private IMyTextPanel OutputPanel { get; set; }
        private IMyRefinery IronExtractor { get; set; }
        private IMyCargoContainer CargoContainer { get; set; }


        private bool toStart = false;
        private int currentCircle = 0;
        private bool safetyRounds = false;
        private string initArgs;

        //TODO: output status
        void Main(string argument)
        {
            initArgs = argument;

            //check if all blocks are initialized
            if (!(HorizontalPiston != null && VerticalPistons != null && Drills != null && Rotor != null && Antenna != null && OutputPanel != null && IronExtractor != null && CargoContainer != null))
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

            //TODO: move items from iron extractor to the container to prevent 'clogs'
            /*CLeanIronExtractor();
        }

        private void CLeanIronExtractor()
        {
            var ironExtractorOutputInventory = IronExtractor.GetInventory(1);
            var cargoContainerInventory = CargoContainer.GetInventory(0);

            for (int i = ironExtractorOutputInventory.GetItems().Count -1; i >= 0; i--)
            {
                var item = ironExtractorOutputInventory.GetItems()[1];
                if (cargoContainerInventory.CanItemsBeAdded(item.Amount, item.GetDefinitionId()))
                {
                    ironExtractorOutputInventory.TransferItemTo(CargoContainer.GetInventory(0), i);
                }
            }*/
        }

        private void Drill()
        {
            ClearOutput();
            //check for init  conditions
            if (currentCircle < 0)
            {
                if (DEBUG)
                    OutputToPanel("Drill sequece, init");

                //move to the start position
                ToStart();

                //if to start is false, the we are at the starting position, set the current index to 0
                if (!toStart)
                {
                    currentCircle = 0;

                    SetRotorLimits(float.NegativeInfinity, float.PositiveInfinity);

                    //Move the horizontal piston to the circle defined
                    MovePistonToPosition(HorizontalPiston, DRILL_RADII[currentCircle]);

                    //start the rotor
                    setRotorSpeed(ROTOR_RPM);

                    if (DEBUG)
                        OutputToPanel("Drill sequece init done");
                }
            }
            else
            {
                if (!(currentCircle >= DRILL_RADII.Count))
                {
                    //start the rotor
                    setRotorSpeed(ROTOR_RPM);

                    if (DEBUG)
                        OutputToPanel(string.Format("Drilling circle {0}", currentCircle));

                    //Move the horizontal piston to the circle defined
                    MovePistonToPosition(HorizontalPiston, DRILL_RADII[currentCircle]);

                    bool maxDepthReached = true;
                    var targetPistonPosition = (float)TARGET_DEPTH / (float)VerticalPistons.Count;

                    foreach (var piston in VerticalPistons)
                    {
                        MovePistonToPosition(piston, targetPistonPosition, (float)DRILL_DOWN_SPEED / (float)VerticalPistons.Count);
                        if (piston.CurrentPosition != targetPistonPosition)
                        {
                            if (DEBUG)
                            {
                                OutputToPanel(string.Format("Pistons current depth: {0}", piston.CurrentPosition));
                                OutputToPanel("All pistons did not reach max depth yet");
                            }

                            maxDepthReached &= false;
                        }
                    }

                    if (maxDepthReached)
                    {
                        if (DEBUG)
                            OutputToPanel("All pistons did reach max depth, returning to start position");

                        ToStart(++currentCircle);
                    }

                }
                else {
                    OutputToPanel("Drill sequece, closure");
                    //move to the start position
                    ToStart();

                    //if to start is false, the we are at the starting position, turn off all unneeded devices to concerve energy
                    if (!toStart)
                    {
                        SetRotorLimits(float.NegativeInfinity, float.PositiveInfinity);
                        OutputToPanel("Drill sequece done");

                        //TODO: turn off everything nonvital
                    }
                }
            }
        }

        private void SafetyRounds()
        {
            //clear the output screen
            ClearOutput();

            if (DEBUG)
            {
                OutputToPanel("Drilling the Safety rounds");
                OutputToPanel(string.Format("Current circle: {0}", currentCircle));
            }

            var currentRotorposition = GetRotorPosition();

            //turn the rotor to zero to if the current circle is -1
            if (currentCircle < 0)
            {
                if (DEBUG)
                    OutputToPanel("init safety rounds, moving rotor to start");

                //debug outs happen in the method
                MoveRotorToPosition(-2);
            }

            currentRotorposition = GetRotorPosition();

            if (DEBUG)
                OutputToPanel(string.Format("Current rotor position: {0}", currentRotorposition));

            /*start a new circle
            *
            * if the rotor position is 0 start a new circle by
            * 1) incrementing the current circle (first pass will invrement from -1 to 0)
            * 2) moving the hPiston to the respective position
            * 3) reversing the rotor so the the radius gets drilled the first time
            *
            * We need to check to prevent en index out of bounds. if that woudl happen, then we end the safety rounds.
            */
            if (currentRotorposition <= -2)
            {
                currentCircle++;

                if (DEBUG)
                    OutputToPanel(string.Format("Current circle: {0}", currentCircle));

                SetRotorLimits(-2, 362);

                //if current circle > circles defined go to back to start and safty circles are done
                if (currentCircle >= DRILL_RADII.Count)
                {
                    //set the safety rounds to false as we are done
                    safetyRounds = false;

                    //set the current position back to -1 (init state of each step)
                    currentCircle = -1;

                    //stop the rotor
                    setRotorSpeed(0f);

                    //Move the horizontal piston to the start circle
                    MovePistonToPosition(HorizontalPiston, DRILL_RADII[0]);

                    if (DEBUG)
                        OutputToPanel("Ended Safety Circles");

                    return;
                }

                //Move the horizontal piston to the circle defined
                MovePistonToPosition(HorizontalPiston, DRILL_RADII[currentCircle]);

                setRotorSpeed(ROTOR_RPM);
            }

            //reverse the drills so the radius gets drilled the second time
            if (currentRotorposition >= 362)
            {
                setRotorSpeed(-ROTOR_RPM);
            }

            if (DEBUG)
                OutputToPanel(string.Format("currentCircle: {0}", currentCircle));
        }

        private void ToStart(int targetCurrentCircle = -1)
        {
            if (DEBUG)
                OutputToPanel("setting up starting conditions");

            bool working = false;
            //When the rotor is turning, stop it
            if (DEBUG)
                OutputToPanel("Setting  Rotor speed to 0RPM");

            setRotorSpeed(0f);

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

            if (!working)
            {
                if (DEBUG)
                    OutputToPanel(string.Format("setting current step to: {0}", targetCurrentCircle));
                //set the current circle, defaul -1 (init state of each step)
                currentCircle = targetCurrentCircle;
            }


            //when the hPiston is retracted end to start and begin safety rounds
            toStart = working;
        }

        private void Init()
        {
            //move drills to start after init
            toStart = true;
            //initiate the safety circles after init
            safetyRounds = true && !initArgs.Contains("SKIPSAFETY");
            //reset the current circle index to init state
            currentCircle = -1;

            OutputPanel = GridTerminalSystem.GetBlockWithName(OUTPUTPANEL_NAME) as IMyTextPanel;

            if (DEBUG)
                OutputPanel.WritePublicText("INIT: clearing text\n");

            HorizontalPiston = GridTerminalSystem.GetBlockWithName(H_PISTON_NAME) as IMyPistonBase;
            VerticalPistons = new List<IMyPistonBase>();
            Drills = new List<IMyShipDrill>();
            Rotor = GridTerminalSystem.GetBlockWithName(ROTOR_NAME) as IMyMotorAdvancedStator;
            Antenna = GridTerminalSystem.GetBlockWithName(ANTENNA_NAME) as IMyRadioAntenna;
            IronExtractor = GridTerminalSystem.GetBlockWithName(IRONEXTRACTOR_NAME) as IMyRefinery;
            CargoContainer = GridTerminalSystem.GetBlockWithName(CARGOCONTAINER_NAME) as IMyCargoContainer;

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
                throw new FormatException("The rotor position could not parsed");
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
                setRotorSpeed(0f);
                Rotor.GetActionWithName("OnOff_Off").Apply(Rotor); // Stop rotor
                if (DEBUG)
                    OutputToPanel("position reached, turning off");
            }
            else if (currentPosition < destinationPosition)
            {
                Rotor.GetActionWithName("OnOff_On").Apply(Rotor); // Start rotor
                OutputToPanel("Rotor - currentPosition < destinationPosition");
                setRotorSpeed(ROTOR_RPM);
            }
            else if (currentPosition > destinationPosition)
            {
                Rotor.GetActionWithName("OnOff_On").Apply(Rotor); // Start rotor 
                OutputToPanel("Rotor - currentPosition > destinationPosition");
                setRotorSpeed(-ROTOR_RPM);
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

        private void setRotorSpeed(float rpm)
        {
            if (DEBUG)
                OutputToPanel(string.Format("Setting Rotor speed to: {0}", rpm));

            Rotor.SetValueFloat("Velocity", rpm);
            Rotor.GetActionWithName("OnOff_On").Apply(Rotor); // Start rotor
        }

        private void RemoveRotorLimits()
        {
            if (DEBUG)
                OutputToPanel("Resetting Rotor limits");

            SetRotorLimits(float.NegativeInfinity, float.PositiveInfinity);
        }

        private void MovePistonToPosition(IMyPistonBase piston, float destPosition, float speed = 0.5f)
        {
            if (DEBUG)
            {
                OutputToPanel(string.Format("Moving piston to: {0}", destPosition));
            }

            piston.SetValueFloat("LowerLimit", destPosition);
            piston.SetValueFloat("UpperLimit", destPosition);

            if (piston.CurrentPosition > destPosition)
            {
                piston.SetValueFloat("Velocity", -speed);
            }
            else
            {
                piston.SetValueFloat("Velocity", speed);
            }
        }

        /*
        *
        * COPY TO HERE
        *
        */
    }
}
