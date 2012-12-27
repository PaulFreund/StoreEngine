//###################################################################################################
/*
    Copyright (c) since 2012 - Paul Freund 
    
    Permission is hereby granted, free of charge, to any person
    obtaining a copy of this software and associated documentation
    files (the "Software"), to deal in the Software without
    restriction, including without limitation the rights to use,
    copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the
    Software is furnished to do so, subject to the following
    conditions:
    
    The above copyright notice and this permission notice shall be
    included in all copies or substantial portions of the Software.
    
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
    OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
    HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
    WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
    OTHER DEALINGS IN THE SOFTWARE.
*/
//###################################################################################################

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.ComponentModel;

namespace StoreEngine
{
    #region Object

    public enum PersistType
    {
        Cache,
        NoCache
    }

    [AttributeUsage(AttributeTargets.Struct)]
    public class Persist : Attribute
    {
        public readonly PersistType Type = PersistType.Cache;

        public Persist() { }
        public Persist(PersistType type) { Type = type; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PersistValue : Attribute
    {
        internal object _initValue = null;

        public PersistValue() { }
        public PersistValue(object initValue) { _initValue = initValue; }
    }

    public class ObjectContainer
    {
        public bool Cached = false;
        public ValueType Data = null;

        public ObjectContainer(bool cached, ValueType data)
        {
            Cached = cached;
            Data = data;
        }
    }

    #endregion

    #region Notifications

    public enum StoreUpdateType
    {
        None,
        Create,
        Update,
        Delete
    }

    public class StoreUpdateEventArgs : EventArgs
    {
        public StoreUpdateEventArgs(string address, StoreUpdateType type)
        {
            Address = address;
            Type = type;
        }

        string Address = string.Empty;
        StoreUpdateType Type = StoreUpdateType.None;
    }

    public delegate void StoreUpdate(object sender, StoreUpdateEventArgs e);

    #endregion

    #region Addressable base

    public class IStoreAddressable<T>
    {
        protected const char _addressSeparator = '.';
        protected Dictionary<string, T> _objects = new Dictionary<string, T>();

        protected string GetRemoteAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Store error: Empty address");

            var segments = address.Split(_addressSeparator);
            if (segments.Count() <= 1)
                throw new Exception("Store error: Invalid address");

            var localAddress = segments.FirstOrDefault();
            var providerAddress = address.Substring(localAddress.Length + 1); // +1 for separator
            if (providerAddress.Length <= 0)
                throw new Exception("Store error: Invalid address");

            return providerAddress;
        }

        protected string GetLocalAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Store error: Empty address");

            var segments = address.Split(_addressSeparator);
            if (segments.Count() <= 0)
                throw new Exception("Store error: Invalid address");

            var localAddress = segments.FirstOrDefault();
            if (localAddress.Length <= 0)
                throw new Exception("Store error: Invalid address");

            return localAddress;
        }

        protected bool ContainsAddress(string address)
        {
            return _objects.ContainsKey(address);
        }

        protected T GetObject(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Store error: Empty address");

            var localAddress = GetLocalAddress(address);

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Store error: Could not resolve store provider");

            if (!_objects.ContainsKey(localAddress))
                throw new Exception("Store error: Could not resolve store provider");

            return _objects[localAddress];
        }
    }

    #endregion

    #region Provider

    public abstract class ITypedStoreProvider<T> : IStoreProvider where T : struct
    {
        public ITypedStoreProvider(string containerPath) : base(containerPath) { }

        internal override ValueType CreateObject()
        {
            Type type = typeof(T);
            return (ValueType)Activator.CreateInstance(type);
        }
    }

    public abstract class IStoreProvider : IStoreAddressable<ObjectContainer>
    {
        protected string _containerPath;

        public IStoreProvider(string containerPath)
        {
            if (string.IsNullOrEmpty(containerPath))
                throw new Exception("Store provider error: Empty location");

            _containerPath = containerPath;

            ContainerLoad();
        }

        ~IStoreProvider()
        {
            ContainerUnload();
        }

        #region Container

        internal abstract void ContainerLoad();
        internal abstract void ContainerUnload();

        internal abstract void ContainerCreate(string localAddress);
        internal abstract void ContainerRead(string localAddress);
        internal abstract void ContainerUpdate(string localAddress);
        internal abstract void ContainerDelete(string localAddress);

        #endregion

        #region Interface

        public ObjectContainer Create(string localAddress)
        {
            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty address");

            if (ContainsAddress(localAddress))
                throw new Exception("Address already taken");

            // Add it to the objects
            _objects.Add(localAddress, new ObjectContainer(false, CreateObject()));

            // Initialize it with default values
            SetDefaultValues(localAddress);

            // Initialize the container
            ContainerCreate(localAddress);

            // Write the values to the store, so init values are stored
            ContainerUpdate(localAddress);

            // Return it
            return _objects[localAddress];
        }

        public Dictionary<string, ValueType> ReadAll()
        {
            var objectList = new Dictionary<string, ValueType>();

            foreach (var obj in _objects)
                objectList.Add(obj.Key, Read(obj.Key).Data);

            return objectList;
        }

