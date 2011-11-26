using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace DesktopBootstrapUpdateSigner {

    public partial class Form1 : Form {

        public Form1() {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) {
            // try to find the path to the private key
            var curPath = Environment.CurrentDirectory;
            while (curPath.Length > 0) {
                if(Directory.Exists(Path.Combine(curPath, "Keys"))) {
                    break;
                }
                if(Directory.Exists(Path.Combine(Path.Combine(curPath, "tools"), "Keys"))) {
                    curPath = Path.Combine(curPath, "tools");
                    break;
                }
                
                curPath = Path.GetDirectoryName(curPath) ?? string.Empty; // go one level up
            }
            if (curPath.Length > 0) {
                curPath = Path.Combine(curPath, "Keys");
                txtPrivateKeyFilePath.Text = Path.Combine(curPath, @"self_signed\desktop_bootstrap_client_update_key\PROD\server-certificate-with-encrypted-key.p12");
            } else {
                MessageBox.Show("Couldn't guess at the key file path.");
            }

            // try to find the path to the latest build's DesktopBootstrapUpdater.exe, and also use that
            // to populate a reasonable value for the sig output file and download URL
            curPath = Environment.CurrentDirectory;
            while (curPath.Length > 0 && !Directory.Exists(Path.Combine(curPath, "Installer"))) {
                curPath = Path.GetDirectoryName(curPath); // go one level up
            }
            if (curPath.Length > 0) {
                curPath = Path.Combine(curPath, "Installer");

                var buildDirs = Directory.GetDirectories(Path.Combine(curPath, "builds")).Select<string, string>(
                    x => x.Substring(Path.GetDirectoryName(x).Length + 1));
                int latestBuildNum = buildDirs.Select<string, int>(int.Parse).Max();

                txtUpdateExecutableFilePath.Text = Path.Combine(curPath, @"builds\" + latestBuildNum + string.Format(@"\DesktopBootstrapUpdater{0}.exe", latestBuildNum));
                txtOutFilePath.Text = Path.Combine(Path.GetDirectoryName(txtUpdateExecutableFilePath.Text),
                    "DesktopBootstrapUpdateInfo.xml");
                txtDownloadUrl.Text = String.Format("http://cdn.desktopbootstrap.com/DesktopBootstrapUpdater{0}.exe", latestBuildNum);
            } else {
                MessageBox.Show("Couldn't guess at the build directory path.");
            }
        }

        private void butGo_Click(object sender, EventArgs e) {
            var xmlDoc = CreateUpdateXmlDoc(txtUpdateExecutableFilePath.Text,
                txtDownloadUrl.Text);

            AddSignatureToXmlDocument(xmlDoc, new X509Certificate2(txtPrivateKeyFilePath.Text,
                txtPassword.Text));

            using (var xmltw = new XmlTextWriter(txtOutFilePath.Text, new UTF8Encoding(false))) {
                xmlDoc.WriteTo(xmltw);
                xmltw.Close();
            }

            MessageBox.Show("Done!");
        }

        private static void AddSignatureToXmlDocument(XmlDocument toSign, X509Certificate2 cert) {
            var signedXml = new SignedXml(toSign);
            signedXml.SigningKey = cert.PrivateKey;

            var reference = new Reference();
            reference.Uri = "";
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            signedXml.AddReference(reference);

            signedXml.ComputeSignature();
            var xmlDigitalSignature = signedXml.GetXml();
            toSign.DocumentElement.AppendChild(toSign.ImportNode(xmlDigitalSignature, true));
            if (toSign.FirstChild is XmlDeclaration) {
                toSign.RemoveChild(toSign.FirstChild);
            }
        }

        private static XmlDocument CreateUpdateXmlDoc(string updateExecutableFilePath, string downloadUrl) {
            XmlDocument document = new XmlDocument();

            var rootNode = document.CreateNode(XmlNodeType.Element, "UpdateInfo", string.Empty);
            document.AppendChild(rootNode);

            var downloadUrlNode = document.CreateNode(XmlNodeType.Element, "DownloadUrl", string.Empty);
            downloadUrlNode.InnerText = downloadUrl;
            rootNode.AppendChild(downloadUrlNode);

            var downloadHashNode = document.CreateNode(XmlNodeType.Element, "", "DownloadHash", "");
            downloadHashNode.InnerText = Convert.ToBase64String(new SHA1CryptoServiceProvider().ComputeHash(
                File.ReadAllBytes(updateExecutableFilePath)));
            rootNode.AppendChild(downloadHashNode);

            return document;
        }
    }
}
