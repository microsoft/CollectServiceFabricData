// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData
{
    public class GenericResourceResult
    {
        public string id;
        public Identity identity = new Identity();
        public string kind;
        public string location;
        public string managedBy;
        public string name;
        public Plan plan = new Plan();
        public Properties properties = new Properties();
        public Sku sku = new Sku();
        public string type;

        public class Identity
        {
            public string principalId;
            public string tenantId;
            public ResourceIdentityType type = new ResourceIdentityType();

            public class ResourceIdentityType
            {
                public string None;
                public string SystemAssigned;
                public string UserAssigned;
            }
        }

        public class Plan
        {
            public string name;
            public string product;
            public string promotionCode;
            public string publisher;
            public string version;
        }

        public class Properties
        {
            public string provisioningState;
        }

        public class Sku
        {
            public string capacity;
            public string family;
            public string model;
            public string name;
            public string size;
            public string tier;
        }
    }
}