using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;

namespace UniCs2Cpp
{
    public class Program
    {
        private const string ProjectName = nameof(UniCs2Cpp);
        private const string AssetsFolderName = "Assets";

        /// <summary>
        /// エントリーポイント
        /// </summary>
        /// <param name="args">コマンドライン引数</param>
        public static void Main(string[] args)
        {
            var app = new CommandLineApplication(false)
            {
                Name = nameof(UniCs2Cpp),
                Description = "UnityのC#スクリプトからIL2CPPで変換されたコードを取得します",
            };

            var input = app.Option("-i|--input", "C#が記述されているファイルへのフルパス", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output", "変換したファイルの出力先", CommandOptionType.SingleValue);
            app.HelpOption("-?|-h|--help");

            app.OnExecute(() => Execute(input, output));

            app.Execute(args);
        }

        /// <summary>
        /// コマンド本体
        /// </summary>
        /// <param name="input">-iに渡されたパラメータ</param>
        /// <param name="output">-oに渡されたパラメータ</param>
        /// <returns>成功したら0を返す</returns>
        private static int Execute(CommandOption input, CommandOption output)
        {
            if (CheckArguments(input, output) is int value)
            {
                return value;
            }

            var work = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                SetupProject(work);
                AddTargetSource(work, input.Value());
                ExecuteUnity(work);
                FetchOutput(work, output.Value());
            }
            finally
            {
                Directory.Delete(work, recursive: true);
            }

            return 0;
        }

        /// <summary>
        /// 引数チェック
        /// </summary>
        /// <param name="input">-iに渡されたパラメータ</param>
        /// <param name="output">-oに渡されたパラメータ</param>
        /// <returns>エラーが無かった場合はnull</returns>
        private static int? CheckArguments(CommandOption input, CommandOption output)
        {
            if (!input.HasValue())
            {
                Console.WriteLine($"[Error] argument '--{nameof(input)} <file path>' is not found.");
                return 11;
            }

            if (!output.HasValue())
            {
                Console.WriteLine($"[Error] argument '--{nameof(output)} <file path>' is not found.");
                return 12;
            }

            if (!Path.IsPathRooted(input.Value()))
            {
                Console.WriteLine($"[Error] argument '--{nameof(input)} {input.Value()}' is not absolute path.");
                return 21;
            }

            if (!Path.IsPathRooted(output.Value()))
            {
                Console.WriteLine($"[Error] argument '--{nameof(output)} {output.Value()}' is not absolute path.");
                return 22;
            }

            if (string.Equals(input.Value(), output.Value(), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Error] argument {nameof(input)} and {nameof(output)} are same.");
                return 30;
            }

            return null;
        }

        /// <summary>
        /// Unityプロジェクトの準備
        /// </summary>
        /// <param name="work">プロジェクトを構築するフォルダ</param>
        private static void SetupProject(string work)
        {
            {
                var folder = Path.Combine(work, ProjectName, AssetsFolderName, ProjectName);
                Directory.CreateDirectory(folder);
                using (var writer = new StreamWriter(Path.Combine(folder, Path.ChangeExtension(ProjectName, "asmdef")), append: false, encoding: Encoding.UTF8))
                {
                    writer.WriteLine(@"{");
                    writer.WriteLine($"  \"name\": \"{ProjectName}\"");
                    writer.WriteLine(@"}");
                }
            }

            {
                var folder = Path.Combine(work, ProjectName, AssetsFolderName, "Scenes");
                Directory.CreateDirectory(folder);
                using (var writer = new StreamWriter(Path.Combine(folder, "scene.unity"), append: false, encoding: Encoding.UTF8))
                {
                    writer.WriteLine(@"%YAML 1.1");
                }
            }

            {
                var folder = Path.Combine(work, ProjectName, AssetsFolderName, "Editor");
                Directory.CreateDirectory(folder);
                File.Copy(@"BuildUtils.dll", Path.Combine(folder, @"BuildUtils.dll"));
            }
        }

        private static void AddTargetSource(string work, string source)
        {
            var folder = Path.Combine(work, ProjectName, AssetsFolderName, ProjectName);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(folder, Path.ChangeExtension(ProjectName, @"cs")));
            }
        }

        private static void FetchOutput(string work, string dest)
        {
            var file = Directory.EnumerateFiles(Path.Combine(work, ProjectName, @"output"))
                .Where(v => Path.GetFileName(v).ToLower().Contains(ProjectName.ToLower()))
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(file))
            {
                if (!Directory.Exists(Path.GetDirectoryName(dest)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                }

                File.Copy(file, dest);
            }
        }

        private static int ExecuteUnity(string work)
        {
#if NET46
#if false
            var sandbox = AppDomain.CreateDomain(
                "sandbox",
                new System.Security.Policy.Evidence(),
                new AppDomainSetup(),
                new System.Security.PermissionSet(System.Security.Permissions.PermissionState.None));
#endif
#endif

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = @"C:\Program Files\Unity\Hub\Editor\2018.1.9f1\Editor\Unity.exe",
                    Arguments = $"-batchmode -nographics -quit -projectPath \"{Path.Combine(work, ProjectName)}\" -executeMethod {"BuildUtils.BuildTool.Build"} --BuildTarget Android --ApplicationIdentifier com.yol.unics2cpp --ScriptingBackend IL2CPP -logFile \"{Path.Combine(work, "batch.log")}\"",
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}
