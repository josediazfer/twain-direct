﻿///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Scanner.Form1
//
// This is our main form.  Our goal is to keep it pretty thin, it's sole purpose
// is to act as a presentation layer for when a windowing system is being used,
// so there's no business logic at this level...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    29-Nov-2014     Initial Release
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2020 Kodak Alaris Inc.
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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TwainDirect.Support;

namespace TwainDirect.Scanner
{
    public partial class FormMain : Form, IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Initialize stuff for our form...
        /// </summary>
        public FormMain(ResourceManager resourceManager)
        {
            m_resourcemanager = resourceManager;
            // Confirm scan, we check for the command line (confirmscan)
            // and for the appdata.txt file (useConfirmScan).  The default
            // is for it to be off...
            bool blConfirmScan = (Config.Get("confirmscan", null) != null) || (Config.Get("useConfirmScan", "no") == "yes");
            Log.Info("ConfirmScan: " + blConfirmScan);

            // Init our form...
            InitializeComponent();

            // Localize...
            this.Text = Config.GetResource(m_resourcemanager, "strFormMainTitle"); // TWAIN Direct: TWAIN Bridge
            m_buttonStart.Text = Config.GetResource(m_resourcemanager, "strButtonStart"); // Start
            m_buttonStop.Text = Config.GetResource(m_resourcemanager, "strButtonStop"); // Stop
            m_buttonSetup.Text = Config.GetResource(m_resourcemanager, "strButtonSetup"); // Setup

            // Context memory for the system tray...
            MenuItem menuitemOpen = new MenuItem(Config.GetResource(m_resourcemanager, "strMenuShowConsole")); // Open...
            MenuItem menuitemAbout = new MenuItem(Config.GetResource(m_resourcemanager, "strMenuAbout")); // About...
            MenuItem menuitemExit = new MenuItem(Config.GetResource(m_resourcemanager, "strMenuExit")); // Exit...
            menuitemOpen.Click += MenuitemOpen_Click;
            menuitemAbout.Click += MenuitemAbout_Click;
            menuitemExit.Click += MenuitemExit_Click; ;
            m_notifyicon.ContextMenu = new ContextMenu();
            m_notifyicon.ContextMenu.MenuItems.Add(menuitemOpen);
            m_notifyicon.ContextMenu.MenuItems.Add("-");
            m_notifyicon.ContextMenu.MenuItems.Add(menuitemAbout);
            m_notifyicon.ContextMenu.MenuItems.Add("-");
            m_notifyicon.ContextMenu.MenuItems.Add(menuitemExit);
            m_notifyicon.DoubleClick += m_notifyicon_DoubleClick;

            // Handle resizing...
            this.Resize += Form1_Resize;

            // Handle scaling...
            float fScale = (float)Config.Get("scale", 1.0);
            if (fScale <= 1)
            {
                fScale = 1;
            }
            else if (fScale > 2)
            {
                fScale = 2;
            }
            if (fScale != 1)
            {
                this.Font = new Font(this.Font.FontFamily, this.Font.Size * fScale, this.Font.Style);
            }
            Log.Info("Scale: " + fScale);

            // Events...
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);

            // Instantiate our scanner object...
            m_scanner = new Scanner
            (
                m_resourcemanager,
                Display,
                ConfirmScan,
                fScale,
                out m_blNoDevices
            );
            if (m_scanner == null)
            {
                Log.Error("Scanner failed...");
                throw new Exception("Scanner failed...");
            }

            // Instantiate our setup object...
            m_formsetup = new FormSetup(this, m_resourcemanager, m_scanner, Display, blConfirmScan);

            // Our current cloud...
            Display(Config.GetResource(m_resourcemanager, "strTextCurrentCloud") + " " + m_formsetup.GetCloudApiRoot());

            // If we don't have any devices, then don't let the user select
            // the start button...
            if (m_blNoDevices)
            {
                SetButtons(ButtonState.NoDevices);
            }
            else
            {
                SetButtons(ButtonState.WaitingForStart);
            }

