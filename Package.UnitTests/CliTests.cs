﻿//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package.UnitTests
{
    static class DummyPackageGenerator
    {
        public static string GeneratePackage(PackageDef definition)
        {
            foreach (var packageFile in definition.Files)
            {
                File.CreateText(packageFile.FileName).Close();
            }
            string defFileName = "generated_package.xml";
            using (var stream = File.Create(defFileName))
                definition.SaveTo(stream);
            var startinfo = new ProcessStartInfo
            {
                FileName = Path.GetFileName(Path.Combine(Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location), "tap.exe")),
                Arguments = "package create " + defFileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var p = Process.Start(startinfo);
            p.WaitForExit();
            string output = p.StandardOutput.ReadToEnd();
            output += p.StandardError.ReadToEnd();
            string outputFile = definition.Name + "." + definition.Version + ".TapPackage";
            if (File.Exists(outputFile))
                return outputFile;
            else
                throw new Exception(output);
        }

        public static void AddFile(this PackageDef def, string filename)
        {
            def.Files.Add(new PackageFile { RelativeDestinationPath = filename, Plugins = new List<PluginFile>() });
        }
    }

    [TestFixture]
    public class CliTests
    {
        [Test]
        public void CheckInvalidPackageName()
        {
            // Check if name contains invalid character
            try
            {
                DummyPackageGenerator.GeneratePackage(new PackageDef(){Name = "Op/en:T\\AP"});
                Assert.Fail("Path contains invalid character");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Assert.True(e.Message.Contains("invalid file path characters"));
            }
        }
        
        [Test]
        public void ListTest()
        {
            int exitCode;
            string output = RunPackageCli("list", out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code.\r\n" + output);
            StringAssert.Contains("OpenTAP ", output);
            Debug.Write(output);
        }

        [Test]
        public void InstallOutsideTapDir()
        {
            var depDef = new PackageDef();
            depDef.Name = "Pkg1";
            depDef.Version = SemanticVersion.Parse("1.0");
            depDef.AddFile("Dependency.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);
            
            string tempFn = Path.Combine(Path.GetTempPath(), Path.GetFileName(dep0File));

            if (File.Exists(tempFn))
                File.Delete(tempFn);
            File.Move(dep0File, tempFn);

            try
            {
                if (File.Exists("Dependency.txt"))
                    File.Delete("Dependency.txt");
                int exitCode;
                string output = RunPackageCli("install " + Path.GetFileName(tempFn), out exitCode, Path.GetDirectoryName(tempFn));
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                //StringAssert.Contains("Pkg1", output);
                //Assert.IsTrue(File.Exists("Dependency.txt"));
                //PluginInstaller.Uninstall(depDef);
            }
            finally
            {
                File.Delete(dep0File);
            }
        }

        [Test]
        public void InstallFileWithDependenciesTest()
        {
            var depDef = new PackageDef();
            depDef.Name = "Dependency";
            depDef.Version = SemanticVersion.Parse("1.0");
            depDef.AddFile("Dependency.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);

            var dummyDef = new PackageDef();
            dummyDef.Name = "Dummy";
            dummyDef.Version = SemanticVersion.Parse("1.0");
            dummyDef.AddFile("Dummy.txt");
            dummyDef.Dependencies.Add(new PackageDependency( "Dependency", VersionSpecifier.Parse("1.0")));
            string dummyFile = DummyPackageGenerator.GeneratePackage(dummyDef);

            try
            {
                if (File.Exists("Dependency.txt"))
                    File.Delete("Dependency.txt");
                if (File.Exists("Dummy.txt"))
                    File.Delete("Dummy.txt");
                int exitCode;
                string output = RunPackageCli("install Dummy -y", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("Dummy", output);
                StringAssert.Contains("Dependency", output);
                Assert.IsTrue(File.Exists("Dependency.txt"));
                Assert.IsTrue(File.Exists("Dummy.txt"));
            }
            finally
            {
                PluginInstaller.Uninstall(dummyDef);
                PluginInstaller.Uninstall(depDef);
                File.Delete(dep0File);
                File.Delete(dummyFile);
            }
        }

        [Test]
        public void CyclicDependenciesTest()
        {
            var depDef = new PackageDef();
            depDef.Name = "Dependency";
            depDef.Version = SemanticVersion.Parse("1.0");
            depDef.AddFile("Dependency.txt");
            depDef.Dependencies.Add(new PackageDependency("Dummy", VersionSpecifier.Parse("1.0")));
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);

            var dummyDef = new PackageDef();
            dummyDef.Name = "Dummy";
            dummyDef.Version = SemanticVersion.Parse("1.0");
            dummyDef.AddFile("Dummy.txt");
            dummyDef.Dependencies.Add(new PackageDependency("Dependency", VersionSpecifier.Parse("1.0")));
            string dummyFile = DummyPackageGenerator.GeneratePackage(dummyDef);

            try
            {
                if (File.Exists("Dependency.txt"))
                    File.Delete("Dependency.txt");
                if (File.Exists("Dummy.txt"))
                    File.Delete("Dummy.txt");
                int exitCode;
                string output = RunPackageCli("install Dummy -y", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code.\r\n" + output);
                StringAssert.Contains("Dummy", output);
                StringAssert.Contains("Dependency", output);
                Assert.IsTrue(File.Exists("Dependency.txt"));
                Assert.IsTrue(File.Exists("Dummy.txt"));
            }
            finally
            {
                PluginInstaller.Uninstall(dummyDef);
                PluginInstaller.Uninstall(depDef);
                File.Delete(dep0File);
                File.Delete(dummyFile);
            }
        }

        [Test]
        public void UninstallTest()
        {
            try
            {
                int exitCode;
                string output = RunPackageCli("install NoDepsPlugin", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("NoDepsPlugin", output);
                Assert.IsTrue(File.Exists("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll"));

                output = RunPackageCli("uninstall NoDepsPlugin", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("NoDepsPlugin", output);
                Assert.IsFalse(File.Exists("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll"));
            }
            finally
            {
                if (Directory.Exists("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll"))
                    Directory.Delete("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll",true);
            }
        }

        [Test]
        public void InstallNonExistentFileTest()
        {
            int exitCode;
            string output = RunPackageCli("install NonExistent.TapPackage", out exitCode);
            //Assert.AreEqual(1, exitCode, "Unexpected exit code");
            StringAssert.Contains("NonExistent", output);
        }

        [Test]
        public void UninstallFirstTest()
        {
            var dep0Def = new PackageDef();
            dep0Def.Name = "UninstallPackage";
            dep0Def.Version = SemanticVersion.Parse("0.1");
            dep0Def.AddFile("UninstallText.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(dep0Def);

            var dep1Def = new PackageDef();
            dep1Def.Name = "UninstallPackage";
            dep1Def.Version = SemanticVersion.Parse("0.2");
            dep1Def.AddFile("SubDir\\UninstallText.txt");
            Directory.CreateDirectory("SubDir");
            string dep1File = DummyPackageGenerator.GeneratePackage(dep1Def);

            int exitCode;
            string output = RunPackageCli("install " + dep0File, out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code1: " + output);

            Assert.IsTrue(File.Exists("UninstallText.txt"), "File0 should exist");

            output = RunPackageCli("install " + dep1File, out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code2: " + output);

            Assert.IsTrue(File.Exists("SubDir\\UninstallText.txt"), "File1 should exist");
            Assert.IsFalse(File.Exists("UninstallText.txt"), "File0 should not exist");
        }

        [Test]
        public void UpgradeDependencyTest()
        {
            var dep0Def = new PackageDef();
            dep0Def.Name = "Dependency";
            dep0Def.Version = SemanticVersion.Parse("0.1");
            dep0Def.AddFile("Dependency0.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(dep0Def);

            var dep1Def = new PackageDef();
            dep1Def.Name = "Dependency";
            dep1Def.Version = SemanticVersion.Parse("1.0");
            dep1Def.AddFile("Dependency1.txt");
            string dep1File = DummyPackageGenerator.GeneratePackage(dep1Def);

            var dummyDef = new PackageDef();
            dummyDef.Name = "Dummy";
            dummyDef.Version = SemanticVersion.Parse("1.0");
            dummyDef.AddFile("Dummy.txt");
            dummyDef.Dependencies.Add(new PackageDependency("Dependency", VersionSpecifier.Parse("1.0")));
            string dummyFile = DummyPackageGenerator.GeneratePackage(dummyDef);


            try
            {
                int exitCode;
                string output = RunPackageCli("install " + dep0File + " --force", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");

                output = RunPackageCli("install Dummy -y", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                //StringAssert.Contains("upgrading", output);
                Assert.IsTrue(File.Exists("Dependency1.txt"));
            }
            finally
            {
                PluginInstaller.Uninstall(dummyDef);
                PluginInstaller.Uninstall(dep1Def);
                File.Delete(dep0File);
                File.Delete(dep1File);
                File.Delete(dummyFile);
            }
        }

        [Test]
        public void InstallFromRepoTest()
        {
            int exitCode;
            // TODO: we need the --version part below because the release version of License Injector does not yet support TAP 9.x, when it does, we can remove it again.
            string output = RunPackageCli("install \"License Injector\" -r http://packages.opentap.keysight.com --version \"-beta\" -f", out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code: " + output);
            Assert.IsTrue(output.Contains("Installed License Injector"));
            output = RunPackageCli("uninstall \"License Injector\" -f", out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code: " + output);
            Assert.IsTrue(output.Contains("Uninstalled License Injector"));
        }
        
        [Test]
        public void InstallFileWithMissingDependencyTest()
        {
            var def = new PackageDef();
            def.Name = "Dummy";
            def.Version = SemanticVersion.Parse("1.0");
            def.AddFile("Dummy.txt");
            def.Dependencies.Add(new PackageDependency("Missing", VersionSpecifier.Parse("1.0")));
            string pkgFile = DummyPackageGenerator.GeneratePackage(def);

            try
            {
                int exitCode;
                string output = RunPackageCli("install Dummy -y", out exitCode);
                //Assert.AreNotEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("'Missing' with a version compatible with 1.0", output);
            }
            finally
            {
                File.Delete(pkgFile);
            }
        }

        [Test]
        public void InstallPackagesFileTest()
        {
            string packageName = "REST-API", prerelease = "rc";
            var installation = new Installation(Directory.GetCurrentDirectory());
            
            int exitCode;

            string output = RunPackageCli("install -f \"" + packageName + "\" --version \"1.1.180-" + prerelease + "\" -y", out exitCode);
            var installedAfter = installation.GetPackages();

            Assert.IsTrue(installedAfter.Any(p => p.Name == packageName), "Package '" + packageName + "' was not installed.");
            Assert.IsTrue(installedAfter.Any(p => p.Name == packageName && p.Version.PreRelease == prerelease), "Package '" + packageName + "' was not installed with '--version'.");
            
            output = RunPackageCli("uninstall \"" + packageName + "\"", out exitCode);
            installedAfter = installation.GetPackages();
            Assert.IsFalse(installedAfter.Any(p => p.Name == packageName), "Package '" + packageName + "' was not uninstalled.");
        }



        private static string RunPackageCli(string args, out int exitCode, string workingDir = null)
        {
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location), "Packages/OpenTap-CE.package.xml")))
            {
                string createOpenTap = "create -v ../../opentap/opentapCE.package.xml -o \"Packages/OpenTAP-CE.package.xml\"";
                RunPackageCliWrapped(createOpenTap, out exitCode, workingDir);
            }
            return RunPackageCliWrapped(args, out exitCode, workingDir);
        }

        private static string RunPackageCliWrapped(string args, out int exitCode, string workingDir)
        {
            var startinfo = new ProcessStartInfo
            {
                FileName = Path.GetFileName(Path.Combine(Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location), "tap.exe")),
                WorkingDirectory = workingDir,
                Arguments = "package " + args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var p = Process.Start(startinfo);

            StringBuilder output = new StringBuilder();
            var lockObj = new object();

            p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (lockObj) output.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (lockObj) output.AppendLine(e.Data); };

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            p.WaitForExit();

            exitCode = p.ExitCode;

            return output.ToString();
        }
    }
}
