// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CollectSFData.Common;
using CollectSFData.DataFile;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace CollectSFData.Azure
{
    public class ClientCertificate
    {
        public X509Certificate2 Certificate;
        private Instance _instance = Instance.Singleton();

        private ClientIdentity _clientIdentity { get; set; } = new ClientIdentity();
        private ConfigurationOptions _config => _instance.Config;

        public ClientCertificate(ClientIdentity clientIdentity)
        {
            _clientIdentity = clientIdentity;
        }

        public ClientCertificate(X509Certificate2 certificate = null)
        {
            Certificate = certificate;
        }

        public X509Certificate2 GetClientCertificate(string clientCertificate)
        {
            X509Certificate2 certificate = null;

            if (string.IsNullOrEmpty(clientCertificate))
            {
                Log.Error("clientcertificate string empty");
                return Certificate = certificate;
            }
            else if (Certificate != null)
            {
                if (Regex.IsMatch(clientCertificate, Certificate.Thumbprint, RegexOptions.IgnoreCase)
                    | Regex.IsMatch(RemoveCn(clientCertificate), RemoveCn(Certificate.Subject), RegexOptions.IgnoreCase))
                {
                    Log.Info($"matched current clientCertificate:{clientCertificate}", Certificate);
                    return Certificate;
                }

                Log.Info($"resetting certificate");
                Certificate = null;
            }

            if (!string.IsNullOrEmpty(_config.AzureKeyVault) & !string.IsNullOrEmpty(_config.AzureClientSecret))
            {
                certificate = ReadCertificateFromKeyvault(_config.AzureKeyVault, _config.AzureClientSecret);
            }
            else if (FileTypes.MapFileUriType(clientCertificate) == FileUriTypesEnum.fileUri)
            {
                certificate = ReadCertificateFromFile(clientCertificate);
            }
            else
            {
                certificate = ReadCertificate(clientCertificate);
            }

            Log.Info($"returning certificate for clientcertificate:{clientCertificate}", certificate);
            return Certificate = certificate;
        }

        public X509Certificate2 ReadCertificate(string certificateValue)
        {
            Log.Info("enter:", certificateValue);
            X509Certificate2 certificate = null;
            certificate = ReadCertificateValue(certificateValue);

            if (certificate == null)
            {
                certificate = ReadCertificateFromStore(certificateValue);
            }

            if (certificate == null)
            {
                certificate = ReadCertificateFromStore(certificateValue, StoreName.My, StoreLocation.LocalMachine);
            }

            Log.Info("exit", certificate);
            return Certificate = certificate;
        }

        public X509Certificate2 ReadCertificateFromFile(string certificateFile)
        {
            Log.Info("enter:", certificateFile);
            X509Certificate2 certificate = null;
            //certificate = new X509Certificate2(certificateFile, Config.AzureClientSecret ?? string.Empty, X509KeyStorageFlags.Exportable);
            certificate = new X509Certificate2(certificateFile, string.Empty, X509KeyStorageFlags.Exportable);

            Log.Info("exit", certificate);
            return Certificate = certificate;
        }

        public X509Certificate2 ReadCertificateFromKeyvault(string keyvaultResourceId /*Config.AzureKeyVault*/, string secretName /*Config.AzureClientSecret*/)
        {
            Log.Info($"enter:{keyvaultResourceId} {secretName}");
            X509Certificate2 certificate = null;
            TokenCredential credential = null;
            string clientId = _config.AzureClientId;

            if (_clientIdentity.IsAppRegistration)
            {
                credential = new ClientCertificateCredential(_config.AzureTenantId, _config.AzureClientId, ReadCertificate(_config.AzureClientCertificate));
            }
            else if (_clientIdentity.IsUserManagedIdentity)
            {
                credential = _clientIdentity.GetDefaultAzureCredentials(clientId);
            }
            else if (_clientIdentity.IsSystemManagedIdentity)
            {
                clientId = "";
                credential = _clientIdentity.GetDefaultAzureCredentials(clientId);
            }
            else
            {
                Log.Error("unknown configuration");
            }

            try
            {
                SecretClient client = new SecretClient(new Uri(keyvaultResourceId), credential);
                KeyVaultSecret secret = client.GetSecret(secretName);

                byte[] privateKeyBytes = Convert.FromBase64String(secret.Value);
                //certificate = new X509Certificate2(privateKeyBytes, Config.AzureClientSecret ?? string.Empty, X509KeyStorageFlags.Exportable);
                certificate = new X509Certificate2(privateKeyBytes, string.Empty, X509KeyStorageFlags.Exportable);
                return certificate;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                certificate = null;
            }

            return Certificate = certificate;
        }

        public X509Certificate2 ReadCertificateFromStore(string certificateId, StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser)
        {
            Log.Info($"enter:certificateId:{certificateId} storename:{storeName} storelocation:{storeLocation}");
            X509Certificate2 certificate = null;

            using (X509Store store = new X509Store(storeName, storeLocation))
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = store.Certificates;
                X509Certificate2Collection currentCerts = certCollection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);

                X509Certificate2Collection signingCert = currentCerts.Find(X509FindType.FindBySubjectName, RemoveCn(certificateId), false);
                signingCert.AddRange(currentCerts.Find(X509FindType.FindByThumbprint, certificateId, false));
                Log.Debug("active:", signingCert);
                certificate = signingCert?.OfType<X509Certificate2>().OrderByDescending(c => c.NotBefore).FirstOrDefault();
            }

            if (certificate != null)
            {
                Log.Info("exit:success");
            }
            else
            {
                Log.Error($"exit:unable to find certificate for {certificateId}");
            }

            Log.Debug("exit:", certificate);
            return Certificate = certificate;
        }

        public X509Certificate2 ReadCertificateValue(string certificateValue)
        {
            Log.Info("enter:", certificateValue);
            X509Certificate2 certificate = null;

            try
            {
                //certificate = new X509Certificate2(Convert.FromBase64String(certificateValue), Config.AzureClientSecret ?? string.Empty, X509KeyStorageFlags.Exportable);
                certificate = new X509Certificate2(Convert.FromBase64String(certificateValue), string.Empty, X509KeyStorageFlags.Exportable);
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
            }

            Log.Info("exit", certificate);
            return Certificate = certificate;
        }

        public bool SaveCertificateToFile(X509Certificate2 certificate, string fileName = null)
        {
            Log.Info("enter:", certificate);

            byte[] bytes = certificate.Export(X509ContentType.Pkcs12);
            File.WriteAllBytes(fileName, bytes);

            Log.Info("exit", certificate);
            return true;
        }

        private string RemoveCn(string nameWithCn)
        {
            return nameWithCn.ToLower().Replace("cn=", "").Trim();
        }
    }
}