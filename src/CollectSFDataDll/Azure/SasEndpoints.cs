// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CollectSFData.Azure
{
    public class SasEndpoints
    {
        public enum SasTypes
        {
            unknown,
            blob,
            file,
            table,
            queue,
        }

        public string AbsolutePath { get; private set; }

        public string BlobEndpoint { get; private set; }

        public string ConnectionString { get; private set; }

        public string FileEndpoint { get; private set; }

        public bool IsConnectionString { get; private set; }

        public SasParameters Parameters { get; private set; }

        public string QueueEndpoint { get; private set; }

        public string SasToken { get; private set; } = string.Empty;

        public string StorageAccountName { get; private set; }

        public string StorageAccountSuffix { get; private set; }

        public string TableEndpoint { get; private set; }

        public SasEndpoints(string sasKey = "")
        {
            if (sasKey.ToLower().Contains("endpoint="))
            {
                IsConnectionString = true;
                Log.Info($"sas connection string:{sasKey}");
                SetEndpoints(sasKey);
                SetToken(sasKey);
                ConnectionString = sasKey;
            }
            else if (!string.IsNullOrEmpty(sasKey))
            {
                Log.Info($"sas key:{sasKey}");
                // verify sas is valid uri if not sasconnection
                Uri testUri = null;
                string errMessage = $"invalid uri.scheme/saskey:{sasKey}";
                testUri = new Uri(sasKey, UriKind.Absolute);

                Log.Debug("sas testUri", testUri);

                if (testUri.Scheme != Uri.UriSchemeHttp && testUri.Scheme != Uri.UriSchemeHttps)
                {
                    Log.Exception(errMessage);
                    throw new ArgumentException(errMessage);
                }

                if (SetToken(sasKey))
                {
                    sasKey = sasKey.Replace(SasToken, "");
                }
            }
            else
            {
                return;
            }

            SetStorageUriInfo(sasKey);
        }

        public bool IsPopulated()
        {
            return !(string.IsNullOrEmpty(ConnectionString)
                | string.IsNullOrEmpty(StorageAccountName)
                | string.IsNullOrEmpty(BlobEndpoint)
                | string.IsNullOrEmpty(TableEndpoint)
                | string.IsNullOrEmpty(SasToken)
                | string.IsNullOrEmpty(Parameters?.Signature));
        }

        public bool IsValid()
        {
            bool retval = true;

            if (!IsPopulated() | Parameters == null)
            {
                Log.Warning("SasEndpoint not populated");
                return false;
            }

            if (Parameters.SignedStartUtc > DateTime.Now.ToUniversalTime()
                | Parameters.SignedExpiryUtc < DateTime.Now.ToUniversalTime())
            {
                Log.Error("Sas is not time valid", Parameters);
                retval = false;
            }
            else if (Parameters.SignedExpiryUtc.AddHours(-1) < DateTime.Now.ToUniversalTime())
            {
                Log.Warning("Sas expiring in less than 1 hour", Parameters);
            }

            if (!Parameters.SignedPermission.Contains("r"))
            {
                Log.Error("Sas does not contain read permissions", Parameters);
                retval = false;
            }

            if (!Parameters.IsServiceSas)
            {
                if (!Parameters.SignedServices.Contains("b")
                    & !Parameters.SignedServices.Contains("t"))
                {
                    Log.Error("Sas does not contain blob or table signed services", Parameters);
                    retval = false;
                }
            }

            Log.Info($"exit: {retval}");
            return retval;
        }

        private void SetConnectionString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"BlobEndpoint={BlobEndpoint};");
            sb.Append($"QueueEndpoint={QueueEndpoint};");
            sb.Append($"FileEndpoint={FileEndpoint};");
            sb.Append($"TableEndpoint={TableEndpoint};");
            sb.Append($"SharedAccessSignature={SasToken.TrimStart('?')}");
            ConnectionString = sb.ToString();
        }

        private void SetEndpoints(string sasConnectionString)
        {
            string pattern = @"(^|;)(?<endpoint>(\w+?))Endpoint=(?<uri>(.+?));";

            foreach (Match match in Regex.Matches(sasConnectionString, pattern, RegexOptions.IgnoreCase))
            {
                SetType(match.Groups["endpoint"].Value, match.Groups["uri"].Value);
            }
        }

        private void SetStorageUriInfo(string uriString)
        {
            uriString = uriString.TrimEnd('/') + "/";
            string pattern = @"https?://(?<storageAccountName>.+?)\..+?\.(?<storageAccountSuffix>.+?)(\?|/$|/(?<absolutePath>.+?))(;|,|\?|$)";

            if (Regex.IsMatch(uriString, pattern, RegexOptions.IgnoreCase))
            {
                Match match = Regex.Match(uriString, pattern, RegexOptions.IgnoreCase);
                StorageAccountName = match.Groups["storageAccountName"].Value;
                StorageAccountSuffix = match.Groups["storageAccountSuffix"].Value;

                if (!IsConnectionString)
                {
                    AbsolutePath = Regex.Match(uriString, pattern, RegexOptions.IgnoreCase).Groups["absolutePath"].Value;
                }

                Log.Info($"setting storage account name:{StorageAccountName}");

                if (!string.IsNullOrEmpty(StorageAccountName) & !string.IsNullOrEmpty(StorageAccountSuffix))
                {
                    BlobEndpoint = BlobEndpoint ?? $"https://{StorageAccountName}.{SasTypes.blob}.{StorageAccountSuffix}/";
                    FileEndpoint = FileEndpoint ?? $"https://{StorageAccountName}.{SasTypes.file}.{StorageAccountSuffix}/";
                    QueueEndpoint = QueueEndpoint ?? $"https://{StorageAccountName}.{SasTypes.queue}.{StorageAccountSuffix}/";
                    TableEndpoint = TableEndpoint ?? $"https://{StorageAccountName}.{SasTypes.table}.{StorageAccountSuffix}/";
                }

                if (string.IsNullOrEmpty(ConnectionString))
                {
                    SetConnectionString();
                }
            }
            else
            {
                Log.Warning($"unable to determine storage account name:{uriString} with pattern:{pattern}");
            }
        }

        private bool SetToken(string sasString)
        {
            string pattern = @"(SharedAccessSignature=|\?)(?<sas>.+?)(;|$|\s|,)";

            if (Regex.IsMatch(sasString, pattern, RegexOptions.IgnoreCase))
            {
                Match match = Regex.Match(sasString, pattern, RegexOptions.IgnoreCase);
                SasToken = "?" + match.Groups["sas"].Value.TrimStart('?');
                Parameters = new SasParameters(SasToken);
                Log.Info($"set token {SasToken}");
                return true;
            }

            Log.Warning($"unable to set token {sasString}");
            return false;
        }

        private bool SetType(string sasType, string uri = null)
        {
            Log.Info($"{sasType}:{uri}");
            if (string.IsNullOrEmpty(sasType))
            {
                Log.Warning("empty/null sasType. returning.");
                return false;
            }

            switch (Enum.Parse(typeof(SasTypes), sasType.ToLower()))
            {
                case SasTypes.blob:
                    BlobEndpoint = uri;
                    break;

                case SasTypes.file:
                    FileEndpoint = uri;
                    break;

                case SasTypes.queue:
                    QueueEndpoint = uri;
                    break;

                case SasTypes.table:
                    TableEndpoint = uri;
                    break;

                default:
                    break;
            }

            return true;
        }
    }
}