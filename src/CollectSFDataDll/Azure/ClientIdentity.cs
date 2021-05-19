﻿using Azure.Core;
using Azure.Identity;
using CollectSFData.Common;
using System;

namespace CollectSFData.Azure
{
    public class ClientIdentity
    {
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

        public bool IsManagedIdentity(string managedClientId = null)
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

            Log.Info($"returning{retval}");
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