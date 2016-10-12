// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Provider;

namespace Google.PowerShell.CloudStorage
{
    /// <summary>
    /// Required by GoogleCloudStorageProvider.GetContentReader, used by Get-Contents.
    /// </summary>
    public class GcsStringReader : IContentReader
    {
        private StreamReader _stream;

        public GcsStringReader(Stream stream)
        {
            _stream = new StreamReader(new BufferedStream(stream));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public IList Read(long readCount)
        {
            List<string> blocks = new List<string>();
            if (readCount <= 0)
            {
                while (!_stream.EndOfStream)
                {
                    blocks.Add(_stream.ReadLine());
                }
            }
            else
            {
                while (!_stream.EndOfStream && readCount > 0)
                {
                    blocks.Add(_stream.ReadLine());
                    readCount--;
                }
            }
            return blocks;
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            _stream.BaseStream.Seek(offset, origin);
        }

        public void Close()
        {
            // StreamReader on .NET Core does not have Close method so we
            // have to call Dispose() instead.
            _stream.Dispose();
        }
    }
}
