using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Collections;
using System.Reflection;
using System.Data.Common;

namespace Thimens.DataMapper.Old
{
    /// <summary>
    /// Extends the Database class
    /// </summary>
    public static class DatabasebExtensions
    {
        /// <summary>
        /// Returns a list of objects from the query result.
        /// Object properties and fields returned from the query must have same name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters">Parameters for the query</param>
        /// <param name="keys">Object properties used as unique keys to fill the main or  nested lists. Inform as keys as necessary.</param>
        /// <returns></returns>
        public static List<T> List<T>(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters, params string[] keys)
        {
            var list = new List<T>();

            using (IDataReader reader = database.ExecuteReader(CreateCommand(database, commandType, query, parameters)))
            {
                T obj = default(T);
                var columnNames = GetColumnNames(reader);

                while (reader.Read())
                {
                    //reference type 
                    if (default(T) == null && typeof(T) != typeof(string))
                        obj = GetObjectFromDataReader<T>(reader, columnNames, keys, null, list);
                    //value type
                    else
                        list.Add((T)reader[0]);
                }

                return list;
            }
        }

        /// <summary>
        /// Returns a object from the query result. If more than one result from query, only the first one is returned.
        /// Object properties and fields returned from the query must have same name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="database"></param>
        /// <param name="commandType"></param>
        /// <param name="query"></param>
        /// <param name="parameters">Parameters for the query</param>
        /// <param name="keys">Object properties used as unique keys to fill nested list. A new key will insert a new object in the list</param>
        /// <returns></returns>
        public static T Get<T>(this Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters, params string[] keys)
        {
            using (IDataReader reader = database.ExecuteReader(CreateCommand(database, commandType, query, parameters)))
            {
                T obj = default(T);
                var columnNames = GetColumnNames(reader);

                while (reader.Read())
                {
                    //reference type 
                    if (default(T) == null && typeof(T) != typeof(string))
                        obj = GetObjectFromDataReader<T>(reader, columnNames, keys, null, obj);
                    //value type
                    else
                        obj = (T)reader[0];
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

        private static DbCommand CreateCommand(Database database, CommandType commandType, string query, IEnumerable<Parameter> parameters)
        {
            var command = database.GetSqlStringCommand(query);
            command.CommandType = commandType;

            if (parameters != null)
                foreach (var parameter in parameters)
                    database.AddParameter(command, parameter.Name, parameter.DbType, parameter.Direction, null, parameter.SourceVersion, parameter.Value);

            return command;
        }

        private static T GetObjectFromDataReader<T>(IDataReader dataReader, IEnumerable<string> columnNames, string[] keys, string columnNamePrefix, IList<T> listBeingFilled)
        {
            T obj = Activator.CreateInstance<T>();
            var isNewObj = false;

            if (listBeingFilled == null)
            {
                isNewObj = true;
                listBeingFilled = Activator.CreateInstance<IList<T>>();
            }
            else
                isNewObj = !ValidateKeyProperties<T>( dataReader, keys, columnNamePrefix, listBeingFilled, out obj);

            CreateObjectWithDataReader<T>(dataReader, columnNames, keys, columnNamePrefix, ref obj);

            if (isNewObj)
                listBeingFilled.Add(obj);

            return obj;
        }

        private static T GetObjectFromDataReader<T>(IDataReader dataReader, IEnumerable<string> columnNames, string[] keys, string columnNamePrefix, T objectBeingFilled)
        {
            T obj = Activator.CreateInstance<T>();
            var isNewObj = false;

            if (Comparer.Equals(objectBeingFilled, default(T)))
                isNewObj = true;
            else
                isNewObj = !ValidateKeyProperties<T>(dataReader, keys, columnNamePrefix, objectBeingFilled, out obj);

            CreateObjectWithDataReader(dataReader, columnNames, keys, columnNamePrefix, ref obj);

            return obj;
        }

        private delegate bool ValidateKeyPropertiesFunc<T>(IDataReader dataReader, string[] keys, string columnNamePrefix, object objectBeingFilled, out T objectToFill);

        /// <summary>
        /// Returns true if exist any object on the list that attends one of the keys and returns that object in the out parameter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataReader"></param>
        /// <param name="keys"></param>
        /// <param name="columnNamePrefix">Datareader column name prefix that being filled at the time</param>
        /// <param name="objectBeingFilled">List or object of type T being filled by datareader at the time</param>
        /// <param name="objectToFill">Object that matchs the keys</param>
        /// <returns></returns>
        private static bool ValidateKeyProperties<T>(IDataReader dataReader, string[] keys, string columnNamePrefix, object objectBeingFilled, out T objectToFill)
        {
            IList<T> list = null;
            T obj = default(T);
            bool isNewObj = false;
            IEnumerable<T> subList = null;
            Dictionary<string, PropertyInfo> dictProps = new Dictionary<string, PropertyInfo>();

            objectToFill = Activator.CreateInstance<T>();

            if ((objectBeingFilled as IList) != null)
                list = (objectBeingFilled as IList).Cast<T>().ToList();

            if (list == null)
                if (objectBeingFilled is T)
                    obj = (T)objectBeingFilled;
                else
                    throw new ArgumentException(string.Format("Objeto passado como parêmetro não é do tipo {0} nem lista de {0}", typeof(T).Name));

            //check if keys exists
            //only keys at the same level at the hierarchy of nested objects and have the same prefix will be valid
            //
            var actualKeys = keys.Where(f => f.Count(c => c == '.') == (columnNamePrefix == null ? 0 : columnNamePrefix.Count(c => c == '.')) && (columnNamePrefix == null ? true : f.ToLower().IndexOf(columnNamePrefix.ToLower()) >= 0));

            if (actualKeys.Count() == 0)
                isNewObj = true;
            else
            {
                //value types don´t have properties. therefore, if one value type are the key, its value will be used in the validation
                if (list != null && (typeof(T).IsValueType || typeof(T) == typeof(string)))
                {
                    subList = list.Where(o => o.Equals(GetValueFromDataReader(dataReader, (columnNamePrefix ?? ""), typeof(T))));
                    if (subList.Count() == 0)
                        isNewObj = true;
                    else
                        objectToFill = subList.LastOrDefault();
                }
                else
                {
                    //remove the prefix from the keys
                    actualKeys = actualKeys.Select(s => columnNamePrefix != null && columnNamePrefix != string.Empty ? s.ToLower().Replace(columnNamePrefix.ToLower(), "") : s);

                    //properties being used as keys
                    actualKeys.ToList().ForEach(key =>
                    {
                        //if the key contains the @ sign, the key is a nested property of another property, so a recursive call is made. 
                        var keyAux = key.IndexOf("@") >= 0 ? key.Substring(0, key.IndexOf("@")).ToLower() : key.ToLower();
                        var ps = typeof(T).GetProperties().LastOrDefault(p => p.Name.ToLower() == keyAux);
                        if (ps != null)
                            dictProps.Add(key, ps);
                        else
                            throw new ArgumentException("Uma ou mais chaves informadas são inválidas");
                    });

                    /*props = typeof(T).GetProperties().Where(p => chavesAtuais.Count(c => c.ToLower() == p.Name.ToLower()) > 0).ToList();*/

                    //validate the properties
                    dictProps.ToList().ForEach(d =>
                    {
                        if (!d.Value.PropertyType.IsValueType && d.Value.PropertyType != typeof(string) && d.Key.IndexOf("@") == -1)
                            throw new ArgumentException("Somente propridades do tipo de valor podem ser usadas como 'chave' no preenchimento do objeto");
                    });

                    if (list == null)
                    {
                        dictProps.ToList().ForEach(d =>
                        {
                            if (d.Key.IndexOf("@") >= 0)
                            {
                                var newKey = columnNamePrefix + d.Key.Substring(0, d.Key.IndexOf("@")) + "." + d.Key.Substring(d.Key.IndexOf("@") + 1);
                                var outObj = Activator.CreateInstance(d.Value.PropertyType);

                                ValidateKeyPropertiesFunc<string> metodo = ValidateKeyProperties<string>;
                                isNewObj = isNewObj || !(bool)metodo.Method.GetGenericMethodDefinition().MakeGenericMethod(d.Value.PropertyType).Invoke(null, new object[] { dataReader, new string[] { newKey }, columnNamePrefix + d.Value.Name + ".", d.Value.GetValue(obj, null), outObj });
                            }
                            else if (!d.Value.GetValue(obj, null).Equals(GetValueFromDataReader(dataReader, (columnNamePrefix ?? "") + d.Value.Name, d.Value.PropertyType)))
                                isNewObj = true;
                        });

                        if (!isNewObj)
                            objectToFill = obj;
                    }
                    else
                    {
                        //create a sublist with list items that matched the keys
                        //in the case of multiple keys, in each loop, only the previous selected objects are validated
                        dictProps.ToList().ForEach(d =>
                        {
                            if (d.Key.IndexOf("@") >= 0)
                            {
                                var newKey = columnNamePrefix + d.Key.Substring(0, d.Key.IndexOf("@")) + "." + d.Key.Substring(d.Key.IndexOf("@") + 1);
                                var outObj = Activator.CreateInstance(d.Value.PropertyType);

                                var parameters = new object[] { dataReader, new string[] { newKey }, columnNamePrefix + d.Value.Name + ".", (subList ?? list).Select(o => Convert.ChangeType(d.Value.GetValue(o, null), d.Value.PropertyType)).ToList(), outObj };
                                ValidateKeyPropertiesFunc<string> metodo = ValidateKeyProperties<string>;
                                isNewObj = isNewObj || !(bool)metodo.Method.GetGenericMethodDefinition().MakeGenericMethod(d.Value.PropertyType).Invoke(null, parameters);
                                subList = (subList ?? list).Where(o => d.Value.GetValue(o, null).Equals(parameters[5]));
                            }
                            else
                                subList = (subList ?? list).Where(o => d.Value.GetValue(o, null).Equals(GetValueFromDataReader(dataReader, (columnNamePrefix ?? "") + d.Value.Name, d.Value.PropertyType)));
                        });

                        if (subList.Count() == 0)
                            isNewObj = true;
                        else
                            objectToFill = subList.LastOrDefault();
                    }
                }
            }

            return !isNewObj;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataReader"></param>
        /// <param name="columnNames"></param>
        /// <param name="keys"></param>
        /// <param name="columnNamePrefix"></param>
        /// <param name="objectToFill"></param>
        private static void CreateObjectWithDataReader<T>(IDataReader dataReader, IEnumerable<string> columnNames, string[] keys, string columnNamePrefix, ref T objectToFill)
        {
            var propertyName = string.Empty;
            var propertyNameAux = string.Empty;
            List<string> properties = new List<string>();


            foreach (var columnName in columnNames)
            {                
                //validate if the datareader field is null or have the same prefix informed in parameters
                if ((dataReader[columnName] != DBNull.Value) && (columnNamePrefix == null || columnName.IndexOf(columnNamePrefix) >= 0))
                {
                    //value type
                    if (typeof(T).IsValueType || typeof(T) == typeof(string))
                        objectToFill = (T)GetValueFromDataReader(dataReader, columnName, typeof(T));
                    //reference type
                    else
                    {
                        //get property name
                        var nomeColunaAux = (columnNamePrefix == null || columnNamePrefix == string.Empty) ? columnName : columnName.Replace(columnNamePrefix, "");

                        if (nomeColunaAux.IndexOf(".") >= 0)
                            propertyNameAux = nomeColunaAux.Substring(0, nomeColunaAux.IndexOf("."));
                        else
                            propertyNameAux = nomeColunaAux;

                        //don´t fill the same property again. this can happen on recursive calls 
                        if (!properties.Contains(propertyNameAux))
                        {
                            propertyName = propertyNameAux;
                            properties.Add(propertyName);

                            //get property with the same datareader column name
                            var property = objectToFill.GetType().GetProperties().FirstOrDefault(p => p.Name.ToLower() == propertyName);
                            if (property != null)
                            {
                                //is list
                                if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
                                {
                                    if (property.PropertyType.IsInterface && !property.PropertyType.IsAssignableFrom(typeof(List<>).MakeGenericType(property.PropertyType.GetGenericArguments())))
                                        throw new ApplicationException($"Could not resolve property {property.Name} of type {property.PropertyType.Name}. The property must be instantiable or assingnabe from a List<T>");

                                    dynamic dynList = property.GetValue(objectToFill, null) ?? (property.PropertyType.IsArray ? Array.CreateInstance(property.PropertyType.GetElementType(), 0) : property.PropertyType.IsInterface ? Activator.CreateInstance(typeof(List<>).MakeGenericType(property.PropertyType.GetGenericArguments())) : Activator.CreateInstance(property.PropertyType));
                                    var list = Enumerable.ToList(dynList);

                                    Func<IDataReader, IEnumerable<string>, string[], string, IList<string>, string> metodo = GetObjectFromDataReader<string>;
                                    var newObject = metodo.Method.GetGenericMethodDefinition().MakeGenericMethod(property.PropertyType.IsArray ? property.PropertyType.GetElementType() : property.PropertyType.GetGenericArguments().First()).Invoke(null, new object[] { dataReader, columnNames, keys, columnNamePrefix + propertyName + ".", list });

                                    property.SetValue(objectToFill, ConvertObjectList(list, dynList), null);
                                }
                                //is class or interface
                                else if (!property.PropertyType.IsValueType && property.PropertyType != typeof(string))
                                {
                                    Func<IDataReader, IEnumerable<string>, string[], string, string, string> metodo = GetObjectFromDataReader<string>;
                                    property.SetValue(objectToFill, metodo.Method.GetGenericMethodDefinition().MakeGenericMethod(property.PropertyType).Invoke(null, new object[] { dataReader, columnNames, keys, columnNamePrefix + propertyName + ".", null }), null);
                                }
                                //is value type
                                else
                                    property.SetValue(objectToFill, GetValueFromDataReader(dataReader, columnName, property.PropertyType), null);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns object from datareader
        /// </summary>
        /// <param name="dataReader"></param>
        /// <param name="columnName">Datareader column name</param>
        /// <param name="type">Type of returned object</param>
        /// <returns></returns>
        private static object GetValueFromDataReader(IDataReader dataReader, string columnName, Type type)
        {
            if (type.IsEnum)
            {
                //check if enum property has an DefaultValueAttribute annotation to be validated. 
                foreach (var eValor in Enum.GetValues(type))
                {
                    var attribute = (DefaultValueAttribute)type.GetField(eValor.ToString()).GetCustomAttributes(typeof(DefaultValueAttribute), false).FirstOrDefault();

                    if (attribute != null && attribute.Value.Equals(dataReader[columnName]))
                        return eValor;
                }

                return Enum.Parse(type, dataReader[columnName].ToString(), true);
            }
            else if (dataReader[columnName].GetType() == typeof(string))
                //check if the database field are a string used as boolean ('Y', 'N') and the property is boolean
                if (type == typeof(bool))
                    return dataReader[columnName].ToString().Trim().Replace('"', '\'').ToLower() == "y" ? true : false;
                else
                    return Convert.ChangeType(dataReader[columnName].ToString().Trim().Replace('"', '\''), Nullable.GetUnderlyingType(type) ?? type);
            else
                return Convert.ChangeType(dataReader[columnName], Nullable.GetUnderlyingType(type) ?? type);
        }

        /// <summary>
        /// Convert a list to another type
        /// </summary>
        /// <typeparam name="T">Type of new List</typeparam>
        /// <typeparam name="V">Type of current List</typeparam>
        /// <param name="objectList">Original list</param>
        /// <param name="newList">List which the type T is used to convert the original list</param>
        /// <returns></returns>
        private static List<T> ConvertObjectList<V, T>(IEnumerable<V> objectList, IEnumerable<T> newList)
        {
            return objectList.Cast<T>().ToList();
        }

        /// <summary>
        /// Convert a list to another array type
        /// </summary>
        /// <typeparam name="T">Type of new Array</typeparam>
        /// <typeparam name="V">Type of current List</typeparam>
        /// <param name="objectList">Original list</param>
        /// <param name="newList">>Array which the type T is used to convert the original list</param>
        /// <returns></returns>
        private static T[] ConvertObjectList<V, T>(IEnumerable<V> objectList, T[] newList)
        {
            return objectList.Cast<T>().ToArray();
        }

        /// <summary>
        /// Get column names from dataReader
        /// </summary>
        /// <param name="dataReader"></param>
        /// <returns></returns>
        private static IEnumerable<string> GetColumnNames(IDataReader dataReader)
        {
            var columns = new HashSet<string>();

            try
            {
                foreach (DataRow column in dataReader.GetSchemaTable().Rows)
                    columns.Add(column["ColumnName"].ToString().ToLower());                
            }

            catch (NotSupportedException)
            {
                for(int i = 0; i < dataReader.FieldCount; i++)
                    columns.Add(dataReader.GetName(i).ToLower());
            }

            return columns;
        }
    }
}
