// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;

namespace CollectSFData.Common
{
    /// <summary>
    /// Abstracts file system operations for dependency injection and testing.
    /// Enables mocking of file/directory operations without touching the actual file system.
    /// </summary>
    public interface IFileSystemAbstraction
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check</param>
        /// <returns>true if the file exists; otherwise, false</returns>
        bool FileExists(string path);

        /// <summary>
        /// Determines whether the specified directory exists.
        /// </summary>
        /// <param name="path">The directory to check</param>
        /// <returns>true if the directory exists; otherwise, false</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Creates all directories in the specified path unless they already exist.
        /// </summary>
        /// <param name="path">The directory to create</param>
        void CreateDirectory(string path);

        /// <summary>
        /// Gets the files in the specified directory.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <param name="searchPattern">The search pattern (e.g., "*.txt")</param>
        /// <param name="searchOption">Search options for subdirectories</param>
        /// <returns>Array of file paths</returns>
        string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Gets the directories in the specified directory.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>Array of directory paths</returns>
        string[] GetDirectories(string path);

        /// <summary>
        /// Gets the file information for the specified file.
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>FileInfo object or null if file doesn't exist</returns>
        FileInfo GetFileInfo(string path);
    }

    /// <summary>
    /// Default implementation that uses the actual file system.
    /// </summary>
    public class FileSystemAbstraction : IFileSystemAbstraction
    {
        /// <inheritdoc />
        public bool FileExists(string path) => File.Exists(path);

        /// <inheritdoc />
        public bool DirectoryExists(string path) => Directory.Exists(path);

        /// <inheritdoc />
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        /// <inheritdoc />
        public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
            => Directory.GetFiles(path, searchPattern, searchOption);

        /// <inheritdoc />
        public string[] GetDirectories(string path) => Directory.GetDirectories(path);

        /// <inheritdoc />
        public FileInfo GetFileInfo(string path) => File.Exists(path) ? new FileInfo(path) : null;
    }
}