        public ObjectContainer Read(string localAddress)
        {
            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty address");

            if (!ContainsAddress(localAddress))
                throw new Exception("Address not registered");

            if (_objects[localAddress].Data == null)
                throw new Exception("Empty store object");

            if (!_objects[localAddress].Cached)
            {
                ContainerRead(localAddress);
                _objects[localAddress].Cached = true;
            }

            return _objects[localAddress];
        }

        public void Update(string localAddress, ObjectContainer data)
        {
            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty address");

            if (!ContainsAddress(localAddress))
                throw new Exception("Address not registered");

            if (_objects[localAddress].Data == null)
                throw new Exception("Empty store object");

            if (data.Data.GetType() != _objects[localAddress].Data.GetType())
                throw new Exception("Wrong object type");

            var equals = ObjectEquals(localAddress, data);
            _objects[localAddress] = data;

            if( !equals )
            {
                ContainerUpdate(localAddress);
                _objects[localAddress].Cached = true;
            }
        }

        public void Delete(string localAddress)
        {
            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty address");

            if (!ContainsAddress(localAddress))
                throw new Exception("Address not registered");

            ContainerDelete(localAddress);
            _objects.Remove(localAddress);
        }

        public bool Contains(string localAddress)
        {
            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty address");

            return ContainsAddress(localAddress);
        }

        #endregion

        #region Objects

        internal abstract ValueType CreateObject();

        internal void SetDefaultValues(string localAddress)
        {
            Type type = _objects[localAddress].Data.GetType();
            foreach (var property in type.GetRuntimeProperties())
            {
                var persistValue = property.GetCustomAttribute(typeof(PersistValue), true) as PersistValue;
                if (persistValue != null && persistValue._initValue != null)
                {
                    if (property.PropertyType == persistValue._initValue.GetType())
                        property.SetValue(_objects[localAddress].Data, persistValue._initValue);
                }
            }
        }

        internal bool ObjectEquals(string localAddress, ObjectContainer second)
        {
            if (_objects[localAddress] == null || second == null || second.Data == null)
                return false;

            bool equals = true;

            Type type = _objects[localAddress].Data.GetType();
            foreach (var property in type.GetRuntimeProperties())
            {
                if (property.GetCustomAttribute(typeof(PersistValue), true) != null)
                {
                    if (!property.GetValue(_objects[localAddress].Data).Equals(property.GetValue(second.Data)))
                        equals = false;
                }
            }

            return equals;
        }

        internal PersistType GetCacheMode(string localAddress)
        {
            Type type = _objects[localAddress].Data.GetType();
            var persist = type.GetTypeInfo().GetCustomAttribute(typeof(Persist), true) as Persist;
            return persist.Type;
        }

        internal List<string> GetPersistantNames(string localAddress)
        {
            var names = new List<string>();

            Type type = _objects[localAddress].Data.GetType();
            foreach (var property in type.GetRuntimeProperties())
            {
                var persistValue = property.GetCustomAttribute(typeof(PersistValue), true) as PersistValue;
                if (persistValue != null)
                    names.Add(property.Name);
            }

            return names;
        }

        internal string GetPersistantValue(string localAddress, string name)
        {
            string value = string.Empty;

            var type = _objects[localAddress].Data.GetType();
            var property = type.GetRuntimeProperty(name);
            if (property != null)
            {
                if (property.PropertyType == typeof(string))
                {
                    value = property.GetValue(_objects[localAddress].Data) as string;
                }
                else if (property.PropertyType == typeof(bool))
                {
                    bool boolVal = (bool)property.GetValue(_objects[localAddress].Data);
                    value = Convert.ToString(boolVal);
                }
                else if (property.PropertyType == typeof(int))
                {
                    int intVal = (int)property.GetValue(_objects[localAddress].Data);
                    value = Convert.ToString(intVal);
                }
                else if (property.PropertyType.GetTypeInfo().IsEnum)
                {
                    int enumVal = (int)property.GetValue(_objects[localAddress].Data);
                    value = Convert.ToString(enumVal);
                }
            }

            if (value == null)
                value = string.Empty;

            return value;
        }

        internal void SetPersistantValue(string localAddress, string name, string value)
        {
            var type = _objects[localAddress].Data.GetType();
            var property = type.GetRuntimeProperty(name);
            if (property != null)
            {
                if (property.PropertyType == typeof(string))
                {
                    property.SetValue(_objects[localAddress].Data, value);
                }
                else if (property.PropertyType == typeof(bool))
                {
                    property.SetValue(_objects[localAddress].Data, Convert.ToBoolean(value));
                }
                else if (property.PropertyType == typeof(int))
                {
                    property.SetValue(_objects[localAddress].Data, Convert.ToInt32(value));
                }
                else if (property.PropertyType.GetTypeInfo().IsEnum)
                {
                    property.SetValue(_objects[localAddress].Data, Convert.ToInt32(value));
                }
            }
        }

        #endregion
    }

    #endregion
}
