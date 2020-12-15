// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.DataFile;
using System.Collections.Generic;

namespace CollectSFData.Kusto
{
    public class KustoCsvSchema
    {
        public string ConstValue;
        public string DataType;
        public string Name;
        public int Ordinal;
    }

    public class KustoIngestionMappings
    {
        private FileObject _fileObject;

        public string ResourceUri { get; set; }

        public bool SetConstants { get; set; }

        public KustoIngestionMappings(FileObject fileObject)
        {
            _fileObject = fileObject;
        }

        public IEnumerable<KustoCsvSchema> CounterSchema()
        {
            int count = 0;
            List<KustoCsvSchema> mapping = new List<KustoCsvSchema>()
            {
                new KustoCsvSchema() { Name = "Timestamp", DataType = "datetime", Ordinal = count++ },
                new KustoCsvSchema() { Name = "CounterName", DataType = "string",  Ordinal = count++ },
                new KustoCsvSchema() { Name = "CounterValue", DataType = "float",  Ordinal = count++ },
                new KustoCsvSchema() { Name = "Object", DataType = "string",  Ordinal = count++ },
                new KustoCsvSchema() { Name = "Counter", DataType = "string",  Ordinal = count++ },
                new KustoCsvSchema() { Name = "Instance", DataType = "string",  Ordinal = count }
            };

            return AddConstants(mapping);
        }

        public IEnumerable<KustoCsvSchema> CsvSchema(List<string> csvColumnNames)
        {
            List<KustoCsvSchema> mapping = new List<KustoCsvSchema>();
            foreach (string csvColumnName in csvColumnNames)
            {
                mapping.Add(new KustoCsvSchema() { Name = csvColumnName, DataType = "string", Ordinal = mapping.Count });
            }

            AddCommon(mapping);
            return mapping;
        }

        public IEnumerable<KustoCsvSchema> CsvSchema(List<KustoCsvSchema> kustoCsvSchemas = null)
        {
            kustoCsvSchemas = kustoCsvSchemas ?? new List<KustoCsvSchema>();
            AddCommon(kustoCsvSchemas);
            return kustoCsvSchemas;
        }

        public IEnumerable<KustoCsvSchema> ExceptionSchema()
        {
            int count = 0;

            List<KustoCsvSchema> mapping = new List<KustoCsvSchema>()
            {
                new KustoCsvSchema() { Name = "Timestamp", DataType = "datetime", Ordinal = count++ },
                new KustoCsvSchema() { Name = "PID", DataType = "int", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Type", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Text", DataType = "string", Ordinal = count }
            };

            return AddConstants(mapping);
        }

        public IEnumerable<KustoCsvSchema> SetupSchema()
        {
            int count = 0;

            List<KustoCsvSchema> mapping = new List<KustoCsvSchema>()
            {
                new KustoCsvSchema() { Name = "Timestamp", DataType = "datetime", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Level", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "PID", DataType = "int", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Type", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Text", DataType = "string", Ordinal = count }
            };

            return AddConstants(mapping);
        }

        public IEnumerable<KustoCsvSchema> TableSchema()
        {
            int count = 0;

            List<KustoCsvSchema> mapping = new List<KustoCsvSchema>()
            {
                new KustoCsvSchema() { Name = "Timestamp", DataType = "datetime", Ordinal = count++ },
                new KustoCsvSchema() { Name = "EventTimeStamp", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "ETag", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "PartitionKey", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "RowKey", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "PropertyName", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "PropertyValue", DataType = "string", Ordinal = count }
            };

            AddCommon(mapping);

            return mapping;
        }

        public IEnumerable<KustoCsvSchema> TraceSchema()
        {
            int count = 0;

            List<KustoCsvSchema> mapping = new List<KustoCsvSchema>()
            {
                new KustoCsvSchema() { Name = "Timestamp", DataType = "datetime", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Level", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "TID", DataType = "int", Ordinal = count++ },
                new KustoCsvSchema() { Name = "PID", DataType = "int", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Type", DataType = "string", Ordinal = count++ },
                new KustoCsvSchema() { Name = "Text", DataType = "string", Ordinal = count }
            };

            return AddConstants(mapping);
        }

        private void AddCommon(List<KustoCsvSchema> mapping)
        {
            if (SetConstants)
            {
                mapping.Add(new KustoCsvSchema() { Name = "RelativeUri", DataType = "string", ConstValue = _fileObject.RelativeUri });

                if (!string.IsNullOrEmpty(ResourceUri))
                {
                    mapping.Add(new KustoCsvSchema() { Name = "ResourceUri", DataType = "string", ConstValue = ResourceUri });
                }
            }
            else
            {
                mapping.Add(new KustoCsvSchema() { Name = "RelativeUri", DataType = "string", Ordinal = mapping.Count });

                if (!string.IsNullOrEmpty(ResourceUri))
                {
                    mapping.Add(new KustoCsvSchema() { Name = "ResourceUri", DataType = "string", Ordinal = mapping.Count });
                }
            }
        }

        private List<KustoCsvSchema> AddConstants(List<KustoCsvSchema> mapping)
        {
            if (SetConstants)
            {
                mapping.Add(new KustoCsvSchema() { Name = "NodeName", DataType = "string", ConstValue = _fileObject.NodeName });
                mapping.Add(new KustoCsvSchema() { Name = "FileType", DataType = "string", ConstValue = _fileObject.FileDataType.ToString() });
            }
            else
            {
                mapping.Add(new KustoCsvSchema() { Name = "NodeName", DataType = "string", Ordinal = mapping.Count });
                mapping.Add(new KustoCsvSchema() { Name = "FileType", DataType = "string", Ordinal = mapping.Count });
            }

            AddCommon(mapping);
            return mapping;
        }
    }
}