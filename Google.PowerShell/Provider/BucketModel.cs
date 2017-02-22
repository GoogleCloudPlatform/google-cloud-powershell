// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// This class maintains a local description of the objects in a bucket. It is used by the
    /// GoogleCloudStorageProvider to prevent redundant service calls e.g. to discover if an object
    /// exists. It keeps track of real objects, which we treat as files, and of name prefixes,
    /// which act like folders. A real object named "myFolder/" will be both a prefix "myFolder" and an object
    /// "myFolder/".
    /// </summary>
    public class BucketModel
    {
        /// <summary>
        /// Map of known object names to objects.
        /// </summary>
        private Dictionary<string, Object> _objectMap = new Dictionary<string, Object>();
        /// <summary>
        /// Map of prefixes (folders), and whether they have children.
        /// </summary>
        private Dictionary<string, bool> _prefixes = new Dictionary<string, bool>();
        /// <summary>
        /// The name of the bucket this is a model of.
        /// </summary>
        private string _bucket;
        /// <summary>
        /// The storage service used to connect to Google Cloud Storage.
        /// </summary>
        private StorageService _service;
        /// <summary>
        /// Set to true if the bucket has more objects than could retrieved in a single request.
        /// </summary>
        private bool _pageLimited = false;

        /// <summary>
        /// The string the provider uses as a folder separator.
        /// </summary>
        private const string SeparatorString = "/";
        /// <summary>
        /// The character the provider uses as a folder separator
        /// </summary>
        private const char Separator = '/';

        /// <summary>
        /// Initializes the bucket model.
        /// </summary>
        /// <param name="bucket">The name of the bucket this models.</param>
        /// <param name="service">The storage service used to maintain the model.</param>
        public BucketModel(string bucket, StorageService service)
        {
            _bucket = bucket;
            _service = service;
            PopulateModel();
        }

        /// <summary>
        /// Gets a clean set of data from the service. If the bucket has more than a single page of objects,
        /// the model will only take the first page.
        /// </summary>
        public void PopulateModel()
        {
            ObjectsResource.ListRequest request = _service.Objects.List(_bucket);
            request.Projection = ObjectsResource.ListRequest.ProjectionEnum.Full;
            Objects objects = request.Execute();
            _pageLimited = objects.NextPageToken != null;
            foreach (Object gcsObject in objects.Items ?? Enumerable.Empty<Object>())
            {
                _objectMap[gcsObject.Name] = gcsObject;
                // Find the prefixes (parent folders) of this object.
                string prefix = gcsObject.Name.TrimEnd(Separator);
                int lastSeparator = gcsObject.Name.LastIndexOf(Separator);
                // If the object name is testing/blah.txt, then the first prefix "testing" has children
                // since '/' is not at the end. Otherwise, we don't know yet.
                bool children = 0 < lastSeparator && lastSeparator < prefix.Length - 1;
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
                    lastSeparator = prefix.LastIndexOf(Separator);
                }
            }
        }

        /// <summary>
        /// Checks if an object exists. If the model did not read all of the objects during its last update, it
        /// may make a service call if the object is not found in its data.
        /// </summary>
        public bool ObjectExists(string objectName)
        {
            if (_pageLimited)
            {
                if (_objectMap.ContainsKey(objectName))
                {
                    return _objectMap[objectName] != null;
                }
                else if (_prefixes.ContainsKey(objectName.TrimEnd('/')))
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
                    catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        _objectMap[objectName] = null;
                        return false;
                    }
                }
            }
            return _objectMap.ContainsKey(objectName) || _prefixes.ContainsKey(objectName.TrimEnd('/'));
        }

        /// <summary>
        /// Checks to see if the given object is a folder.
        /// </summary>
        /// <param name="objectName">The name of the object to check.</param>
        /// <returns>True if the object name is an existant object that ends with "/", or is a prefix for other
        /// existant objects.</returns>
        public bool IsContainer(string objectName)
        {
            if (_prefixes.ContainsKey(objectName.TrimEnd(Separator)))
            {
                return true;
            }
            else if (_pageLimited)
            {
                ObjectsResource.ListRequest request = _service.Objects.List(_bucket);
                request.Prefix = objectName;
                request.Delimiter = SeparatorString;
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
        public Object GetGcsObject(string objectName)
        {
            if (_objectMap.ContainsKey(objectName))
            {
                return _objectMap[objectName];
            }
            else if (_prefixes.ContainsKey(objectName))
            {
                if (_objectMap.ContainsKey(objectName + Separator))
                {
                    return _objectMap[objectName + Separator];
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
        /// Adds or updates a Google Cloud Storage object to the model.
        /// </summary>
        /// <param name="gcsObject">The Google Cloud Storage object to add or update.</param>
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
