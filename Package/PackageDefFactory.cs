using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace OpenTap.Package
{
    class PackageDefFactory
    {
        static Stream ConvertXml(Stream stream)
        {
            var root = XElement.Load(stream);

            var xns = root.GetDefaultNamespace();
            var filesElement = root.Element(xns + "Files");
            if (filesElement != null)
            {
                var fileElements = filesElement.Elements(xns + "File");
                foreach (var file in fileElements)
                {
                    var plugins = file.Element(xns + "Plugins");
                    if (plugins == null) continue;

                    var pluginElements = plugins.Elements(xns + "Plugin");
                    foreach (var plugin in pluginElements)
                    {
                        if (!plugin.HasElements && !plugin.IsEmpty)
                        {
                            plugin.SetAttributeValue("Type", plugin.Value);
                            var value = plugin.Value;
                            plugin.Value = "";
                        }
                    }
                }
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(root.ToString()));
        }

        static string GetMetadataFromPackage(string path)
        {
            string metaFilePath = PluginInstaller.FilesInPackage(path)
                .Where(p => p.Contains(PackageDef.PackageDefDirectory) && p.EndsWith(PackageDef.PackageDefFileName))
                .OrderBy(p => p.Length).FirstOrDefault(); // Find the xml file in the most top level
            if (String.IsNullOrEmpty(metaFilePath))
            {
                // for TAP 8.x support, we could remove when 9.0 is final, and packages have been migrated.
                metaFilePath = PluginInstaller.FilesInPackage(path).FirstOrDefault(p => (p.Contains("package/") || p.Contains("Package Definitions/")) && p.EndsWith("package.xml"));
                if (String.IsNullOrEmpty(metaFilePath))
                    throw new IOException("No metadata found in package " + path);
            }

            return metaFilePath;
        }

        /// <summary>
        /// Loads package definition from a file.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static T FromXml<T>(Stream stream) where T : PackageDef
        {
            stream = ConvertXml(stream);

            var serializer = new TapSerializer();
            return (T)serializer.Deserialize(stream, type: TypeData.FromType(typeof(T)));
        }

        public static IEnumerable<T> ManyFromXml<T>(Stream stream) where T : PackageDef
        {
            var root = XElement.Load(stream);
            List<T> packages = new List<T>();

            Parallel.ForEach(root.Nodes(), node =>
            {
                using (Stream str = new MemoryStream())
                {
                    if (node is XElement)
                    {
                        (node as XElement).Save(str);
                        str.Seek(0, 0);
                        var package = FromXml<T>(str);
                        if (package != null)
                        {
                            lock (packages)
                            {
                                packages.Add(package);
                            }
                        }
                    }
                    else
                    {
                        throw new XmlException("Invalid XML");
                    }
                }
            });

            return packages;
        }

        /// <summary>
        /// Constructs a PackageDef object to represent a TapPackage package that has already been created.
        /// </summary>
        /// <param name="path">Path to a *.TapPackage file</param>
        public static T FromPackage<T>(string path) where T : PackageDef
        {
            string metaFilePath = GetMetadataFromPackage(path);

            T pkgDef;
            using (Stream metaFileStream = new MemoryStream(1000))
            {
                if (!PluginInstaller.UnpackageFile(path, metaFilePath, metaFileStream))
                    throw new Exception("Failed to extract package metadata from package.");
                metaFileStream.Seek(0, SeekOrigin.Begin);
                pkgDef = FromXml<T>(metaFileStream);
            }
            //pkgDef.updateVersion();
            pkgDef.DirectUrl = Path.GetFullPath(path);
            return pkgDef;
        }


        public static List<T> FromPackages<T>(string path) where T : PackageDef
        {
            var packageList = new List<T>();

            if (Path.GetExtension(path).ToLower() != ".tappackages")
            {
                packageList.Add(FromPackage<T>(path));
                return packageList;
            }

            try
            {
                using (var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
                {
                    foreach (var part in zip.Entries)
                    {
                        FileSystemHelper.EnsureDirectory(part.FullName);
                        var instream = part.Open();
                        using (var outstream = File.Create(part.FullName))
                        {
                            var task = instream.CopyToAsync(outstream, 4096, TapThread.Current.AbortToken);
                            ConsoleUtils.PrintProgressTillEnd(task, "Decompressing", () => instream.Position, () => instream.Length);
                        }

                        var package = FromPackage<T>(part.FullName);
                        packageList.Add(package);

                        if (File.Exists(part.FullName))
                            File.Delete(part.FullName);
                    }
                }
            }
            catch (InvalidDataException)
            {
                Log.CreateSource("PackageDef").Error($"Could not unpackage '{path}'.");
                throw;
            }

            return packageList;
        }
    }
}
