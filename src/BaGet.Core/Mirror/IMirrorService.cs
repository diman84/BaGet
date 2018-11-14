using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core.Entities;
using NuGet.Versioning;

namespace BaGet.Core.Mirror
{
    /// <summary>
    /// Indexes packages from an external source.
    /// </summary>
    public interface IMirrorService
    {
        /// <summary>
        /// Attempt to find a package's metadata from an upstream source.
        /// </summary>
        /// <param name="id">The package's id to lookup</param>
        /// <param name="cancellationToken">The token to cancel the lookup</param>
        /// <returns>
        /// The package's metadata on the upstream source, or null if the package cannot be found
        /// </returns>
        Task<IReadOnlyList<Package>> FindUpstreamPackagesOrNullAsync(string id, CancellationToken cancellationToken);

        /// <summary>
        /// If the package is unknown, attempt to index it from an upstream source.
        /// </summary>
        /// <param name="id">The package's id</param>
        /// <param name="version">The package's version</param>
        /// <param name="cancellationToken">The token to cancel the mirroring</param>
        /// <returns>A task that completes when the package has been mirrored.</returns>
        Task MirrorAsync(string id, NuGetVersion version, CancellationToken cancellationToken);
    }
}
