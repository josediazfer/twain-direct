using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Forms;
using System.IO;
using HazyBits.Twain.Cloud.Forms;
using TwainDirect.Support;
using System.Resources;
using System.Diagnostics;

namespace TwainDirect.Scanner
{
    public partial class FormBurowebConfig : Form
    {
        public FormBurowebConfig(ResourceManager a_resourcemanager)
        {
            InitializeComponent();

            m_resourcemanager = a_resourcemanager;
        }

        public bool isConfigSaved()
        {
            return m_configSaved;
        }

        public bool isAdvertise()
        {
            return m_advertise;
        }

        public bool isRunOnLogon()
        {
            return m_runOnLogon;
        }

        private string getBaseFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "twaindirect");
        }

        private void m_buttonConfigure_Click(object sender, EventArgs e)
        {
            string szbaseUrl;
            FacebookLoginForm loginForm;

            if (!m_textBurowebURL.Text.StartsWith("https://") && !m_textBurowebURL.Text.StartsWith("http://"))
            {
                MessageBox.Show("La direccíon URL de buroweb no es valida o no se informo", "Configuracion");
                return;
            }
            szbaseUrl = m_textBurowebURL.Text + "/report/scannerManager.do";
            loginForm = new FacebookLoginForm(szbaseUrl + "?action=signin");
            loginForm.Authorized += (_, args) =>
            {
                List<string> tempConfigFiles = new List<string>();

                try
                {
                    JsonLookup jsonLookup = new JsonLookup();
                    long a_lJsonErrorindex = 0;
                    string[] szExecutableConfigs = new string[] { "TwainDirect.OnTwain", "TwainDirect.Scanner"};
                    string baseOutFolder;
                    WebClient webClient = new WebClient();
                    int iIndex = 0;

                    loginForm.Close();
                    szbaseUrl += "?action=gettwainbridgeconfig&authorization_code=" + args.Tokens.AuthorizationToken + "&config=";
                    // Download the configuration files
                    foreach (string szExecutableConfig in szExecutableConfigs)
                    {
                        string szTempConfigFile = Path.GetTempFileName();

                        tempConfigFiles.Add(szTempConfigFile);

                        webClient.DownloadFile(szbaseUrl + szExecutableConfig + ".appdata.json", szTempConfigFile);

                        if ((new FileInfo(szTempConfigFile)).Length <= 0)
                        {
                            throw new Exception("No se ha podido descargar la configuracion");
                        }
                        jsonLookup.Load(File.ReadAllText(szTempConfigFile), out a_lJsonErrorindex);
                        if (a_lJsonErrorindex != 0)
                        {
                            throw new Exception("El formato JSON de la configuración descargada de '" + szExecutableConfig + "' no es valido");
                        }
                        if (szExecutableConfig.Equals("TwainDirect.Scanner"))
                        {
                            string szParam = jsonLookup.Get("_runOnLogon");

                            if (szParam != null)
                            {
                                m_runOnLogon = szParam.Equals("true");
                            }
                            szParam = jsonLookup.Get("_startMonitoring");
                            if (szParam != null)
                            {
                                m_advertise = szParam.Equals("true");
                            }
                        }
                    }
                    baseOutFolder = getBaseFolder();

                    // Copy the configuration files
                    foreach (string szExecutableConfig in szExecutableConfigs)
                    {
                        string szBaseConfigPath = Path.Combine(baseOutFolder, szExecutableConfig);
                        string szExecutableConfigPath = Path.Combine(szBaseConfigPath, szExecutableConfig + ".appdata.txt");
                        string szTempConfigFile = tempConfigFiles[iIndex++];

                        if (!Directory.Exists(szBaseConfigPath))
                        {
                            Directory.CreateDirectory(szBaseConfigPath);
                        }
                        File.Copy(szTempConfigFile, szExecutableConfigPath, true);
                    }
                    File.WriteAllText(Path.Combine(baseOutFolder, "lastBurowebConfig.txt"), m_textBurowebURL.Text);
                    m_configSaved = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Configuracion");
                }
                finally
                {
                    // Clean up temporal config files
                    foreach (string szTempConfigFile in tempConfigFiles)
                    {
                        try
                        {
                            File.Delete(szTempConfigFile);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }
                }
            };
            loginForm.ShowDialog();
        }

        private void FormBurowebConfig_Load(object sender, EventArgs e)
        {
            string lastBurowebConfigFile = Path.Combine(getBaseFolder(), "lastBurowebConfig.txt");
          
            if (File.Exists(lastBurowebConfigFile))
            {
                string szBurowebURL = File.ReadAllText(lastBurowebConfigFile);
                if (szBurowebURL.Length > 0)
                {
                    m_textBurowebURL.Text = szBurowebURL;
                    m_buttonConfigure.Focus();
                }
            }
        }

        private ResourceManager m_resourcemanager;
        private bool m_configSaved = false;
        private bool m_advertise = true;
        private bool m_runOnLogon = true;
    }
}
