using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace Thimens.DataMapper
{
    public class NameOf<T> : DynamicObject
    {
        internal string _name;
        private readonly string _prefix = string.Empty;
        private readonly string _suffix = string.Empty;

        public NameOf() { }

        public NameOf(string prefix, string suffix)
        {
            _prefix = prefix;
            _suffix = suffix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="affix"></param>
        public NameOf(NameOfAffix affix)
        {
            switch (affix)
            {
                case NameOfAffix.SQL:
                    _prefix = "[";
                    _suffix = "]";
                    break;

                case NameOfAffix.DB2:
                    _prefix = "\"";
                    _suffix = "\"";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(affix), affix, null);
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = GetProperty(binder.Name);
            return result != null;
        }

        public override string ToString() => $"{_prefix}{_name}{_suffix}";
        public string ToSQL => $"[{_name}]";
        public string ToDB2 => $@"""{_name}""";

        public static implicit operator string(NameOf<T> p) => p.ToString();

        private dynamic GetProperty(string propertyName)
        {
            //get property
            var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase)
                ?? throw new MissingMemberException($@"Member ""{propertyName}"" of ""{typeof(T).Name}"" was not found");

            var propertyType = property.PropertyType;

            // check if property is list and get inner type
            if (IsListType(propertyType))
                propertyType = property.PropertyType.IsArray ? propertyType.GetElementType() : propertyType.GetGenericArguments()[0];

            var type = typeof(NameOf<>).MakeGenericType(propertyType);
            var name = $"{_name}{(string.IsNullOrEmpty(_name) ? string.Empty : ".")}{property.Name}";
            var result = (dynamic)Activator.CreateInstance(type, _prefix, _suffix);
            result._name = name;

            return result;
        }

        private static bool IsListType(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
        }
    }

    public enum NameOfAffix
    {
        SQL,
        DB2
    }
}
