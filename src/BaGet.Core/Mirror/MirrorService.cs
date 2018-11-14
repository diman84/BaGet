using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core.Entities;
using BaGet.Core.Services;
using BaGet.Protocol;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace BaGet.Core.Mirror
{
    public class MirrorService : IMirrorService
    {
        private readonly IPackageService _localPackages;
        private readonly IPackageMetadataService _upstreamFeed;
        private readonly IPackageDownloader _downloader;
        private readonly IIndexingService _indexer;
        private readonly ILogger<MirrorService> _logger;

        public MirrorService(
            IPackageService localPackages,
            IPackageMetadataService upstreamFeed,
            IPackageDownloader downloader,
            IIndexingService indexer,
            ILogger<MirrorService> logger)
        {
            _localPackages = localPackages ?? throw new ArgumentNullException(nameof(localPackages));
            _upstreamFeed = upstreamFeed ?? throw new ArgumentNullException(nameof(upstreamFeed));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<Package>> FindUpstreamPackagesOrNullAsync(string id, CancellationToken cancellationToken)
        {
            Uri ParseUri(string uriString)
            {
                if (uriString == null) return null;

                if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                {
                    return null;
                }

                return uri;
            }

            // TODO: This should merge results with the local packages.
            var upstreamPackages = await _upstreamFeed.GetAllMetadataOrNullAsync(id, cancellationToken);

            var result = upstreamPackages.Select(entry => new Package
            {
                Id = entry.PackageId,
                Version = entry.Version,
                Authors = new[] { entry.Authors }, // TODO
                Description = entry.Description,
                Downloads = entry.Downloads,
                HasReadme = entry.HasReadme,
                Language = entry.Language,
                Listed = entry.Listed,
                MinClientVersion = entry.MinClientVersion,
                Published = entry.Published,
                RequireLicenseAcceptance = entry.RequireLicenseAcceptance,
                Summary = entry.Summary,
                Title = entry.Title,
                IconUrl = ParseUri(entry.IconUrl),
                LicenseUrl = ParseUri(entry.LicenseUrl),
                ProjectUrl = ParseUri(entry.ProjectUrl),
                RepositoryUrl = ParseUri(entry.RepositoryUrl),
                RepositoryType = entry.RepositoryType,
                Tags = entry.Tags.ToArray(),

                Dependencies = FindDependencies(entry)
            });

            return result.ToList();
        }

        public async Task MirrorAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            if (await _localPackages.ExistsAsync(id, version))
            {
                return;
            }

            _logger.LogInformation(
                "Package {PackageId} {PackageVersion} does not exist locally. Indexing from upstream feed...",
                id,
                version);

            await IndexFromSourceAsync(id, version, cancellationToken);

            _logger.LogInformation(
                "Finished indexing {PackageId} {PackageVersion} from the upstream feed",
                id,
                version);
        }

        private List<PackageDependency> FindDependencies(CatalogEntry entry)
        {
            if ((entry.DependencyGroups?.Count ?? 0) == 0)
            {
                return new List<PackageDependency>();
            }

            return entry.DependencyGroups
                .SelectMany(FindDependenciesFromDependencyGroup)
                .ToList();
        }

        private IEnumerable<PackageDependency> FindDependenciesFromDependencyGroup(DependencyGroupItem group)
        {
            if ((group.Dependencies?.Count ?? 0) == 0)
            {
                return new[]
                {
                    new PackageDependency
                    {
                        Id = null,
                        VersionRange = null,
                        TargetFramework = group.TargetFramework
                    }
                };
            }

            return group.Dependencies.Select(d => new PackageDependency
            {
                Id = d.Id,
                VersionRange = d.Range,
                TargetFramework = group.TargetFramework
            });
        }

        private async Task IndexFromSourceAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Attempting to mirror package {PackageId} {PackageVersion}...",
                id,
                version);

            try
            {
                var packageUri = await _upstreamFeed.GetPackageContentUriAsync(id, version);

                using (var stream = await _downloader.DownloadOrNullAsync(packageUri, cancellationToken))
                {
                    if (stream == null)
                    {
                        _logger.LogWarning(
                            "Failed to download package {PackageId} {PackageVersion}",
                            id,
                            version);

                        return;
                    }

                    _logger.LogInformation(
                        "Downloaded package {PackageId} {PackageVersion}, indexing...",
                        id,
                        version);

                    var result = await _indexer.IndexAsync(stream, cancellationToken);

                    _logger.LogInformation(
                        "Finished indexing package {PackageId} {PackageVersion} with result {Result}",
                        packageUri,
                        result);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Failed to mirror package {PackageId} {PackageVersion}",
                    id,
                    version);
            }
        }
    }
}
