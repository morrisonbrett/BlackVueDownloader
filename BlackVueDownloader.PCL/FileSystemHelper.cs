using System.IO;

namespace BlackVueDownloader.PCL
{
    public class FileSystemHelper : IFileSystemHelper
    {
        public virtual void Copy(string source, string dest)
        {
            File.Copy(source, dest);
        }

        public virtual void Delete(string fn)
        {
            File.Delete(fn);
        }

        public virtual bool Exists(string fn)
        {
            return File.Exists(fn);
        }
    }
}