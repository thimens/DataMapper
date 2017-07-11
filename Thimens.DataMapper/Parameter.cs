using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Thimens.DataMapper
{
    /// <summary>
    /// A parameter struct that is used with the query parameter on List and Get methods
    /// </summary>
    public struct Parameter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="dbType"></param>
        /// <param name="value"></param>
        public Parameter(string name, DbType dbType, object value)
            : this()
        {
            this.Name = name;
            this.DbType = dbType;
            this.Direction = ParameterDirection.Input;
            this.SourceVersion = DataRowVersion.Default;
            this.Value = value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="dbType"></param>
        /// <param name="direction"></param>
        /// <param name="value"></param>
        public Parameter(string name, DbType dbType, ParameterDirection direction, object value)
            : this()
        {
            this.Name = name;
            this.DbType = dbType;
            this.Direction = direction;
            this.SourceVersion = DataRowVersion.Default;
            this.Value = value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="dbType"></param>
        /// <param name="direction"></param>
        /// <param name="sourceVersion"></param>
        /// <param name="value"></param>
        public Parameter(string name, DbType dbType, ParameterDirection direction, DataRowVersion sourceVersion, object value)
            : this()
        {
            this.Name = name;
            this.DbType = dbType;
            this.Direction = direction;
            this.SourceVersion = sourceVersion;
            this.Value = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public DbType DbType { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public ParameterDirection Direction { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public DataRowVersion SourceVersion { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public object Value { get; private set; }
    }
}
