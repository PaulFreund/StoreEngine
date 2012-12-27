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
using System.IO;
using System.Xml.Linq;

namespace StoreEngine
{
    public class IStoreProviderFile<T> : ITypedStoreProvider<T> where T : struct
    {
        private XName _xmlStoreObjects = XName.Get("StoreObjects");
        private XName _xmlStoreObject = XName.Get("StoreObject");

        private XName _xmlStoreObjectName = XName.Get("name");

        private XDocument _container = null;

        public IStoreProviderFile(string containerPath) : base(containerPath) { }

        internal override void ContainerLoad()
        {
            if (string.IsNullOrEmpty(_containerPath))
                throw new Exception("Store provider error: Empty location");

            ModifyFile((stream) =>
            {
                if (stream.Length <= 0)
                    _container = new XDocument(new XElement(_xmlStoreObjects));
                else
                    _container = XDocument.Load(stream);
            });

            if (_container.Root == null)
                throw new Exception("File load failed");

            foreach (var element in _container.Root.Elements(_xmlStoreObject))
            {
                var name = element.Attribute(_xmlStoreObjectName).Value;
                if (string.IsNullOrEmpty(name))
                    continue;

                _objects.Add(name, new ObjectContainer(false, CreateObject()));

                if (GetCacheMode(name) == PersistType.Cache)
                {
                    ContainerRead(name);
                    _objects[name].Cached = true;
                }

            }
        }

        internal override void ContainerUnload()
        {
            SaveFile();
        }

        internal override void ContainerCreate(string localAddress)
        {
            if (_container == null)
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");


            XElement newEle = new XElement(_xmlStoreObject);
            newEle.Add(new XAttribute(_xmlStoreObjectName, localAddress));

            foreach (var name in GetPersistantNames(localAddress))
                newEle.Add(new XElement(XName.Get(name)));

            _container.Root.Add(newEle);
            SaveFile();
        }

        internal override void ContainerRead(string localAddress)
        {
            if (_container == null)
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");

            var element = GetXMLObject(localAddress);
            if (element == null)
                throw new Exception("Could not load object");

            foreach (var name in GetPersistantNames(localAddress))
                SetPersistantValue(localAddress, name, GetXMLObjectValue(element, name));
        }

        internal override void ContainerUpdate(string localAddress)
        {
            if (_container == null)
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");

            var element = GetXMLObject(localAddress);
            if (element == null)
                throw new Exception("Could not load object");

            foreach (var name in GetPersistantNames(localAddress))
                SetXMLObjectValue(element, name, GetPersistantValue(localAddress, name));

            SaveFile();
        }

        internal override void ContainerDelete(string localAddress)
        {
            if (_container == null)
                throw new Exception("Container not initialized");

            if (string.IsNullOrEmpty(localAddress))
                throw new Exception("Empty Address");

            var obj = GetXMLObject(localAddress);
            if (obj == null)
                throw new Exception("Could not delete object");

            obj.Remove();
            SaveFile();
        }

        private void ModifyFile(Action<Stream> action, bool clear = false)
        {
            try
            {
                var createOption = clear ? CreationCollisionOption.ReplaceExisting : CreationCollisionOption.OpenIfExists;

                var fileHandleTask = ApplicationData.Current.LocalFolder.CreateFileAsync(_containerPath, createOption).AsTask();
                fileHandleTask.Wait(10000);
                if (fileHandleTask.IsCompleted && fileHandleTask.Result != null)
                {
                    var fileHandle = fileHandleTask.Result;
                    var streamHandleTask = fileHandle.OpenAsync(FileAccessMode.ReadWrite).AsTask();
                    streamHandleTask.Wait(10000);

                    if (streamHandleTask.IsCompleted && streamHandleTask.Result != null)
                    {
                        var stream = streamHandleTask.Result.AsStream();

                        if (stream == null)
                            throw new Exception("Could not open file");

                        action(stream);

                        stream.Flush();
                        stream.Dispose();
                    }
                }
            }
            catch
            {
                throw new Exception("Could not load File");
            }
        }

        private void SaveFile()
        {
            if (_container == null || _container.Root == null)
                throw new Exception("Invalid data");

            ModifyFile((stream) => _container.Save(stream), true);
        }

        private XElement GetXMLObject(string name)
        {
            var elements = _container.Root.Elements(_xmlStoreObject).Where((ele) =>
            {
                return ele.Attribute(_xmlStoreObjectName).Value == name;
            });

            if (elements.Count() <= 0)
                return null;
            else
                return elements.First();
        }

        private string GetXMLObjectValue(string name, string key)
        {
            var element = GetXMLObject(name);
            if (element == null)
                throw new Exception("Could not load object");

            return GetXMLObjectValue(element, key);
        }

        private string GetXMLObjectValue(XElement obj, string key)
        {
            var keyElement = obj.Element(XName.Get(key));
            if (keyElement == null)
                throw new Exception("Value not present in object");

            return keyElement.Value;
        }

        private void SetXMLObjectValue(string name, string key, string value)
        {
            var element = GetXMLObject(name);
            if (element == null)
                throw new Exception("Could not load object");

            SetXMLObjectValue(element, key, value);
        }

        private void SetXMLObjectValue(XElement obj, string key, string value)
        {
            var keyElement = obj.Element(XName.Get(key));
            if (keyElement == null)
                throw new Exception("Value not present in object");

            keyElement.Value = value;
        }
    }
}
