using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Thimens.DataMapper;
using Thimens.DataMapper.Old;
using System.ComponentModel;
using System.Globalization;

namespace Thimens.DataMapper.New
{
    public static class Extensions
    {
        /// <summary>
        /// Executes the <paramref name="query"/> and returns the number of rows affected.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static int ExecuteNonQuery(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters) => 
            database.ExecuteNonQuery(CreateCommand(database, commandType, query, parameters));

        /// <summary>
        /// Executes the <paramref name="query"/> and returns the first column of the first row in the result set returned by the query. Extra columns or rows are ignored.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object ExecuteScalar(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters) => 
            database.ExecuteScalar(CreateCommand(database, commandType, query, parameters));

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public static T Get<T>(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters, params string[] keys)
        {
            if (IsListType(typeof(T)))
                return Get<ContainerClass<T>>(database, commandType, query, parameters, keys).Content;

            using (IDataReader dataReader = database.ExecuteReader(CreateCommand(database, commandType, query, parameters)))
            {
                T obj = default(T);

                var maps = GetDataMaps<T>(GetColumnsDictonary(dataReader), keys);

                while (dataReader.Read())
                {
                    if (!IsValueType(typeof(T)))
                        GetObjectFromDataReader(dataReader, maps, ref obj);
                    else
                        obj = (T)dataReader[0];
                }

                return obj;
            }
        }
        
        private static void GetObjectFromDataReader<T>(IDataReader dataReader, IEnumerable<DataMap> maps, ref T obj)
        {
            if (obj == null)
                obj = Activator.CreateInstance<T>();

            foreach(var map in maps)
            {
                var property = map.Property;
                var propertyType = property?.PropertyType;

                if (map.MapType == MapType.Value)
                    property.SetValue(obj, ConvertValue(dataReader[map.Column], propertyType));
                else if (map.MapType == MapType.Reference)
                {
                    var oProp = Activator.CreateInstance(propertyType);
                    GetObjectFromDataReader(dataReader, map.Maps, ref oProp);
                    property.SetValue(obj, oProp);
                }
                else if (map.MapType == MapType.List || map.MapType == MapType.ValueList)
                {                    
                    var listInfo = (dynamic)map.GetListMethod.Invoke(null, new object[] { obj, map, dataReader });

                    if (map.MapType != MapType.ValueList)
                        GetObjectFromDataReader(dataReader, map.Maps, ref listInfo.Item3);

                    if (listInfo.Item1)
                        listInfo.Item2.Add(listInfo.Item3);

                    property.SetValue(obj, listInfo.Item2);
                }
            }
        }

        private static (bool IsNewItem, ICollection<T> List, T Item) GetList<T>(object sourceObj, DataMap map, IDataReader dataReader)
        {
            var isNewItem = true;
            T item = Activator.CreateInstance<T>();
            var keys = map.Maps.Where(m => m.IsKey);

            var list = map.Property.GetValue(sourceObj) as ICollection<T>;

            if (keys.Any())
            {
                if (list == null)
                    list = new HashSet<T>(new HashItemEqualityComparer<T>(map.MapType == MapType.ValueList ? null : keys.Select(k => k.Property).ToArray()));

                foreach (var key in keys)
                    if (map.MapType == MapType.ValueList)
                        item = (T)ConvertValue(dataReader[key.Column], typeof(T));
                    else
                        key.Property.SetValue(item, ConvertValue(dataReader[key.Column], key.Property.PropertyType));

                if (((HashSet<T>)list).Contains(item))
                {
                    isNewItem = false;
                    item = (((HashSet<T>)list).Comparer as HashItemEqualityComparer<T>).HashItem;
                }
            }
            else
            {
                if (list == null)
                    list = new List<T>();

                if (map.MapType == MapType.ValueList)
                    item = (T)ConvertValue(dataReader[map.Maps.First().Column], typeof(T));
            }

            return (isNewItem, list, item);
        }


        /// <summary>
        /// Converts source obj to Type destinationType
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="destinationType">Type of returned object</param>
        /// <returns></returns>
        private static object ConvertValue(object obj, Type destinationType)
        {
            if (destinationType.IsEnum)
            {
                //check if enum property has an DefaultValueAttribute annotation to be validated. 
                foreach (var eValor in Enum.GetValues(destinationType))
                {
                    var attribute = (DefaultValueAttribute)destinationType.GetField(eValor.ToString()).GetCustomAttributes(typeof(DefaultValueAttribute), false).FirstOrDefault();

                    if (attribute != null && attribute.Value.Equals(obj))
                        return eValor;
                }

                return Enum.Parse(destinationType, obj.ToString(), true);
            }
            else if (obj.GetType() == typeof(string))
                //check if the database field are a string used as boolean ('Y', 'N') and the property is boolean
                if (destinationType == typeof(bool))
                    return obj.ToString().Trim().Replace('"', '\'').ToLower() == "y" ? true : false;
                else
                    return Convert.ChangeType(obj.ToString().Trim().Replace('"', '\''), Nullable.GetUnderlyingType(destinationType) ?? destinationType);
            else
                return Convert.ChangeType(obj, Nullable.GetUnderlyingType(destinationType) ?? destinationType);
        }


