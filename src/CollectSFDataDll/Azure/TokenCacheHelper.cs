// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CollectSFData.Azure
{
    public static class TokenCacheHelper
    {
        public static readonly string CacheFilePath;
        private static readonly object _fileLock = new object();
        private static string _appDataFolder;
        private static string _friendlyName;

        static TokenCacheHelper()
        {
            _friendlyName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
            _appDataFolder = $"{Environment.GetEnvironmentVariable("LOCALAPPDATA")}\\{_friendlyName}";
            CacheFilePath = $"{_appDataFolder}\\{_friendlyName}.msalcache.bin3";

            try
            {
                if (!Directory.Exists(_appDataFolder))
                {
                    Directory.CreateDirectory(_appDataFolder);
                }
            }
            catch
            {
                Log.Warning($"unable to create directory {_appDataFolder}");
            }
        }

        public static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            try
            {
                if (args.HasStateChanged)
                {
                    lock (_fileLock)
                    {
                        File.WriteAllBytes(CacheFilePath,
                            ProtectedData.Protect(args.TokenCache.SerializeMsalV3(), null, DataProtectionScope.CurrentUser));
                    }
                }
            }
            catch
            {
                Log.Warning($"unable to write file {CacheFilePath}");
            }
        }

        public static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            try
            {
                lock (_fileLock)
                {
                    args.TokenCache.DeserializeMsalV3(File.Exists(CacheFilePath)
                            ? ProtectedData.Unprotect(File.ReadAllBytes(CacheFilePath), null, DataProtectionScope.CurrentUser)
                            : null);
                }
            }
            catch
            {
                Log.Warning($"unable to read file {CacheFilePath}");
            }
        }

        public static void EnableSerialization(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(BeforeAccessNotification);
            tokenCache.SetAfterAccess(AfterAccessNotification);
        }
    }
}