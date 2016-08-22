using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Provider;

namespace Google.PowerShell.CloudStorage
{
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
            _stream.Close();
        }
    }
}