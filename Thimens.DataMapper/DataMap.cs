using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Thimens.DataMapper
{
    internal class DataMap
    {
        public PropertyInfo Property { get; set; }
        public string Column { get; set; }
        public bool IsKey { get; set; }
        public MapType MapType { get; set; }
        public IEnumerable<DataMap> Maps { get; set; }
        public Type ListInnerType { get; set; }
        public MethodInfo GetListInfoMethod { get; set; }
    }

    internal enum MapType
    {
        Value,
        Reference,
        List,
        ValueList
    }
}
