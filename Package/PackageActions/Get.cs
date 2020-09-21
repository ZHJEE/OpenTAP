using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
      [Display("get", Group: "package", Description: "Downloads one or more packages.")]
    public class PackageGetAction : ICliAction
    {
        [CommandLineArgument("dependencies", Description = "Also Download dependencies.", ShortName = "y")]
        public bool Dependencies { get; set; }

        [CommandLineArgument("repository", Description = LockingPackageAction.CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("version", Description = LockingPackageAction.CommandLineArgumentVersionDescription)]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = LockingPackageAction.CommandLineArgumentOsDescription)]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = LockingPackageAction.CommandLineArgumentArchitectureDescription)]
        public CpuArchitecture Architecture { get; set; }

        [UnnamedCommandLineArgument("Packages", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("dry-run", Description = "Initiates the command and checks for errors, but does not download any packages.")]
        public bool DryRun { get; set; } = false;
        
        
        [CommandLineArgument("out", Description = "Location to put the package file. If --dependencies is specified this has to be a folder.")]
        public string[] Out { get; set; }
        
        /// <summary>
        /// The location to apply the command to. The default is the location of OpenTap.PackageManager.exe
        /// </summary>
        [CommandLineArgument("target", Description = "The location where the command is applied. The default is the directory of the application itself.\nThis setting only applies when --compatible is specified.", ShortName = "t")]
        public string Target { get; set; }
        
        /// <summary>
        /// This is used when specifying multiple packages with different version numbers. In that case <see cref="Packages"/> can be left null.
        /// </summary>
        public PackageSpecifier[] PackageReferences { get; set; }
        
        /// <summary> Specifies that the downloaded package should be compatible with the currently used TAP installation. </summary>
        [CommandLineArgument("compatible", Description = "Specifies that the downloaded package should be compatible with the currently used TAP installation.")]
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

        PackageSpecifier getPackageSpecifier(string name)
        {
            if(!VersionSpecifier.TryParse(Version, out var ver))
                ver = VersionSpecifier.Parse("release");
            return new PackageSpecifier(name, ver, Architecture, OS);
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

            List<IPackageRepository> repositories = new List<IPackageRepository>();

            if (Repository == null)
                repositories.AddRange(PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager).ToList());
            else
                repositories.AddRange(Repository.Select(s => PackageRepositoryHelpers.DetermineRepositoryType(s)));

            IPackageIdentifier[] id = Array.Empty<IPackageIdentifier>();
            if (Compatible)
            {
                id = new Installation(targetDir)
                    .GetPackages()
                    .Select(x =>(IPackageIdentifier)x).ToArray();
            }

            

            if(PackageReferences == null)
                PackageReferences = Packages.Select(getPackageSpecifier).ToArray();
            id = id.Where(x => PackageReferences.Any(y => x.Name == y.Name)).ToArray();

            VersionSpecifier specifier = null;
            if (Version != null)
                specifier = VersionSpecifier.Parse(Version).With(matchBehavior: VersionMatchBehavior.MatchPrerelease);

            List<PackageDef> toDownload = new List<PackageDef>();

            for (int i = 0; i < PackageReferences.Length; i++)
            {
                var spec = PackageReferences[i];
                PackageDef packageToDownload = null;

                foreach (var repo in repositories)
                {
                    var pkg = repo.GetPackageVersions(spec.Name, id)
                        .OrderByDescending(x => x.Version).ToArray();
                    if (specifier != null && specifier.PreRelease != null)
                    {
                        pkg = pkg.Where(x => specifier.IsCompatible(x.Version)).ToArray();
                    }
                    if (pkg.Any())
                    {
                        var packages = repo.GetPackages(new PackageSpecifier(pkg.FirstOrDefault()), id).ToArray();
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

            List<PackageDef> downloadedPackages = new List<PackageDef>();
            if (Dependencies)
            {
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
                downloadedPackages = toDownload;
            }
            else
            {
                
                for(int i = 0; i < PackageReferences.Length; i++)
                {
                    var spec = PackageReferences[i];
                    bool downloaded = false;
                    foreach (var repo in repositories)
                    {
                        if (repo.GetPackageVersions(spec.Name, id).Any())
                        {
                            var array = repo.GetPackages(spec, id).ToArray();
                            if (array.FirstOrDefault() is PackageDef package)
                            {
                                downloadedPackages.Add(package);
                                var name = PackageActionHelpers.GetDefaultPackageFileName(package);
                                log.Info("Found remote file {0}", name);
                                string targetFile = Path.Combine(Directory.GetCurrentDirectory(), name);
                                if (Out != null && i < Out.Length)
                                {
                                    var o = Out[i];
                                    if (o.EndsWith("/"))
                                    {
                                        // if the Out is a folder.
                                        targetFile = Path.Combine(o, name);
                                    }
                                    else
                                    {
                                        targetFile = o;
                                    }
                                }

                                if (DryRun == false)
                                {
                                    var sw = Stopwatch.StartNew();
                                    repo.DownloadPackage(array.First(), targetFile);
                                    log.Info(sw, "Downloaded {0}", targetFile);
                                }
                                downloaded = true;
                            }
                            break;
                        }
                    }

                    if (!downloaded)
                    {
                        var name = PackageActionHelpers.GetDefaultPackageFileName(spec);
                        log.Error("Unable to download package {0}", name);   
                    }
                }
                
                
            }

            DownloadedPackages = downloadedPackages;
            
            //if (downloadedPackages.Count < Packages.Length)
            //    return -1;
            
            return 0;
        }
    }
}