            // Have we been asked to run in the background?
            if (Config.Get("background", "false") == "true")
            {
                Log.Info("Minimize us to the background...");
                WindowState = FormWindowState.Minimized;
                this.Hide();
            }

            // Have we been asked automatically start monitoring?
            if (Config.Get("startmonitoring", "false") == "true")
            {
                // Only if we have a device...
                if (!m_blNoDevices)
                {
                    Log.Info("Autostart monitoring...");
                    m_buttonStart_Click(m_buttonStart, null);
                }
            }
        }

        /// <summary>
        /// We have at least one device...
        /// </summary>
        /// <returns>true if we have a device</returns>
        public bool DevicesFound()
        {
            return (m_buttonStart.Enabled);
        }

        /// <summary>
        /// Get the current driver...
        /// </summary>
        /// <returns></returns>
        public string GetTwainLocalTy()
        {
            if (m_scanner == null)
            {
                return ("");
            }
            return (m_scanner.GetTwainLocalTy());
        }

        /// <summary>
        /// Get the current note...
        /// </summary>
        /// <returns></returns>
        public string GetTwainLocalNote()
        {
            if (m_scanner == null)
            {
                return ("");
            }
            return (m_scanner.GetTwainLocalNote());
        }

        /// <summary>
        /// Register a device with cloud infrastructure.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        public async Task RegisterCloud()
        {
            await m_scanner.RegisterCloudScanner();
        }

        /// <summary>
        /// Select the TWAIN driver to use...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SelectScanner()
        {
            int iScanner;
            long lResponseCharacterOffset;
            string szScanners;
            string szText;
            JsonLookup jsonlookup;
            ApiCmd apicmd;
            List<string> lszDrivers = new List<string>();

            // Turn the buttons off...
            SetButtons(ButtonState.Undefined);
            Display("");
            Display(Config.GetResource(m_resourcemanager, "errLookingForScanners"));

            // Get the list of scanners...
            szScanners = m_scanner.GetAvailableScanners("getproductnames", "");
            if (szScanners == null)
            {
                Display(Config.GetResource(m_resourcemanager, "errNoScannersFound"));
                SetButtons(ButtonState.NoDevices);
                return;
            }
            try
            {
                jsonlookup = new JsonLookup();
                jsonlookup.Load(szScanners, out lResponseCharacterOffset);
            }
            catch
            {
                Display(Config.GetResource(m_resourcemanager, "errNoScannersFound"));
                SetButtons(ButtonState.NoDevices);
                return;
            }

            // Show all the scanners, and then ask for the number of
            // the one to use as the new default...
            szText = "";
            string szMessage = "";
            string szDefault = "";
            for (iScanner = 0; ; iScanner++)
            {
                // Get the next scanner...
                string szScanner = jsonlookup.Get("scanners[" + iScanner + "].twidentityProductName");
                if (string.IsNullOrEmpty(szScanner))
                {
                    szScanner = jsonlookup.Get("scanners[" + iScanner + "].sane");
                }

                // We're out of stuff...
                if (string.IsNullOrEmpty(szScanner))
                {
                    break;
                }

                // If this is the current default, make a note of it...
                lszDrivers.Add(szScanner);
                if (m_scanner.GetTwainLocalTy() == szScanner)
                {
                    szMessage += (iScanner + 1) + ": " + szScanner + " ***DEFAULT***" + Environment.NewLine;
                    szText = (iScanner + 1) + ": " + szScanner + " ***DEFAULT***";
                    szDefault = szScanner;
                    Display(szText);
                }
                // Otherwise, just list it...
                else
                {
                    szMessage += (iScanner + 1) + ": " + szScanner + Environment.NewLine;
                    Display((iScanner + 1) + ": " + szScanner);
                }
            }

            // Ask the user to pick a scanner...
            FormSelect formselect = new FormSelect(m_resourcemanager, lszDrivers, szDefault, m_scanner.GetTwainLocalNote());
            formselect.ShowDialog();
            int iNumber = formselect.GetSelectedDriver();
            string szNote = formselect.GetNote();
            if (iNumber < 0)
            {
                Display("");
                Display(Config.GetResource(m_resourcemanager, "errNoScannersFound"));
                SetButtons(ButtonState.NoDevices);
                return;
            }

            // A little sleight of hand to get a busy cursor...
            this.Refresh();
            Cursor.Current = Cursors.WaitCursor;
            this.Refresh();

            // Tell the user what we're up to...
            Display("");
            Display("We're going to ask the scanner some questions.");
            Display("This may take a minute...");

            // A little sleight of hand to get a busy cursor...
            this.Refresh();
            Cursor.Current = Cursors.WaitCursor;
            this.Refresh();

            // Do a deep inquiry on the selected scanner...
            szScanners = m_scanner.GetAvailableScanners("getinquiry", jsonlookup.Get("scanners[" + iNumber + "].twidentityProductName"));
            if (string.IsNullOrEmpty(szScanners))
            {
                // We are unable to use the selected scanner.  Please make sure your scanner is turned on and connected before trying again.
                Display("");
                Display(Config.GetResource(m_resourcemanager, "errUnableToUseScanner"));
                MessageBox.Show(Config.GetResource(m_resourcemanager, "errUnableToUseScanner"), Config.GetResource(m_resourcemanager, "strFormMainTitle"));
                SetButtons(ButtonState.NoDevices);
                Cursor.Current = Cursors.Default;
                return;
            }
            try
            {
                jsonlookup = new JsonLookup();
                jsonlookup.Load(szScanners, out lResponseCharacterOffset);
            }
            catch
            {
                Display("");
                Display(Config.GetResource(m_resourcemanager, "errUnableToUseScanner"));
                MessageBox.Show(Config.GetResource(m_resourcemanager, "errUnableToUseScanner"), Config.GetResource(m_resourcemanager, "strFormMainTitle"));
                SetButtons(ButtonState.NoDevices);
                Cursor.Current = Cursors.Default;
                return;
            }

            // Register it, make a note if it works by clearing the
            // no devices flag.  Note that the way things work now
            // there is only ever one scanner in the list...
            apicmd = new ApiCmd();
            if (m_scanner.RegisterScanner(jsonlookup, 0, szNote, ref apicmd))
            {
                m_blNoDevices = false;
                Display("Done...");
            }
            else
            {
                Display("Registration failed for: " + iNumber);
                MessageBox.Show("Registration failed for: " + iNumber, "Error");
            }

            // Fix the buttons...
            Display("Registration done...");
            if (m_blNoDevices)
            {
                SetButtons(ButtonState.NoDevices);
            }
            else
            {
                SetButtons(ButtonState.WaitingForStart);
            }
        }

        /// <summary>
        /// Set the buttons based on the current state...
        /// </summary>
        public void SetButtons(ButtonState a_ebuttonstate)
        {
            switch (a_ebuttonstate)
            {
                // When we first start up, use this...
                default:
                case ButtonState.Undefined:
                    m_buttonSetup.Enabled = false;
                    m_buttonStart.Enabled = false;
                    m_buttonStop.Enabled = false;
                    break;

                // We have no devices, they need to register...
                case ButtonState.NoDevices:
                    m_buttonSetup.Enabled = true;
                    m_buttonStart.Enabled = false;
                    m_buttonStop.Enabled = false;
                    break;

                // We have devices, they can register or start...
                case ButtonState.WaitingForStart:
                    m_buttonSetup.Enabled = true;
                    m_buttonStart.Enabled = true;
                    m_buttonStop.Enabled = false;
                    break;

                // We're waiting for a command, they can stop...
                case ButtonState.Started:
                    m_buttonSetup.Enabled = false;
                    m_buttonStart.Enabled = false;
                    m_buttonStop.Enabled = true;
                    break;
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// The button states (enable/disable)...
        /// </summary>
        public enum ButtonState
        {
            Undefined,
            NoDevices,
            WaitingForStart,
            Started
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Prompt the user prior to scanning...
        /// </summary>
        /// <returns>the button they pressed...</returns>
        private TwainLocalScanner.ButtonPress ConfirmScan(float a_fScale)
        {
            // Let's make sure we do this in the right place...
            if (InvokeRequired)
            {
                m_buttonpress = TwainLocalScanner.ButtonPress.Cancel;
                Invoke(new MethodInvoker(delegate { m_buttonpress = ConfirmScan(a_fScale); }));
                return (m_buttonpress);
            }

            // This is turned off, so return OK...
            if (!m_formsetup.GetConfirmation())
            {
                return (TwainLocalScanner.ButtonPress.OK);
            }

            // Okay, keep going...
            DialogResult dialogresult = DialogResult.No;
            ConfirmScan confirmscan;

            // Ask the question...
            confirmscan = new ConfirmScan
            (
                (int)Config.Get("confirmTimeout", 10000),
                (Config.Get("useBeep", "yes") == "yes"),
                a_fScale,
                m_scanner.GetSessionUserDns()
            );
            dialogresult = confirmscan.ShowDialog(this);
            confirmscan.Dispose();
            confirmscan = null;

            // Okay...
            if (dialogresult == DialogResult.Yes)
            {
                return (TwainLocalScanner.ButtonPress.OK);
            }

            // Nope...
            return (TwainLocalScanner.ButtonPress.Cancel);
        }

        /// <summary>
        /// Hide the task bar icon when we're minimized...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }

        /// <summary>
        /// Open the 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_notifyicon_DoubleClick(object sender, EventArgs e)
        {
            MenuitemOpen_Click(null, null);
        }

        /// <summary>
        /// Tell the user about this program...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuitemAbout_Click(object sender, EventArgs e)
        {
            AboutBox aboutbox = new AboutBox(m_resourcemanager);
            aboutbox.ShowDialog();
        }

        /// <summary>
        /// Shutdown the TWAIN Direct on TWAIN Bridge...
        /// </summary>
        private bool m_blAllowFormToClose;
        private void MenuitemExit_Click(object sender, EventArgs e)
        {
            // Do you want to close the 'TWAIN Direct on TWAIN Bridge' program?
            DialogResult dialogresult = MessageBox.Show
            (
                Config.GetResource(m_resourcemanager, "strCloseTwainBridge"),
                Config.GetResource(m_resourcemanager, "strFormMainTitle"),
                MessageBoxButtons.YesNo
            );
            if (dialogresult == DialogResult.Yes)
            {
                m_blAllowFormToClose = true;
                Application.Exit();
            }
        }

        /// <summary>
        /// Display the form on the user's desktop...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuitemOpen_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Show();
            this.Invalidate(true);
            this.Update();
        }

        /// <summary>
        /// Input text...
        /// </summary>
        /// <param name="title">title of the box</param>
        /// <param name="promptText">prompt to the user</param>
        /// <param name="value">text typed by the user</param>
        /// <returns>button pressed</returns>
        private static DialogResult InputBox(string a_szTitle, string a_szPrompt, ref string a_szValue)
        {
            DialogResult dialogResult = DialogResult.Cancel;
            Form form = null;
            Label label = null;
            TextBox textBox = null;
            Button buttonOk = null;
            Button buttonCancel = null;

            try
            {
                form = new Form();
                label = new Label();
                textBox = new TextBox();
                buttonOk = new Button();
                buttonCancel = new Button();

                form.Text = a_szTitle;
                label.Text = a_szPrompt;
                textBox.Text = a_szValue;

                buttonOk.Text = "OK";
                buttonCancel.Text = "Cancel";
                buttonOk.DialogResult = DialogResult.OK;
                buttonCancel.DialogResult = DialogResult.Cancel;

                label.SetBounds(9, 20, 472, 13);
                textBox.SetBounds(12, 56, 472, 20);
                buttonOk.SetBounds(328, 92, 75, 23);
                buttonCancel.SetBounds(409, 92, 75, 23);

                label.AutoSize = true;
                textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
                buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                form.ClientSize = new Size(496, 127);
                form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
                form.ClientSize = new Size(Math.Max(400, label.Right + 10), form.ClientSize.Height);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.AcceptButton = buttonOk;
                form.CancelButton = buttonCancel;

                dialogResult = form.ShowDialog();
                a_szValue = textBox.Text;
            }
            catch (Exception exception)
            {
                Log.Error("Something bad happened..." + exception.Message);
            }
            finally
            {
                // On the advice of analyze...
                if (form != null)
                {
                    form.Dispose();
                    form = null;
                }
                if (label != null)
                {
                    label.Dispose();
                    label = null;
                }
                if (textBox != null)
                {
                    textBox.Dispose();
                    textBox = null;
                }
                if (buttonOk != null)
                {
                    buttonOk.Dispose();
                    buttonOk = null;
                }
                if (buttonCancel != null)
                {
                    buttonCancel.Dispose();
                    buttonCancel = null;
                }
            }

            // All done...
            return (dialogResult);
        }

        /// <summary>
        /// Display a message...
        /// </summary>
        /// <param name="a_szMsg">the thing to display</param>
        private void Display(string a_szMsg)
        {
            // Let us be called from any thread...
            if (this.InvokeRequired)
            {
                Invoke(new MethodInvoker(delegate() { Display(a_szMsg); }));
                return;
            }

            // Okay, do the real work...
            m_richtextboxTask.Text += a_szMsg + Environment.NewLine;
            m_richtextboxTask.Select(m_richtextboxTask.Text.Length - 1, 0);
            m_richtextboxTask.ScrollToCaret();
            m_richtextboxTask.Update();
            this.Refresh();

            // This is bad...
            Application.DoEvents();
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Form Controls...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Form Controls...

        /// <summary>
        /// Make sure we shutdown properly if the user logs off...
        /// </summary>
        /// <param name="m"></param>
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            // Tag it...
            if (m.Msg == WM_QUERYENDSESSION)
            {
                ms_blSystemShutdown = true;
            }

            // If this is WM_QUERYENDSESSION, the closing event should be  
            // raised in the base WndProc.  
            base.WndProc(ref m);

        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Reeeeeeeeejected!
            if (!ms_blSystemShutdown && !m_blAllowFormToClose && (e.CloseReason == CloseReason.UserClosing))
            {
                DialogResult dialogresult = MessageBox.Show(Config.GetResource(m_resourcemanager, "strCloseTwainBridge"), Config.GetResource(m_resourcemanager, "strFormMainTitle"), MessageBoxButtons.OKCancel);
                if (dialogresult != DialogResult.OK)
                {
                    e.Cancel = true;
                }
            }
        }

        /// <summary>
        /// Bring up a form for setting up and configuring the bridge...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonSetup_Click(object sender, EventArgs e)
        {
            string szCloudApiRoot = m_formsetup.GetCloudApiRoot();
            m_formsetup.ShowDialog();
            string szNewCloudApiRoot = m_formsetup.GetCloudApiRoot();
            if (    !string.IsNullOrEmpty(szCloudApiRoot)
                &&  !string.IsNullOrEmpty(szNewCloudApiRoot)
                &&  (szCloudApiRoot != m_formsetup.GetCloudApiRoot()))
            {
                Display(Config.GetResource(m_resourcemanager, "strTextCurrentCloud") + " " + m_formsetup.GetCloudApiRoot());
            }
        }

        /// <summary>
        /// Start polling for work...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void m_buttonStart_Click(object sender, EventArgs e)
        {
            bool blSuccess;

            // Turn all the buttons off...
            SetButtons(ButtonState.Undefined);

            // Is PDF/raster happy and healthy?
            PdfRaster pdfraster = new PdfRaster();
            if (!pdfraster.HealthCheck())
            {
                DialogResult dialogresult = MessageBox.Show
                (
                    "We need to install the Visual Studio 2017 Redistributables for PDF/raster.  May we continue?",
                    "Warning",
                    MessageBoxButtons.YesNo
                );
                if (dialogresult == DialogResult.Yes)
                {
                    pdfraster.InstallVisualStudioRedistributables();
                }
            }

            // Start polling...
            Display("");
            Display("Starting, please wait...");
            if (Config.Get("imageBlockSize", 0) < 8192)
            {
                Display("Each image will be transferred in its own imageBlock...");
            }
            else
            {
                Display("imageBlocks will not exceed " + Config.Get("imageBlockSize", 0) + " bytes...");
            }
            string szNote = m_scanner.GetTwainLocalNote();
            if (!string.IsNullOrEmpty(szNote))
            {
                Display(m_scanner.GetTwainLocalTy() + " (" + szNote + ")");
            }
            else
            {
                Display(m_scanner.GetTwainLocalTy());
            }

            // Start monitoring the cloud...
            int iConnectionRetries = 0;

            blSuccess = await m_scanner.MonitorTasksStart((code, message) =>
            {
                switch (code)
                {
                    // Twain Local states
                    case TwainLocalScannerDevice.STARTING_CALLBACK_EVENT.LOCAL_BAD_PARAMETER:
                    case TwainLocalScannerDevice.STARTING_CALLBACK_EVENT.LOCAL_ERROR_STARTING:
                        Display("TWAIN Local " + message);
                        break;
                    case TwainLocalScannerDevice.STARTING_CALLBACK_EVENT.LOCAL_SERVER_STARTED:
                        Display("TWAIN Local is ready for use...");
                        SetButtons(ButtonState.Started);
                        break;
                        // Twain Cloud states
                        case TwainLocalScannerDevice.STARTING_CALLBACK_EVENT.CLOUD_CONNECTION_FAILED:
                        {
                            if (iConnectionRetries < 10)
                            {
                                Display("TWAIN Cloud  " + message);
                            }
                        }
                        iConnectionRetries++;
                        break;
                    case TwainLocalScannerDevice.STARTING_CALLBACK_EVENT.CLOUD_CONNECTION_SUCCESS:
                        Display("TWAIN Cloud is ready for use...");
                        break;
                }
            });
            if (!blSuccess)
            {
                Log.Error("MonitorTasksStart failed...");
                MessageBox.Show("Failed to start cloud monitoring, check the logs for more information.", Config.GetResource(m_resourcemanager, "strFormMainTitle"));
                SetButtons(ButtonState.WaitingForStart);
                return;
            }
        }

        /// <summary>
        /// Stop polling for work...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_buttonStop_Click(object sender, EventArgs e)
        {
            // Turn all the buttons off...
            SetButtons(ButtonState.Undefined);

            // Staaaaaaahp...
            m_scanner.MonitorTasksStop(true);
            Display("Stop...");

            // Set buttons...
            SetButtons(ButtonState.WaitingForStart);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Set up the bridge...
        /// </summary>
        private FormSetup m_formsetup;

        /// <summary>
        /// Our scanner interface...
        /// </summary>
        private Scanner m_scanner;

        /// <summary>
        /// Localized text...
        /// </summary>
        private ResourceManager m_resourcemanager;

        /// <summary>
        /// True if we have no devices...
        /// </summary>
        private bool m_blNoDevices;

        /// <summary>
        /// Scratchpad for the confirm scan dialog...
        /// </summary>
        private TwainLocalScanner.ButtonPress m_buttonpress;

        /// <summary>
        /// Handle end session...
        /// </summary>
        private static int WM_QUERYENDSESSION = 0x11;
        private static bool ms_blSystemShutdown = false;

        #endregion

        private void FormMain_Load(object sender, EventArgs e)
        {
            Config.ChangeInternetExplorerVersion();
        }

        private void m_richtextboxTask_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
