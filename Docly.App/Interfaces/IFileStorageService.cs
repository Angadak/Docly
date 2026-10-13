using System;
using System.Collections.Generic;
using System.Text;

namespace Docly.App.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(Stream fileStream, string storageKey, CancellationToken cancellationToken = default);
        Task<Stream> GetFileAsync(string storageKey, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default);
    }
}
