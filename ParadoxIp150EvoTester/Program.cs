using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ParadoxIp150EvoTester
{
    /// <summary>
    /// This program demonstrates how to connect to a Paradox EVO panel 
    /// using the IP150 module via the software port (typically 10,000) 
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // Connection details
            string ipAddress = ConfigurationManager.AppSettings["IPAddress"];
            int softwarePort = Convert.ToInt32(ConfigurationManager.AppSettings["SoftwarePort"]);
            string modulePassword = ConfigurationManager.AppSettings["ModulePassword"];

            var labelPadding = 30;

            Log("Paradox IP150 software port tester (EVO panels only)", ConsoleColor.White);
            Log("".PadRight(60,'-'), ConsoleColor.White);

            using (var tcpClient = new TcpClient())
            {
                try
                {
                    // Establish TCP/IP connection
                    tcpClient.Connect(ipAddress, softwarePort);
                    var networkStream = tcpClient.GetStream();

                    // Login to the IP150 module & panel
                    Log("Starting login process...");
                    if (Login(networkStream, modulePassword, out var panelType))
                    {
                        Log("Login succeeded!", ConsoleColor.Green);
                        Log("");
                        Log("Panel type", panelType, labelPadding);
                        Log("");

                        #region Reading RAM

                        Log("Requesting RAM data...");
                        var ram = ReadRamMemory(networkStream, 1);
                        Log("Received RAM data", ram?.Length.ToString(), labelPadding);
                        Log("");

                        if (ram != null)
                        {
                            var panelDate = new DateTime(ram[18] * 100 + ram[19], ram[20], ram[21], ram[22], ram[23], ram[24]);
                            Log("Panel date", panelDate.ToString("yyyy-MM-dd HH:ss"), labelPadding);
                            Log("");
                        }

                        #endregion

                        #region Reading EEPROM

                        Log("Requesting EEPROM data...");
                        var eeprom = ReadEepromMemory(networkStream, 0x430, 0, 16);
                        Log("Received EEPROM data", eeprom?.Length.ToString(), labelPadding);
                        Log("");

                        #endregion

                        #region Reading Zone Labels

                        for (byte zoneNo = 1; zoneNo <= 2; zoneNo++)
                        {
                            Log($"Requesting zone {zoneNo:000} label...");
                            var zoneLabel = ReadZoneLabel(networkStream, zoneNo);
                            Log("Received zone label", zoneLabel, labelPadding);
                            Log("");
                        }

                        #endregion

                        #region Reading Partition Labels

                        for (byte partitionNo = 1; partitionNo <= 2; partitionNo++)
                        {
                            Log($"Requesting partition {partitionNo:000} label...");
                            var partitionLabel = ReadPartitionLabel(networkStream, partitionNo);
                            Log("Received partition label", partitionLabel, labelPadding);
                            Log("");
                        }

                        #endregion

                        #region Reading User Labels

                        for (byte userNo = 1; userNo <= 2; userNo++)
                        {
                            Log($"Requesting user {userNo:000} label...");
                            var userLabel = ReadUserLabel(networkStream, userNo);
                            Log("Received user label", userLabel, labelPadding);
                            Log("");
                        }

                        #endregion

                        #region Reading Door Labels

                        for (byte doorNo = 1; doorNo <= 2; doorNo++)
                        {
                            Log($"Requesting door {doorNo:000} label...");
                            var doorLabel = ReadDoorLabel(networkStream, doorNo);
                            Log("Received door label", doorLabel, labelPadding);
                            Log("");
                        }

                        #endregion

                        #region Reading Module Labels

                        for (byte moduleNo = 1; moduleNo <= 2; moduleNo++)
                        {
                            Log($"Requesting module {moduleNo:000} label...");
                            var moduleLabel = ReadModuleLabel(networkStream, moduleNo);
                            Log("Received module label", moduleLabel, labelPadding);
                            Log("");
                        }

                        #endregion
                        
                        Wait(2000);

                        // Logout from the IP150 module & panel
                        Log("Starting logout process...");
                        Logout(networkStream);
                        Log("Logout succeeded!", ConsoleColor.Green);
                    }
                    else
                    {
                        Log("Login failed!", ConsoleColor.Red);
                    }
                }
                catch (Exception e)
                {
                    Log(e.ToString(), ConsoleColor.Red);
                }
                finally
                {
                    if (tcpClient.Connected)
                        tcpClient.Close();
                }

                if (Debugger.IsAttached)
                {
                    Log("");
                    Log("Press any key to exit...");
                    Console.ReadKey();
                }
            }
        }

        #region Login / logout
        /// <summary>
        /// This method performs the IP150 module login sequence.
        /// </summary>
        /// <param name="networkStream">The open active TCP/IP stream.</param>
        /// <param name="password">The IP150 module password.</param>
        /// <param name="panelType">An out parameter that contains the panel type value.</param>
        /// <returns>Returns True if the login was successful.</returns>
        static bool Login(NetworkStream networkStream, string password, out string panelType)
        {
            panelType = "UNKNOWN";

            // 1: Login to module request (IP150 only)
            var header1 = new byte[] { 0xAA, (byte)password.Length, 0x00, 0x03, 0x08, 0xF0, 0x00, 0x0A }.PadRight(16, 0xEE);
            var message1 = GetModulePasswordBytes(password);
            SendPacket(networkStream, header1.Append(message1));
            var response1 = ReceivePacket(networkStream);
            if (response1[4] != 0x38)
                return false;

            // 2: Unknown request (IP150 only)
            var header2 = new byte[] { 0xAA, 0x00, 0x00, 0x03, 0x08, 0xF2, 0x00, 0x0A }.PadRight(16, 0xEE);
            SendPacket(networkStream, header2);
            var response2 = ReceivePacket(networkStream);

            // 3: Unknown request (IP150 only)
            var header3 = new byte[] { 0xAA, 0x00, 0x00, 0x03, 0x08, 0xF3, 0x00, 0x0A }.PadRight(16, 0xEE);
            SendPacket(networkStream, header3);
            var response3 = ReceivePacket(networkStream);

            // 4: Init communication over UIP softawre request (IP150 and direct serial)
            var header4 = new byte[] { 0xAA, 0x25, 0x00, 0x04, 0x08, 0x00, 0x00, 0x0A }.PadRight(16, 0xEE);
            var message4 = new byte[] { 0x72 }.PadRight(37, 0x00);
            message4[message4.Length - 1] = CalculateChecksum(message4, 0, message4.Length - 1);
            SendPacket(networkStream, header4.Append(message4));
            var response4 = ReceivePacket(networkStream);
            var initCommOverSoftwareMessage = response4.SubArray(16);
            panelType = initCommOverSoftwareMessage.GetString(28, 8);

            // 5: Unknown request (IP150 only)
            var header5 = new byte[] { 0xAA, 0x09, 0x00, 0x03, 0x08, 0xF8, 0x00, 0x0A }.PadRight(16, 0xEE);
            var payload5 = new byte[] { 0x0A, 0x50, 0x08, 0x00, 0x00, 0x01, 0x00, 0x00, 0x59 }.PadRight(16, 0xEE);

            SendPacket(networkStream, header5.Append(payload5));
            var response5 = ReceivePacket(networkStream);

            // 6: Initialize serial communication request (IP150 and direct serial)
            var header6 = new byte[] { 0xAA, 0x25, 0x00, 0x04, 0x08, 0x00, 0x00, 0x0A }.PadRight(16, 0xEE);
            var message6 = new byte[] { 0x5F, 0x20 }.PadRight(37, 0x00);
            message6[message6.Length - 1] = CalculateChecksum(message6, 0, message6.Length - 1);
            SendPacket(networkStream, header6.Append(message6));
            var response6 = ReceivePacket(networkStream);
            var initializationMessage = response6.SubArray(16);

            // 7: Initialization request (in response to the initialization from the panel) (IP150 and direct serial)
            var header7 = new byte[] { 0xAA, 0x25, 0x00, 0x04, 0x08, 0x00, 0x00, 0x14 }.PadRight(16, 0xEE);
            var message7 = new byte[]
            {
                // Initialization command
                0x00, 

                // Module address
                initializationMessage[1], 

                // Not used
                0x00, 0x00,
                
                // Product ID
                initializationMessage[4], 

                // Software version
                initializationMessage[5], 

                // Software revision
                initializationMessage[6], 

                // Software ID
                initializationMessage[7], 

                // Module ID
                initializationMessage[8], initializationMessage[9], 

                // PC Password
                0x00, 0x00, 

                // Modem speed
                0x0A, 

                // Winload type ID
                0x30, 

                // User code (for some reason Winload sends user code 021000)
                0x02, 0x10, 0x00, 

                // Module serial number
                initializationMessage[17], initializationMessage[18], initializationMessage[19], initializationMessage[20], 

                // EVO section 3030-3038 data
                initializationMessage[21],
                initializationMessage[22],
                initializationMessage[23],
                initializationMessage[24],
                initializationMessage[25],
                initializationMessage[26],
                initializationMessage[27],
                initializationMessage[28],
                initializationMessage[29],

                // Not used
                0x00, 0x00, 0x00, 0x00, 

                // Source ID (0x02 = Winload through IP)
                0x02, 

                // Carrier length
                0x00, 

                // Checksum
                0x00
            };
            message7[message7.Length - 1] = CalculateChecksum(message7, 0, message7.Length - 1);
            SendPacket(networkStream, header7.Append(message7));
            var response7 = ReceivePacket(networkStream);
            var loginConfirmationMessage = response7.SubArray(16);

            return loginConfirmationMessage[0].GetHighNibble() == 0x1;
        }

        /// <summary>
        /// This method terminates the connection between the IP150 module and the PC.
        /// </summary>
        /// <param name="networkStream">The open active TCP/IP stream.</param>
        static void Logout(NetworkStream networkStream)
        {
            // 1: Login to module request (IP150 only)
            var header1 = new byte[] { 0xAA, 0x07, 0x00, 0x04, 0x08, 0x00, 0x00, 0x14 }.PadRight(16, 0xEE);
            var message1 = new byte[] { 0x00, 0x07, 0x05, 0x00, 0x00, 0x00, 0x00 };
            message1[message1.Length - 1] = CalculateChecksum(message1, 0, message1.Length - 1);
            SendPacket(networkStream, header1.Append(message1));
        }

        static byte[] GetModulePasswordBytes(string password)
        {
            // Body containing the password is 16 bytes (or multiples thereof)
            var passwordLength = password.Length;
            var byteCount = (int)(Math.Ceiling(passwordLength / 16.0) * 16.0);
            return password.ToBytes(byteCount, 0xEE);
        }
        #endregion

        #region Panel commands

        static byte[] ReadEepromMemory(NetworkStream networkStream, uint address, byte blockNo, byte bytesToRead)
        {
            if (blockNo > 15)
                throw new Exception("Invalid block number. Valid values are 0 to 15");

            if (bytesToRead < 1 || bytesToRead > 64)
                throw new Exception("Invalid bytes to read. Valid values are 1 to 64.");

            var header = new byte[] { 0xAA, 0x08, 0x00, 0x04, 0x08, 0x00, 0x00, 0x14 }.PadRight(16, 0xEE);
            var message = new byte[8];

            byte controlByte = 0x00;
            controlByte = controlByte.SetBit(7, false); // EEPROM

            var eepromRamAddressBits17And16 = address.GetBits17To16();
            controlByte = controlByte.SetBit(1, eepromRamAddressBits17And16.IsBitSet(1));
            controlByte = controlByte.SetBit(0, eepromRamAddressBits17And16.IsBitSet(0));

            message[0] = 0x50;
            message[1] = 0x08;
            message[2] = controlByte;
            message[3] = 0x00;
            message[4] = address.GetBits15To08();
            message[5] = address.GetBits07To00();
            message[6] = bytesToRead;
            message[message.Length - 1] = CalculateChecksum(message, 0, message.Length - 1);

            SendPacket(networkStream, header.Append(message));

            var response = ReceivePacketForCommand(networkStream, 0x5);

            if (response == null || response.Length < bytesToRead + 7)
                return null;

            return response.SubArray(6, bytesToRead);
        }

        static byte[] ReadRamMemory(NetworkStream networkStream, uint blockNo, byte bytesToRead = 64)
        {
            if (blockNo < 1 || blockNo > 16)
                throw new Exception("Invalid block number. Valid values are 1 to 16");

            if (bytesToRead < 1 || bytesToRead > 64)
                throw new Exception("Invalid bytes to read. Valid values are 1 to 64.");

            var header = new byte[] { 0xAA, 0x08, 0x00, 0x04, 0x08, 0x00, 0x00, 0x14 }.PadRight(16, 0xEE);
            var message = new byte[8];

            byte controlByte = 0x00;
            controlByte = controlByte.SetBit(7, true); // RAM

            message[0] = 0x50;
            message[1] = 0x08;
            message[2] = controlByte;
            message[3] = 0x00;
            message[4] = blockNo.GetBits15To08();
            message[5] = blockNo.GetBits07To00();
            message[6] = bytesToRead;
            message[message.Length - 1] = CalculateChecksum(message, 0, message.Length - 1);

            SendPacket(networkStream, header.Append(message));

            var response = ReceivePacketForCommand(networkStream, 0x5);

            if (response == null || response.Length < bytesToRead + 7)
                return null;

            return response.SubArray(6, bytesToRead);
        }

        static string ReadZoneLabel(NetworkStream networkStream, byte zoneNo)
        {
            if (zoneNo < 1 || zoneNo > 192)
                throw new Exception("Invalid zone number. Valid values are 1-192.");

            #region EVO192 specific

            uint address;
            byte blockNo = 0;
            byte labelLength = 16;

            if (zoneNo <= 96)
                address = (uint)(0x430 + (zoneNo - 1) * 16);
            else
                address = (uint)(0x62F7 + (zoneNo - 97) * 16);

            #endregion

            var zoneLabelBytes = ReadEepromMemory(networkStream, address, blockNo, labelLength);

            return zoneLabelBytes.GetString(0, zoneLabelBytes.Length);
        }

        static string ReadPartitionLabel(NetworkStream networkStream, byte partitionNo)
        {
            if (partitionNo < 1 || partitionNo > 8)
                throw new Exception("Invalid partition number. Valid values are 1-8.");

            #region EVO192 specific

            uint address = (uint)(0x3A6B + (partitionNo - 1) * 107);
            byte blockNo = 0;
            byte labelLength = 16;

            #endregion

            var zoneLabelBytes = ReadEepromMemory(networkStream, address, blockNo, labelLength);

            return zoneLabelBytes.GetString(0, zoneLabelBytes.Length);
        }

        static string ReadUserLabel(NetworkStream networkStream, ushort userNo)
        {
            if (userNo < 1 || userNo > 999)
                throw new Exception("Invalid partition number. Valid values are 1-999.");

            #region EVO192 specific

            uint address;
            byte blockNo = 0;
            byte labelLength = 16;

            if (userNo < 257)
            {
                address = (uint)(0x3E47 + (userNo - 1) * 16);
            }
            else
            {
                address = (uint)(0x15190 + (userNo - 257) * 16);
            }

            #endregion

            var zoneLabelBytes = ReadEepromMemory(networkStream, address, blockNo, labelLength);

            return zoneLabelBytes.GetString(0, zoneLabelBytes.Length);
        }

        static string ReadDoorLabel(NetworkStream networkStream, byte doorNo)
        {
            if (doorNo < 1 || doorNo > 32)
                throw new Exception("Invalid door number. Valid values are 1-32.");

            #region EVO192 specific

            uint address = (uint)(0x345C + (doorNo - 1) * 16);
            byte blockNo = 0;
            byte labelLength = 16;

            #endregion

            var zoneLabelBytes = ReadEepromMemory(networkStream, address, blockNo, labelLength);

            return zoneLabelBytes.GetString(0, zoneLabelBytes.Length);
        }

        static string ReadModuleLabel(NetworkStream networkStream, byte moduleNo)
        {
            if (moduleNo < 1 || moduleNo > 254)
                throw new Exception("Invalid module number. Valid values are 1-254.");

            #region EVO192 specific

            uint address = (uint)(0x4E47 + (moduleNo - 1) * 16);
            byte blockNo = 0;
            byte labelLength = 16;

            #endregion

            var zoneLabelBytes = ReadEepromMemory(networkStream, address, blockNo, labelLength);

            return zoneLabelBytes.GetString(0, zoneLabelBytes.Length);
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// This method stops the current thread for a specified duration in milliseconds.
        /// </summary>
        /// <param name="milliseconds">Milliseconds to wait.</param>
        static void Wait(int milliseconds)
        {
            Task.Delay(milliseconds).Wait();
        }

        /// <summary>
        /// This method sends raw bytes to the IP150 module.
        /// </summary>
        /// <param name="networkStream">The open active TCP/IP stream.</param>
        /// <param name="request">The raw bytes to send.</param>
        static void SendPacket(NetworkStream networkStream, byte[] request)
        {
            if (request == null || request.Length == 0)
                return;
            networkStream.Write(request, 0, request.Length);
        }

        /// <summary>
        /// This method reads data from the IP150 module. It does no splitting
        /// of responses should more than one response be combined.
        /// </summary>
        /// <param name="networkStream">The open active TCP/IP stream.</param>
        /// <returns>An array of the raw bytes received from the TCP/IP stream.</returns>
        static byte[] ReceivePacket(NetworkStream networkStream)
        {
            byte[] response = new byte[2048];
            var bytesReceived = networkStream.Read(response, 0, response.Length);
            return response.SubArray(0, bytesReceived);
        }

        /// <summary>
        /// This method reads data from the IP150 module.  It can return multiple responses
        /// e.g. a live event is combined with another response.
        /// </summary>
        /// <param name="networkStream">The open active TCP/IP stream.</param>
        /// <param name="command">A panel command, e.g. 0x5 (read memory)</param>
        /// <returns>An array of an array of the raw bytes received from the TCP/IP stream.</returns>
        static byte[] ReceivePacketForCommand(NetworkStream networkStream, byte command)
        {
            if (command > 0xF)
                command = command.GetHighNibble();

            byte retryCounter = 0;

            // We might enter this too early, meaning the panel has not yet had time to respond
            // to our command.  We add a retry counter that will wait and retry.
            while (networkStream.DataAvailable || retryCounter < 3)
            {
                if (networkStream.DataAvailable)
                {
                    var responses = SplitResponsePackets(ReceivePacket(networkStream));
                    foreach (var response in responses)
                    {
                        // Message too short
                        if (response.Length < 17)
                            continue;

                        // Response command (after header) is not related to reading memory
                        if (response[16].GetHighNibble() != command)
                            continue;

                        return response.SubArray(16);
                    }
                }

                // Give the panel time to send us a response
                Wait(100);

                retryCounter++;
            }

            Console.WriteLine("Failed to receive data for command 0x{0:X}", command);

            return null;
        }

        /// <summary>
        /// The method splits a single response from the IP150 module into individual responses.
        /// If the delay is too big between a request and a response you might get more than
        /// one response's data in one read.  E.g. you might receive a 'read EEPROM (0x50)'
        /// response combined with a live event response in one read.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        static byte[][] SplitResponsePackets(byte[] data)
        {
            var packets = new List<byte[]>();

            while (data.Length > 0)
            {
                if (data.Length < 16 || data[0] != 0xAA)
                    throw new Exception("No 16 byte header found");

                var header = data.SubArray(0, 16);
                var messageLength = header[1];

                // Remove the header
                data = data.SubArray(16);

                if (data.Length < messageLength)
                    throw new Exception("Unexpected end of data");

                // Check if there's padding bytes (0xEE)
                if (data.Length > messageLength)
                {
                    for (var i = messageLength; i < data.Length; i++)
                    {
                        if (data[i] == 0xEE)
                            messageLength++;
                        else
                            break;
                    }
                }

                var message = data.SubArray(0, messageLength);

                data = data.SubArray(messageLength);

                packets.Add(header.Append(message));
            }

            return packets.ToArray();
        }

        public static byte CalculateChecksum(byte[] data, int startIndex, int length)
        {
            byte[] checksumData = new byte[length];
            Array.Copy(data, startIndex, checksumData, 0, length);
            return CalculateChecksum(checksumData);
        }

        public static byte CalculateChecksum(byte[] data)
        {
            byte checksum = 0;

            foreach (byte value in data)
            {
                checksum += value;
            }

            return checksum;
        }

        private static void Log(string text, bool newLine = true)
        {
            Log(text, null, null, null, null, newLine);
        }

        private static void Log(string text, ConsoleColor textForeColor, bool newLine = true)
        {
            Log(text, null, null, textForeColor, null, newLine);
        }

        private static void Log(string label, string value, bool newLine = true)
        {
            Log(label, value, null, ConsoleColor.White, ConsoleColor.Yellow, newLine);
        }

        private static void Log(string label, string value, int labelPadding, bool newLine = true)
        {
            Log(label, value, labelPadding, ConsoleColor.White, ConsoleColor.Yellow, newLine);
        }

        private static void Log(string label, string value, int? labelPadding, ConsoleColor? labelForeColor, ConsoleColor? valueForeColor, bool newLine = true)
        {
            ConsoleColor defaultForeColor = Console.ForegroundColor;

            bool hasLabel = !string.IsNullOrEmpty(label);
            bool hasValue = !string.IsNullOrEmpty(value);
            
            if (hasLabel)
            {
                if (labelForeColor.HasValue)
                    Console.ForegroundColor = labelForeColor.Value;

                Console.Write(label);
                if (hasValue)
                {
                    if (labelPadding.HasValue)
                        Console.Write("".PadRight(labelPadding.Value - label.Length, ' '));
                    Console.Write(": ");
                }
            }

            if (hasValue)
            {
                if (valueForeColor.HasValue)
                Console.ForegroundColor = valueForeColor.Value;

                Console.Write(value);
            }

            if (newLine)
                Console.WriteLine();

            if (Console.ForegroundColor != defaultForeColor)
                Console.ForegroundColor = defaultForeColor;
        }

        #endregion
    }
}
