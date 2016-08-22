using System.Collections;
using System.IO;
using System.Management.Automation.Provider;

namespace Google.PowerShell.CloudStorage
{
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
            _writer.Close();
        }
    }
}