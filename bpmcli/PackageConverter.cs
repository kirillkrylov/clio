﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Json;
using System.Linq;
using File = System.IO.File;

namespace bpmcli
{
	internal class PackageConverter
	{
		private static string prefix = string.Empty;

		internal static int Convert(ConvertOptions options) {
			try {
				var names = new List<string>();
				if (String.IsNullOrEmpty(options.Name)) {
					if (options.Path == null) {
						options.Path = Environment.CurrentDirectory;
					}
					DirectoryInfo info = new DirectoryInfo(options.Path);
					foreach (var directory in info.GetDirectories()) {
						if (File.Exists(Path.Combine(directory.FullName, prefix, "descriptor.json"))) {
							names.Add(directory.Name);
						}
					}
				} else {
					names = options.Name.Split(',').Select((a) => a.Trim()).ToList();
				}
				foreach (var name in names) {
					ConvertOptions convertOptions = new ConvertOptions {
						Path = Path.Combine(options.Path, name)
					};
					if (ConvertPackage(convertOptions) == 1) {
						return 1;
					}
				}
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e);
				return 1;
			}
		}

		private static int ConvertPackage(ConvertOptions options) {
			try {
				string packageName = new DirectoryInfo(options.Path).Name;
				Console.WriteLine("Start converting package '{0}'.", packageName);
				string packagePath = Path.Combine(options.Path, prefix);
				var backupPath = packageName + ".zip";
				if (File.Exists(backupPath)) {
					File.Delete(backupPath);
				}
				ZipFile.CreateFromDirectory(packagePath, backupPath);
				Console.WriteLine("Created backup package '{0}'.", packageName);

				var fileNames = MoveCsFiles(packagePath);
				CorrectingFiles(packagePath);
				CreateProjectInfo(packagePath, packageName, fileNames);
				Console.WriteLine("Package '{0}' converted.", packageName);
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e);
				return 1;
			}
		}

		private static List<string> MoveCsFiles(string path) {
			var schemasPath = Path.Combine(path, "Schemas");
			var csFilesPath = Path.Combine(path, "Files", "cs");
			Directory.CreateDirectory(csFilesPath);
			return MoveFiles(schemasPath, csFilesPath, "*.cs");
		}

		private static void CorrectingFiles(string path) {
			var csFilesPath = Path.Combine(path, "Files", "cs");
			var resourcePath = Path.Combine(path, "Resources");
			var schemasPath = Path.Combine(path, "Schemas");
			var names = new List<string>();
			var csFilesDir = new DirectoryInfo(csFilesPath);
			foreach (var file in csFilesDir.GetFiles("*.cs")) {
				var name = file.Name.Split('.')[0];
				names.Add(name);
				var currentResourcesDirectory = new DirectoryInfo(Path.Combine(resourcePath, name + ".SourceCode"));
				if (!currentResourcesDirectory.Exists) {
					break;
				}
				var countLines = 0;
				foreach (var resourceFile in currentResourcesDirectory.GetFiles("*.xml")) {
					var currentCount = File.ReadAllLines(resourceFile.FullName).Length;
					countLines = countLines > currentCount ? countLines : currentCount;
				}
				if (countLines < 9) {
					currentResourcesDirectory.Delete(true);
					Directory.Delete(Path.Combine(schemasPath, name), true);
				} else {
					File.WriteAllText(Path.Combine(schemasPath, name, file.Name), string.Empty);
				}
			}
		}

		private static void CreateProjectInfo(string path, string name, List<string> fileNames) {
			var filePath = Path.Combine(path, name + "." + "csproj");
			var refs = new List<string>();
			var csFilesPath = Path.Combine(path, "Files", "cs");
			refs = GetRefs(csFilesPath, fileNames);
			var descriptorPath = Path.Combine(path, "descriptor.json");
			string descriptorContent = File.ReadAllText(descriptorPath);
			JsonObject jsonDoc = (JsonObject)JsonObject.Parse(descriptorContent);
			string maintainer = jsonDoc["Descriptor"]["Maintainer"];
			var depends = new List<string>();
			foreach (var depend in jsonDoc["Descriptor"]["DependsOn"]) {
				var curName = depend.ToString().Split("\": \"")[1].Split("\"")[0];
				depends.Add(curName);
			}
			CreateFromTpl(GetTplPath(BpmPkg.EditProjTpl), filePath, name, fileNames, maintainer, refs, depends);
			var propertiesDirPath = Path.Combine(path, "Properties");
			Directory.CreateDirectory(propertiesDirPath);
			var propertiesFilePath = Path.Combine(propertiesDirPath, "AssemblyInfo.cs");
			CreateFromTpl(GetTplPath(BpmPkg.AssemblyInfoTpl), propertiesFilePath, name, new List<string>(), maintainer, refs, depends);
			var packagesConfigFilePath = Path.Combine(path, "packages.config");
			CreateFromTpl(GetTplPath(BpmPkg.PackageConfigTpl), packagesConfigFilePath, name, new List<string>(), maintainer, refs, depends);
		}

		private static List<string> GetRefs(string path, List<string> files) {
			List<string> result = new List<string>();
			List<string> refs = new List<string>();
			foreach (var fileName in files) {
				refs = GetRefFromFile(Path.Combine(path, fileName));
				foreach (var line in refs) {
					if (!result.Contains(line)) {
						result.Add(line);
					}
				}
			}

			return result;
		}


		private static List<string> GetRefFromFile(string path) {
			List<string> result = new List<string>();
			string line;
			try {
				StreamReader sr = new StreamReader(path);
				line = sr.ReadLine();
				line = sr.ReadLine();
				line = sr.ReadLine();
				while (line != null) {
					if (line.Contains("=") || line.Contains(":")) {
						line = sr.ReadLine();
						continue;
					}
					if (line.Contains("{") || line.Contains("}") || line.Contains("[") || line.Contains("]")) {
						break;
					}
					if (line.ToLower().Contains("using")) {
						line = line.Replace("\t", string.Empty).Replace("using ", string.Empty).Replace(";", string.Empty)
							.Replace(" ", string.Empty);
					} else {
						line = sr.ReadLine();
						continue;
					}
					result.Add(line);
					line = sr.ReadLine();
				}
				sr.Close();
			} catch (Exception) {
				return new List<string>();
			}
			return result;
		}

		private static void CreateFromTpl(string tplPath, string filePath, string packageName, List<string> fileNames, string maintainer, List<string> refs, List<string> deps) {
			var text = ReplaceMacro(File.ReadAllText(tplPath), packageName, fileNames, maintainer, refs, deps);
			var file = new FileInfo(filePath);
			using (StreamWriter sw = file.CreateText()) {
				sw.Write(text);
			}
		}

		private static string ReplaceMacro(string text, string packageName, List<string> fileNames, string maintainer, List<string> refs, List<string> deps) {
			return text.Replace("$safeprojectname$", packageName)
				.Replace("$userdomain$", maintainer)
				.Replace("$guid1$", Guid.NewGuid().ToString())
				.Replace("$year$", DateTime.Now.Year.ToString())
				.Replace("$modifiedon$", ToJsonMsDate(DateTime.Now))
				.Replace("$ref1$", GetRefPaths(refs))
				.Replace("$files$", GetFilesPaths(fileNames))
				.Replace("$projects$", GetProjectsPath(deps));
		}

		private static string GetProjectsPath(List<string> deps) {
			var template = "\t<ProjectReference Include=\"..\\{0}\\{0}.csproj\"><Name>{0}</Name></ProjectReference>" + Environment.NewLine + "\t";
			var result = string.Empty;
			foreach (var dep in deps) {
				result += string.Format(template, dep);
			}
			return result;
		}

		private static string GetFilesPaths(List<string> fileNames) {
			var template = "<Compile Include=\"Files\\cs\\{0}\" />" + Environment.NewLine + "\t";
			var result = string.Empty;
			foreach (var fileName in fileNames) {
				result += string.Format(template, fileName);
			}
			return result;
		}

		private static string GetRefPaths(List<string> refs) {
			string template = "\t<Reference Include=\"{0}\" />" + Environment.NewLine;
			string result = string.Empty;
			foreach (var _ref in refs) {
				result += string.Format(template, _ref);
			}
			return result;
		}

		private static string GetPathFromEnvironment() {
			string[] cliPath = (Environment.GetEnvironmentVariable("PATH")?.Split(';'));
			return cliPath?.First(p => p.Contains("bpmcli"));
		}

		private static string GetTplPath(string tplPath) {
			string fullPath = tplPath;
			if (File.Exists(tplPath)) {
				fullPath = tplPath;
			}  else { 
				var envPath = GetPathFromEnvironment();
				fullPath = Path.Combine(envPath, tplPath);
			}
			return fullPath;
		}

		private static string ToJsonMsDate(DateTime date) {
			var microsoftDateFormatSettings = new JsonSerializerSettings {
				DateFormatHandling = DateFormatHandling.MicrosoftDateFormat
			};
			return JsonConvert.SerializeObject(date, microsoftDateFormatSettings).Replace("\"", "").Replace("\\", "");
		}

		private static List<string> MoveFiles(string schemasPath, string filesPath, string extension) {
			List<string> fileNames = new List<string>();
			DirectoryInfo dir = new DirectoryInfo(schemasPath);
			foreach (var schemaDirectory in dir.GetDirectories()) {
				foreach (var file in schemaDirectory.GetFiles(extension)) {
					var destFilePath = Path.Combine(filesPath, file.Name);
					if (File.Exists(destFilePath)) {
						File.Delete(destFilePath);
					}
					fileNames.Add(file.Name);
					file.MoveTo(destFilePath);
				}
			}
			return fileNames;
		}
	}
}
