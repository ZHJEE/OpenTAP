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
        [Display("complete", "Get valid TAP completions for the current command line"), Browsable(false)]
        public class CompleteCliAction : ICliAction{

            public int Execute(CancellationToken cancellationToken)
            {
                List<string> args = System.Environment.GetCommandLineArgs().ToList();
                // skip tap.exe and complete
                args = args.Skip(2).ToList();
                args = args.Take(args.Count - 1).ToList();
                var tpmPipe = Environment.GetEnvironmentVariable("TPM_PIPE"); // test to see if properly overriden by WSL
                var debugAssembly = Environment.GetEnvironmentVariable("OPENTAP_DEBUGGER_ASSEMBLY"); // test to see if properly overriden by WSL

                CliActionTree root = CliActionTree.Root;
                CliActionTree subCmd = root.GetSubCommand(args.ToArray());

                foreach (IMemberData member in subCmd.Type.GetMembers())
                {
                    foreach (var memberAttribute in member.Attributes)
                    {
                        
                    }
                    
                }

                foreach (CliActionTree cmd in subCmd.SubCommands)
                {
                    foreach (var attribute in cmd.Type.Attributes)
                    {
                        
                    }
                }
                
                return 0;
            }
        }
    }
}