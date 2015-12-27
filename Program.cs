using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
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
        const string CARGO_CONTAINER_LOCKED = "Locked Container;";
        const string SCRIPT_TIMER_NAME = "Inventory Manager Timer";
        #endregion

        #region 
        ManagedCargoContainerInfo managedCargoContainerInfo;
        List<IMyTerminalBlock> managedAssemblers;
        List<IMyTerminalBlock> managedReactors;
        List<IMyTerminalBlock> managedRefineries;
        #endregion

        void Main()
        {
            //init the info
            managedCargoContainerInfo = new ManagedCargoContainerInfo();
            managedAssemblers = new List<IMyTerminalBlock>();
            managedReactors = new List<IMyTerminalBlock>();
            managedRefineries = new List<IMyTerminalBlock>();

            //build the managed cargo container info
            var itemTypeToContainerNameDict = new Dictionary<string, string>();
            ParseContainerConfig(CARGO_CONTAINER_CONFIG, out itemTypeToContainerNameDict);
            managedCargoContainerInfo.BuildAcceptDictionary(GridTerminalSystem, itemTypeToContainerNameDict);

            //TODO: build the managed assembler info
            //TODO: build the managed reactor info
            //TODO: build the managed refinery info

            //clean the assemblers
            CleanAssemblers(ref managedCargoContainerInfo);

            //sort the items
            SortItems(ref managedCargoContainerInfo);


        }

        #region Assembly Management

        void CleanAssemblers(ref ManagedCargoContainerInfo managedCargoContainerInfo)
        {
            var assemblers = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblers);
            if (assemblers == null)
                return;

            // check assemblers for clogging
            for (var index = 0; index < assemblers.Count; index++)
            {
                if (assemblers[index] == null) continue;
                var inventory = assemblers[index].GetInventory(0);
                var items = inventory.GetItems();

                //each source type should only be found once
                bool stoneFound, ironFound, nickelFound, cobaltFound, siliconFound, magnesiumFound, silverFound, goldFound, platinumFound, uraniumFound;
                stoneFound = ironFound = nickelFound = cobaltFound = siliconFound = magnesiumFound = silverFound = goldFound = platinumFound = uraniumFound = false;
                var i = -1;
                while (inventory.IsItemAt(++i))
                { // set MaxAmount based on what it is.
                    var maxVal = 0.00;

                    if (items[i].Content.SubtypeName == "Stone" && !stoneFound)
                    {
                        maxVal = 10.00; stoneFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Iron" && !ironFound)
                    {
                        maxVal = 600.00; ironFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Nickel" && !nickelFound)
                    {
                        maxVal = 70.00; nickelFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Cobalt" && !cobaltFound)
                    {
                        maxVal = 220.00; cobaltFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Silicon" && !siliconFound)
                    {
                        maxVal = 15.00; siliconFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Magnesium" && !magnesiumFound)
                    {
                        maxVal = 5.20; magnesiumFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Silver" && !silverFound)
                    {
                        maxVal = 10.00; silverFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Gold" && !goldFound)
                    {
                        maxVal = 5.00; goldFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Platinum" && !platinumFound)
                    {
                        maxVal = 0.40; platinumFound = true;
                    }
                    if (items[i].Content.SubtypeName == "Uranium" && !uraniumFound)
                    {
                        maxVal = 0.50; uraniumFound = true;
                    }
                    
                    var CargoContainer = managedCargoContainerInfo.ItemTypeToContainerListDict["ore"].FirstOrDefault();

                    if (items[i].Amount > (VRage.MyFixedPoint)maxVal && CargoContainer != null)
                        inventory.TransferItemTo(CargoContainer.GetInventory(0), i, null, true, items[i].Amount - (VRage.MyFixedPoint)maxVal);
                }
            }
        }

        #endregion

        #region ItemSorting

        void SortItems(ref ManagedCargoContainerInfo managedCargoContainerInfo)
        {
            foreach (var keyValuePair in managedCargoContainerInfo.ItemTypeToContainerListDict)
            {
                var type = keyValuePair.Key;
                var typeContainers = keyValuePair.Value;
                var containerName = managedCargoContainerInfo.ItemTypeToContainerNameDict[keyValuePair.Key];

                // if there are no containers with our specific name, nothing to do so return
                if (typeContainers == null)
                {
                    //TODO: output this info
                    return;
                }

                IMyInventory containerDestination = null;

                // search our named containers until we find an empty one
                for (int n = 0; n < typeContainers.Count; n++)
                {
                    var containerInv = typeContainers[n].GetInventory(0);
                    if (!containerInv.IsFull)
                    {
                        containerDestination = containerInv;
                        break;
                    }
                }

                // if there was no empty container, nothing more to do so return
                if (containerDestination == null)
                {
                    //TODO: output this info
                    return;
                }

                // search all containers
                var containers = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
                for (int i = 0; i < containers.Count; i++)
                {
                    if (containers[i].CustomName.Contains(CARGO_CONTAINER_LOCKED))
                        continue;
                    var containerInv = containers[i].GetInventory(0);
                    var containerItems = containerInv.GetItems();

                    for (int j = containerItems.Count - 1; j >= 0; j--)
                    {
                        if (containerItems[j].Content.ToString().Contains(type) && !containers[i].CustomName.Contains(containerName))
                        {
                            containerInv.TransferItemTo(containerDestination, j, null, true, null);
                        }
                    }

                    // add percentages to names
                    if (containers[i].CustomName.Contains("%") && containers[i].CustomName.Contains(containerName))
                    {
                        // get rid of the % symbol the player added
                        containers[i].SetCustomName(containers[i].CustomName.Replace("%", "").Replace("  ", " "));
                        // get the container's name without the precentage
                        string[] delim = { " - " };
                        string[] fullName = containers[i].CustomName.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                        string name = fullName[0];
                        string percentage = " - " + getPercent(containerInv).ToString("0.##") + "%";
                        containers[i].SetCustomName((name + percentage).Replace("  ", " "));
                    }
                }

                // search all refineries
                var refineries = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineries);
                for (int i = 0; i < refineries.Count; i++)
                {

                    var refineryInventory = refineries[i].GetInventory(1);
                    var refineryItems = refineryInventory.GetItems();

                    for (int j = refineryItems.Count - 1; j >= 0; j--)
                    {
                        if (refineryItems[j].Content.ToString().Contains(type))
                        {
                            refineryInventory.TransferItemTo(containerDestination, j, null, true, null);
                        }
                    }
                }

                // search all assemblers
                var assemblers = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblers);
                for (int i = 0; i < assemblers.Count; i++)
                {

                    var assemblerInventory = assemblers[i].GetInventory(1);
                    var assemblerItems = assemblerInventory.GetItems();

                    for (int j = assemblerItems.Count - 1; j >= 0; j--)
                    {

                        if (assemblerItems[j].Content.ToString().Contains(type))
                        {
                            assemblerInventory.TransferItemTo(containerDestination, j, null, true, null);
                        }
                    }
                }

                // search all connectors
                var connectors = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors);
                for (int i = 0; i < connectors.Count; i++)
                {

                    var connectorInv = connectors[i].GetInventory(0);
                    var connectorItems = connectorInv.GetItems();

                    for (int j = connectorItems.Count - 1; j >= 0; j--)
                    {
                        if (connectorItems[j].Content.ToString().Contains(type))
                        {
                            connectorInv.TransferItemTo(containerDestination, j, null, true, null);
                        }
                    }
                }
            }
        }

        void ParseContainerConfig(string configVal, out Dictionary<string, string> itemTypeToContainerNameDict)


        {
            itemTypeToContainerNameDict = new Dictionary<string, string>();

            var containerConfigs = configVal.Split(new string[] { ";" }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var containerConfig in containerConfigs)
            {
                var configVals = containerConfig.Trim().Split(new string[] { ":" }, System.StringSplitOptions.None);
                try
                {
                    itemTypeToContainerNameDict.Add(configVals[0].Trim(), configVals[1].Trim());
                }
                catch (IndexOutOfRangeException)
                {
                    //TODO: output error
                }
            }

        }

        #endregion

        #region Util

        float getPercent(IMyInventory inv)
        {
            return ((float)inv.CurrentVolume / (float)inv.MaxVolume) * 100f;
        }

        class ManagedCargoContainerInfo
        {
            public Dictionary<string, List<IMyTerminalBlock>> ItemTypeToContainerListDict { get; private set; }
            public Dictionary<string, string> ItemTypeToContainerNameDict { get; private set; }

            public ManagedCargoContainerInfo()
            {
                this.ItemTypeToContainerListDict = new Dictionary<string, List<IMyTerminalBlock>>();
            }

            public void BuildAcceptDictionary(IMyGridTerminalSystem gridTerminalSystem, Dictionary<string, string> itemTypeToContainerNameDict)
            {
                //save the dict locally
                this.ItemTypeToContainerNameDict = itemTypeToContainerNameDict;

                foreach (var keyValuePair in itemTypeToContainerNameDict)
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

        #endregion
    }
}
