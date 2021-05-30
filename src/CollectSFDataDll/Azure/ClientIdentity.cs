using Azure.Core;
using Azure.Identity;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Text;
using System.Net.Sockets;

namespace CollectSFData.Azure
{
    public class ClientIdentity
    {
        private static string _instanceMetaDataRestIp = "169.254.169.254";
        private static string _instanceMetaDataRestUri = $"http://{_instanceMetaDataRestIp}/metadata/instance?api-version=2017-08-01";
        private static bool _isManagedIdentity;
        private static bool _instanceMetaDataEndpointChecked;
        private ConfigurationOptions _config;
        public bool IsAppRegistration { get; private set; } = false;
        public bool IsSystemManagedIdentity { get; private set; } = false;
        public bool IsTypeManagedIdentity => (IsSystemManagedIdentity | IsUserManagedIdentity);
        public bool IsUserManagedIdentity { get; private set; } = false;
        public AccessToken ManagedIdentityToken { get; private set; }

        public ClientIdentity(ConfigurationOptions config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            SetIdentityType();
        }

        public DefaultAzureCredential GetDefaultAzureCredentials(string clientId = null)
        {
            DefaultAzureCredentialOptions credentialOptions = new DefaultAzureCredentialOptions()
            {
                Diagnostics = {
                        ApplicationId = Constants.ApplicationName,
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

            credentialOptions.InteractiveBrowserTenantId = _config.AzureTenantId;
            credentialOptions.ManagedIdentityClientId = clientId;
            DefaultAzureCredential defaultCredential = new DefaultAzureCredential(credentialOptions);

            Log.Info($"returning defaultCredential:", defaultCredential);
            return defaultCredential;
        }

        public bool CheckInstanceMetaDataEndpoint()
        {
            string hostUri = _instanceMetaDataRestIp;
            int portNumber = _instanceMetaDataRestUri.ToLower().StartsWith("https:") ? 443 : 80;

            try
            {
                using (TcpClient client = new TcpClient(hostUri, portNumber))
                {
                    Log.Info($"successful pinging host:{hostUri}:{portNumber}", ConsoleColor.Green);
                    return true;
                }
            }
            catch (SocketException e)
            {
                Log.Info($"error pinging host:{hostUri}:{portNumber}");
                Log.Debug($"error pinging host:{hostUri}:{portNumber}\r\n{e}");
                return false;
            }
        }

        public bool IsManagedIdentity(string managedClientId = null)
        {
            if (!_instanceMetaDataEndpointChecked)
            {
                _instanceMetaDataEndpointChecked = true;

                if (CheckInstanceMetaDataEndpoint())
                {
                    _isManagedIdentity = GetManagedIdentity(managedClientId);
                }
            }

            Log.Info($"returning:{_isManagedIdentity}");
            return _isManagedIdentity;
        }

        private bool GetManagedIdentity(string managedClientId)
        {
            bool retval = false;
            try
            {
                ManagedIdentityCredential managedCredential = new ManagedIdentityCredential(managedClientId, new TokenCredentialOptions
                {
                    Diagnostics = {
                        ApplicationId = Constants.ApplicationName,
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

                ManagedIdentityToken = managedCredential.GetTokenAsync(new TokenRequestContext(new string[1] { $"{Constants.ManagementAzureCom}/.default" })).Result;

                retval = true;
            }
            catch (Exception e)
            {
                Log.Info($"exception:{e.Message}");
            }

            return retval;
        }

        public void SetIdentityType()
        {
            if (!string.IsNullOrEmpty(_config.AzureClientId) & !string.IsNullOrEmpty(_config.AzureClientCertificate))
            {
                IsAppRegistration = true;
            }
            else if (!string.IsNullOrEmpty(_config.AzureClientId))
            {
                IsUserManagedIdentity = IsManagedIdentity(_config.AzureClientId);
            }
            else if (string.IsNullOrEmpty(_config.AzureClientId))
            {
                IsSystemManagedIdentity = IsManagedIdentity();
            }
        }
    }
}