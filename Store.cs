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

namespace StoreEngine
{
    public class Store : IStoreAddressable<IStoreProvider>
    {
        #region Provider

        public void RegisterProvider(string address, IStoreProvider provider)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            if (_objects.ContainsKey(address) || _objects.ContainsValue(provider))
                throw new Exception("Provider already registered");

            _objects.Add(address, provider);
        }

        public void UnregisterProvider(string address)
        {
            if( string.IsNullOrEmpty(address) )
                throw new Exception("Empty address");

            if (!_objects.ContainsKey(address))
                throw new Exception("Provider not registered");

            _objects.Remove(address);
        }

        #endregion

        #region Interface

        public T Create<T>(string address, bool silent = false) where T : struct
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            var provider = GetObject(address);
            if (provider == null)
                throw new Exception("Could not resolve store provider");

            var providerAddress = GetRemoteAddress(address);
            if (string.IsNullOrEmpty(providerAddress))
                throw new Exception("Invalid Address");

            var result = provider.Create(providerAddress);

            if( !silent )
                NotifySubscribers(address, StoreUpdateType.Create);

            return (T)result.Data;
        }

        public T Read<T>(string address) where T : struct
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            var provider = GetObject(address);
            if( provider == null )
                throw new Exception("Could not resolve store provider");

            var providerAddress = GetRemoteAddress(address);
            if( string.IsNullOrEmpty(providerAddress) )
                throw new Exception("Invalid Address");

            return (T)provider.Read(providerAddress).Data;
        }

        public Dictionary<string, T> ReadAll<T>(string address) where T : struct
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            var provider = GetObject(address);
            if( provider == null )
                throw new Exception("Could not resolve store provider");

            return provider.ReadAll() as Dictionary<string, T>;
        }

        public void Update(string address, ValueType data, bool silent = false)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            var provider = GetObject(address);
            if (provider == null)
                throw new Exception("Could not resolve store provider");

            var providerAddress = GetRemoteAddress(address);
            if (string.IsNullOrEmpty(providerAddress))
                throw new Exception("Invalid Address");

            provider.Update(providerAddress, new ObjectContainer(true, data));

            if (!silent)
                NotifySubscribers(address, StoreUpdateType.Update);
        }

        public void Delete(string address, bool silent = false)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            var provider = GetObject(address);
            if (provider == null)
                throw new Exception("Could not resolve store provider");

            var providerAddress = GetRemoteAddress(address);
            if (string.IsNullOrEmpty(providerAddress))
                throw new Exception("Invalid Address");

            provider.Delete(providerAddress);

            if (!silent)
                NotifySubscribers(address, StoreUpdateType.Delete);
        }

        public bool Contains(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            var localAddress = GetLocalAddress(address);

            if (string.IsNullOrEmpty(localAddress) || !_objects.ContainsKey(localAddress))
                throw new Exception("Could not resolve store provider");

            var providerAddress = GetRemoteAddress(address);

            if (string.IsNullOrEmpty(providerAddress))
                throw new Exception("Invalid Address");

            return _objects[localAddress].Contains(providerAddress);
        }

        #endregion

        #region Notifications

        private Dictionary<string, List<StoreUpdate>> _subscriptions = new Dictionary<string, List<StoreUpdate>>();

        public void Subscribe(string address, StoreUpdate receiver)
        {
            if( string.IsNullOrEmpty(address) )
                throw new Exception("Empty address");

            if (receiver == null)
                throw new Exception("Subscription receiver empty");

            if (!_subscriptions.ContainsKey(address))
                _subscriptions.Add(address, new List<StoreUpdate>());

            if( _subscriptions[address].Contains(receiver) )
                throw new Exception("Subscription receiver already registered");

            _subscriptions[address].Add(receiver);
        }

        public void UnSubscribe(string address, StoreUpdate receiver)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Empty address");

            if (!_subscriptions.ContainsKey(address))
                return; // We don't want to throw an error on that one

            if (!_subscriptions[address].Contains(receiver))
                throw new Exception("Subscription receiver is not registered");

            _subscriptions[address].Remove(receiver);
        }

        private void NotifySubscribers(string address, StoreUpdateType type)
        {
            if (!_subscriptions.ContainsKey(address))
                return;

            var subscribers = _subscriptions[address];
            foreach (var subscriber in subscribers)
                subscriber(this, new StoreUpdateEventArgs(address, type));
        }

        #endregion
    }
}
