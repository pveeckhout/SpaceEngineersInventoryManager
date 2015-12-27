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

        //linking item type names to container names
        readonly Dictionary<string, string> CARGO_CONTAINER_CONFIG = new Dictionary<string, string>()
        {
            { "Ore" , "Ore Container"},
            { "Ingot" , "Ingot Container"},
            { "Component" , "Component Container"},
            { "AmmoMagazines" , "Ammo Container"},
            { "PhysicalGunObject" , "Gun Container"}
        };
        //this container will not be touched by the sorting algoritme
        const string CARGO_CONTAINER_LOCKED = "Locked Container;";
        //multiplier to ensure that there is no gap in the production process.
        const int ASSEMBLER_SOURCE_MULTIPLIER = 3;
        //name of the managed assemblers
        const string ASSEMBLER_MANAGED_NAME = "Managed Assembler";
        //list of components to keep in stock, order of the list will determine the priority
        readonly Dictionary<string, VRage.MyFixedPoint> ASSEMBLER_AUTOASSEMBLY_COMPONENT_LIST = new Dictionary<string, VRage.MyFixedPoint>()
        {
            { "BulletproofGlass", 0 },
            { "Computer", 0 },
            { "Construction", 0 },
            { "Detector", 0 },
            { "Display", 0 },
            { "Explosives", 0 },
            { "Girder", 0 },
            { "GravityGenerator", 0 },
            { "InteriorPlate", 0 },
            { "LargeTube", 0 },
            { "Medical", 0 },
            { "MetalGrid", 0 },
            { "Motor", 0 },
            { "PowerCell", 0 },
            { "RadioCommunication", 0 },
            { "Reactor", 0 },
            { "SmallTube", 0 },
            { "SolarCell", 0 },
            { "SteelPlate", 0 },
            { "Thrust", 0 },
        };
        //list of tools to keep in stock, order of the list will determine the priority
        readonly Dictionary<string, VRage.MyFixedPoint> ASSEMBLER_AUTOASSEMBLY_GUN_LIST = new Dictionary<string, VRage.MyFixedPoint>()
        {
            { "AngleGrinderItem", 0 },
            { "AutomaticRifleItem", 0 },
            { "HandDrillItem", 0 },
            { "WelderItem", 0 }
        };
        //list of ammo to keep in stock, order of the list will determine the priority
        readonly Dictionary<string, VRage.MyFixedPoint> ASSEMBLER_AUTOASSEMBLY_AMMO_LIST = new Dictionary<string, VRage.MyFixedPoint>()
        {
            { "Missile200mm", 0 },
            { "NATO_25x184mm", 0 },
            { "NATO_5p56x45mm", 0 }
        };
        //the name of the managed reactors
        const string REACTOR_MANAGED_NAME = "Managed Reactor";
        //amount of uranium ingot to try to keep in each reactor
        const double REACTOR_MAX_URANIUM = 2500.00;
        //the ore types that can be refined
        readonly string[] REFINERY_PROCESS_ORETYPE = { "cobalt", "gold", "iron", "magnesium", "nickel", "platinum", "silicon", "silver", "stone", "uranium" };
        //the max amount of ore to keep in the refinery
        const double REFINERY_MAX_ORE = 2000.00;
        //the name of the timer that triggers this script
        const string SCRIPT_TIMER_NAME = "Inventory Manager Timer";

        #endregion

        ManagedCargoContainerInfo managedCargoContainerInfo;

        void Main()
        {
            //init the info
            managedCargoContainerInfo = new ManagedCargoContainerInfo();

            //build the managed cargo container info
            managedCargoContainerInfo.BuildAcceptDictionary(GridTerminalSystem, CARGO_CONTAINER_CONFIG);

            //clean the assemblers
            CleanAssemblers(ref managedCargoContainerInfo);

            //sort the items
            SortItems(ref managedCargoContainerInfo);

            //distribute the uranium
            ManageReactorUraniumLevel(ref managedCargoContainerInfo);

            //distribute the ore to the refineries
            ManageRefineryTasks(ref managedCargoContainerInfo);

            //queue up the production
            QueueAssemblers(ref managedCargoContainerInfo);
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

                    //multiply to ensure that there is no gap in the production process.
                    //Increase this should this still happen.
                    maxVal *= ASSEMBLER_SOURCE_MULTIPLIER;

                    //find the container that stores the ingots, is not full and is functional
                    var cargoContainer = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_Ingot"].FirstOrDefault(block => !block.GetInventory(0).IsFull && (block.IsFunctional || block.IsWorking));

                    if (items[i].Amount > (VRage.MyFixedPoint)maxVal && cargoContainer != null)
                        inventory.TransferItemTo(cargoContainer.GetInventory(0), i, null, true, items[i].Amount - (VRage.MyFixedPoint)maxVal);
                }
            }
        }

        void QueueAssemblers(ref ManagedCargoContainerInfo managedCargoContainerInfo)
        {
            var managedAssemblerInfo = new ManagedAssemblerInfo();

            //add all named assemblers to the info
            GridTerminalSystem.SearchBlocksOfName(ASSEMBLER_MANAGED_NAME, managedAssemblerInfo.ManagedAssemblers, block => block is IMyAssembler);

            //check how much of each item we need to make
            foreach (var componentItem in ASSEMBLER_AUTOASSEMBLY_COMPONENT_LIST)
            {
                managedAssemblerInfo.ComponentsToProduce[componentItem.Key] += componentItem.Value;
            }

            foreach (var componentItem in ASSEMBLER_AUTOASSEMBLY_AMMO_LIST)
            {
                managedAssemblerInfo.AmmoToProduce[componentItem.Key] += componentItem.Value;
            }

            foreach (var componentItem in ASSEMBLER_AUTOASSEMBLY_GUN_LIST)
            {
                managedAssemblerInfo.GunsToProduce[componentItem.Key] += componentItem.Value;
            }

            //we can iterate over all items in all component containers to reduce the amount of each item we need to make
            var containers = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_Component"];

            foreach (var componentContainer in containers)
            {
                foreach (var item in componentContainer.GetInventory(0).GetItems())
                {
                    if (item.Content.ToString().Contains("MyObjectBuilder_Component"))
                    {
                        managedAssemblerInfo.ComponentsToProduce[item.Content.SubtypeName.ToLower()] -= item.Amount;
                    }
                }
            }

            //we can iterate over all items in all Ammo containers to reduce the amount of each item we need to make
            containers = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_AmmoMagazines"];

            foreach (var componentContainer in containers)
            {
                foreach (var item in componentContainer.GetInventory(0).GetItems())
                {
                    if (item.Content.ToString().Contains("MyObjectBuilder_AmmoMagazines"))
                    {
                        managedAssemblerInfo.AmmoToProduce[item.Content.SubtypeName.ToLower()] -= item.Amount;
                    }
                }
            }

            //we can iterate over all items in all Gun containers to reduce the amount of each item we need to make
            containers = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_PhysicalGunObject"];

            foreach (var componentContainer in containers)
            {
                foreach (var item in componentContainer.GetInventory(0).GetItems())
                {
                    if (item.Content.ToString().Contains("MyObjectBuilder_PhysicalGunObject"))
                    {
                        managedAssemblerInfo.GunsToProduce[item.Content.SubtypeName.ToLower()] -= item.Amount;
                    }
                }
            }

            //check which assemblers produce each component
            foreach (var assemblyItem in managedAssemblerInfo.ComponentsToProduce)
            {
                var assemblers = managedAssemblerInfo.ManagedAssemblers.Where(block => block.CustomName.Contains("[" + assemblyItem.Key + "]"));

                if (assemblyItem.Value > 0)
                {
                    foreach (var assembler in assemblers)
                    {
                        assembler.GetActionWithName("OnOff_On").Apply(assembler);
                    }
                }
                else
                {
                    foreach (var assembler in assemblers)
                    {
                        assembler.GetActionWithName("OnOff_Off").Apply(assembler);
                    }
                }
            }

            //check which assemblers produce each ammo
            foreach (var assemblyItem in managedAssemblerInfo.AmmoToProduce)
            {
                var assemblers = managedAssemblerInfo.ManagedAssemblers.Where(block => block.CustomName.Contains("[" + assemblyItem.Key + "]"));

                if (assemblyItem.Value > 0)
                {
                    foreach (var assembler in assemblers)
                    {
                        assembler.GetActionWithName("OnOff_On").Apply(assembler);
                    }
                }
                else
                {
                    foreach (var assembler in assemblers)
                    {
                        assembler.GetActionWithName("OnOff_Off").Apply(assembler);
                    }
                }
            }

            //check which assemblers produce each gun 
            foreach (var assemblyItem in managedAssemblerInfo.GunsToProduce)
            {
                var assemblers = managedAssemblerInfo.ManagedAssemblers.Where(block => block.CustomName.Contains("[" + assemblyItem.Key + "]"));

                if (assemblyItem.Value > 0)
                {
                    foreach (var assembler in assemblers)
                    {
                        assembler.GetActionWithName("OnOff_On").Apply(assembler);
                    }
                }
                else
                {
                    foreach (var assembler in assemblers)
                    {
                        assembler.GetActionWithName("OnOff_Off").Apply(assembler);
                    }
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
                var containerName = CARGO_CONTAINER_CONFIG[keyValuePair.Key];

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
                    itemTypeToContainerNameDict.Add("MyObjectBuilder_" + configVals[0].Trim(), configVals[1].Trim());
                }
                catch (IndexOutOfRangeException)
                {
                    //TODO: output error
                }
            }

        }

        #endregion

        #region Reactor Management

        void ManageReactorUraniumLevel(ref ManagedCargoContainerInfo managedCargoContainerInfo)
        {
            //find all managed reactors
            var managedReactors = new List<IMyTerminalBlock>();
            var overstockedReactors = new Dictionary<IMyTerminalBlock, VRage.MyFixedPoint>();
            var understockedReactors = new Dictionary<IMyTerminalBlock, VRage.MyFixedPoint>();
            GridTerminalSystem.SearchBlocksOfName(REACTOR_MANAGED_NAME, managedReactors, block => block is IMyReactor);

            //turn of the conveyor use for the managed reactors
            foreach (var reactor in managedReactors)
            {
                if (reactor.GetUseConveyorSystem())
                    reactor.GetActionWithName("UseConveyor").Apply(reactor);

                //search the managed reactors for reactors that have to much or to litle uranium.
                if (reactor.GetInventory(0).CurrentMass > (VRage.MyFixedPoint)REACTOR_MAX_URANIUM)
                {
                    overstockedReactors.Add(reactor, reactor.GetInventory(0).CurrentMass - (VRage.MyFixedPoint)REACTOR_MAX_URANIUM);
                }
                else
                {
                    understockedReactors.Add(reactor, (VRage.MyFixedPoint)REACTOR_MAX_URANIUM - reactor.GetInventory(0).CurrentMass);
                }
            }

            //move the overstocked ingots to ingot containers
            foreach (var keyValue in overstockedReactors)
            {
                var amount = keyValue.Value;
                var reactor = keyValue.Key;

                //find the container that stores the ingots, is not full and is functional
                var cargoContainer = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_Ingot"].FirstOrDefault(block => !block.GetInventory(0).IsFull && (block.IsFunctional || block.IsWorking));
                var items = reactor.GetInventory(0).GetItems();

                //move the excess ingots to the container
                for (int i = items.Count - 1; i >= 0 && amount > 0; i--)
                {
                    if (items[i].Amount > amount && cargoContainer != null)
                    {
                        cargoContainer.GetInventory(0).TransferItemTo(reactor.GetInventory(0), i, null, true, items[i].Amount - amount);
                    }
                    else
                    {
                        var itemAmount = items[i].Amount;
                        cargoContainer.GetInventory(0).TransferItemTo(reactor.GetInventory(0), i, null, true, amount - items[i].Amount);
                        amount -= itemAmount;
                    }
                }
            }

            if (understockedReactors.Count > 0)
            {
                //find the 'free' uranium ingots in the ingot containers
                var uraniumContainers = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_Ingot"].Where(block => (block.IsFunctional || block.IsWorking) && HasItem(block, "MyObjectBuilder_Ingot", "Uranium"));

                //loop the reactors, move the uranium to the reactor (fill up one by one)
                foreach (var keyValue in understockedReactors)
                {
                    var amount = keyValue.Value;
                    var reactor = keyValue.Key;

                    //find the container that stores the ingots
                    var containers = uraniumContainers.Where(block => (block.IsFunctional || block.IsWorking) && HasItem(block, "MyObjectBuilder_Ingot", "Uranium"));

                    //there is no free uranium
                    if (containers.Count() < 1)
                        break;

                    //loop over the containers to fill up the reactor to the quota.
                    foreach (var container in containers)
                    {
                        var items = container.GetInventory(0).GetItems();

                        //move the excess ingots to the container
                        for (int i = items.Count - 1; i >= 0 && amount > 0; i--)
                        {
                            if (items[i].Amount > amount)
                            {
                                reactor.GetInventory(0).TransferItemTo(container.GetInventory(0), i, null, true, items[i].Amount - amount);
                                //fulfilled the reactor quota
                                break;
                            }
                            else
                            {
                                var itemAmount = items[i].Amount;
                                reactor.GetInventory(0).TransferItemTo(container.GetInventory(0), i, null, true, amount - items[i].Amount);
                                amount -= itemAmount;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Refinery Management

        void ManageRefineryTasks(ref ManagedCargoContainerInfo managedCargoContainerInfo)
        {
            var noItems = new List<string>(REFINERY_PROCESS_ORETYPE);

            var refineries = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineries, block => block.IsFunctional || block.IsWorking);

            var namedRefineries = new List<IMyRefinery>();
            var emptyRefineries = new List<IMyRefinery>();

            var acceptingList = new Dictionary<string, List<IMyRefinery>>();

            foreach (var refinery in refineries)
            {
                if (refinery.CustomName.Contains("+") || refinery.CustomName.Contains("-") || refinery.CustomName.Contains("|"))
                    namedRefineries.Add(refinery as IMyRefinery);
            }

            if (namedRefineries.Count == 0)
                return;


            foreach (var container in managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_Ore"])
            {
                var containerItems = container.GetInventory(0).GetItems();

                // since we aren't moving anyting we can iterate forward
                foreach (var item in containerItems)
                {
                    string itemSubName = item.Content.SubtypeName.ToLower();

                    if (noItems.Contains(itemSubName) && item.Content.ToString().Contains("MyObjectBuilder_Ore"))
                        noItems.Remove(itemSubName);
                }
            }

            foreach (var refinery in namedRefineries)
            {
                var managedRefineryInfo = new ManagedRefineryInfo(refinery);

                var refineryInv = managedRefineryInfo.Refinery.GetInventory(0);
                var refineryItems = refineryInv.GetItems();

                if (managedRefineryInfo.Refinery.UseConveyorSystem)
                    managedRefineryInfo.Refinery.GetActionWithName("UseConveyor").Apply(managedRefineryInfo.Refinery);

                if (IsEmpty(refineryInv))
                    emptyRefineries.Add(managedRefineryInfo.Refinery);
                else
                    managedRefineryInfo.Refinery.GetActionWithName("OnOff_On").Apply(managedRefineryInfo.Refinery);

                string[] splitName = managedRefineryInfo.Refinery.CustomName.Split(' ');

                foreach (var split in splitName)
                {
                    if (split.Contains("+"))
                    {
                        string name = split.Replace("+", "").ToLower();
                        managedRefineryInfo.AcceptList.Add(name);

                    }
                    else if (split.Contains("-"))
                    {
                        string name = split.Replace("-", "").ToLower();
                        managedRefineryInfo.IgnoreList.Add(name);
                    }
                    else if (split.Contains("|"))
                    {
                        string name = split.Replace("|", "").ToLower();
                        managedRefineryInfo.SecondaryList.Add(name);
                    }
                }

                foreach (var missingItem in noItems)
                {
                    managedRefineryInfo.AcceptList.Remove(missingItem);
                }

                foreach (var item in refineryItems)
                {
                    string itemSubName = item.Content.SubtypeName.ToLower();

                    if (managedRefineryInfo.Refinery.CustomName.Contains("+" + itemSubName))
                    {
                        List<IMyRefinery> list;
                        if (!acceptingList.TryGetValue(itemSubName, out list))
                            acceptingList.Add(itemSubName, list = new List<IMyRefinery>());
                        AddUnique(list, managedRefineryInfo.Refinery);
                        managedRefineryInfo.AcceptList.Add(itemSubName);
                    }
                }

                foreach (var acceptedTypeList in managedRefineryInfo.AcceptList)
                {
                    List<IMyRefinery> list;
                    if (!acceptingList.TryGetValue(acceptedTypeList, out list))
                        acceptingList.Add(acceptedTypeList, list = new List<IMyRefinery>());
                    AddUnique(list, managedRefineryInfo.Refinery);
                }

                if (managedRefineryInfo.AcceptList.Count == 0 && managedRefineryInfo.IgnoreList.Count > 0)
                {
                    foreach (var oreType in REFINERY_PROCESS_ORETYPE)
                    {
                        if (managedRefineryInfo.IgnoreList.Contains(oreType))
                            continue;
                        List<IMyRefinery> list;
                        if (!acceptingList.TryGetValue(oreType, out list))
                            acceptingList.Add(oreType, list = new List<IMyRefinery>());
                        AddUnique(list, managedRefineryInfo.Refinery);
                        managedRefineryInfo.AcceptList.Add(oreType);
                    }
                }

                if (managedRefineryInfo.AcceptList.Count == 0)
                {
                    foreach (var secondaryTypeList in managedRefineryInfo.SecondaryList)
                    {
                        List<IMyRefinery> list;
                        if (!acceptingList.TryGetValue(secondaryTypeList, out list))
                            acceptingList.Add(secondaryTypeList, list = new List<IMyRefinery>());
                        AddUnique(list, managedRefineryInfo.Refinery);
                    }
                }

                List<IMyRefinery> allList = new List<IMyRefinery>();
                if (acceptingList.TryGetValue("all", out allList))
                {
                    foreach (var allRefinery in allList)
                    {
                        for (int k = 0; k < REFINERY_PROCESS_ORETYPE.Length; k++)
                        {
                            List<IMyRefinery> list;
                            if (!acceptingList.TryGetValue(REFINERY_PROCESS_ORETYPE[k], out list))
                                acceptingList.Add(REFINERY_PROCESS_ORETYPE[k], list = new List<IMyRefinery>());
                            AddUnique(list, allRefinery);
                        }
                    }
                }

                var containerDestination = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_Ore"].Where(block => (block.IsFunctional || block.IsWorking) && !block.GetInventory(0).IsFull).FirstOrDefault();

                if (containerDestination == null)
                    //TODO: output
                    return;

                var refineryInventory = managedRefineryInfo.Refinery.GetInventory(0);
                refineryItems = refineryInventory.GetItems();

                for (int j = refineryItems.Count - 1; j >= 0; j--)
                {
                    string itemSubName = refineryItems[j].Content.SubtypeName.ToLower();


                    List<IMyRefinery> list;
                    if (acceptingList.TryGetValue(itemSubName, out list))
                    {
                        if (!list.Contains(refinery))
                        {
                            refineryInventory.TransferItemTo(containerDestination.GetInventory(0), j, null, true, null);
                            continue;
                        }
                        else
                        {
                            refineryInventory.TransferItemTo(containerDestination.GetInventory(0), j, null, true, refineryItems[j].Amount - (VRage.MyFixedPoint)REFINERY_MAX_ORE);
                        }
                    }
                    else
                    {
                        refineryInventory.TransferItemTo(containerDestination.GetInventory(0), j, null, true, null);
                        continue;
                    }


                    if (acceptingList.Count == 0)
                    {
                        refineryInventory.TransferItemTo(containerDestination.GetInventory(0), j, null, true, null);
                    }
                }
            }

            //move items
            if (acceptingList.Count == 0)
                return;

            //search all ore contain
            var orecontainers = managedCargoContainerInfo.ItemTypeToContainerListDict["MyObjectBuilder_Ore"].Where(block => (block.IsFunctional || block.IsWorking));

            foreach (var container in orecontainers)
            {
                var containerInv = container.GetInventory(0);
                var containerItems = containerInv.GetItems();

                //moving items, cannot enumerate
                for (int j = containerItems.Count - 1; j >= 0; j--)
                {
                    string itemSubName = containerItems[j].Content.SubtypeName.ToLower();

                    if (acceptingList.ContainsKey(itemSubName) && containerItems[j].Content.ToString().Contains("Ore"))
                    {
                        var amount = (float)containerItems[j].Amount;

                        if (amount < 1)
                        {
                            VRage.MyFixedPoint amountTotal = (VRage.MyFixedPoint)(amount);
                            containerInv.TransferItemTo(acceptingList[itemSubName][0].GetInventory(0), j, null, true, amountTotal);
                            return;
                        }

                        float total = 0;
                        if (acceptingList[itemSubName].Count == 1)
                            total = amount;
                        else
                            total = amount / acceptingList[itemSubName].Count;

                        VRage.MyFixedPoint amountToMove = (VRage.MyFixedPoint)(total);

                        for (int i = 0; i < acceptingList[itemSubName].Count; i++)
                        {
                            containerInv.TransferItemTo(acceptingList[itemSubName][i].GetInventory(0), j, null, true, amountToMove);
                        }
                    }
                }
            }

            //turn of all the empty refineries
            foreach (var refinery in emptyRefineries)
            {
                var emptyRefineryInv = refinery.GetInventory(0);
                if (IsEmpty(emptyRefineryInv))
                    refinery.GetActionWithName("OnOff_Off").Apply(refinery);
            }
        }

        #endregion

        #region Util

        void AddUnique<T>(List<T> list, T value)
        {
            if (!list.Contains(value))
                list.Add(value);
        }

        float getPercent(IMyInventory inv)
        {
            return ((float)inv.CurrentVolume / (float)inv.MaxVolume) * 100f;
        }

        bool IsEmpty(IMyInventory inv)
        {
            if ((float)inv.CurrentVolume > 0)
                return false;
            else
                return true;
        }

        bool HasItem(IMyTerminalBlock terminalBlock, string subTypeId, string typeId)
        {
            if (terminalBlock.IsFunctional == false || terminalBlock.IsWorking == false)
                return false;
            for (int inventoryIndex = 0; inventoryIndex < terminalBlock.GetInventoryCount(); inventoryIndex++)
            {
                var inventory = terminalBlock.GetInventory(inventoryIndex);
                if (GetItemAmmount(inventory, subTypeId, typeId) > 0)
                    return true;

            }
            return false;
        }

        VRage.MyFixedPoint GetItemAmmount(IMyInventory inventory, string subTypeId, string typeId)
        {
            VRage.MyFixedPoint ammount = 0;
            var items = GetItemsOfType(inventory, subTypeId, typeId);
            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var item = items[itemIndex];
                ammount += item.Amount;
            }
            return ammount;
        }

        List<IMyInventoryItem> GetItemsOfType(IMyInventory inventory, string subTypeId, string typeId)
        {
            var matchingItems = new List<IMyInventoryItem>();
            var items = inventory.GetItems();
            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var item = items[itemIndex];
                if (item.Content.SubtypeId.ToString() == subTypeId && item.Content.TypeId.ToString() == typeId)
                    matchingItems.Add(item);
            }
            return matchingItems;
        }

        class ManagedCargoContainerInfo
        {
            public Dictionary<string, List<IMyTerminalBlock>> ItemTypeToContainerListDict { get; private set; }

            public ManagedCargoContainerInfo()
            {
                this.ItemTypeToContainerListDict = new Dictionary<string, List<IMyTerminalBlock>>();
            }

            public void BuildAcceptDictionary(IMyGridTerminalSystem gridTerminalSystem, Dictionary<string, string> itemTypeToContainerNameDict)
            {
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

        class ManagedRefineryInfo
        {
            public IMyRefinery Refinery { get; private set; }
            public List<string> IgnoreList { get; private set; }
            public List<string> AcceptList { get; private set; }
            public List<string> SecondaryList { get; private set; }

            public ManagedRefineryInfo(IMyRefinery refinery)
            {
                Refinery = refinery;
                IgnoreList = new List<string>();
                AcceptList = new List<string>();
                SecondaryList = new List<string>();
            }
        }

        class ManagedAssemblerInfo
        {
            public List<IMyTerminalBlock> ManagedAssemblers { get; private set; }
            public List<IMyTerminalBlock> ComponentAssemblers { get; private set; }
            public List<IMyTerminalBlock> GunsAssemblers { get; private set; }
            public List<IMyTerminalBlock> AmmoAssemblers { get; private set; }

            public Dictionary<string, VRage.MyFixedPoint> ComponentsToProduce { get; private set; }
            //list of tools to keep in stock, order of the list will determine the priority
            public Dictionary<string, VRage.MyFixedPoint> GunsToProduce { get; private set; }
            //list of ammo to keep in stock, order of the list will determine the priority
            public Dictionary<string, VRage.MyFixedPoint> AmmoToProduce { get; private set; }

            public ManagedAssemblerInfo()
            {
                this.ManagedAssemblers = new List<IMyTerminalBlock>();
                this.ComponentAssemblers = new List<IMyTerminalBlock>();
                this.GunsAssemblers = new List<IMyTerminalBlock>();
                this.AmmoAssemblers = new List<IMyTerminalBlock>();

                this.ComponentsToProduce = new Dictionary<string, VRage.MyFixedPoint>()
                {
                    { "BulletproofGlass", 0 },
                    { "Computer", 0 },
                    { "Construction", 0 },
                    { "Detector", 0 },
                    { "Display", 0 },
                    { "Explosives", 0 },
                    { "Girder", 0 },
                    { "GravityGenerator", 0 },
                    { "InteriorPlate", 0 },
                    { "LargeTube", 0 },
                    { "Medical", 0 },
                    { "MetalGrid", 0 },
                    { "Motor", 0 },
                    { "PowerCell", 0 },
                    { "RadioCommunication", 0 },
                    { "Reactor", 0 },
                    { "SmallTube", 0 },
                    { "SolarCell", 0 },
                    { "SteelPlate", 0 },
                    { "Thrust", 0 },
                };

                this.GunsToProduce = new Dictionary<string, VRage.MyFixedPoint>()
                {
                    { "AngleGrinderItem", 0 },
                    { "AutomaticRifleItem", 0 },
                    { "HandDrillItem", 0 },
                    { "WelderItem", 0 }
                };

                this.AmmoToProduce = new Dictionary<string, VRage.MyFixedPoint>()
                {
                    { "Missile200mm", 0 },
                    { "NATO_25x184mm", 0 },
                    { "NATO_5p56x45mm", 0 }
                };
            }
        }

        #endregion
    }
}
