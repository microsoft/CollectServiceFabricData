// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CollectSFData.Common;
using CollectSFData.DataFile;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Azure
{
    public class AzureResourceManager : Constants
    {
        private string _baseLogonUri = "https://management.azure.com/";
        private X509Certificate2 _certificate;
        private string _commonTenantId = "common";
        private IConfidentialClientApplication _confidentialClientApp;
        private List<string> _defaultScope = new List<string>() { ".default" };
        private string _getSubscriptionRestUri = "https://management.azure.com/subscriptions/{subscriptionId}?api-version=2016-06-01";
        private Http _httpClient = Http.ClientFactory();
        private Instance _instance = Instance.Singleton();
        private string _listSubscriptionsRestUri = "https://management.azure.com/subscriptions?api-version=2016-06-01";
        private IPublicClientApplication _publicClientApp;
        private string _resource;
        private Timer _timer;
        private DateTimeOffset _tokenExpirationHalfLife;
        private string _wellKnownClientId = "1950a258-227b-4e31-a9cf-717495945fc2";

        public delegate void MsalDeviceCodeHandler(DeviceCodeResult arg);

        public delegate void MsalHandler(LogLevel level, string message, bool containsPII);

        public static event MsalDeviceCodeHandler MsalDeviceCode;

        public static event MsalHandler MsalMessage;

        public AccessToken AuthenticationResultToken { get; private set; } = new AccessToken();

        public string BearerToken { get; private set; }

        private ConfigurationOptions Config => _instance.Config;

        public bool IsAppRegistration { get; private set; } = false;

        public bool IsAuthenticated { get; private set; }

        public bool IsSystemManagedIdentity { get; private set; } = false;

        public bool IsUserManagedIdentity { get; private set; } = false;

        public AccessToken ManagedIdentityToken { get; private set; }

        public List<string> Scopes { get; set; } = new List<string>();

        public SubscriptionRecordResult[] Subscriptions { get; private set; } = new SubscriptionRecordResult[] { };

        public AzureResourceManager()
        {
            Log.Info($"enter: token cache path: {TokenCacheHelper.CacheFilePath}");
            _certificate = Config.ClientCertificate;
            SetClientIdentityInfo();
        }

        public bool Authenticate(bool throwOnError = false, string resource = ManagementAzureCom)
        {
            Exception ex = new Exception();
            Log.Debug("azure ad:enter");
            _resource = resource;

            if (!NeedsAuthentication())
            {
                return true;
            }

            try
            {
                if (string.IsNullOrEmpty(Config.AzureTenantId))
                {
                    Config.AzureTenantId = _commonTenantId;
                }

                CreateClient(false, false, resource);
                return SetToken();
            }
            catch (MsalClientException e)
            {
                Log.Warning("MsalClientException");
                ex = e;

                if (CreateClient(true, true, resource))
                {
                    return SetToken();
                }
            }
            catch (MsalUiRequiredException e)
            {
                Log.Warning("MsalUiRequiredException");
                ex = e;

                try
                {
                    if (CreateClient(true, false, resource))
                    {
                        return SetToken();
                    }
                }
                catch (AggregateException ae)
                {
                    Log.Warning($"AggregateException");

                    if (ae.GetBaseException() is MsalClientException)
                    {
                        Log.Warning($"innerexception:MsalClientException");
                        if (CreateClient(true, true, resource))
                        {
                            return SetToken();
                        }
                    }

                    Log.Exception($"AggregateException:{ae}");
                }
            }
            catch (AggregateException e)
            {
                Log.Warning($"AggregateException");
                ex = e;

                if (e.GetBaseException() is MsalClientException)
                {
                    Log.Warning($"innerexception:MsalClientException");

                    if (CreateClient(true, true, resource))
                    {
                        return SetToken();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
            }

            IsAuthenticated = false;
            Log.Last($"Authentication exception:", ConsoleColor.Yellow, null, ex);

            if (throwOnError)
            {
                throw ex;
            }

            return false;
        }

        public bool CheckResource(string resourceId)
        {
            string uri = $"{ManagementAzureCom}{resourceId}?{ArmApiVersion}";

            if (_httpClient.SendRequest(uri: uri, authToken: BearerToken, httpMethod: HttpMethod.Head))
            {
                return _httpClient.StatusCode == System.Net.HttpStatusCode.NoContent;
            }

            return false;
        }

        public bool CreateClient(bool prompt, bool deviceLogin = false, string resource = "")
        {
            if (Config.IsClientIdConfigured() & prompt)
            {
                return false;
            }
            else if (Config.IsClientIdConfigured() & !prompt)
            {
                if (!string.IsNullOrEmpty(Config.AzureClientCertificate) & !IsUserManagedIdentity)
                {
                    CreateConfidentialClient(resource, Config.AzureClientCertificate);
                }
                else if (IsUserManagedIdentity)
                {
                    ManagedIdentityUserConfidentialClient(resource);
                }
                else if (!string.IsNullOrEmpty(Config.AzureClientSecret))
                {
                    CreateConfidentialClient(resource);
                }
                else
                {
                    Log.Error("unknown configuration");
                    return false;
                }

                return true;
            }
            else
            {
                CreatePublicClient(prompt, deviceLogin);
                return true;
            }
        }

        public void CreateConfidentialClient(string resource, string clientCertificate)
        {
            X509Certificate2 certificate = GetClientCertificate(clientCertificate);
            _confidentialClientApp = ConfidentialClientApplicationBuilder
                .CreateWithApplicationOptions(new ConfidentialClientApplicationOptions
                {
                    ClientId = Config.AzureClientId,
                    RedirectUri = resource,
                    TenantId = Config.AzureTenantId,
                    ClientName = Constants.ApplicationName
                })
                .WithAuthority(AzureCloudInstance.AzurePublic, Config.AzureTenantId)
                .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
                .WithCertificate(certificate)
                .Build();
            AddClientScopes();
        }

        public void CreateConfidentialClient(string resource)
        {
            Log.Info($"enter: {resource}");
            // no prompt with clientid and secret
            _confidentialClientApp = ConfidentialClientApplicationBuilder
               .CreateWithApplicationOptions(new ConfidentialClientApplicationOptions
               {
                   ClientId = Config.AzureClientId,
                   RedirectUri = resource,
                   ClientSecret = Config.AzureClientSecret,
                   TenantId = Config.AzureTenantId,
                   ClientName = Config.AzureClientId
               })
               .WithAuthority(AzureCloudInstance.AzurePublic, Config.AzureTenantId)
               .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
               .Build();

            AddClientScopes();
        }

        public bool CreatePublicClient(bool prompt, bool deviceLogin = false)
        {
            Log.Info($"enter: {prompt} {deviceLogin}");
            AuthenticationResult result = null;
            _publicClientApp = PublicClientApplicationBuilder
                .Create(_wellKnownClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, Config.AzureTenantId)
                .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
                .WithDefaultRedirectUri()
                .Build();

            if (_instance.IsWindows)
            {
                TokenCacheHelper.EnableSerialization(_publicClientApp.UserTokenCache);
            }

            if (prompt)
            {
                if (deviceLogin)
                {
                    result = _publicClientApp.AcquireTokenWithDeviceCode(_defaultScope, MsalDeviceCodeCallback).ExecuteAsync().Result;
                }
                else
                {
                    result = _publicClientApp.AcquireTokenInteractive(_defaultScope).ExecuteAsync().Result;
                }
            }
            else
            {
                IAccount hint = _publicClientApp.GetAccountsAsync().Result.FirstOrDefault();

                if (hint == null && !TokenCacheHelper.HasTokens)
                {
                    throw new MsalUiRequiredException("unable to acquire token silently.", "no hint and no cached tokens.");
                }

                result = _publicClientApp.AcquireTokenSilent(_defaultScope, hint).ExecuteAsync().Result;
            }

            if (Scopes.Count > 0)
            {
                Log.Info($"adding scopes {Scopes.Count}");
                result = _publicClientApp.AcquireTokenSilent(Scopes, _publicClientApp.GetAccountsAsync().Result.FirstOrDefault()).ExecuteAsync().Result;
            }

            AuthenticationResultToken = new AccessToken(result.AccessToken, result.ExpiresOn);
            return true;
        }

        public bool CreateResourceGroup(string resourceId, string location)
        {
            Log.Info($"Checking resource group: {resourceId}");

            if (!CheckResource(resourceId))
            {
                Log.Warning($"creating resourcegroup {resourceId}");
                string uri = $"{ManagementAzureCom}{resourceId}?{ArmApiVersion}";
                JObject jBody = new JObject()
                {
                   new JProperty("location", location)
                };

                ProvisionResource(resourceId, jBody.ToString());
                return _httpClient.Success;
            }

            Log.Info($"resourcegroup exists {resourceId}");
            return true;
        }

        public void ManagedIdentityUserConfidentialClient(string resource)
        {
            Log.Info($"enter: {resource}");
            // no prompt with clientid and secret
            AuthenticationResultToken = new AccessToken(ManagedIdentityToken.Token, ManagedIdentityToken.ExpiresOn);
            SetToken();
            // _confidentialClientApp = ConfidentialClientApplicationBuilder
            //    .CreateWithApplicationOptions(new ConfidentialClientApplicationOptions
            //    {
            //        ClientId = Config.AzureClientId,
            //        RedirectUri = resource,
            //        //ClientSecret = Config.AzureKeyVault,
            //        TenantId = Config.AzureTenantId,
            //        ClientName = Config.AzureClientId
            //    })
            //    .WithAuthority(AzureCloudInstance.AzurePublic, Config.AzureTenantId)
            //    .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
            //    .Build();

            // AddClientScopes();
        }

        public Task MsalDeviceCodeCallback(DeviceCodeResult arg)
        {
            Log.Highlight($"device code info:", arg);

            MsalDeviceCodeHandler deviceCodeMessage = MsalDeviceCode;
            deviceCodeMessage?.Invoke(arg);

            return Task.FromResult(0);
        }

        public void MsalLoggerCallback(LogLevel level, string message, bool containsPII)
        {
            Log.Debug($"{level} {message.Replace(" [", "\r\n [")}");
            MsalHandler logMessage = MsalMessage;
            logMessage?.Invoke(level, message, containsPII);
        }

        public bool PopulateSubscriptions()
        {
            bool response = false;

            if (Subscriptions.Length == 0 && !string.IsNullOrEmpty(Config.AzureSubscriptionId))
            {
                response = _httpClient.SendRequest(_getSubscriptionRestUri, BearerToken
                    .Replace("{subscriptionId}", Config.AzureSubscriptionId));

                Subscriptions = new SubscriptionRecordResult[1]
                {
                    JsonConvert.DeserializeObject<SubscriptionRecordResult>(_httpClient.ResponseStreamString)
                };
            }
            else
            {
                response = _httpClient.SendRequest(_listSubscriptionsRestUri, BearerToken);
                Subscriptions = (JsonConvert.DeserializeObject<SubscriptionRecordResults>(_httpClient.ResponseStreamString)).value
                    .Where(x => x.state.ToLower() == "enabled").ToArray();
            }

            return response;
        }

        public Http ProvisionResource(string resourceId, string body = "", string apiVersion = ArmApiVersion)
        {
            Log.Info("enter");
            string uri = $"{ManagementAzureCom}{resourceId}?{apiVersion}";

            if (_httpClient.SendRequest(uri: uri, authToken: BearerToken, jsonBody: body, httpMethod: HttpMethod.Put))
            {
                int count = 0;

                // wait for state
                while (count < RetryCount)
                {
                    bool response = _httpClient.SendRequest(uri: uri, authToken: BearerToken);
                    GenericResourceResult result = JsonConvert.DeserializeObject<GenericResourceResult>(_httpClient.ResponseStreamString);

                    if (result.properties.provisioningState.ToLower() == "succeeded")
                    {
                        Log.Info($"resource provisioned {resourceId}", ConsoleColor.Green);
                        return _httpClient;
                    }

                    count++;
                    Log.Info($"requery count: {count} of {RetryCount} response: {response}");
                    Thread.Sleep(ThreadSleepMs10000);
                }
            }

            Log.Error($"unable to provision {resourceId}");
            _httpClient.Success = false;
            return _httpClient;
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
            return certificate;
        }

        public X509Certificate2 ReadCertificateFromFile(string certificateFile)
        {
            Log.Info("enter:", certificateFile);
            X509Certificate2 certificate = null;
            //certificate = new X509Certificate2(certificateFile, Config.AzureClientSecret ?? string.Empty, X509KeyStorageFlags.Exportable);
            certificate = new X509Certificate2(certificateFile, string.Empty, X509KeyStorageFlags.Exportable);

            Log.Info("exit", certificate);
            return certificate;
        }

        public X509Certificate2 ReadCertificateFromKeyvault(string keyvaultResourceId /*Config.AzureKeyVault*/, string secretName /*Config.AzureClientSecret*/)
        {
            Log.Info($"enter:{keyvaultResourceId} {secretName}");
            X509Certificate2 certificate = null;
            TokenCredential credential = null;
            string clientId = Config.AzureClientId;

            if (IsAppRegistration)
            {
                credential = new ClientCertificateCredential(Config.AzureTenantId, Config.AzureClientId, ReadCertificate(Config.AzureClientCertificate));
            }
            else if (IsUserManagedIdentity)
            {
                credential = GetDefaultAzureCredentials(clientId);
            }
            else if (IsSystemManagedIdentity)
            {
                clientId = "";
                credential = GetDefaultAzureCredentials(clientId);
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
                return null;
            }
        }

        public void Reauthenticate(object state = null)
        {
            Log.Highlight("azure ad reauthenticate");
            Authenticate(true, _resource);
        }

        public bool SaveCertificateToFile(X509Certificate2 certificate, string fileName = null)
        {
            Log.Info("enter:", certificate);

            byte[] bytes = certificate.Export(X509ContentType.Pkcs12);
            File.WriteAllBytes(fileName, bytes);

            Log.Info("exit", certificate);
            return true;
        }

        public Http SendRequest(string uri, HttpMethod method = null, string body = "")
        {
            method = method ?? _httpClient.Method;
            _httpClient.SendRequest(uri: uri, authToken: BearerToken, jsonBody: body, httpMethod: method);
            return _httpClient;
        }

        public void SetClientIdentityInfo()
        {
            if (!string.IsNullOrEmpty(Config.AzureClientId))
            {
                IsAppRegistration = true;
            }

            if (!Config.AzureManagedIdentity)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Config.AzureClientId))
            {
                IsUserManagedIdentity = IsManagedIdentity(Config.AzureClientId);
            }

            if (!IsUserManagedIdentity && string.IsNullOrEmpty(Config.AzureClientId))
            {
                IsSystemManagedIdentity = IsManagedIdentity();
            }

            IsAppRegistration = !(IsUserManagedIdentity | IsSystemManagedIdentity);
        }

        public bool SetToken()
        {
            if (!string.IsNullOrEmpty(AuthenticationResultToken.Token))
            {
                BearerToken = AuthenticationResultToken.Token;
                long tickDiff = ((AuthenticationResultToken.ExpiresOn.ToLocalTime().Ticks - DateTime.Now.Ticks) / 2) + DateTime.Now.Ticks;
                _tokenExpirationHalfLife = new DateTimeOffset(new DateTime(tickDiff));

                Log.Info($"authentication result:", ConsoleColor.Green, null, AuthenticationResultToken);
                Log.Highlight($"aad token expiration: {AuthenticationResultToken.ExpiresOn.ToLocalTime()}");
                Log.Highlight($"aad token half life expiration: {_tokenExpirationHalfLife}");

                _timer = new Timer(Reauthenticate, null, Convert.ToInt32((_tokenExpirationHalfLife - DateTime.Now).TotalMilliseconds), Timeout.Infinite);
                IsAuthenticated = true;

                return true;
            }
            else
            {
                Log.Warning($"authentication result:", AuthenticationResultToken);
                IsAuthenticated = false;
                return false;
            }
        }

        private static X509Certificate2 ReadCertificateFromStore(string certificateId, StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser)
        {
            Log.Info($"enter:certificateId:{certificateId} storename:{storeName} storelocation:{storeLocation}");
            X509Certificate2 certificate = null;

            using (X509Store store = new X509Store(storeName, storeLocation))
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = store.Certificates;

                // Find unexpired certificates.
                X509Certificate2Collection currentCerts = certCollection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);

                // From the collection of unexpired certificates, find the ones with the correct name.
                X509Certificate2Collection signingCert = currentCerts.Find(X509FindType.FindBySubjectName, certificateId.ToLower().Replace("cn=", "").Trim(), false);
                signingCert.AddRange(currentCerts.Find(X509FindType.FindByThumbprint, certificateId, false));
                Log.Debug("active:", signingCert);

                // Return the first certificate in the collection, has the right name and is current.
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

        private void AddClientScopes()
        {
            if (Scopes.Count < 1)
            {
                Scopes = _defaultScope;
            }

            if (_instance.IsWindows)
            {
                TokenCacheHelper.EnableSerialization(_confidentialClientApp.AppTokenCache);
            }

            foreach (string scope in Scopes)
            {
                AuthenticationResult result = _confidentialClientApp
                    .AcquireTokenForClient(new List<string>() { scope })
                    .ExecuteAsync().Result;
                Log.Debug($"scope authentication result:", AuthenticationResultToken);
                AuthenticationResultToken = new AccessToken(result.AccessToken, result.ExpiresOn);
            }
        }

        private X509Certificate2 GetClientCertificate(string clientCertificate)
        {
            if (string.IsNullOrEmpty(clientCertificate))
            {
                Log.Error("clientcertificate string empty");
                return _certificate;
            }
            else if (_certificate != null)
            {
                string clientCert = clientCertificate.ToLower().Replace("cn=", "").Trim();
                string certificateSubject = _certificate.Subject.ToLower().Replace("cn=", "").Trim();

                if (Regex.IsMatch(clientCert, _certificate.Thumbprint, RegexOptions.IgnoreCase)
                    | Regex.IsMatch(clientCert, certificateSubject, RegexOptions.IgnoreCase))
                {
                    Log.Info($"matched current clientCertificate:{clientCertificate}", _certificate);
                    return _certificate;
                }
            }

            if (!string.IsNullOrEmpty(Config.AzureKeyVault) & !string.IsNullOrEmpty(Config.AzureClientSecret))
            {
                _certificate = ReadCertificateFromKeyvault(Config.AzureKeyVault, Config.AzureClientSecret);
            }
            else if (FileTypes.MapFileUriType(clientCertificate) == FileUriTypesEnum.fileUri)
            {
                _certificate = ReadCertificateFromFile(clientCertificate);
            }
            else
            {
                _certificate = ReadCertificate(clientCertificate);
            }

            Log.Info($"returning certificate string:{_certificate} for clientcertificate:{clientCertificate}");
            return _certificate;
        }

        private DefaultAzureCredential GetDefaultAzureCredentials(string clientId = null)
        {
            DefaultAzureCredentialOptions credentialOptions = new DefaultAzureCredentialOptions()
            {
                Diagnostics = {
                        ApplicationId = ApplicationName,
                        IsDistributedTracingEnabled = true,
                        IsLoggingContentEnabled = true,
                        IsLoggingEnabled = true,
                        LoggedHeaderNames = {
                            "x-ms-request-id"
                        },
                        LoggedQueryParameters = {
                            "api-version"
                        }
                    }
            };

            credentialOptions.InteractiveBrowserTenantId = Config.AzureTenantId;
            credentialOptions.ManagedIdentityClientId = clientId;
            DefaultAzureCredential defaultCredential = new DefaultAzureCredential(credentialOptions);

            Log.Info($"returning defaultCredential:", defaultCredential);
            return defaultCredential;
        }

        private bool IsManagedIdentity(string managedClientId = null)
        {
            bool retval = false;
            try
            {
                ManagedIdentityCredential managedCredential = new ManagedIdentityCredential(managedClientId, new TokenCredentialOptions
                {
                    Diagnostics = {
                        ApplicationId = ApplicationName,
                        IsDistributedTracingEnabled = true,
                        IsLoggingContentEnabled = true,
                        IsLoggingEnabled = true,
                        LoggedHeaderNames = {
                            "x-ms-request-id"
                        },
                        LoggedQueryParameters = {
                            "api-version"
                        }
                    }
                });

                ManagedIdentityToken = managedCredential.GetTokenAsync(new TokenRequestContext(new string[1] { $"{_baseLogonUri}/.default" })).Result;

                retval = true;
            }
            catch (Exception e)
            {
                Log.Info($"exception:{e}");
            }

            Log.Info($"returning{retval}");
            return retval;
        }

        private bool NeedsAuthentication()
        {
            if (_tokenExpirationHalfLife > DateTime.Now)
            {
                Log.Debug("token still valid");
                return false;
            }
            else if (!IsAuthenticated)
            {
                Log.Info("authenticating to azure", ConsoleColor.Green);
            }
            else
            {
                Log.Warning($"refreshing aad token. token expiration half life: {_tokenExpirationHalfLife}");
            }

            return true;
        }

        private X509Certificate2 ReadCertificateValue(string certificateValue)
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
            return certificate;
        }
    }
}