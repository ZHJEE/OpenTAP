using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace OpenTap.Cli
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using OpenTap;
    using OpenTap.Cli;

    namespace TapBashCompletion
    {
        internal class MyPackage
        {
            public string name { get; set; }
            public string version { get; set; }
            public bool installed { get; set; }
        }
        /// <summary>
        /// Accepts an OpenTAP CLI command and displays valid subcommands for that command.
        /// </summary>
        [Display("complete", "Get valid TAP completions for the current command line")]
        [Browsable(false)]
        public class CompleteCliAction : ICliAction
        {
            [CommandLineArgument("instructions", Description = "Show setup instructions", ShortName = "i")]
            public bool Help { get; set; } = false;
            
            [CommandLineArgument("show-config", Description = "Show a bash script to be sourced for completions",
                ShortName = "s")]
            public bool Dump { get; set; } = false;
            private void DebugLog(string input)
            {
                #if DEBUG
                using (var stream = new StreamWriter(Path.Combine(CacheDir, "CompletionDebug.log"), append: true))
                {
                    stream.WriteLine($"{DateTime.Now}: {input}");
                }
                #endif
            }

            private static string CacheDir { get; } = Path.Combine(Path.GetTempPath(), "TapCompletion");

            private void writeCompletion(string completion, bool flag)
            {
                string prepend = flag ? "--" : "";
//                completion = completion.Contains(' ') && !flag ? $"\"{completion}\"" : completion;
                completion = $"{prepend}{completion.Trim()}";
                Console.Write($"{completion}\n");
                DebugLog(completion);
            }

            private void DumpSubCommands(CliActionTree cmd)
            {
                foreach (CliActionTree subCmd in cmd.SubCommands)
                {
                    if (subCmd.IsGroup || subCmd.Type.IsBrowsable())
                    {
                        writeCompletion(subCmd.Name, false);
                    }
                }
            }

            private void DumpFlags(CliActionTree cmd)
            {
                foreach (IMemberData member in cmd.Type.GetMembers())
                {
                    foreach (var attr in member.Attributes.OfType<CommandLineArgumentAttribute>())
                    {
                        writeCompletion(attr.Name, true);
                    }
                }
            }

            private void DumpSpecialCases(CliActionTree cmd)
            {
                if (cmd.Parent?.Name != "package") 
                    return;
                
                List<MyPackage> packageList;
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
                        writeCompletion(package.name, false);
                
                else if (cmd.Name == "install" || cmd.Name == "list")
                    foreach (var package in packageList)
                        writeCompletion(package.name, false);
            }

            private List<MyPackage> QueryPackages()
            {
                var tap = Assembly.GetEntryAssembly();
                ProcessStartInfo pi = new ProcessStartInfo()
                {
                    FileName = tap.Location,
                    RedirectStandardOutput = true,
                    Arguments = "package list",
                    UseShellExecute = false,
                };
                var process = new Process()
                {
                    StartInfo = pi,
                };
                process.Start();

                var packages = new List<MyPackage>();

                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    var parts = line.Split(new string[] {" - "}, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        packages.Add(new MyPackage()
                            {name = parts[0].Trim(), installed = false, version = parts[1].Trim()});
                    }
                    else if (parts.Length == 3)
                    {
                        packages.Add(new MyPackage()
                            {name = parts[0].Trim(), installed = true, version = parts[1].Trim()});
                    }
                    else
                    {
                        // red alert
                        continue;
                    }
                }


                return packages;
            }

            private static string PackageCache { get; } = Path.Combine(CacheDir, "output_of_package_list");

            private List<MyPackage> GetPackages()
            {
                List<MyPackage> packages;
                if (File.Exists(PackageCache))
                {
                    DateTime lastWrite = File.GetLastWriteTime(PackageCache);
                    var timeSinceLastWrite = DateTime.Now - lastWrite;

                    if (timeSinceLastWrite < TimeSpan.FromDays(1))
                    {
                        using (Stream stream = new FileStream(PackageCache, FileMode.Open))
                        {
                            var deserializer = new TapSerializer();
                            packages = (List<MyPackage>) deserializer.Deserialize(stream);
                            DebugLog("Using package cache");
                            return packages;
                        }
                    }
                }

                packages = QueryPackages();

                using (Stream stream = new FileStream(PackageCache, FileMode.Create))
                {
                    var serializer = new TapSerializer();
                    serializer.Serialize(stream, packages);
                    DebugLog($"Wrote package cache to {CacheDir}");
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
                    DebugLog("Got null command from input");
                }

                return cmd;

            }
            
            /// <summary>
            /// The code to be executed by the action.
            /// </summary>
            /// <returns>Return 0 on success. Return -1 to indicate parsing error.</returns>
            public int Execute(CancellationToken cancellationToken)
            {
                if (Help)
                {
                    return ShowHelp();
                }
                else if (Dump)
                {
                    return DumpConfig();
                }
                // Print magic string so the bash script knows completions are supported
                Console.WriteLine("BASH_COMPLETIONS_SUPPORTED");
                
                var start = DateTime.Now;
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }

                var cliArgs = Environment.GetCommandLineArgs();

                string argString = "";
                if (cliArgs.Length > 2)
                {
                    argString = cliArgs[2];
                }
                    
                List<string> args = argString.Split(new string[] {" "}, StringSplitOptions.RemoveEmptyEntries).ToList();
                DebugLog($"Completion called with '{argString}'");

                CliActionTree cmd = GetCmd(args);

                if (cmd == null)
                {
                    DebugLog("Got null command from input");
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

                var elapsed = DateTime.Now;                
                
                DebugLog($"Provided completions in {(elapsed - start).Milliseconds}ms");

                return 0;
            }
            private int ShowHelp()
            {
                Console.WriteLine("Bash tab completion is available in bash!");
                Console.WriteLine("Run `tap complete --show-config` to get the completion script and source it somewhere.");
                Console.WriteLine("\nActivate for one session:\neval \"$(tap complete --show-config)\"");
                Console.WriteLine("\nLoad completions on startup:\ntap complete --show-config | sudo dd  of=/usr/share/bash-completion/completions/tap");

                return 0;
            }
            private int DumpConfig()
            {
                
//                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenTap.Sdk.New.Resources.tasksTemplate.txt"))
                var files = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenTap.bash_completion_source"))
                using (var reader = new StreamReader(stream))
                {
                    Console.WriteLine(reader.ReadToEnd());                    
                }

                return 0;
            }
        }
    }
}
