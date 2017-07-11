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

namespace Thimens.DataMapper.New
{
    public static class Extensions
    {
        public static T Get<T>(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters, params string[] keys)
        {
            using (IDataReader dataReader = database.ExecuteReader(CreateCommand(database, commandType, query, parameters)))
            {
                T obj = default(T);

                var maps = GetDataMap<T>(GetColumnsDictonary(dataReader), keys);

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

        /// <summary>
        /// <para>Executes the <paramref name="query"/> and returns the number of rows affected.</para>
        /// </summary>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static int ExecuteNonQuery(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters)
        {
            return database.ExecuteNonQuery(CreateCommand(database, commandType, query, parameters));
        }

        /// <summary>
        /// Executes the <paramref name="query"/> and returns the first column of the first row in the result set returned by the query. Extra columns or rows are ignored.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object ExecuteScalar(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters)
        {
            return database.ExecuteScalar(CreateCommand(database, commandType, query, parameters));
        }

        private static void GetObjectFromDataReader<T>(IDataReader dataReader, IEnumerable<DataMap> maps, ref T obj)
        {
            if (obj == null)
                obj = Activator.CreateInstance<T>();

            foreach(var map in maps)
            {
                var property = map.Property;
                var propertyType = property.PropertyType;

                if (map.MapType == MapType.Value)
                    property.SetValue(obj, ConvertValue(dataReader[map.Column], propertyType));
                else if (map.MapType == MapType.Reference)
                {
                    var oProp = Activator.CreateInstance(propertyType);
                    GetObjectFromDataReader(dataReader, map.Maps, ref oProp);
                    property.SetValue(obj, oProp);
                }
                else if (map.MapType == MapType.List)
                {
                    var listInfo = GetList(obj, map, dataReader);

                    GetObjectFromDataReader(dataReader, map.Maps, ref listInfo.Item);

                    if (listInfo.IsNewItem)
                        listInfo.List.Add(listInfo.Item);

                    property.SetValue(obj, listInfo.List);
                }
            }
        }

        private static (bool IsNewItem, ICollection<T> List, T Item) GetList<T>(object sourceObj, DataMap listMap, IDataReader dataReader)
        {
            var isNewItem = true;
            T item = Activator.CreateInstance<T>();
            var keys = listMap.Maps.Where(m => m.IsKey);

            var list = listMap.Property.GetValue(sourceObj) as ICollection<T>;

            if (keys.Any())
            {
                if (list == null)
                    list = new HashSet<T>(new HashItemEqualityComparer<T>());

                foreach (var key in keys)
                    key.Property.SetValue(item, ConvertValue(dataReader[key.Column], key.Property.PropertyType));

                if (((HashSet<T>)list).Contains(item))
                {
                    isNewItem = false;
                    item = (((HashSet<T>)list).Comparer as HashItemEqualityComparer<T>).HashItem;
                }
            }
            else
                if (list == null)
                    list = new List<T>();
                        

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


        private static IEnumerable<DataMap> GetDataMap<T>(IDictionary<string, string[]> columns, IEnumerable<string> keys)
        {
            //properties to return
            var maps = new List<DataMap>();
                        
            //properties of T
            var tProps = typeof(T).GetProperties()?.ToDictionary(p => p.Name.ToLower());

            foreach(var columnGroup in columns.GroupBy(c => c.Value[0]))
            {
                if (tProps.TryGetValue(columnGroup.Key, out PropertyInfo property))
                {
                    //value type
                    if (IsValueType(property.PropertyType))
                        maps.Add(new DataMap() {
                            Column = columnGroup.First().Key,
                            Property = property,
                            MapType = MapType.Value,
                            IsKey = keys.Any(k => k.Equals(columnGroup.First().Key, StringComparison.OrdinalIgnoreCase))
                        });
                    else
                    {
                        Type propType;
                        MapType mapType; 

                        //list
                        if (IsListType(property.PropertyType))
                        {
                            mapType = MapType.List;

                            if (!property.PropertyType.IsInterface && !property.PropertyType.IsAssignableFrom(typeof(List<>).MakeGenericType(property.PropertyType.GetGenericArguments())) && !property.PropertyType.IsAssignableFrom(typeof(HashSet<>).MakeGenericType(property.PropertyType.GetGenericArguments())))
                                throw new ApplicationException($"Could not resolve property {property.Name} of type {property.PropertyType.Name}. The property must be instantiable or assingnabe from a List<T> or HashSet<T>");
                            
                            if (property.PropertyType.IsArray)
                                propType = property.PropertyType.GetElementType();
                            else
                                propType = property.PropertyType.GetGenericArguments()[0];
                        }
                        //class or interface
                        else
                        {
                            mapType = MapType.Reference;
                            propType = property.PropertyType;
                        }
                        
                        var columnsDict = new Dictionary<string, string[]>();
                        foreach(var column in columnGroup)
                        {
                            var arr = column.Value.ToList();
                            arr.RemoveAt(0);
                            if (arr.Count > 0)
                                columnsDict[column.Key] = arr.ToArray();
                        }

                        var map = new DataMap()
                        {
                            Property = property,
                            MapType = mapType,
                            Maps = (IEnumerable<DataMap>)((Func<IDictionary<string, string[]>, IEnumerable<string> , IEnumerable<DataMap>>)GetDataMap<string>)
                            .Method
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(propType)
                            .Invoke(null, new object[] { columnsDict, keys })
                        };

                        maps.Add(map);                                       
                    }
                }
            }            

            return maps;
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
}
