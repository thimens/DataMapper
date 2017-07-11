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

        private static bool GetObjectFromDataReader<T>(IDataReader dataReader, IEnumerable<DataMap> maps, ref T obj)
        {
            if (obj == null)
                obj = Activator.CreateInstance<T>();

            foreach(var map in maps)
            {
                var property = map.Property;

                if (map.MapType == MapType.Value)
                    property.SetValue(obj, ConvertValue(dataReader[map.Column], property.PropertyType));
                else if (map.MapType == MapType.Reference)
                {
                    var oProp = Activator.CreateInstance(property.PropertyType);
                    GetObjectFromDataReader(dataReader, map.Maps, ref oProp);
                    property.SetValue(obj, oProp);
                }
                else if (map.MapType == MapType.List)
                {
                    if (property.PropertyType.IsInterface && !property.PropertyType.IsAssignableFrom(typeof(List<>).MakeGenericType(property.PropertyType.GetGenericArguments())))
                        throw new ApplicationException($"Could not resolve property {property.Name} of type {property.PropertyType.Name}. The property must be instantiable or assingnabe from a List<T>");

                    dynamic dynList = property.GetValue(obj) ?? (property.PropertyType.IsArray ? Array.CreateInstance(property.PropertyType.GetElementType(), 0) : property.PropertyType.IsInterface ? Activator.CreateInstance(typeof(List<>).MakeGenericType(property.PropertyType.GetGenericArguments())) : Activator.CreateInstance(property.PropertyType));
                    var list = Enumerable.ToList(dynList);

                    Func<IDataReader, IEnumerable<string>, string[], string, IList<string>, string> metodo = GetObjectFromDataReader<string>;
                    var newObject = metodo.Method.GetGenericMethodDefinition().MakeGenericMethod(property.PropertyType.IsArray ? property.PropertyType.GetElementType() : property.PropertyType.GetGenericArguments().First()).Invoke(null, new object[] { dataReader, columnNames, keys, columnNamePrefix + propertyName + ".", list });

                    property.SetValue(obj, ConvertObjectList(list, dynList), null);
                }
            }

            return obj;
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
