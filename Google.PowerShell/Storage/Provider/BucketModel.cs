using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace Google.PowerShell.CloudStorage
{
    public class BucketModel
    {
        private Dictionary<string, Object> _objectMap = new Dictionary<string, Object>();
        private HashSet<string> _prefixes = new HashSet<string>();
        private DateTimeOffset _lastSync = DateTimeOffset.Now;
        private string _bucket;
        private StorageService _service;
        private static TimeSpan staleTime = TimeSpan.FromMinutes(1);
        private bool pageLimited = false;

        public BucketModel(string bucket, StorageService service)
        {
            _bucket = bucket;
            _service = service;
            UpdateModel();
        }

        public void UpdateIfStale()
        {
            if (DateTimeOffset.Now - _lastSync > staleTime)
            {
                UpdateModel();
                _lastSync = DateTimeOffset.Now;
            }
        }

        public void UpdateModel()
        {
            _objectMap.Clear();
            _prefixes.Clear();
            ObjectsResource.ListRequest request = _service.Objects.List(_bucket);
            request.Projection = ObjectsResource.ListRequest.ProjectionEnum.Full;
            Objects objects = request.Execute();
            pageLimited = objects.NextPageToken != null;
            foreach (Object gcsObject in objects.Items ?? Enumerable.Empty<Object>())
            {
                _objectMap[gcsObject.Name] = gcsObject;
                string[] objectPrefixes = gcsObject.Name.Split('/');
                string prefix = objectPrefixes.First();
                foreach (string objectPrefix in objectPrefixes.Skip(1))
                {
                    prefix += "/";
                    _prefixes.Add(prefix);
                    prefix += objectPrefix;
                }
            }
        }

        public bool ObjectExists(string objectName)
        {
            if (pageLimited)
            {

                if (_objectMap.ContainsKey(objectName))
                {
                    return _objectMap[objectName] != null;
                }
                else if (_prefixes.Contains(objectName))
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
                   _prefixes.Contains(objectName);
        }

        public bool IsContainer(string objectName)
        {
            return _prefixes.Contains(objectName) ||
                   (_objectMap.ContainsKey(objectName) && objectName.EndsWith("/"));
        }

        public bool HasChildren(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return _objectMap.Count > 0;
            }
            else
            {
                return _prefixes.Contains(objectName);
            }
        }

        public Object GetGcsObject(string objectPath)
        {
            if (_objectMap.ContainsKey(objectPath))
            {
                return _objectMap[objectPath];
            }
            else if (_prefixes.Contains(objectPath))
            {
                return new Object { Bucket = _bucket, Name = objectPath, ContentType = "Folder" };
            }
            else
            {
                ObjectsResource.GetRequest request = _service.Objects.Get(_bucket, objectPath);
                request.Projection = ObjectsResource.GetRequest.ProjectionEnum.Full;
                Object gcsObject = request.Execute();
                _objectMap[gcsObject.Name] = gcsObject;
                return gcsObject;
            }
        }

        public void AddObject(Object gcsObject)
        {
            _objectMap[gcsObject.Name] = gcsObject;
        }
    }
}