// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Net;
namespace CollectSFData.Azure
{
    public class SasParameters
    {
        public string ApiVersion { get; set; }

        public bool IsServiceSas { get; set; }

        public string Signature { get; set; }

        public string SignedExpiry { get; set; }

        public DateTime SignedExpiryLocal { get; set; } = DateTime.MinValue;

        public DateTime SignedExpiryUtc { get; set; } = DateTime.MinValue;

        public string SignedIp { get; set; }

        public string SignedPermission { get; set; }

        public string SignedProtocol { get; set; }

        public string SignedResourceTypes { get; set; }

        public string SignedServices { get; set; }

        public string SignedStart { get; set; }

        public DateTime SignedStartLocal { get; set; } = DateTime.MinValue;

        public DateTime SignedStartUtc { get; set; } = DateTime.MinValue;

        public string SignedVersion { get; set; }

        public string SasToken{get;private set;}

        public SasParameters() { }
        public SasParameters(string sasToken)
        {
            SasToken = sasToken.TrimStart('?');

            foreach (string parameter in SasToken.Split('&'))
            {
                string paramName = parameter.ToLower().Split('=')[0];
                string encodedParamValue = parameter.Split('=')[1];
                string paramValue = WebUtility.UrlDecode(encodedParamValue);

                if (paramName.Equals("api-version")) { ApiVersion = paramValue; }
                if (paramName.Equals("sv")) { SignedVersion = paramValue; }
                if (paramName.Equals("ss")) { SignedServices = paramValue; }
                if (paramName.Equals("srt")) { SignedResourceTypes = paramValue; }
                if (paramName.Equals("sp")) { SignedPermission = paramValue; }
                if (paramName.Equals("sip")) { SignedIp = paramValue; }
                if (paramName.Equals("spr")) { SignedProtocol = paramValue; }
                if (paramName.Equals("sig")) { Signature = encodedParamValue; }

                if (paramName.Equals("st"))
                {
                    SignedStart = paramValue;
                    SignedStartUtc = ParseDate(SignedStart);
                    SignedStartLocal = SignedStartUtc.ToLocalTime();
                }

                if (paramName.Equals("se"))
                {
                    SignedExpiry = paramValue;
                    SignedExpiryUtc = ParseDate(SignedExpiry);
                    SignedExpiryLocal = SignedExpiryUtc.ToLocalTime();
                }

                // service sas
                if (paramName.Equals("sr") | paramName.Equals("tn"))
                {
                    IsServiceSas = true;
                }
            }
        }

        private DateTime ParseDate(string dateString)
        {
            DateTime.TryParse(dateString, out DateTime parsedTime);
            return parsedTime.ToUniversalTime();
        }
    }
}