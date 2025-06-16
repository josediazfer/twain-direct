using System;
using System.IO;
using HazyBits.Twain.Cloud.Client;
using HazyBits.Twain.Cloud.Forms;
using Microsoft.Web.WebView2.Core;

namespace TwainDirect.Scanner
{
    public partial class FormWebview : FormLogin
    {
        private string m_Url;
        private const string AuthorizationTokenName = "authorization_token";
        private const string RefreshTokenName = "refresh_token";

        public FormWebview(string url)
        {
            InitializeComponent();
            this.Resize += new System.EventHandler(this.Form_Resize);
            InitializeAsync();
            m_Url = url;
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            webView.Size = this.ClientSize - new System.Drawing.Size(webView.Location);
        }

        async void InitializeAsync()
        {
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.GetTempPath(), options: null);

            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            await webView.EnsureCoreWebView2Async(environment);
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                webView.CoreWebView2.Navigate(m_Url);
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(webView.Source.Query);
            var authToken = queryParams[AuthorizationTokenName];
            var refreshToken = queryParams[RefreshTokenName];

            // There will be several redirects when new user accesses the app.
            // Make sure we fire Authorized event only when we have both token successfully extracted.
            if (!string.IsNullOrEmpty(authToken) && !string.IsNullOrEmpty(refreshToken))
                OnAuthorized(new TwainCloudAuthorizedEventArgs(new TwainCloudTokens(authToken, refreshToken)));
        }
  
        private void FormWebview_Load(object sender, EventArgs e)
        {

        }

        private void FormWebview_Load_1(object sender, EventArgs e)
        {

        }
    }
}
