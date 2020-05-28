using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Clio.Common;
using Clio.Package;

namespace Clio.Project.NuGet
{

	#region Class: NuGetManager

	public class NuGetManager : INuGetManager
	{

		#region Fields: Private

		private readonly INuspecFilesGenerator _nuspecFilesGenerator;
		private readonly INugetPacker _nugetPacker;
		private readonly INugetPackageRestorer _nugetPackageRestorer;
		private readonly INugetPackagesProvider _nugetPackagesProvider;
		private readonly IPackageInfoProvider _packageInfoProvider;
		private readonly IApplicationPackageListProvider _applicationPackageListProvider;
		private readonly IPackageArchiver _packageArchiver;
		private readonly IDotnetExecutor _dotnetExecutor;
		private readonly IFileSystem _fileSystem;
		private readonly ILogger _logger;
		private readonly IEnumerable<string> _isNotEmptyPackageInfoFields = new[] {
			nameof(PackageInfo.Descriptor.Name),
			nameof(PackageInfo.Descriptor.Maintainer),
			nameof(PackageInfo.Descriptor.PackageVersion)
		};

		#endregion

		#region Constructors: Public

		public NuGetManager(INuspecFilesGenerator nuspecFilesGenerator, INugetPacker nugetPacker, 
				INugetPackageRestorer nugetPackageRestorer, INugetPackagesProvider nugetPackagesProvider, 
				IPackageInfoProvider packageInfoProvider, 
				IApplicationPackageListProvider applicationPackageListProvider,
				IPackageArchiver packageArchiver, IDotnetExecutor dotnetExecutor, IFileSystem fileSystem, 
				ILogger logger) {
			nuspecFilesGenerator.CheckArgumentNull(nameof(nuspecFilesGenerator));
			nugetPacker.CheckArgumentNull(nameof(nugetPacker));
			nugetPackageRestorer.CheckArgumentNull(nameof(nugetPackageRestorer));
			nugetPackagesProvider.CheckArgumentNull(nameof(nugetPackagesProvider));
			packageInfoProvider.CheckArgumentNull(nameof(packageInfoProvider));
			applicationPackageListProvider.CheckArgumentNull(nameof(applicationPackageListProvider));
			packageArchiver.CheckArgumentNull(nameof(packageArchiver));
			dotnetExecutor.CheckArgumentNull(nameof(dotnetExecutor));
			fileSystem.CheckArgumentNull(nameof(fileSystem));
			logger.CheckArgumentNull(nameof(logger));
			_nuspecFilesGenerator = nuspecFilesGenerator;
			_nugetPacker = nugetPacker;
			_nugetPackageRestorer = nugetPackageRestorer;
			_nugetPackagesProvider = nugetPackagesProvider;
			_packageInfoProvider = packageInfoProvider;
			_applicationPackageListProvider = applicationPackageListProvider;
			_packageArchiver = packageArchiver;
			_dotnetExecutor = dotnetExecutor;
			_fileSystem = fileSystem;
			_logger = logger;
		}

		#endregion

		#region Methods: Private

		private static void CheckPackArguments(string packagePath, IEnumerable<PackageDependency> dependencies) {
			packagePath.CheckArgumentNullOrWhiteSpace(nameof(packagePath));
			dependencies.CheckArgumentNull(nameof(dependencies));
		}

		private static void CheckPushArguments(string nupkgFilePath, string apiKey, string nugetSourceUrl) {
			nupkgFilePath.CheckArgumentNullOrWhiteSpace(nameof(nupkgFilePath));
			apiKey.CheckArgumentNullOrWhiteSpace(nameof(apiKey));
			nugetSourceUrl.CheckArgumentNullOrWhiteSpace(nameof(nugetSourceUrl));
		}

		private void CheckDependencies(IEnumerable<PackageDependency> dependencies, 
				IEnumerable<PackageDependency> packageDependencies) {
			StringBuilder sb = null;
			foreach (PackageDependency dependencyInfo in dependencies) {
				if (!packageDependencies.Contains(dependencyInfo)) {
					if (sb == null) {
						sb = new StringBuilder();
						sb.Append("The following dependencies do not exist in the package descriptor:");
					}
					sb.Append($" {dependencyInfo.Name}:{dependencyInfo.PackageVersion};");
				}
			}
			if (sb != null) {
				throw new ArgumentException(sb.ToString());
			}
		}

		private void CheckEmptyFieldPackageInfo(PackageInfo packageInfo, string fieldName) {
			string fieldValue = (string)packageInfo.Descriptor
				.GetType()
				.GetProperty(fieldName)
				.GetValue(packageInfo.Descriptor);
			if (string.IsNullOrWhiteSpace(fieldValue)) {
				throw new InvalidOperationException(
					$"Field: '{fieldName}' must not be empty in package descriptor: '{packageInfo.PackageDescriptorPath}'");
			}
		}

		private void CheckEmptyFieldsPackageInfo(PackageInfo packageInfo) {
			foreach (string isNotEmptyPackageInfoField in _isNotEmptyPackageInfoFields) {
				CheckEmptyFieldPackageInfo(packageInfo, isNotEmptyPackageInfoField);
			}
		}

		private  IEnumerable<PackageDependency> SetEmptyUIdDependencies(IEnumerable<PackageDependency> dependencies) {
			return dependencies.Select(dependency => {
				dependency.UId = string.Empty;
				return dependency;
			});

		}

