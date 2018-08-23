﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api;
using Tgstation.Server.Client;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public class MainWindowViewModel : ViewModelBase, IDisposable, ICommand, IRequestLogger
	{
		public static string Versions => String.Format(CultureInfo.InvariantCulture, "Version: {0}, API Version: {1}", Assembly.GetExecutingAssembly().GetName().Version, ApiHeaders.Version);

		public static string Meme
		{
			get
			{
				var memes = new List<string>
				{
					"Proudly created by Cyberboss",
					"True Canadian Beer",
					"Brainlet Resistant",
					"Need Milk",
					"Absolute Seperation",
					"Deleting Data Directory..."
				};
				return memes[new Random().Next(memes.Count)];
			}
		}

		public string ConsoleContent { get; private set; }

		public List<ServerViewModel> Connections { get; }

		readonly IServerClientFactory serverClientFactory;
		readonly CancellationTokenSource settingsSaveLoopCts;
		readonly Task settingsSaveLoop;
		readonly UserSettings settings;

		readonly string storageDirectory;
		readonly string settingsPath;

		public event EventHandler CanExecuteChanged;

		public MainWindowViewModel()
		{
			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			serverClientFactory = new ServerClientFactory(new ProductHeaderValue(assemblyName.Name, assemblyName.Version.ToString()));

			var storagePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			const string SettingsFileName = "settings.json";
			storageDirectory = Path.Combine(storagePath, assemblyName.Name);
			settingsPath = Path.Combine(storageDirectory, SettingsFileName);
			settings = LoadSettings().Result;

			settingsSaveLoopCts = new CancellationTokenSource();
			settingsSaveLoop = SettingsSaveLoop(settingsSaveLoopCts.Token);

			Connections = new List<ServerViewModel>(settings.Connections.Select(x => new ServerViewModel(serverClientFactory, x, this)));
			ConsoleContent = "Request details will be shown here...";
		}

		async Task<UserSettings> LoadSettings()
		{
			UserSettings settings = null;
			try
			{
				var settingsFile = await File.ReadAllTextAsync(settingsPath).ConfigureAwait(false);
				settings = JsonConvert.DeserializeObject<UserSettings>(settingsFile);
			}
			catch (IOException) { }
			catch (JsonException) { }

			if (settings == null)
				settings = new UserSettings();
			settings.Connections = settings.Connections ?? new List<Connection>();

#if DEBUG
			if(settings.Connections.Count == 0)
				//load default localhost admin
				settings.Connections.Add(new Connection
				{
					Credentials = new Credentials
					{
						Username = Api.Models.User.AdminName,
						Password = Api.Models.User.DefaultAdminPassword
					},
					Timeout = TimeSpan.FromSeconds(10),
					Url = new Uri("http://localhost:5000")
				});
#endif

			return settings;
		}

		async Task SaveSettings()
		{
			try
			{
				Directory.CreateDirectory(storageDirectory);
				var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
				await File.WriteAllTextAsync(settingsPath, json).ConfigureAwait(false);
			}
			catch (IOException) { }
		}

		async Task SettingsSaveLoop(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
					cancellationToken.ThrowIfCancellationRequested();
					await SaveSettings().ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) { }
		}

		public void Dispose()
		{
			settingsSaveLoopCts.Cancel();
			settingsSaveLoop.GetAwaiter().GetResult();
			SaveSettings().GetAwaiter().GetResult();
		}

		public bool CanExecute(object parameter) => true;

		public void Execute(object parameter)
		{
			//add server command
		}

		public Task LogRequest(HttpRequestMessage requestMessage, CancellationToken cancellationToken)
		{
			lock (this)
				ConsoleContent = String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", ConsoleContent, Environment.NewLine, requestMessage);
			return Task.CompletedTask;
		}

		public Task LogResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
		{
			lock (this)
				ConsoleContent = String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", ConsoleContent, Environment.NewLine, responseMessage);
			return Task.CompletedTask;
		}
	}
}