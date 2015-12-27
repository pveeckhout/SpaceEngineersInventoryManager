using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersInventoryManager
{
    class Program
    {
        #region programming environment essential inits, DO NOT COPY TO GAME
        IMyGridTerminalSystem GridTerminalSystem;
        #endregion

        #region Script Configuration
        const string CARGO_CONTAINER_CONFIG = "Ore:Ore Container;Ingot:Ingot Container;Component:Component Container;Ammo:Ammo Container;Gun:Ammo Container;";
        const string SCRIPT_TIMER_NAME = "Inventory Manager Timer";
        #endregion

        #region 
        CargoContainerInfo cargoContainerInfo;
        List<IMyTerminalBlock> assemblers;
        List<IMyTerminalBlock> reactors;
        List<IMyTerminalBlock> refineries;
        #endregion

        void Main()
        {
            //init the info
            cargoContainerInfo = new CargoContainerInfo();
            assemblers = new List<IMyTerminalBlock>();
            reactors = new List<IMyTerminalBlock>();
            refineries = new List<IMyTerminalBlock>();

            //build the managed cargo container info
            var itemTypeToContainerNameDict = new Dictionary<string, string>();
            ParseContainerConfig(CARGO_CONTAINER_CONFIG, out itemTypeToContainerNameDict);
            cargoContainerInfo.BuildAcceptDictionary(GridTerminalSystem, itemTypeToContainerNameDict);

            //TODO: build the managed assembler info
            //TODO: build the managed reactor info
            //TODO: build the managed refinery info
        }

        void ParseContainerConfig(string configVal, out Dictionary<string, string> itemTypeToContainerNameDict)
        {
            itemTypeToContainerNameDict = new Dictionary<string, string>();

            var containerConfigs = configVal.Split(new string[] { ";" }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var containerConfig in containerConfigs)
            {
                var configVals = containerConfig.Split(new string[] { ":" }, System.StringSplitOptions.None);
                try {
                    itemTypeToContainerNameDict.Add(configVals[0], configVals[1]);
                }
                catch (IndexOutOfRangeException)
                {
                    //TODO: output error
                }
            }

        }

        class CargoContainerInfo
        {
            public Dictionary<string, List<IMyTerminalBlock>> ItemTypeToContainerListDict { get; private set; }
            
            public CargoContainerInfo()
            {
                this.ItemTypeToContainerListDict = new Dictionary<string, List<IMyTerminalBlock>>();
            }
            
            public void BuildAcceptDictionary(IMyGridTerminalSystem gridTerminalSystem, Dictionary<string, string> itemTypeToContainerNameDict)
            {
                foreach (KeyValuePair<string, string> keyValuePair in itemTypeToContainerNameDict)
                {
                    var containers = new List<IMyTerminalBlock>();
                    gridTerminalSystem.SearchBlocksOfName(keyValuePair.Value, containers, (block => block is IMyCargoContainer && block.IsFunctional));
                    try
                    {
                        ItemTypeToContainerListDict.Add(keyValuePair.Key, containers);
                    }
                    catch (ArgumentNullException)
                    {
                        //TODO: output error 
                    }
                    catch (ArgumentException)
                    {
                        //TODO: output error
                    }
                }  
            }
        }
    }
}
