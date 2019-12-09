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
        [Display("complete", "Get valid TAP completions for the current command line"), Browsable(false)]
        public class CompleteCliAction : ICliAction
        {
            [CommandLineArgument("help", Description = "Show  setup instructions", ShortName = "h", Visible = false)]
            private bool Help { get; } = false;

            [CommandLineArgument("dump-config", Description = "Dump a bash script to be sourced for completions",
                ShortName = "d", Visible = false)]
            private bool Dump { get; } = false;
            private void writeCompletion(string completion, bool flag)
            {
                string prepend = flag ? "--" : "";
                completion = completion.Contains(' ') && !flag ? $"\"\\\"{completion}\\\"\"" : completion;
                Console.Write($"{prepend}{completion}\n");
            }

            private void dumpSubCommands(CliActionTree cmd)
            {
                foreach (CliActionTree subCmd in cmd.SubCommands)
                {
                    if (subCmd.IsGroup || subCmd.Type.IsBrowsable())
                    {
                        writeCompletion(subCmd.Name, false);
                    }
                }
            }

            private void dumpFlags(CliActionTree cmd)
            {
                foreach (IMemberData member in cmd.Type.GetMembers())
                {
                    foreach (var attr in member.Attributes.OfType<CommandLineArgumentAttribute>())
                    {
                        writeCompletion(attr.Name, true);
                    }
                }
            }
            
            private void dumpSpecialCases(CliActionTree cmd)
            {
                List<MyPackage> packageList;
                if (cmd.Parent?.Name == "package")
                {
                    switch (cmd.Name)
                    {
                        case "uninstall":
                        case "test":
                        case "install":
                        case "list":
                            packageList = getPackages();
                            break;
                        default:
                            return;                        
                    }
                    switch (cmd.Name)
                    {
                        case "uninstall":
                        case "test":
                            foreach (var package in packageList)
                            {
                                if (package.installed)
                                {
                                    writeCompletion(package.name, false);
                                }
                            }
                            break;
                        case "install":
                        case "list":
                            foreach (var package in packageList)
                            {
                                writeCompletion(package.name, false);
                            }
                            break;
                    }                    
                }
                
            }

            private List<MyPackage> queryPackages()
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
                //serializer.Serialize(new StreamWriter(".output_of_package_list", false), (object)packages);
                
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    var parts = line.Split(new string[]{" - "}, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        packages.Add(new MyPackage() {name = parts[0].Trim(), installed = false, version = parts[1].Trim()});

                    }
                    else if (parts.Length == 3)
                    {
                        packages.Add(new MyPackage() {name = parts[0].Trim(), installed = true, version = parts[1].Trim()});
                    }
                    else
                    {
                        // red alert
                        continue;
                    }
                }

                return packages;
            }

            private static string packageCache { get; } = ".output_of_package_list";

            private List<MyPackage> getPackages()
            {
                if (File.Exists(packageCache))
                {
                    DateTime lastWrite = File.GetLastWriteTime(packageCache);
                    var timeSinceLastWrite = DateTime.Now - lastWrite;

                    if (timeSinceLastWrite < TimeSpan.FromDays(1))
                    {
                        using (Stream stream = new FileStream(packageCache, FileMode.Open))
                        {
                            var deserializer = new TapSerializer();
                            return (List<MyPackage>)deserializer.Deserialize(stream);
                        }
                    }
                    
                }

                using (Stream stream = new FileStream("serialized_struct.txt", FileMode.Create))
                {
                    var serializer = new TapSerializer();
                    serializer.Serialize(stream, new MyPackage(){name="test", version="1.2.3", installed=false});
                }

                var packages = queryPackages();

                return packages;
            }

            public int Execute(CancellationToken cancellationToken)
            {
                List<string> args = System.Environment.GetCommandLineArgs().ToList();
                // skip tap.exe and complete
                args = args.Skip(2).ToList();
//                args = args.Take(args.Count - 1).ToList();
                var tpmPipe = Environment.GetEnvironmentVariable("TPM_PIPE"); // test to see if properly overriden by WSL
                var debugAssembly = Environment.GetEnvironmentVariable("OPENTAP_DEBUGGER_ASSEMBLY"); // test to see if properly overriden by WSL

                CliActionTree root = CliActionTree.Root;
                CliActionTree cmd = root.GetSubCommand(args.ToArray()) ??
                                    root.GetSubCommand(args.Take(args.Count - 1).ToArray()) ?? root;

                if (cmd.SubCommands.Count > 0)
                {
                    dumpSubCommands(cmd);
                }
                else
                {
                    dumpFlags(cmd);
                }

                dumpSpecialCases(cmd);

                return 0;
            }


        }
    }
}