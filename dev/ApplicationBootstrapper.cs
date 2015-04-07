﻿#region Usings

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UltimaXNA.Data;
using UltimaXNA.Diagnostics;
using UltimaXNA.Diagnostics.Tracing;
using UltimaXNA.Diagnostics.Tracing.Listeners;
using UltimaXNA.Patterns.IoC;
using UltimaXNA.UltimaData;
using UltimaXNA.Windows.Diagnostics.Tracing.Listeners;

#endregion

namespace UltimaXNA
{
    public sealed class ApplicationBootstrapper
    {
        private bool _isInitialized;

        public ApplicationBootstrapper()
        {
            GeneralExceptionHandler.Instance = new GeneralExceptionHandler();
        }
        
        public string BaseApplicationPath
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        private void ConfigureContainer(IContainer container)
        {
            container.RegisterModule<CoreModule>();
        }

        private void ConfigureTraceListeners()
        {
#if DEBUG
            Tracer.RegisterListener(new ConsoleOutputEventListener());
            Tracer.RegisterListener(new DebugOutputEventListener());
#endif
            Tracer.RegisterListener(new FileLogEventListener("debug.log"));
        }

        private void Configure()
        {
            var container = new Container();

            ConfigureContainer(container);
            ConfigurePlugins(container);
        }

        private void ConfigurePlugins(IContainer container)
        {
            var directory = new DirectoryInfo(Path.Combine(BaseApplicationPath, "plugins"));

            if (!directory.Exists)
            {
                return;
            }

            var assemblies = directory.GetFiles("*.dll");

            foreach (var file in assemblies)
            {
                try
                {
                    Tracer.Info("Loading plugin {0}.", file.Name);

                    var assembly = Assembly.LoadFile(file.FullName);
                    var modules = assembly.GetTypes().Where(t => t.GetInterfaces().Contains(typeof (IModule)));

                    foreach (var module in modules)
                    {
                        Tracer.Info("Activating module {0}.", module.FullName);

                        var instance = (IModule) Activator.CreateInstance(module);

                        instance.Load(container);
                    }
                }
                catch (Exception e)
                {
                    Tracer.Warn(e, "An error occurred while trying to load plugin. [{0}]", file.FullName);
                }
            }
        }

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            if (!Settings.IsSettingsFileCreated)
            {
                Settings.Debug.IsConsoleEnabled = false;
                Settings.Game.AlwaysRun = false;
                Settings.Server.ServerAddress = Settings.Server.ServerAddress;
                Settings.Server.ServerPort = Settings.Server.ServerPort;
                Settings.Server.UserName = "";
                Settings.UltimaOnline.DataDirectory = FileManager.DataPath;
            }

            if (Settings.Debug.IsConsoleEnabled && !NativeMethods.HasConsole)
            {
                ConsoleManager.Show();
            }

            ConfigureTraceListeners();

            try
            {
                StartEngine();
            }
            finally
            {
                if (ConsoleManager.HasConsole)
                {
                    ConsoleManager.Hide();
                }
            }
        }

        private void Prepare()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void StartEngine()
        {
            Prepare();
            Configure();

            using (var engine = new UltimaEngine(800, 600))
            {
                engine.Run();
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            GeneralExceptionHandler.Instance.OnError((Exception) e.ExceptionObject);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            GeneralExceptionHandler.Instance.OnError(e.Exception);
        }
    }
}