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
    //TODO: fix FlatteningState.Handle
    //TODO: output to debug
    class MiningStationAutomation
    {
        #region programming environment essential inits, DO NOT COPY TO GAME
        private static IMyGridTerminalSystem GridTerminalSystem;
        private static IMyProgrammableBlock Me { get; }
        private static void Echo(string message) { }
        private static string Storage;
        #endregion

        #region Programmable block script
        #region Config
        //OPERATIONAL
        const float ROTOR_RPM = 0.5f;
        const float DRILL_DOWN_SPEED = 0.008f; //discovered that .008 is still not to fast for the drill down speed
        static readonly List<float> DRILL_RADII = new List<float>() { 0f, 3.5f, 7f, 10f }; //Drills can technically do a 5 wide trench, to be sure nu small floating rocks are left, do smaller intervals.
        const bool DEBUG = true;
        const bool FORCEROTOR_TORQUE = true;
        const bool INIT_FLATTENING = false; //safety precaution
        const bool END_FLATTENING = false; //flatten pit bottom to allow cars to drive more easily;
        const float VERTICAL_OFFSET = 0f;

        //BLOCK SETUP
        const string TIMER_NAME = "Timer";
        const string PROGRAMMABLEBLOCK_NAME = "Programmable Block";
        const string ROTOR_NAME = "Drill Rotor";
        const string H_PISTON_NAME = "Horizontal Piston";
        const string V_PISTON_NAME = "Vertical Piston";
        const string DRILL_STATION_NAME = "Drill Station";
        const string DEBUG_PANEL_NAME = "Debug Panel";
        const float TARGET_DEPTH = 20f;
        #endregion

        DrillStation station = null;

        void Main(string arg)
        {
            if (arg.Contains("reseststorage=true"))
            {
                Storage = "";
            }

            if (station == null)
            {
                station = new DrillStation(GridTerminalSystem, Storage);
            }

            station.Request();

            Storage = station.State.GetStateDTO(station).ToString();
            Echo(string.Format("Storage:\n{0}", Storage));
        }

        /// <summary>
        /// The InitState
        /// </summary>
        class InitState : State
        {
            public StateDTO GetStateDTO(Context context)
            {
                return new StateDTO(typeof(InitState).Name, -1, -1);
            }

            public void Handle(Context context)
            {
                var drillStationBlocks = (context as DrillStation).DrillStationBlocks;

                //turn on the timer block and set the interval, also start the timer
                drillStationBlocks.Timer.GetActionWithName("OnOff_On").Apply(drillStationBlocks.Timer);
                drillStationBlocks.Timer.SetValueFloat("TriggerDelay", 1);
                drillStationBlocks.Timer.GetActionWithName("Start").Apply(drillStationBlocks.Timer);

                //turn on all antenna
                drillStationBlocks.Antennas.ForEach(block =>
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

                //if the storage contains state info, load it.
                var storage = (context as DrillStation).PersistantStorage;
                if (storage.Contains("state="))
                {
                    context.State = new StateDTO(storage).BuildState();
                }
                else {
                    //move to the start position (pistons at 1m/s rotor at 1 rpm)
                    if (drillStationBlocks.ToPosition(drillStationBlocks.VerticalPistons, 0, 1, drillStationBlocks.HorizontalPiston, 0, 1, drillStationBlocks.Rotor, 0, 1))
                    {
                        //when done proceed to the next state
                        if (INIT_FLATTENING)
                        {
                            context.State = new FlatteningState(VERTICAL_OFFSET);
                        }
                        else
                        {
                            context.State = new DeepeningState();
                        }
                    }
                }
            }

            public void LoadFromStateDTO(StateDTO stateDTO)
            {
                //nothing to do here
                return;
            }

            public string Status(Context context)
            {
                //leave blank
                return "INIT";
            }
        }

        /// <summary>
        /// The FlatteningState
        /// </summary>
        class FlatteningState : State
        {
            private int currentCircle = 0;
            private float depth;

            /// <summary>
            /// initializes a new FlatteningState
            /// </summary>
            /// <param name="targetDepth"> the depth on wich the flattening needs to happen</param>
            public FlatteningState(float targetDepth)
            {
                this.depth = targetDepth;
            }

            public StateDTO GetStateDTO(Context context)
            {
                return new StateDTO(typeof(FlatteningState).Name, currentCircle, depth);
            }

            public void Handle(Context context)
            {
                var drillStationBlocks = (context as DrillStation).DrillStationBlocks;

                //if currentCircle < the number of radii the flatten, else move to start, and proceed to next state
                if (currentCircle < DRILL_RADII.Count)
                {
                    //move the rotor to -360 degree on even circles, to 0 degree on unevem circles, with ROTOR_RPM
                    var targetDegree = (currentCircle % 2 == 0) ? -360 : 0;
                    if ((drillStationBlocks.ToPosition(drillStationBlocks.VerticalPistons, depth, 1, drillStationBlocks.HorizontalPiston, DRILL_RADII[currentCircle], 1, drillStationBlocks.Rotor, targetDegree, ROTOR_RPM)))
                    {
                        //when it is not there
                        currentCircle++;
                    }
                }
                else
                {
                    //move to the start position (pistons at 1m/s rotor at 1 rpm)
                    if (drillStationBlocks.ToPosition(drillStationBlocks.VerticalPistons, 0, 1, drillStationBlocks.HorizontalPiston, 0, 1, drillStationBlocks.Rotor, 0, 1))
                    {
                        if (depth != TARGET_DEPTH)
                        {
                            context.State = new DeepeningState();
                        }
                        else
                        {
                            context.State = new DoneState();
                        }
                    }
                }
            }

            public string Status(Context context)
            {
                if (currentCircle == DRILL_RADII.Count)
                {
                    return "Flattening DONE";
                }
                else
                {
                    return string.Format("Flattening [{0}/{1}] ({2:0.##}m)", currentCircle + 1, DRILL_RADII.Count, BlockUtils.GetPistonsTotalPosition((context as DrillStation).DrillStationBlocks.VerticalPistons));
                }
            }

            public void LoadFromStateDTO(StateDTO stateDTO)
            {
                this.currentCircle = stateDTO.Circle;
                this.depth = stateDTO.Depth;
            }
        }

        /// <summary>
        /// The DrillingState
        /// </summary>
        class DeepeningState : State
        {
            private int currentCircle = 0;
            private bool depthReached = true;
            private float verticalOffset = VERTICAL_OFFSET;

            public StateDTO GetStateDTO(Context context)
            {
                return new StateDTO(typeof(DeepeningState).Name, currentCircle, depthReached ? verticalOffset : BlockUtils.GetPistonsTotalPosition((context as DrillStation).DrillStationBlocks.VerticalPistons));
            }

            public void LoadFromStateDTO(StateDTO stateDTO)
            {
                currentCircle = stateDTO.Circle;

                //we need to know how much the drills drop per round to build in safety margin
                var safetyMargin = 60 * DRILL_DOWN_SPEED / ROTOR_RPM;

                verticalOffset = stateDTO.Depth - safetyMargin;
            }

            public void Handle(Context context)
            {
                var drillStationBlocks = (context as DrillStation).DrillStationBlocks;

                //if currentCircle < the number of radii the flatten, else move to start, and proceed to next state
                if (currentCircle < DRILL_RADII.Count)
                {
                    if (depthReached)
                    {
                        //move to the start position (pistons at 1m/s rotor at 1 rpm)
                        if (drillStationBlocks.ToPosition(drillStationBlocks.VerticalPistons, verticalOffset, 1, drillStationBlocks.HorizontalPiston, DRILL_RADII[currentCircle], 1, drillStationBlocks.Rotor, 0, 1))
                        {
                            depthReached = false;
                        }
                        else
                        {
                            //if not at start, break excution
                            return;
                        }
                    }

                    //unlock rotor
                    BlockUtils.RemoveRotorLimits(drillStationBlocks.Rotor);

                    //set rotor speed
                    BlockUtils.setRotorSpeed(drillStationBlocks.Rotor, ROTOR_RPM);

                    //move the vPistons down until the depth is reached
                    if (BlockUtils.MovePistonsToPosition(drillStationBlocks.VerticalPistons, TARGET_DEPTH, DRILL_DOWN_SPEED))
                    {
                        currentCircle++;
                        verticalOffset = VERTICAL_OFFSET;
                        depthReached = true;
                    }
                }
                else {
                    //move to the start position (pistons at 1m/s rotor at 1 rpm)
                    if (drillStationBlocks.ToPosition(drillStationBlocks.VerticalPistons, 0, 1, drillStationBlocks.HorizontalPiston, 0, 1, drillStationBlocks.Rotor, 0, 1))
                    {
                        //when done proceed to the next state
                        if (END_FLATTENING)
                        {
                            context.State = new FlatteningState(TARGET_DEPTH);
                        }
                        else
                        {
                            context.State = new DoneState();
                        }
                    }
                }
            }

            public string Status(Context context)
            {
                if (currentCircle == DRILL_RADII.Count)
                {
                    return "Drilling DONE";
                }
                else
                {
                    return string.Format("Drilling [{0}/{1}] ({2:0.##}m/{3:0.##}m)", currentCircle + 1, DRILL_RADII.Count, depthReached ? verticalOffset : BlockUtils.GetPistonsTotalPosition((context as DrillStation).DrillStationBlocks.VerticalPistons), TARGET_DEPTH);
                }
            }
        }

        /// <summary>
        /// The DoneState
        /// </summary>
        class DoneState : State
        {
            public StateDTO GetStateDTO(Context context)
            {
                return new StateDTO(typeof(DoneState).Name, -1, -1);
            }

            public void Handle(Context context)
            {
                var drillStationBlocks = (context as DrillStation).DrillStationBlocks;

                //move to the start position (pistons at 1m/s rotor at 1 rpm)
                if (!drillStationBlocks.ToPosition(drillStationBlocks.VerticalPistons, 0, 1, drillStationBlocks.HorizontalPiston, 0, 1, drillStationBlocks.Rotor, 0, 1))
                    return;

                //turn off all panels
                drillStationBlocks.DebugPanels.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_Off").Apply(block);
                });

                //turn off all drills
                drillStationBlocks.Drills.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_Off").Apply(block);
                });

                //turn off all hPistons
                drillStationBlocks.HorizontalPiston.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_Off").Apply(block);
                });

                //turn off the rotor
                drillStationBlocks.Rotor.GetActionWithName("OnOff_Off").Apply(drillStationBlocks.Rotor);

                //turn off all vPistons
                drillStationBlocks.VerticalPistons.ForEach(block =>
                {
                    block.GetActionWithName("OnOff_Off").Apply(block);
                });

                //if the refineries do not have anything left to refine, turn them off
                var allRefineriesDone = true;
                drillStationBlocks.Refineries.ForEach(refinery =>
                {
                    var refineryItems = refinery.GetInventory(0).GetItems();
                    //if there are items in the refinery
                    if (refineryItems.Count > 0)
                    {
                        var stop = true;
                        refineryItems.ForEach(item =>
                        {
                            stop &= item.Amount <= (VRage.MyFixedPoint)0.1f;
                            allRefineriesDone &= stop;
                        });

                        if (stop)
                            refinery.GetActionWithName("OnOff_Off").Apply(refinery);
                    }
                    else
                    {
                        refinery.GetActionWithName("OnOff_Off").Apply(refinery);
                    }
                });

                //if all refineries are done: clean the remaining input and output, stop timer and shutdown script
                if (allRefineriesDone)
                {
                    drillStationBlocks.CleanRefineries(0);
                    drillStationBlocks.CleanRefineries(1);

                    //timer
                    drillStationBlocks.Timer.GetActionWithName("OnOff_Off").Apply(drillStationBlocks.Timer);

                    //scriot
                    drillStationBlocks.ProgrammableBlock.GetActionWithName("OnOff_Off").Apply(drillStationBlocks.ProgrammableBlock);
                }
            }

            public void LoadFromStateDTO(StateDTO stateDTO)
            {
                return;
            }

            public string Status(Context context)
            {
                return "DONE";
            }
        }

        /// <summary>
        /// The 'State' interface
        /// </summary>
        interface State
        {
            /// <summary>
            /// handle the context in order to move to the next state.
            /// </summary>
            /// <param name="context"></param>
            void Handle(Context context);

            /// <summary>
            /// retrieves a friendly status message
            /// </summary>
            /// <param name="context"></param>
            string Status(Context context);

            /// <summary>
            /// gets the stateDTO based on the current variables
            /// </summary>
            /// <param name="context"></param>
            StateDTO GetStateDTO(Context context);

            /// <summary>
            /// sets the state to a point contained in the DTO
            /// </summary>
            /// <param name="stateDTO"></param>
            void LoadFromStateDTO(StateDTO stateDTO);
        }

        class StateDTO
        {
            public string State { get; private set; }
            public int Circle { get; private set; }
            public float Depth { get; private set; }

            public StateDTO(string state, int circle, float depth)
            {
                State = state;
                Circle = circle;
                Depth = depth;
            }

            /// <summary>
            /// builds the DTO info from a string value
            /// </summary>
            /// <param name="persistantStorage"></param>
            public StateDTO(string persistantStorage)
            {
                var keyValues = persistantStorage.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyValue in keyValues)
                {
                    var key = keyValue.Split('=')[0];
                    var value = keyValue.Split('=')[1];

                    switch (key)
                    {
                        case "state":
                            State = value;
                            break;
                        case "circle":
                            Circle = int.Parse(value);
                            break;
                        case "depth":
                            Depth = float.Parse(value);
                            break;
                    }
                }
            }

            /// <summary>
            /// formats the DTO to display as a string, also used in persistant storage
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return string.Format("state={0}\ncircle={1}\ndepth={2}", State, Circle, Depth);
            }

            /// <summary>
            /// returns a build state from the DTO
            /// </summary>
            /// <returns></returns>
            public State BuildState()
            {
                State targetState;

                switch (State)
                {
                    case "FlatteningState":
                        targetState = new FlatteningState(0);
                        break;
                    case "DeepeningState":
                        targetState = new DeepeningState();
                        break;
                    case "DoneState":
                        targetState = new DoneState();
                        break;
                    default:
                        targetState = new InitState();
                        break;
                }

                targetState.LoadFromStateDTO(this);

                return targetState;
            }
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
            public string _storage;

            // Constructor
            public DrillStation(IMyGridTerminalSystem GridTerminalSystem, string storage)
            {
                //init is the default state
                State = new InitState();

                //store the storage
                this._storage = storage;

                //build the station blocks
                this._drillStationBlocks = new DrillStationBlocks(GridTerminalSystem);
            }

            public string PersistantStorage
            {
                get
                {
                    return _storage;
                }
            }

            // Gets the DrillStationBlocks
            public DrillStationBlocks DrillStationBlocks
            {
                get { return _drillStationBlocks; }
            }

            public override void Request()
            {
                base.Request();

                //move the refined goods to the cargo container
                DrillStationBlocks.CleanRefineries(1);

                BlockUtils.SetStatusToAntennas(DrillStationBlocks.Antennas, State.Status(this));
            }
        }

        class DrillStationBlocks
        {
            public IMyMotorAdvancedStator Rotor { get; set; }
            public List<IMyPistonBase> HorizontalPiston { get; set; }
            public List<IMyPistonBase> VerticalPistons { get; set; }
            public List<IMyShipDrill> Drills { get; set; }
            public List<IMyRadioAntenna> Antennas { get; set; }
            public List<IMyTextPanel> DebugPanels { get; set; }
            public List<IMyRefinery> Refineries { get; set; }
            public List<IMyCargoContainer> CargoContainers { get; set; }
            public IMyTimerBlock Timer { get; set; }
            public IMyProgrammableBlock ProgrammableBlock { get; set; }

            public DrillStationBlocks(IMyGridTerminalSystem GridTerminalSystem)
            {
                Rotor = GridTerminalSystem.GetBlockWithName(ROTOR_NAME) as IMyMotorAdvancedStator;
                Timer = GridTerminalSystem.GetBlockWithName(TIMER_NAME) as IMyTimerBlock;
                ProgrammableBlock = GridTerminalSystem.GetBlockWithName(PROGRAMMABLEBLOCK_NAME) as IMyProgrammableBlock;

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
                containerTempList.ForEach(container => CargoContainers.Add(container as IMyCargoContainer));
            }

            public bool ToPosition(List<IMyPistonBase> pistons1, float pistons1position, float speed1, List<IMyPistonBase> pistons2, float pistons2position, float speed2, IMyMotorAdvancedStator rotor, float rotorPosition, float rpm)
            {
                if (!BlockUtils.MovePistonsToPosition(pistons1, pistons1position, speed1))
                {
                    return false;
                }

                if (!BlockUtils.MovePistonsToPosition(pistons2, pistons2position, speed2))
                {
                    return false;
                }

                if (!BlockUtils.MoveRotorToPosition(rotor, rotorPosition, rpm))
                {
                    return false;
                }

                return true;
            }

            public void CleanRefineries(int refineryInventoryIndex)
            {
                foreach (var refinery in Refineries)
                {
                    //get the target inventory, 0 is input, 1 is output, else will probably throw error, CBA to check on this
                    var refineryInventory = refinery.GetInventory(refineryInventoryIndex);
                    var refineryItems = refineryInventory.GetItems();
                    IMyInventory targetInventory = null;

                    //if there are items in the refinery
                    if (refineryItems.Count > 0)
                    {
                        //revers loop as we will remove items
                        for (int i = refineryItems.Count - 1; i >= 0; i--)
                        {
                            //loop the containers to find a target
                            foreach (var cargoContainer in CargoContainers)
                            {
                                //if the container is operational and not full it is a valid target
                                if ((cargoContainer.IsFunctional || cargoContainer.IsWorking) && !cargoContainer.GetInventory(0).IsFull)
                                {
                                    targetInventory = cargoContainer.GetInventory(0);
                                    break;
                                }
                            }

                            // transfer items if target is found
                            if (targetInventory != null)
                                refineryInventory.TransferItemTo(targetInventory, i);
                        }
                    }
                }
            }
        }

        class BlockUtils
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
                    //Echo("The rotor position could not parsed");
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
                    //Echo("[WARN] Setting Rotor limits is doing wierd stuff around or beyond the 360 degree mark, often SE interprets this as infinity");
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
        #endregion
    }
}