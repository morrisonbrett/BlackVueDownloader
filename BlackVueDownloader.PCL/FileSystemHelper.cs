using System.IO;

namespace BlackVueDownloader.PCL
{
    public class FileSystemHelper : IFileSystemHelper
    {
        public virtual void Copy(string sourceFilename, string destFilename)
        {
            File.Copy(sourceFilename, destFilename);
        }

        public virtual void Delete(string filename)
        {
            File.Delete(filename);
        }

        public virtual bool Exists(string filename)
        {
            return File.Exists(filename);
        }
    }
}
