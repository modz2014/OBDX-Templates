﻿using OBDXWindows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Windows.Forms;
using static OBDXWindows.Scantool;

namespace OBDXWindowsExample1
{
    public partial class Form1 : Form
    {
        // This is our scantool instance from OBDXWindows library
        Scantool MyOBDXScantool = new Scantool();
        List<OBDXDevice> FoundOBDXDevicesList = new List<OBDXDevice>();

        public Form1()
        {
            InitializeComponent(); // Sets up form UI and controls
        }

        /*
        * This code runs as soon as the Form Loads
        * We use this point to set the communication type we want to use
        */
        private void Form1_Load(object sender, EventArgs e)
        {
            MyOBDXScantool.SelectCommunicationType(ConnectionTypeEnum.USB); // Select communication type as USB
            // TODO: Add support for classic BT and USB.
            AppendToLog("Form Loaded");
        }

        public J2534Handler J2534;
        /*
         * combo box will display all currently available OBDX tools which are not in use
         */
        private async void comboBoxTools_DropDown(object sender, EventArgs e)
        {
            comboBoxTools.Items.Clear(); // clear old found tools from combo box
            FoundOBDXDevicesList.Clear(); // clear old found tool from list
            Tuple<Errors, List<OBDXDevice>> FoundDevices = await MyOBDXScantool.SearchForDevices(); // search for available devices
            if (FoundDevices.Item1 != Errors.Success)
            {
                // Error occurred while searching for device, error can be processed here
                return;
            }

            AppendToLog("OBDX Scantools Found: " + FoundDevices.Item2.Count);
            foreach (OBDXDevice tempdevice in FoundDevices.Item2)
            {
                // Add each found device to the combo box and list.
                FoundOBDXDevicesList.Add(tempdevice);
                comboBoxTools.Items.Add(tempdevice.UniqueIDString);
            }

            if (comboBoxTools.Items.Count > 0)
            {
                comboBoxTools.SelectedIndex = 0;
            }
        }


        /*
         * This function will do the following actions in order:
         * 1) Connect to selected scantool from the combo box
         * 2) Read scantool information and display to richtextbox
         * 3) Set OBD protocol to HS CAN
         * 4) Set a filter to 7E8 (GM ECU ID)
         * 5) Enable OBD communication
         * 6) Send an OBD request (Mode 1, 00)
         * 7) Search for a response and display to screen
         * 
         */

