// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.DataFile;
using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace CollectSFData.Common
{
    public class CertificateUtilities
    {
        public SecureString SecurePassword { private get; set; } = new SecureString();

        public CertificateUtilities()
        {
        }

        public bool CheckCertificate(X509Certificate2 certificate)
        {
            bool retval = false;
            if (certificate == null)
            {
                Log.Error("certificate is null");
                return retval;
            }

            Log.Debug("certificate:", certificate);

            if (!certificate.Verify())
            {
                Log.Warning("certificate chain invalid");
            }

            if (DateTime.Now < certificate.NotBefore | DateTime.Now > certificate.NotAfter)
            {
                Log.Error("certificate invalid time");
                return retval;
            }
            return true;
        }

        public void SetSecurePassword(string stringPassword)
        {
            SecurePassword = new SecureString();

            foreach (char character in stringPassword.ToCharArray())
            {
                SecurePassword.AppendChar(character);
            }
        }

        public X509Certificate2 GetClientCertificate(string clientCertificate)
        {
            X509Certificate2 certificate = null;

            if (string.IsNullOrEmpty(clientCertificate))
            {
                Log.Error("clientcertificate string empty");
            }
            else if (FileTypes.MapFileUriType(clientCertificate) == FileUriTypesEnum.fileUri)
            {
                certificate = ReadCertificateFromFile(clientCertificate);
            }
            else
            {
                certificate = ReadCertificateFromBase64String(clientCertificate);

                if (certificate == null)
                {
                    certificate = ReadCertificateFromStore(clientCertificate);
                }

                if (certificate == null)
                {
                    certificate = ReadCertificateFromStore(clientCertificate, StoreName.My, StoreLocation.LocalMachine);
                }
            }

            Log.Info($"returning certificate for clientcertificate:{clientCertificate}", certificate);
            return certificate;
        }

        public X509Certificate2 ReadCertificateFromBase64String(string certificateValue)
        {
            Log.Info("enter:", certificateValue);
            X509Certificate2 certificate = null;

            try
            {
                certificate = new X509Certificate2(Convert.FromBase64String(certificateValue), SecurePassword, X509KeyStorageFlags.Exportable);
            }
            catch (Exception e)
            {
                Log.Warning($"{e.Message}");
            }

            Log.Debug("exit", certificate);
            return certificate;
        }

        public X509Certificate2 ReadCertificateFromFile(string certificateFile)
        {
            Log.Info("enter:", certificateFile);
            X509Certificate2 certificate = new X509Certificate2(certificateFile, SecurePassword, X509KeyStorageFlags.Exportable);

            Log.Info("exit", certificate);
            return certificate;
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
            return certificate;
        }

        public bool SaveCertificateToFile(X509Certificate2 certificate, string fileName = null)
        {
            Log.Info("enter:", certificate);

            byte[] bytes = certificate.Export(X509ContentType.Pkcs12, SecurePassword);
            File.WriteAllBytes(fileName, bytes);

            Log.Info("exit", certificate);
            return true;
        }

        public string ToBase64String(X509Certificate2 certificate)
        {
            Log.Info("enter:");

            string base64String = Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12, SecurePassword));

            Log.Debug("exit", base64String);
            return base64String;
        }

        private string RemoveCn(string nameWithCn)
        {
            return nameWithCn.ToLower().Replace("cn=", "").Trim();
        }
    }
}