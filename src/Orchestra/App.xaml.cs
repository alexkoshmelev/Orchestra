﻿namespace Orchestra
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using Catel.IoC;
    using Catel.Logging;
    using Catel.MVVM;
    using Catel.MVVM.ViewModels;
    using Catel.Reflection;
    using Catel.Windows;
    using Modules;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// The log.
        /// </summary>
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Startup"/> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.StartupEventArgs"/> that contains the event data.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            var appDomain = AppDomain.CurrentDomain;
            appDomain.AssemblyResolve += OnAssemblyResolve;

            StyleHelper.CreateStyleForwardersForDefaultStyles(Current.Resources.MergedDictionaries[1]);

            var serviceLocator = ServiceLocator.Instance;
            Catel.Environment.RegisterDefaultViewModelServices();

            var viewLocator = serviceLocator.ResolveType<IViewLocator>();
            viewLocator.Register(typeof(ProgressNotifyableViewModel), typeof(Views.SplashScreen));

            var viewModelLocator = serviceLocator.ResolveType<IViewModelLocator>();
            viewModelLocator.Register(typeof(Views.SplashScreen), typeof(ProgressNotifyableViewModel));

            var bootstrapper = new OrchestraBootstrapper();
            bootstrapper.RunWithSplashScreen<ProgressNotifyableViewModel>();

            base.OnStartup(e);
        }

        /// <summary>
        /// Called when the resolving of assemblies failed. In that case, this method will try to load the 
        /// assemblies from the modules directory.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="System.ResolveEventArgs"/> instance containing the event data.</param>
        /// <returns>The assembly or <c>null</c> if the assembly could not be resolved.</returns>
        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = TypeHelper.GetAssemblyNameWithoutOverhead(args.Name);

            var files = Directory.GetFiles(ModuleBase.ModulesDirectory, "*.dll", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var fileAssemblyName = fileInfo.Name;
                int extensionStart = fileAssemblyName.LastIndexOf(fileInfo.Extension);
                if (extensionStart > 0)
                {
                    fileAssemblyName = fileAssemblyName.Substring(0, extensionStart);
                }

                if (string.Equals(fileAssemblyName, assemblyName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return ResolveAssemblyAndReferencedAssemblies(file);
                }
            }

            Log.Error("Failed to delay resolve assembly '{0}'", args.Name);
            return null;
        }

        /// <summary>
        /// Resolves the assembly and referenced assemblies.
        /// </summary>
        private Assembly ResolveAssemblyAndReferencedAssemblies(string assemblyFileName)
        {
            Log.Debug("Resolving assembly '{0}' manually", assemblyFileName);

            var appDomain = AppDomain.CurrentDomain;
            var assemblyDirectory = Catel.IO.Path.GetParentDirectory(assemblyFileName);

            // Load references
            var assemblyForReflectionOnly = Assembly.ReflectionOnlyLoadFrom(assemblyFileName);
            foreach (var referencedAssembly in assemblyForReflectionOnly.GetReferencedAssemblies())
            {
                if (!appDomain.GetAssemblies().Any(a => string.CompareOrdinal(a.GetName().Name, referencedAssembly.Name) == 0))
                {
                    // First, try to load from GAC
                    if (referencedAssembly.GetPublicKeyToken() != null)
                    {
                        try
                        {
                            appDomain.Load(referencedAssembly.FullName);
                            continue;
                        }
                        catch (Exception)
                        {
                            Log.Debug("Failed to load assembly '{0}' from GAC, trying local file", referencedAssembly.FullName);
                        }
                    }

                    // Second, try to load from directory
                    var referencedAssemblyPath = Path.Combine(assemblyDirectory, referencedAssembly.Name + ".dll");
                    ResolveAssemblyAndReferencedAssemblies(referencedAssemblyPath);
                }
            }

            // Load assembly itself
            var assembly = Assembly.LoadFrom(assemblyFileName);

            Log.Info("Resolved assembly '{0}' manually", assemblyFileName);

            return assembly;
        }
    }
}