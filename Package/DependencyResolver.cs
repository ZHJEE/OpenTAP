//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// Finds dependencies for specified packages in Package Repositories
    /// </summary>
    public class DependencyResolver
    {
        /// <summary>
        /// List of all the dependencies to the specified packages
        /// </summary>
        public List<PackageDef> Dependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies to the specified packages that are currently not installed
        /// </summary>
        public List<PackageDef> MissingDependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies to the specified packages that could not be found in the package repositories
        /// </summary>
        public List<PackageDependency> UnknownDependencies = new List<PackageDependency>();


        private TraceSource log = Log.CreateSource("DependencyResolver");
        /// <summary>
        /// Instantiates a new dependency resolver.
        /// </summary>
        /// <param name="packages">The packages to resolve dependencies for.</param>
        /// <param name="tapInstallation">The tap installation containing installed packages.</param>
        /// <param name="repositories">The repositories to use for resolving dependencies</param>
        public DependencyResolver(Installation tapInstallation, IEnumerable<PackageDef> packages, List<IPackageRepository> repositories)
        {
            InstalledPackages = new Dictionary<string, PackageDef>();
            foreach (var pkg in tapInstallation.GetPackages())
                InstalledPackages[pkg.Name] = pkg;

            resolve(repositories, packages);
        }

        /// <summary>
        /// Instantiates a new dependency resolver.
        /// </summary>
        /// <param name="packages">The packages to resolve dependencies for.</param>
        /// <param name="repositories">The repositories to use for resolving dependencies</param>
        public DependencyResolver(IEnumerable<PackageDef> packages, List<IPackageRepository> repositories)
        {
            InstalledPackages = new Dictionary<string, PackageDef>();
            resolve(repositories, packages);
        }
        
        
        private void resolve(List<IPackageRepository> repositories, IEnumerable<PackageDef> packages)
        {
            var firstleveldependencies = packages.SelectMany(pkg => pkg.Dependencies.Select(dep => new { Dependency = dep, Architecture = pkg.Architecture, OS = pkg.OS }));
            Dependencies.AddRange(packages);
            foreach (var dependency in firstleveldependencies)
            {
                GetDependenciesRecursive(repositories, dependency.Dependency, dependency.Architecture, dependency.OS);
            }
        }

        private void GetDependenciesRecursive(List<IPackageRepository> repositories, PackageDependency dependency, CpuArchitecture packageArchitecture, string OS)
        {
            if (Dependencies.Any(p => (p.Name == dependency.Name) &&
                dependency.Version.IsCompatible(p.Version) &&
                ArchitectureHelper.PluginsCompatible(p.Architecture, packageArchitecture)))
                return;
            PackageDef depPkg = GetPackageDefFromInstallation(dependency.Name, dependency.Version);
            if (depPkg == null)
            {
                depPkg = GetPackageDefFromRepo(repositories, dependency.Name, dependency.Version);
                MissingDependencies.Add(depPkg);
            }
            if (depPkg == null)
            {
                UnknownDependencies.Add(dependency);
                return;
            }
            Dependencies.Add(depPkg);
            foreach (var nextLevelDep in depPkg.Dependencies)
            {
                GetDependenciesRecursive(repositories, nextLevelDep, packageArchitecture, OS);
            }
        }

        private Dictionary<string, PackageDef> InstalledPackages;

        PackageDef GetPackageDefFromInstallation(string name, VersionSpecifier version)
        {
            if (name.ToLower().EndsWith(".tappackage"))
                name = Path.GetFileNameWithoutExtension(name);
            if(InstalledPackages.ContainsKey(name))
            {
                PackageDef package = InstalledPackages[name];
                // Check that the installed package is compatible with the required package
                if (version.IsCompatible(package.Version))
                    return package;
            }
            return null;
        }

        private PackageDef GetPackageDefFromRepo(List<IPackageRepository> repositories, string name, VersionSpecifier version)
        {
            if (name.ToLower().EndsWith(".tappackage"))
                name = Path.GetFileNameWithoutExtension(name);

            var specifier = new PackageSpecifier(name, version, CpuArchitecture.Unspecified, OperatingSystem.Current.ToString());
            var packages =  PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, specifier, InstalledPackages.Values.ToArray());

            if (packages.Any() == false)
            {
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, specifier);
                if (packages.Any())
                    log.Warning($"Unable to find a version of '{name}' package compatible with currently installed packages. Some installed packages may be upgraded.");
            }

            return packages.OrderByDescending(pkg => pkg.Version).FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.HostArchitecture));
        }
    }
}
