using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;

namespace Thimens.DataMapper
{
    /// <summary>
    /// Extends the Database class
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Executes the <paramref name="query"/> and returns the number of rows affected.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters">Inform null if no parameter is necessary</param>
        /// <returns></returns>
        public static int ExecuteNonQuery(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters) =>
            database.ExecuteNonQuery(CreateCommand(database, commandType, query, parameters));

        /// <summary>
        /// Executes the <paramref name="query"/> and returns the first column of the first row in the result set returned by the query. Extra columns or rows are ignored.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters">Inform null if no parameter is necessary</param>
        /// <returns></returns>
        public static object ExecuteScalar(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters) =>
            database.ExecuteScalar(CreateCommand(database, commandType, query, parameters));

        /// <summary>
        /// Return a list from the query result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        [Obsolete("List<T> method is deprecated. Please use Get<U> instead, where U is a list of T, e.g., Get<IEnumerable<T>> or Get<ICollection<T>>", true)]
        public static IEnumerable<T> List<T>(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters, params string[] keys)
        {
            return null;
        }

        /// <summary>
        /// Return T object from the result set
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="keys">Fields from query result that will be used as lists keys</param>
        /// <returns></returns>
        public static T Get<T>(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters, params string[] keys)
        {
            //use a container to enclose lists
            if (IsListType(typeof(T)))
            {
                var container = Get<ContainerClass<T>>(database, commandType, query, parameters, keys);

                return container != null ? container.Content : CreateEmptyList<T>();
                
            }

            //read data from database
            using (IDataReader dataReader = database.ExecuteReader(CreateCommand(database, commandType, query, parameters)))
            {
                T obj = default(T);

                //get maps of all columns returned from the result set
                var maps = GetDataMaps<T>(GetColumnsDictonary(dataReader), keys);

                //get data from datareader
                while (dataReader.Read())
                {
                    if (!IsValueType(typeof(T)))
                        CreateObjectFromDataReader(dataReader, maps, ref obj);
                    else
                        obj = (T)dataReader[0];
                }

                return obj;
            }
        }

        /// <summary>
        /// Create a T object from dataReader
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataReader"></param>
        /// <param name="maps"></param>
        /// <param name="obj"></param>
        private static void CreateObjectFromDataReader<T>(IDataReader dataReader, IEnumerable<DataMap> maps, ref T obj)
        {
            if (obj == null)
                obj = Activator.CreateInstance<T>();

            //go through all maps tree (recursively if necessary)
            foreach (var map in maps)
            {
                var property = map.Property;
                var propertyType = property?.PropertyType;

                //value types
                if (map.MapType == MapType.Value)
                    property.SetValue(obj, ConvertValue(dataReader[map.Column], propertyType));
                //classes
                else if (map.MapType == MapType.Reference)
                {
                    var oProp = Activator.CreateInstance(propertyType);
                    CreateObjectFromDataReader(dataReader, map.Maps, ref oProp);
                    property.SetValue(obj, oProp);
                }
                //lists
                else if (map.MapType == MapType.List || map.MapType == MapType.ValueList)
                {
                    //get list info
                    var listInfo = (dynamic)map.GetListInfoMethod.Invoke(null, new object[] { obj, map, dataReader });

                    //if item value or item keys are null, the item is dismissed
                    if (!listInfo.Item2)
                    {
                        //for lists of value types, listInfo.Item4 already have the value from datareader, so no recursive call is necessary
                        if (map.MapType != MapType.ValueList)
                            CreateObjectFromDataReader(dataReader, map.Maps, ref listInfo.Item4);

                        //if new item, add it to list
                        if (listInfo.Item1)
                            listInfo.Item3.Add(listInfo.Item4);
                    }

                    //attach list to T object
                    property.SetValue(obj, listInfo.Item3);
                }
            }
        }

