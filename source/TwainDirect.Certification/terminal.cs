﻿///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.Certification.Program
//
//  Our entry point.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    01-Jun-2017     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2017 Kodak Alaris Inc.
//
//  Permission is hereby granted, free of charge, to any person obtaining a
//  copy of this software and associated documentation files (the "Software"),
//  to deal in the Software without restriction, including without limitation
//  the rights to use, copy, modify, merge, publish, distribute, sublicense,
//  and/or sell copies of the Software, and to permit persons to whom the
//  Software is furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
//  THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
//  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
//  DEALINGS IN THE SOFTWARE.
///////////////////////////////////////////////////////////////////////////////////////

// Helpers...
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using TwainDirect.Support;
using Microsoft.Win32.SafeHandles;

namespace TwainDirect.Certification
{
    /// <summary>
    /// The certification object that we'll use to test and exercise functions
    /// for TWAIN Direct.
    /// </summary>
    class Terminal
    {
        // Public Methods
        #region Public Methods

        /// <summary>
        /// Initialize stuff...
        /// </summary>
        public Terminal()
        {
            // Make sure we have a console...
            if (TwainLocalScanner.GetPlatform() == TwainLocalScanner.Platform.WINDOWS)
            {
                NativeMethods.AllocConsole();
                // We have to do some additional work to get out text in the console instead
                // of having it redirected to Visual Studio's output window...
                IntPtr stdHandle = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
                SafeFileHandle safefilehandle = new SafeFileHandle(stdHandle, true);
                FileStream fileStream = new FileStream(safefilehandle, FileAccess.Write);
                Encoding encoding = System.Text.Encoding.GetEncoding(Encoding.Default.CodePage);
                StreamWriter streamwriterStdout = new StreamWriter(fileStream, encoding);
                streamwriterStdout.AutoFlush = true;
                Console.SetOut(streamwriterStdout);
            }

            // Init stuff...
            m_blSilent = false;
            m_adnssddeviceinfoSnapshot = null;
            m_dnssddeviceinfoSelected = null;
            m_twainlocalscanner = null;
            m_lkeyvalue = new List<KeyValue>();

            // Create the mdns monitor, and start it...
            m_dnssd = new Dnssd(Dnssd.Reason.Monitor);
            m_dnssd.MonitorStart(null, IntPtr.Zero);

            // Build our command table...
            m_ldispatchtable = new List<Interpreter.DispatchTable>();

            // Discovery and Selection...
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdGoto,                         new string[] { "goto" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdHelp,                         new string[] { "help", "?" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdList,                         new string[] { "list" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdQuit,                         new string[] { "ex", "exit", "q", "quit" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdSelect,                       new string[] { "select" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdSleep,                        new string[] { "sleep" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdStatus,                       new string[] { "status" }));

            // Api commands...
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiClosesession,              new string[] { "close", "closesession", "closeSession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiCreatesession,             new string[] { "create", "createsession", "createSession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiGetsession,                new string[] { "get", "getsession", "getSession" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiInfoex,                    new string[] { "info", "infoex" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiReadimageblockmetadata,    new string[] { "readimageblockmetadata", "readImageBlockMetadata" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiReadimageblock,            new string[] { "readimageblock", "readImageBlock" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiReleaseimageblocks,        new string[] { "release", "releaseimageblocks", "releaseImageBlocks" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiSendtask,                  new string[] { "send", "sendtask", "sendTask" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiStartcapturing,            new string[] { "start", "startcapturing", "startCapturing" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiStopcapturing,             new string[] { "stop", "stopcapturing", "stopCapturing" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdApiWaitforevents,             new string[] { "wait", "waitforevents", "waitForEvents" }));

            // Scripting...
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdIf,                           new string[] { "if" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdRun,                          new string[] { "run" }));
            m_ldispatchtable.Add(new Interpreter.DispatchTable(CmdSet,                          new string[] { "set" }));

            // Say hi...
            Assembly assembly = typeof(Terminal).Assembly;
            AssemblyName assemblyname = assembly.GetName();
            Version version = assemblyname.Version;
            DateTime datetime = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.MinorRevision * 2);

            Console.Out.WriteLine("TWAIN Direct Certification v" + version.Major + "." + version.Minor + " " + datetime.ToShortDateString() + " " + ((IntPtr.Size == 4) ? " (32-bit)" : " (64-bit)"));
            Console.Out.WriteLine("Enter \"help\" for more info.");
        }

        /// <summary>
        /// Run the certification tool...
        /// </summary>
        public void Run()
        {
            string szPrompt = "tdc";
            Interpreter interpreter = new Interpreter(szPrompt + ">>> ");

            // Run until told to stop...
            while (true)
            {
                int iCmd;
                bool blDone;
                string szCmd;
                string[] aszCmd;

                // Prompt...
                szCmd = interpreter.Prompt();

                // Tokenize...
                aszCmd = interpreter.Tokenize(szCmd);

                // Expansion...
                for (iCmd = 0; iCmd < aszCmd.Length; iCmd++)
                {
                    // Use the value as a JSON key to get data from the response data...
                    string szValue = aszCmd[iCmd];
                    if (szValue.StartsWith("rj:"))
                    {
                        if (m_ltransations.Count > 0)
                        {
                            string szResponseData = m_ltransations[m_ltransations.Count - 1].GetResponseData();
                            if (!string.IsNullOrEmpty(szResponseData))
                            {
                                bool blSuccess;
                                long lJsonErrorIndex;
                                JsonLookup jsonlookup = new JsonLookup();
                                blSuccess = jsonlookup.Load(szResponseData, out lJsonErrorIndex);
                                if (blSuccess)
                                {
                                    aszCmd[iCmd] = jsonlookup.Get(szValue.Substring(3));
                                }
                            }
                        }
                    }

                    // Use value as a GET key to get a value, we don't allow a null in this
                    // case, it has to be an empty string...
                    else if (szValue.StartsWith("get:"))
                    {
                        if (m_lkeyvalue.Count == 0)
                        {
                            aszCmd[iCmd] = "";
                        }
                        else
                        {
                            bool blFound = false;
                            string szKey = szValue.Substring(4);
                            foreach (KeyValue keyvalue in m_lkeyvalue)
                            {
                                if (keyvalue.szKey == szKey)
                                {
                                    aszCmd[iCmd] = (keyvalue.szValue == null) ? "" : keyvalue.szValue;
                                    blFound = true;
                                    break;
                                }
                            }
                            if (!blFound)
                            {
                                aszCmd[iCmd] = "";

                            }
                        }
                    }
                }

                // Dispatch...
                Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
                functionarguments.aszCmd = aszCmd;
                blDone = interpreter.Dispatch(ref functionarguments, m_ldispatchtable);
                if (blDone)
                {
                    return;
                }

                // Update the prompt with state information...
                if (m_twainlocalscanner == null)
                {
                    interpreter.SetPrompt(szPrompt + ">>> ");
                }
                else
                {
                    switch (m_twainlocalscanner.GetState())
                    {
                        default: interpreter.SetPrompt(szPrompt + "." + m_twainlocalscanner.GetState() + ">>> "); break;
                        case "noSession": interpreter.SetPrompt(szPrompt + ">>> "); break;
                        case "ready": interpreter.SetPrompt(szPrompt + ".rdy>>> "); break;
                        case "capturing": interpreter.SetPrompt(szPrompt + ".cap>>> "); break;
                        case "draining": interpreter.SetPrompt(szPrompt + ".drn>>> "); break;
                        case "closed": interpreter.SetPrompt(szPrompt + ".cls>>> "); break;
                    }
                }
            }
        }

        #endregion


        // Private Methods (api)
        #region Private Methods (api)

        /// <summary>
        /// Close a session...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiClosesession(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerCloseSession(ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Create a session...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiCreatesession(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerCreateSession(m_dnssddeviceinfoSelected, ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Get the current session object
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiGetsession(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerGetSession(ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send an infoex command to the selected scanner...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiInfoex(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientInfo(m_dnssddeviceinfoSelected, ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Read an image data block's metadata and thumbnail...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiReadimageblockmetadata(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            long lImageBlock;
            bool blGetThumbnail;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }
            if (a_functionarguments.aszCmd.Length < 3)
            {
                Console.Out.WriteLine("please specify image block to read and thumbnail flag...");
                return (false);
            }

            // Get the image block number...
            if (!long.TryParse(a_functionarguments.aszCmd[1], out lImageBlock))
            {
                Console.Out.WriteLine("image block must be a number...");
                return (false);
            }
            if (!bool.TryParse(a_functionarguments.aszCmd[2], out blGetThumbnail))
            {
                Console.Out.WriteLine("thumbnail flag must be true or false...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerReadImageBlockMetadata(lImageBlock, blGetThumbnail, null, ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Read an image data block and it's metadata...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiReadimageblock(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            long lImageBlock;
            bool blGetMetadataWithImage;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }
            if (a_functionarguments.aszCmd.Length < 3)
            {
                Console.Out.WriteLine("please specify image block to read and thumbnail flag...");
                return (false);
            }

            // Get the image block number...
            if (!long.TryParse(a_functionarguments.aszCmd[1], out lImageBlock))
            {
                Console.Out.WriteLine("image block must be a number...");
                return (false);
            }
            if (!bool.TryParse(a_functionarguments.aszCmd[2], out blGetMetadataWithImage))
            {
                Console.Out.WriteLine("getmetdata flag must be true or false...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerReadImageBlock(lImageBlock, blGetMetadataWithImage, null, ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Release or or more image blocks, or all image blocks...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiReleaseimageblocks(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            long lFirstImageBlock;
            long lLastImageBlock;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }
            if (a_functionarguments.aszCmd.Length < 3)
            {
                Console.Out.WriteLine("please specify the first and last image block to release...");
                return (false);
            }

            // Get the values...
            if (!long.TryParse(a_functionarguments.aszCmd[1], out lFirstImageBlock))
            {
                Console.Out.WriteLine("first image block must be a number...");
                return (false);
            }
            if (!long.TryParse(a_functionarguments.aszCmd[2], out lLastImageBlock))
            {
                Console.Out.WriteLine("last image block must be a number...");
                return (false);
            }

            // Loop so we can handle the release-all scenerio...
            while (true)
            {
                // Make the call...
                apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
                m_twainlocalscanner.ClientScannerReleaseImageBlocks(lFirstImageBlock, lLastImageBlock, ref apicmd);

                // Squirrel away the transaction...
                m_ltransations.Add(apicmd.GetTransaction());

                // Scoot...
                if ((lFirstImageBlock != 1) || (lLastImageBlock != int.MaxValue))
                {
                    break;
                }

                // Otherwise, we'll only scoot if we're out of images, we
                // must be in a draining state for this to be allowed...
                if (apicmd.GetSessionState() != "draining")
                {
                    break;
                }

                // If the flag says we're done, then we're done...
                if (apicmd.GetImageBlocksDrained())
                {
                    break;
                }

                // Wait a little before beating up the scanner with another attempt...
                Thread.Sleep(1000);
            }

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Send task...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiSendtask(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;
            string szTask;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Must supply a task...
            if ((a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                Console.Out.WriteLine("must supply a task...");
                return (false);
            }

            // Is the argument a file?
            if (File.Exists(a_functionarguments.aszCmd[1]))
            {
                try
                {
                    szTask = File.ReadAllText(a_functionarguments.aszCmd[1]);
                }
                catch (Exception exception)
                {
                    Console.Out.WriteLine("failed to open file...<" + a_functionarguments.aszCmd[1] + "> - " + exception.Message);
                    return (false);
                }
            }
            else
            {
                szTask = a_functionarguments.aszCmd[1];
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerSendTask(szTask, ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Start capturing...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiStartcapturing(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerStartCapturing(ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Stop capturing...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiStopcapturing(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerStopCapturing(ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        /// <summary>
        /// Wait for events, like changes to the session object...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdApiWaitforevents(ref Interpreter.FunctionArguments a_functionarguments)
        {
            ApiCmd apicmd;

            // Validate...
            if ((m_dnssddeviceinfoSelected == null) || (m_twainlocalscanner == null))
            {
                Console.Out.WriteLine("must first select a scanner...");
                return (false);
            }

            // Make the call...
            apicmd = new ApiCmd(m_dnssddeviceinfoSelected);
            m_twainlocalscanner.ClientScannerWaitForEvents(ref apicmd);

            // Squirrel away the transaction...
            m_ltransations.Add(apicmd.GetTransaction());

            // Display what we send...
            DisplayApicmd(apicmd);

            // All done...
            return (false);
        }

        #endregion


        // Private Methods (commands)
        #region Private Methods (commands)

        /// <summary>
        /// Goto the user...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdGoto(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iLine;
            string szLabel;

            // Validate...
            if (    (a_functionarguments.aszScript == null)
                ||  (a_functionarguments.aszScript.Length < 2)
                ||  (a_functionarguments.aszScript[0] == null)
                ||  (a_functionarguments.aszCmd == null)
                ||  (a_functionarguments.aszCmd.Length < 2)
                ||  (a_functionarguments.aszCmd[1] == null))
            {
                return (false);
            }

            // Search for a match...
            szLabel = ":" + a_functionarguments.aszCmd[1];
            for (iLine = 0; iLine < a_functionarguments.aszScript.Length; iLine++)
            {
                if (a_functionarguments.aszScript[iLine].Trim() == szLabel)
                {
                    a_functionarguments.blGotoLabel = true;
                    a_functionarguments.iLabelLine = iLine;
                    return (false);
                }
            }

            // Ugh...
            Console.Out.WriteLine("goto label not found: <" + szLabel + ">");
            return (false);
        }

        /// <summary>
        /// Help the user...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdHelp(ref Interpreter.FunctionArguments a_functionarguments)
        {
            string szCommand;

            // Summary...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                Console.Out.WriteLine("Discovery and Selection");
                Console.Out.WriteLine("help.........................................this text");
                Console.Out.WriteLine("list.........................................list scanners");
                Console.Out.WriteLine("quit.........................................exit the program");
                Console.Out.WriteLine("select {pattern}.............................select a scanner");
                Console.Out.WriteLine("status.......................................status of the program");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Image Capture APIs (in order of use)");
                Console.Out.WriteLine("infoex.......................................get information about the scanner");
                Console.Out.WriteLine("createSession................................create a new session");
                Console.Out.WriteLine("getSession...................................show the current session object");
                Console.Out.WriteLine("waitForEvents................................wait for events, like session object changes");
                Console.Out.WriteLine("sendTask {task|file}.........................send task");
                Console.Out.WriteLine("startCapturing...............................start capturing new images");
                Console.Out.WriteLine("readImageBlockMetadata {block} {thumbnail}...read metadata for a block");
                Console.Out.WriteLine("readImageBlock {block} {metadata}............read image data block");
                Console.Out.WriteLine("releaseImageBlocks {first} {last}............release images blocks in the scanner");
                Console.Out.WriteLine("stopCapturing................................stop capturing new images");
                Console.Out.WriteLine("closeSession.................................close the current session");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Scripting");
                Console.Out.WriteLine("if {item1} {operator} {item2} goto {label}...if statement");
                Console.Out.WriteLine("run [script].................................run a script");
                return (false);
            }

            // Get the command...
            szCommand = a_functionarguments.aszCmd[1].ToLower();

            // Discovery and Selection
            #region Discovery and Selection

            // Help...
            if ((szCommand == "help"))
            {
                Console.Out.WriteLine("HELP [COMMAND]");
                Console.Out.WriteLine("Provides assistence with command and their arguments.  It does not");
                Console.Out.WriteLine("go into detail on TWAIN Direct.  Please read the Specifications for");
                Console.Out.WriteLine("more information.");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Curly brackets {} indicate mandatory arguments to a command.  Square");
                Console.Out.WriteLine("brackets [] indicate optional arguments.");
                return (false);
            }

            // List...
            if ((szCommand == "list"))
            {
                Console.Out.WriteLine("LIST");
                Console.Out.WriteLine("List the scanners that are advertising themselves.  Note that the");
                Console.Out.WriteLine("same scanner make be seen multiple times, if it's being advertised");
                Console.Out.WriteLine("on more than one network.");
                return (false);
            }

            // Quit...
            if ((szCommand == "quit"))
            {
                Console.Out.WriteLine("QUIT");
                Console.Out.WriteLine("Exit from this program.");
                return (false);
            }

            // Select...
            if ((szCommand == "select"))
            {
                Console.Out.WriteLine("SELECT {PATTERN}");
                Console.Out.WriteLine("Selects one of the scanners shown in the list command, which is");
                Console.Out.WriteLine("the scanner that will be accessed by the API commands.  The pattern");
                Console.Out.WriteLine("must match some or all of the name, the IP address, or the note.");
                return (false);
            }

            // Status...
            if ((szCommand == "status"))
            {
                Console.Out.WriteLine("STATUS");
                Console.Out.WriteLine("General information about the current operation of the program.");
                return (false);
            }

            #endregion

            // Image Capture APIs (in order of use)
            #region Image Capture APIs (in order of use)

            // infoex...
            if ((szCommand == "infoex"))
            {
                Console.Out.WriteLine("INFOEX");
                Console.Out.WriteLine("Issues an infoex command to the scanner that picked out using");
                Console.Out.WriteLine("the SELECT command.  The command must be issued before making");
                Console.Out.WriteLine("a call to CREATESESSION.");
                return (false);
            }

            // createSession...
            if ((szCommand == "createsession"))
            {
                Console.Out.WriteLine("CREATESESSION");
                Console.Out.WriteLine("Creates a session for the scanner picked out using the SELECT");
                Console.Out.WriteLine("command.  To end the session use CLOSESESSION.");
                return (false);
            }

            // getSession...
            if ((szCommand == "getsession"))
            {
                Console.Out.WriteLine("GETSESSION");
                Console.Out.WriteLine("Gets infornation about the current session.");
                return (false);
            }

            // waitForEvents...
            if ((szCommand == "waitforevents"))
            {
                Console.Out.WriteLine("WAITFOREVENTS");
                Console.Out.WriteLine("TWAIN Direct is event driven.  The command creates the event");
                Console.Out.WriteLine("monitor used to detect updates to the session object.  It");
                Console.Out.WriteLine("should be called once after CREATESESSION.");
                return (false);
            }

            // sendTask...
            if ((szCommand == "sendtask"))
            {
                Console.Out.WriteLine("SENDTASK {TASK|FILE}");
                Console.Out.WriteLine("Sends a TWAIN Direct task.  The argument can either be the");
                Console.Out.WriteLine("task itself, or a file containing the task.");
                return (false);
            }

            // startCapturing...
            if ((szCommand == "startcapturing"))
            {
                Console.Out.WriteLine("STARTCAPTURING");
                Console.Out.WriteLine("Start capturing images from the scanner.");
                return (false);
            }

            // readImageBlockMetadata...
            if ((szCommand == "readimageblockmetadata"))
            {
                Console.Out.WriteLine("READIMAGEBLOCKMETADATA {BLOCK} {INCLUDETHUMBNAIL}");
                Console.Out.WriteLine("Reads the metadata for the specified image BLOCK, and");
                Console.Out.WriteLine("optionally includes a thumbnail for that image.");
                return (false);
            }

            // readImageBlock...
            if ((szCommand == "readimageblock"))
            {
                Console.Out.WriteLine("READIMAGEBLOCK {BLOCK} {INCLUDEMETADATA}");
                Console.Out.WriteLine("Reads the image data for the specified image BLOCK, and");
                Console.Out.WriteLine("optionally includes the metadata for that image.");
                return (false);
            }

            // releaseImageBlocks...
            if ((szCommand == "releaseimageblocks"))
            {
                Console.Out.WriteLine("RELEASEIMAGEBLOCKS {FIRST} {LAST}");
                Console.Out.WriteLine("Releases the image blocks from FIRST to LAST inclusive.");
                return (false);
            }

            // stopCapturing...
            if ((szCommand == "stopCapturing"))
            {
                Console.Out.WriteLine("STOPCAPTURING");
                Console.Out.WriteLine("Stop capturing images from the scanner.");
                return (false);
            }

            // closeSession...
            if ((szCommand == "closeSession"))
            {
                Console.Out.WriteLine("CLOSESESSION");
                Console.Out.WriteLine("Close the session, which unlocks the scanner.  The user");
                Console.Out.WriteLine("is responsible for releasing any remaining images.");
                return (false);
            }

            #endregion

            // Scripting
            #region Scripting

            // if...
            if ((szCommand == "if"))
            {
                Console.Out.WriteLine("IF {ITEM1} {OPERATOR} {ITEM2} GOTO {LABEL}");
                Console.Out.WriteLine("If the operator for ITEM1 and ITEM2 is true, then goto the");
                Console.Out.WriteLine("label.  For the best experience get in the habit of putting");
                Console.Out.WriteLine("either single or double quotes around the items.");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Operators");
                Console.Out.WriteLine("==....values are equal (case sensitive)");
                Console.Out.WriteLine("~~....values are equal (case insensitive)");
                Console.Out.WriteLine("!=....values are not equal (case sensitive)");
                Console.Out.WriteLine("!~....values are not equal (case insensitive)");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Items");
                Console.Out.WriteLine("Items prefixed with 'rj:' indicate that the item is a JSON");
                Console.Out.WriteLine("key in the last command's response payload.  For instance:");
                Console.Out.WriteLine("  if 'rj:results.success' != 'true' goto FAIL");
                Console.Out.WriteLine("Items prefixed with 'get:' indicate that the item is the");
                Console.Out.WriteLine("result of a prior set command.");
                Console.Out.WriteLine("  if 'get:lastsuccess' != 'true' goto FAIL");
                return (false);
            }

            // Run...
            if ((szCommand == "run"))
            {
                Console.Out.WriteLine("RUN [SCRIPT]");
                Console.Out.WriteLine("Runs the specified script.  SCRIPT is the full path to the script");
                Console.Out.WriteLine("to be run.  If a SCRIPT is not specified, the scripts in the");
                Console.Out.WriteLine("current folder are listed.");
                return (false);
            }

            // Set...
            if ((szCommand == "set"))
            {
                Console.Out.WriteLine("SET {KEY} {VALUE}");
                Console.Out.WriteLine("Set a key to the specified value.  If a KEY is not specified");
                Console.Out.WriteLine("all of the current keys are listed with their values.");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Values");
                Console.Out.WriteLine("Values prefixed with 'rj:' indicate that the item is a JSON");
                Console.Out.WriteLine("key in the last command's response payload.  For instance:");
                Console.Out.WriteLine("  set success 'rj:results.success'");
                return (false);
            }

            #endregion

            // Well, this ain't good...
            Console.Out.WriteLine("unrecognized command: " + a_functionarguments.aszCmd[1]);

            // All done...
            return (false);
        }

        /// <summary>
        /// Process an if-statement...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdIf(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blDoAction = false;
            string szItem1;
            string szItem2;
            string szOperator;
            string szAction;

            // Validate...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 4) || (a_functionarguments.aszCmd[1] == null))
            {
                Console.Out.WriteLine("badly formed if-statement...");
                return (false);
            }

            // Get all of the stuff...
            szItem1 = a_functionarguments.aszCmd[1];
            szOperator = a_functionarguments.aszCmd[2];
            szItem2 = a_functionarguments.aszCmd[3];
            szAction = a_functionarguments.aszCmd[4];

            // Items must match (case sensitive)...
            if (szOperator == "==")
            {
                if (szItem1 == szItem2)
                {
                    blDoAction = true;
                }
            }

            // Items must match (case insensitive)...
            else if (szOperator == "~~")
            {
                if (szItem1.ToLowerInvariant() == szItem2.ToLowerInvariant())
                {
                    blDoAction = true;
                }
            }

            // Items must not match (case sensitive)...
            else if (szOperator == "!=")
            {
                if (szItem1 != szItem2)
                {
                    blDoAction = true;
                }
            }

            // Items must not match (case insensitive)...
            else if (szOperator == "!~")
            {
                if (szItem1.ToLowerInvariant() != szItem2.ToLowerInvariant())
                {
                    blDoAction = true;
                }
            }

            // Unrecognized operator...
            else
            {
                Console.Out.WriteLine("unrecognized operator: <" + szOperator + ">");
                return (false);
            }

            // We've been told to do the action...
            if (blDoAction)
            {
                // We're doing a goto...
                if (szAction.ToLowerInvariant() == "goto")
                {
                    int iLine;
                    string szLabel;

                    // Validate...
                    if ((a_functionarguments.aszCmd.Length < 5) || string.IsNullOrEmpty(a_functionarguments.aszCmd[4]))
                    {
                        Console.Out.WriteLine("goto label is missing...");
                        return (false);
                    }

                    // Find the label...
                    szLabel = ":" + a_functionarguments.aszCmd[5];
                    for (iLine = 0; iLine < a_functionarguments.aszScript.Length; iLine++)
                    {
                        if (a_functionarguments.aszScript[iLine].Trim() == szLabel)
                        {
                            a_functionarguments.blGotoLabel = true;
                            a_functionarguments.iLabelLine = iLine;
                            return (false);
                        }
                    }

                    // Ugh...
                    Console.Out.WriteLine("goto label not found: <" + szLabel + ">");
                    return (false);
                }

                // We have no idea what we're doing...
                else
                {
                    Console.Out.WriteLine("unrecognized action: <" + szAction + ">");
                    return (false);
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// List scanners, both ones on the LAN and ones that are
        /// available in the cloud (when we get that far)...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdList(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blUpdated;

            // Get a snapshot of the TWAIN Local scanners...
            m_adnssddeviceinfoSnapshot = m_dnssd.GetSnapshot(null, out blUpdated);

            // Display TWAIN Local...
            if (!m_blSilent)
            {
                if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
                {
                    Console.Out.WriteLine("*** no TWAIN Local scanners ***");
                }
                else
                {
                    foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
                    {
                        Console.Out.WriteLine(dnssddeviceinfo.szLinkLocal + " " + (!string.IsNullOrEmpty(dnssddeviceinfo.szIpv4) ? dnssddeviceinfo.szIpv4 : dnssddeviceinfo.szIpv6) + " " + dnssddeviceinfo.szTxtNote);
                    }
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Quit...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdQuit(ref Interpreter.FunctionArguments a_functionarguments)
        {
            return (true);
        }

        /// <summary>
        /// With no arguments, list the scripts.  With an argument,
        /// run the specified script.
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdRun(ref Interpreter.FunctionArguments a_functionarguments)
        {
            string szPrompt = "tdc>>> ";
            string[] aszScript;
            string szScriptFile;
            Interpreter interpreter;

            // List...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                // Get the script files...
                string[] aszScriptFiles = Directory.GetFiles(".", "*.tdc");
                if ((aszScriptFiles == null) || (aszScriptFiles.Length == 0))
                {
                    Console.Out.WriteLine("no script files found");
                }

                // List what we found...
                Console.Out.WriteLine("SCRIPT FILES");
                foreach (string sz in aszScriptFiles)
                {
                    Console.Out.WriteLine(sz.Replace(".tdc",""));
                }

                // All done...
                return (false);
            }

            // Make sure the file exists...
            szScriptFile = a_functionarguments.aszCmd[1];
            if (!File.Exists(szScriptFile))
            {
                szScriptFile = a_functionarguments.aszCmd[1] + ".tdc";
                if (!File.Exists(szScriptFile))
                {
                    Console.Out.WriteLine("script not found");
                    return (false);
                }
            }

            // Read the file...
            try
            {
                aszScript = File.ReadAllLines(szScriptFile);
            }
            catch (Exception exception)
            {
                Console.Out.WriteLine("failed to read script: " + exception.Message);
                return (false);
            }

            // Give ourselves an interpreter...
            interpreter = new Interpreter("");

            // Run each line in the script...
            int iLine = 0;
            while (iLine < aszScript.Length)
            {
                int iCmd;
                bool blDone;
                string szLine;
                string[] aszCmd;

                // Grab our line...
                szLine = aszScript[iLine];

                // Show the command...
                Console.Out.WriteLine(szPrompt + szLine.Trim());

                // Tokenize...
                aszCmd = interpreter.Tokenize(szLine.Trim());

                // Expansion...
                for (iCmd = 0; iCmd < aszCmd.Length; iCmd++)
                {
                    // Use the value as a JSON key to get data from the response data...
                    string szValue = aszCmd[iCmd];
                    if (szValue.StartsWith("rj:"))
                    {
                        if (m_ltransations.Count > 0)
                        {
                            string szResponseData = m_ltransations[m_ltransations.Count - 1].GetResponseData();
                            if (!string.IsNullOrEmpty(szResponseData))
                            {
                                bool blSuccess;
                                long lJsonErrorIndex;
                                JsonLookup jsonlookup = new JsonLookup();
                                blSuccess = jsonlookup.Load(szResponseData, out lJsonErrorIndex);
                                if (blSuccess)
                                {
                                    aszCmd[iCmd] = jsonlookup.Get(szValue.Substring(3));
                                }
                            }
                        }
                    }

                    // Use value as a GET key to get a value...
                    else if (szValue.StartsWith("get:"))
                    {
                        if (m_lkeyvalue.Count == 0)
                        {
                            aszCmd[iCmd] = "";
                        }
                        else
                        {
                            bool blFound = false;
                            string szKey = szValue.Substring(4);
                            foreach (KeyValue keyvalue in m_lkeyvalue)
                            {
                                if (keyvalue.szKey == szKey)
                                {
                                    aszCmd[iCmd] = (keyvalue.szValue == null) ? "" : keyvalue.szValue;
                                    blFound = true;
                                    break;
                                }
                            }
                            if (!blFound)
                            {
                                aszCmd[iCmd] = "";
                            }
                        }
                    }
                }

                // Dispatch...
                Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
                functionarguments.aszCmd = aszCmd;
                functionarguments.aszScript = aszScript;
                blDone = interpreter.Dispatch(ref functionarguments, m_ldispatchtable);
                if (blDone)
                {
                    break;
                }

                // Handle gotos...
                if (functionarguments.blGotoLabel)
                {
                    iLine = functionarguments.iLabelLine;
                }
                // Otherwise, just increment...
                else
                {
                    iLine += 1;
                }

                // Update the prompt with state information...
                if (m_twainlocalscanner == null)
                {
                    szPrompt = "tdc>>> ";
                }
                else
                {
                    switch (m_twainlocalscanner.GetState())
                    {
                        default: szPrompt = "tdc." + m_twainlocalscanner.GetState() + ">>> "; break;
                        case "noSession": szPrompt = "tdc>>> "; break;
                        case "ready": szPrompt = "tdc.rdy>>> "; break;
                        case "capturing": szPrompt = "tdc.cap>>> "; break;
                        case "draining": szPrompt = "tdc.drn>>> "; break;
                        case "closed": szPrompt = "tdc.cls>>> "; break;
                    }
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Select a scanner, do a snapshot, if needed, if no selection
        /// is offered, then pick the first scanner found...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdSelect(ref Interpreter.FunctionArguments a_functionarguments)
        {
            bool blSilent;

            // Clear the transactions...
            m_ltransations = new List<ApiCmd.Transaction>();

            // Clear the last selected scanner...
            m_dnssddeviceinfoSelected = null;
            if (m_twainlocalscanner != null)
            {
                m_twainlocalscanner.Dispose();
                m_twainlocalscanner = null;
            }

            // If we don't have a snapshot, get one...
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                blSilent = m_blSilent;
                m_blSilent = true;
                Interpreter.FunctionArguments functionarguments = default(Interpreter.FunctionArguments);
                CmdList(ref functionarguments);
                m_blSilent = blSilent;
            }

            // No joy...
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                Console.Out.WriteLine("*** no TWAIN Local scanners ***");
                return (false);
            }

            // We didn't get a selection, so grab the first item...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || string.IsNullOrEmpty(a_functionarguments.aszCmd[1]))
            {
                m_dnssddeviceinfoSelected = m_adnssddeviceinfoSnapshot[0];
                return (false);
            }

            // Look for a match...
            foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
            {
                // Check the name...
                if (!string.IsNullOrEmpty(dnssddeviceinfo.szLinkLocal) && dnssddeviceinfo.szLinkLocal.Contains(a_functionarguments.aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }

                // Check the IPv4...
                else if (!string.IsNullOrEmpty(dnssddeviceinfo.szIpv4) && dnssddeviceinfo.szIpv4.Contains(a_functionarguments.aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }

                // Check the note...
                else if (!string.IsNullOrEmpty(dnssddeviceinfo.szTxtNote) && dnssddeviceinfo.szTxtNote.Contains(a_functionarguments.aszCmd[1]))
                {
                    m_dnssddeviceinfoSelected = dnssddeviceinfo;
                    break;
                }
            }

            // Report the result...
            if (m_dnssddeviceinfoSelected != null)
            {
                Console.Out.WriteLine(m_dnssddeviceinfoSelected.szLinkLocal + " " + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.szIpv4) ? m_dnssddeviceinfoSelected.szIpv4 : m_dnssddeviceinfoSelected.szIpv6) + " " + m_dnssddeviceinfoSelected.szTxtNote);
                m_twainlocalscanner = new TwainLocalScanner(null, 1, null, null, null);
            }
            else
            {
                Console.Out.WriteLine("*** no selection matches ***");
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// With no arguments, list the keys with their values.  With an argument,
        /// set the specified value.
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdSet(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iKey;

            // If we don't have any arguments, list what we have...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || (a_functionarguments.aszCmd[1] == null))
            {
                if (m_lkeyvalue.Count == 0)
                {
                    Console.Out.WriteLine("no keys to list...");
                    return (false);
                }

                // Loopy...
                Console.Out.WriteLine("KEY/VALUE PAIRS");
                foreach (KeyValue keyvalue in m_lkeyvalue)
                {
                    Console.Out.WriteLine(keyvalue.szKey + "=" + keyvalue.szValue);
                }

                // All done...
                return (false);
            }

            // Find the value for this key...
            for (iKey = 0; iKey < m_lkeyvalue.Count; iKey++)
            {
                if (m_lkeyvalue[iKey].szKey == a_functionarguments.aszCmd[1])
                {
                    break;
                }
            }

            // If we have no value to set, then delete this item...
            if ((a_functionarguments.aszCmd.Length < 3) || (a_functionarguments.aszCmd[2] == null))
            {
                if (iKey < m_lkeyvalue.Count)
                {
                    m_lkeyvalue.Remove(m_lkeyvalue[iKey]);
                }
                return (false);
            }

            // Create a new keyvalue...
            KeyValue keyvalueNew = new KeyValue();
            keyvalueNew.szKey = a_functionarguments.aszCmd[1];
            keyvalueNew.szValue = a_functionarguments.aszCmd[2];

            // If the key already exists, update it's value...
            if (iKey < m_lkeyvalue.Count)
            {
                m_lkeyvalue[iKey] = keyvalueNew;
                return (false);
            }

            // Otherwise, add it, and sort...
            m_lkeyvalue.Add(keyvalueNew);
            m_lkeyvalue.Sort(SortByKeyAscending);

            // All done...
            return (false);
        }

        /// <summary>
        /// A comparison operator for sorting keys in CmdSet...
        /// </summary>
        /// <param name="name1"></param>
        /// <param name="name2"></param>
        /// <returns></returns>
        private int SortByKeyAscending(KeyValue a_keyvalue1, KeyValue a_keyvalue2)
        {

            return (a_keyvalue1.szKey.CompareTo(a_keyvalue2.szKey));
        }

        /// <summary>
        /// Sleep some number of milliseconds...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdSleep(ref Interpreter.FunctionArguments a_functionarguments)
        {
            int iMilliseconds;

            // Get the milliseconds...
            if ((a_functionarguments.aszCmd == null) || (a_functionarguments.aszCmd.Length < 2) || !int.TryParse(a_functionarguments.aszCmd[1], out iMilliseconds))
            {
                iMilliseconds = 0;
            }
            if (iMilliseconds < 0)
            {
                iMilliseconds = 0;
            }

            // Wait...
            Thread.Sleep(iMilliseconds);

            // All done...
            return (false);
        }

        /// <summary>
        /// Status of the program...
        /// </summary>
        /// <param name="a_functionarguments">tokenized command and anything needed</param>
        /// <returns>true to quit</returns>
        private bool CmdStatus(ref Interpreter.FunctionArguments a_functionarguments)
        {
            // Current scanner...
            Console.Out.WriteLine("SELECTED SCANNER");
            if (m_dnssddeviceinfoSelected == null)
            {
                Console.Out.WriteLine("*** no selected scanner ***");
            }
            else
            {
                Console.Out.WriteLine(m_dnssddeviceinfoSelected.szLinkLocal + " " + (!string.IsNullOrEmpty(m_dnssddeviceinfoSelected.szIpv4) ? m_dnssddeviceinfoSelected.szIpv4 : m_dnssddeviceinfoSelected.szIpv6) + " " + m_dnssddeviceinfoSelected.szTxtNote);
            }

            // Current snapshot of scanners...
            Console.Out.WriteLine("");
            Console.Out.WriteLine("LAST SCANNER LIST SNAPSHOT");
            if ((m_adnssddeviceinfoSnapshot == null) || (m_adnssddeviceinfoSnapshot.Length == 0))
            {
                Console.Out.WriteLine("*** no TWAIN Local scanners ***");
            }
            else
            {
                foreach (Dnssd.DnssdDeviceInfo dnssddeviceinfo in m_adnssddeviceinfoSnapshot)
                {
                    Console.Out.WriteLine(dnssddeviceinfo.szLinkLocal + " " + (!string.IsNullOrEmpty(dnssddeviceinfo.szIpv4) ? dnssddeviceinfo.szIpv4 : dnssddeviceinfo.szIpv6) + " " + dnssddeviceinfo.szTxtNote);
                }
            }

            // All done...
            return (false);
        }

        /// <summary>
        /// Display information about this apicmd object...
        /// </summary>
        /// <param name="a_apicmd">the object we want to display</param>
        private void DisplayApicmd
        (
            ApiCmd a_apicmd
        )
        {
            ApiCmd.Transaction transaction = new ApiCmd.Transaction(a_apicmd);
            List<string> lszTransation = transaction.GetAll();
            if (lszTransation != null)
            {
                foreach (string sz in lszTransation)
                {
                    Console.Out.WriteLine(sz);
                }
            }
        }

        #endregion


        // Private Methods (certification)
        #region Private Methods (certification)

        /// <summary>
        /// Run the TWAIN Certification tests.  
        /// </summary>
        private void TwainDirectCertification()
        {
            int ii;
            int iPass = 0;
            int iFail = 0;
            int iSkip = 0;
            int iTotal = 0;
            bool blSuccess;
            long lJsonErrorIndex;
            long lTaskIndex;
            string szCertificationFolder;
            string[] aszCategories;
            string[] aszTestFiles;
            string szTestData;
            string[] aszTestData;
            JsonLookup jsonlookupTest;
            JsonLookup jsonlookupReply;
            ApiCmd apicmd;

            // Find our cert stuff...
            szCertificationFolder = Path.Combine(Config.Get("writeFolder", ""), "tasks");
            szCertificationFolder = Path.Combine(szCertificationFolder, "certification");

            // Whoops...nothing to work with...
            if (!Directory.Exists(szCertificationFolder))
            {
                Console.Out.WriteLine("Cannot find certification folder:\n" + szCertificationFolder);
                return;
            }

            // Get the categories...
            aszCategories = Directory.GetDirectories(szCertificationFolder);
            if (aszCategories == null)
            {
                Console.Out.WriteLine("Cannot find any certification categories:\n" + szCertificationFolder);
                return;
            }

            // Loop the catagories...
            foreach (string szCategory in aszCategories)
            {
                // Get the tests...
                aszTestFiles = Directory.GetFiles(Path.Combine(szCertificationFolder, szCategory));
                if (aszTestFiles == null)
                {
                    continue;
                }

                // Loop the tests...
                foreach (string szTestFile in aszTestFiles)
                {
                    string szSummary;
                    string szStatus;

                    // Log it...
                    Log.Info("");
                    Log.Info("certification>>> file........................." + szTestFile);

                    // The total...
                    iTotal += 1;

                    // Add a new item to show what we're doing...
                    jsonlookupTest = new JsonLookup();

                    // Init stuff...
                    szSummary = Path.GetFileNameWithoutExtension(szTestFile);
                    szStatus = "skip";

                    // Load the test...
                    szTestData = File.ReadAllText(szTestFile);
                    if (string.IsNullOrEmpty(szTestData))
                    {
                        Log.Info("certification>>> status.......................skip (empty file)");
                        iSkip += 1;
                        continue;
                    }

                    // Split the data...
                    if (!szTestData.Contains("***DATADATADATA***"))
                    {
                        Log.Info("certification>>> status.......................skip (data error)");
                        iSkip += 1;
                        continue;
                    }
                    aszTestData = szTestData.Split(new string[] { "***DATADATADATA***\r\n", "***DATADATADATA***\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (aszTestData.Length != 2)
                    {
                        Log.Info("certification>>> status.......................skip (data error)");
                        iSkip += 1;
                        continue;
                    }

                    // Always start this part with a clean slate...
                    apicmd = new ApiCmd(m_dnssddeviceinfoSelected);

                    // Get our instructions...
                    blSuccess = jsonlookupTest.Load(aszTestData[0], out lJsonErrorIndex);
                    if (!blSuccess)
                    {
                        Log.Info("certification>>> status.......................skip (json error)");
                        iSkip += 1;
                        continue;
                    }

                    // Validate the instructions...
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("category")))
                    {
                        Log.Info("certification>>> status.......................ERROR (missing category)");
                        iSkip += 1;
                        continue;
                    }
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("summary")))
                    {
                        Log.Info("certification>>> status.......................skip (missing summary)");
                        iSkip += 1;
                        continue;
                    }
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("description")))
                    {
                        Log.Info("certification>>> status.......................skip (missing description)");
                        iSkip += 1;
                        continue;
                    }
                    if (string.IsNullOrEmpty(jsonlookupTest.Get("expects")))
                    {
                        Log.Info("certification>>> status.......................skip (missing expects)");
                        iSkip += 1;
                        continue;
                    }

                    // Log what we're doing...
                    Log.Info("certification>>> summary......................" + jsonlookupTest.Get("summary"));
                    Log.Info("certification>>> description.................." + jsonlookupTest.Get("description"));
                    for (ii = 0; ; ii++)
                    {
                        string szExpects = "expects[" + ii + "]";
                        if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects, false)))
                        {
                            break;
                        }
                        Log.Info("certification>>> " + szExpects + ".success..........." + jsonlookupTest.Get(szExpects + ".success"));
                        if (jsonlookupTest.Get(szExpects + ".success") == "false")
                        {
                            Log.Info("certification>>> " + szExpects + ".code.............." + jsonlookupTest.Get(szExpects + ".code"));
                            if (jsonlookupTest.Get(szExpects + ".code") == "invalidJson")
                            {
                                Log.Info("certification>>> " + szExpects + ".characterOffset..." + jsonlookupTest.Get(szExpects + ".characterOffset"));
                            }
                            if (jsonlookupTest.Get(szExpects + ".code") == "invalidValue")
                            {
                                Log.Info("certification>>> " + szExpects + ".jsonKey..........." + jsonlookupTest.Get(szExpects + ".jsonKey"));
                            }
                        }
                    }

                    // Make sure the last item is showing, and then show it...
                    szSummary = jsonlookupTest.Get("summary");
                    szStatus = "(running)";

                    // Perform the test...
                    blSuccess = m_twainlocalscanner.ClientScannerSendTask(aszTestData[1], ref apicmd);
                    if (!blSuccess)
                    {
                        //mlmtbd Add errror check...
                    }

                    // Figure out the index offset to the task, so that we don't
                    // have to dink with the certification tests if the API is
                    // changed for any reason.  Note that we're assuming that the
                    // API is packed...
                    string szSendCommand = apicmd.GetSendCommand();
                    lTaskIndex = (szSendCommand.IndexOf("\"task\":") + 7);

                    // Check out the reply...
                    string szHttpReplyData = apicmd.HttpResponseData();
                    jsonlookupReply = new JsonLookup();
                    blSuccess = jsonlookupReply.Load(szHttpReplyData, out lJsonErrorIndex);
                    if (!blSuccess)
                    {
                        Log.Info("certification>>> status.......................fail (json error)");
                        szStatus = "fail";
                        iFail += 1;
                        continue;
                    }

                    // Check for a task...
                    szHttpReplyData = jsonlookupReply.Get("results.session.task");
                    if (!string.IsNullOrEmpty(szHttpReplyData))
                    {
                        jsonlookupReply = new JsonLookup();
                        blSuccess = jsonlookupReply.Load(szHttpReplyData, out lJsonErrorIndex);
                        if (!blSuccess)
                        {
                            Log.Info("certification>>> status.......................fail (json error)");
                            szStatus = "fail";
                            iFail += 1;
                            continue;
                        }
                    }

                    // Loopy...
                    for (ii = 0; ; ii++)
                    {
                        // Make sure we have this entry...
                        string szExpects = "expects[" + ii + "]";
                        if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects, false)))
                        {
                            break;
                        }

                        // We need to bump the total for values of ii > 0, this handles
                        // tasks with multiple actions...
                        if (ii > 0)
                        {
                            iTotal += 1;
                        }

                        // We need the path to the results...
                        string szPath = jsonlookupTest.Get(szExpects + ".path");
                        if (string.IsNullOrEmpty(szPath))
                        {
                            szPath = "";
                        }
                        else
                        {
                            szPath += ".";
                        }

                        // The command is expected to succeed...
                        if (jsonlookupTest.Get(szExpects + ".success") == "true")
                        {
                            // Check success...
                            if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.success")))
                            {
                                Log.Info("certification>>> status.......................fail (missing " + szPath + "results.success)");
                                szStatus = "fail (missing " + szPath + "results.success)";
                                iFail += 1;
                            }
                            else if (jsonlookupReply.Get(szPath + "results.success") != "true")
                            {
                                Log.Info("certification>>> status.......................fail (expected " + szPath + "results.success to be 'true')");
                                szStatus = "fail (expected " + szPath + "results.success to be 'true')";
                                iFail += 1;
                            }
                            else
                            {
                                Log.Info("certification>>> status.......................pass");
                                szStatus = "pass";
                                 iPass += 1;
                            }
                        }

                        // The command is expected to fail...
                        else if (jsonlookupTest.Get(szExpects + ".success") == "false")
                        {
                            // Check success...
                            if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.success")))
                            {
                                Log.Info("certification>>> status.......................fail (missing " + szPath + "results.success)");
                                szStatus = "fail (missing " + szPath + "results.success)";
                                iFail += 1;
                            }
                            else if (jsonlookupReply.Get(szPath + "results.success") != "false")
                            {
                                Log.Info("certification>>> status.......................fail (expected " + szPath + "results.success to be 'false')");
                                szStatus = "fail (expected " + szPath + "results.success to be 'false')";
                                iFail += 1;
                            }

                            // Check the code...
                            else
                            {
                                switch (jsonlookupTest.Get(szExpects + ".code"))
                                {
                                    // Tell the programmer to fix their code or their tests...  :)
                                    default:
                                        Log.Info("certification>>> status.......................fail (no handler for this code '" + jsonlookupTest.Get(szExpects + ".code") + "')");
                                        iFail += 1;
                                        break;

                                    // JSON violations...
                                    case "invalidJson":
                                        if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.code")))
                                        {
                                            Log.Info("certification>>> status.......................fail (missing " + szPath + "results.code)");
                                            szStatus = "fail (missing " + szPath + "results.code)";
                                            iFail += 1;
                                        }
                                        else if (jsonlookupReply.Get(szPath + "results.code") == "invalidJson")
                                        {
                                            if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects + ".characterOffset")))
                                            {
                                                Log.Info("certification>>> status.......................fail (missing " + szExpects + ".characterOffset)");
                                                szStatus = "fail (missing " + szExpects + ".characterOffset)";
                                                iFail += 1;
                                            }
                                            else if (int.Parse(jsonlookupTest.Get(szExpects + ".characterOffset")) == (int.Parse(jsonlookupReply.Get(szPath + "results.characterOffset")) - lTaskIndex))
                                            {
                                                Log.Info("certification>>> status.......................pass");
                                                szStatus = "pass";
                                                iPass += 1;
                                            }
                                            else
                                            {
                                                Log.Info("certification>>> status.......................fail (" + szExpects + ".characterOffset wanted:" + jsonlookupTest.Get(szExpects + ".characterOffset") + " got:" + (int.Parse(jsonlookupReply.Get(szPath + "results.characterOffset")) - lTaskIndex).ToString() + ")");
                                                szStatus = "fail (" + szExpects + ".characterOffset wanted:" + jsonlookupTest.Get(szExpects + ".characterOffset") + " got:" + (int.Parse(jsonlookupReply.Get(szPath + "results.characterOffset")) - lTaskIndex).ToString() + ")";
                                                iFail += 1;
                                            }
                                        }
                                        else
                                        {
                                            Log.Info("certification>>> status.......................fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + ")");
                                            szStatus = "fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + "')";
                                            iFail += 1;
                                        }
                                        break;

                                    // TWAIN Direct violations...
                                    case "invalidTask":
                                        if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.code")))
                                        {
                                            Log.Info("certification>>> status.......................fail (missing " + szPath + "results.code)");
                                            szStatus = "fail (missing " + szPath + "results.code)";
                                            iFail += 1;
                                        }
                                        else if (jsonlookupReply.Get(szPath + "results.code") == "invalidTask")
                                        {
                                            if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects + ".jsonKey")))
                                            {
                                                Log.Info("certification>>> status.......................fail (missing " + szExpects + ".jsonKey)");
                                                szStatus = "fail (missing " + szExpects + "jsonKey)";
                                                iFail += 1;
                                            }
                                            else if (jsonlookupTest.Get(szExpects + ".jsonKey") == jsonlookupReply.Get(szPath + "results.jsonKey"))
                                            {
                                                Log.Info("certification>>> status.......................pass");
                                                szStatus = "pass";
                                                iPass += 1;
                                            }
                                            else
                                            {
                                                Log.Info("certification>>> status.......................fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey"));
                                                szStatus = "fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey");
                                                iFail += 1;
                                            }
                                        }
                                        else
                                        {
                                            Log.Info("certification>>> status.......................fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + ")");
                                            szStatus = "fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + "')";
                                            iFail += 1;
                                        }
                                        break;

                                    // invalidValue forced by exception...
                                    case "invalidValue":
                                        if (string.IsNullOrEmpty(jsonlookupReply.Get(szPath + "results.code")))
                                        {
                                            Log.Info("certification>>> status.......................fail (missing " + szPath + "results.code)");
                                            szStatus = "fail (missing " + szPath + "results.code)";
                                            iFail += 1;
                                        }
                                        else if (jsonlookupReply.Get(szPath + "results.code") == "invalidValue")
                                        {
                                            if (string.IsNullOrEmpty(jsonlookupTest.Get(szExpects + ".jsonKey")))
                                            {
                                                Log.Info("certification>>> status........................fail (missing " + szExpects + ".jsonKey)");
                                                szStatus = "fail (missing " + szExpects + ".jsonKey)";
                                                iFail += 1;
                                            }
                                            else if (jsonlookupTest.Get(szExpects + ".jsonKey") == jsonlookupReply.Get(szPath + "results.jsonKey"))
                                            {
                                                Log.Info("certification>>> status.......................pass");
                                                szStatus = "pass";
                                                iPass += 1;
                                            }
                                            else
                                            {
                                                Log.Info("certification>>> status.......................fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey"));
                                                szStatus = "fail (" + szExpects + ".jsonKey wanted:" + jsonlookupTest.Get(szExpects + ".jsonKey") + " got:" + jsonlookupReply.Get(szPath + "results.jsonKey");
                                                iFail += 1;
                                            }
                                        }
                                        else
                                        {
                                            Log.Info("certification>>> status.......................fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + ")");
                                            szStatus = "fail (" + szExpects + ".code wanted:" + jsonlookupTest.Get(szExpects + ".code") + " got:" + jsonlookupReply.Get(szPath + "results.code") + "')";
                                            iFail += 1;
                                        }
                                        break;
                                }
                            }
                        }

                        // Oops...
                        else
                        {
                            Log.Info("certification>>> status.......................fail (expectedSuccess must be 'true' or 'false')");
                            szStatus = "fail";
                            iFail += 1;
                        }
                    }
                }
            }

            // Pass count...
            Log.Info("certification>>> PASS: " + iPass);

            // Fail count...
            Log.Info("certification>>> FAIL: " + iFail);

            // Skip count...
            Log.Info("certification>>> SKIP: " + iSkip);

            // Total count...
            Log.Info("certification>>> TOTAL: " + iTotal);
        }

        #endregion


        // Private Definitions
        #region Private Definitions

        /// <summary>
        /// A key/value pair...
        /// </summary>
        private struct KeyValue
        {
            /// <summary>
            /// Our key...
            /// </summary>
            public string szKey;

            /// <summary>
            /// The key's value...
            /// </summary>
            public string szValue;
        }

        #endregion


        // Private Attributes
        #region Private Attributes

        /// <summary>
        /// Map commands to functions...
        /// </summary>
        private List<Interpreter.DispatchTable> m_ldispatchtable;

        /// <summary>
        /// A snapshot of the current available devices...
        /// </summary>
        private Dnssd.DnssdDeviceInfo[] m_adnssddeviceinfoSnapshot;

        /// <summary>
        /// Information about our device...
        /// </summary>
        private Dnssd.DnssdDeviceInfo m_dnssddeviceinfoSelected;

        /// <summary>
        /// The connection to our device...
        /// </summary>
        private TwainLocalScanner m_twainlocalscanner;

        /// <summary>
        /// Our object for discovering TWAIN Local scanners...
        /// </summary>
        private Dnssd m_dnssd;

        /// <summary>
        /// No output when this is true...
        /// </summary>
        private bool m_blSilent;

        /// <summary>
        /// A record of RESTful transactions with the scanner...
        /// </summary>
        private List<ApiCmd.Transaction> m_ltransations;

        /// <summary>
        /// The list of key/value pairs created by the SET command...
        /// </summary>
        private List<KeyValue> m_lkeyvalue;

        #endregion
    }
}