		private static IEnumerable<string> GetApplicationPackagesNamesInNuget(
				IEnumerable<PackageInfo> applicationPackages, IEnumerable<NugetPackage> nugetPackages) {
			IEnumerable<string> applicationPackagesNames = 
				applicationPackages.Select(pkg => pkg.Descriptor.Name);
			IEnumerable<string> nugetPackagesNames = nugetPackages.Select(pkg => pkg.Name);
			return applicationPackagesNames.Intersect(nugetPackagesNames);
		}

		private IEnumerable<PackageForUpdate> GetPackagesForUpdate(IEnumerable<string> applicationPackagesNamesInNuget, 
				IEnumerable<PackageInfo> applicationPackages, IEnumerable<NugetPackage> nugetPackages) {
			var packagesForUpdate = new List<PackageForUpdate>();
			foreach (string applicationPackageNameInNuget in applicationPackagesNamesInNuget) {
				PackageInfo package = applicationPackages
					.First(pkg => pkg.Descriptor.Name == applicationPackageNameInNuget);
				if (!PackageVersion.TryParseVersion(package.Descriptor.PackageVersion,
					out PackageVersion packageVersion)) {
					continue;
				}
				LastVersionNugetPackages lastVersionNugetPackages =
					_nugetPackagesProvider.GetLastVersionPackages(applicationPackageNameInNuget, nugetPackages);
				if (lastVersionNugetPackages == null) {
					continue;
				}
				if (lastVersionNugetPackages.Last.Version > packageVersion) {
					packagesForUpdate.Add(new PackageForUpdate(lastVersionNugetPackages, package));
				}
			}
			return packagesForUpdate;
		}

		#endregion

		#region Methods: Public

		public void Pack(string packagePath, IEnumerable<PackageDependency> dependencies, bool skipPdb, 
				string destinationNupkgDirectory) {
			CheckPackArguments(packagePath, dependencies);
			destinationNupkgDirectory = _fileSystem.GetCurrentDirectoryIfEmpty(destinationNupkgDirectory);
			PackageInfo packageInfo = _packageInfoProvider.GetPackageInfo(packagePath);
			CheckEmptyFieldsPackageInfo(packageInfo);
			IEnumerable<PackageDependency> packagesDependencies = 
				SetEmptyUIdDependencies(packageInfo.Descriptor.DependsOn);
			CheckDependencies(dependencies, packagesDependencies);
			string packedPackagePath = Path.Combine(destinationNupkgDirectory, 
				_packageArchiver.GetPackedPackageFileName(packageInfo.Descriptor.Name));
			string nuspecFilePath = Path.Combine(destinationNupkgDirectory,
				_nuspecFilesGenerator.GetNuspecFileName(packageInfo));
			try {
				_packageArchiver.Pack(packagePath, packedPackagePath, skipPdb, true);
				_nuspecFilesGenerator.Create(packageInfo, dependencies, packedPackagePath, nuspecFilePath);
				_nugetPacker.Pack(nuspecFilePath, destinationNupkgDirectory);
			}
			finally {
				_fileSystem.DeleteFileIfExists(nuspecFilePath);
				_fileSystem.DeleteFileIfExists(packedPackagePath);
			}
		}

		public void Push(string nupkgFilePath, string apiKey, string nugetSourceUrl) {
			CheckPushArguments(nupkgFilePath, apiKey, nugetSourceUrl);
			if (!File.Exists(nupkgFilePath)) {
				throw new InvalidOperationException($"Invalid nupkg file path '{nupkgFilePath}'");
			}
			string pushCommand = $"nuget push \"{nupkgFilePath}\" -k {apiKey} -s {nugetSourceUrl}";
			string result = _dotnetExecutor.Execute(pushCommand, true);
			_logger.WriteLine(result);
		}

		public void RestoreToNugetFileStorage(string packageName, string version, string nugetSourceUrl,
				string destinationNupkgDirectory) =>
			_nugetPackageRestorer.RestoreToNugetFileStorage(packageName, version, nugetSourceUrl, destinationNupkgDirectory);

		public void RestoreToDirectory(string packageName, string version, string nugetSourceUrl,
				string destinationNupkgDirectory, bool overwrite) =>
			_nugetPackageRestorer.RestoreToDirectory(packageName, version, nugetSourceUrl, destinationNupkgDirectory, 
				overwrite);

		public void RestoreToPackageStorage(string packageName, string version, string nugetSourceUrl,
			string destinationNupkgDirectory, bool overwrite) =>
			_nugetPackageRestorer.RestoreToPackageStorage(packageName, version, nugetSourceUrl, 
				destinationNupkgDirectory, overwrite);

		public IEnumerable<PackageForUpdate> GetPackagesForUpdate(string nugetSourceUrl) {
			nugetSourceUrl.CheckArgumentNullOrWhiteSpace(nameof(nugetSourceUrl));
			IEnumerable<PackageInfo> applicationPackages = _applicationPackageListProvider.GetPackages();
			IEnumerable<NugetPackage> nugetPackages = _nugetPackagesProvider.GetPackages(nugetSourceUrl);
			IEnumerable<string> applicationPackagesNamesInNuget = 
				GetApplicationPackagesNamesInNuget(applicationPackages, nugetPackages);
			return GetPackagesForUpdate(applicationPackagesNamesInNuget, applicationPackages, nugetPackages);
		}

		#endregion

	}

	#endregion

}
