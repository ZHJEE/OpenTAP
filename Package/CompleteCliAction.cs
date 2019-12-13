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
        internal class CompletionPackage
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
                completion = $"{(flag ? "--" : "")}{completion.Trim()}";
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
                List<string> commandNames = new List<string>();
                foreach (CliActionTree subCmd in cmd.SubCommands)
                {
                    if (subCmd.IsGroup || IsBrowsable(subCmd.Type))
                    {
                        commandNames.Add(subCmd.Name);                        
                    }
                }
                commandNames.Sort();

                foreach (var name in commandNames)
                {
                    WriteCompletion(name, false);                    
                }
            }

            private void DumpFlags(CliActionTree cmd)
            {
                List<string> flags = new List<string>();
                foreach (IMemberData member in cmd.Type.GetMembers())
                {
                    foreach (var attr in member.Attributes.OfType<CommandLineArgumentAttribute>())
                    {
                        flags.Add(attr.Name);                        
                    }
                }
                flags.Sort();

                foreach (var flag in flags)
                {
                    WriteCompletion(flag, true);
                }
            }

            private void DumpSpecialCases(CliActionTree cmd)
            {
                if (cmd.Parent?.Name != "package")
                    return;

                List<CompletionPackage> packageList;
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
                packageList.Sort((p1, p2) => String.Compare(p1.name, p2.name, StringComparison.Ordinal));

                if (cmd.Name == "uninstall" || cmd.Name == "test")
                    foreach (var package in packageList.Where(package => package.installed))
                        WriteCompletion(package.name, false);

                else if (cmd.Name == "install" || cmd.Name == "list")
                    foreach (var package in packageList)
                        WriteCompletion(package.name, false);
            }

            private List<CompletionPackage> QueryPackages()
            {
                List<IPackageRepository> repositories = PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager).ToList();
                
                var installed = new Installation(FileSystemHelper.GetCurrentInstallationDirectory()).GetPackages();
                var packages = PackageRepositoryHelpers.GetPackageNameAndVersionFromAllRepos(repositories, new PackageSpecifier());
                
                List<CompletionPackage> tapPackages = new List<CompletionPackage>();
                tapPackages.AddRange(installed.Select(x => new CompletionPackage() {name = x.Name, version = x.Version?.ToString() ?? "Unknown", installed = true}));
                tapPackages.AddRange(packages.Select(x => new CompletionPackage() {name = x.Name, version = x.Version?.ToString() ?? "Unknown"}));

                return tapPackages;
            }
           
            private List<CompletionPackage> GetPackages()
            {
                List<CompletionPackage> packages;
                if (File.Exists(PackageCache))
                {
                    DateTime lastWrite = File.GetLastWriteTime(PackageCache);
                    var timeSinceLastWrite = DateTime.Now - lastWrite;

                    if (timeSinceLastWrite < TimeSpan.FromMinutes(5))
                    {
                        using (Stream stream = new FileStream(PackageCache, FileMode.Open))
                        {
                            var deserializer = new TapSerializer();
                            packages = (List<CompletionPackage>) deserializer.Deserialize(stream);
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
                Console.WriteLine(
                    "OpenTAP tab completion is available in bash!\n" +
                    "Run 'tap complete --show-config' to get the completion script and source it somewhere.\n" +
                    "The directory containing 'tap' must be in your PATH environment variable in order for completions to work\n" +
                    "The script has been tested with Windows Subsystem for Linux and GNU/Linux versions of bash, and depends on 'bash-completion' (usually bundled with bash)\n" +
                    "\nActivate for one session:\neval \"$(tap complete --show-config)\"\n" +
                    "\nAutomatically load completions on startup:\ntap complete --show-config | sudo dd of=/etc/bash_completion.d/tap\n" +
                    "\nReplace 'tap' with 'tap.exe' if you are using windows binaries in WSL"
                    );

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
                        var content = reader.ReadToEnd();
                        // CR-LF line endings 
                        content = content.Replace("\r\n", "\n");
                        Console.Write($"{content}\n");
                        
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