﻿// Copyright (c) Lex Li. All rights reserved.
// 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace JexusManager
{
    using JexusManager.Services;
    using Microsoft.Web.Administration;
    using Ookii.Dialogs.WinForms;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Windows.Forms;

    public static class DialogHelper
    {
        public static string[] Conditions = new[]
                            {
                        "ALL_HTTP",
                        "ALL_RAW",
                        "APPL_MD_PATH",
                        "APPL_PHYSICAL_PATH",
                        "AUTH_PASSWORD",
                        "AUTH_TYPE",
                        "AUTH_USER",
                        "CERT_COOKIE",
                        "CERT_FLAGS",
                        "CERT_ISSUER",
                        "CERT_KEYSIZE",
                        "CERT_SECRETKEYSIZE",
                        "CERT_SERIALNUMBER",
                        "CERT_SERVER_ISSUER",
                        "CERT_SERVER_SUBJECT",
                        "CERT_SUBJECT",
                        "CONTENT_LENGTH",
                        "CONTENT_TYPE",
                        "GATEWAY_INTERFACE",
                        "HTTP_ACCEPT",
                        "HTTP_ACCEPT_ENCODING",
                        "HTTP_ACCEPT_LANGUAGE",
                        "HTTP_CONNECTION",
                        "HTTP_COOKIE",
                        "HTTP_HOST",
                        "HTTP_METHOD",
                        "HTTP_REFERER",
                        "HTTP_URL",
                        "HTTP_USER_AGENT",
                        "HTTP_VERSION",
                        "HTTPS",
                        "HTTPS_KEYSIZE",
                        "HTTPS_SECRETKEYSIZE",
                        "HTTPS_SERVER_ISSUER",
                        "HTTPS_SERVER_SUBJECT",
                        "INSTANCE_ID",
                        "INSTANCE_META_PATH",
                        "LOCAL_ADDR",
                        "LOGON_USER",
                        "PATH_INFO",
                        "PATH_TRANSLATED",
                        "QUERY_STRING",
                        "REMOTE_ADDR",
                        "REMOTE_HOST",
                        "REMOTE_PORT",
                        "REMOTE_USER",
                        "REQUEST_METHOD",
                        "SCRIPT_NAME",
                        "SERVER_NAME",
                        "SERVER_PORT",
                        "SERVER_PORT_SECURE",
                        "SERVER_PROTOCOL",
                        "SERVER_SOFTWARE",
                        "UNMAPPED_REMOTE_USER",
                        "URL"
                            };
        public static void ShowBrowseDialog(TextBox textBox)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                SelectedPath = textBox.Text.ExpandIisExpressEnvironmentVariables()
            };
            try
            {
                if (dialog.ShowDialog() == DialogResult.Cancel)
                {
                    return;
                }

                textBox.Text = dialog.SelectedPath;
            }
            catch (COMException ex)
            {
                if (ex.StackTrace.Contains("Ookii.Dialogs.VistaFolderBrowserDialog.RunDialog(System.IntPtr hwndOwner)"))
                {
                    // IMPORTANT: use a workaround to suppress failure.
                    var fallback = new FolderBrowserDialog { SelectedPath = textBox.Text.ExpandIisExpressEnvironmentVariables() };
                    if (fallback.ShowDialog() == DialogResult.Cancel)
                    {
                        return;
                    }

                    textBox.Text = fallback.SelectedPath;
                }
            }
        }

        public static void ShowOpenFileDialog(TextBox textBox, string filter)
        {
            var initial = textBox.Text.ExpandIisExpressEnvironmentVariables();
            string initialDirectory;
            try
            {
                initialDirectory = string.IsNullOrEmpty(initial) ? string.Empty : Path.GetDirectoryName(initial);
            }
            catch (ArgumentException)
            {
                initialDirectory = string.Empty;
            }

            var dialog = new OpenFileDialog
            {
                InitialDirectory = initialDirectory,
                Filter = filter
            };
            try
            {
                if (dialog.ShowDialog() == DialogResult.Cancel)
                {
                    return;
                }
            }
            catch (COMException ex)
            {
                if (ex.StackTrace.Contains("System.Windows.Forms.OpenFileDialog.CreateVistaDialog()"))
                {
                    // IMPORTANT: use a workaround to suppress failure.
                    dialog.AutoUpgradeEnabled = false;
                    if (dialog.ShowDialog() == DialogResult.Cancel)
                    {
                        return;
                    }
                }
            }

            textBox.Text = dialog.FileName;
        }

        public static string ShowSaveFileDialog(TextBox textBox, string filter)
        {
            var initial = textBox?.Text.ExpandIisExpressEnvironmentVariables();
            string initialDirectory;
            try
            {
                initialDirectory = string.IsNullOrEmpty(initial) ? string.Empty : Path.GetDirectoryName(initial);
            }
            catch (ArgumentException)
            {
                initialDirectory = string.Empty;
            }

            var dialog = new SaveFileDialog
            {
                InitialDirectory = initialDirectory,
                Filter = filter,
                FileName = textBox?.Text
            };
            try
            {
                if (dialog.ShowDialog() == DialogResult.Cancel)
                {
                    return string.Empty;
                }
            }
            catch (COMException ex)
            {
                if (ex.StackTrace.Contains("System.Windows.Forms.SaveFileDialog.CreateVistaDialog()"))
                {
                    // IMPORTANT: use a workaround to suppress failure.
                    dialog.AutoUpgradeEnabled = false;
                    if (dialog.ShowDialog() == DialogResult.Cancel)
                    {
                        return string.Empty;
                    }
                }
            }

            if (textBox != null)
            {
                textBox.Text = dialog.FileName;
            }

            return dialog.FileName;
        }

        public static bool GetImageArchitecture(string filepath)
        {
            var file = new PeNet.PeFile(filepath);
            if (file.HasValidComDescriptor)
            {
                throw new BadImageFormatException(".NET assembly is not supported.");
            }

            return file.Is32Bit;
        }

        public static void DisplayCertificate(X509Certificate2 x509Certificate2, IntPtr handle)
        {
            if (!Helper.IsRunningOnMono())
            {
                X509Certificate2UI.DisplayCertificate(x509Certificate2, handle);
                return;
            }

            var file = GetTempFileName() + ".crt";
            var bytes = x509Certificate2.Export(X509ContentType.Cert);
            File.WriteAllBytes(file, bytes);
            if (Helper.IsRunningOnMono())
            {
                Process.Start(file);
            }
            else
            {
                Process.Start("explorer.exe", file);
            }
        }

        public static void Explore(string folder)
        {
            try
            {
                if (Helper.IsRunningOnMono())
                {
                    Process.Start(folder);
                }
                else
                {
                    // IMPORANT: to avoid jumping to another folder with ".com"
                    // More info can be found in https://forums.iis.net/p/1239773/2144186.aspx?
                    Process.Start("explorer.exe", folder);
                }
            }
            catch (Exception ex)
            {
                // TODO: use dialog service.
                MessageBox.Show(ex.Message, "Jexus Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void LoadCertificates(ComboBox comboBox, byte[] hash, string store, IConfigurationService service)
        {
            comboBox.Items.Add("No selected");
            comboBox.SelectedIndex = 0;
            if (service == null)
            {
                throw new InvalidOperationException("null service");
            }

            if (service.ServerManager.Mode == WorkingMode.Jexus)
            {
                var certificate = AsyncHelper.RunSync(() => ((JexusServerManager)service.ServerManager).GetCertificateAsync());
                if (certificate == null)
                {
                    return;
                }

                comboBox.Items.Add(new CertificateInfo(certificate, "Jexus"));
                if (hash != null &&
                    hash.SequenceEqual(certificate.GetCertHash()))
                {
                    comboBox.SelectedIndex = 1;
                }

                return;
            }

            X509Store store1 = new X509Store("MY", StoreLocation.LocalMachine);
            store1.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var certificate in store1.Certificates)
            {
                var index = comboBox.Items.Add(new CertificateInfo(certificate, store1.Name));
                if (hash != null &&
                    hash.SequenceEqual(certificate.GetCertHash()) &&
                    store1.Name == store)
                {
                    comboBox.SelectedIndex = index;
                }
            }

            store1.Close();

            if (Environment.OSVersion.Version < Version.Parse("6.2"))
            {
                // IMPORTANT: WebHosting store is available since Windows 8.
                return;
            }

            X509Store store2 = new X509Store("WebHosting", StoreLocation.LocalMachine);
            try
            {
                store2.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                foreach (var certificate1 in store2.Certificates)
                {
                    var index1 = comboBox.Items.Add(new CertificateInfo(certificate1, store2.Name));
                    if (hash != null &&
                        hash.SequenceEqual(certificate1.GetCertHash()) &&
                        store2.Name == store)
                    {
                        comboBox.SelectedIndex = index1;
                    }
                }

                store2.Close();
            }
            catch (CryptographicException ex)
            {
                if (ex.HResult != Microsoft.Web.Administration.NativeMethods.NonExistingStore)
                {
                    throw;
                }
            }
        }

        public static void LoadAddresses(ComboBox cbAddress)
        {
            const string DefaultBinding = "All Unassigned";
            cbAddress.Items.Add(DefaultBinding);
            foreach (
                IPAddress address in
                    Dns.GetHostEntry(string.Empty).AddressList.Where(address => !address.IsIPv6LinkLocal))
            {
                cbAddress.Items.Add(address);
            }

            cbAddress.Text = DefaultBinding;
        }

        public static string GetTempFileName()
        {
            return GetSpecialFolder("temp", "temp");
        }

        private static string GetSpecialFolder(string name, string file)
        {
            var result = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Jexus Manager", name);
            if (!Directory.Exists(result))
            {
                try
                {
                    Directory.CreateDirectory(result);
                }
                catch (IOException)
                {
                }
            }

            return Path.Combine(result, file);
        }

        public static string GetPrivateKeyFile(string file)
        {
            return GetSpecialFolder("PrivateKey", file);
        }

        public static string ListIisExpress => GetSpecialFolder("lists", "iisExpressList");

        public static string ListJexus => GetSpecialFolder("lists", "list");

        public static string DebugLog => GetSpecialFolder("temp", "debug");

        public static void ProcessStart(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Win32Exception)
            {
                Help.ShowHelp(null, url);
            }
        }

        public static void BrowseFile(string file)
        {
            Process.Start("explorer.exe", $"/select,\"{file}\"");
        }
    }
}
