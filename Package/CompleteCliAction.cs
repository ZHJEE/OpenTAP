using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenTap.Package;
using OpenTap.Plugins.BasicSteps.Tap.Shared;

namespace OpenTap.Cli
{
    namespace TapBashCompletion
    {
        internal class TapPackage
        {
            public string name { get; set; }
            public string version { get; set; }
            public bool installed { get; set; }
        }

        /// <summary>
        /// Accepts an OpenTAP CLI command and displays valid subcommands for that command.
        /// </summary>
        [Display("complete", "Get valid OpenTAP completions for the current command line")]
        [Browsable(false)]
        internal class CompleteCliAction : ICliAction
        {
            private TraceSource log = OpenTap.Log.CreateSource("CompleteCliAction");

            [CommandLineArgument("instructions", Description = "Show setup instructions", ShortName = "i")]
            public bool Instructions { get; set; } = false;

            [CommandLineArgument("show-config", Description = "Show a bash script to be sourced for completions", ShortName = "s")]
            public bool ShowConfig { get; set; } = false;

            private static string PackageCache { get; } = Path.Combine(ExecutorClient.ExeDir, ".TapCompletion.cache");

            /// <summary>
            /// The code to be executed by the action.
            /// </summary>
            /// <returns>Return 0 on success. Return -1 to indicate parsing error.</returns>
            public int Execute(CancellationToken cancellationToken)
            {
                if (Instructions)
                {
                    return ShowHelp();
                }
                else if (ShowConfig)
                {
                    return DumpConfig();
                }

                // Print magic string so the bash script knows completions are supported
                Console.WriteLine("BASH_COMPLETIONS_SUPPORTED");

                var cliArgs = Environment.GetCommandLineArgs();

                string argString = "";
                if (cliArgs.Length > 2)
                {
                    argString = cliArgs[2];
                }

                List<string> args = argString.Split(new string[] {" "}, StringSplitOptions.RemoveEmptyEntries).ToList();
                log.Debug($"Completion called with '{argString}'");

                CliActionTree cmd = GetCmd(args);

                if (cmd == null)
                {
                    log.Debug("Got null command from input");
                    return -1;
                }

                if (cmd.SubCommands.Count > 0)
                {
                    DumpSubCommands(cmd);
                }
                else
                {
                    DumpFlags(cmd);
                }

                DumpSpecialCases(cmd);

                return 0;
            }

            private void WriteCompletion(string completion, bool flag)
            {
                string prepend = flag ? "--" : "";
                completion = $"{prepend}{completion.Trim()}";
                Console.Write($"{completion}\n");
                log.Debug(completion);
            }

            private static bool IsBrowsable(ITypeData type)
            {                
                if (type is TypeData td)
                {
                    return td.IsBrowsable;
                }
                var attr = type.GetAttribute<BrowsableAttribute>();
                if (attr is null)
                    return true;
                
                return attr.Browsable;
            }
            
            private void DumpSubCommands(CliActionTree cmd)
            {
                foreach (CliActionTree subCmd in cmd.SubCommands)
                {
                    if (subCmd.IsGroup || IsBrowsable(subCmd.Type))
                    {
                        WriteCompletion(subCmd.Name, false);
                    }
                }
            }

            private void DumpFlags(CliActionTree cmd)
            {
                foreach (IMemberData member in cmd.Type.GetMembers())
                {
                    foreach (var attr in member.Attributes.OfType<CommandLineArgumentAttribute>())
                    {
                        WriteCompletion(attr.Name, true);
                    }
                }
            }

            private void DumpSpecialCases(CliActionTree cmd)
            {
                if (cmd.Parent?.Name != "package")
                    return;

                List<TapPackage> packageList;
                switch (cmd.Name)
                {
                    case "uninstall":
                    case "test":
                    case "install":
                    case "list":
                        packageList = GetPackages();
                        break;
                    default:
                        return;
                }

                if (cmd.Name == "uninstall" || cmd.Name == "test")
                    foreach (var package in packageList.Where(package => package.installed))
                        WriteCompletion(package.name, false);

                else if (cmd.Name == "install" || cmd.Name == "list")
                    foreach (var package in packageList)
                        WriteCompletion(package.name, false);
            }

            private List<TapPackage> QueryPackages()
            {
                List<IPackageRepository> repositories = PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager).ToList();
                
                var installed = new Installation(FileSystemHelper.GetCurrentInstallationDirectory()).GetPackages();
                var packages = PackageRepositoryHelpers.GetPackageNameAndVersionFromAllRepos(repositories, new PackageSpecifier());
                
                List<TapPackage> tapPackages = new List<TapPackage>();
                tapPackages.AddRange(installed.Select(x => new TapPackage() {name = x.Name, version = x.Version?.ToString() ?? "Unknown", installed = true}));
                tapPackages.AddRange(packages.Select(x => new TapPackage() {name = x.Name, version = x.Version?.ToString() ?? "Unknown"}));

                return tapPackages;
            }
           
            private List<TapPackage> GetPackages()
            {
                List<TapPackage> packages;
                if (File.Exists(PackageCache))
                {
                    DateTime lastWrite = File.GetLastWriteTime(PackageCache);
                    var timeSinceLastWrite = DateTime.Now - lastWrite;

                    if (timeSinceLastWrite < TimeSpan.FromMinutes(5))
                    {
                        using (Stream stream = new FileStream(PackageCache, FileMode.Open))
                        {
                            var deserializer = new TapSerializer();
                            packages = (List<TapPackage>) deserializer.Deserialize(stream);
                            log.Debug($"Used package cache ({PackageCache})");
                            return packages;
                        }
                    }
                }

                packages = QueryPackages();

                using (Stream stream = new FileStream(PackageCache, FileMode.Create))
                {
                    var serializer = new TapSerializer();
                    serializer.Serialize(stream, packages);
                    log.Debug($"Wrote new package cache ({PackageCache})");
                }

                return packages;
            }

            private CliActionTree GetCmd(List<string> args)
            {
                CliActionTree root = CliActionTree.Root;
                CliActionTree cmd;

                cmd = root.GetSubCommand(args.ToArray()) ??
                      root.GetSubCommand(args.Take(args.Count - 1).ToArray()) ?? null;

                if (cmd == null && args.Count <= 1)
                    cmd = root;


                if (cmd == null)
                {
                    log.Debug("Got null command from input");
                }

                return cmd;
            }

            private int ShowHelp()
            {
                Console.WriteLine("Bash tab completion is available in bash!");
                Console.WriteLine(
                    "Run `tap complete --show-config` to get the completion script and source it somewhere.");
                Console.WriteLine("\nActivate for one session:\neval \"$(tap complete --show-config)\"");
                Console.WriteLine(
                    "\nLoad completions on startup:\ntap complete --show-config | sudo dd  of=/usr/share/bash-completion/completions/tap");

                return 0;
            }

            private int DumpConfig()
            {
                try
                {
                    using (var stream = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("OpenTap.Package.bash_completion_source"))
                    using (var reader =
                        new StreamReader(stream ?? throw new Exception("Bash completions script not embedded.")))
                    {
                        Console.Write(reader.ReadToEnd());
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    log.Debug(ex);
                }

                return 0;
            }
        }
    }
}