        private static IEnumerable<DataMap> GetDataMaps<T>(IDictionary<string, string[]> columns, IEnumerable<string> keys)
        {
            //properties to return
            var maps = new List<DataMap>();
            var columnGroups = columns.GroupBy(c => c.Value[0]);

            var t = typeof(ContainerClass<>);
            var w = typeof(T);

            //ContainerClass is not mapped on columns. Used as container for lists on Get<IEnumarable<T>> calls
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ContainerClass<>))
                AddMap(columns, typeof(T).GetProperty("Content"), "");
            else
            {
                //properties of T
                var tProps = new Dictionary<string, PropertyInfo>();

                //value types do not have properties, so fake one
                if (IsValueType(typeof(T)))                
                    tProps.Add(columnGroups.First().Key, typeof(ContainerClass<T>).GetProperty("Content"));
                else
                    tProps = typeof(T).GetProperties()?.ToDictionary(p => p.Name.ToLower());

                foreach (var columnGroup in columnGroups)
                    if (tProps.TryGetValue(columnGroup.Key, out PropertyInfo property))
                        AddMap(columnGroup, property, columnGroup.First().Key);
            }          

            return maps;

            //local function to create and add a map to maplist
            void AddMap(IEnumerable<KeyValuePair<string, string[]>> columnGroup, PropertyInfo property, string columnName)
            {
                //value type
                if (IsValueType(property.PropertyType))
                    maps.Add(new DataMap()
                    {
                        Column = columnName,
                        Property = property,
                        MapType = MapType.Value,
                        IsKey = keys.Any(k => k.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    });
                else
                {
                    Type propType;
                    MapType mapType;

                    //list
                    if (IsListType(property.PropertyType))
                    {
                        if (!property.PropertyType.IsAssignableFrom(typeof(List<>).MakeGenericType(property.PropertyType.GetGenericArguments())) && !property.PropertyType.IsAssignableFrom(typeof(HashSet<>).MakeGenericType(property.PropertyType.GetGenericArguments())))
                            throw new ApplicationException($"Could not resolve property {property.Name} of type {property.PropertyType.Name}. The property must be assingnabe from a List<T> if has no keys, or from a HashSet<T> if has keys");

                        if (property.PropertyType.IsArray)
                            propType = property.PropertyType.GetElementType();
                        else
                            propType = property.PropertyType.GetGenericArguments()[0];

                        if (IsValueType(propType))
                            mapType = MapType.ValueList;
                        else
                            mapType = MapType.List;
                    }
                    //class or interface
                    else
                    {
                        mapType = MapType.Reference;
                        propType = property.PropertyType;
                    }

                    var columnsDict = new Dictionary<string, string[]>();

                    foreach (var column in columnGroup)
                    {
                        var arr = column.Value.ToList();

                        if (!string.IsNullOrWhiteSpace(columnName))
                            arr.RemoveAt(0);

                        if (arr.Count > 0)
                            columnsDict[column.Key] = arr.ToArray();
                    }

                    var map = new DataMap()
                    {
                        Property = property,
                        MapType = mapType,
                        GetListMethod = ((Func<object, DataMap, IDataReader, (bool, ICollection<int>, int)>)GetList<int>).Method.GetGenericMethodDefinition().MakeGenericMethod(propType),
                        Maps = (IEnumerable<DataMap>)((Func<IDictionary<string, string[]>, IEnumerable<string>, IEnumerable<DataMap>>)GetDataMaps<string>)
                        .Method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(propType)
                        .Invoke(null, new object[] { columnsDict, keys })
                    };

                    maps.Add(map);
                }
            }
        }

        private static IDictionary<string, string[]> GetColumnsDictonary(IDataReader dataReader)
        {
            var dict = new Dictionary<string, string[]>();

            for (int i = 0; i < dataReader.FieldCount; i++)
            {
                var column = dataReader.GetName(i).ToLower();
                dict.Add(column, column.Split('.'));
            }

            return dict;
        }

        private static bool IsValueType(Type type)
        {
            return type.IsValueType || type == typeof(string);
        }

        private static bool IsListType(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
        }

        private static DbCommand CreateCommand(Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters)
        {
            var command = database.GetSqlStringCommand(query);
            command.CommandType = commandType;

            if (parameters != null)
                foreach (var parameter in parameters)
                    database.AddParameter(command, parameter.Name, parameter.DbType, parameter.Direction, null, parameter.SourceVersion, parameter.Value);

            return command;
        }
    }
    
    internal class ContainerClass<T>
    {
        public T Content { get; set; }
    }
}