        private async void buttonConnect_Click(object sender, EventArgs e)
        {
            if (comboBoxTools.SelectedIndex == -1)
            {
                //No tool selected
                MessageBox.Show("Please select a tool from the drop down box before attempting to connect.", "Hold Up!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Attempts to connect to scantool selected from combobox.
            AppendToLog("Connecting to Scantool...");
            Errors RsltError1 = await MyOBDXScantool.Connect(FoundOBDXDevicesList[comboBoxTools.SelectedIndex]);
            if (RsltError1 != Errors.Success)
            {
                AppendToLog("Failed to Connect");
                await MyOBDXScantool.Disconnect();
                //Process error here if not successful.
                //Errors possible in Connect are: 
                MessageBox.Show("An error has occured while trying to connect to the selected OBDX device." + Environment.NewLine + Environment.NewLine +
                    "Error: " + RsltError1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            //Get scantool name
            // MyOBDXScantool.Details.Name
            AppendToLog("OBDX Scantool Connected" + Environment.NewLine +
                "Name: " + MyOBDXScantool.Details.Name + Environment.NewLine +
                "Hardware Version: " + MyOBDXScantool.Details.Hardware + Environment.NewLine +
                "Firmware Version: " + MyOBDXScantool.Details.Firmware + Environment.NewLine +
                "Unique ID: " + MyOBDXScantool.Details.UniqueSerial + Environment.NewLine +
                "Supported OBD Protocols: " + MyOBDXScantool.Details.SupportedProtocols + Environment.NewLine +
                "Supported Communication: " + MyOBDXScantool.Details.SupportedPCComms);



            //Read Battery Voltage from pin 16 of diagnostic connector (DLC).
            Tuple<Errors, double> RsltError0 = await MyOBDXScantool.ReadBatteryVoltage();
            if (RsltError0.Item1 != Errors.Success)
            {
                //Errors possible in set OBD Protocol are: 
                await MyOBDXScantool.Disconnect();
                MessageBox.Show("An error has occured while trying to read voltage." + Environment.NewLine + Environment.NewLine +
                    "Error: " + RsltError0.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            AppendToLog("Battery Voltage is: " + RsltError0.Item2.ToString("N2"));



            AppendToLog("Choose Which Protocol to Connect");
            Tuple<Errors, byte> RsltError2 = await MyOBDXScantool.GetSupportedCommunicationProtocols();

            var supportedProtocols = MyOBDXScantool.GetType()
                                        .GetNestedTypes()
                                        .FirstOrDefault(t => t.Name == "Protocols")
                                        ?.GetEnumNames();

            // populate the comboBox with the list of supported protocols
            if (supportedProtocols != null)
            {
                conConCBoProtocol.Items.AddRange(supportedProtocols);
                conConCBoProtocol.SelectedIndex = 0; // set the first protocol as the default
            }

            if (RsltError2.Item1 != Errors.Success)
            {
                MessageBox.Show("An error occurred: " + RsltError2.Item2);
                // other error handling code here
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {

            if (conConCBoProtocol.SelectedItem.ToString() == "HSCAN")
            {
                Tuple<Errors, byte> RsltError2_CANBUS = await MyOBDXScantool.SetOBDProtocol(Protocols.HSCAN);
                if (RsltError2_CANBUS.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Protocol are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occurred while trying to set OBD Protocol." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError2_CANBUS.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Tuple<Errors, byte[]> RsltError3_CANBUS = await MyOBDXScantool.CANCommands.SetRxFilterEntire(0, new CAN_Class.Filter(0x7E8, 0x7FF, 0x7E0, 0, 1, 1));
                if (RsltError3_CANBUS.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Filter are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occurred while trying to set OBD Filter." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError3_CANBUS.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                AppendToLog("Sending OBD Network Message to ECU");
                RsltError3_CANBUS = await MyOBDXScantool.WriteNetworkFrame(0x7E0, new byte[] { 0x01, 0x00 });
                if (RsltError3_CANBUS.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Protocol are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occured while trying write OBD message." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError3_CANBUS.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }


                //search for response
                AppendToLog("Searching for response from ECU");
                Tuple<Errors, NetworkMessage> RsltError4 = await MyOBDXScantool.ReadNetworkFrame(2000, 2); //Retry twice with a max of 2000ms timeout on each attempt.
                if (RsltError4.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Protocol are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occured while trying read OBD message." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError4.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }


                AppendToLog("Message Found: " + RsltError4.Item2.ToString());


                AppendToLog("Disconnecting from Scantool");
                await MyOBDXScantool.Disconnect();

            }
            else if (conConCBoProtocol.SelectedItem.ToString() == "VPW")
            {
                Tuple<Errors, byte> RsltError2_VPW = await MyOBDXScantool.SetOBDProtocol(Protocols.VPW);
                if (RsltError2_VPW.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Protocol are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occurred while trying to set OBD Protocol." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError2_VPW.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;

                }

                Tuple<Errors, byte[]> RsltError3_VPW = await MyOBDXScantool.VPWCommands.SetFromRangeFilter(140, 00, 50);
                if (RsltError3_VPW.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Filter are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occurred while trying to set OBD Filter." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError3_VPW.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                AppendToLog("Sending VPW Network Message to ECU");
                RsltError3_VPW = await MyOBDXScantool.WriteNetworkFrame(0x33, new byte[] { 0x01, 0x00 });
                if (RsltError3_VPW.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Protocol are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occured while trying write OBD message." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError3_VPW.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                //search for response
                AppendToLog("Searching for response from ECU");
                Tuple<Errors, NetworkMessage> RsltError4 = await MyOBDXScantool.ReadNetworkFrame(2000, 2); //Retry twice with a max of 2000ms timeout on each attempt.
                if (RsltError4.Item1 != Errors.Success)
                {
                    //Errors possible in set OBD Protocol are: 
                    await MyOBDXScantool.Disconnect();
                    MessageBox.Show("An error has occured while trying read OBD message." + Environment.NewLine + Environment.NewLine +
                        "Error: " + RsltError4.Item1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }



                //await Task.Delay(10000);
            }

            string selectedProtocol = conConCBoProtocol.SelectedItem.ToString();
            switch (selectedProtocol)
            {
                case "HSCAN":
                    Tuple<Errors, byte> RsltError2_CANBUS = await MyOBDXScantool.SetOBDProtocol(Protocols.HSCAN);
                    if (RsltError2_CANBUS.Item1 != Errors.Success)
                    {
                        // Handle error
                    }
                    // Set filter and write message for HSCAN protocol
                    Tuple<Errors, byte[]> RsltError3_CANBUS = await MyOBDXScantool.CANCommands.SetRxFilterEntire(0, new CAN_Class.Filter(0x7E8, 0x7FF, 0x7E0, 0, 1, 1))
                    break;

                case "VPW":
                    Tuple<Errors, byte> RsltError2_VPW = await MyOBDXScantool.SetOBDProtocol(Protocols.VPW);
                    if (RsltError2_VPW.Item1 != Errors.Success)
                    {
                        // Handle error
                    }
                    Tuple<Errors, byte[]> RsltError3_VPW = await MyOBDXScantool.VPWCommands.SetRxFilterSingleFrame(0x68, 0x6A, 0x10);
                    if (RsltError3_VPW.Item1 != Errors.Success)
                    {
                        // Handle error
                    }
                    RsltError3_VPW = await MyOBDXScantool.WriteNetworkFrame(0x68, new byte[] { 0x6A, 0x10, 0x03 });
                    if (RsltError3_VPW.Item1 != Errors.Success)
                    {
                        // Handle error
                    }
                    break;
                // Add cases for other protocols as needed
                default:
                    // Handle unsupported protocol
                    break;
            }

        }
        public ECU_Functions ECUFunctions;

        //Appends a message to the debug log along with a timetamp.
        public void AppendToLog(string message)
        {
            richTextBoxLog.AppendText("[" + DateTime.Now.ToString() + "] " + message + Environment.NewLine);
            richTextBoxLog.HideSelection = false; //allows automatic scrolling
        }

        private void button2_Click(object sender, EventArgs e)
        {
            J2534.ClearBuffer(J2534Handler.BufType.Rx);
            AppendToLog("Checking if Kernel is runing");

            Response<bool> response = ECUFunctions.CheckIfKernelRunning();
            if (response.Status != ResponseStatus.Success)
            {
                if (response.Status == ResponseStatus.Network_NoResponse)
                {
                    AppendToLog("No Response Back From ECU");
                }
                else
                {
                    AppendToLog("No Response");
                }
                return;
            }
            checked
            {
                Response<bool> response2;

                if (response.Value)
                {
                    AppendToLog("Kernel running");
                }
                else
                {
                    AppendToLog("Kernel not running");

                    AppendToLog("Status: Identifying ECU..");

                    Response<int> operatingSystem = ECUFunctions.GetOperatingSystem();
                    if (operatingSystem.Status != ResponseStatus.Success)
                    {
                        if (operatingSystem.Status == ResponseStatus.Network_NoResponse)
                        {
                            AppendToLog("No response back from ECU, please ensure ignition is on before trying again.");
                        }
                        else
                        {
                            AppendToLog("No Response");
                        }

                        return;
                    }
                }
            }
        }
    }
}
