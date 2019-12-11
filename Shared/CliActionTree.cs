using System.Collections.Generic;
using System.Linq;
using OpenTap.Cli;

namespace OpenTap.Plugins.BasicSteps.Tap.Shared
{
    internal class CliActionTree
    {
        public string Name { get; set; }
        public bool IsGroup => Type == null;
        public ITypeData Type { get; set; }
        public List<CliActionTree> SubCommands { get; set; }

        public static CliActionTree Root { get; internal set; }
        public CliActionTree Parent { get; set; }

        static CliActionTree()
        {
            var commands = TypeData.GetDerivedTypes(TypeData.FromType(typeof(ICliAction))).Where(t => t.CanCreateInstance && t.GetDisplayAttribute() != null).ToList();
            Root = new CliActionTree { Name = "tap" };
            foreach (var item in commands)
                ParseCommand(item, item.GetDisplayAttribute().Group, Root);
        }

        private static void ParseCommand(ITypeData type, string[] group, CliActionTree command)
        {
            if (command.SubCommands == null)
                command.SubCommands = new List<CliActionTree>();

            // If group is not empty. Find command with first group name
            if (group.Length > 0)
            {
                var existingCommand = command.SubCommands.FirstOrDefault(c => c.Name == group[0]);

                if (existingCommand == null)
                {
                    existingCommand = new CliActionTree() { Name = group[0] };
                    command.SubCommands.Add(existingCommand);
                }

                ParseCommand(type, group.Skip(1).ToArray(), existingCommand);
            }
            else
            {
                command.SubCommands.Add(new CliActionTree() { Name = type.GetDisplayAttribute().Name, Type = type, SubCommands = new List<CliActionTree>(), Parent = command });
                command.SubCommands = command.SubCommands.OrderBy(c => c.Name).ToList();
            }
        }

        public CliActionTree GetSubCommand(string[] args)
        {
            if (args.Length == 0)
                return null;

            foreach (var item in SubCommands)
            {
                if (item.Name == args[0])
                {
                    if (args.Length == 1 || item.SubCommands.Any() == false)
                       return item;
                    else
                    {
                        return item.GetSubCommand(args.Skip(1).ToArray());
                    }
                }
            }

            return null;
        }
    }
}