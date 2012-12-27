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
using Windows.Storage;

namespace StoreEngine
{
    public class IStoreProviderLocalStorage<T> : ITypedStoreProvider<T> where T : struct
    {
        private ApplicationDataContainer _container = null;

        public IStoreProviderLocalStorage(string containerPath) : base(containerPath) { }

        internal override void ContainerLoad()
        {
            if (string.IsNullOrEmpty(_containerPath))
                throw new Exception("Store provider error: Empty location");

            _container = ApplicationData.Current.LocalSettings.CreateContainer(_containerPath, ApplicationDataCreateDisposition.Always);
            if (_container == null)
                throw new Exception("Store provider error: Could not load LocalStore");

            foreach (var child in _container.Containers)
            {
                _objects.Add(child.Key, new ObjectContainer(false, CreateObject()));

                if (GetCacheMode(child.Key) == PersistType.Cache)
                {
                    ContainerRead(child.Key);
                    _objects[child.Key].Cached = true;
                }
            }
        }

        internal override void ContainerUnload()
        {
            // Nothing to do here
        }

        internal override void ContainerCreate(string localAddress)
        {
            if (_container == null)
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");

            _container.CreateContainer(localAddress, ApplicationDataCreateDisposition.Always);

            foreach (var name in GetPersistantValueNames(localAddress))
                _container.Containers[localAddress].Values.Add(name, string.Empty);
        }

        internal override void ContainerRead(string localAddress)
        {
            if (_container == null)
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");

            if(!_container.Containers.ContainsKey(localAddress))
                throw new Exception("Mising object in container");

            if (!_objects.ContainsKey(localAddress))
                throw new Exception("No object for address");

            var containerObject = _container.Containers[localAddress];

            foreach (var name in GetPersistantValueNames(localAddress))
                SetPersistantValue(localAddress, name, containerObject.Values[name] as string);
        }

        internal override void ContainerUpdate(string localAddress)
        {
            if (_container == null)
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");

            if (!_container.Containers.ContainsKey(localAddress))
                throw new Exception("Mising object in container");

            if (!_objects.ContainsKey(localAddress))
                throw new Exception("No object for address");

            var containerObject = _container.Containers[localAddress];

            foreach (var name in GetPersistantValueNames(localAddress))
                containerObject.Values[name] = GetPersistantValue(localAddress, name);
        }

        internal override void ContainerDelete(string localAddress)
        {
            if( _container == null )
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");

            _container.DeleteContainer(localAddress);
        }
    }
}
