using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// A PackageDef with metadata about its source.
    /// </summary>
    abstract class RepositoryPackageDef : PackageDef
    {
        public string RepositoryUrl 
        {
            get => (string)Parameters["PackageRepositoryUrl"];
            set => Parameters["PackageRepositoryUrl"] = value;
        }

        public abstract IPackageRepository GetRepository();
    }

    class FileRepositoryPackageDef : RepositoryPackageDef
    {
        /// <summary>
        /// Constructs a PackageDef object to represent a TapPackage package that has already been created.
        /// </summary>
        /// <param name="path">Path to a *.TapPackage file</param>
        public static new FileRepositoryPackageDef FromPackage(string path)
        {
            var pkg = PackageDefFactory.FromPackage<FileRepositoryPackageDef>(path);
            pkg.PackageFilePath = path;
            return pkg;
        }

        /// <summary>
        /// Direct path to the *.TapPackage file that this object represents
        /// </summary>
        public string PackageFilePath { get; private set; }

        public override IPackageRepository GetRepository() => new FilePackageRepository(RepositoryUrl);
    }

    class HttpRepositoryPackageDef : RepositoryPackageDef
    {
        /// <summary>
        /// Direct url to download the *.TapPackage file that this object represents
        /// </summary>
        public string PackageUrl
        {
            get => (string)Parameters["DirectUrl"];
            set => Parameters["DirectUrl"] = value;
        }

        public override IPackageRepository GetRepository() => new HttpPackageRepository(RepositoryUrl);
    }
}
