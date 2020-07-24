//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using OpenTap.Cli;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenTap.Sdk.New
{
    public abstract class GenerateType : ICliAction
    {
        public TraceSource log = Log.CreateSource("New");

        [CommandLineArgument("out", ShortName = "o", Description = "Path to generated file.")]
        public virtual string output { get; set; }

        private string _target;
        
        [CommandLineArgument("target", ShortName = "t", Description = "Directory where the command should be applied.")]
        public string Target {
            get => _target;
            set
            {
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    var dir = new DirectoryInfo(value);
                    if (dir.Exists == false)
                    {
                        var ex = new Exception($"Target directory {dir.FullName} does not exist.");
                        log.Error(ex.Message);
                        throw ex;
                    }

                    Directory.SetCurrentDirectory(dir.FullName);
                }
                _target = value;
            } 
        }
        public abstract int Execute(CancellationToken cancellationToken);

        public void WriteFile(string filepath, string content, bool force = false)
        {
            if (File.Exists(filepath) && force == false)
            {
                log.Error("File already exists: '{0}'", Path.GetFileName(filepath));
                log.Info("Do you want to override?");

                var request = new OverrideRequest();
                UserInput.Request(request, true);

                if (request.Override == RequestEnum.No)
                {
                    log.Info("File was not overridden.");
                    return;
                }
            }

            if (!Directory.Exists(Path.GetDirectoryName(filepath)) && string.IsNullOrWhiteSpace(Path.GetDirectoryName(filepath)) == false)
                Directory.CreateDirectory(Path.GetDirectoryName(filepath));

            File.WriteAllText(filepath, content);
            log.Info($"Generated file: {filepath}");
        }

        protected string TryGetNamespace()
        {
            string dir = output;
            if (output == null)
                dir = Directory.GetCurrentDirectory();
            else if (output.EndsWith("/") == false)
                dir = Path.GetDirectoryName(dir);

            var csprojFiles = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
            var csprojPath = csprojFiles.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(csprojPath) == false)
            {
                var match = Regex.Match(File.ReadAllText(csprojPath), "<RootNamespace>(.*/)</RootNamespace>");
                if (match.Success)
                    return match.Groups[1].Value;
                else
                    return Path.GetFileNameWithoutExtension(csprojPath);
            }

            throw new Exception($"Could not find project file ('.csproj') in '{dir}'.\nNote: You can create a new project with 'tap sdk new project <name>'.");
        }

        protected string ReplaceInTemplate(string content, params string[] fields)
        {
            content = Regex.Replace(content, "\\{(\\d)\\}", (m) =>
            {
                if (int.TryParse(m.Groups[1].Value, out int index) && index < fields.Length)
                    return fields[index];
                else
                    return "";
            });
            return content;
        }
    }

    public class OverrideRequest
    {
        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        [Submit]
        public RequestEnum Override { get; set; }
    }

    public enum RequestEnum
    {
        No,
        Yes
    }
}
