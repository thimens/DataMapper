﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Thimens.DataMapper
{
    internal class HashItemEqualityComparer<T> : IEqualityComparer<T>
    {
        private readonly IEnumerable<PropertyInfo> _keyProperties;
        public T HashItem { get; private set; }

        internal HashItemEqualityComparer(params PropertyInfo[] propertiesToCompare)
        {
            _keyProperties = propertiesToCompare;
            HashItem = default(T);
        }

        public bool Equals(T x, T y)
        {
            if (_keyProperties != null)
            {
                foreach (var prop in _keyProperties)
                    if (!prop.GetValue(x).Equals(prop.GetValue(y)))
                        return false;
            }
            else
            {
                if (!x.Equals(y))
                    return false;
            }

            HashItem = x;
            return true;
        }

        public int GetHashCode(T obj)
        {
            int hash = 27;

            if (_keyProperties != null)
                foreach (var prop in _keyProperties)
                    hash = (13 * hash) + prop.GetValue(obj).GetHashCode();
            else
                hash = (13 * hash) + obj.GetHashCode();

            return hash;
        }
    }
}
