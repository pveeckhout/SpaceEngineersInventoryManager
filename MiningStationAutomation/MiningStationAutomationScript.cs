using Sandbox.ModAPI.Ingame;
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
        readonly List<float> DRILL_RADII = new List<float>() { 0f, 3.5f, 7f, 10f }; //Drills can technically do a 5 wide trench, to be sure nu small floating rocks are left, do smaller intervals.
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
            public override void Handle(DrillStation context)
            {

            }
        }

        /// <summary>
        /// The InitFlatteningState
        /// </summary>
        class InitFlatteningState : State
        {
            public override void Handle(DrillStation context)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The DrillingState
        /// </summary>
        class DrillingState : State
        {
            public override void Handle(DrillStation context)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The EndFlattening State
        /// </summary>
        class EndFlatteningState : State
        {
            public override void Handle(DrillStation context)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The DoneState
        /// </summary>
        class DoneState : State
        {
            public override void Handle(DrillStation context)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The 'State' abstract class
        /// </summary>
        abstract class State
        {
            /*protected IMyMotorAdvancedStator Rotor { get; set; }
            protected List<IMyPistonBase> HorizontalPiston { get; set; }
            protected List<IMyPistonBase> VerticalPistons { get; set; }
            protected List<IMyShipDrill> Drills { get; set; }
            protected List<IMyRadioAntenna> Antennas { get; set; }
            protected List<IMyTextPanel> DebugPanels { get; set; }
            protected List<IMyRefinery> Refineries { get; set; }*/
            protected List<IMyCargoContainer> CargoContainers { get; set; }

            private void Initialize(DrillStation context)
            {
                IMyGridTerminalSystem GridTerminalSystem = context.GridTerminalSystem;

                //VerticalPistons 
                /*VerticalPistons = new List<IMyPistonBase>();
                var hPistonTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(hPistonTempList);
                hPistonTempList.ForEach(vPiston => { if (vPiston.CustomName.Contains(H_PISTON_NAME)) { VerticalPistons.Add(vPiston as IMyPistonBase); } });*/

                //VerticalPistons 
                /*VerticalPistons = new List<IMyPistonBase>();
                var vPistonTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(vPistonTempList);
                vPistonTempList.ForEach(vPiston => { if (vPiston.CustomName.Contains(V_PISTON_NAME)) { VerticalPistons.Add(vPiston as IMyPistonBase); } });*/

                //Drills
                /*Drills = new List<IMyShipDrill>();
                var drillTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drillTempList);
                drillTempList.ForEach(drill => Drills.Add(drill as IMyShipDrill));*/

                //Antennas
                /*Antennas = new List<IMyRadioAntenna>();
                var antennaTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(antennaTempList);
                antennaTempList.ForEach(antenna => Antennas.Add(antenna as IMyRadioAntenna));*/

                //DebugPanels
                /*DebugPanels = new List<IMyTextPanel>();
                var debugPanelTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(debugPanelTempList);
                debugPanelTempList.ForEach(debugPanel => DebugPanels.Add(debugPanel as IMyTextPanel));*/

                //Refineries
                /*Refineries = new List<IMyRefinery>();
                var refineryTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineryTempList);
                refineryTempList.ForEach(refinery => Refineries.Add(refinery as IMyRefinery));*/

                //CargoContainers
                CargoContainers = new List<IMyCargoContainer>();
                var containerTempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containerTempList);
                containerTempList.ForEach(antenna => CargoContainers.Add(antenna as IMyCargoContainer));
            }

            public virtual void Handle(DrillStation context) {
                Initialize(context);
            }
        }

        /// <summary>
        /// The 'Context' class
        /// </summary>
        [Serializable]
        class DrillStation : ISerializable
        {
            private State _state;
            private IMyGridTerminalSystem _gridTerminalSystem;

            // Constructor
            public DrillStation(IMyGridTerminalSystem GridTerminalSystem)
            {
                this._gridTerminalSystem = GridTerminalSystem;

                //get the state from storage
                if (Storage.Contains("StateName"))
                {
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
                                    this.State = new InitFlatteningState();
                                    break;
                                case "DrillingState":
                                    this.State = new DrillingState();
                                    break;
                                case "EndFlatteningState":
                                    this.State = new EndFlatteningState();
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
                    this.State = new InitState();
                }
            }

            // Gets or sets the state
            public State State
            {
                get { return _state; }
                set { _state = value; }
            }

            // Gets the GridTerminalSystem
            public IMyGridTerminalSystem GridTerminalSystem
            {
                get { return _gridTerminalSystem;  }
                set { _gridTerminalSystem = value; }
            }

            public void Request()
            {
                _state.Handle(this);
            }

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("StateType", State.GetType().Name);
            }
        }
        #endregion
    }
}
