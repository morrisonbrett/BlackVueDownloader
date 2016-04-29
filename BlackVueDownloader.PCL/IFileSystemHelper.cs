namespace BlackVueDownloader.PCL
{
    public interface IFileSystemHelper
    {
        void Copy(string source, string dest);

        void Delete(string fn);

        bool Exists(string fn);
    }
}