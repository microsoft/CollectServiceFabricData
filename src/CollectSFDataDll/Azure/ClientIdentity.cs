﻿using Azure.Core;
using Azure.Identity;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CollectSFData.Azure
{
    public class ClientIdentity
    {
        private Instance _instance = Instance.Singleton();
        public bool IsAppRegistration { get; private set; } = false;
        public bool IsSystemManagedIdentity { get; private set; } = false;
        public bool IsUserManagedIdentity { get; private set; } = false;
        public AccessToken ManagedIdentityToken { get; private set; }
        private ConfigurationOptions _config => _instance.Config;

        public ClientIdentity()
        {
            if (!string.IsNullOrEmpty(_config.AzureClientId))
            {
                IsAppRegistration = true;
            }

            if (!_config.AzureManagedIdentity)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_config.AzureClientId))
            {
                IsUserManagedIdentity = IsManagedIdentity(_config.AzureClientId);
            }

            if (!IsUserManagedIdentity && string.IsNullOrEmpty(_config.AzureClientId))
            {
                IsSystemManagedIdentity = IsManagedIdentity();
            }

            IsAppRegistration = !(IsUserManagedIdentity | IsSystemManagedIdentity);
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
                Log.Info($"exception:{e}");
            }

            Log.Info($"returning{retval}");
            return retval;
        }
    }
}