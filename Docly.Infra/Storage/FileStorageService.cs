using Docly.App.Interfaces;
using Docly.Infra.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Docly.Infra.Storage
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _rootPath;

        public FileStorageService(IOptions<FileStorageOptions> options, IHostEnvironment environment)
        {
            // Relative paths resolve against the API's content root, not the process working directory.
            _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.RootPath));
            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string storageKey, CancellationToken cancellationToken = default)
        {
            var fullPath = GetFullPath(storageKey);

            await using var fileOutputStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await fileStream.CopyToAsync(fileOutputStream, cancellationToken);
            return storageKey;
        }

        public Task<Stream> GetFileAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            var fullPath = GetFullPath(storageKey);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File not found for storage key: {storageKey}");

            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            return Task.FromResult(stream);
        }

        public Task DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            var fullPath = GetFullPath(storageKey);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.CompletedTask;
        }

        private string GetFullPath(string storageKey)
        {
            // Storage keys are generated server-side as "<guid>.<ext>"; reject anything else outright.
            if (string.IsNullOrWhiteSpace(storageKey) || storageKey != Path.GetFileName(storageKey))
                throw new UnauthorizedAccessException("Invalid storage key.");

            var combined = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
            if (!combined.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Invalid storage key - path traversal detected.");

            return combined;
        }
    }
}
