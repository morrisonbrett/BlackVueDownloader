namespace BlackVueDownloader.PCL
{
    public interface IFileSystemHelper
    {
        void Copy(string sourceFilename, string destFilename);

        void Move(string sourceFilename, string destFilename);

        void Delete(string filename);

        bool Exists(string filename);

        bool DirectoryExists(string directory);

        void CreateDirectory(string directory);
    }
}
