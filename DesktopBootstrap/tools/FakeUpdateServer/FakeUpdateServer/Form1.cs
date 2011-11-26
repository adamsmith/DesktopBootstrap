using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Net;
using System.IO;

namespace FakeUpdateServer {

    public partial class Form1 : Form {

        private List<string> m_clientGuidsSeen = new List<string>();
        private int m_numUniqueClientGuids = -1;

        public Form1() {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) {
            if (!HttpListener.IsSupported) {
                Log_ThreadSafe("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }

            ThreadPool.QueueUserWorkItem(RunServer);

            txtXml.Text = Path.Combine(Environment.CurrentDirectory, "DesktopBootstrapUpdateInfo.xml");
            txtExe.Text = Path.Combine(Environment.CurrentDirectory, "DesktopBootstrapUpdater.exe");

            CountNewUniqueClientGuid_ThreadSafe();
        }

        private delegate string SimpleDelegate();
        private delegate int IntDelegate();
        private delegate void VoidDelegate();

        private void RunServer(object unusedState) {
            var prefixes = new string[] { "http://*/" };
            if (prefixes == null || prefixes.Length == 0) {
                throw new ArgumentException("prefixes");
            }

            // Create a listener.
            HttpListener listener = new HttpListener();
            // Add the prefixes.
            foreach (string s in prefixes) {
                listener.Prefixes.Add(s);
            }
            listener.Start();
            Log_ThreadSafe("Listening...");
            // Note: The GetContext method blocks while waiting for a request. 
            while (true) {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;

                try {

                    if (request.RawUrl.StartsWith("/updateCheck?")) {
                        if ((request.RawUrl.ToLowerInvariant().Contains("requestIsFromService=true".ToLowerInvariant()) && !chkboxService.Checked)
                                || (request.RawUrl.ToLowerInvariant().Contains("requestIsFromService=false".ToLowerInvariant()) && !chkboxClientApp.Checked)) {

                            context.Response.ContentLength64 = 0;
                            context.Response.OutputStream.Close();

                            Log_ThreadSafe("Turned down update request for " + request.RawUrl);
                        } else {
                            var buildNumber = GetUrlParameter(request.RawUrl, "build");
                            var clientGuid = GetUrlParameter(request.RawUrl, "clientGuid");

                            if (!m_clientGuidsSeen.Contains(clientGuid)) {
                                var numUniqueClientsLeftToUpdate = (int)txtNumUniqueClientUpdatesRemaining.Invoke(
                                    new IntDelegate(GetNumUniqueClientUpdatesRemaining_ThreadUnsafe));
                                if (numUniqueClientsLeftToUpdate == 0) {
                                    // no more!

                                    context.Response.ContentLength64 = 0;
                                    context.Response.OutputStream.Close();
                                    Log_ThreadSafe("Turned down update request because no more update counts available");
                                    continue;
                                }

                                txtNumUniqueClientUpdatesRemaining.Invoke(new VoidDelegate(DecrementNumUniqueClientUpdatesRemaining_ThreadSafe));

                                m_clientGuidsSeen.Add(clientGuid);
                                CountNewUniqueClientGuid_ThreadSafe();
                            }

                            if (txtBuildNumber.Text.Length > 0 && int.Parse(txtBuildNumber.Text) <= int.Parse(buildNumber)) {
                                context.Response.ContentLength64 = 0;
                                context.Response.OutputStream.Close();
                                Log_ThreadSafe("Turned down update request due to lower build number, for " + request.RawUrl);
                                continue;
                            }

                            // we're actually going to send the update command!

                            HttpListenerResponse response = context.Response;
                            // Construct a response.
                            string responseString = File.ReadAllText((string)txtXml.Invoke(new SimpleDelegate(delegate() { return txtXml.Text; })));
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                            // Get a response stream and write the response to it.
                            response.ContentLength64 = buffer.Length;
                            System.IO.Stream output = response.OutputStream;
                            output.Write(buffer, 0, buffer.Length);
                            // You must close the output stream.
                            output.Close();

                            Log_ThreadSafe(string.Format("Recieved request for ({0}/{1}) {2} ... sent response from {3}", buildNumber, clientGuid, request.RawUrl, txtXml.Text));
                        }
                    } else {
                        // Obtain a response object.
                        HttpListenerResponse response = context.Response;
                        // Construct a response.
                        var filePath = (string)txtExe.Invoke(new SimpleDelegate(delegate() { return txtExe.Text; }));
                        if (File.Exists(filePath)) {
                            byte[] buffer = File.ReadAllBytes(filePath);
                            // Get a response stream and write the response to it.
                            response.ContentLength64 = buffer.Length;
                            System.IO.Stream output = response.OutputStream;
                            output.Write(buffer, 0, buffer.Length);
                            // You must close the output stream.
                            output.Close();

                            Log_ThreadSafe("Recieved request for " + request.RawUrl + "... sent response from " + txtExe.Text);
                        } else {
                            Log_ThreadSafe("Recieved request for " + request.RawUrl + "... rejecting...");
                            context.Response.ContentLength64 = 0;
                            context.Response.OutputStream.Close();
                        }
                    }

                } catch (Exception e) {
                    Log_ThreadSafe(string.Format("Exception {0} on URL {1}", e.Message, request.RawUrl));
                }
            }
            listener.Stop();
        }

        private void Log_ThreadSafe(string x) {
            log.Invoke(new SimpleDelegate(delegate() {
                if (log.Items.Count > 2000) {
                    log.Items.Clear();
                }

                log.Items.Add(DateTime.Now.ToString() + ":  " + x);
                log.SelectedIndex = log.Items.Count - 1;
                return null;
            }));
        }

        private string GetUrlParameter(string rawUrl, string paramName) {
            var indexOf = rawUrl.IndexOf(paramName + "=");
            if (indexOf < 0) {
                throw new Exception("URL parameter not found");
            }
            indexOf += paramName.Length + 1;
            return rawUrl.Substring(indexOf, rawUrl.IndexOf('&', indexOf) - indexOf);
        }

        private void CountNewUniqueClientGuid_ThreadSafe() {
            numClientGuidsLabel.Invoke(new SimpleDelegate(delegate() {
                numClientGuidsLabel.Text = string.Format("{0} unique client guids", ++m_numUniqueClientGuids);
                return null;
            }));
        }

        private int GetNumUniqueClientUpdatesRemaining_ThreadUnsafe() {
            try {
                return int.Parse(txtNumUniqueClientUpdatesRemaining.Text);
            } catch {
                return -1;
            }
        }

        private void DecrementNumUniqueClientUpdatesRemaining_ThreadSafe() {
            txtNumUniqueClientUpdatesRemaining.Text = 
                (GetNumUniqueClientUpdatesRemaining_ThreadUnsafe() < 0 ? -1 : GetNumUniqueClientUpdatesRemaining_ThreadUnsafe() - 1).ToString();
        }

        private void log_SelectedIndexChanged(object sender, EventArgs e) {
            Clipboard.SetText(log.SelectedItem.ToString());
        }
    }
}