        /// <summary>
        /// Get list info for the list property been populated
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceObj"></param>
        /// <param name="map"></param>
        /// <param name="dataReader"></param>
        /// <returns></returns>
        private static (bool IsNewItem, bool DismissItem, ICollection<T> List, T Item) GetListInfo<T>(object sourceObj, DataMap map, IDataReader dataReader)
        {
            var isNewItem = true;
            T item = Activator.CreateInstance<T>();

            //get the keys of the list
            var keys = map.Maps.Where(m => m.IsKey);

            //get the list from its parent object
            var list = map.Property.GetValue(sourceObj) as ICollection<T>;

            //if the list has keys, create a hashset oject (due to performance)
            if (keys.Any())
            {
                if (list == null)
                    list = new HashSet<T>(new HashItemEqualityComparer<T>(map.MapType == MapType.ValueList ? null : keys.Select(k => k.Property).ToArray()));

                //get item keys from database
                foreach (var key in keys)
                {
                    var dbValue = dataReader[key.Column];

                    //if a key is null, the item is dismissed and not included in the list
                    if (dbValue == DBNull.Value)
                        return (false, true, list, item);

                    //for value types, the item itself is used as key
                    if (map.MapType == MapType.ValueList)
                        item = (T)ConvertValue(dbValue, typeof(T));
                    else
                        key.Property.SetValue(item, ConvertValue(dbValue, key.Property.PropertyType));
                }

                //check if the new item exists in the list
                if (((HashSet<T>)list).Contains(item))
                {
                    //get the existing item
                    isNewItem = false;
                    item = (((HashSet<T>)list).Comparer as HashItemEqualityComparer<T>).HashItem;
                }
            }
            //if the list has no keys, create a list object
            else
            {
                if (list == null)
                    list = new List<T>();

                //for value types, get the item value from database
                if (map.MapType == MapType.ValueList)
                {
                    var dbValue = dataReader[map.Maps.First().Column];

                    //if the item is null, it is dismissed and not included in the list
                    if (dbValue == DBNull.Value)
                        return (false, true, list, item);

                    item = (T)ConvertValue(dbValue, typeof(T));
                }
            }

            return (isNewItem, false, list, item);
        }


        /// <summary>
        /// Converts source obj to destinationType
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="destinationType"></param>
        /// <returns></returns>
        private static object ConvertValue(object obj, Type destinationType)
        {
            //if the obj is DbNull, return default value of destinationType
            if (obj == DBNull.Value)
                return destinationType.IsValueType ? Activator.CreateInstance(destinationType) : null;

            //convert value
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

        /// <summary>
        /// Get the data maps between the columns from result set and the properties of T object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="columns">Dictionary of the columns of the result set. The dictiorary key is the full column name. The dictionary value (string array) is the column name splited</param>
        /// <param name="keys">The columns from result set that are used as lists keys</param>
        /// <returns></returns>
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

                //create the maps
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

                    //remove first item in the array value of coulmns dictionary. 
                    //each position in the array is a step into the properties tree of the classes been populated
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
                        GetListInfoMethod = ((Func<object, DataMap, IDataReader, (bool, bool, ICollection<int>, int)>)GetListInfo<int>).Method.GetGenericMethodDefinition().MakeGenericMethod(propType),
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

        /// <summary>
        /// Get the columns names dictionary from the datareader
        /// </summary>
        /// <param name="dataReader"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Check if a type is a value type. Strings have being used as value types
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsValueType(Type type)
        {
            return type.IsValueType || type == typeof(string);
        }

        /// <summary>
        /// Check if a type is a list
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsListType(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
        }

        /// <summary>
        /// Create DbCommand to be executed into database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static DbCommand CreateCommand(Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters)
        {
            var command = database.GetSqlStringCommand(query);
            command.CommandType = commandType;

            if (parameters != null)
                foreach (var parameter in parameters)
                    database.AddParameter(command, parameter.Name, parameter.DbType, parameter.Direction, null, parameter.SourceVersion, parameter.Value);

            return command;
        }


        private static T CreateEmptyList<T>()
        {
            var arguments = typeof(T).GetGenericArguments();
            var listType = typeof(List<>).MakeGenericType(arguments);
            if (typeof(T).IsAssignableFrom(listType))
                return (T)Activator.CreateInstance(listType);
            else
                return (T)Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(arguments));
        }
    }

    /// <summary>
    /// Helper container class 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ContainerClass<T>
    {
        public T Content { get; set; }
    }
}
