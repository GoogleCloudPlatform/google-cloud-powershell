using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// This class maintains a local description of the objects in a bucket.
    /// </summary>
    public class BucketModel
    {
        private Dictionary<string, Object> _objectMap = new Dictionary<string, Object>();
        private Dictionary<string, bool> _prefixes = new Dictionary<string, bool>();
        private DateTimeOffset _lastSync = DateTimeOffset.UtcNow;
        private string _bucket;
        private StorageService _service;
        private static TimeSpan staleTime = TimeSpan.FromMinutes(1);
        private bool _pageLimited = false;
        private string _separatorString = "/";
        private char _separator = '/';

        /// <summary>
        /// Initializes the bucket model.
        /// </summary>
        /// <param name="bucket">The name of the bucket this models.</param>
        /// <param name="service">The storage service used to maintain the model.</param>
        public BucketModel(string bucket, StorageService service)
        {
            _bucket = bucket;
            _service = service;
            UpdateModel();
        }

        /// <summary>
        /// Updates the model if it hasn't been updated in a minute.
        /// </summary>
        public void UpdateIfStale()
        {
            if (DateTimeOffset.UtcNow - _lastSync > staleTime)
            {
                UpdateModel();
                _lastSync = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// Gets a clean set of data from the service. If the bucket has more than a single page of objects,
        /// the model will only take the first page.
        /// </summary>
        public void UpdateModel()
        {
            _objectMap.Clear();
            _prefixes.Clear();
            ObjectsResource.ListRequest request = _service.Objects.List(_bucket);
            request.Projection = ObjectsResource.ListRequest.ProjectionEnum.Full;
            Objects objects = request.Execute();
            _pageLimited = objects.NextPageToken != null;
            foreach (Object gcsObject in objects.Items ?? Enumerable.Empty<Object>())
            {
                _objectMap[gcsObject.Name] = gcsObject;
                string prefix = gcsObject.Name.TrimEnd(_separator);
                int lastSeparator = gcsObject.Name.LastIndexOf(_separator);
                bool children = false;
                while (lastSeparator > 0)
                {
                    prefix = prefix.Substring(0, lastSeparator);
                    if (_prefixes.ContainsKey(prefix))
                    {
                        _prefixes[prefix] = children || _prefixes[prefix];
                        break;
                    }
                    _prefixes[prefix] = children;
                    children = true;
                    lastSeparator = prefix.LastIndexOf(_separator);
                }
            }
        }

        /// <summary>
        /// Checks if an object exists. If the model did not read all of the object during its last update, it
        /// may make a service call if the object is not found in it's data.
        /// </summary>
        /// <param name="objectName">The name of the object to search for.</param>
        /// <returns></returns>
        public bool ObjectExists(string objectName)
        {
            if (_pageLimited)
            {

                if (_objectMap.ContainsKey(objectName))
                {
                    return _objectMap[objectName] != null;
                }
                else if (_prefixes.ContainsKey(objectName))
                {
                    return true;
                }
                else
                {
                    try
                    {
                        ObjectsResource.GetRequest request = _service.Objects.Get(_bucket, objectName);
                        request.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
                        _objectMap[objectName] = request.Execute();
                        return true;
                    }
                    catch
                    {
                        _objectMap[objectName] = null;
                        return false;
                    }
                }
            }
            return _objectMap.ContainsKey(objectName) ||
                   _prefixes.ContainsKey(objectName);
        }

        /// <summary>
        /// Checks to see if the given object is a folder.
        /// </summary>
        /// <param name="objectName">The name of the object to check.</param>
        /// <returns>True if the object name is an existant object that ends with "/", or is a prefix for other
        /// existant objects.</returns>
        public bool IsContainer(string objectName)
        {
            if (_prefixes.ContainsKey(objectName.TrimEnd(_separator)))
            {
                return true;
            }
            else if (_pageLimited)
            {
                ObjectsResource.ListRequest request = _service.Objects.List(_bucket);
                request.Prefix = objectName;
                request.Delimiter = _separatorString;
                Objects response = request.Execute();
                return response.Prefixes != null && response.Prefixes.Count > 0;
            }
            else
            {
                return false;
            }
        }

        public bool HasChildren(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return _objectMap.Count > 0;
            }
            else if (_pageLimited && !_prefixes.ContainsKey(objectName))
            {
                ObjectsResource.ListRequest request = _service.Objects.List(_bucket);
                request.Prefix = objectName;
                Objects response = request.Execute();
                return response.Items != null && response.Items.Count > 0;
            }
            else
            {
                return _prefixes.ContainsKey(objectName) && _prefixes[objectName];
            }
        }

        /// <summary>
        /// Gets the Google Cloud Storage object of a given object name.
        /// </summary>
        /// <param name="objectName">The name of the object to get.</param>
        /// <returns></returns>
        public Object GetGcsObject(string objectName)
        {
            if (_objectMap.ContainsKey(objectName))
            {
                return _objectMap[objectName];
            }
            else if (_prefixes.ContainsKey(objectName))
            {
                if (_objectMap.ContainsKey(objectName + _separator))
                {
                    return _objectMap[objectName + _separator];
                }
                else
                {
                    return new Object { Bucket = _bucket, Name = objectName, ContentType = "Folder" };
                }
            }
            else
            {
                ObjectsResource.GetRequest request = _service.Objects.Get(_bucket, objectName);
                request.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
                Object gcsObject = request.Execute();
                _objectMap[gcsObject.Name] = gcsObject;
                return gcsObject;
            }
        }

        /// <summary>
        /// Adds a Google Cloud Storage object to the model.
        /// </summary>
        /// <param name="gcsObject">The Google Cloud Storage object to add.</param>
        public void AddObject(Object gcsObject)
        {
            _objectMap[gcsObject.Name] = gcsObject;
        }

        /// <summary>
        /// Checks if the given object is a real object. An object could "exist" but not be "real" if it is a
        /// prefix for another object (a logical folder that is not "real").
        /// </summary>
        /// <param name="objectName">The name of the object to check.</param>
        /// <returns>True if the object actually exists.</returns>
        public bool IsReal(string objectName)
        {
            if (_objectMap.ContainsKey(objectName))
            {
                return true;
            }
            else if (!_pageLimited)
            {
                return false;
            }
            else
            {
                try
                {

                    ObjectsResource.GetRequest request = _service.Objects.Get(_bucket, objectName);
                    request.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
                    Object gcsObject = request.Execute();
                    _objectMap[gcsObject.Name] = gcsObject;
                    return true;
                }
                catch (GoogleApiException e)
                {
                    if (e.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}