using Clio.Common;
using Clio.Logger;
using Clio.Requests;
using Clio.UserEnvironment;
using CommandLine;
using FluentValidation;
using FluentValidation.Results;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace Clio.Command
{
	[Verb("reg-web-app", Aliases = new string[] { "reg", "cfg" }, HelpText = "Configure a web application settings")]
	public class RegAppOptions : EnvironmentNameOptions
	{
		[Option('a', "ActiveEnvironment", Required = false, HelpText = "Set as default web application")]
		public string ActiveEnvironment {
			get; set;
		}

		[Option("all-from-IIS", Required = false, HelpText = "Register all Creatios from IIS")]
		public bool FromIis {
			get; set;
		}


		[Option("WithLogin", Required = false, HelpText = "Try login after registration")]
		public bool TryLogIn {
			get; set;
		}
	}

	public class RegAppCommand : Command<RegAppOptions>
	{
		private readonly ISettingsRepository _settingsRepository;
		private readonly IApplicationClientFactory _applicationClientFactory;

		public RegAppCommand(ISettingsRepository settingsRepository,
			IApplicationClientFactory applicationClientFactory, ILogger<RegAppCommand> logger)
		{
			_settingsRepository = settingsRepository;
			_applicationClientFactory = applicationClientFactory;
			_logger = logger;
		}

		public override int Execute(RegAppOptions options)
		{
			if (options.FromIis && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				// HACK: AutoFac cannot resolve IISScannerHandler because IISScannerHandler needs RegAppCommand
				// Two types with circular constructor dependencies are not supported. 
				// https://autofac.readthedocs.io/en/latest/advanced/circular-dependencies.html#constructor-constructor-dependencies
				// This is not an issue since we know that the Uri is valid
				IISScannerHandler iis = new IISScannerHandler(_settingsRepository, this);
				iis.Handle(new IISScannerRequest { Content = "clio://IISScannerRequest/?return=registerAll" }, CancellationToken.None);
				return 0;
			}

			if (options.Name.ToLower(CultureInfo.InvariantCulture) == "open")
			{
				_settingsRepository.OpenSettingsFile();
				return 0;
			}
			else
			{
				var environment = new EnvironmentSettings
				{
					Login = options.Login,
					Password = options.Password,
					Uri = options.Uri,
					Maintainer = options.Maintainer,
					Safe = options.SafeValue.HasValue ? options.SafeValue : false,
					IsNetCore = options.IsNetCore ?? false,
					DeveloperModeEnabled = options.DeveloperModeEnabled,
					ClientId = options.ClientId,
					ClientSecret = options.ClientSecret,
					AuthAppUri = options.AuthAppUri
				};
				if (!string.IsNullOrWhiteSpace(options.ActiveEnvironment))
				{
					if (_settingsRepository.IsEnvironmentExists(options.ActiveEnvironment))
					{
						_settingsRepository.SetActiveEnvironment(options.ActiveEnvironment);
						_logger.LogInfo($"Active environment set to {options.ActiveEnvironment}");
						return 0;
					}
					else
					{
						throw new Exception($"Not found environment {options.ActiveEnvironment} in settings");
					}
				}
				_settingsRepository.ConfigureEnvironment(options.Name, environment);
				_logger.LogInfo($"Environment {options.Name} was configured...");
				environment = _settingsRepository.GetEnvironment(options);
				_logger.LogInfo($"Try login to {environment.Uri} with {environment.Login} credentials ...");

				if (options.TryLogIn)
				{
					IApplicationClient creatioClient = _applicationClientFactory.CreateClient(environment);
					creatioClient.Login();
					_logger.LogInfo($"Login successfull");
				}
				return 0;
			}
		}
	}


	public class RegAppOptionsValidator : AbstractValidator<RegAppOptions>
	{

		public RegAppOptionsValidator()
		{
			RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
			.Custom((value, context) =>
			{
				if (!context.InstanceToValidate.FromIis && string.IsNullOrWhiteSpace(value))
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO1",
						ErrorMessage = "Environment name cannot be empty",
						Severity = Severity.Error
					});
				}
			});

			RuleFor(r => r.Uri).Cascade(CascadeMode.Stop)
			.Custom((value, context) =>
			{
				if (!context.InstanceToValidate.FromIis && !Uri.TryCreate(value, UriKind.Absolute, out _))
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO3",
						ErrorMessage = "Environment Url invalid format",
						Severity = Severity.Error
					});
				}
			});

			RuleFor(r => r.Login).Cascade(CascadeMode.Stop)
			.Custom((value, context) =>
			{
				if (!context.InstanceToValidate.FromIis && string.IsNullOrWhiteSpace(context.InstanceToValidate.ClientId) && string.IsNullOrWhiteSpace(value))
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO3",
						ErrorMessage = "Environment Username cannot be empty",
						Severity = Severity.Error
					});
				}
			});

			RuleFor(r => r.ClientId).Cascade(CascadeMode.Stop)
			.Custom((value, context) =>
			{
				if (!context.InstanceToValidate.FromIis && string.IsNullOrWhiteSpace(context.InstanceToValidate.Login) && string.IsNullOrWhiteSpace(value))
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO4",
						ErrorMessage = "Environment Clientid cannot be empty",
						Severity = Severity.Error
					});
				}
			});



			RuleFor(r => r.Password).Cascade(CascadeMode.Stop)
			.Custom((value, context) =>
			{
				if (!context.InstanceToValidate.FromIis && string.IsNullOrWhiteSpace(context.InstanceToValidate.ClientSecret) && string.IsNullOrWhiteSpace(value))
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO3",
						ErrorMessage = "Environment Password cannot be empty",
						Severity = Severity.Error
					});
				}
			});

			RuleFor(r => r.ClientSecret).Cascade(CascadeMode.Stop)
			.Custom((value, context) =>
			{
				if (!context.InstanceToValidate.FromIis && string.IsNullOrWhiteSpace(context.InstanceToValidate.Login) && string.IsNullOrWhiteSpace(value))
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO4",
						ErrorMessage = "Environment ClientSecret cannot be empty",
						Severity = Severity.Error
					});
				}
			});


		}
	}


}
