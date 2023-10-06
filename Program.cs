using EmptyKeys.UserInterface.Generated.DataTemplatesContractsDataGrid_Bindings;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Lights;
using Sandbox.Game.WorldEnvironment.Modules;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using System.Xml.Linq;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
using VRageRender.Messages;


namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /// <summary>
        /// RIH - RECKLESS INVENTORY HANDLER
        /// By "RECKLESS"
        /// CURRENT VERSION = V1.1.2
        /// </summary>

        const string version = "V: 1.1.2";
        bool isStation;
        const int timeSpan = 1;
        
        int yieldTime;
        readonly MyIni _ini = new MyIni();
        readonly float fontsize = 0.5f; // font of lcd panel

        bool setupCompleted;

        const string TagDefault = "[RIH]";
        string TagCustom;

        const bool ExpanseServerDefault = true;
        bool ExpanseServerCustom;

        const string BaseContainersDefault = "BaseCargo";
        string BaseContainersCustom;
        const string ShipContainersDefault = "ShipCargo";
        string ShipContainersCustom;
        const bool readCargoDefault = true;
        bool readCargoCustom;
        const int defaultFuel = 25;
        int customFuel;
        const int timeMultDefault = 1;
        int timeMultCustom = 1;
        const bool specialCargoUsageDefault = true;
        bool specialCargoUsageCustom;
        const string specialContainersTagDefault = "Special";
        string specialContainersTagCustom;

        readonly List<IMyTextPanel> LCDList = new List<IMyTextPanel>();
        readonly List<IMyCargoContainer> shipContainers = new List<IMyCargoContainer>();
        readonly List<IMyReactor> reactors = new List<IMyReactor>();
        readonly List<IMyCargoContainer> allCargo = new List<IMyCargoContainer>();
        readonly List<IMyCargoContainer> specialCargo = new List<IMyCargoContainer> ();
        readonly List<IMyShipConnector> specialConnector = new List<IMyShipConnector> ();
        readonly StringBuilder missingItems = new StringBuilder();
        readonly StringBuilder missingCargo = new StringBuilder();

        IMyTextPanel LCDLog;
        IMyTextPanel LCDInv;
        bool LCDLogBool;
        bool LCDInvBool;
        const string defaultHUDLCD = "hudlcd:-.7:.99:0.55";
        const string defaultHUDLCDInv = "hudlcd:-.5:.99:0.55";
        Color lcd_font_colour = new Color(30, 144, 255, 255);
        
        //HUD STUFF
        const string lcd_divider =       "------------------------------";
        const string lcd_title =         "  RECKLESS INVENTORY HANDLER  ";
        const string lcd_inv_title =     "       RIH INVENTORY LCD      ";
        readonly Dictionary<IMyCargoContainer, Dictionary<MyDefinitionId, int>> itemDict = new Dictionary<IMyCargoContainer, Dictionary<MyDefinitionId, int>>();

        //ores
        MyDefinitionId OreId = MyDefinitionId.Parse("Ore/");

        //init Timer State machine
        readonly SimpleTimerSM timerSM;
        public Program()
        {
            
            CustomData();
            Setup();
            
            if (setupCompleted)
            {
                Echo($"      SETUP COMPLETED:\n" +
                    $"Tag used: [{TagCustom}]\n" +
                    $"Expanse Server = {ExpanseServerCustom}\n" +
                    $"Found {shipContainers.Count} ship cargos\n" +
                    $"LCD.Log found {LCDLogBool}\n" +
                    $"LCD.Inv found {LCDInvBool}\n" +
                    $"[List of commands:\nstart: start auto pull\n" +
                    $"stop: suppress script and stealth drive\n" +
                    $"reload: load the cargo with their custom data\n" +
                    $"unload: unload all comps\nrefresh: re-read cargos' CD\n" +
                    $"read&write: read tagged items and write in the CD\n" +
                    $"toggle: toggle all blcoks but Epsteins, tools, proj]");

                yieldTime = timeSpan * timeMultCustom;
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                timerSM = new SimpleTimerSM(this, Sequence());
                
            }
        }
        public void CustomData()
        {
            /// Custom Data
            bool wasparsed = _ini.TryParse(Me.CustomData);
            TagCustom = _ini.Get("data", "TAG").ToString(TagDefault);
            ExpanseServerCustom = _ini.Get("data", "ExpanseServer").ToBoolean(ExpanseServerDefault);
            customFuel = _ini.Get("data", "FuelAmount").ToInt16(defaultFuel);
            readCargoCustom = _ini.Get("data", "SetCargo").ToBoolean(readCargoDefault);
            BaseContainersCustom = _ini.Get("data", "BaseContainersGroup").ToString(BaseContainersDefault);
            ShipContainersCustom = _ini.Get("data", "ShipContainersGroup").ToString(ShipContainersDefault);
            specialCargoUsageCustom = _ini.Get("data", "SpecialCargoUsage").ToBoolean(specialCargoUsageDefault);
            specialContainersTagCustom = _ini.Get("data", "SpecialContainersTag").ToString(specialContainersTagDefault);
            timeMultCustom = _ini.Get("data", "Extra100Ticks").ToInt32(timeMultDefault);

            if (!wasparsed)
            {
                _ini.Clear();
            }
            _ini.Set("data", "TAG", TagCustom);
            _ini.Set("data", "ExpanseServer", ExpanseServerCustom);
            _ini.Set("data", "FuelAmount", customFuel);
            _ini.Set("data", "SetCargo", readCargoCustom);
            _ini.Set("data", "BaseContainersGroup", BaseContainersCustom);
            _ini.Set("data", "ShipContainersGroup", ShipContainersCustom);
            _ini.Set("data", "SpecialCargoUsage", specialCargoUsageCustom);
            _ini.Set("data", "SpecialContainersTag", specialContainersTagCustom);
            _ini.Set("data", "Extra100Ticks", timeMultCustom);

            Me.CustomData = _ini.ToString();
        }
        public void Main(string argument, UpdateType updateType)
        {
            if ((updateType & (UpdateType.Terminal)) != 0)
            {
                switch (argument.ToLower())
                {
                    case "start":
                        timerSM.Start();
                        Runtime.UpdateFrequency = UpdateFrequency.Update100;
                        break;

                    case "stop":
                        Runtime.UpdateFrequency = UpdateFrequency.None;
                        timerSM.Start();
                        TextWriting(LCDLog, LCDLogBool, "Stopping Script", false);
                        break;

                    case "refresh":
                        SetUpCargo(shipContainers, ShipContainersCustom);
                        Echo("Reading Cargos' CD");
                        TextWriting(LCDLog, LCDLogBool, $"Reading Cargos' CD\n", false);
                        break;

                    case "read&write":
                        timerSM.Stop();
                        TextWriting(LCDLog, LCDLogBool, "Reading tagged Cargos' CD\n", false);
                        ReadAndWrite();
                        break;

                    case "reload":
                        timerSM.Stop();
                        if (ShipConnectedToBase() && readCargoCustom)
                        {
                            List<IMyCargoContainer> baseContainers = GetCargoContainerBase(BaseContainersCustom);
                            if (baseContainers != null)
                            {
                                try
                                {
                                    //Echo("Ship Connected \nBase Cargo found");
                                    Load(baseContainers, shipContainers, itemDict);
                                    //Echo("Load Finished");
                                    //TextWriting(LCD, LCDBool,"Load Finished", false);
                                }
                                catch { 
                                    //Echo("Error Occurred"); 
                                    //TextWriting(LCD, LCDBool, $"{lcd_divider}\n{lcd_title}\n{lcd_divider}\nError Occurred\n", false);
                                }
                            }

                            else
                            {
                                Echo("No Base Containers, change the Base's Cargos Group Name as BaseCargo");
                                TextWriting(LCDLog, LCDLogBool,"No Base Containers, change the Base's Cargos Group Name as BaseCargo", false);
                            }
                        }
                        else
                        {
                            Echo("Ship is not Connected, or cargo has not been set. Conncect or change SetCargo to true in CD");
                            TextWriting(LCDLog, LCDLogBool, $"{lcd_divider}\n{lcd_title}\n{lcd_divider}\nShip is not Connected, or cargo has not been set. Conncect or change SetCargo to true in CD", false);
                        }
                        Runtime.UpdateFrequency |= UpdateFrequency.None;
                        break;

                    case "unload":
                        if (ShipConnectedToBase() && readCargoCustom)
                        {
                            List<IMyCargoContainer> baseContainers = GetCargoContainerBase(BaseContainersCustom);
                            if (baseContainers != null)
                            {
                                try
                                {
                                    //Echo("Ship Connected \nBase Cargo found");
                                    Unload(shipContainers, baseContainers);
                                    Echo("Unload Finished");
                                    TextWriting(LCDLog, LCDLogBool,$"{ lcd_divider}\n{ lcd_title}\n{ lcd_divider}\nUnload Finished", false);
                                }
                                catch { Echo("Error Occurred"); TextWriting(LCDLog, LCDLogBool,"Error Occurred", false); }
                                
                            }
                            else
                            {
                                Echo("No Base Containers, change the Base's Cargos Group Name as BaseCargo");
                                TextWriting(LCDLog, LCDLogBool, $"{lcd_divider}\n{lcd_title}\n{lcd_divider}\nNo Base Containers, change the Base's Cargos Group Name as BaseCargo", false);
                            }
                        }
                        else
                        {
                            Echo("Ship is not Connected, or cargo has not been set. Conncect or change SetCargo to true in CD");
                            TextWriting(LCDLog, LCDLogBool, $"{lcd_divider}\n{lcd_title}\n{lcd_divider}\nShip is not Connected, or cargo has not been set. Conncect or change SetCargo to true in CD", false);
                        }
                        Runtime.UpdateFrequency |= UpdateFrequency.None;
                        break;

                    case "toggle":
                        timerSM.Stop();
                        Runtime.UpdateFrequency |= UpdateFrequency.None;
                        ToggleOn();
                        Echo("Toggling On everything except for Epsteins and tools");
                        TextWriting(LCDLog, LCDLogBool, $"{lcd_divider}\n{lcd_title}\n{lcd_divider}\nToggling On everything except for Epsteins\nand tools", false);
                        break;

                    default:
                        Echo("Command not valid");
                        TextWriting(LCDLog, LCDLogBool, "Command not valid", false);
                        break;
                }
            }
            else
            {
                timerSM.Run();
                
            }
        }
        public void Setup()
        {
            if(Me.CubeGrid.IsStatic) { isStation = true; }
            //LCD
            GridTerminalSystem.GetBlocksOfType(LCDList, x => x.CustomName.Contains(TagCustom +".Log"));
            if (LCDList == null && (LCDList.Count==0 || LCDList.Count>1))
            {
                Echo($"No LCD found or more than 1 LCD found \nUse [{TagDefault}] tag, or change it in Custom Data");
                LCDLogBool = false;
            }
            
            if (LCDList != null && LCDList.Count == 1)
            {
                LCDLogBool = true;
                LCDLog = LCDList[0];
                LCDLog.FontSize = fontsize;
                LCDLog.Font = "Monospace";
                LCDLog.FontColor = lcd_font_colour;
                LCDLog.ContentType = ContentType.TEXT_AND_IMAGE;
                TextWriting(LCDLog, LCDLogBool, "", false);
                if (!LCDLog.CustomData.Contains("hudlcd"))
                {
                    LCDLog.CustomData += defaultHUDLCD;
                }
            }
            //inventory LCD
            LCDList.Clear();
            GridTerminalSystem.GetBlocksOfType(LCDList, x=>x.CustomName.Contains(TagCustom+".Inventory"));
            if(LCDList != null && LCDList.Count == 1)
            {
                LCDInv = LCDList[0];
                LCDInvBool = true;
                LCDInv.FontSize = fontsize;
                LCDInv.Font = "Monospace";
                LCDInv.FontColor = lcd_font_colour;
                LCDInv.ContentType = ContentType.TEXT_AND_IMAGE;

                if (!LCDInv.CustomData.Contains("hudlcd"))
                {
                    LCDInv.CustomData += defaultHUDLCDInv;
                }
            }
            //cargo set up
            SetUpCargo(shipContainers, ShipContainersCustom);

            foreach (var container in shipContainers)
            {
                //Echo($"{container.CustomName}");
                CustomDataCargo(container);
            }
            //all reactors
            if (ExpanseServerCustom)
            {
                GridTerminalSystem.GetBlocksOfType(reactors);
                foreach (var r in reactors)
                {
                    r.UseConveyorSystem = false;
                    //Echo($"reactors: {r.CustomName}\n{r.Name}\n\n");
                }
            }
            //all containers, non special
            GridTerminalSystem.GetBlocksOfType(allCargo, x => !x.CustomName.Contains(specialContainersTagCustom));
            //finish setup
            setupCompleted = true;
        }

        public void CustomDataCargo(IMyCargoContainer container)
        {
            const int defaultMultiplier = 1;
            int customMultiplier;
            bool cargoWasparsed = _ini.TryParse(container.CustomData);
            customMultiplier = _ini.Get("Change this value to multiply all comps", "Multiplier").ToInt32(defaultMultiplier);
            if (!cargoWasparsed)
            {
                _ini.Clear();
            }
            _ini.Set("Change this value to multiply all comps", "Multiplier", customMultiplier);
            _ini.AddSection("Cargo");
            
            container.CustomData = _ini.ToString();
        }

        public bool ShipConnectedToBase()
        {
            List<IMyShipConnector> connectors = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(connectors);

            foreach (IMyShipConnector connector in connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected && connector.OtherConnector != null && !connector.Closed)
                    if (connector.OtherConnector.CubeGrid == Me.CubeGrid)
                    {
                        return true;
                    }
            }
            return false;
        }

        public void Unload(List<IMyCargoContainer> sourceContainers, List<IMyCargoContainer> baseContainers)
        {
            foreach (IMyCargoContainer destinationContainer in baseContainers)
            {
                IMyInventory destinationInventory = destinationContainer.GetInventory();
                foreach (var sourceContainer in sourceContainers)
                {
                    IMyInventory sourceInventory = sourceContainer.GetInventory();
                    var ShipItemsList = ItemToList(sourceContainer);
                    foreach (var item in ShipItemsList)
                    {
                        sourceInventory.TransferItemTo(destinationInventory, item, item.Amount);
                    }

                }
                List<IMyShipConnector> sourceConnectors = new List<IMyShipConnector>();
                GridTerminalSystem.GetBlocksOfType(sourceConnectors);
                foreach (var connector in sourceConnectors)
                {
                    IMyInventory sourceConnectorInventory = connector.GetInventory();
                    List<MyInventoryItem> ConnectorShipItemsList = new List<MyInventoryItem>();
                    destinationContainer.GetInventory().GetItems(ConnectorShipItemsList);
                    foreach (var connectorItem in ConnectorShipItemsList)
                    {
                        sourceConnectorInventory.TransferItemTo(destinationInventory, connectorItem, connectorItem.Amount);
                    }
                }
            }
        }

        public void Load(List<IMyCargoContainer> sourceContainers, List<IMyCargoContainer> destinationContainers, Dictionary<IMyCargoContainer, Dictionary<MyDefinitionId, int>> itemDict)
        {
            TextWriting(LCDLog, LCDLogBool, "", false);
            //Echo($"We're loading up\nItems{itemDict.Count}");
            foreach (IMyCargoContainer destinationContainer in destinationContainers)
            {
                IMyInventory destinationInventory = destinationContainer.GetInventory();
                foreach (var sourceContainer in sourceContainers)
                {
                    IMyInventory sourceInventory = sourceContainer.GetInventory();
                    foreach (var container in itemDict.Keys)
                    {
                        //Echo($"container: {container}");
                        Dictionary<MyDefinitionId, int> containerItems = itemDict[container]; //item's name in custom data container
                        List<MyInventoryItem> ShipItemsList = ItemToList(destinationContainer);
                        foreach (KeyValuePair<MyDefinitionId, int> itemEntry in containerItems)
                        {
                            
                            MyDefinitionId itemId = itemEntry.Key;
                            int quantity = itemEntry.Value;
                            int startingListCount = ShipItemsList.Count;
                            MyInventoryItem? itemSource = sourceInventory.FindItem(itemId);
                            if (itemSource != null && container == destinationContainer)
                            {
                                if (ShipItemsList.Count == 0)
                                {
                                     sourceInventory.TransferItemTo(destinationInventory, (MyInventoryItem)itemSource, quantity);
                                    
                                    //Echo($"ItemLoaded: {itemId} = {quantity}");
                                }
                                //Echo("itemsource!=null");
                                if (ShipItemsList.Count > 0)
                                {
                                    //Echo("for loop");
                                    for (int i = ShipItemsList.Count - 1; i >= 0; i--)
                                    {
                                        //Echo($"item of list {ShipItemsList[i].Type.SubtypeId};\n" +
                                        //    $"item number: {ShipItemsList[i].Amount}\n" +
                                        //    $" dict key: {quantity}");
                                        if (itemId == (MyDefinitionId)ShipItemsList[i].Type)
                                        {
                                            //Echo("loading");
                                            MyFixedPoint itemCount = ShipItemsList[i].Amount;
                                            MyFixedPoint zero = 0;
                                            MyFixedPoint amountToPass = MyFixedPoint.Max(quantity - itemCount, zero);
                                            sourceInventory.TransferItemTo(destinationInventory, (MyInventoryItem)itemSource, amountToPass);
                                            //Echo($"final list {ShipItemsList.Count}");
                                            //Echo($"ItemLoaded: {ShipItemsList[i].Type.SubtypeId} = {amountToPass}");
                                            ShipItemsList.RemoveAt(i);
                                        }
                                        int finalListCount = ShipItemsList.Count;
                                        if (finalListCount == startingListCount && i == 0)
                                        {
                                            sourceInventory.TransferItemTo(destinationInventory, (MyInventoryItem)itemSource, quantity);
                                            //Echo($"ItemLoaded: {itemId} = {quantity}");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                }
            }
            Echo("Reload COMPLETED!\n");

            foreach (var container in destinationContainers)
            {
                //int customMultiplier;
                //customMultiplier = _ini.Get("Change this value to multiply all comps below", "Multiplier").ToInt32();
                foreach (var dictContainer in itemDict.Keys)
                {

                    if (container == dictContainer)
                    {
                        //Echo("missing items dict creation");
                        Dictionary<MyDefinitionId, MyFixedPoint> nestedDictionary = new Dictionary<MyDefinitionId, MyFixedPoint>();
                        //Echo("custom data dict per container");
                        Dictionary<MyDefinitionId, int> containerItems = itemDict[dictContainer]; //item's name in custom data container
                        //Echo("list creation");
                        List<MyInventoryItem> missingItemsList = ItemToList(container);
                        //Echo("finish");
                        foreach (KeyValuePair<MyDefinitionId, int> itemEntry in containerItems)
                        {
                            int startingListCount = missingItemsList.Count;
                            //Echo($"{startingListCount}");
                            MyDefinitionId itemId = itemEntry.Key;
                            MyFixedPoint quantity = itemEntry.Value;

                            if (missingItemsList.Count == 0)
                            {
                                nestedDictionary.Add(itemId, quantity);
                                //Echo($"Missing Items: Cargo:{container}; {nestedDictionary.Keys}={nestedDictionary.Values}");
                            }

                            if (missingItemsList.Count > 0)
                            {
                                for (int i = missingItemsList.Count - 1; i >= 0; i--)
                                {
                                    //Echo($"item of list {ShipItemsList[i].Type.SubtypeId};\n" +
                                    //    $"item number: {ShipItemsList[i].Amount}\n" +
                                    //    $" dict key: {quantity}");
                                    if (itemId == (MyDefinitionId)missingItemsList[i].Type)
                                    {
                                        MyFixedPoint itemCount = missingItemsList[i].Amount;
                                        MyFixedPoint missingQuantity = MyFixedPoint.Max(quantity - itemCount, 0);
                                        if (missingQuantity != 0)
                                        {
                                            nestedDictionary.Add(itemId, missingQuantity);
                                        }
                                        missingItemsList.RemoveAt(i);
                                    }
                                    int finalListCount = missingItemsList.Count;
                                    if (finalListCount == startingListCount && i == 0)
                                    {
                                        nestedDictionary.Add(itemId, MyFixedPoint.Max(quantity, 0));
                                        //Echo($"itemDict: {itemEntry}; dictamount{quantity}; listQuant {itemCount};missquant {missingQuantity}; itemlist{missingItemsList[i].Type}");
                                    }
                                }
                            }
                        }
                        int length;
                        int lengthAmount;
                        int maxLength = 0;
                        int maxLengthAmount = 0;
                        //HUD STUFF
                        const string lcd_divider_short =  "-";
                        foreach (var kv in nestedDictionary)
                        {
                            length = kv.Key.SubtypeName.Length + 7;
                            lengthAmount = kv.Value.ToString().Length;
                            if (length > maxLength) { maxLength = length +1; }
                            if(lengthAmount>maxLengthAmount) { maxLengthAmount = lengthAmount +1; }

                        }
                        foreach (var kv in nestedDictionary)
                        {
                            if (kv.Value == 0) { continue; }
                            else { missingItems.Append($"Items: {kv.Key.SubtypeName.PadRight(maxLength)}= {MyFixedPoint.Max(kv.Value, 0), 3}\r\n"); }
                        }
                        if(missingItems.Length>0)
                        {
                            missingCargo.Append($"{string.Concat(Enumerable.Repeat(lcd_divider_short, maxLength + 2 * maxLengthAmount + 2))}\n" +
                            $"Missing Items from Cargo = {container.CustomName}\r\n" +
                            $"{string.Concat(Enumerable.Repeat(lcd_divider_short, maxLength + 2 * maxLengthAmount + 2))}\n" +
                            $"{missingItems}");
                            missingItems.Clear();
                        }
                        else { missingCargo.Append($"");}
                    }
                }
            }
            Echo(missingCargo.ToString());
            TextWriting(LCDLog, LCDLogBool, $"{missingCargo}", true);
            missingItems.Clear();
            missingCargo.Clear();
            //Echo($"{nestedDictionary.Count}");
        }
  
        public List<MyInventoryItem> ItemToList(IMyCargoContainer destinationContainer)
        {
            //Echo("Enter the list");
            List<MyInventoryItem> ShipItemsList = new List<MyInventoryItem>();
            destinationContainer.GetInventory().GetItems(ShipItemsList);
            //Echo($"list is {ShipItemsList.Count}");
            return ShipItemsList;
        }

        public void SetUpCargo(List<IMyCargoContainer> shipContainers, string ShipContainersCustom)
        { //parsing containers
            if (readCargoCustom)
            {
                //containers
                IMyBlockGroup cargoShipBlock = GridTerminalSystem.GetBlockGroupWithName(ShipContainersCustom);
                if (cargoShipBlock == null)
                {
                    Echo($"Ship Cargo not found");
                    return;
                }
                cargoShipBlock.GetBlocksOfType(shipContainers);
                Parsing(shipContainers, itemDict);
            }
        }

        public List<IMyCargoContainer> GetCargoContainerBase(string BaseContainersCustom)
        {
            List<IMyCargoContainer> baseContainers = new List<IMyCargoContainer>();
            IMyBlockGroup BaseGroupContiners = GridTerminalSystem.GetBlockGroupWithName(BaseContainersCustom);

            if (BaseGroupContiners == null)
            {
                return null;
            }
            BaseGroupContiners.GetBlocksOfType(baseContainers);
            return baseContainers;
        }

        public void Parsing(List<IMyCargoContainer> shipContainers, Dictionary<IMyCargoContainer, Dictionary<MyDefinitionId, int>> itemDict)
        {
            itemDict.Clear();
            foreach (var container in shipContainers)
            {
                
                MyIniParseResult result;
                if (!_ini.TryParse(container.CustomData, out result))
                {
                    Echo("Add [Cargo] in the first line of Container custom data, or check for right formatting");
                    TextWriting(LCDLog, LCDLogBool,"Add [Cargo] in the\nfirst line of Container custom data,\n or check for right formatting", false);
                    throw new Exception(result.ToString());
                }

                else
                {
                    int customMultiplier;
                    customMultiplier = _ini.Get("Change this value to multiply all comps", "Multiplier").ToInt32();
                    //Echo($"{customMultiplier}");
                    List<MyIniKey> keyList = new List<MyIniKey>();
                    _ini.GetKeys(keyList);
                    foreach (var key in keyList)
                    {
                        //Echo($"item: {key}");
                        MyDefinitionId name;
                        int amount;
                        if (MyDefinitionId.TryParse(key.Name, out name))
                        {
                            //Echo($"key.name= {key.Name}");
                            if (_ini.Get(key).TryGetInt32(out amount))
                            {
                                //Echo($"Amount {amount}");
                                var nested = itemDict.GetValueOrDefault(container);
                                if (nested == null)
                                {
                                    nested = new Dictionary<MyDefinitionId, int>();
                                    itemDict[container] = nested;
                                }
                                nested.Add(name, amount*customMultiplier);
                            }
                            else
                            {
                                Echo("Wrong Value in one or more Items");
                                TextWriting(LCDLog, LCDLogBool,"Wrong Value in one or more Items", false);
                            }
                        }
                    }
                }
            }
            //foreach (var kv in itemDict)
            //{
            //    foreach (var kv2 in kv.Value)
            //    {
            //        Echo($"container: {kv.Key}\nitem: {kv2.Key}\namount: {kv2.Value}");
            //    }
            //}
        }
        public void ReadAndWrite()
        {
            List<IMyCargoContainer> taggedCargos = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType(taggedCargos, x => x.CustomName.Contains(TagCustom));
            _ini.Clear();
            if (taggedCargos!=null && taggedCargos.Count> 0)
            {
                foreach(var c in taggedCargos)
                {
                    c.CustomData += _ini.DeleteSection("WrittenItems");
                    List<MyInventoryItem> items = new List<MyInventoryItem>();
                    c.GetInventory().GetItems(items);
                    foreach(var i in items)
                    {
                        CustomDataCargo(c);
                        _ini.DeleteSection("Cargo");
                        string itemName = i.Type.ToString().Replace("MyObjectBuilder_","");
                        MyFixedPoint itemAmount = i.Amount;
                        //Echo($"1: {itemName}\n2:{itemAmount}");
                        TextWriting(LCDLog, LCDLogBool, itemName, false);
                        _ini.Set("WrittenItems", itemName, itemAmount.ToString());
                        c.CustomData = _ini.ToString();
                    }
                    if(_ini.TryParse(c.CustomData))
                    {
                        TextWriting(LCDLog, LCDLogBool, "CD Created", false);
                    }
                    else { TextWriting(LCDLog, LCDLogBool, "Error during the Parse", false ); }
                }
            }
            else { TextWriting(LCDLog, LCDLogBool, "Cargo is empty!", false); }
        }
        public void ToggleOn()
        {
            //turn on all blocks
            List<IMyShipWelder> allWelders = new List<IMyShipWelder>();
            List<IMyShipDrill> allDrills = new List<IMyShipDrill>();
            List<IMyShipGrinder> allGrinder = new List<IMyShipGrinder>();
            List<IMyProjector> allProj = new List<IMyProjector>();
            List<IMyFunctionalBlock> toggleBlocksList = new List<IMyFunctionalBlock>();
            List<IMyThrust> epsteinList = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(toggleBlocksList);
            foreach (var block in toggleBlocksList)
            {
                if (block != null && !block.Enabled)
                {
                    block.Enabled = true;
                }
            }
            try
            {
                GridTerminalSystem.GetBlocksOfType(epsteinList);
                List<MyDefinitionId> thrustersList = new List<MyDefinitionId>()
                {
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_MUNR_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_PNDR_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_QUADRA_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_RAIDER_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_ROCI_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_Leo_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLYNX_SILVERSMITH_Epstein_DRIVE"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_DRUMMER_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_SCIRCOCCO_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_Mega_Epstein_Drive"),
                    //chemical drivers
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/LargeBlockLargeHydrogenThrust"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/LargeBlockSmallHydrogenThrust")

                };
                //Echo($"{epsteinList.Count}Thrust");
                foreach (var driver in epsteinList)
                {
                    foreach (var thruster in thrustersList)
                    {
                        //Echo($"{thruster.BlockDefinition}");
                        if (driver.BlockDefinition == thruster)
                        {
                            driver.Enabled = false;
                        }
                    }
                }
                //turn off tools and proj
                GridTerminalSystem.GetBlocksOfType(allWelders);
                GridTerminalSystem.GetBlocksOfType(allGrinder);
                GridTerminalSystem.GetBlocksOfType(allDrills);
                GridTerminalSystem.GetBlocksOfType(allProj);
                if(allWelders!=null && allWelders.Count>0)
                {
                    foreach (var w in allWelders)
                    {
                        w.Enabled = false;
                    }
                }
                if (allGrinder != null && allGrinder.Count > 0)
                {
                    foreach (var w in allGrinder)
                    {
                        w.Enabled = false;
                    }
                }
                if (allDrills != null && allDrills.Count > 0)
                {
                    foreach (var w in allDrills)
                    {
                        w.Enabled = false;
                    }
                }
                if (allProj != null && allProj.Count > 0)
                {
                    foreach (var w in allProj)
                    {
                        w.Enabled = false;
                    }
                }
            }
            catch
            {
                string output = "Seems like ya ar not on SIGMA DRACONIS EXPANSE SERVER kopeng.\n" +
                                    "Are you a welwala, ke?\n" +
                                    "Get you blocks toggle on away from here Inyalowda";
                Echo(output);
                TextWriting(LCDLog, LCDLogBool,output, false);
            }
        }

        IEnumerable<double> Sequence()
        {
            while (true)
            {
                if (Me.CubeGrid.IsStatic) { isStation = true; }
                else isStation = false;
                //double runtimeTot=0;
                //double runtime = 1.6*yieldTime;
                //Echo($"runtime: {runtimeTot}");

                /////////////////////
                //FUSION CANISTERS
                /////////////////////
                if (ExpanseServerCustom)
                {
                    
                    if (!isStation )
                    {
                        //Echo("ship");
                        FuelTransfer(customFuel, reactors);
                        TextWriting(LCDLog, LCDLogBool, "1)Pulling Fusion Canisters\n", false);
                    }
                    if (isStation  )
                    {
                        TextWriting(LCDLog, LCDLogBool, $"1)Looping through {reactors.Count} Reactors\n", false);
                        //Echo("station");
                        MyItemType fuelCanister = new MyItemType("MyObjectBuilder_Ingot", "FusionFuel");
                        //Echo($"canister: {fuelCanister}");
                        //Echo($"fuelcan{fuelCanister}");
                        //runtimeTot += runtime;
                        //Echo($"runtime: {runtimeTot}");
                        yield return yieldTime;
                        foreach (var r in reactors)
                        {
                            //Echo($"reactor: {r}");
                            var fuelInReactor = r.GetInventory().GetItemAmount(fuelCanister);
                            if (Math.Abs((int)fuelInReactor - customFuel) >= 0 && Math.Abs((int)fuelInReactor - customFuel) < 3)
                            {
                                //Echo($"fuel: {Math.Abs((int)fuelInReactor - customFuel)}");
                                continue;
                            }
                            TextWriting(LCDLog, LCDLogBool, $"1)Looping through {reactors.Count} Reactors\n", false);
                            //runtimeTot += runtime;
                            //Echo($"runtime: {runtimeTot}");
                            //Echo($"reactor: {r}");
                            fuelInReactor = r.GetInventory().GetItemAmount(fuelCanister);
                            yield return yieldTime;
                            foreach (var cargo in allCargo)
                            {

                                Echo($"Cargo allcargo: {cargo}");
                                MyInventoryItem? fuel = cargo.GetInventory().FindItem(fuelCanister);
                                //runtimeTot += runtime;
                                //Echo($"runtime: {runtimeTot}");
                                if (Math.Abs((int)fuelInReactor - customFuel) >= 0 && Math.Abs((int)fuelInReactor - customFuel) < 3)
                                {
                                    //Echo($"fuel: {Math.Abs((int)fuelInReactor - customFuel)}");
                                    break;
                                }
                                //Echo($"fuelcan{fuel}");
                                fuelInReactor = r.GetInventory().GetItemAmount(fuelCanister);
                                //Echo($"fuel in reactor {fuelInReactor}");
                                MyFixedPoint fuelTransfering = (MyFixedPoint)customFuel - fuelInReactor;
                                //Echo($"fuelin difference {fuelTransfering}");
                                //runtimeTot += runtime;
                                //Echo($"runtime: {runtimeTot}");
                                yield return yieldTime;
                                if (fuelTransfering < 0)
                                {
                                    MyInventoryItem? fuelReactor = r.GetInventory().FindItem(fuelCanister);
                                    //Echo($"negative");
                                    var reverseFueling = fuelInReactor - (MyFixedPoint)customFuel;
                                    //Echo($"i'm transfering back{reverseFueling}");
                                    TextWriting(LCDLog, LCDLogBool, $"1)Looping through {reactors.Count} Reactors\nPulling Fusion Canisters", false);
                                    r.GetInventory().TransferItemTo(cargo.GetInventory(), (MyInventoryItem)fuelReactor, (int)reverseFueling);
                                    //Echo("finished back");
                                }
                                else if (fuelTransfering >= 0)
                                {
                                    //Echo("POSITIVE");
                                    if (!cargo.GetInventory().CanTransferItemTo(r.GetInventory(), fuelCanister) ||
                                    fuel == null)
                                    { continue; }
                                    TextWriting(LCDLog, LCDLogBool, $"1)Looping through {reactors.Count} Reactors\nPulling Fusion Canisters", false);
                                    cargo.GetInventory().TransferItemTo(r.GetInventory(), (MyInventoryItem)fuel, (int)fuelTransfering);
                                }
                            }
                        }
                        //Echo("END");
                        //runtimeTot += runtime;
                        //Echo($"runtime: {runtimeTot}");
                    }
                }
                /////////////////////
                //SPECIAL CONTAINERS
                /////////////////////
                //Echo($"loopFuel: {imLoopingFuel}\nloopSpecial: {imLoopingSpecial}");
                if(specialCargoUsageCustom)
                {
                    GridTerminalSystem.GetBlocksOfType(specialCargo, x => x.CustomName.Contains(specialContainersTagCustom));
                    GridTerminalSystem.GetBlocksOfType(specialConnector, x => x.CustomName.Contains(specialContainersTagCustom));
                    TextWriting(LCDLog, LCDLogBool, $"2)Looping {specialCargo.Count + specialConnector.Count} Special Containers\n", false);
                    if ((specialCargo != null && specialCargo.Count > 0))
                    {
                        //runtimeTot += runtime;
                        //Echo($"runtime: {runtimeTot}");
                        yield return yieldTime;
                        List<MyInventoryItem> items = new List<MyInventoryItem>();
                        foreach (var container in specialCargo)
                        {
                            items.Clear();
                            //Echo($"special c: {container}");
                            if (container.GetInventory() == null) { continue; }
                            //runtimeTot += runtime;
                            //Echo($"runtime: {runtimeTot}");
                            yield return yieldTime;
    
                            container.GetInventory().GetItems(items);
                            foreach (var i in items)
                            {
                                //Echo($"item: {i}");
                                //runtimeTot += runtime;
                                //Echo($"runtime: {runtimeTot}");
    
                                yield return yieldTime;
                                foreach (var destC in allCargo)
                                {
                                    TextWriting(LCDLog, LCDLogBool, $"2)Looping {specialCargo.Count + specialConnector.Count} Special Containers\nPulling Special Containers", false);
                                    container.GetInventory().TransferItemTo(destC.GetInventory(), i, i.Amount);
                                }
                            }
                        }
                    }
                    if(specialConnector != null && specialConnector.Count > 0)
                    {
                        List<MyInventoryItem> items = new List<MyInventoryItem>();
                        foreach (var connectorInv in specialConnector)
                        {
                            items.Clear();
                            if (connectorInv.GetInventory() == null) continue;
                            yield return yieldTime;
                            connectorInv.GetInventory().GetItems(items);
                            foreach (var i in items)
                            {
                                //Echo($"item: {i}");
                                //Echo($"amount: {i.Amount}");
                                yield return yieldTime;
                                foreach (var destC in allCargo)
                                {
                                    TextWriting(LCDLog, LCDLogBool, $"2)Looping {specialCargo.Count + specialConnector.Count} Special Containers\nPulling Special Containers", false);
                                    connectorInv.GetInventory().TransferItemTo(destC.GetInventory(), i, i.Amount);
                                }
                            }
                        }
                    }
                }
                /////////////////////
                //LCD INVENTORY
                /////////////////////
                
                TextWriting(LCDLog, LCDLogBool, "3)Updating Inventory LCD\n", false);
                yield return yieldTime;

                if (LCDInvBool)
                {
                    Dictionary<string, float> oresName = new Dictionary<string, float>();
                    oresName = ParsingLCDInventory();
                    List<MyItemType> acceptedItems = new List<MyItemType>();
                    //runtimeTot += runtime;
                    //Echo($"runtime: {runtimeTot}");
                    StringBuilder output = new StringBuilder();
                    yield return yieldTime;
                    foreach (var oreQuota in oresName)
                    {
                        if(oreQuota.Key.ToLower()!="ore")
                        {
                            var oreType = MyItemType.MakeOre(oreQuota.Key);
                            float totOre = 0;
                            float oreAmount = 0;
                            //Echo($"type: {oreType}");
                            yield return yieldTime;
                            foreach (var c in allCargo)
                            {
                                c.GetInventory().GetAcceptedItems(acceptedItems);
                                if (acceptedItems.Contains(oreType))
                                {
                                    if (c.GetInventory().FindItem(oreType) != null)
                                    {
                                        oreAmount = (float)c.GetInventory().GetItemAmount(oreType);
                                        totOre += oreAmount;
                                    }
                                    else continue;
                                }
                                else
                                {
                                    InvTextWriting($"{oreQuota.Key,-15}= not correctly written\n");
                                    break;
                                }
                                    
                            }
                            output.Append($"{oreQuota.Key, -13}{"= " + Math.Round(totOre / oreQuota.Value, 2), 19}\n");
                            
                            totOre = 0;
                        }
                        
                        else
                        {
                            Dictionary<string, float> oreDict = new Dictionary<string, float>();
                            List<MyInventoryItem> allInv = new List<MyInventoryItem>();
                            
                            
                            foreach (var c in allCargo)
                            {
                                allInv.Clear();
                                c.GetInventory().GetItems(allInv);
                                //(Echo($"item: {allInv.Count}\n");
                                foreach (var i  in allInv)
                                {
                                    
                                    MyDefinitionId ore = i.Type;
                                    //Echo($"ore: {ore}\n");

                                    if (ore.TypeId == OreId.TypeId)
                                    {
                                        var oreName = i.Type.SubtypeId.ToString();
                                        var oreType = MyItemType.MakeOre(oreName);
                                        var amount = c.GetInventory().GetItemAmount(oreType);
                                        //Me.CustomData+= $"\ncont: {c.CustomName}; oreType: {oreType}; amount: {amount}\n";
                                        if (oreDict.ContainsKey(oreName))
                                        {
                                            oreDict[oreName] += (float)amount;
                                            //Me.CustomData+=$"DICT: {oreDict[oreName]}\n";
                                        }
                                        else oreDict.Add(oreName, (float)amount); 
                                    }
                                }
                            }
                            //foreach (var kv in oreDict)
                            //{
                            //    Echo($"ore: {kv.Key}---amount: {kv.Value}\n");
                            //}
                            foreach (var kv in oreDict)
                            {
                                output.Append($"{kv.Key, -10}{"= "+ Math.Round( kv.Value / oreQuota.Value,2), 17}\n");
                            }
                            oreDict.Clear();
                        }
                        
                    }
                    InvTextWriting(output.ToString());
                }
                //runtimeTot = 0;

            }
        }   
        public void TextWriting(IMyTextPanel LCD, bool lcdBool,string input, bool append)
        {
            if (lcdBool)
            {
                string header = $"{lcd_divider}\n{lcd_title}\n           {version}\n{lcd_divider}\n" +
                    $"Station: {isStation}\n{lcd_divider}\nCommands: start, stop, unload, reload\nrefresh, read&write, toggle\n{lcd_divider}\n";
                LCD.WriteText(header + input, append);
            }
        }
        public Dictionary<string, float> ParsingLCDInventory()
        {
            List<string> output = LCDInv.CustomData.Split('\n').ToList();
            Dictionary<string, float> result = new Dictionary<string, float>();
            output.RemoveAll(x => x.Contains("hudlcd"));
            foreach(var i in output)
            {
                var name = i.Split(' ')[0];
                //Echo($"name: {name}");
                float quota = 0;
                try
                {
                    if (float.TryParse(i.Split(' ')[1], out quota))
                    {
                        //Echo($"parse: {float.TryParse(i.Split(' ')[1], out quota)}");
                        //Echo($"quota: {quota}");
                        result.Add(name, quota);
                    }
                }
                catch
                {
                    result.Add(name, 1);
                }
            }
            //Echo($"LIST: {output.Count}");
            //foreach(var ore in output)
            //{
            //    Echo($"ore: {ore}");
            //}
            return result;
        }
        public void InvTextWriting(string input)
        {
            if(LCDInv!=null)
            {
                string header = $"{lcd_divider}\n{lcd_inv_title}\n           {version}\n{lcd_divider}\n" +
                    $"Station: {isStation}\n{lcd_divider}\n";
                LCDInv.WriteText(header + input);
            }
        }
        public void FuelTransfer(int fuelAmount, List<IMyReactor> reactors)
        {
            MyItemType fuelCanister = new MyItemType("MyObjectBuilder_Ingot", "FusionFuel");
            
            //Echo($"fuelcan{fuelCanister}");
            foreach (var r in reactors)
            {
                TextWriting(LCDLog, LCDLogBool, "", false);
                var fuelInReactor = r.GetInventory().GetItemAmount(fuelCanister);
                if (Math.Abs((int)fuelInReactor - customFuel) >= 0 && Math.Abs((int)fuelInReactor - customFuel) < 3)
                {
                    //Echo($"fuel: {Math.Abs((int)fuelInReactor - customFuel)}");
                    continue;
                }
                foreach (var cargo in allCargo)
                {
                    try
                    {
                        if (Math.Abs((int)fuelInReactor - customFuel) >= 0 && Math.Abs((int)fuelInReactor - customFuel) < 3)
                        {
                            //Echo($"fuel: {Math.Abs((int)fuelInReactor - customFuel)}");
                            break;
                        }
                        MyInventoryItem? fuel = cargo.GetInventory().FindItem(fuelCanister);
                        //Echo($"fuelcan{fuel}");
                        //Echo($"fuel in reactor {fuelInReactor}");
                        if (!cargo.GetInventory().CanTransferItemTo(r.GetInventory(), fuelCanister) ||
                                    fuel == null)
                        { continue; }
                        fuelInReactor = r.GetInventory().GetItemAmount(fuelCanister);
                        MyFixedPoint fuelTransfering = (MyFixedPoint)fuelAmount - fuelInReactor;
                        //Echo($"fuelin difference {fuelTransfering}");
                        if (fuelTransfering < 0)
                        {
                            MyInventoryItem? fuelReactor = r.GetInventory().FindItem(fuelCanister);
                            //Echo($"negative");
                            var reverseFueling = fuelInReactor - (MyFixedPoint)fuelAmount;
                            //Echo($"i'm transfering back{reverseFueling}");
                            r.GetInventory().TransferItemTo(cargo.GetInventory(), (MyInventoryItem)fuelReactor, (int)reverseFueling);
                            //Echo("finished back");
                        }
                        else if (fuelTransfering >= 0)
                        {
                            if (!cargo.GetInventory().CanTransferItemTo(r.GetInventory(), fuelCanister) ||
                                    fuel == null)
                            { continue; }
                            TextWriting(LCDLog, LCDLogBool, "Pulling Fusion Canisters", false);
                            cargo.GetInventory().TransferItemTo(r.GetInventory(), (MyInventoryItem)fuel, (int)fuelTransfering);
                        }
                    }
                    catch { }
                }
            }
        }

        public class SimpleTimerSM
        {
            public readonly Program Program;

            /// <summary>
            /// Wether the timer starts automatically at initialization and auto-restarts it's done iterating.
            /// </summary>
            public bool AutoStart { get; set; }

            /// <summary>
            /// <para>Returns true if a sequence is actively being cycled through.</para>
            /// <para>False if it ended, got stopped or no sequence is assigned anymore.</para>
            /// </summary>
            public bool Running { get; private set; }

            /// <summary>
            /// <para>The sequence used by Start(). Can be null.</para>
            /// <para>Setting this will not automatically start it.</para>
            /// </summary>
            public IEnumerable<double> Sequence { get; set; }

            /// <summary>
            /// Time left until the next part is called.
            /// </summary>
            public double SequenceTimer { get; private set; }

            private IEnumerator<double> sequenceSM;

            public SimpleTimerSM(Program program, IEnumerable<double> sequence = null, bool autoStart = true)
            {
                Program = program;
                Sequence = sequence;
                AutoStart = autoStart;

                if (AutoStart)
                {
                    Start();
                }
            }

            /// <summary>
            /// <para>Starts or restarts the sequence declared in Sequence property.</para>
            /// <para>If it's already running, it will be stoped and started from the begining.</para>
            /// <para>Don't forget to set Runtime.UpdateFrequency and call this class' Run() in Main().</para>
            /// </summary>
            public void Start()
            {
                SetSequenceSM(Sequence);
            }

            /// <summary>
            /// <para>Stops the sequence from progressing.</para>
            /// <para>Calling Start() after this will start the sequence from the begining (the one declared in Sequence property).</para>
            /// </summary>
            public void Stop()
            {
                SetSequenceSM(null);
            }

            /// <summary>
            /// <para>Call this in your Program's Main() and have a reasonable update frequency, usually Update10 is good for small delays, Update100 for 2s or more delays.</para>
            /// <para>Checks if enough time passed and executes the next chunk in the sequence.</para>
            /// <para>Does nothing if no sequence is assigned or it's ended.</para>
            /// </summary>
            public void Run()
            {
                if (sequenceSM == null)
                    return;

                SequenceTimer -= Program.Runtime.TimeSinceLastRun.TotalSeconds;

                if (SequenceTimer > 0)
                    return;

                bool hasValue = sequenceSM.MoveNext();

                if (hasValue)
                {
                    SequenceTimer = sequenceSM.Current;

                    if (SequenceTimer <= -0.5)
                        hasValue = false;
                }

                if (!hasValue)
                {
                    if (AutoStart)
                        SetSequenceSM(Sequence);
                    else
                        SetSequenceSM(null);
                }
            }

            private void SetSequenceSM(IEnumerable<double> seq)
            {
                Running = false;
                SequenceTimer = 0;

                sequenceSM?.Dispose();
                sequenceSM = null;

                if (seq != null)
                {
                    Running = true;
                    sequenceSM = seq.GetEnumerator();
                }
            }
        }


    }
}
