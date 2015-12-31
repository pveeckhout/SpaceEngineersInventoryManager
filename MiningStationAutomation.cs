using Sandbox.ModAPI.Ingame;
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
        readonly float[] DRILL_RADII = new float[] { 0, 5, 10 };
        //BLOCK SETUP
        const string ROTOR_NAME = "Rotor";
        const string H_PISTON_NAME = "Horizontal Piston";
        const string V_PISTON_NAME = "Vertical Piston";
        #endregion

        private IMyPistonBase HorizontalPiston { get; set; }
        private List<IMyPistonBase> VerticalPistons { get; set; }
        private List<IMyShipDrill> Drills { get; set; }
        private IMyMotorAdvancedStator Rotor { get; set; }

        private bool toStart = false;
        private int currentCircle = 0;
        private bool safetyRounds = false;

        void Main(string argument)
        {
            if (!(HorizontalPiston != null && VerticalPistons != null && Drills != null && Rotor != null))
            {
                Init();
            }

            if (toStart)
            {
                ToStart();
            }
        }

        private void ToStart()
        {
            //TODO: when the rotor is turning, stop it

            //TODO: if the drills ar not on, turn on

            //TODO: while the vPistons are not retracted, retract

            //TODO: when the vPistons are retracted, retract the hPiston

            //when the hPiston is retracted end to start and begin safety rounds
            toStart = false;
            //safetyRounds has been set to true during Init()
            //safetyRounds = true;
        }

        private void Init()
        {
            //move drills to start after init
            toStart = true;
            //initiate the safety circles after init
            safetyRounds = true;
            //reset the current circle index
            currentCircle = 0;

            HorizontalPiston = GridTerminalSystem.GetBlockWithName(H_PISTON_NAME) as IMyPistonBase;
            VerticalPistons = new List<IMyPistonBase>();
            Drills = new List<IMyShipDrill>();
            Rotor = GridTerminalSystem.GetBlockWithName(ROTOR_NAME) as IMyMotorAdvancedStator;


            var tempList = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlockWithName(V_PISTON_NAME);

            foreach (var vPiston in tempList)
            {
                VerticalPistons.Add(vPiston as IMyPistonBase);
            }

            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(tempList);

            foreach (var drill in tempList)
            {
                Drills.Add(drill as IMyShipDrill);
            }
        }
    }

    class derp
    {
        void Main()
        {
            //Create elevator object and define actions for it--------------------------------------------------------------------------------------------
            IMyPistonBase elevator = GridTerminalSystem.GetBlockWithName("Elevator1") as IMyPistonBase;
            var reset_elevator = elevator.GetActionWithName("ResetVelocity");
            var reverse_elevator = elevator.GetActionWithName("Reverse");
            var raise_elevator = elevator.GetActionWithName("IncreaseVelocity");
            var lower_elevator = elevator.GetActionWithName("DecreaseVelocity");
            var IncLowerLimit = elevator.GetActionWithName("IncreaseLowerLimit");
            var DecLowerLimit = elevator.GetActionWithName("DecreaseLowerLimit");
            var DecUpperLimit = elevator.GetActionWithName("DecreaseUpperLimit");
            var IncUpperLimit = elevator.GetActionWithName("IncreaseUpperLimit");
            //-----------------------------------------------------------------------------------------------------------------------------------------------------------


            bool needs_to_raise = false;
            double gotoZ = 7.0; //desired height of piston      


            //Determine if elevator needs to be raised or lowered-------------------------------------------------------------------------------
            if (elevator.MinLimit < gotoZ)
            {
                needs_to_raise = true;
            }//------------------------------------------------------------------------------------------------------------------------------------------------------  

            //Resets the upper and lower limits------------------------------------------------------------------------------------------------------------------
            for (int i = 0; i < 20; i++)
            {
                IncUpperLimit.Apply(elevator);
                DecLowerLimit.Apply(elevator);
            }

            //Set the upper and lower limits of the piston to the desired height--------------------------------------------------------------
            while (elevator.MinLimit < gotoZ)
            {
                IncLowerLimit.Apply(elevator);
            }
            while (elevator.MaxLimit > gotoZ)
            {
                DecUpperLimit.Apply(elevator);
            }//------------------------------------------------------------------------------------------------------------------------------------------------------


            //Raise or lower the elevator as needed-----------------------------------------------------------------------------------------------------
            reset_elevator.Apply(elevator);
            if (needs_to_raise == true)
            {
                reverse_elevator.Apply(elevator);
            }//----------------------------------------------------------------------------------------------------------------------------------------------------------  
        }
    }
}
