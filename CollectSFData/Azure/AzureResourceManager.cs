// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace CollectSFData.Azure
{
    public class AzureResourceManager : Instance
    {
        private string _commonTenantId = "common";
        private IConfidentialClientApplication _confidentialClientApp;
        private List<string> _defaultScope = new List<string>() { ".default" };
        private string _getSubscriptionRestUri = "https://management.azure.com/subscriptions/{subscriptionId}?api-version=2016-06-01";
        private Http _httpClient = Http.ClientFactory();
        private string _listSubscriptionsRestUri = "https://management.azure.com/subscriptions?api-version=2016-06-01";
        private IPublicClientApplication _publicClientApp;
        private string _resource;
        private Timer _timer;
        private DateTimeOffset _tokenExpirationHalfLife;
        private string _wellKnownClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
        public AuthenticationResult AuthenticationResult { get; private set; }
        public string BearerToken { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public List<string> Scopes { get; set; } = new List<string>();
        public SubscriptionRecordResult[] Subscriptions { get; private set; } = new SubscriptionRecordResult[] { };

        public bool Authenticate(bool throwOnError = false, string resource = ManagementAzureCom, bool prompt = false)
        {
            Log.Debug("azure ad:enter");
            _resource = resource;

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

            try
            {
                if (string.IsNullOrEmpty(Config.AzureTenantId))
                {
                    Config.AzureTenantId = _commonTenantId;
                }

                if (Config.IsClientIdConfigured())
                {
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

                    TokenCacheHelper.EnableSerialization(_confidentialClientApp.UserTokenCache);
                    AuthenticationResult = _confidentialClientApp
                        .AcquireTokenForClient(_defaultScope)
                        .ExecuteAsync().Result;
                }
                else
                {
                    _publicClientApp = PublicClientApplicationBuilder
                        .Create(_wellKnownClientId)
                        .WithAuthority(AzureCloudInstance.AzurePublic, Config.AzureTenantId)
                        .WithLogging(MsalLoggerCallback, LogLevel.Verbose, true, true)
                        .WithDefaultRedirectUri()
                        .Build();

                    TokenCacheHelper.EnableSerialization(_publicClientApp.UserTokenCache);
                    AuthenticationResult = _publicClientApp
                        .AcquireTokenSilent(_defaultScope, _publicClientApp.GetAccountsAsync().Result.FirstOrDefault())
                        .ExecuteAsync().Result;
                }

                if (Scopes.Count > 0)
                {
                    Log.Info($"adding scopes {Scopes.Count}");
                    AuthenticationResult = _publicClientApp
                        .AcquireTokenSilent(Scopes, _publicClientApp.GetAccountsAsync().Result.FirstOrDefault())
                        .ExecuteAsync().Result;
                }

                BearerToken = AuthenticationResult.AccessToken;
                long tickDiff = ((AuthenticationResult.ExpiresOn.ToLocalTime().Ticks - DateTime.Now.Ticks) / 2) + DateTime.Now.Ticks;
                _tokenExpirationHalfLife = new DateTimeOffset(new DateTime(tickDiff));

                Log.Info($"authentication result:", ConsoleColor.Green, null, AuthenticationResult);
                Log.Highlight($"aad token expiration: {AuthenticationResult.ExpiresOn.ToLocalTime()}");
                Log.Highlight($"aad token half life expiration: {_tokenExpirationHalfLife}");

                _timer = new Timer(Reauthenticate, null, Convert.ToInt32((_tokenExpirationHalfLife - DateTime.Now).TotalMilliseconds), Timeout.Infinite);
                IsAuthenticated = true;

                return true;
            }
            catch (MsalUiRequiredException)
            {
                AuthenticationResult = _publicClientApp
                    .AcquireTokenInteractive(_defaultScope)
                    .ExecuteAsync().Result;
                return Authenticate(throwOnError, resource, true);
            }
            catch (AggregateException ae)
            {
                Log.Exception($"aggregate exception:{ae}");

                if (ae.GetBaseException() is MsalException)
                {
                    MsalException me = ae.GetBaseException() as MsalException;
                    Log.Exception($"msal exception:{me}");

                    if (me.ErrorCode.Contains("interaction_required") && !prompt)
                    {
                        return Authenticate(throwOnError, resource, true);
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");

                if (throwOnError)
                {
                    throw;
                }

                IsAuthenticated = false;
                return false;
            }
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

        public void MsalLoggerCallback(LogLevel level, string message, bool containsPII)
        {
            if (!containsPII | (containsPII & Config.LogDebug))
            {
                Log.Info($"{level} {message}");
            }
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
    }
}