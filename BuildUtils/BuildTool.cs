using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuildUtils
{
    public static class BuildTool
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();

            var target = GetEnumArgument<BuildTarget>(args);
            var targetGroup = ConvertBuildTargetGroup(target);

            var levels = EditorBuildSettings.scenes
                .Where(v => v.enabled)
                .ToArray();

            SetPlayerSettingsPropertiesWithArguments(args);
            SetPlayerSettingsWithArguments(args, targetGroup);

            BuildPipeline.BuildPlayer(
                levels,
                "bin",
                target.Value,
                BuildOptions.None);

            var src = Path.Combine(Application.dataPath, @"..\Temp\StagingArea\Il2Cpp\il2cppOutput");
            var dest = Path.Combine(Application.dataPath, @"..\output");
            var files = Directory.GetFiles(src, @"*.cpp", SearchOption.TopDirectoryOnly);

            if (!Directory.Exists(dest))
            {
                Directory.CreateDirectory(dest);
            }

            foreach (var file in files)
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            }
        }

        /// <summary>
        /// コマンドラインからパラメータを取得
        /// </summary>
        /// <typeparam name="T">取得するEnumの型</typeparam>
        /// <param name="args">コマンドライン引数</param>
        /// <returns>指定されたパラメータ</returns>
        private static T? GetEnumArgument<T>(string[] args)
            where T : struct, Enum
        {
            var value = GetArgument(args, typeof(T).Name);

            try
            {
                return (T)Enum.Parse(typeof(T), value, ignoreCase: true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// コマンドラインからパラメータを取得
        /// </summary>
        /// <param name="args">コマンドライン引数</param>
        /// <param name="key">取得するパラメータ</param>
        /// <returns>パラメータに設定されていた値</returns>
        private static string GetArgument(string[] args, string key)
        {
            var name = $"--{key}";

            return args
                .SkipWhile(v => !string.Equals(v, name, StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .FirstOrDefault();
        }

        /// <summary>
        /// コマンドラインに指定されたキーがあるか調べる
        /// </summary>
        /// <param name="args">コマンドライン引数</param>
        /// <param name="key">調べるキー</param>
        /// <returns>含まれていればtrue</returns>
        private static bool HasArgument(string[] args, string key)
        {
            var name = $"--{key}";
            return args.Any(v => string.Equals(v, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// BuildTargetからBuildTargetGroupを取得する
        /// </summary>
        /// <param name="target">確認するtarget</param>
        /// <returns>BuildTargetGroup</returns>
        private static BuildTargetGroup ConvertBuildTargetGroup(BuildTarget? target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return BuildTargetGroup.Android;
                case null:
                    throw new InvalidOperationException($"'--{nameof(BuildTarget)} <value>' is not found.");
                default:
                    throw new NotImplementedException($"{nameof(BuildUtils)} does not support {target}.");
            }
        }

        /// <summary>
        /// PlayerSettingsのプロパティをコマンドラインから設定
        /// </summary>
        /// <param name="args">コマンドライン引数</param>
        private static void SetPlayerSettingsPropertiesWithArguments(string[] args)
        {
            var whitelist = new[]
            {
                nameof(PlayerSettings.stripEngineCode)
            };

            var type = typeof(PlayerSettings);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(v => whitelist.Contains(v.Name));

            foreach (var prop in properties)
            {
                if (prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(null, HasArgument(args, prop.Name), null);
                }
                else
                {
                    var value = GetArgument(args, prop.Name);
                    if (!string.IsNullOrEmpty(value))
                    {
                        prop.SetValue(null, value, null);
                    }
                }
            }
        }

        /// <summary>
        /// PlayerSettingsのSetメソッドをコマンドラインから設定
        /// </summary>
        /// <param name="args">コマンドライン引数</param>
        private static void SetPlayerSettingsWithArguments(string[] args, BuildTargetGroup targetGroup)
        {
            var whitelist = new[]
            {
                nameof(PlayerSettings.SetApplicationIdentifier),
                nameof(PlayerSettings.SetScriptingBackend),
            };

            var type = typeof(PlayerSettings);

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(v => v.GetParameters().Length == 2)
                .Where(v => v.GetParameters()[0].ParameterType == typeof(BuildTargetGroup))
                .Where(v => whitelist.Contains(v.Name));

            foreach (var method in methods)
            {
                var parameter = method.GetParameters()[1];

                object value = null;
                {
                    value = GetArgument(args, method.Name.Substring(3));
                }

                if (parameter.ParameterType.IsEnum)
                {
                    if (value != null)
                    {
                        try
                        {
                            value = Enum.Parse(parameter.ParameterType, value as string);
                        }
                        catch (Exception)
                        {
                            value = null;
                        }
                    }
                    else
                    {
                        var getArgument = typeof(BuildTool).GetMethod(nameof(GetEnumArgument), BindingFlags.NonPublic | BindingFlags.Static);
                        value = getArgument.MakeGenericMethod(parameter.ParameterType).Invoke(null, args);
                    }
                }

                if (value != null)
                {
                    method.Invoke(null, new object[] { targetGroup, value });
                }
            }
        }
    }
}
