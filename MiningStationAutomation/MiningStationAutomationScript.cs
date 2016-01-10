using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersScripts.MiningStationAutomation
{
    class MiningStationAutomationScript
    {
        #region programming environment essential inits, DO NOT COPY TO GAME
        IMyGridTerminalSystem GridTerminalSystem;
        IMyProgrammableBlock Me { get; }
        private static void Echo(string message) { }
        private static string Storage;
        #endregion

        #region Programmable block script
        #region Config
        //OPERATIONAL
        const float ROTOR_RPM = 0.5f;
        const float DRILL_DOWN_SPEED = 0.006f;
        static readonly List<float> DRILL_RADII = new List<float>() { 0f, 3.5f, 7f, 10f }; //Drills can technically do a 5 wide trench, to be sure nu small floating rocks are left, do smaller intervals.
        const bool DEBUG = true;
        const bool FORCEROTOR_TORQUE = true;
        const bool INIT_FLATTENING = true; // safety precaoution
        const bool END_FLATTENING = true; //flatten pit bottom to allow cars to drive more easily;

        //BLOCK SETUP
        const string ROTOR_NAME = "Drill Rotor";
        const string H_PISTON_NAME = "Horizontal Piston";
        const string V_PISTON_NAME = "Vertical Piston";
        const string DRILL_STATION_NAME = "Drill Station";
        const string DEBUG_PANEL_NAME = "Debug Panel";
        const float TARGET_DEPTH = 20f;
        #endregion

        DrillStation station = null;

        void Main()
        {
            if (station == null)
            {
                station = new DrillStation(GridTerminalSystem);
            }

            station.Request();
        }

        /// <summary>
        /// The InitState
        /// </summary>
        class InitState : State
        {
            public void Handle(Context context)
            {
                var drillStationBlocks = (context as DrillStation).DrillStationBlocks;

                //turn on all antenna
                drillStationBlocks.Antennas.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_On").Apply(block);
                });

                //turn on all containers
                drillStationBlocks.CargoContainers.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_On").Apply(block);
                });

                //turn on all panels
                drillStationBlocks.DebugPanels.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_On").Apply(block);
                });

                //turn on all drills
                drillStationBlocks.Drills.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_On").Apply(block);
                });

                //turn on all hPistons
                drillStationBlocks.HorizontalPiston.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_On").Apply(block);
                });

                //turn on all refineries
                drillStationBlocks.Refineries.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_On").Apply(block);
                });

                //turn on the rotor
                drillStationBlocks.Rotor.GetActionWithName("OnOff_On").Apply(drillStationBlocks.Rotor);

                //turn on all vPistons
                drillStationBlocks.VerticalPistons.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_On").Apply(block);
                });

                //move the vertical pistons to fully retrackted with 1m/s speed
                if (BlockUtils.MovePistonsToPosition(drillStationBlocks.VerticalPistons, 0, 1))
                {
                    //move the horizontal pistons to fully retrackted with 1m/s speed
                    if (BlockUtils.MovePistonsToPosition(drillStationBlocks.HorizontalPiston, 0, 1))
                    {
                        //move the rotor to 0 degree with 1rpm
                        if (BlockUtils.MoveRotorToPosition(drillStationBlocks.Rotor, 0, 1))
                        {
                            //when doen proceed to the next state
                            if (INIT_FLATTENING)
                            {
                                context.State = new FlatteningState(0);
                            }
                            else
                            {
                                context.State = new DeepeningState();
                            }
                        }

                    }
                }
            }
        }

        /// <summary>
        /// The FlatteningState
        /// </summary>
        class FlatteningState : State
        {
            private int currentCircle = 0;
            protected readonly float depth;

            /// <summary>
            /// initializes a new FlatteningState
            /// </summary>
            /// <param name="targetDepth"> the depth on wich the flattening needs to happen</param>
            public FlatteningState(float targetDepth)
            {
                this.depth = targetDepth;
            }

            public void Handle(Context context)
            {
                var drillStationBlocks = (context as DrillStation).DrillStationBlocks;

                //move the vPistons to the desired depth with speed of 1m/s
                if (!BlockUtils.MovePistonsToPosition(drillStationBlocks.VerticalPistons, depth, 1))
                {
                    return;
                }

                //move the hPistons to the first radius with speed of 1m/s
                if (!BlockUtils.MovePistonsToPosition(drillStationBlocks.HorizontalPiston, DRILL_RADII[currentCircle], 1))
                {
                    return;
                }

                //move the rotor to -360 degree on even circles, to 0 degree on unevem circles, with ROTOR_RPM
                var targetDegree = (currentCircle % 2 == 0) ? -360 : 0;
                if (!BlockUtils.MoveRotorToPosition(drillStationBlocks.Rotor, targetDegree, ROTOR_RPM))
                {
                    //when it is not there yet, return
                    return;
                }

                //if reached the target, increment currentCircle
                currentCircle++;
                
                //if currentCircle == the number of radii, them all the circkles have been done.
                //proceed to next state
                if (currentCircle == DRILL_RADII.Count)
                {
                    context.State = new DeepeningState();
                }
            }
        }

        /// <summary>
        /// The DrillingState
        /// </summary>
        class DeepeningState : State
        {
            public void Handle(Context context)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The DoneState
        /// </summary>
        class DoneState : State
        {
            public void Handle(Context context)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The 'State' interface
        /// </summary>
        interface State
        {
            void Handle(Context context);
        }

        abstract class Context
        {
            private State _state;

            // Gets or sets the state
            public State State
            {
                get { return _state; }
                set { _state = value; }
            }

            /// <summary>
            /// 
            /// </summary>
            public virtual void Request()
            {
                _state.Handle(this);
            }
        }

        /// <summary>
        /// The 'Context' class
        /// </summary>
        class DrillStation : Context
        {
            private DrillStationBlocks _drillStationBlocks;

            // Constructor
            public DrillStation(IMyGridTerminalSystem GridTerminalSystem)
            {
                if (Storage.Contains("StateName"))
                {
                    //get the state from storage
                    var entries = Storage.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                    entries.ForEach(entry =>
                    {
                        if (entry.Contains("StateName"))
                        {
                            var stateName = entry.Split('=')[1];

                            Echo(string.Format("Found State {0} persisted in Storage with key 'StateName', using to init", stateName));

                            switch (stateName)
                            {
                                case "InitState":
                                    this.State = new InitState();
                                    break;
                                case "InitFlatteningState":
                                    this.State = new FlatteningState(0);
                                    break;
                                case "DeepeningState":
                                    this.State = new DeepeningState();
                                    break;
                                case "EndFlatteningState":
                                    this.State = new FlatteningState(TARGET_DEPTH);
                                    break;
                                case "DoneState":
                                    this.State = new DoneState();
                                    break;
                                default:
                                    Echo(string.Format("StateName {0} was not recognized as a State, using default InitState", stateName));
                                    //init is the default state
                                    this.State = new InitState();
                                    break;
                            }
                        }
                    });
                }
                else
                {
                    //init is the default state
                    State = new InitState();
                }

                //build the station blocks
                this._drillStationBlocks = new DrillStationBlocks(GridTerminalSystem);
            }

            // Gets the DrillStationBlocks
            public DrillStationBlocks DrillStationBlocks
            {
                get { return _drillStationBlocks; }
            }
        }

        public class DrillStationBlocks
        {
            public IMyMotorAdvancedStator Rotor { get; set; }
            public List<IMyPistonBase> HorizontalPiston { get; set; }
            public List<IMyPistonBase> VerticalPistons { get; set; }
            public List<IMyShipDrill> Drills { get; set; }
            public List<IMyRadioAntenna> Antennas { get; set; }
            public List<IMyTextPanel> DebugPanels { get; set; }
            public List<IMyRefinery> Refineries { get; set; }
            public List<IMyCargoContainer> CargoContainers { get; set; }

            public DrillStationBlocks(IMyGridTerminalSystem GridTerminalSystem)
            {
                Rotor = GridTerminalSystem.GetBlockWithName(ROTOR_NAME) as IMyMotorAdvancedStator;

                //HorizontalPiston 
                HorizontalPiston = new List<IMyPistonBase>();
                var hPistonTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(hPistonTempList);
                hPistonTempList.ForEach(vPiston =>
                {
                    if (vPiston.CustomName.Contains(H_PISTON_NAME))
                    {
                        HorizontalPiston.Add(vPiston as IMyPistonBase);
                    }
                });

                //VerticalPistons 
                VerticalPistons = new List<IMyPistonBase>();
                var vPistonTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(vPistonTempList);
                vPistonTempList.ForEach(vPiston =>
                {
                    if (vPiston.CustomName.Contains(V_PISTON_NAME))
                    {
                        VerticalPistons.Add(vPiston as IMyPistonBase);
                    }
                });

                //Drills
                Drills = new List<IMyShipDrill>();
                var drillTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drillTempList);
                drillTempList.ForEach(drill => Drills.Add(drill as IMyShipDrill));

                //Antennas
                Antennas = new List<IMyRadioAntenna>();
                var antennaTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(antennaTempList);
                antennaTempList.ForEach(antenna => Antennas.Add(antenna as IMyRadioAntenna));

                //DebugPanels
                DebugPanels = new List<IMyTextPanel>();
                var debugPanelTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(debugPanelTempList);
                debugPanelTempList.ForEach(debugPanel => DebugPanels.Add(debugPanel as IMyTextPanel));

                //Refineries
                Refineries = new List<IMyRefinery>();
                var refineryTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineryTempList);
                refineryTempList.ForEach(refinery => Refineries.Add(refinery as IMyRefinery));

                //CargoContainers
                CargoContainers = new List<IMyCargoContainer>();
                var containerTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containerTempList);
                containerTempList.ForEach(antenna => CargoContainers.Add(antenna as IMyCargoContainer));
            }
        }

        public static class BlockUtils
        {
            /// <summary>
            /// Sets the state to all antena connected on the GridTerminalSystem
            /// </summary>
            /// <param name="antennaLists">List of IMyRadioAntenna</param>
            /// <param name="status">Current status</param>
            public static void SetStatusToAntennas(List<IMyRadioAntenna> antennaLists, string status)
            {
                if (antennaLists == null)
                    return;

                string antennaName = "{0} - {1}";
                antennaLists.ForEach(antenna => antenna.SetCustomName(string.Format(antennaName, DRILL_STATION_NAME, status)));
            }

            /// <summary>
            /// gets the total amount of psiton extention
            /// </summary>
            /// <param name="pistons">List of IMyPistonBase</param>
            /// <returns>the total extention in meter</returns>
            public static float GetPistonsTotalPosition(List<IMyPistonBase> pistons)
            {
                var total = 0f;

                pistons.ForEach(piston => total += piston.CurrentPosition);

                return total;
            }

            /// <summary>
            /// Evaluates the rotors currect position
            /// </summary>
            /// <param name="rotor">The rotor to get the position from.</param>
            /// <returns>the rotor degree position</returns>
            public static float GetRotorPosition(IMyMotorAdvancedStator rotor)
            {
                var currentposition = "";

                System.Text.RegularExpressions.Regex matchthis = new System.Text.RegularExpressions.Regex(@"^.+\n.+\:\s?(-?[0-9]+).*[\s\S]*$");
                System.Text.RegularExpressions.Match match = matchthis.Match(rotor.DetailedInfo);
                if (match.Success)
                {
                    currentposition = match.Groups[1].Value;
                }
                else
                {
                    Echo("The rotor position could not parsed");
                    throw new FormatException("The rotor position could not parsed");
                }
                return float.Parse(currentposition);
            }

            /// <summary>
            /// forces the torque of the rotor to what i would call safe automatic operational levels
            /// </summary>
            public static void ForceRotorsTorque(IMyMotorAdvancedStator rotor)
            {
                rotor.SetValueFloat("BrakingTorque", 36000000);
                rotor.SetValueFloat("Torque", 10000000);
            }

            /// <summary>
            /// Moves the rotor to a certain position
            /// </summary>
            /// <param name="destinationPosition">the degree value of the postion</param>
            /// <param name="rpm">the rotor speed to move with</param>
            /// <returns>returns true wen the rotor is in postion, false if it needs to move.</returns>
            public static bool MoveRotorToPosition(IMyMotorAdvancedStator rotor, float destinationPosition, float rpm)
            {
                var currentPosition = GetRotorPosition(rotor);

                //set the limits
                SetRotorLimits(rotor, destinationPosition, destinationPosition);

                //move the rotor to within the limits
                if (currentPosition == destinationPosition)
                {
                    setRotorSpeed(rotor, 0f);
                    rotor.GetActionWithName("OnOff_Off").Apply(rotor); // Stop rotor
                    return true;
                }
                else if (currentPosition < destinationPosition)
                {
                    rotor.GetActionWithName("OnOff_On").Apply(rotor); // Start rotor
                    setRotorSpeed(rotor, rpm);
                    return false;
                }
                else if (currentPosition > destinationPosition)
                {
                    rotor.GetActionWithName("OnOff_On").Apply(rotor); // Start rotor
                    setRotorSpeed(rotor, -rpm);
                    return false;
                }

                return false;
            }

            /// <summary>
            /// Sets the rotor limits
            /// </summary>
            /// <param name="rotor">the rotor to set the limits on</param>
            /// <param name="lower">the lower bound</param>
            /// <param name="upper">the upper bound</param>
            public static void SetRotorLimits(IMyMotorAdvancedStator rotor, float lower, float upper)
            {
                //warn for fuckery if settin values possible out of bounds when not obviously meant to be that way
                if ((lower < -360 && lower != float.NegativeInfinity) || (upper > 360 && upper != float.PositiveInfinity))
                {
                    Echo("[WARN] Setting Rotor limits is doing wierd stuff around or beyond the 360 degree mark, often SE interprets this as infinity");
                }

                rotor.SetValueFloat("LowerLimit", lower);
                rotor.SetValueFloat("UpperLimit", upper);
            }

            /// <summary>
            /// Sets the rotor speeds
            /// </summary>
            /// <param name="rotor">the rotor to set the limits on</param>
            /// <param name="rpm">the rotor speed in rpm</param>
            public static void setRotorSpeed(IMyMotorAdvancedStator rotor, float rpm)
            {
                rotor.SetValueFloat("Velocity", rpm);
                rotor.GetActionWithName("OnOff_On").Apply(rotor); // Start rotor
            }

            /// <summary>
            /// sets the rotor limits to [-infity,infinity]
            /// </summary>
            /// <param name="rotor">The rotor to set the limits on.</param>
            public static void RemoveRotorLimits(IMyMotorAdvancedStator rotor)
            {
                SetRotorLimits(rotor, float.NegativeInfinity, float.PositiveInfinity);
            }

            /// <summary>
            /// moves the pistons to a certain extension postion, if there are multiple pistons, then the destPosition is split between all the psitons
            /// </summary>
            /// <param name="pistons"></param>
            /// <param name="destPosition"></param>
            /// <param name="speed"></param>
            /// <returns>true if the pistosn is in position</returns>
            public static bool MovePistonsToPosition(List<IMyPistonBase> pistons, float destPosition, float speed)
            {
                var inPosition = true;

                pistons.ForEach(piston =>
                {
                    inPosition &= MovePistonToPosition(piston, destPosition / (float)pistons.Count, speed / (float)pistons.Count);
                });

                return inPosition;
            }

            /// <summary>
            /// moves the piston to a certain position
            /// </summary>
            /// <param name="piston"></param>
            /// <param name="destinationPosition"></param>
            /// <param name="speed"></param>
            /// <returns>true if the piston is in position</returns>
            public static bool MovePistonToPosition(IMyPistonBase piston, float destinationPosition, float speed)
            {
                piston.SetValueFloat("LowerLimit", destinationPosition);
                piston.SetValueFloat("UpperLimit", destinationPosition);

                var currentPosition = piston.CurrentPosition;

                //move the rotor to within the limits
                if (currentPosition == destinationPosition)
                {
                    // Stop piston
                    piston.SetValueFloat("Velocity", 0);
                    return true;
                }
                else if (currentPosition < destinationPosition)
                {
                    piston.SetValueFloat("Velocity", speed);
                    return false;
                }
                else if (currentPosition > destinationPosition)
                {
                    piston.SetValueFloat("Velocity", -speed);
                    return false;
                }

                return false;
            }
        }
    }
    #endregion
}


