// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CollectSFData.Common;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Azure
{
    public class AzureResourceManager
    {
        private string _commonTenantId = "common";
        private IConfidentialClientApplication _confidentialClientApp;
        private ConfigurationOptions _config;
        private List<string> _defaultScope = new List<string>() { ".default" };
        private string _getSubscriptionRestUri = Constants.ManagementAzureCom + "/subscriptions/{subscriptionId}?api-version=2016-06-01";
        private Http _httpClient = Http.ClientFactory();
        private string _listSubscriptionsRestUri = Constants.ManagementAzureCom + "/subscriptions?api-version=2016-06-01";
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

        public ClientIdentity ClientIdentity { get; private set; }

        public bool IsAuthenticated { get; private set; }
        public List<string> Scopes { get; set; } = new List<string>();
        public SubscriptionRecordResult[] Subscriptions { get; private set; } = new SubscriptionRecordResult[] { };

        public AzureResourceManager(ConfigurationOptions config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Log.Info($"enter: token cache path: {TokenCacheHelper.CacheFilePath}");
            ClientIdentity = new ClientIdentity(_config);
        }

        public bool Authenticate(bool throwOnError = false, string resource = Constants.ManagementAzureCom)
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
                if (string.IsNullOrEmpty(_config.AzureTenantId))
                {
                    _config.AzureTenantId = _commonTenantId;
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
            string uri = $"{Constants.ManagementAzureCom}{resourceId}?{Constants.ArmApiVersion}";

            if (_httpClient.SendRequest(uri: uri, authToken: BearerToken, httpMethod: HttpMethod.Head))
            {
                return _httpClient.StatusCode == System.Net.HttpStatusCode.NoContent;
            }

            return false;
        }

        public bool CreateClient(bool prompt, bool deviceLogin = false, string resource = "")
        {
            if (_config.IsClientIdConfigured() & prompt)
            {
                return false;
            }
            else if (_config.IsClientIdConfigured() & !prompt)
            {
                if (_config.ClientCertificate != null)
                {
                    CreateConfidentialCertificateClient(resource, _config.ClientCertificate);
                }
                else if (!string.IsNullOrEmpty(_config.AzureKeyVault) & !string.IsNullOrEmpty(_config.AzureClientSecret))
                {
                    CreateConfidentialCertificateClient(resource, ReadCertificateFromKeyvault(_config.AzureKeyVault, _config.AzureClientSecret));
                }
                else if (ClientIdentity.IsTypeManagedIdentity)
                {
                    CreateConfidentialManagedIdentityClient(resource);
                }
                else if (!string.IsNullOrEmpty(_config.AzureClientCertificate))
                {
                    CreateConfidentialCertificateClient(resource, new CertificateUtilities().GetClientCertificate(_config.AzureClientCertificate));
                }
                else if (!string.IsNullOrEmpty(_config.AzureClientSecret))
                {
                    CreateConfidentialClient(resource, _config.AzureClientSecret);
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

        public void CreateConfidentialCertificateClient(string resource, X509Certificate2 clientCertificate)
        {
            Log.Info($"enter: {resource}");
            _confidentialClientApp = ConfidentialClientApplicationBuilder
                .CreateWithApplicationOptions(new ConfidentialClientApplicationOptions
                {
                    ClientId = _config.AzureClientId,
                    RedirectUri = resource,
                    TenantId = _config.AzureTenantId,
                    ClientName = Constants.ApplicationName
                })
                .WithAuthority(AzureCloudInstance.AzurePublic, _config.AzureTenantId)
                .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
                .WithCertificate(clientCertificate)
                .Build();
            AddClientScopes();
        }

        public void CreateConfidentialClient(string resource, string secret)
        {
            Log.Info($"enter: {resource}");
            // no prompt with clientid and secret
            _confidentialClientApp = ConfidentialClientApplicationBuilder
               .CreateWithApplicationOptions(new ConfidentialClientApplicationOptions
               {
                   ClientId = _config.AzureClientId,
                   RedirectUri = resource,
                   ClientSecret = secret,
                   TenantId = _config.AzureTenantId,
                   ClientName = _config.AzureClientId
               })
               .WithAuthority(AzureCloudInstance.AzurePublic, _config.AzureTenantId)
               .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
               .Build();

            AddClientScopes();
        }

        public void CreateConfidentialManagedIdentityClient(string resource)
        {
            Log.Info($"enter: {resource}");
            // no prompt with clientid and secret
            AuthenticationResultToken = new AccessToken(ClientIdentity.ManagedIdentityToken.Token, ClientIdentity.ManagedIdentityToken.ExpiresOn);
            SetToken();
        }

        public bool CreatePublicClient(bool prompt, bool deviceLogin = false)
        {
            Log.Info($"enter: {prompt} {deviceLogin}");
            AuthenticationResult result = null;
            _publicClientApp = PublicClientApplicationBuilder
                .Create(_wellKnownClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, _config.AzureTenantId)
                .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
                .WithDefaultRedirectUri()
                .Build();

            TokenCacheHelper.EnableSerialization(_publicClientApp.UserTokenCache);

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
                string uri = $"{Constants.ManagementAzureCom}{resourceId}?{Constants.ArmApiVersion}";
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

            if (Subscriptions.Length == 0 && !string.IsNullOrEmpty(_config.AzureSubscriptionId))
            {
                response = _httpClient.SendRequest(_getSubscriptionRestUri, BearerToken
                    .Replace("{subscriptionId}", _config.AzureSubscriptionId));

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

        public Http ProvisionResource(string resourceId, string body = "", string apiVersion = Constants.ArmApiVersion)
        {
            Log.Info("enter");
            string uri = $"{Constants.ManagementAzureCom}{resourceId}?{apiVersion}";

            if (_httpClient.SendRequest(uri: uri, authToken: BearerToken, jsonBody: body, httpMethod: HttpMethod.Put))
            {
                int count = 0;

                // wait for state
                while (count < Constants.RetryCount)
                {
                    bool response = _httpClient.SendRequest(uri: uri, authToken: BearerToken);
                    GenericResourceResult result = JsonConvert.DeserializeObject<GenericResourceResult>(_httpClient.ResponseStreamString);

                    if (result.properties.provisioningState.ToLower() == "succeeded")
                    {
                        Log.Info($"resource provisioned {resourceId}", ConsoleColor.Green);
                        return _httpClient;
                    }

                    count++;
                    Log.Info($"requery count: {count} of {Constants.RetryCount} response: {response}");
                    Thread.Sleep(Constants.ThreadSleepMs10000);
                }
            }

            Log.Error($"unable to provision {resourceId}");
            _httpClient.Success = false;
            return _httpClient;
        }

        public X509Certificate2 ReadCertificateFromKeyvault(string keyvaultResourceId, string secretName)
        {
            Log.Info($"enter:{keyvaultResourceId} {secretName}");
            X509Certificate2 certificate = null;
            TokenCredential credential = null;
            string clientId = _config.AzureClientId;

            if (ClientIdentity.IsSystemManagedIdentity)
            {
                clientId = "";
            }

            try
            {
                credential = ClientIdentity.GetDefaultAzureCredentials(clientId);
                SecretClient client = new SecretClient(new Uri(keyvaultResourceId), credential);
                KeyVaultSecret secret = client.GetSecret(secretName);

                //certificate = new X509Certificate2(Convert.FromBase64String(secret.Value), GetPassword(), X509KeyStorageFlags.Exportable);
                certificate = new X509Certificate2(Convert.FromBase64String(secret.Value), string.Empty, X509KeyStorageFlags.Exportable);
                return certificate;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                certificate = null;
            }

            return certificate;
        }

        public void Reauthenticate(object state = null)
        {
            Log.Highlight("azure ad reauthenticate");
            Authenticate(true, _resource);
        }

        public Http SendRequest(string uri, HttpMethod method = null, string body = "")
        {
            method = method ?? _httpClient.Method;
            _httpClient.SendRequest(uri: uri, authToken: BearerToken, jsonBody: body, httpMethod: method);
            return _httpClient;
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

        private void AddClientScopes()
        {
            if (Scopes.Count < 1)
            {
                Scopes = _defaultScope;
            }

            TokenCacheHelper.EnableSerialization(_confidentialClientApp.AppTokenCache);

            foreach (string scope in Scopes)
            {
                AuthenticationResult result = _confidentialClientApp
                    .AcquireTokenForClient(new List<string>() { scope })
                    .ExecuteAsync().Result;
                Log.Debug($"scope authentication result:", AuthenticationResultToken);
                AuthenticationResultToken = new AccessToken(result.AccessToken, result.ExpiresOn);
            }
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
    }
}