using System.Windows;
using System.IO;
using ArabicDocumentSearch.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArabicDocumentSearch.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private IHost? _host;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArabicDocumentSearch");
		_host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				services.AddArabicSearchInfrastructure(dataRoot);
				services.AddSingleton<SearchViewModel>();
				services.AddSingleton<MainWindow>();
			}).Build();
		await _host.StartAsync();
		var window = _host.Services.GetRequiredService<MainWindow>();
		MainWindow = window;
		window.Show();
	}

	protected override async void OnExit(ExitEventArgs e)
	{
		if (_host is not null) await _host.StopAsync();
		_host?.Dispose();
		base.OnExit(e);
	}
}

