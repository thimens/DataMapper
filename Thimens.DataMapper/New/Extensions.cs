using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;


namespace ClassLibrary1
{
    public class Extensions
    {
        public T Get<T>(IDataReader dataReader, params string[] keys)
        {
            T obj = default(T);
            /*
            columns = new string[] { "id", "name", "orders.id", "orders.deliverytime", "orders.products.id", "orders.products.name", "orders.products.value" };
            */
            var maps = GetDataMap<T>(GetColumnsDictonary(dataReader), keys);

            while (dataReader.Read())
            {
                if (IsValueType(typeof(T)))
                    obj = GetObjectFromDataReader<T>(dataReader, maps);
                else
                    obj = (T)dataReader[0];
            }

            return obj;
        }

        public T GetObjectFromDataReader<T>(IDataReader dataReader, IEnumerable<DataMap> maps)
        {
            T obj = Activator.CreateInstance<T>();

            //foreach (var column in columns)
            //{
            //    if (dataReader[column] != DBNull.Value)
            //    {


            //    }
            //}

            return obj;
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
    }
}
