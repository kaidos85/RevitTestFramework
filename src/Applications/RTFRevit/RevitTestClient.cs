﻿using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RTF.Framework;

namespace RTF.Applications
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.UsingCommandData)]
    public class RTFClientStartCmd : IExternalCommand
    {
        private static int iPort = 0;
        private static Socket clientSocket;
        private static IPAddress ipAddress;
        private static IPEndPoint endPoint;

        /// <summary>
        /// The entry point when the command runs. This command will try to connect to localhost
        /// at a port passed in through the journaling data.
        /// </summary>
        /// <param name="cmdData"></param>
        /// <param name="message"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public Result Execute(ExternalCommandData cmdData, ref string message, ElementSet elements)
        {
            if (!ReadDataFromJournal(cmdData.JournalData))
            {
                message = "The port is not given or is in a bad format!";
                return Result.Failed;
            }            
            ipAddress = IPAddress.Parse(CommonData.LocalIPAddress);
            endPoint = new IPEndPoint(ipAddress, iPort);
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            clientSocket.Connect(endPoint);
            NotifyClientStarted();

            return Result.Succeeded;
        }

        /// <summary>
        /// Read the port data from the journal data
        /// </summary>
        /// <param name="dataMap"></param>
        /// <returns></returns>
        private bool ReadDataFromJournal(IDictionary<string, string> dataMap)
        {
            string strPort;
            if (!dataMap.TryGetValue("Port", out strPort))
            {
                return false;
            }

            if (!int.TryParse(strPort, out iPort))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Send a control message to the server to say that the client has started
        /// </summary>
        private static void NotifyClientStarted()
        {
            if (clientSocket != null)
            {
                ControlMessage msg = new ControlMessage(ControlType.NotificationOfStart);
                SendMessage(msg);
            }
        }

        /// <summary>
        /// Send a control message to the server to say that the client has ended
        /// </summary>
        private static void NotifyClientEnded()
        {
            if (clientSocket != null)
            {
                ControlMessage msg = new ControlMessage(ControlType.NotificationOfEnd);
                SendMessage(msg);
            }
        }

        /// <summary>
        /// When the client application has completed running, this function is called
        /// to shutdown client
        /// </summary>
        public static void ShutdownClient()
        {
            if (clientSocket != null)
            {
                NotifyClientEnded();
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
                clientSocket = null;
                iPort = 0;
            }
        }

        /// <summary>
        /// Helper method to send the test name in a fixture (of a given name) to the server
        /// (this is called when a tests starts)
        /// </summary>
        /// <param name="testName">name of test</param>
        /// <param name="fixtureName">name of fixture (assembly)</param>
        public static void SendTestInformation(string testName, string fixtureName)
        {
            DataMessage msg = new DataMessage(testName, fixtureName);
            SendMessage(msg);
        }

        /// <summary>
        /// Helper method to send information about the test results to the server
        /// e.g. did the test pass/fail/etc?
        /// (this is called when a test completes)
        /// </summary>
        /// <param name="testName">name of test</param>
        /// <param name="fixtureName">name of fixture (assembly)</param>
        /// <param name="result">test result (<see cref="RevitTestExecutive.RunTest"/></param>
        /// <param name="stackTrace">stack trace of failure, if any</param>
        public static void SendTestResultInformation(string testName, string fixtureName, string result, string stackTrace)
        {
            TestResultMessage msg = new TestResultMessage(testName, fixtureName, result, stackTrace);
            SendMessage(msg);
        }

        /// <summary>
        /// Helper method to send an intercepted console out/error out line of text to the server
        /// </summary>
        /// <param name="messageType">type of message (error or console out)</param>
        /// <param name="text">line of text</param>
        public static void SendConsoleMessage(string text, ConsoleMessageType messageType = ConsoleMessageType.ConsoleOut)
        {
            ConsoleOutMessage msg = new ConsoleOutMessage(messageType, text);
            SendMessage(msg);
        }

        /// <summary>
        /// Send a message to the server. At the beginning of the message, there are 4 bytes to
        /// identify the message length
        /// </summary>
        /// <param name="msg"></param>
        private static void SendMessage(Message msg)
        {
            RTFClientStartCmd.ClientSocket?.Send(MessageHelper.AddHeader(Message.ToBytes(msg)));
        }

        /// <summary>
        /// The client socket
        /// </summary>
        public static Socket ClientSocket
        {
            get
            {
                return clientSocket;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.UsingCommandData)]
    public class RTFClientEndCmd : IExternalCommand
    {
        /// <summary>
        /// This command will shutdown the client socket
        /// </summary>
        /// <param name="cmdData"></param>
        /// <param name="message"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public Result Execute(ExternalCommandData cmdData, ref string message, ElementSet elements)
        {
            RTFClientStartCmd.ShutdownClient();

            return Result.Succeeded;
        }
    }
}