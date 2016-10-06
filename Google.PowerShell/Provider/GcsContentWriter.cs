// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections;
using System.IO;
using System.Management.Automation.Provider;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// Required by GoogleCloudStorageProvider.GetContentWriter, which is used by the cmdlet Set-Content.
    /// </summary>
    public class GcsContentWriter : IContentWriter
    {
        private StreamWriter _writer;

        public GcsContentWriter(Stream stream)
        {
            _writer = new StreamWriter(stream);
        }

        public void Dispose()
        {
            _writer.Dispose();
        }

        public IList Write(IList content)
        {
            foreach (var item in content)
            {
                _writer.WriteLine(item);
            }
            return null;
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            _writer.BaseStream.Seek(offset, origin);
        }

        public void Close()
        {
#if !CORECLR
            _writer.Close();
#else
            // StreamWriter on .NET Core does not have Close method so we
            // have to call Dispose() instead.
            _writer.Dispose();
#endif
        }
    }
}