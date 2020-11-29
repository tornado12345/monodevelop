﻿//
// LaunchSettingsTests.cs
//
// Author:
//       josemiguel <jostor@microsoft.com>
//
// Copyright (c) 2019 ${CopyrightHolder}
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System.Linq;
using System.Threading.Tasks;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.AspNetCore.Tests
{
	[TestFixture]
	public class LaunchSettingsTests : IdeTestBase
	{
		#region launchSettings.json sample
		const string LaunchSettings = @"{
						  ""iisSettings"": {
						    ""windowsAuthentication"": false,
						    ""anonymousAuthentication"": true,
						    ""iisExpress"": {
						      ""applicationUrl"": ""http://localhost:54339/"",
						      ""sslPort"": 0
						    }
						  },
						  ""profiles"": {
						    ""IIS Express"": {
						      ""commandName"": ""IISExpress"",
						      ""launchBrowser"": true,
						      ""environmentVariables"": {
						        ""ASPNETCORE_My_Environment"": ""1"",
						        ""ASPNETCORE_DETAILEDERRORS"": ""1"",
						        ""ASPNETCORE_ENVIRONMENT"": ""Staging""
						      }
						    },
						    ""EnvironmentsSample"": {
						      ""commandName"": ""Project"",
						      ""launchBrowser"": true,
						      ""environmentVariables"": {
						        ""ASPNETCORE_ENVIRONMENT"": ""Staging""
						      },
						      ""applicationUrl"": ""http://localhost:54340/""
						    },
						    ""Kestrel Staging"": {
						      ""commandName"": ""Project"",
						      ""launchBrowser"": true,
						      ""environmentVariables"": {
						        ""ASPNETCORE_My_Environment"": ""1"",
						        ""ASPNETCORE_DETAILEDERRORS"": ""1"",
						        ""ASPNETCORE_ENVIRONMENT"": ""Staging""
						      },
						      ""applicationUrl"": ""http://localhost:51997/""
						    }
						  }
						}";
		#endregion

		Solution solution;

		[Test]
		public async Task Checks_Adding_Removing_Profiles ()
		{
			var solutionFileName = Util.GetSampleProject ("aspnetcore-empty-22", "aspnetcore-empty-22.sln");
			solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName);
			var project = (DotNetProject)solution.GetAllProjects ().Single ();

			var launchProfileProvider = new LaunchProfileProvider (project);
			launchProfileProvider.LoadLaunchSettings ();

			Assert.That (launchProfileProvider.ProfilesObject, Is.Not.Null);
			Assert.That (launchProfileProvider.GlobalSettings, Is.Not.Empty);

			Assert.That (launchProfileProvider.Profiles, Has.Count.EqualTo (2));

			launchProfileProvider.Profiles ["Test"] = new LaunchProfileData ();

			launchProfileProvider.SaveLaunchSettings ();

			launchProfileProvider = new LaunchProfileProvider (project);
			launchProfileProvider.LoadLaunchSettings ();

			Assert.That (launchProfileProvider.Profiles, Has.Count.EqualTo (3));
		}

		[Test]
		public async Task RefreshLaunchSettings_returns_expected_Profile ()
		{
			var solutionFileName = Util.GetSampleProject ("aspnetcore-empty-22", "aspnetcore-empty-22.sln");
			solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName);
			var project = (DotNetProject)solution.GetAllProjects ().Single ();

			var launchProfileProvider = new LaunchProfileProvider (project);
			launchProfileProvider.LoadLaunchSettings ();

			Assert.That (launchProfileProvider.ProfilesObject, Is.Not.Null);
			Assert.That (launchProfileProvider.GlobalSettings, Is.Not.Empty);
			Assert.That (project.RunConfigurations, Has.Count.EqualTo (1));

			//modifiying launchSettings.json externally and loading it again
			System.IO.File.WriteAllText (launchProfileProvider.LaunchSettingsJsonPath, LaunchSettings);
			solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName);
			project = (DotNetProject)solution.GetAllProjects ().Single ();

			var config = project.GetDefaultRunConfiguration () as AspNetCoreRunConfiguration;

			Assert.That (config, Is.Not.Null, "GetDefaultRunConfiguration cast to AspNetCoreRunConfiguration is null");
			Assert.That (project.RunConfigurations, Has.Count.EqualTo (3));
			Assert.That (config.CurrentProfile, Is.Not.Null);
		}

		[Test]
		public void ToSerializableForm_Returns_ExpectedValues ()
		{
			var launchSettingsJson = JObject.Parse (LaunchSettings);
			var profiles = launchSettingsJson?.GetValue ("profiles") as JObject;
			var profilesData = LaunchProfileData.DeserializeProfiles (profiles);
			var result = profilesData.ToSerializableForm ();

			Assert.That (result, Is.Not.Null, "ToSerializableForm returned null; expected not null");
			Assert.That (result ["IIS Express"], Has.Count.EqualTo (3), "IIS Express profile expects 3 elements");
			Assert.That (result ["EnvironmentsSample"], Has.Count.EqualTo (4), "EnvironmentsSample profile expects 4 elements");
			Assert.True (result ["EnvironmentsSample"].ContainsKey ("applicationUrl"), "EnvironmentsSample does not contains applicationUrl");
			Assert.That (result ["EnvironmentsSample"] ["applicationUrl"], Is.EqualTo ("http://localhost:54340/"), "EnvironmentsSample applicationUrl is incorrect");
			Assert.That (result ["Kestrel Staging"], Has.Count.EqualTo (4), "Kestrel Staging profile expects 4 elements");
			Assert.True (result ["Kestrel Staging"].ContainsKey ("environmentVariables"), "Kestrel Staging profile does not contains environmentVariables");
			Assert.That (result ["Kestrel Staging"] ["environmentVariables"], Has.Count.EqualTo (3), "Kestrel Staging profile, environmentVariables count is incorrect");
		}

		[Test]
		public void TryGetOtherSettings_Returns_ExpectedValue ()
		{
			var launchSettingsJson = JObject.Parse (LaunchSettings);
			var profiles = launchSettingsJson?.GetValue ("profiles") as JObject;
			var profilesData = LaunchProfileData.DeserializeProfiles (profiles);

			var appUrl = profilesData ["EnvironmentsSample"].TryGetApplicationUrl ();

			Assert.That (appUrl, Is.EqualTo ("http://localhost:54340/"));
		}

		[Test]
		public async Task If_launchSettings_is_emtpy_it_should_return_defaultProfile ()
		{
			var solutionFileName = Util.GetSampleProject ("aspnetcore-empty-22", "aspnetcore-empty-22.sln");
			solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName);
			var project = (DotNetProject)solution.GetAllProjects ().Single ();

			var launchProfileProvider = new LaunchProfileProvider (project);
			launchProfileProvider.LoadLaunchSettings ();

			Assert.That (launchProfileProvider.ProfilesObject, Is.Not.Null);
			Assert.That (launchProfileProvider.GlobalSettings, Is.Not.Empty);

			//modifiying launchSettings.json externally
			System.IO.File.WriteAllText (launchProfileProvider.LaunchSettingsJsonPath, string.Empty);

			var config = project.GetDefaultRunConfiguration () as AspNetCoreRunConfiguration;

			Assert.That (config, Is.Not.Null, "GetDefaultRunConfiguration cast to AspNetCoreRunConfiguration is null");
			Assert.That (config.Name, Is.EqualTo ("Default"));
		}

		[Test]
		public async Task SyncRunConfigurations_syncs_RunConfigs ()
		{
			var solutionFileName = Util.GetSampleProject ("aspnetcore-empty-22", "aspnetcore-empty-22.sln");
			solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName);
			var project = (DotNetProject)solution.GetAllProjects ().Single ();

			project.RunConfigurations.Clear ();
			Assert.That (project.RunConfigurations, Is.Empty);

			var launchProfileProvider = new LaunchProfileProvider (project);
			System.IO.File.WriteAllText (launchProfileProvider.LaunchSettingsJsonPath, LaunchSettings);
			launchProfileProvider.LoadLaunchSettings ();
			launchProfileProvider.SyncRunConfigurations ();

			Assert.That (project.RunConfigurations, Has.Count.EqualTo (2));
			Assert.That (project.RunConfigurations [0].Name, Is.EqualTo ("Kestrel Staging"));
			Assert.False (project.RunConfigurations [0].StoreInUserFile);
			Assert.That (project.RunConfigurations [1].Name, Is.EqualTo ("EnvironmentsSample"));
			Assert.False (project.RunConfigurations [1].StoreInUserFile);
		}

		[Test]
		public async Task CreateProfile_creates_http_only ()
		{
			var solutionFileName = Util.GetSampleProject ("aspnetcore-empty-30", "aspnetcore-empty-30.sln");
			solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName);
			var project = (DotNetProject)solution.GetAllProjects ().Single ();
			var launchProfileProvider = new LaunchProfileProvider (project);
			var launchProfile = launchProfileProvider.CreateDefaultProfile ();
			launchProfile.OtherSettings ["applicationUrl"] = "http://localhost:5000";
		}

		[Test]
		public async Task Project_ports_do_not_clash ()
		{
			var solutionFileName = Util.GetSampleProject ("aspnetcore-two-projects", "aspnetcore-two-projects.sln");
			solution = (Solution)await MonoDevelop.Projects.Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solutionFileName);
			var projects = solution.GetAllProjects ().Cast<DotNetProject> ().ToArray ();
			Assert.That (GetExecutionCommand (projects [0]), Is.EqualTo ("https://localhost:5001;http://localhost:5000"));
			Assert.That (GetExecutionCommand (projects [1]), Is.EqualTo ("https://localhost:5003;http://localhost:5002"));
		}

		static string GetExecutionCommand (DotNetProject project)
		{
			var runConfig = project.RunConfigurations.OfType<AspNetCoreRunConfiguration> ().First ();
			var launchProfileProvider = runConfig.LaunchProfileProvider;
			launchProfileProvider.FixPortNumbers ();
			return launchProfileProvider.DefaultProfile.TryGetApplicationUrl ();
		}

		[TearDown]
		public override void TearDown ()
		{
			solution?.Dispose ();
			solution = null;

			base.TearDown ();
		}
	}
}
