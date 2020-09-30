using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Cli;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{
    [Display("get", Group: "package",
        Description:
        "Download one or more packages without resolving dependencies and maintaining compatibility with current installation.")]
    public class PackageGetAction : ICliAction
    {
        [CommandLineArgument("dependencies", Description = "Also Download dependencies.", ShortName = "y")]
        public bool Dependencies { get; set; }

        [CommandLineArgument("repository", Description = LockingPackageAction.CommandLineArgumentRepositoryDescription,
            ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("version", Description = LockingPackageAction.CommandLineArgumentVersionDescription)]
        public string[] Version { get; set; }

        [CommandLineArgument("os", Description = LockingPackageAction.CommandLineArgumentOsDescription)]
        public string OS { get; set; }

        [CommandLineArgument("architecture",
            Description = LockingPackageAction.CommandLineArgumentArchitectureDescription)]
        public CpuArchitecture Architecture { get; set; }

        [UnnamedCommandLineArgument("Packages", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("dry-run",
            Description = "Initiates the command and checks for errors, but does not download any packages.")]
        public bool DryRun { get; set; } = false;


        [CommandLineArgument("out",
            Description =
                "Location to put the package file. If --dependencies is specified this has to be a folder. If a single folder is specified, all downloads are placed in that folder.")]
        public string[] Out { get; set; }

        /// <summary>
        /// The location to apply the command to. The default is the location of OpenTap.PackageManager.exe
        /// </summary>
        [CommandLineArgument("target",
            Description =
                "The location where the command is applied. The default is the directory of the application itself.\nThis setting only applies when --compatible is specified.",
            ShortName = "t")]
        public string Target { get; set; }

        /// <summary>
        /// This is used when specifying multiple packages with different version numbers. In that case <see cref="Packages"/> can be left null.
        /// </summary>
        public PackageSpecifier[] PackageReferences { get; set; }

        /// <summary> Specifies that the downloaded package should be compatible with the currently used TAP installation. </summary>
        [CommandLineArgument("compatible",
            Description =
                "Specifies that the downloaded package should be compatible with the currently used TAP installation.")]
        public bool Compatible { get; set; }

        /// <summary>
        /// PackageDef of downloaded packages. Value is null until packages have actually been downloaded (after Execute)
        /// </summary>
        public IEnumerable<PackageDef> DownloadedPackages { get; private set; } = null;

        TraceSource log = Log.CreateSource("Get");

        public PackageGetAction()
        {
            Architecture = ArchitectureHelper.GuessBaseArchitecture;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    OS = "OSX";
                    break;
                case PlatformID.Unix:
                    OS = "Linux";
                    break;
                default:
                    OS = "Windows";
                    break;
            }
        }

        public int Execute(CancellationToken cancellationToken)
        {
            string targetDir = Path.GetDirectoryName(typeof(TestPlan).Assembly.Location);
            if (Target != null)
                targetDir = Target;
            if (File.Exists(Path.Combine(targetDir, "OpenTap.dll")) == false)
            {
                log.Error("Target directory must contain an OpenTAP installation.");
                return -1;
            }

            List<IPackageRepository> repositories = new List<IPackageRepository>
            {
                PackageRepositoryHelpers.DetermineRepositoryType(PackageCacheHelper.PackageCacheDirectory)
            };

            if (Repository == null)
                repositories.AddRange(PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled)
                    .Select(s => s.Manager).ToList());
            else
                repositories.AddRange(Repository.Select(s => PackageRepositoryHelpers.DetermineRepositoryType(s)));

            IPackageIdentifier[] id = Array.Empty<IPackageIdentifier>();
            if (Compatible)
            {
                id = new Installation(targetDir)
                    .GetPackages()
                    .Select(x => (IPackageIdentifier) x).ToArray();
            }


            VersionSpecifier[] specifiers = new VersionSpecifier[Packages.Length];
            if (PackageReferences == null)
            {
                PackageReferences = new PackageSpecifier[Packages.Length];
                for (int i = 0; i < Packages.Length; i++)
                {
                    var versionString = Version?.Length > i ? Version[i] : "release";
                    var ver = VersionSpecifier.Parse(versionString);
                    specifiers[i] = ver;
                    PackageReferences[i] = new PackageSpecifier(Packages[i], ver, Architecture, OS);
                }
            }

            id = id.Where(x => PackageReferences.Any(y => x.Name == y.Name)).ToArray();

            if (Dependencies)
            {
                List<PackageDef> toDownload = new List<PackageDef>();

                for (int i = 0; i < PackageReferences.Length; i++)
                {
                    var specifier = specifiers[i];
                    var spec = PackageReferences[i];
                    PackageDef packageToDownload = null;

                    foreach (var repo in repositories)
                    {
                        var pkgs = repo.GetPackageVersions(spec.Name, id).ToArray();
                        // If Version -> filter specific version
                        if (specifier != null && specifier.PreRelease != null)
                        {
                            pkgs = pkgs.Where(x => specifier.IsCompatible(x.Version)).ToArray();
                        }

                        if (pkgs.Any())
                        {
                            var pkg = pkgs.FindMax(p => p.Version);
                            var packages = repo.GetPackages(new PackageSpecifier(pkg), id).ToArray();
                            var package = packages.FirstOrDefault();
                            if (packageToDownload == null || packageToDownload.Version.CompareTo(package.Version) < 0)
                            {
                                packageToDownload = package;
                            }
                        }
                    }

                    if (packageToDownload != null)
                        toDownload.Add(packageToDownload);
                }

                var resolver = new DependencyResolver(toDownload, repositories);
                toDownload = resolver.Dependencies;
                if (resolver.UnknownDependencies.Any())
                {
                    log.Error("Cannot resolve all dependencies.");
                    log.Info("Missing dependencies: ");
                    foreach (var item in resolver.UnknownDependencies)
                    {
                        log.Info("   {0}", item);
                    }

                    return -1;
                }

                string destination = Out?.FirstOrDefault() ?? targetDir;
                if (Directory.Exists(destination) == false)
                {
                    log.Error("Folder does not exist '{0}'", destination);
                    return -1;
                }

                PackageActionHelpers.DownloadPackages(destination, toDownload);
                DownloadedPackages = toDownload;
            }
            else
            {
                DownloadedPackages = DoDownload(repositories, id, PackageReferences);
            }


            //if (downloadedPackages.Count < Packages.Length)
            //    return -1;

            return 0;
        }

        IEnumerable<PackageDef> DoDownload(List<IPackageRepository> repositories, IPackageIdentifier[] id,
            PackageSpecifier[] packageReferences)
        {
            List<PackageDef> downloadedPackages = new List<PackageDef>();
            string outDir = Out?.Length == 1 && Directory.Exists(Out[0]) ? Out[0] : null;
            for (int i = 0; i < packageReferences.Length; i++)
            {
                var spec = packageReferences[i];
                bool downloaded = false;
                var candidates = GetCandidates(repositories, spec, id);
                var maxVersion = candidates.Max(p => p.Version);
                candidates = candidates.Where(p => p.Version == maxVersion).ToArray();

                foreach (var repo in repositories)
                {
                    var repoUrl = PackageActionHelpers.NormalizeRepoUrl(repo.Url);
                    if (repoUrl.StartsWith("HTTP"))
                    {
                        var secondSlash = repoUrl.IndexOf('/', repoUrl.IndexOf('/') + 1);
                        repoUrl = repoUrl.Substring(secondSlash + 1);
                    }

                    var pkg = candidates.FirstOrDefault(p =>
                        PackageActionHelpers
                            .NormalizeRepoUrl((p.PackageSource as IRepositoryPackageDefSource)?.RepositoryUrl)
                            .EndsWith(repoUrl));

                    if (pkg is PackageDef package)
                    {
                        downloadedPackages.Add(package);
                        var name = PackageActionHelpers.GetDefaultPackageFileName(package);

                        var source = "remote repository";
                        if (repo is FilePackageRepository)
                        {
                            source = PackageCacheHelper.PackageIsFromCache(package)
                                ? "package cache"
                                : "file repository";
                        }

                        log.Info($"Found file {name} in {source} {repo.Url}");
                        string targetFile = Path.Combine(outDir ?? Directory.GetCurrentDirectory(), name);
                        if (outDir == null && Out != null && i < Out.Length)
                        {
                            var o = Out[i];
                            targetFile = Directory.Exists(o) ? Path.Combine(o, name) : o;
                        }

                        if (DryRun == false)
                        {
                            var sw = Stopwatch.StartNew();
                            repo.DownloadPackage(package, targetFile);
                            if (PackageCacheHelper.PackageIsFromCache(package) == false)
                                PackageCacheHelper.CachePackage(targetFile);

                            var verb = repo is HttpPackageRepository ? "Downloaded" : "Copied";

                            log.Info(sw, $"{verb} {package} to {targetFile}");
                        }

                        downloaded = true;
                        break;
                    }
                }

                if (!downloaded)
                {
                    // Debugger.Launch();

                    var msg = new StringBuilder();
                    msg.Append($"Unable to download package {spec.Name} {spec.Version}.");

                    var anySpec = new PackageSpecifier(spec.Name, VersionSpecifier.Any);
                    var available = GetCandidates(repositories, anySpec, id);
                    if (available.Any())
                    {
                        var availableVersion = available.Max(p => p.Version);
                        msg.Append($" Latest available version: '{availableVersion}'.");
                    }

                    log.Error(msg.ToString());
                }
            }

            return downloadedPackages;
        }

        private PackageDef[] GetCandidates(List<IPackageRepository> repositories, PackageSpecifier spec,
            IPackageIdentifier[] id)
        {
            var timeout = TimeSpan.FromSeconds(30);

            var result = new List<PackageDef>();
            var sw = Stopwatch.StartNew();
            var tasks = new List<(IPackageRepository repo, Task<PackageDef[]> task)>();

            foreach (var repository in repositories)
            {
                var repo = repository;
                tasks.Add((repo, Task.Run(() =>
                {
                    try
                    {
                        return repo.GetPackages(spec, id);
                    }
                    catch (Exception e)
                    {
                        log.Error($"Unexpected error when contacting repository {repo.Url}");
                        log.Debug(e);
                        RemoveRepo(repo, repositories);
                        return new PackageDef[] { };
                    }
                })));
            }

            while (sw.Elapsed < timeout && tasks.Any(t => t.task.IsCompleted == false))
            {
                var i = Task.WaitAny(tasks.Select(t => t.task).ToArray(), 500);
                if (i < 0) continue;
                var (repo, task) = tasks[i];
                var taskResult = task.Result;
                if (taskResult.Any())
                {
                    result.AddRange(taskResult);
                    if (spec.Version.ToString() != "^-release" &&
                        spec.Version != VersionSpecifier.Any &&
                        repo is FilePackageRepository &&
                        result.Any(r => spec.Version.IsCompatible(r.Version)))
                    {
                        return taskResult;
                    }

                    result.AddRange(taskResult);
                }

                tasks.RemoveAt(i);

                if (sw.Elapsed > TimeSpan.FromMilliseconds(500))
                    foreach (var rt in tasks.Where(t => t.task.IsCompleted == false))
                        log.Info(sw, $"Waiting for repo {rt.repo.Url} to respond...");
            }


            foreach (var rt in tasks.Where(t => t.task.IsCompleted == false))
            {
                log.Debug($"Repo {rt.repo.Url} did not respond within {(int) timeout.TotalMilliseconds} ms.");
                RemoveRepo(rt.repo, repositories);
            }

            return result.ToArray();
        }

        private void RemoveRepo(IPackageRepository repo, List<IPackageRepository> repos)
        {
            if (repos.Contains(repo))
            {
                log.Warning($"Disabling repository {repo.Url} for the rest of this action.");
                repos.Remove(repo);
            }
        }        
    }
}