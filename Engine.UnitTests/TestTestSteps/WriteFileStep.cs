
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Write File", Description: "Writes a string to a file.", Group: "Tests")]
    public class WriteFileStep : TestStep
    {
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        public string String { get; set; }
        public string File { get; set; }
        public override void Run() => System.IO.File.WriteAllText(File, String);
    }

    [Display("Create Directory", Description: "Creates a new directory.", Group: "Tests")]
    public class CreateDirectoryStep : TestStep
    {
        public string Directory { get; set; }
        public override void Run()
        {
            System.IO.Directory.CreateDirectory(Directory);
        }
    }

    [Display("Replace In File", Description: "Replaces some text in a file.", Group: "Tests")]
    public class ReplaceInFileStep : TestStep
    {
        [FilePath]
        [Display("File", Order: 0)]
        public string File { get; set;}
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        [Display("Search For", Order: 1)]
        public string Search { get; set; }
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        [Display("Replace With", Order: 2)]
        public string Replace { get; set; }

        public override void Run()
        {
            var content = System.IO.File.ReadAllText(File);
            content = content.Replace(Search, Replace);
            System.IO.File.WriteAllText(File, content);
        }
    }

    [Display("Expect", Description: "Expects  verdict in the child step.", Group: "Tests")]
    [AllowAnyChild]
    public class ExpectStep : TestStep
    {
        public Verdict ExpectedVerdict { get; set; }
        public override void Run()
        {
            RunChildSteps();
            if (Verdict == ExpectedVerdict)
                Verdict = Verdict.Pass;
            else
            {
                Verdict = Verdict.Fail;
            }
        }
    }

    [Display("Read Assembly Version", Group: "Tests")]
    public class ReadAssemblyVersionStep : TestStep
    {
        [FilePath]
        public string File { get; set; }
        
        public string MatchVersion { get; set; }
        
        public override void Run()
        {
            var searcher = new PluginSearcher(PluginSearcher.Options.IncludeNonPluginAssemblyVersions);
            searcher.Search(new[] {File});
            var asmFile = searcher.Assemblies.FirstOrDefault(asm => Path.GetFileName(asm.Location) == Path.GetFileName(File));

            var semver = asmFile.SemanticVersion;
            if (string.IsNullOrWhiteSpace(MatchVersion) == false)
            {
                if (Equals(semver.ToString(), MatchVersion))
                {
                    UpgradeVerdict(Verdict.Pass);
                }
                else
                {
                    UpgradeVerdict(Verdict.Fail);
                }
            }
            Log.Info("Read Version {0}", semver.ToString());
        }
    }

    [Display("Remove Directory", "Removes a directory.", "Tests")]
    public class RemoveDirectory : TestStep
    {
        [DirectoryPath]
        public string Path { get; set; }
        public override void Run()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
    
}