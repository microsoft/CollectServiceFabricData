// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace CollectSFData.Kusto
{
    public class KustoRestTableOfContentsColumnV1 : KustoRestResponseColumnV1
    {
        public int _index;
    }

    public class KustoRestTableOfContentsRowV1
    {
        public int _index;
        public string Id;
        public string Kind;
        public string Name;
        public long Ordinal;
        public string PrettyName;

        public override string ToString()
        {
            return $"{Ordinal},{Kind},{Name},{Id},{PrettyName}";
        }
    }

    public class KustoRestTableOfContentsV1
    {
        public List<KustoRestTableOfContentsColumnV1> Columns { get; set; } = new List<KustoRestTableOfContentsColumnV1>();
        public bool HasData => Rows.Count > 0;
        public List<KustoRestTableOfContentsRowV1> Rows { get; set; } = new List<KustoRestTableOfContentsRowV1>();
    }
}