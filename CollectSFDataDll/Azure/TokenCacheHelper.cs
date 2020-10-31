// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CollectSFData.Azure
{
    public static class TokenCacheHelper
    {
        private static string friendlyName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
        private static string appDataFolder = $"{Environment.GetEnvironmentVariable("LOCALAPPDATA")}\\{friendlyName}";
        public static readonly string CacheFilePath = $"{appDataFolder}\\{friendlyName}.msalcache.bin3";
        private static readonly object FileLock = new object();

        static TokenCacheHelper()
        {
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
        }

        public static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (args.HasStateChanged)
            {
                lock (FileLock)
                {
                    File.WriteAllBytes(CacheFilePath,
                        ProtectedData.Protect(args.TokenCache.SerializeMsalV3(), null, DataProtectionScope.CurrentUser));
                }
            }
        }

        public static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                args.TokenCache.DeserializeMsalV3(File.Exists(CacheFilePath)
                        ? ProtectedData.Unprotect(File.ReadAllBytes(CacheFilePath), null, DataProtectionScope.CurrentUser)
                        : null);
            }
        }

        public static void EnableSerialization(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(BeforeAccessNotification);
            tokenCache.SetAfterAccess(AfterAccessNotification);
        }
    }
}